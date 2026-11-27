using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace JueMingR.Features.Blueprint
{
    // One import owns this sparse graph. JSON is consumed record by record;
    // yields bound work between the shared projection/material worker lanes.
    internal sealed class HelperBlueprintJson
    {
        internal string Name;
        internal int Width, Height;
        private readonly SortedDictionary<long, int> entities = new SortedDictionary<long, int>();
        private readonly SortedDictionary<long, string> containers = new SortedDictionary<long, string>();
        internal readonly SortedDictionary<long, CaptureCell> Cells = new SortedDictionary<long, CaptureCell>();
        internal readonly List<CaptureObject> Objects = new List<CaptureObject>();
        private readonly HashSet<long> tileKeys = new HashSet<long>(), wallKeys = new HashSet<long>(), wireKeys = new HashSet<long>();
        private readonly HashSet<long> containerKeys = new HashSet<long>();
        private static readonly string[] TileFields = { "x", "y", "tileType", "frameX", "frameY", "color", "slope", "isHalfBrick", "inActive", "fullbright", "invisible" };
        private static readonly string[] WallFields = { "x", "y", "wallType", "color", "fullbright", "invisible" };
        private static readonly string[] WireFields = { "x", "y", "red", "blue", "green", "yellow", "actuator" };
        private static long Key(int x, int y) { return ((long)y << 32) | (uint)x; }
        internal IEnumerable<bool> Read(byte[] bytes, IBlueprintImportProfile profile)
        {
            if (profile == null || !profile.Ready) throw Bad("原版物件资料尚未就绪，请稍后导入");
            int bom = bytes.Length >= 3 && bytes[0] == 239 && bytes[1] == 187 && bytes[2] == 191 ? 3 : 0;
            // Strict decoding rejects damaged UTF-8 before the XML JSON reader
            // can normalize malformed sequences. No source bytes are rewritten.
            new UTF8Encoding(false, true).GetCharCount(bytes, bom, bytes.Length - bom);
            var quotas = new XmlDictionaryReaderQuotas { MaxDepth = 64, MaxStringContentLength = 32768, MaxArrayLength = Int32.MaxValue, MaxBytesPerRead = 16384, MaxNameTableCharCount = 65536 };
            using (var stream = new MemoryStream(bytes, bom, bytes.Length - bom, false))
            using (var reader = JsonReaderWriterFactory.CreateJsonReader(stream, Encoding.UTF8, quotas, null))
            {
                reader.MoveToContent(); Type(reader, "object"); reader.ReadStartElement();
                var fields = new HashSet<string>(StringComparer.Ordinal);
                while (reader.MoveToContent() == XmlNodeType.Element)
                {
                    string field = reader.LocalName;
                    if (!fields.Add(field)) throw Bad("根属性重复：" + field);
                    if (field == "tiles" || field == "walls" || field == "wires" || field == "containers" || field == "entities")
                    {
                        if (reader.GetAttribute("type") == "null") { reader.Skip(); yield return true; continue; }
                        Type(reader, "array"); bool empty = reader.IsEmptyElement; reader.ReadStartElement();
                        while (!empty && reader.MoveToContent() == XmlNodeType.Element)
                        {
                            var item = (XElement)XNode.ReadFrom(reader);
                            if ((string)item.Attribute("type") != "null")
                            {
                                if (field == "containers" || field == "entities") PayloadRecord(item, field);
                                else Record(item, field, profile);
                            }
                            yield return true;
                        }
                        if (!empty) reader.ReadEndElement();
                    }
                    else if (field == "name") Name = OptionalString((XElement)XNode.ReadFrom(reader));
                    else if (field == "createdAt")
                    {
                        DateTimeOffset date;
                        if (!DateTimeOffset.TryParse(Scalar((XElement)XNode.ReadFrom(reader), "string"), CultureInfo.InvariantCulture, DateTimeStyles.None, out date)) throw Bad("创建时间无效");
                    }
                    else if (field == "width") Width = Integer((XElement)XNode.ReadFrom(reader), 1, Int32.MaxValue);
                    else if (field == "height") Height = Integer((XElement)XNode.ReadFrom(reader), 1, Int32.MaxValue);
                    else throw Bad("不支持的蓝图属性：" + field);
                    yield return true;
                }
                reader.ReadEndElement(); if (reader.MoveToContent() != XmlNodeType.None) throw Bad("蓝图尾部有额外内容");
                if (!fields.Contains("width") || !fields.Contains("height")) throw Bad("蓝图缺少宽度或高度");
            }
            foreach (CaptureCell cell in Cells.Values)
            {
                if (cell.X >= Width || cell.Y >= Height) throw Bad("坐标超出声明尺寸：" + cell.X + "," + cell.Y);
                yield return true;
            }
            // Empty wire records also require coordinate validation.
            foreach (long key in wireKeys) { if ((uint)key >= Width || (key >> 32) >= Height) throw Bad("线路坐标超出声明尺寸"); yield return true; }
            foreach (long key in containerKeys) { if ((uint)key >= Width || (key >> 32) >= Height) throw Bad("容器坐标超出声明尺寸"); yield return true; }
            if (Cells.Count == 0) throw Bad("没有建筑内容");
            // Enumerate a stable key copy: setting ObjectId changes values, not
            // order. A footprint is proven exclusively against source records.
            var keys = new long[Cells.Count]; Cells.Keys.CopyTo(keys, 0);
            foreach (long key in keys)
            {
                CaptureCell cell = Cells[key];
                if ((cell.Layers & CaptureLayer.Tile) != 0 && cell.ObjectId == 0)
                {
                    BlueprintObjectShape shape = profile.DescribeImport(cell);
                    if (shape != null)
                    {
                        CaptureObject obj = shape.Object;
                        if (!obj.Reliable || !shape.CanMatchMembers || obj.Width < 1 || obj.Height < 1 || (long)obj.Width * obj.Height >= CaptureChunk.MaximumRecords ||
                            obj.X < 0 || obj.Y < 0 || (long)obj.X + obj.Width > Width || (long)obj.Y + obj.Height > Height) throw Bad("物件布局无法确认：" + cell.X + "," + cell.Y);
                        obj.Id = checked(Objects.Count + 1);
                        for (int member = 0; member < obj.Width * obj.Height; member++)
                        {
                            long memberKey = Key(obj.X + member % obj.Width, obj.Y + member / obj.Width); CaptureCell value;
                            if (!Cells.TryGetValue(memberKey, out value) || (value.Layers & CaptureLayer.Tile) == 0 || value.ObjectId != 0 || value.TileType != obj.Type ||
                                !shape.MatchesMemberFrame(value, member)) throw Bad("物件成员残缺或帧冲突：" + obj.X + "," + obj.Y);
                            BlueprintObjectShape description = profile.DescribeImport(value);
                            if (description == null || !Same(obj, description.Object)) throw Bad("物件成员布局不一致：" + obj.X + "," + obj.Y);
                            value.ObjectId = obj.Id; Cells[memberKey] = value; yield return true;
                        }
                        Objects.Add(obj);
                    }
                }
                yield return true;
            }
            foreach (var entry in entities)
            {
                CaptureCell cell;
                if (!Cells.TryGetValue(entry.Key, out cell) || (cell.Layers & CaptureLayer.Tile) == 0 || !BlueprintObjectPayload.EntityType(cell.TileType)) throw Bad("实体记录没有对应锚点");
                var validation = profile as IBlueprintEntityImportProfile;
                if (validation == null || !validation.ValidEntityItem(cell.TileType, entry.Value, cell.FrameY)) throw Bad("实体物品与原版生产者不符");
                cell.Payload.EntityItemType = entry.Value; cell.Payload.EntityState = BlueprintPayloadState.Known; Cells[entry.Key] = cell; yield return true;
            }
            foreach (var entry in containers)
            {
                CaptureCell cell;
                if (!Cells.TryGetValue(entry.Key, out cell) || cell.ObjectId == 0 || !BlueprintObjectPayload.ContainerType(cell.TileType)) throw Bad("容器记录没有完整箱体");
                CaptureObject obj = Objects[cell.ObjectId - 1];
                if (obj.X != cell.X || obj.Y != cell.Y) throw Bad("容器记录不是左上锚点");
                cell.Payload.NameState = BlueprintPayloadState.Known; cell.Payload.Name = entry.Value; Cells[entry.Key] = cell; yield return true;
            }
        }
        private void PayloadRecord(XElement item, string layer)
        {
            string payload = layer == "entities" ? "itemType" : "name";
            if ((string)item.Attribute("type") != "object") throw Bad("附加记录必须是对象");
            var fields = new Dictionary<string, XElement>(StringComparer.Ordinal);
            foreach (XElement value in item.Elements())
            {
                string field = value.Name.LocalName;
                if (field != "x" && field != "y" && field != payload) throw Bad("不支持的附加记录属性：" + field);
                if (fields.ContainsKey(field)) throw Bad("附加记录属性重复：" + field);
                fields.Add(field, value);
            }
            int x = RequiredInteger(fields, "x", 0, Int32.MaxValue), y = RequiredInteger(fields, "y", 0, Int32.MaxValue); long key = Key(x, y);
            if (layer == "entities")
            {
                int value = RequiredInteger(fields, payload, 1, Int16.MaxValue);
                if (entities.ContainsKey(key)) throw Bad("实体锚点重复"); entities.Add(key, value);
            }
            else
            {
                if (!containerKeys.Add(key)) throw Bad("容器锚点重复");
                XElement field;
                string value = fields.TryGetValue(payload, out field) ? OptionalString(field) : null;
                // Helper asks for a name only after Terraria's 20-unit trim.
                // Known empty names authored by R keep their separate meaning.
                if (value != null && value.Length > 20) value = value.Substring(0, Char.IsHighSurrogate(value[19]) ? 19 : 20);
                if (!String.IsNullOrEmpty(value)) containers.Add(key, value);
            }
        }
        private static bool Same(CaptureObject a, CaptureObject b)
        { return b.Reliable && a.Type == b.Type && a.X == b.X && a.Y == b.Y && a.Width == b.Width && a.Height == b.Height && a.Style == b.Style && a.Alternate == b.Alternate && a.Direction == b.Direction && a.OriginX == b.OriginX && a.OriginY == b.OriginY && a.PlacementStyle == b.PlacementStyle && a.RandomVariant == b.RandomVariant; }
        private void Record(XElement item, string layer, IBlueprintImportProfile profile)
        {
            string[] expected = layer == "tiles" ? TileFields : layer == "walls" ? WallFields : WireFields;
            if ((string)item.Attribute("type") != "object") throw Bad("图层记录必须是对象");
            var fields = new Dictionary<string, XElement>(StringComparer.Ordinal);
            foreach (XElement value in item.Elements())
            {
                string field = value.Name.LocalName;
                if (Array.IndexOf(expected, field) < 0) throw Bad("不支持的图层记录属性：" + field);
                if (fields.ContainsKey(field)) throw Bad("记录属性重复：" + field);
                fields.Add(field, value);
            }
            int x = RequiredInteger(fields, "x", 0, Int32.MaxValue), y = RequiredInteger(fields, "y", 0, Int32.MaxValue); long key = Key(x, y);
            HashSet<long> seen = layer == "tiles" ? tileKeys : layer == "walls" ? wallKeys : wireKeys;
            if (!seen.Add(key)) throw Bad("同层坐标重复：" + x + "," + y);
            CaptureCell cell; Cells.TryGetValue(key, out cell); cell.X = x; cell.Y = y;
            if (layer == "tiles")
            {
                cell.TileType = (ushort)RequiredInteger(fields, "tileType", 0, Math.Min(UInt16.MaxValue, profile.TileCount - 1)); cell.Layers |= CaptureLayer.Tile;
                cell.FrameX = (short)RequiredInteger(fields, "frameX", Int16.MinValue, Int16.MaxValue); cell.FrameY = (short)RequiredInteger(fields, "frameY", Int16.MinValue, Int16.MaxValue);
                cell.TilePaint = (byte)DefaultInteger(fields, "color", 31); cell.Slope = (byte)DefaultInteger(fields, "slope", 4); cell.HalfBrick = DefaultBoolean(fields, "isHalfBrick");
                if (cell.HalfBrick && cell.Slope != 0) throw Bad("半砖与斜坡形状冲突");
                cell.Inactive = DefaultBoolean(fields, "inActive"); cell.FullbrightTile = DefaultBoolean(fields, "fullbright"); cell.InvisibleTile = DefaultBoolean(fields, "invisible");
            }
            else if (layer == "walls")
            {
                cell.WallType = (ushort)RequiredInteger(fields, "wallType", 1, Math.Min(UInt16.MaxValue, profile.WallCount - 1)); cell.Layers |= CaptureLayer.Wall;
                cell.WallPaint = (byte)DefaultInteger(fields, "color", 31); cell.FullbrightWall = DefaultBoolean(fields, "fullbright"); cell.InvisibleWall = DefaultBoolean(fields, "invisible");
            }
            else
            {
                cell.Wires = (byte)((DefaultBoolean(fields, "red") ? 1 : 0) | (DefaultBoolean(fields, "blue") ? 2 : 0) | (DefaultBoolean(fields, "green") ? 4 : 0) | (DefaultBoolean(fields, "yellow") ? 8 : 0));
                if (cell.Wires != 0) cell.Layers |= CaptureLayer.Wire;
                cell.Actuator = DefaultBoolean(fields, "actuator"); if (cell.Actuator) cell.Layers |= CaptureLayer.Actuator;
            }
            if (cell.HasContent) Cells[key] = cell;
        }
        private static void Type(XmlReader reader, string type) { if (reader.GetAttribute("type") != type) throw Bad("属性类型无效"); }
        private static string Scalar(XElement value, string type)
        { if ((string)value.Attribute("type") != type || value.HasElements) throw Bad("属性类型无效"); return value.Value; }
        private static string OptionalString(XElement value)
        { return (string)value.Attribute("type") == "null" ? null : Scalar(value, "string"); }
        private static int RequiredInteger(Dictionary<string, XElement> fields, string field, int min, int max)
        { XElement value; if (!fields.TryGetValue(field, out value)) throw Bad("记录缺少：" + field); return Integer(value, min, max); }
        private static int DefaultInteger(Dictionary<string, XElement> fields, string field, int max)
        { XElement value; return fields.TryGetValue(field, out value) ? Integer(value, 0, max) : 0; }
        private static bool DefaultBoolean(Dictionary<string, XElement> fields, string field)
        { XElement value; return fields.TryGetValue(field, out value) && Boolean(value); }
        private static int Integer(XElement value, int min, int max)
        { int result; if (!Int32.TryParse(Scalar(value, "number"), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out result) || result < min || result > max) throw Bad("数值超出支持范围：" + value.Name.LocalName); return result; }
        private static bool Boolean(XElement value)
        { string text = Scalar(value, "boolean"); if (text != "true" && text != "false") throw Bad("布尔值无效"); return text == "true"; }
        internal static InvalidDataException Bad(string reason) { return new InvalidDataException("小助手蓝图：" + reason); }
    }
}

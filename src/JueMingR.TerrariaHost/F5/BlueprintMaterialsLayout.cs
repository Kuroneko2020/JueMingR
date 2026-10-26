using System;
using System.Collections.Generic;
using System.Globalization;
using JueMingR.Features.Blueprint;
using JueMingR.Features.Text;
using Microsoft.Xna.Framework;

namespace JueMingR.TerrariaHost.F5
{
    internal sealed class BlueprintMaterialGlyph
    {
        internal F5Element Element;
        internal Color Ink;
    }
    internal sealed class BlueprintMaterialCell
    {
        internal int ItemType;
        internal string Name;
        internal F5Rect Rect, Icon;
        internal float NaturalHeight;
        internal readonly List<BlueprintMaterialGlyph> Glyphs = new List<BlueprintMaterialGlyph>();
        internal readonly List<BlueprintMaterialGlyph> PendingGlyphs = new List<BlueprintMaterialGlyph>();
    }
    internal sealed class BlueprintMaterialDrawing
    {
        internal BlueprintMaterialCell Cell;
        internal F5Rect Rect, Icon;
        internal readonly List<BlueprintMaterialGlyph> Glyphs = new List<BlueprintMaterialGlyph>();
    }

    // Material values never determine their order: the provider publishes
    // ItemType order. This cache owns only measured rows, not material facts.
    // A changed set preserves the first surviving visible ItemType as its anchor.
    internal sealed class BlueprintMaterialsLayout
    {
        private readonly List<BlueprintMaterialCell> cells = new List<BlueprintMaterialCell>();
        internal readonly List<BlueprintMaterialDrawing> Visible = new List<BlueprintMaterialDrawing>();
        private BlueprintMaterialSnapshot snapshot;
        private float width;
        private bool dynamic;
        private long nameRevision = -1;
        private int resources = -1;
        internal float Height { get; private set; }
        internal int Generation { get; private set; }
        internal int Count { get { return cells.Count; } }
        internal int Columns { get; private set; }
        internal float RequiredX { get; private set; }
        internal float HeldX { get; private set; }
        internal float MissingX { get; private set; }
        internal float NumberWidth { get; private set; }
        internal bool Dynamic { get { return dynamic; } }
        internal bool HoldingPending { get; private set; }
        internal float Prepare(BlueprintMaterialSnapshot value, bool world, float available,
            Func<string, float, F5Size> measure, Func<int, string> name, long names, int resource,
            float currentScroll)
        {
            // Host publication is bounded and can be revoked after this frame's
            // material pass. Empty Pending is no proof that prior ItemTypes
            // disappeared. Keep their measured geometry as unknown placeholders
            // until the same scope publishes rows again; never draw old numbers.
            bool hold = world && value?.Status == BlueprintMaterialStatus.Pending && value.Rows.Count == 0 && snapshot != null && cells.Count > 0 && dynamic == world;
            bool pendingChanged = HoldingPending != hold; HoldingPending = hold;
            if (hold) value = snapshot;
            if (ReferenceEquals(snapshot, value) && dynamic == world && width == available && nameRevision == names && resources == resource)
            { if (pendingChanged) Generation++; return currentScroll; }
            int anchor = 0; float distance = 0;
            var incoming = new HashSet<int>();
            if (value != null) foreach (var row in value.Rows) incoming.Add(row.ItemType);
            // If the first visible material just completed, retain the next
            // surviving row at its old screen offset instead of anchoring to a
            // retired identity. A surviving preceding row is the last fallback.
            foreach (var cell in cells)
                if (incoming.Contains(cell.ItemType))
                { anchor = cell.ItemType; distance = currentScroll - cell.Rect.Y; if (cell.Rect.Bottom > currentScroll) break; }
            var previous = new Dictionary<int, BlueprintMaterialCell>();
            var previousValues = new Dictionary<int, BlueprintMaterialRow>();
            if (dynamic == world && width == available && nameRevision == names && resources == resource)
            {
                foreach (var cell in cells) previous[cell.ItemType] = cell;
                if (snapshot != null) foreach (var row in snapshot.Rows) previousValues[row.ItemType] = row;
            }
            var old = snapshot;
            snapshot = value; dynamic = world; width = available; nameRevision = names; resources = resource;
            cells.Clear(); Visible.Clear(); Height = 0;
            float countWidth = 0;
            if (!world && value != null) foreach (var row in value.Rows)
                countWidth = Math.Max(countWidth, measure(Number(row.Required), .65f).Width + 8);
            Columns = world ? 1 : Math.Max(1, (int)((available + 6) / (Math.Max(62, countWidth) + 6)));
            float cellWidth = world ? available : (available - (Columns - 1) * 6) / Columns;
            // Fixed table columns keep changing quantities from reordering or
            // moving the name column. Oversize decimal strings wrap in-place;
            // no digits are abbreviated, clipped or scaled down.
            NumberWidth = world ? Math.Max(1, (available - Math.Min(170, available * .34f) - 18) / 3) : 0;
            RequiredX = world ? available - NumberWidth * 3 - 12 : 0;
            HeldX = RequiredX + NumberWidth + 4; MissingX = HeldX + NumberWidth + 4;
            int index = 0; float gridTop = 0, gridHeight = 0;
            if (value != null) foreach (var row in value.Rows)
            {
                string itemName = name(row.ItemType) ?? ("物品 " + row.ItemType.ToString(CultureInfo.InvariantCulture));
                BlueprintMaterialCell cell;
                BlueprintMaterialRow prior; previousValues.TryGetValue(row.ItemType, out prior);
                bool reusable = previous.TryGetValue(row.ItemType, out cell) && cell.Rect.Width == cellWidth && cell.Name == itemName && prior != null &&
                    prior.Required == row.Required && (!world || prior.Held == row.Held && prior.Missing == row.Missing &&
                    prior.InventoryReady == row.InventoryReady && old.InventoryReady == value.InventoryReady && old.Status == value.Status);
                if (!reusable) cell = Build(row, itemName, world, cellWidth, measure, value.InventoryReady, value.Status == BlueprintMaterialStatus.Exact);
                else if (!world) SetGridHeight(cell, cell.NaturalHeight);
                float x = world ? 0 : index % Columns * (cellWidth + 6);
                if (!world && index % Columns == 0 && index > 0)
                { for (int i = cells.Count - Columns; i < cells.Count; i++) SetGridHeight(cells[i], gridHeight); gridTop += gridHeight + 6; gridHeight = 0; }
                float top = world ? Height : gridTop;
                float dx = x - cell.Rect.X, dy = top - cell.Rect.Y;
                Offset(cell, dx, dy);
                cells.Add(cell);
                if (world) Height = cell.Rect.Bottom + 4; else gridHeight = Math.Max(gridHeight, cell.Rect.Height);
                index++;
            }
            if (!world && cells.Count > 0)
            { for (int i = (cells.Count - 1) / Columns * Columns; i < cells.Count; i++) SetGridHeight(cells[i], gridHeight); Height = gridTop + gridHeight; }
            Generation++;
            if (anchor != 0) foreach (var cell in cells) if (cell.ItemType == anchor) return Math.Max(0, cell.Rect.Y + distance);
            return Math.Max(0, currentScroll);
        }
        private BlueprintMaterialCell Build(BlueprintMaterialRow row, string name, bool world, float cellWidth,
            Func<string, float, F5Size> measure, bool inventoryReady, bool complete)
        {
            var cell = new BlueprintMaterialCell { ItemType = row.ItemType, Name = name };
            if (!world)
            {
                var count = Wrap(Number(row.Required), Math.Max(1, cellWidth - 8), .65f, measure);
                float textHeight = BlockHeight(count, 2), height = Math.Max(58, 38 + textHeight);
                cell.Rect = new F5Rect(0, 0, cellWidth, height);
                cell.Icon = new F5Rect((cellWidth - 32) / 2, 4, 32, 32);
                Add(cell, count, cellWidth - 4, height - 4 - textHeight, .65f, Color.White, true);
                AddUnknown(cell, cellWidth - 4, height - 4, .65f, measure, true);
            }
            else
            {
                var label = Wrap(name, Math.Max(1, RequiredX - 40), .70f, measure);
                var required = Wrap(Number(row.Required), NumberWidth - 4, .70f, measure);
                var held = Wrap(inventoryReady && row.InventoryReady ? Number(row.Held) : "—", NumberWidth - 4, .70f, measure);
                // A reliable known contribution is not a complete scope. Until
                // all materials and inventory are current, zero here would be
                // an unsupported sufficiency claim for the unknown remainder.
                bool missingReady = complete && inventoryReady && row.InventoryReady;
                var missing = Wrap(missingReady ? Number(row.Missing) : "—", NumberWidth - 4, .70f, measure);
                float height = Math.Max(38, Math.Max(Math.Max(BlockHeight(label, 2), BlockHeight(required, 2)), Math.Max(BlockHeight(held, 2), BlockHeight(missing, 2))) + 8);
                cell.Rect = new F5Rect(0, 0, cellWidth, height); cell.Icon = new F5Rect(0, (height - 32) / 2, 32, 32);
                Add(cell, label, 38, 4, .70f, Color.White, false);
                foreach (var glyph in cell.Glyphs) cell.PendingGlyphs.Add(new BlueprintMaterialGlyph { Element = glyph.Element, Ink = glyph.Ink });
                Add(cell, required, RequiredX + NumberWidth - 4, 4, .70f, Color.White, true);
                Add(cell, held, HeldX + NumberWidth - 4, 4, .70f, Color.White, true);
                Add(cell, missing, MissingX + NumberWidth - 4, 4, .70f,
                    missingReady && row.Missing > 0 ? Color.LightSalmon : Color.White, true);
                AddUnknown(cell, RequiredX + NumberWidth - 4, 4, .70f, measure, false);
                AddUnknown(cell, HeldX + NumberWidth - 4, 4, .70f, measure, false);
                AddUnknown(cell, MissingX + NumberWidth - 4, 4, .70f, measure, false);
            }
            cell.NaturalHeight = cell.Rect.Height; return cell;
        }
        private static void SetGridHeight(BlueprintMaterialCell cell, float height)
        {
            float delta = height - cell.Rect.Height;
            if (delta == 0) return;
            cell.Rect = new F5Rect(cell.Rect.X, cell.Rect.Y, cell.Rect.Width, height);
            foreach (var glyph in cell.Glyphs)
            { var e = glyph.Element; glyph.Element = new F5Element(e.Kind, e.Rect.Offset(0, delta), e.Text, e.TextSize, e.TextScale, e.Command); }
            foreach (var glyph in cell.PendingGlyphs)
            { var e = glyph.Element; glyph.Element = new F5Element(e.Kind, e.Rect.Offset(0, delta), e.Text, e.TextSize, e.TextScale, e.Command); }
        }
        private static void Offset(BlueprintMaterialCell cell, float x, float y)
        {
            if (x == 0 && y == 0) return;
            cell.Rect = cell.Rect.Offset(x, y); cell.Icon = cell.Icon.Offset(x, y);
            for (int i = 0; i < cell.Glyphs.Count; i++)
            { var old = cell.Glyphs[i].Element; cell.Glyphs[i].Element = new F5Element(old.Kind, old.Rect.Offset(x, y), old.Text, old.TextSize, old.TextScale, old.Command); }
            for (int i = 0; i < cell.PendingGlyphs.Count; i++)
            { var old = cell.PendingGlyphs[i].Element; cell.PendingGlyphs[i].Element = new F5Element(old.Kind, old.Rect.Offset(x, y), old.Text, old.TextSize, old.TextScale, old.Command); }
        }
        private static void AddUnknown(BlueprintMaterialCell cell, float right, float y, float scale, Func<string, float, F5Size> measure, bool bottom)
        {
            var size = measure("—", scale);
            cell.PendingGlyphs.Add(new BlueprintMaterialGlyph { Element = new F5Element(F5ElementKind.Text,
                new F5Rect(right - size.Width, bottom ? y - size.Height : y, size.Width, size.Height), "—", size, scale, F5Command.None), Ink = Color.White });
        }
        private static void Add(BlueprintMaterialCell cell, IList<F5Element> lines, float x, float y, float scale, Color ink, bool right)
        {
            foreach (var line in lines)
            {
                var rect = new F5Rect(right ? x - line.TextSize.Width : x, y + line.Rect.Y, line.TextSize.Width, line.TextSize.Height);
                cell.Glyphs.Add(new BlueprintMaterialGlyph { Element = new F5Element(F5ElementKind.Text, rect, line.Text, line.TextSize, scale, F5Command.None), Ink = ink });
            }
        }
        internal void Project(float x, float y, F5Rect clip)
        {
            Visible.Clear();
            int low = 0, high = cells.Count;
            while (low < high) { int mid = (low + high) / 2; if (cells[mid].Rect.Bottom + y <= clip.Y) low = mid + 1; else high = mid; }
            for (int i = low; i < cells.Count; i++)
            {
                var cell = cells[i]; var rect = cell.Rect.Offset(x, y);
                if (rect.Y >= clip.Bottom) break;
                if (rect.Right <= clip.X || rect.X >= clip.Right) continue;
                var draw = new BlueprintMaterialDrawing { Cell = cell, Rect = rect, Icon = cell.Icon.Offset(x, y) };
                foreach (var glyph in HoldingPending ? cell.PendingGlyphs : cell.Glyphs)
                { var e = glyph.Element; draw.Glyphs.Add(new BlueprintMaterialGlyph { Element = new F5Element(e.Kind, e.Rect.Offset(x, y), e.Text, e.TextSize, e.TextScale, e.Command), Ink = glyph.Ink }); }
                Visible.Add(draw);
            }
        }
        internal BlueprintMaterialDrawing Hit(float x, float y, F5Rect clip)
        { if (clip.Contains(x, y)) foreach (var cell in Visible) if (cell.Rect.Contains(x, y)) return cell; return null; }
        internal void Clear() { cells.Clear(); Visible.Clear(); snapshot = null; Height = 0; resources = -1; HoldingPending = false; }
        internal static string Number(long value) { return value.ToString(CultureInfo.InvariantCulture); }
        internal static string Summary(BlueprintMaterialSnapshot value, bool world)
        {
            bool exact = value?.Status == BlueprintMaterialStatus.Exact && (!world || value.InventoryReady);
            return world ? "剩余材料 " + (exact ? value.KindCount.ToString(CultureInfo.InvariantCulture) : "—") + " 种 · 还需 " + (exact ? Number(value.Total) : "—") :
                "材料 " + (exact ? value.KindCount.ToString(CultureInfo.InvariantCulture) : "—") + " 种 · 总计 " + (exact ? Number(value.Total) : "—");
        }
        internal static List<F5Element> Wrap(string text, float available, float scale, Func<string, float, F5Size> measure)
        {
            var result = new List<F5Element>(); var boundaries = TextElements.Boundaries(text ?? ""); int start = 0; float y = 0;
            while (start < boundaries.Length - 1)
            {
                int lo = start + 1, hi = boundaries.Length - 1;
                while (lo < hi) { int mid = (lo + hi + 1) / 2; if (measure(text.Substring(boundaries[start], boundaries[mid] - boundaries[start]), scale).Width <= available) lo = mid; else hi = mid - 1; }
                string part = text.Substring(boundaries[start], boundaries[lo] - boundaries[start]); var size = measure(part, scale);
                if (size.Width > available) throw new InvalidOperationException("材料文字无法以可读字号放入当前窗口");
                result.Add(new F5Element(F5ElementKind.Text, new F5Rect(0, y, size.Width, size.Height), part, size, scale, F5Command.None)); y += size.Height + 2; start = lo;
            }
            return result;
        }
        internal static float BlockHeight(IList<F5Element> lines, float gap)
        { return lines.Count == 0 ? 0 : lines[lines.Count - 1].Rect.Bottom; }
    }
}

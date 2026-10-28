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
        internal bool Name;
    }
    internal sealed class BlueprintMaterialCell
    {
        internal int ItemType;
        internal bool IconCountOnly;
        internal string Name;
        internal F5Rect Rect, Icon;
        internal float NaturalHeight;
        internal List<F5Element> NameLines, RequiredLines, HeldLines, MissingLines;
        internal string RequiredText, HeldText, MissingText;
        internal F5Size UnknownSize;
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
        private bool namedGrid;
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
        // The save workspace retains its compact icon/count grid and hover
        // names; standalone template details share the named list geometry.
        internal bool ShowNames { get; set; } = true;
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
            if (ReferenceEquals(snapshot, value) && dynamic == world && namedGrid == ShowNames && width == available && nameRevision == names && resources == resource)
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
            bool compatible = dynamic == world && namedGrid == ShowNames && width == available && nameRevision == names && resources == resource;
            if (compatible)
            {
                foreach (var cell in cells) previous[cell.ItemType] = cell;
                if (snapshot != null) foreach (var row in snapshot.Rows) previousValues[row.ItemType] = row;
            }
            var old = snapshot;
            bool sameSet = compatible && SameSet(old, value);
            float oldNumberWidth = NumberWidth;
            if (world)
            {
                // Reserve readable name space and keep a scope's number budget
                // after digits shrink. Only changed values need new metrics.
                float maximum = Math.Max(1, Math.Min(96, (available - 150 - 24) / 3));
                float desired = sameSet ? NumberWidth : Math.Max(48, Math.Max(measure("还需", .70f).Width,
                    Math.Max(measure("持有", .70f).Width, measure("缺少", .70f).Width)));
                if (value != null) foreach (var row in value.Rows)
                {
                    BlueprintMaterialRow prior; previousValues.TryGetValue(row.ItemType, out prior);
                    if (prior == null || prior.Required != row.Required) desired = Math.Max(desired, measure(Number(row.Required), .70f).Width);
                    if (prior == null || prior.Held != row.Held) desired = Math.Max(desired, measure(Number(row.Held), .70f).Width);
                    if (prior == null || prior.Missing != row.Missing) desired = Math.Max(desired, measure(Number(row.Missing), .70f).Width);
                }
                NumberWidth = Math.Min(maximum, desired);
            }
            else NumberWidth = 0;
            snapshot = value; dynamic = world; namedGrid = ShowNames; width = available; nameRevision = names; resources = resource;
            cells.Clear(); Visible.Clear(); Height = 0;
            Columns = world ? 1 : Math.Max(1, (int)((available + 6) / ((ShowNames ? 202 : 44) + 6)));
            float cellWidth = world ? available : ShowNames ? (available - (Columns - 1) * 6) / Columns : Math.Min(44, available);
            RequiredX = world ? available - NumberWidth * 3 - 16 : 0;
            HeldX = RequiredX + NumberWidth + 8; MissingX = HeldX + NumberWidth + 8;
            int index = 0; float gridTop = 0, gridHeight = 0;
            if (value != null) foreach (var row in value.Rows)
            {
                string itemName = name(row.ItemType) ?? ("物品 " + row.ItemType.ToString(CultureInfo.InvariantCulture));
                BlueprintMaterialCell cell;
                BlueprintMaterialRow prior; previousValues.TryGetValue(row.ItemType, out prior);
                bool existed = previous.TryGetValue(row.ItemType, out cell);
                float minimumHeight = sameSet && existed ? cell.NaturalHeight : 0;
                bool reusable = existed && oldNumberWidth == NumberWidth && cell.Rect.Width == cellWidth && cell.Name == itemName && prior != null &&
                    prior.Required == row.Required && (!world || prior.Held == row.Held && prior.Missing == row.Missing &&
                    prior.InventoryReady == row.InventoryReady && old.InventoryReady == value.InventoryReady && old.Status == value.Status);
                var measured = existed && oldNumberWidth == NumberWidth && cell.Rect.Width == cellWidth && cell.Name == itemName ? cell : null;
                if (!reusable) cell = Build(row, itemName, world, cellWidth, measure, value.InventoryReady, value.Status == BlueprintMaterialStatus.Exact, minimumHeight, measured);
                else if (!world) SetGridHeight(cell, cell.NaturalHeight);
                float x = world ? 0 : index % Columns * (cellWidth + 6);
                if (!world && index % Columns == 0 && index > 0)
                { for (int i = cells.Count - Columns; i < cells.Count; i++) SetGridHeight(cells[i], gridHeight); gridTop += gridHeight + 6; gridHeight = 0; }
                float top = world ? Height : gridTop;
                float dx = x - cell.Rect.X, dy = top - cell.Rect.Y;
                Offset(cell, dx, dy);
                cells.Add(cell);
                if (world) Height = cell.Rect.Bottom; else gridHeight = Math.Max(gridHeight, cell.Rect.Height);
                index++;
            }
            if (!world && cells.Count > 0)
            { for (int i = (cells.Count - 1) / Columns * Columns; i < cells.Count; i++) SetGridHeight(cells[i], gridHeight); Height = gridTop + gridHeight; }
            Generation++;
            if (anchor != 0) foreach (var cell in cells) if (cell.ItemType == anchor) return Math.Max(0, cell.Rect.Y + distance);
            return Math.Max(0, currentScroll);
        }
        private static bool SameSet(BlueprintMaterialSnapshot a, BlueprintMaterialSnapshot b)
        {
            if (a == null || b == null || a.Rows.Count != b.Rows.Count) return false;
            for (int i = 0; i < a.Rows.Count; i++) if (a.Rows[i].ItemType != b.Rows[i].ItemType) return false;
            return true;
        }
        private BlueprintMaterialCell Build(BlueprintMaterialRow row, string name, bool world, float cellWidth,
            Func<string, float, F5Size> measure, bool inventoryReady, bool complete, float minimumHeight, BlueprintMaterialCell prior)
        {
            var cell = new BlueprintMaterialCell { ItemType = row.ItemType, Name = name };
            if (world || !ShowNames) cell.UnknownSize = prior?.UnknownSize ?? measure("—", world ? .70f : .65f);
            if (!world && !ShowNames)
            {
                cell.IconCountOnly = true;
                cell.RequiredText = Number(row.Required);
                var count = cell.RequiredLines = Reuse(cell.RequiredText, prior?.RequiredText, prior?.RequiredLines, Math.Max(1, Math.Min(32, cellWidth - 8)), .65f, measure);
                float textHeight = BlockHeight(count, 2), height = count.Count > 1 ? 47 + textHeight : 44;
                cell.Rect = new F5Rect(0, 0, cellWidth, height);
                // Invisible inventory-sized slots keep icons on one baseline.
                // Renderer uses the prepared native frame's drawn footprint to
                // attach the count; full long integers retain a lower text block.
                cell.Icon = new F5Rect((cellWidth - 36) / 2, 4, 36, 36);
                Add(cell, count, cellWidth - 6, count.Count > 1 ? 43 : 38 - textHeight, .65f, Color.White, true);
                AddUnknown(cell, cellWidth - 4, height - 4, .65f, cell.UnknownSize, true);
            }
            else if (!world)
            {
                var label = cell.NameLines = prior?.NameLines ?? Wrap(name, Math.Max(1, cellWidth - 46), .70f, measure);
                cell.RequiredText = Number(row.Required);
                var count = cell.RequiredLines = Reuse(cell.RequiredText, prior?.RequiredText, prior?.RequiredLines, 32, .65f, measure);
                bool below = count.Count > 1;
                float countHeight = BlockHeight(count, 2), groupHeight = below ? 34 + countHeight : 32;
                float height = Math.Max(minimumHeight, Math.Max(groupHeight, BlockHeight(label, 2)) + 8);
                cell.Rect = new F5Rect(0, 0, cellWidth, height);
                float iconTop = (height - groupHeight) / 2;
                cell.Icon = new F5Rect(4, iconTop, 32, 32);
                Add(cell, label, 42, (height - BlockHeight(label, 2)) / 2, .70f, Color.White, false, true);
                Add(cell, count, 36, below ? iconTop + 34 : iconTop + 32 - countHeight, .65f, Color.White, true);
            }
            else
            {
                // These local measured blocks are independent of the row's
                // projected origin and height. A numeric refresh reuses the
                // name and every unchanged value instead of measuring them.
                var label = cell.NameLines = prior?.NameLines ?? Wrap(name, Math.Max(1, RequiredX - 38), .70f, measure);
                cell.RequiredText = Number(row.Required);
                cell.HeldText = inventoryReady && row.InventoryReady ? Number(row.Held) : "—";
                var required = cell.RequiredLines = Reuse(cell.RequiredText, prior?.RequiredText, prior?.RequiredLines, NumberWidth, .70f, measure);
                var held = cell.HeldLines = Reuse(cell.HeldText, prior?.HeldText, prior?.HeldLines, NumberWidth, .70f, measure);
                // A reliable known contribution is not a complete scope. Until
                // all materials and inventory are current, zero here would be
                // an unsupported sufficiency claim for the unknown remainder.
                bool missingReady = complete && inventoryReady && row.InventoryReady;
                cell.MissingText = missingReady ? Number(row.Missing) : "—";
                var missing = cell.MissingLines = Reuse(cell.MissingText, prior?.MissingText, prior?.MissingLines, NumberWidth, .70f, measure);
                float height = Math.Max(minimumHeight, Math.Max(24, Math.Max(Math.Max(BlockHeight(label, 2), BlockHeight(required, 2)), Math.Max(BlockHeight(held, 2), BlockHeight(missing, 2)))) + 6);
                cell.Rect = new F5Rect(0, 0, cellWidth, height); cell.Icon = new F5Rect(0, (height - 24) / 2, 24, 24);
                Add(cell, label, 30, (height - BlockHeight(label, 2)) / 2, .70f, Color.White, false, true);
                foreach (var glyph in cell.Glyphs) cell.PendingGlyphs.Add(new BlueprintMaterialGlyph { Element = glyph.Element, Ink = glyph.Ink, Name = true });
                Add(cell, required, RequiredX + NumberWidth, (height - BlockHeight(required, 2)) / 2, .70f, Color.White, true);
                Add(cell, held, HeldX + NumberWidth, (height - BlockHeight(held, 2)) / 2, .70f, Color.White, true);
                Add(cell, missing, MissingX + NumberWidth, (height - BlockHeight(missing, 2)) / 2, .70f,
                    missingReady && row.Missing > 0 ? Color.LightSalmon : Color.White, true);
                float unknownY = (height - cell.UnknownSize.Height) / 2;
                AddUnknown(cell, RequiredX + NumberWidth, unknownY, .70f, cell.UnknownSize, false);
                AddUnknown(cell, HeldX + NumberWidth, unknownY, .70f, cell.UnknownSize, false);
                AddUnknown(cell, MissingX + NumberWidth, unknownY, .70f, cell.UnknownSize, false);
            }
            cell.NaturalHeight = cell.Rect.Height; return cell;
        }
        private static List<F5Element> Reuse(string text, string oldText, List<F5Element> measured, float width, float scale, Func<string, float, F5Size> measure)
        { return measured != null && text == oldText ? measured : Wrap(text, width, scale, measure); }
        private static void SetGridHeight(BlueprintMaterialCell cell, float height)
        {
            // Natural blocks are centered independently. Grow around that
            // center, keeping the icon/count group together with its anchors.
            float delta = height - cell.Rect.Height;
            if (delta == 0) return;
            cell.Rect = new F5Rect(cell.Rect.X, cell.Rect.Y, cell.Rect.Width, height);
            if (cell.IconCountOnly) return;
            cell.Icon = cell.Icon.Offset(0, delta / 2);
            foreach (var glyph in cell.Glyphs)
            { var e = glyph.Element; glyph.Element = new F5Element(e.Kind, e.Rect.Offset(0, delta / 2), e.Text, e.TextSize, e.TextScale, e.Command); }
            foreach (var glyph in cell.PendingGlyphs)
            { var e = glyph.Element; glyph.Element = new F5Element(e.Kind, e.Rect.Offset(0, delta / 2), e.Text, e.TextSize, e.TextScale, e.Command); }
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
        private static void AddUnknown(BlueprintMaterialCell cell, float right, float y, float scale, F5Size size, bool bottom)
        {
            cell.PendingGlyphs.Add(new BlueprintMaterialGlyph { Element = new F5Element(F5ElementKind.Text,
                new F5Rect(right - size.Width, bottom ? y - size.Height : y, size.Width, size.Height), "—", size, scale, F5Command.None), Ink = Color.White });
        }
        private static void Add(BlueprintMaterialCell cell, IList<F5Element> lines, float x, float y, float scale, Color ink, bool right, bool name = false)
        {
            foreach (var line in lines)
            {
                var rect = new F5Rect(right ? x - line.TextSize.Width : x, y + line.Rect.Y, line.TextSize.Width, line.TextSize.Height);
                cell.Glyphs.Add(new BlueprintMaterialGlyph { Element = new F5Element(F5ElementKind.Text, rect, line.Text, line.TextSize, scale, F5Command.None), Ink = ink, Name = name });
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
                { var e = glyph.Element; draw.Glyphs.Add(new BlueprintMaterialGlyph { Element = new F5Element(e.Kind, e.Rect.Offset(x, y), e.Text, e.TextSize, e.TextScale, e.Command), Ink = glyph.Ink, Name = glyph.Name }); }
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

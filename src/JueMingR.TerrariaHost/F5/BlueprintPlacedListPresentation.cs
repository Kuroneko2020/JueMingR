using System;
using System.Collections.Generic;
using System.Globalization;
using JueMingR.Features.Blueprint;
using JueMingR.TerrariaHost.Blueprint;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameInput;

namespace JueMingR.TerrariaHost.F5
{
    // A lightweight projection of the one committed scene. Only directory or
    // font/width changes measure all names. Stable frames project visible rows;
    // neither opening nor scrolling requests body, world or persistence work.
    internal sealed class BlueprintPlacedListPresentation : IDisposable
    {
        private sealed class Row
        {
            internal BlueprintMaterialInstance Instance;
            internal float Top, Height, RelativeY;
            internal readonly List<BlueprintLibraryPart> Parts = new List<BlueprintLibraryPart>();
            internal BlueprintLibraryPart Materials, Remove, Relative;
        }
        private readonly HostBlueprint host;
        private readonly F5Interaction shell;
        private readonly BlueprintLibraryRenderer renderer = new BlueprintLibraryRenderer();
        private readonly List<Row> rows = new List<Row>();
        private readonly List<BlueprintLibraryPart> visible = new List<BlueprintLibraryPart>();
        private IReadOnlyList<BlueprintMaterialInstance> directory;
        private readonly Func<string, float, F5Size> measure;
        private BlueprintLibraryPart back, hovered, armed;
        private long session, pressedSession, pressedInstance, generation, pressedGeneration, drawnGeneration = -1;
        private int resources = -1, layout = -1, firstVisible = -1, lastVisible = -1, playerX = int.MinValue, playerY = int.MinValue;
        private float measuredWidth, contentHeight, scroll = -1, savedScroll, headerHeight = 44;
        private F5Rect view, hitClip;
        private Matrix matrix;
        private Vector2 screen;
        private bool ready, leftTail, rightTail, previousLeft = true, previousRight = true;
        private string shownRemoveReason;
        internal bool Opened { get; private set; }
        internal bool Visible { get { return Opened && shell.Visible && shell.Page == 6; } }
        internal bool OwnsPointer { get; private set; }
        internal bool ConsumeLeft { get; private set; }
        internal bool ConsumeRight { get; private set; }
        internal bool ConsumeWheel { get; private set; }
#if DEBUG
        internal long DirectoryBuilds, RelativeUpdates, VisibleRowVisits;
#endif
        internal BlueprintPlacedListPresentation(HostBlueprint host, F5Interaction shell)
        { this.host = host; this.shell = shell; measure = shell.Layout.DynamicTextSize; }
        internal void Open()
        {
            Close(); Opened = true; session = host.UiSession; shell.ScrollTo(0);
            host.Input.Hotkeys.SuppressHeld(); host.Input.ConsumeHotkeyActions();
        }
        internal void Close()
        {
            Opened = ready = OwnsPointer = false; armed = hovered = null;
            rows.Clear(); visible.Clear(); directory = null; firstVisible = lastVisible = -1;
            renderer.Dispose(); resources = layout = -1; generation++; drawnGeneration = -1;
            // Visibility/focus do not prove physical release. The shared owner
            // and these tails consume it later, even after the last row retires.
        }
        internal void PauseForMaterials() { armed = hovered = null; savedScroll = shell.Scroll; ready = false; drawnGeneration = -1; }
        internal void ResumeAfterMaterials() { if (Visible) { shell.ScrollTo(savedScroll); ready = false; drawnGeneration = -1; } }
        private void CheckSession()
        { if (Opened && (!host.HasSession || session != host.UiSession)) Close(); }
        internal void ProcessInput(bool active, KeyboardState keys, Vector2 point, bool geometryCurrent, bool focused, bool blocked)
        {
            CheckSession(); bool left = host.Input.RawMouseLeft, right = host.Input.RawMouseRight;
            ConsumeLeft = leftTail; ConsumeRight = rightTail; ConsumeWheel = OwnsPointer = false;
            bool current = active && focused && Visible && ready && !blocked && geometryCurrent &&
                host.Input.CanStartActions && drawnGeneration == generation && renderer.ResourcesCurrent &&
                matrix == Main.UIScaleMatrix && screen == PlayerInput.OriginalScreenSize && layout == shell.Layout.Generation &&
                scroll == shell.Scroll && F5PopupPlacement.SameAnchor(view, shell.Layout.Viewport.Offset(shell.X, shell.Y)) &&
                ReferenceEquals(directory, host.MaterialInstances);
            bool inside = current && hitClip.Contains(point.X, point.Y);
            hovered = inside ? Hit(point) : null;
            OwnsPointer = inside || leftTail || rightTail;
            if (!current) armed = null;
            if (current)
            {
                if (left && !previousLeft && inside)
                {
                    leftTail = ConsumeLeft = true; armed = hovered?.Enabled == true ? hovered : null;
                    pressedSession = session; pressedGeneration = generation; pressedInstance = InstanceOf(armed);
                }
                if (right && inside) { rightTail = ConsumeRight = true; armed = null; }
                if (!left && previousLeft && armed != null)
                {
                    var target = armed; armed = null;
                    if (!right && !previousRight && ReferenceEquals(target, hovered) && pressedGeneration == generation && pressedSession == session && pressedInstance == InstanceOf(target)) Execute(target, pressedInstance);
                }
                if (host.Input.Hotkeys.IsNew((int)Keys.Escape))
                { host.Input.Hotkeys.SuppressKey((int)Keys.Escape); host.Input.ConsumeHotkeyActions(); Back(); }
            }
            ConsumeLeft |= leftTail; ConsumeRight |= rightTail;
            if (focused) { if (!left) leftTail = false; if (!right) rightTail = false; }
            previousLeft = focused ? left : true; previousRight = focused ? right : true;
            if (Opened && !shell.Visible && focused) Close();
        }
        private long InstanceOf(BlueprintLibraryPart part)
        {
            if (part == null || ReferenceEquals(part, back)) return 0;
            // Only the projected visible slice is inspected, never all scene rows.
            for (int i = Math.Max(0, firstVisible); i <= lastVisible && i < rows.Count; i++)
                if (ReferenceEquals(rows[i].Materials, part) || ReferenceEquals(rows[i].Remove, part)) return rows[i].Instance.ProjectionGeneration;
            return -1;
        }
        private BlueprintLibraryPart Hit(Vector2 point)
        {
            float x = point.X - view.X, y = point.Y - view.Y;
            if (back != null && back.Element.Rect.Contains(x, y)) return back;
            if (y < headerHeight) return null;
            y += scroll;
            foreach (var part in visible) if (!part.Fixed && part.Command != BlueprintLibraryCommand.None && part.Element.Rect.Contains(x, y)) return part;
            return null;
        }
        private void Execute(BlueprintLibraryPart target, long instance)
        {
            if (ReferenceEquals(target, back)) { Back(); return; }
            if (target.Command == BlueprintLibraryCommand.PlacedMaterials)
                host.OpenPlacedInstanceMaterials(session, instance, target.Element.Rect.Offset(view.X, view.Y - scroll));
            else if (target.Command == BlueprintLibraryCommand.PlacedRemove)
                host.TryRemovePlacedInstance(session, instance);
        }
        private void Back() { Close(); shell.Layout.SetBlueprintLibraryVisible(false); shell.ScrollTo(0); }
        private const float Gap = 8;
        internal void Prepare(bool active, Matrix transform, Vector2 dimensions)
        {
            CheckSession(); if (!Visible) { ready = false; return; }
            if (!active || !renderer.Refresh()) { ready = false; armed = null; return; }
            if (host.Shell?.BlueprintMaterialsUi?.Visible == true) { ready = false; return; }
            var next = shell.Layout.Viewport.Offset(shell.X, shell.Y);
            var instances = host.MaterialInstances;
            bool rebuild = !ReferenceEquals(directory, instances) || measuredWidth != next.Width || resources != renderer.Generation;
            bool geometry = rebuild || !F5PopupPlacement.SameAnchor(view, next) || matrix != transform || screen != dimensions || layout != shell.Layout.Generation || scroll != shell.Scroll;
            matrix = transform; screen = dimensions; view = next; resources = renderer.Generation; layout = shell.Layout.Generation;
            if (rebuild) { directory = instances; Build(); }
            shell.Layout.SetBlueprintContentHeight(contentHeight);
            shell.ClampScroll();
            geometry |= scroll != shell.Scroll;
            if (geometry) { generation++; armed = null; drawnGeneration = -1; }
            scroll = shell.Scroll;
            var player = Main.LocalPlayer;
            int px = player == null ? 0 : (int)Math.Floor(player.Center.X / 16.0), py = player == null ? 0 : (int)Math.Floor(player.Center.Y / 16.0);
            bool moved = px != playerX || py != playerY;
            if (geometry || moved || shownRemoveReason != host.PlacedRemoveDisabledReason)
                Project(px, py, geometry || moved);
            playerX = px; playerY = py; ready = true;
            hitClip = F5ControlRenderer.LogicalClip(F5ControlRenderer.ContentClip(view, matrix, new Rectangle(0, 0, (int)dimensions.X, (int)dimensions.Y)), matrix);
        }
        private void Build()
        {
#if DEBUG
            DirectoryBuilds++;
#endif
            rows.Clear(); measuredWidth = view.Width;
            float backWidth = Math.Max(70, measure("返回", .70f).Width + 16), backHeight = Math.Max(30, measure("返回", .70f).Height + 8);
            headerHeight = Math.Max(44, Math.Max(backHeight + 14, measure("已放置列表", .70f).Height + 16));
            back = Part(F5ElementKind.Button, new F5Rect(view.Width - backWidth - 2, 7, backWidth, backHeight), "返回", BlueprintLibraryCommand.Back); back.Fixed = true;
            float top = headerHeight + Gap;
            foreach (var item in directory)
            {
                var row = new Row { Instance = item, Top = top };
                float y = top + 8;
                string label = item.Name + " · #" + item.Sequence.ToString(CultureInfo.InvariantCulture);
                foreach (var line in Wrap(label, view.Width - 20)) { row.Parts.Add(Text(line, 10, y, view.Width - 20)); y += Math.Max(22, measure(line, .70f).Height + 2) + 3; }
                string size = item.Width > 0 && item.Height > 0 ? "尺寸 " + item.Width + "×" + item.Height + " 格" : "正在恢复尺寸";
                foreach (var line in Wrap(size + " · 左上角（" + item.OriginX + "，" + item.OriginY + "）", view.Width - 20))
                { row.Parts.Add(Text(line, 10, y, view.Width - 20)); y += Math.Max(22, measure(line, .70f).Height + 2) + 3; }
                row.RelativeY = y; y += Math.Max(22, measure("左上方 · 距中心约 12345 格", .70f).Height + 2) + 6;
                float buttonHeight = Math.Max(30, measure("材料 移除", .70f).Height + 8);
                float materialWidth = Math.Max(76, measure("材料", .70f).Width + 16), removeWidth = Math.Max(76, measure("移除", .70f).Width + 16);
                row.Materials = Part(F5ElementKind.Button, new F5Rect(10, y, materialWidth, buttonHeight), "材料", BlueprintLibraryCommand.PlacedMaterials);
                bool wrapButtons = materialWidth + removeWidth + 8 > view.Width - 20;
                if (wrapButtons) y += buttonHeight + 6;
                row.Remove = Part(F5ElementKind.Button, new F5Rect(wrapButtons ? 10 : 18 + materialWidth, y, removeWidth, buttonHeight), "移除", BlueprintLibraryCommand.PlacedRemove);
                row.Height = y + buttonHeight + 10 - top;
                row.Parts.Insert(0, Part(F5ElementKind.Panel, new F5Rect(0, top, view.Width, row.Height), "", BlueprintLibraryCommand.None));
                row.Parts.Add(row.Materials); row.Parts.Add(row.Remove); rows.Add(row); top += row.Height + Gap;
            }
            contentHeight = rows.Count == 0 ? headerHeight + 70 : top;
            firstVisible = lastVisible = -1;
        }
        private void Project(int px, int py, bool relative)
        {
            visible.Clear(); visible.Add(Text("已放置列表", 0, 8, back.Element.Rect.X - 8, true)); visible.Add(back);
            if (rows.Count == 0) { visible.Add(Text("当前没有已放置蓝图", 8, headerHeight + 16, view.Width - 16)); firstVisible = lastVisible = -1; return; }
            int lo = 0, hi = rows.Count;
            while (lo < hi) { int mid = lo + (hi - lo) / 2; if (rows[mid].Top + rows[mid].Height < scroll + headerHeight) lo = mid + 1; else hi = mid; }
            firstVisible = Math.Max(0, lo - 1); lastVisible = firstVisible - 1;
            shownRemoveReason = host.PlacedRemoveDisabledReason;
            for (int i = firstVisible; i < rows.Count && rows[i].Top < scroll + view.Height + 16; i++)
            {
                var row = rows[i]; lastVisible = i;
#if DEBUG
                VisibleRowVisits++;
#endif
                row.Remove.Enabled = shownRemoveReason == null; row.Remove.Hint = shownRemoveReason;
                row.Remove.HintSize = shownRemoveReason == null ? default(F5Size) : measure(shownRemoveReason, .62f);
                row.Materials.Enabled = host.CanOpenMaterials;
                if (relative || row.Relative == null)
                {
                    row.Relative = Text(RelativePosition(row.Instance, px, py), 10, row.RelativeY, view.Width - 20);
#if DEBUG
                    RelativeUpdates++;
#endif
                }
                visible.AddRange(row.Parts); visible.Add(row.Relative);
            }
        }
        // Coordinates use tile boundaries for both centers. A 1x1 blueprint in
        // the player's tile is distance zero; even sizes center on tile seams.
        internal static string RelativePosition(BlueprintMaterialInstance item, int playerTileX, int playerTileY)
        {
            if (item.Width <= 0 || item.Height <= 0) return "正在恢复位置";
            double dx = item.OriginX + item.Width / 2.0 - (playerTileX + .5), dy = item.OriginY + item.Height / 2.0 - (playerTileY + .5);
            double distance = Math.Sqrt(dx * dx + dy * dy);
            string direction;
            if (distance < .5) direction = "附近";
            else
            {
                int sector = ((int)Math.Floor(Math.Atan2(dy, dx) / (Math.PI / 4) + .5) + 8) % 8;
                direction = Directions[sector];
            }
            return direction + " · 距中心约 " + Math.Round(distance, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture) + " 格";
        }
        private static readonly string[] Directions = { "右方", "右下方", "下方", "左下方", "左方", "左上方", "上方", "右上方" };
        private IEnumerable<string> Wrap(string text, float width)
        {
            var starts = StringInfo.ParseCombiningCharacters(text); int start = 0;
            while (start < starts.Length)
            {
                int low = start + 1, high = starts.Length;
                while (low < high) { int mid = low + (high - low + 1) / 2; int end = mid == starts.Length ? text.Length : starts[mid]; if (measure(text.Substring(starts[start], end - starts[start]), .70f).Width <= width) low = mid; else high = mid - 1; }
                int finish = low == starts.Length ? text.Length : starts[low]; yield return text.Substring(starts[start], finish - starts[start]); start = low;
            }
        }
        private BlueprintLibraryPart Text(string text, float x, float y, float width, bool fixedPart = false)
        { var value = Part(F5ElementKind.Text, new F5Rect(x, y, width, Math.Max(22, measure(text, .70f).Height + 2)), text, BlueprintLibraryCommand.None); value.Fixed = fixedPart; return value; }
        private BlueprintLibraryPart Part(F5ElementKind kind, F5Rect rect, string text, BlueprintLibraryCommand command)
        { return new BlueprintLibraryPart { Element = new F5Element(kind, rect, text, measure(text, .70f), .70f, F5Command.None), Command = command, Enabled = true }; }
        internal void Draw()
        {
            if (!Visible || !ready || host.Shell?.BlueprintMaterialsUi?.Visible == true || matrix != Main.UIScaleMatrix || !renderer.ResourcesCurrent) { drawnGeneration = -1; return; }
            renderer.Draw(visible, view, matrix, scroll, hovered, armed, null, null, false, headerHeight);
            if (!F5PopupPlacement.SameAnchor(hitClip, renderer.LastClip)) { hitClip = renderer.LastClip; generation++; armed = null; }
            drawnGeneration = generation;
        }
        public void Dispose() { Close(); }
    }
}

using System;
using System.Collections.Generic;
using JueMingR.TerrariaHost.Items;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria.GameInput;

namespace JueMingR.TerrariaHost.F5
{
    // Like PotionConfigurationPopup, this owns an anchored overlay and its
    // complete physical gesture. Preferences remain exclusively in Host.
    internal sealed class BlueprintReplacementPopup
    {
        private const string Title = "同类替换 · 配置";
        private const float TextScale = .7f, Gap = 6;
        private readonly Blueprint.HostBlueprint host;
        private readonly Func<string, float, F5Size> measure;
        private readonly ItemsRenderer renderer = new ItemsRenderer();
        private readonly List<F5Element> cells = new List<F5Element>();
        private readonly F5Size[] sizes = new F5Size[12];
        private F5Size titleSize;
        private F5Rect anchor, body, close, title;
        private Matrix matrix;
        private Vector2 screen, pointer;
        private object font;
        private int skin, sourceGeneration, rendererGeneration, armed = -1, armedGeneration;
        private long session, preferenceRevision, armedRevision;
        private bool ready, resourcesPrepared, previousLeft, previousEscape, leftTail, rightTail;
        private float cellHeight;
        internal bool Visible { get; private set; }
        internal F5Rect Panel { get; private set; }
        internal F5Rect Body { get { return body; } }
        internal F5Rect CloseRect { get { return close; } }
        internal IReadOnlyList<F5Element> Cells { get { return cells; } }
        internal int FirstRow { get; private set; }
        internal int Columns { get; private set; }
        internal int VisibleRows { get; private set; }
        internal int MaxFirstRow { get; private set; }
        internal int Generation { get; private set; }
        internal int MeasurementCount { get; private set; }
        internal bool OwnsPointer { get; private set; }
        internal bool ConsumeLeft { get; private set; }
        internal bool ConsumeRight { get; private set; }
        internal bool ConsumeWheel { get; private set; }
        internal bool Captured { get { return armed != -1 || leftTail || rightTail; } }
        internal bool BlockPointer { get { return OwnsPointer || ConsumeLeft || ConsumeRight || Captured; } }
        internal bool CanHint { get { return Visible && ready && !Captured && host.Input.CanPrepareText; } }

        internal BlueprintReplacementPopup(Blueprint.HostBlueprint host, Func<string, float, F5Size> measure)
        { this.host = host; this.measure = measure; }
        internal void Open(F5Rect button)
        {
            bool same = Visible; Close(); if (same || !host.ControlsEnabled) return;
            Visible = true; anchor = button; FirstRow = 0; session = host.UiSession;
        }
        internal void Close()
        {
            Visible = ready = false; armed = -1; cells.Clear(); renderer.Dispose();
            // Neither close nor focus suspension invents a physical release.
            // Process keeps the original tail until a trusted neutral sample.
        }
        internal bool Contains(float x, float y) { return Visible && ready && Panel.Contains(x, y); }
        internal void CheckSession() { if (Visible && (!host.ControlsEnabled || session != host.UiSession)) Close(); }
        internal void Prepare(Matrix transform, Vector2 physical, F5Rect button, object fontIdentity, int skinGeneration, int layoutGeneration, bool resources)
        {
            CheckSession(); if (!Visible) return;
            if (resources && !renderer.Refresh()) { ready = false; armed = -1; return; }
            bool fontChanged = !ReferenceEquals(font, fontIdentity);
            bool changed = !ready || fontChanged || skin != skinGeneration || sourceGeneration != layoutGeneration ||
                screen != physical || matrix != transform || !F5PopupPlacement.SameAnchor(anchor, button) ||
                resources && rendererGeneration != renderer.Generation;
            if (preferenceRevision != host.UiPreferenceRevision) armed = -1;
            preferenceRevision = host.UiPreferenceRevision;
            if (!changed) return;
            armed = -1;
            if (fontChanged || MeasurementCount == 0)
            {
                titleSize = measure(Title, TextScale); MeasurementCount++;
                for (int i = 0; i < sizes.Length; i++) { sizes[i] = measure(BlueprintControls.ReplacementCategoryNames[i], TextScale); MeasurementCount++; }
            }
            float textWidth = 0, textHeight = 0;
            foreach (var size in sizes) { textWidth = Math.Max(textWidth, size.Width); textHeight = Math.Max(textHeight, size.Height); }
            float header = Math.Max(44, titleSize.Height + 16), minimumCell = Math.Max(72, textWidth + 34);
            Vector2 logical = physical / transform.M11;
            float requiredHeader = titleSize.Width + 62;
            float width = Math.Min(Math.Max(352, Math.Max(requiredHeader, 4 * minimumCell + 3 * Gap + 16)), logical.X - 24);
            float maxHeight = Math.Min(400, logical.Y - 24); cellHeight = Math.Max(36, textHeight + 16);
            if (width < requiredHeader || width < minimumCell + 16 || maxHeight < header + 8 + cellHeight + Gap)
            { Close(); return; }
            Columns = Math.Min(4, Math.Max(1, (int)((width - 16 + Gap) / (minimumCell + Gap))));
            int totalRows = (sizes.Length + Columns - 1) / Columns;
            VisibleRows = Math.Min(totalRows, Math.Max(1, (int)((maxHeight - header - 8) / (cellHeight + Gap))));
            MaxFirstRow = Math.Max(0, totalRows - VisibleRows); FirstRow = Math.Min(FirstRow, MaxFirstRow);
            float height = header + 8 + VisibleRows * (cellHeight + Gap), x = button.Right + 8;
            if (x + width > logical.X - 12) x = button.X - width - 8;
            x = (float)Math.Floor(Math.Max(12, Math.Min(logical.X - width - 12, x)));
            float y = (float)Math.Floor(Math.Max(12, Math.Min(logical.Y - height - 12, button.Y)));
            Panel = new F5Rect(x, y, width, height);
            body = new F5Rect(x + 4, y + header, width - 8, height - header - 4);
            close = new F5Rect(x + width - 38, y + (header - 30) / 2, 30, 30);
            title = new F5Rect(x + 8, y + 4, titleSize.Width + 4, header - 8);
            anchor = button; screen = physical; matrix = transform; font = fontIdentity; skin = skinGeneration; sourceGeneration = layoutGeneration;
            if (resources) { resourcesPrepared = true; rendererGeneration = renderer.Generation; }
            ready = true; Generation++; ProjectCells();
        }
        private void ProjectCells()
        {
            cells.Clear(); float width = (Panel.Width - 16 - (Columns - 1) * Gap) / Columns;
            for (int i = FirstRow * Columns; i < sizes.Length && i < (FirstRow + VisibleRows) * Columns; i++)
            {
                var rect = new F5Rect(Panel.X + 8 + i % Columns * (width + Gap), body.Y + (i / Columns - FirstRow) * (cellHeight + Gap), width, cellHeight);
                cells.Add(new F5Element(F5ElementKind.Button, rect, BlueprintControls.ReplacementCategoryNames[i], sizes[i], TextScale, F5Command.None));
            }
        }
        internal void Process(bool active, KeyboardState keys, Vector2 point, Matrix transform, Vector2 physical, bool focused, int wheel)
        {
            pointer = point; OwnsPointer = ConsumeWheel = false; ConsumeLeft = leftTail; ConsumeRight = rightTail;
            bool left = PlayerInput.MouseInfo.LeftButton == ButtonState.Pressed, right = PlayerInput.MouseInfo.RightButton == ButtonState.Pressed;
            bool escape = keys.IsKeyDown(Keys.Escape);
            if (!active || !focused || session != host.UiSession)
            {
                Close(); previousLeft = focused ? left : true; previousEscape = focused ? escape : true;
                if (focused) { if (!left) leftTail = false; if (!right) rightTail = false; } return;
            }
            if (!Visible)
            { previousLeft = left; previousEscape = escape; if (!left) leftTail = false; if (!right) rightTail = false; return; }
            bool current = ready && matrix == transform && screen == physical && preferenceRevision == host.UiPreferenceRevision &&
                (!resourcesPrepared || renderer.ResourcesCurrent && renderer.Generation == rendererGeneration);
            OwnsPointer = Captured || Contains(point.X, point.Y);
            if (escape && !previousEscape)
            { host.Input.Hotkeys.SuppressKey((int)Keys.Escape); host.Input.ConsumeHotkeyActions(); Close(); }
            previousEscape = escape;
            int hit = -1;
            if (Visible && current)
            {
                if (close.Contains(point.X, point.Y)) hit = -2;
                else for (int i = 0; i < cells.Count; i++) if (cells[i].Rect.Contains(point.X, point.Y)) { hit = FirstRow * Columns + i; break; }
            }
            if (left && !previousLeft) { armed = hit; armedGeneration = Generation; armedRevision = preferenceRevision; }
            if (!current) armed = -1;
            if (OwnsPointer)
            {
                if (left) leftTail = true; if (right) rightTail = true;
                for (int key = 256; key <= 260; key++) if (host.Input.Hotkeys.IsNew(key)) host.Input.Hotkeys.SuppressKey(key);
            }
            // A wheel and physical release can share one sample. Scrolling
            // invalidates that press before any old card may execute.
            if (Visible && Contains(point.X, point.Y) && wheel != 0) armed = -1;
            if (Visible && !left && previousLeft && armed == hit && hit != -1 && armedGeneration == Generation && armedRevision == host.UiPreferenceRevision)
            {
                if (hit == -2) Close();
                else if (host.ControlsEnabled && host.Input.CanStartActions) host.SetReplacementCategories(host.ReplacementCategories ^ 1 << hit);
            }
            if (Visible && Contains(point.X, point.Y) && wheel != 0)
            {
                int next = Math.Max(0, Math.Min(MaxFirstRow, FirstRow + (wheel < 0 ? 1 : -1)));
                if (next != FirstRow) { FirstRow = next; Generation++; ProjectCells(); }
                armed = -1; ConsumeWheel = true;
            }
            ConsumeLeft = leftTail; ConsumeRight = rightTail;
            if (!left) { leftTail = false; armed = -1; } if (!right) rightTail = false; previousLeft = left;
        }
        internal string Hint(float x, float y, out F5Rect rect)
        {
            rect = default(F5Rect); if (!Visible || !ready) return null;
            for (int i = 0; i < cells.Count; i++) if (cells[i].Rect.Contains(x, y))
            { rect = cells[i].Rect; return BlueprintControls.ReplacementHint(FirstRow * Columns + i); }
            return null;
        }
        internal void Draw()
        {
            if (!Visible || !ready) return;
            renderer.Pass(matrix, Panel, () => { renderer.Panel(Panel); renderer.Text(Title, title, Microsoft.Xna.Framework.Color.White, TextScale); renderer.Cross(close, true); });
            renderer.Pass(matrix, body, () => {
                for (int i = 0; i < cells.Count; i++)
                {
                    var cell = cells[i]; var rect = cell.Rect;
                    renderer.ItemButton(rect, host.ControlsEnabled, rect.Contains(pointer.X, pointer.Y));
                    renderer.Text(cell.Text, new F5Rect(rect.X + 6, rect.Y, rect.Width - 26, rect.Height), Microsoft.Xna.Framework.Color.White, TextScale);
                    if ((host.ReplacementCategories & 1 << (FirstRow * Columns + i)) != 0) renderer.Selection(rect);
                }
            });
        }
    }
}

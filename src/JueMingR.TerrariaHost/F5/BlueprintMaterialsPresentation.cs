using System;
using System.Collections.Generic;
using System.Globalization;
using JueMingR.Features.Blueprint;
using JueMingR.TerrariaHost.Blueprint;
using Microsoft.Xna.Framework;

namespace JueMingR.TerrariaHost.F5
{
    internal enum BlueprintMaterialsSource { World, F5, Library }

    // This modal owns only its source, selected identity, geometry and gestures.
    // Viewing a scope never selects or pauses an automatic construction target.
    internal sealed class BlueprintMaterialsPresentation : IDisposable
    {
        private static readonly IReadOnlyList<BlueprintMaterialInstance> NoInstances = Array.Empty<BlueprintMaterialInstance>();
        private sealed class ScopeRow
        { internal long Identity; internal string Name, Position; internal F5Element Element; }
        private readonly HostBlueprint host;
        private readonly F5Interaction shell;
        private readonly BlueprintMaterialsLayout content = new BlueprintMaterialsLayout();
        private readonly BlueprintMaterialsRenderer renderer = new BlueprintMaterialsRenderer();
        private readonly List<F5Element> labels = new List<F5Element>(), buttons = new List<F5Element>(), scopeLabels = new List<F5Element>();
        private readonly List<ScopeRow> scopes = new List<ScopeRow>(), visibleScopes = new List<ScopeRow>();
        private readonly Dictionary<long, ScopeRow> scopesById = new Dictionary<long, ScopeRow>();
        private IReadOnlyList<BlueprintMaterialInstance> scopedInstances;
        private float scopedWidth;
        private int scopedResources = -1;
        private BlueprintMaterialSnapshot snapshot;
        private LibraryEntry asset;
        private IReadOnlyList<BlueprintMaterialInstance> shownInstances;
        private IReadOnlyList<BlueprintMaterialInstance> observedInstances;
        private BlueprintMaterialsSource source;
        private long session, selected;
        private Matrix matrix;
        private Vector2 screen;
        private F5Rect anchor, preparedAnchor, panel, body, selector, scopePanel, summaryRect;
        private float scroll, scopeScroll, scopeHeight, lineHeight, summaryHeight;
        private int generation, pressedGeneration, projection, shownContent = -1, resource = -1, layout = -1;
        private F5Element hovered, armed;
        private bool dirty = true, ready, dropDown, previousLeft, leftTail, rightTail;
        private int hoveredItem;
        private long names = -1;
        private Vector2 pointer;
        internal bool Visible { get; private set; }
        internal bool WorldMode { get { return asset == null; } }
        internal bool BlockPointer { get { return Visible; } }
        internal bool OwnsPointer { get; private set; }
        internal bool ConsumeLeft { get; private set; }
        internal bool ConsumeRight { get; private set; }
        internal bool ConsumeWheel { get; private set; }
        internal F5Rect Panel { get { return panel; } }
        internal F5Rect Body { get { return body; } }
        internal BlueprintMaterialsSource Source { get { return source; } }
        internal Guid AssetId { get { return asset?.AssetId ?? Guid.Empty; } }
        internal long Selected { get { return selected; } }
        internal bool RangeExpanded { get { return dropDown; } }
        internal int HoveredItem { get { return hoveredItem; } }
        internal int Generation { get { return generation; } }
        internal BlueprintMaterialsLayout Content { get { return content; } }
#if DEBUG
        internal long LayoutBuilds { get; private set; }
        internal long IconLoads { get { return renderer.IconLoads; } }
        internal long VisibleSetBuilds { get { return renderer.VisibleSetBuilds; } }
#endif
        internal BlueprintMaterialsPresentation(HostBlueprint host, F5Interaction shell) { this.host = host; this.shell = shell; }
        internal void OpenWorld(BlueprintMaterialsSource origin, F5Rect entry)
        { Open(origin, entry, null); host.SetMaterialViewDemand(true); }
        internal void OpenAsset(LibraryEntry entry, F5Rect position)
        { if (entry != null) Open(BlueprintMaterialsSource.Library, position, entry); }
        private void Open(BlueprintMaterialsSource origin, F5Rect entry, LibraryEntry template)
        {
            Close(); source = origin; anchor = entry; asset = template; selected = 0; session = host.UiSession;
            Visible = true; ready = false; dirty = true; scroll = scopeScroll = 0; snapshot = null; shownInstances = null;
            // Retire the opening gesture in the shared physical owner. We do
            // not create another mouse sample or synthesize a neutral release.
            host.Input.Hotkeys.SuppressHeld(); host.Input.ConsumeHotkeyActions();
        }
        internal void MoveAnchor(F5Rect entry) { if (!F5PopupPlacement.SameAnchor(anchor, entry)) { anchor = entry; dirty = true; armed = null; } }
        internal bool Contains(float x, float y) { return Visible && (panel.Contains(x, y) || dropDown && scopePanel.Contains(x, y)); }
        internal bool Captured { get { return armed != null || leftTail || rightTail; } }
        internal void Close()
        {
            if (Visible) { host.SetMaterialViewDemand(false); host.Input.Hotkeys.SuppressHeld(); host.Input.ConsumeHotkeyActions(); }
            Visible = ready = dropDown = OwnsPointer = false; armed = hovered = null; hoveredItem = 0;
            asset = null; snapshot = null; shownInstances = observedInstances = null; labels.Clear(); buttons.Clear(); scopes.Clear(); visibleScopes.Clear(); scopeLabels.Clear();
            content.Clear(); renderer.Dispose(); shownContent = resource = layout = -1; dirty = true;
            summaryHeight = 0;
            scopedInstances = null; scopesById.Clear(); scopedResources = -1;
            // Existing physical tails stay owned until a trustworthy sample
            // proves release, including when focus/map closes the whole modal.
        }
        internal void CheckSession() { if (Visible && (!host.HasSession || session != host.UiSession)) Close(); }
        internal void Process(bool active, Vector2 point, bool focused, int wheel, bool f5)
        {
            pointer = point;
            ConsumeLeft = leftTail; ConsumeRight = rightTail; ConsumeWheel = OwnsPointer = false;
            bool left = host.Input.RawMouseLeft, right = host.Input.RawMouseRight;
            CheckSession();
            if (!active || !focused) { if (Visible) Close(); armed = null; previousLeft = focused ? left : true; RetireTails(left, right, focused); return; }
            if (!Visible) { previousLeft = left; RetireTails(left, right, focused); return; }
            bool current = ready && matrix == Terraria.Main.UIScaleMatrix && screen == Terraria.GameInput.PlayerInput.OriginalScreenSize &&
                resource == renderer.Generation && renderer.ResourcesCurrent && layout == shell.Layout.Generation && F5PopupPlacement.SameAnchor(anchor, preparedAnchor) && !dirty;
            bool inside = current && Contains(point.X, point.Y);
            OwnsPointer = inside || leftTail || rightTail || host.Input.HotkeyPointerOwned;
            if (!current) armed = null;
            hovered = current ? Hit(point) : null;
            if (host.Input.Hotkeys.IsNew(27) || f5)
            {
                host.Input.Hotkeys.SuppressKey(27); host.Input.ConsumeHotkeyActions();
                if (dropDown && !f5) { dropDown = false; armed = null; dirty = true; } else Close();
                previousLeft = left; RetireTails(left, right, focused); return;
            }
            if (left && !previousLeft && (inside || dropDown))
            {
                // This is a modal for the page. A click outside closes only its
                // expanded selector and cannot become a click on a lower row.
                leftTail = true; ConsumeLeft = true;
                if (dropDown && !scopePanel.Contains(point.X, point.Y) && !selector.Contains(point.X, point.Y))
                { dropDown = false; dirty = true; armed = null; }
                else { armed = hovered; pressedGeneration = generation; }
            }
            if (right && inside) { rightTail = ConsumeRight = true; armed = null; }
            if (!left && previousLeft && armed != null)
            {
                var target = armed; armed = null;
                if (ReferenceEquals(target, hovered) && pressedGeneration == generation)
                {
                    if (ReferenceEquals(target, buttons[0])) Close();
                    else if (buttons.Count > 1 && ReferenceEquals(target, buttons[1])) { dropDown = !dropDown; dirty = true; }
                    else foreach (var scope in visibleScopes) if (ReferenceEquals(scope.Element, target))
                    { SelectScope(scope.Identity); break; }
                }
            }
            if (Visible && current && (inside || leftTail || rightTail) && wheel != 0)
            {
                // The list still owns top/bottom wheel input. Pointer samples
                // outside our panel remain available to other native UI.
                ConsumeWheel = true; armed = null;
                if (dropDown) scopeScroll = Math.Max(0, Math.Min(Math.Max(0, scopeHeight - scopePanel.Height + 8), scopeScroll - Math.Sign(wheel) * lineHeight * 3));
                else scroll = Math.Max(0, Math.Min(Math.Max(0, content.Height - body.Height), scroll - Math.Sign(wheel) * lineHeight * 3));
                dirty = true;
            }
            if (Visible && current && !left && !right && !dropDown)
            { var row = content.Hit(point.X, point.Y, body); hoveredItem = row?.Cell.ItemType ?? 0; }
            else hoveredItem = 0;
            if (OwnsPointer || ConsumeLeft || ConsumeRight || ConsumeWheel)
            { for (int key = 256; key <= 260; key++) host.Input.Hotkeys.ConsumePress(key); host.Input.ConsumeHotkeyActions(); }
            previousLeft = left; RetireTails(left, right, focused);
        }
        private void RetireTails(bool left, bool right, bool focused)
        { if (!focused) return; if (!left) leftTail = false; if (!right) rightTail = false; }
        private F5Element Hit(Vector2 point)
        {
            if (dropDown && scopePanel.Contains(point.X, point.Y)) foreach (var row in visibleScopes)
                if (row.Element.Rect.Contains(point.X, point.Y)) return row.Element;
            foreach (var button in buttons) if (button.Rect.Contains(point.X, point.Y)) return button;
            return null;
        }
        internal void Prepare(bool active, Matrix transform, Vector2 dimensions)
        {
            CheckSession(); if (!Visible) return;
            if (!active) { Close(); return; }
            if (!renderer.Refresh()) { Close(); return; }
            if (WorldMode) host.SetMaterialViewDemand(true);
            var value = ReadSnapshot(); var instances = observedInstances;
            bool geometry = matrix != transform || screen != dimensions || resource != renderer.Generation || layout != shell.Layout.Generation ||
                !F5PopupPlacement.SameAnchor(anchor, preparedAnchor);
            bool changed = !ReferenceEquals(snapshot, value) || !ReferenceEquals(shownInstances, instances) || names != host.MaterialNameRevision;
            matrix = transform; screen = dimensions;
            if (geometry || changed || dirty)
            {
                bool identity = geometry || dirty || !ReferenceEquals(shownInstances, instances) || !SameRows(snapshot, value);
                var oldButtons = buttons.ToArray(); var oldBody = body;
                bool keepClose = !geometry && !dirty && oldButtons.Length > 0 && ReferenceEquals(armed, oldButtons[0]);
                snapshot = value; shownInstances = instances;
                Build(dimensions.X / transform.M11, dimensions.Y / transform.M11, WorldMode && !geometry && content.Count > 0);
                // Numeric updates do not create new button identities. Preserve
                // a close press if its geometry and the selected scope survive.
                for (int i = 0; i < buttons.Count && i < oldButtons.Length; i++)
                    if (buttons[i].Text == oldButtons[i].Text && F5PopupPlacement.SameAnchor(buttons[i].Rect, oldButtons[i].Rect)) buttons[i] = oldButtons[i];
                identity |= !F5PopupPlacement.SameAnchor(oldBody, body);
                projection++; if (identity) { generation++; armed = null; } dirty = false; ready = true;
                // Close has no row/scope operand. A bounded publication gap or
                // fresh bill cannot invalidate its unchanged physical control.
                if (keepClose && ReferenceEquals(buttons[0], oldButtons[0])) { armed = buttons[0]; pressedGeneration = generation; }
                resource = renderer.Generation; layout = shell.Layout.Generation; preparedAnchor = anchor;
                names = host.MaterialNameRevision;
#if DEBUG
                LayoutBuilds++;
#endif
            }
            if (shownContent != content.Generation) { shownContent = content.Generation; projection++; }
            renderer.Prepare(content, projection);
            string hint = null; F5Rect target = default(F5Rect);
            if (hoveredItem > 0) foreach (var row in content.Visible) if (row.Cell.ItemType == hoveredItem) { hint = row.Cell.Name; target = row.Rect; break; }
            if (dropDown && hovered != null) foreach (var row in visibleScopes) if (ReferenceEquals(row.Element, hovered)) { hint = row.Position; target = row.Element.Rect; break; }
            if (hint == null && SummaryRect().Contains(pointer.X, pointer.Y))
            { hint = snapshot?.Error ?? (snapshot?.Status == BlueprintMaterialStatus.Pending ? "正在核对材料" : snapshot?.Status == BlueprintMaterialStatus.Unresolved ? "部分目标的材料尚未确定" : WorldMode && snapshot?.InventoryReady != true ? "随身材料尚未核对完成" : null); target = SummaryRect(); }
            renderer.PrepareHint(hint, target, new F5Rect(12, 12, dimensions.X / transform.M11 - 24, dimensions.Y / transform.M11 - 24),
                Terraria.GameContent.FontAssets.MouseText?.Value, shell.Layout.DynamicTextSize);
        }
        internal bool SelectScope(long identity)
        {
            if (!Visible || !WorldMode) return false;
            if (identity != 0)
            {
                bool exists = false; foreach (var item in host.MaterialInstances)
                    if (item.SessionGeneration == session && item.ProjectionGeneration == identity) { exists = true; break; }
                if (!exists) return false;
            }
            if (selected != identity) content.Clear();
            selected = identity; dropDown = false; scroll = 0; snapshot = null; dirty = true; return true;
        }
        // Scope recovery is a pure UI consumer of the Host's current committed
        // scene. It neither owns instances nor admits construction candidates.
        internal BlueprintMaterialSnapshot ReadSnapshot()
        {
            observedInstances = WorldMode ? host.MaterialInstances : NoInstances;
            if (WorldMode && selected != 0 && !ReferenceEquals(shownInstances, observedInstances))
            {
                bool found = false; foreach (var item in observedInstances)
                    if (item.ProjectionGeneration == selected && item.SessionGeneration == session) { found = true; break; }
                if (!found) { selected = 0; snapshot = null; content.Clear(); dirty = true; }
            }
            return WorldMode ? host.MaterialWorld(selected) : host.MaterialAsset(asset);
        }
        private F5Rect SummaryRect() { return summaryRect; }
        private void Build(float screenWidth, float screenHeight, bool preserveBodyOrigin)
        {
            float previousBodyY = body.Y;
            var oldScopeRows = new Dictionary<long, ScopeRow>(); foreach (var row in visibleScopes) oldScopeRows[row.Identity] = row;
            labels.Clear(); buttons.Clear(); scopeLabels.Clear(); visibleScopes.Clear();
            float width = Math.Min(720, screenWidth - 24), height = Math.Min(540, screenHeight - 24);
            if (width < 300 || height < 160) throw new InvalidOperationException("材料窗口当前空间不足");
            var measure = new Func<string, float, F5Size>(shell.Layout.DynamicTextSize);
            lineHeight = Math.Max(28, measure("测试Ag", .70f).Height + 8);
            panel = F5PopupPlacement.Place(anchor, width, height, screenWidth, screenHeight);
            var closeSize = measure("关闭", .70f); float closeWidth = Math.Max(58, closeSize.Width + 18);
            buttons.Add(new F5Element(F5ElementKind.Button, new F5Rect(panel.Right - closeWidth - 10, panel.Y + 7, closeWidth, Math.Max(28, closeSize.Height + 8)), "关闭", closeSize, .70f, F5Command.None));
            var title = BlueprintMaterialsLayout.Wrap(WorldMode ? "施工材料" : "蓝图材料：" + asset.Name, width - closeWidth - 36, .76f, measure);
            foreach (var line in title) labels.Add(new F5Element(F5ElementKind.Text, line.Rect.Offset(panel.X + 12, panel.Y + 10), line.Text, line.TextSize, line.TextScale, line.Command));
            float y = panel.Y + Math.Max(42, Math.Max(BlueprintMaterialsLayout.BlockHeight(title, 2) + 18, buttons[0].Rect.Height + 14));
            var summary = BlueprintMaterialsLayout.Wrap(BlueprintMaterialsLayout.Summary(snapshot, WorldMode), width - 24, .70f, measure);
            foreach (var line in summary) labels.Add(new F5Element(F5ElementKind.Text, line.Rect.Offset(panel.X + 12, y), line.Text, line.TextSize, line.TextScale, line.Command));
            float measuredSummary = BlueprintMaterialsLayout.BlockHeight(summary, 2);
            summaryHeight = snapshot?.Status == BlueprintMaterialStatus.Pending && snapshot.Rows.Count == 0 && content.Count > 0 ? Math.Max(summaryHeight, measuredSummary) : measuredSummary;
            summaryRect = new F5Rect(panel.X + 12, y, width - 24, summaryHeight);
            y += summaryHeight + 8;
            if (WorldMode)
            {
                BuildScopes(); ScopeRow currentScope; string current = scopesById.TryGetValue(selected, out currentScope) ? currentScope.Name : scopes[0].Name;
                var lines = BlueprintMaterialsLayout.Wrap("查看范围：" + current, width - 48, .70f, measure);
                float sh = Math.Max(lineHeight, BlueprintMaterialsLayout.BlockHeight(lines, 2) + 8);
                selector = new F5Rect(panel.X + 12, y, width - 24, sh);
                // The selector is one control even when a long instance name
                // needs several readable lines. Its label is prepared in full.
                buttons.Add(new F5Element(F5ElementKind.Button, selector, "", default(F5Size), .70f, F5Command.None));
                foreach (var line in lines) labels.Add(new F5Element(F5ElementKind.Text, line.Rect.Offset(selector.X + 8, selector.Y + 4), line.Text, line.TextSize, line.TextScale, line.Command));
                y = selector.Bottom + 8;
            }
            float contentWidth = width - 32;
            scroll = content.Prepare(snapshot, WorldMode, contentWidth, measure, host.MaterialName, host.MaterialNameRevision, renderer.Generation, scroll);
            if (WorldMode)
            {
                AddLabel("物品", panel.X + 16, y, content.RequiredX - 4, .70f);
                AddRightLabel("还需", panel.X + 16 + content.RequiredX + content.NumberWidth - 4, y, .70f);
                AddRightLabel("持有", panel.X + 16 + content.HeldX + content.NumberWidth - 4, y, .70f);
                AddRightLabel("缺少", panel.X + 16 + content.MissingX + content.NumberWidth - 4, y, .70f);
                y += lineHeight;
            }
            body = new F5Rect(panel.X + 16, y, contentWidth, Math.Max(1, panel.Bottom - 12 - y));
            // The ItemType anchor above preserves its content-space offset.
            // Actual summary glyph heights can also move the body during a
            // same-scope publication. Compensate that origin before the sole
            // final clamp; window/viewport/scale/resource moves are excluded by
            // the caller, and a new scope has already cleared retained content.
            if (preserveBodyOrigin) scroll += body.Y - previousBodyY;
            scroll = Math.Max(0, Math.Min(Math.Max(0, content.Height - body.Height), scroll));
            content.Project(body.X, body.Y - scroll, body);
            if (snapshot?.Status == BlueprintMaterialStatus.Exact && content.Count == 0) AddLabel("没有需要的材料", body.X, body.Y + 6, body.Width, .70f);
            if (dropDown)
            {
                float dropdownHeight = Math.Min(Math.Max(lineHeight + 8, scopeHeight + 8), Math.Max(lineHeight + 8, panel.Bottom - selector.Bottom - 18));
                scopePanel = F5PopupPlacement.Place(selector, selector.Width, dropdownHeight, screenWidth, screenHeight);
                scopeScroll = Math.Max(0, Math.Min(Math.Max(0, scopeHeight - dropdownHeight + 8), scopeScroll));
                int first = 0, last = scopes.Count;
                while (first < last) { int mid = (first + last) / 2; if (scopes[mid].Element.Rect.Bottom <= scopeScroll) first = mid + 1; else last = mid; }
                for (int i = first; i < scopes.Count; i++)
                {
                    var option = scopes[i];
                    var rect = option.Element.Rect.Offset(scopePanel.X + 4, scopePanel.Y + 4 - scopeScroll);
                    if (rect.Y >= scopePanel.Bottom - 4) break;
                    var element = new F5Element(option.Element.Kind, rect, option.Element.Text, option.Element.TextSize, option.Element.TextScale, option.Element.Command);
                    ScopeRow previous;
                    if (oldScopeRows.TryGetValue(option.Identity, out previous) && previous.Element.Text == element.Text && F5PopupPlacement.SameAnchor(previous.Element.Rect, rect)) element = previous.Element;
                    var projected = new ScopeRow { Identity = option.Identity, Name = option.Name, Position = option.Position, Element = element };
                    visibleScopes.Add(projected); scopeLabels.Add(element);
                }
            }
        }
        private void BuildScopes()
        {
            if (ReferenceEquals(scopedInstances, shownInstances) && scopedWidth == panel.Width && scopedResources == renderer.Generation) return;
            scopedInstances = shownInstances; scopedWidth = panel.Width; scopedResources = renderer.Generation; scopesById.Clear();
            scopes.Clear(); float y = 0;
            AddScope(0, "全部已放置（" + shownInstances.Count.ToString(CultureInfo.InvariantCulture) + " 份）", null, ref y);
            foreach (var item in shownInstances)
            {
                string suffix = item.Sequence > 0 && item.Sequence <= 20 ? char.ConvertFromUtf32(0x2460 + (int)item.Sequence - 1) : "（" + item.Sequence.ToString(CultureInfo.InvariantCulture) + "）";
                AddScope(item.ProjectionGeneration, item.Name + suffix, "放置位置（" + item.OriginX.ToString(CultureInfo.InvariantCulture) + "，" + item.OriginY.ToString(CultureInfo.InvariantCulture) + "）", ref y);
            }
            scopeHeight = y;
        }
        private void AddScope(long id, string name, string position, ref float y)
        {
            var size = shell.Layout.DynamicTextSize(name, .70f);
            // Long scope names keep their full text in the existing hint; they
            // are not material-name rows, whose full wrapped names stay visible.
            string shown = name;
            if (size.Width > panel.Width - 48)
            {
                var boundaries = JueMingR.Features.Text.TextElements.Boundaries(name); int lo = 0, hi = boundaries.Length - 1;
                while (lo < hi) { int mid = (lo + hi + 1) / 2; if (shell.Layout.DynamicTextSize(name.Substring(0, boundaries[mid]) + "…", .70f).Width <= panel.Width - 48) lo = mid; else hi = mid - 1; }
                shown = name.Substring(0, boundaries[lo]) + "…"; size = shell.Layout.DynamicTextSize(shown, .70f);
            }
            float height = Math.Max(lineHeight, size.Height + 8);
            var option = new ScopeRow { Identity = id, Name = name, Position = position == null ? null : name + "\n" + position,
                Element = new F5Element(F5ElementKind.Button, new F5Rect(0, y, panel.Width - 32, height), shown, size, .70f, F5Command.None) };
            scopes.Add(option); scopesById[id] = option; y += height;
        }
        private static bool SameRows(BlueprintMaterialSnapshot a, BlueprintMaterialSnapshot b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a == null || b == null || a.Rows.Count != b.Rows.Count) return false;
            for (int i = 0; i < a.Rows.Count; i++) if (a.Rows[i].ItemType != b.Rows[i].ItemType) return false;
            return true;
        }
        private void AddLabel(string value, float x, float y, float width, float scale)
        { var size = shell.Layout.DynamicTextSize(value, scale); labels.Add(new F5Element(F5ElementKind.Text, new F5Rect(x, y, width, size.Height), value, size, scale, F5Command.None)); }
        private void AddRightLabel(string value, float right, float y, float scale)
        { var size = shell.Layout.DynamicTextSize(value, scale); labels.Add(new F5Element(F5ElementKind.Text, new F5Rect(right - size.Width, y, size.Width, size.Height), value, size, scale, F5Command.None)); }
        internal void Draw()
        {
            if (!Visible || !ready) return;
            renderer.DrawPanel(matrix, panel, labels, buttons, hovered, armed);
            renderer.DrawContent(content, matrix, body);
            if (dropDown) renderer.DrawScope(matrix, scopePanel, scopeLabels, hovered);
            renderer.DrawHint(matrix, new F5Rect(0, 0, screen.X / matrix.M11, screen.Y / matrix.M11));
        }
        public void Dispose() { Close(); }
    }
}

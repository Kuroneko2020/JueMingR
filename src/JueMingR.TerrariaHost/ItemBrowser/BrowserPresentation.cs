using System;
using System.Collections.Generic;
using System.Linq;
using JueMingR.Features.ItemBrowser;
using JueMingR.Features.Text;
using JueMingR.Platform.ItemCatalog;
using JueMingR.TerrariaHost.F5;
using JueMingR.TerrariaHost.Input;
using JueMingR.TerrariaHost.Notes;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameInput;

namespace JueMingR.TerrariaHost.ItemBrowser
{
    internal sealed class BrowserPart
    {
        internal F5Element Element; internal int Command, Argument, Type; internal bool Enabled, Selected; internal string FullText;
    }
    // Page-local commands/geometry; the shell only supplies lifecycle and input.
    internal sealed class BrowserPresentation : ITextEditSession, IBrowserPage, IDisposable
    {
        private readonly HostItemKnowledge host;
        private readonly F5Interaction shell;
        private readonly HostInputState input;
        private readonly BrowserRenderer renderer = new BrowserRenderer();
        internal readonly TextEditInput TextInput;
        internal readonly SingleLineEditView EditView = new SingleLineEditView();
        internal readonly List<BrowserPart> Parts = new List<BrowserPart>();
        private TextEditBuffer query = NewDraft(""), locator = NewDraft("");
        private int editing, generation, skin, armedGeneration, armedButton, pageSize = 1;
        private int group { get { return host.Workspace.Group; } set { host.Workspace.Group = value; } }
        private int groupPage { get { return host.Workspace.GroupPage; } set { host.Workspace.GroupPage = value; } }
        private int detailScroll { get { return host.Workspace.DetailScroll; } set { host.Workspace.DetailScroll = value; } }
        private long revision = -1;
        private long locatorRevision = -1;
        private int locatorView, locatorOffset;
        private string candidateQuery;
        private int[] locatorCandidates = new int[0];
        private bool ready, dirty = true, priorLeft, priorRight, leftTail, rightTail;
        private BrowserPart armed;
        private BrowserPart lastHint;
        internal readonly List<F5Element> HintLines = new List<F5Element>();
        internal F5Rect HintPanel;
        private Matrix matrix;
        private int[] matches = new int[0];
        private string matchedQuery;
        private int matchedCategory; private bool matchedSort;
        private BrowserCatalog matchedCatalog;
        private BrowserCatalog candidateCatalog;
        private IReadOnlyList<ItemRelation> relations = new ItemRelation[0];
        private string shownStatus, shownLocatorStatus;
        private float row;
        internal F5Rect View, EditRect;
        internal BrowserPart Hovered;
        public bool OwnsPointer { get; private set; }
        public bool OwnsTextToken { get { return TextInput.OwnsTextToken; } }
        public bool ConsumeLeft { get; private set; }
        public bool ConsumeRight { get; private set; }
        public bool ConsumeWheel { get; private set; }
        internal Action PickRequested { get; set; }
        internal Action<string> LocateRequested { get; set; }
        internal Action<int> LocateSelectedRequested { get; set; }
        internal Action<F5Rect> ConfigurePickHotkey { get; set; }
        internal Action ClearLocator { get; set; }
        internal Func<string> LocatorStatus { get; set; }
        internal Func<IReadOnlyList<string>> LocatorDetails { get; set; }
        internal Func<long> LocatorRevision { get; set; }
#if DEBUG
        internal long LayoutBuilds, Searches;
#endif
        public TextEditBuffer Editor { get { return editing == 1 ? query : editing == 2 ? locator : null; } }
        internal BrowserPresentation(HostItemKnowledge host, F5Interaction shell, HostInputState input, INotesIme ime = null)
        {
            this.host = host; this.shell = shell; this.input = input;
            TextInput = new TextEditInput(this, new NotesClipboard(() => Main.instance.Window.Handle), ime);
        }
        private static TextEditBuffer NewDraft(string text) { return new TextEditBuffer(text, true, 96, 512, "搜索文字最多 96 个字"); }
        public bool RequestFinish() { TextInput.FinishComposition(false); if (TextInput.HasComposition) return false; if (editing == 1 && host.Workspace.Query != query.Text) { host.Workspace.Query = query.Text; host.Workspace.CatalogOffset = 0; } editing = 0; TextInput.Release(false); dirty = true; return true; }
        public void CancelEdit() { editing = 0; TextInput.Release(false); dirty = true; }
        public void PreserveUncommittedInput(TextEditBuffer editor) { }
        public void BeforeInput(bool active) { TextInput.BeforeSample(active && shell.Visible && shell.Page == 3); }
        public bool Wheel(float x, float y, int delta)
        {
            ConsumeWheel = ready && View.Contains(x, y) && delta != 0;
            if (!ConsumeWheel) return false;
            if (locatorView != 0) locatorOffset = Math.Max(0, locatorOffset - Math.Sign(delta) * 3);
            else if (View.Height >= 400 ? x > View.X + 200 : host.Workspace.Detail) detailScroll = Math.Max(0, detailScroll - Math.Sign(delta) * 3);
            else host.Workspace.CatalogOffset = Math.Max(0, Math.Min(Math.Max(0, matches.Length - 1), host.Workspace.CatalogOffset - Math.Sign(delta) * pageSize));
            dirty = true; return true;
        }
        public void Process(bool active, Vector2 pointer, bool blocked, bool geometryCurrent)
        {
            bool left = PlayerInput.MouseInfo.LeftButton == ButtonState.Pressed, right = PlayerInput.MouseInfo.RightButton == ButtonState.Pressed;
            ConsumeLeft = leftTail; ConsumeRight = rightTail; OwnsPointer = false;
            long editRevision = Editor?.Revision ?? -1;
            bool wasEditing = Editor != null || TextInput.HasComposition;
            TextInput.AfterSample(active && shell.Visible && shell.Page == 3, null, input.KeyboardSample, input.SampleFocused);
            if (editing == 1 && host.Workspace.Query != query.Text) { host.Workspace.Query = query.Text; host.Workspace.CatalogOffset = 0; dirty = true; }
            if (editRevision != (Editor?.Revision ?? -1)) dirty = true;
            if (!active || !shell.Visible || shell.Page != 3) { Suspend(); if (input.SampleFocused) { if (!left) leftTail = false; if (!right) rightTail = false; } priorLeft = left; priorRight = right; return; }
            bool resources = renderer.Refresh();
            bool current = resources && skin == renderer.Generation && ready && geometryCurrent && generation == shell.Layout.Generation && View.X == shell.X + shell.Layout.Viewport.X && View.Y == shell.Y + shell.Layout.Viewport.Y;
            OwnsPointer = current && View.Contains(pointer.X, pointer.Y);
            Hovered = current && !dirty && !blocked ? Parts.FirstOrDefault(p => p.Command != 0 && p.Enabled && p.Element.Rect.Contains(pointer.X, pointer.Y)) : null;
            if (!ReferenceEquals(lastHint, Hovered)) { lastHint = Hovered; PrepareHint(pointer); }
            if (OwnsPointer) { if (left) leftTail = true; if (right) rightTail = true; }
            if (!current || blocked) armed = null;
            if ((left && !priorLeft || right && !priorRight) && Hovered != null)
            { if (armed != null || left && right) { armed = null; } else { armed = Hovered; armedGeneration = generation; armedButton = left ? 1 : 2; } }
            if ((armedButton == 1 && !left && priorLeft || armedButton == 2 && !right && priorRight) && armed != null)
            {
                var action = armed; armed = null;
                if (ReferenceEquals(action, Hovered) && armedGeneration == generation && current && input.CanStartActions) Execute(action, armedButton == 2);
            }
            ConsumeLeft = leftTail; ConsumeRight = rightTail;
            if (!left) leftTail = false; if (!right) rightTail = false;
            priorLeft = left; priorRight = right;
            if (!wasEditing && input.Hotkeys.IsNew(27) && Editor == null) { host.Workspace.Detail = false; dirty = true; }
        }
        private void Execute(BrowserPart part, bool right)
        {
            if (!RequestFinish()) return;
            var state = host.Workspace; dirty = true;
            switch (part.Command)
            {
                case 1: editing = 1; TextInput.PrepareEditor(); break;
                case 2: editing = 2; locatorView = 2; locatorOffset = 0; TextInput.PrepareEditor(); break;
                case 3: int[] categories = { 0, 1, 2, 4, 16, 8, 32 }; state.Category = categories[(Array.IndexOf(categories, state.Category) + 1) % categories.Length]; state.CatalogOffset = 0; break;
                case 4: state.SortByName = !state.SortByName; state.CatalogOffset = 0; break;
                case 5: state.Back(); RestoreQuery(); break;
                case 6: state.Forward(); RestoreQuery(); break;
                case 7: PickRequested?.Invoke(); break;
                case 8: if (locatorView != 0) locatorView = 0; else state.Detail = !state.Detail; break;
                case 9: Navigate(state.Selected, false); break;
                case 10: Navigate(state.Selected, true); break;
                case 11: state.Kind = state.Kind == 6 ? -1 : state.Kind + 1; state.RelationOffset = detailScroll = 0; group = -1; break;
                case 12: state.CatalogOffset = Math.Max(0, state.CatalogOffset - pageSize); break;
                case 13: state.CatalogOffset = Math.Min(Math.Max(0, matches.Length - 1), state.CatalogOffset + pageSize); break;
                case 14: state.RelationOffset = Math.Max(0, state.RelationOffset - 1); detailScroll = 0; group = -1; break;
                case 15: state.RelationOffset = Math.Min(Math.Max(0, relations.Count - 1), state.RelationOffset + 1); detailScroll = 0; group = -1; break;
                case 16: host.Shops.Request(); break;
                case 17: LocateRequested?.Invoke(locator.Text); break;
                case 18: ClearLocator?.Invoke(); break;
                case 19: Navigate(part.Type, right); break;
                case 20: group = part.Argument; groupPage = 0; break;
                case 21: group = -1; break;
                case 22: groupPage = Math.Max(0, groupPage + part.Argument); break;
                case 23: detailScroll = Math.Max(0, detailScroll + part.Argument); break;
                case 24: LocateSelectedRequested?.Invoke(state.Selected); break;
                case 25: locatorView = locatorView == 1 ? 0 : 1; locatorOffset = 0; break;
                case 26: ConfigurePickHotkey?.Invoke(part.Element.Rect); break;
                case 27: LocateSelectedRequested?.Invoke(part.Type); break;
                case 28: locatorOffset = Math.Max(0, locatorOffset + part.Argument); break;
            }
        }
        internal void Navigate(int type, bool uses)
        { locatorView = 0; host.Workspace.Navigate(type, uses); dirty = true; }
        private void RestoreQuery() { query = NewDraft(host.Workspace.Query); }
        public void Suspend() { ready = OwnsPointer = false; armed = Hovered = lastHint = null; HintLines.Clear(); TextInput.Release(false); editing = 0; dirty = true; }
        public void Prepare(bool active, Matrix transform)
        {
            bool requested = active && shell.Visible && shell.Page == 3;
            host.Step(requested); if (!requested) { if (ready || editing != 0) Suspend(); return; }
            if (!renderer.Refresh()) { ready = false; return; }
            F5Rect view = shell.Layout.Viewport.Offset(shell.X, shell.Y); matrix = transform;
            var state = host.Workspace;
            if (matchedCatalog != host.Native.Catalog || matchedQuery != state.Query || matchedCategory != state.Category || matchedSort != state.SortByName)
            {
                matchedCatalog = host.Native.Catalog; matchedQuery = state.Query; matchedCategory = state.Category; matchedSort = state.SortByName;
                matches = matchedCatalog?.Search(matchedQuery, matchedCategory, matchedSort) ?? new int[0]; dirty = true;
#if DEBUG
                Searches++;
#endif
            }
            string status = host.Native.Ready ? matches.Length + " 件 · 配方 / 掉落 / 精选来源" : host.Native.Status;
            string locatorStatus = LocatorStatus?.Invoke() ?? "定位只读；最多匹配 24 种物品";
            long nextLocatorRevision = LocatorRevision?.Invoke() ?? 0;
            if (candidateQuery != locator.Text || candidateCatalog != host.Native.Catalog) { candidateQuery = locator.Text; candidateCatalog = host.Native.Catalog; locatorCandidates = string.IsNullOrWhiteSpace(locator.Text) ? new int[0] : host.Native.Catalog?.Search(locator.Text, 0, false) ?? new int[0]; dirty = true; }
            if (View.X != view.X || View.Y != view.Y || View.Width != view.Width || View.Height != view.Height || generation != shell.Layout.Generation ||
                skin != renderer.Generation || revision != host.Revision || shownStatus != status || shownLocatorStatus != locatorStatus || locatorRevision != nextLocatorRevision) dirty = true;
            locatorRevision = nextLocatorRevision;
            View = view; generation = shell.Layout.Generation; skin = renderer.Generation; revision = host.Revision; shownStatus = status; shownLocatorStatus = locatorStatus;
            row = Math.Max(30, shell.Layout.TextSize("查找", .7f).Height + 8); ready = true;
            bool rebuilt = dirty;
            if (dirty) { Build(); dirty = false; armed = null;
#if DEBUG
                LayoutBuilds++;
#endif
            }
            if (Editor != null) EditView.Prepare(Editor, TextInput.Composition, EditRect.Width - 12, shell.Layout.TextSize);
            renderer.PrepareIcons(Parts, rebuilt);
        }
        private void Build()
        {
            Parts.Clear(); var s = host.Workspace; float x = View.X, y = View.Y, w = View.Width;
            if (View.Height < row * 9 + 22) { Label("窗口过矮；请降低 UI 缩放或增大窗口后浏览物品。", x, y, w, row); return; }
            Button(1, 0, editing == 1 ? "" : (query.Text.Length == 0 ? "搜索名称 / 内部名 / #ID" : query.Text), x, y, w - 150, row);
            if (editing == 1) EditRect = new F5Rect(x, y, w - 150, row);
            Button(26, 0, "查询键", x + w - 144, y, 68, row, ConfigurePickHotkey != null);
            Button(7, 0, "点选", x + w - 70, y, 70, row, PickRequested != null); y += row + 4;
            string[] labels = { "全部", "武器", "工具", "装备", "材料", "消耗品", "可放置" }; int[] masks = { 0, 1, 2, 4, 16, 8, 32 };
            Button(3, 0, labels[Array.IndexOf(masks, s.Category)], x, y, 94, row);
            Button(4, 0, s.SortByName ? "名称排序" : "ID 排序", x + 100, y, 96, row);
            Button(5, 0, "返回", x + 202, y, 70, row, s.CanBack); Button(6, 0, "前进", x + 278, y, 70, row, s.CanForward);
            Button(8, 0, s.Detail ? "看目录" : "看详情", x + 354, y, 84, row);
            Button(16, 0, "商店", x + 444, y, w - 444, row, !host.Shops.Busy); y += row + 4;
            Label(shownStatus, x, y, w, row); y += row;
            float footer = View.Bottom - row * 2 - 8, height = footer - y - 4;
            bool narrow = View.Height < 400;
            if (locatorView != 0) LocatorPanel(new F5Rect(x, y, w, height));
            else if (!narrow) { Catalog(new F5Rect(x, y, 192, height)); Detail(new F5Rect(x + 202, y, w - 202, height)); }
            else if (s.Detail) Detail(new F5Rect(x, y, w, height)); else Catalog(new F5Rect(x, y, w, height));
            Button(2, 0, editing == 2 ? "" : locator.Text.Length == 0 ? "箱内定位：独立输入名称或 ID" : locator.Text, x, footer, w - 154, row);
            if (editing == 2) EditRect = new F5Rect(x, footer, w - 154, row);
            Button(17, 0, "定位", x + w - 148, footer, 70, row, LocateRequested != null && host.Native.Catalog != null);
            Button(18, 0, "清除", x + w - 72, footer, 72, row, ClearLocator != null);
            Button(25, 0, "定位详情 · " + shownLocatorStatus, x, footer + row + 4, w, row);
        }
        private void LocatorPanel(F5Rect bounds)
        {
            int visible = Math.Max(1, (int)((bounds.Height - row) / row));
            if (locatorView == 2)
            {
                locatorOffset = Math.Min(locatorOffset, Math.Max(0, locatorCandidates.Length - visible));
                for (int i = locatorOffset; i < locatorCandidates.Length && i < locatorOffset + visible; i++)
                { int type = locatorCandidates[i]; Add(27, 0, type, (host.Native.Catalog?.Find(type)?.Name ?? "") + "  #" + type + " · 定位此物品", bounds.X, bounds.Y + (i - locatorOffset) * row, bounds.Width, row, true, false); }
                if (locatorCandidates.Length == 0) Label("输入名称或 ID 后，可选择精确物品", bounds.X, bounds.Y, bounds.Width, row);
            }
            else
            {
                var lines = new List<Tuple<string, int, int>>(); foreach (string line in LocatorDetails?.Invoke() ?? new string[0]) AddWrapped(lines, line, bounds.Width);
                locatorOffset = Math.Min(locatorOffset, Math.Max(0, lines.Count - visible));
                for (int i = locatorOffset; i < lines.Count && i < locatorOffset + visible; i++) Label(lines[i].Item1, bounds.X, bounds.Y + (i - locatorOffset) * row, bounds.Width, row);
                if (lines.Count == 0) Label("尚无定位结果；草稿保留", bounds.X, bounds.Y, bounds.Width, row);
            }
            Button(28, -visible, "上一页", bounds.X, bounds.Bottom - row, bounds.Width / 2 - 2, row, locatorOffset > 0);
            Button(28, visible, "下一页", bounds.X + bounds.Width / 2 + 2, bounds.Bottom - row, bounds.Width / 2 - 2, row);
        }
        private void Catalog(F5Rect bounds)
        {
            int columns = Math.Max(1, (int)(bounds.Width / 46)), rows = Math.Max(1, (int)((bounds.Height - row) / 46));
            pageSize = columns * rows; int start = Math.Max(0, Math.Min(host.Workspace.CatalogOffset, Math.Max(0, matches.Length - 1)));
            for (int i = start; i < matches.Length && i < start + pageSize; i++)
            { int n = i - start; Icon(matches[i], bounds.X + n % columns * 46, bounds.Y + n / columns * 46, 42); }
            if (matches.Length == 0) Label(BrowserCatalog.ClassifyQuery(host.Workspace.Query) < 0 ? "ID 格式不正确" : "没有匹配物品", bounds.X, bounds.Y, bounds.Width, row);
            Button(12, 0, "上一页", bounds.X, bounds.Bottom - row, bounds.Width / 2 - 2, row, start > 0);
            Button(13, 0, "下一页", bounds.X + bounds.Width / 2 + 2, bounds.Bottom - row, bounds.Width / 2 - 2, row, start + pageSize < matches.Length);
        }
        private void Detail(F5Rect bounds)
        {
            var s = host.Workspace; var item = host.Native.Catalog?.Find(s.Selected); float x = bounds.X, y = bounds.Y, w = bounds.Width;
            if (item == null) { Label("点击物品查看获取与用途", x, y, w, row); return; }
            int headerStart = Parts.Count;
            Icon(item.Type, x, y, row - 2); Label(item.Name + "  #" + item.Type, x + row, y, w - row - 74, row); Button(24, 0, "定位", x + w - 70, y, 70, row, LocateSelectedRequested != null); y += row;
            Button(9, 0, "获取", x, y, 64, row, true, !s.Uses); Button(10, 0, "用途", x + 68, y, 64, row, true, s.Uses);
            string[] kinds = { "全部", "配方", "掉落", "开包", "商店", "钓鱼", "世界", "微光" };
            Button(11, 0, kinds[s.Kind + 1], x + 138, y, w - 138, row); y += row + 2;
            relations = host.Sources.Find(s.Selected, s.Uses, s.Kind, host.Native, host.Shops);
            s.RelationOffset = Math.Min(s.RelationOffset, Math.Max(0, relations.Count - 1));
            float bottom = bounds.Bottom - row;
            ItemRelation relation = relations.Count == 0 ? null : relations[s.RelationOffset];
            if (relation != null && group >= 0 && group < relation.Ingredients.Count)
            {
                // Expanded substitutes own the detail pane, leaving enough room
                // for real icons even in the accepted 297-unit compact viewport.
                Parts.RemoveRange(headerStart, Parts.Count - headerStart); y = bounds.Y; bottom = bounds.Bottom;
                var alternatives = relation.Ingredients[group]; Button(21, 0, "返回材料 · 任选一种 ×" + alternatives.Count, x, y, w, row); y += row + 2;
                int columns = Math.Max(1, (int)(w / 42)), count = Math.Max(1, (int)((bottom - y - row) / 42)) * columns;
                int first = Math.Min(groupPage * count, Math.Max(0, alternatives.Types.Count - 1));
                for (int i = first; i < alternatives.Types.Count && i < first + count; i++) Icon(alternatives.Types[i], x + (i - first) % columns * 42, y + (i - first) / columns * 42, 38);
                Button(22, -1, "上一组", x, bottom - row, w / 2 - 2, row, groupPage > 0);
                Button(22, 1, "下一组", x + w / 2 + 2, bottom - row, w / 2 - 2, row, first + count < alternatives.Types.Count);
                return;
            }
            else
            {
                // Only one relation is projected; full ingredient alternatives are
                // paged separately. Wheel offsets text/rows, never global recipes.
                var lines = new List<Tuple<string, int, int>>();
                float contentWidth = w - 34;
                AddWrapped(lines, FamilyStatus(s.Kind), contentWidth);
                if (relation == null) AddWrapped(lines, "暂无已收录关系；不等于没有来源", contentWidth);
                else
                {
                    AddWrapped(lines, relation.Title + " · " + (s.RelationOffset + 1) + "/" + relations.Count, contentWidth);
                    lines.Add(Tuple.Create((host.Native.Catalog?.Find(relation.Output)?.Name ?? "产物") + " ×" + relation.Minimum + (relation.Maximum == relation.Minimum ? "" : "–" + relation.Maximum), relation.Output, -1));
                    for (int i = 0; i < relation.Ingredients.Count; i++)
                    {
                        var ingredient = relation.Ingredients[i]; string name = ingredient.Types.Count > 1 ? "任选 " + ingredient.Label + "（" + ingredient.Types.Count + " 种）" : host.Native.Catalog?.Find(ingredient.Types[0])?.Name ?? "物品";
                        lines.Add(Tuple.Create(name + " ×" + ingredient.Count, ingredient.Types[0], ingredient.Types.Count > 1 ? i : -1));
                    }
                    if (relation.StationName.Length > 0) lines.Add(Tuple.Create("工作台：" + relation.StationName, relation.Station, -1));
                    AddWrapped(lines, relation.Conditions, contentWidth);
                }
                AddWrapped(lines, "内部名 " + item.InternalName + "；基础价值 " + item.BaseValue + " 铜币（非当前成交价）", contentWidth);
                foreach (string description in item.Description.Split('\n')) AddWrapped(lines, description, contentWidth);
                int visible = Math.Max(1, (int)((bottom - y) / row)); detailScroll = Math.Min(detailScroll, Math.Max(0, lines.Count - visible));
                for (int i = detailScroll; i < lines.Count && i < detailScroll + visible; i++)
                {
                    var line = lines[i]; float ly = y + (i - detailScroll) * row;
                    if (line.Item2 > 0) { Icon(line.Item2, x, ly, row - 2); Label(line.Item1, x + row, ly, contentWidth - row - (line.Item3 >= 0 ? 60 : 0), row); }
                    else Label(line.Item1, x, ly, contentWidth, row);
                    if (line.Item3 >= 0) Button(20, line.Item3, "替代", x + contentWidth - 56, ly, 56, row);
                }
                Button(23, -1, "↑", x + w - 72, bounds.Bottom - row, 34, row, detailScroll > 0);
                Button(23, 1, "↓", x + w - 34, bounds.Bottom - row, 34, row, detailScroll + visible < lines.Count);
            }
            Button(14, 0, "上一条", x, bounds.Bottom - row, (w - 80) / 2 - 2, row, s.RelationOffset > 0);
            Button(15, 0, "下一条", x + (w - 80) / 2 + 2, bounds.Bottom - row, (w - 80) / 2 - 2, row, s.RelationOffset + 1 < relations.Count);
        }
        private string FamilyStatus(int kind)
        {
            if (kind == 0) return host.Native.Status;
            if (kind == 1) return host.Sources.DropStatus;
            if (kind == 3) return host.Shops.Status;
            if (kind == 6) return host.Sources.ShimmerStatus;
            if (kind == 2) return "精选开包线索；1.4.5.8；仅部分宝藏袋、匣与锁盒";
            if (kind == 4) return "精选渔夫里程碑奖励；普通鱼获与其它奖励尚未收录";
            if (kind == 5) return "精选采掘与提炼线索；不表示当前世界已发现";
            return "左键获取 / 右键用途；各类资料的准备与覆盖状态见分类";
        }
        private void PrepareHint(Vector2 pointer)
        {
            HintLines.Clear(); if (Hovered == null || Hovered.Type <= 0) return;
            var item = host.Native.Catalog?.Find(Hovered.Type); if (item == null) return;
            var lines = new List<Tuple<string, int, int>>(); float width = Math.Min(410, View.Width);
            AddWrapped(lines, item.Name + " #" + item.Type, width - 16);
            AddWrapped(lines, "左键获取 / 右键用途", width - 16);
            foreach (string text in item.Description.Split('\n')) AddWrapped(lines, text, width - 16);
            AddWrapped(lines, item.InternalName + "；基础价值 " + item.BaseValue + " 铜币", width - 16);
            int count = Math.Min(lines.Count, Math.Max(1, (int)((View.Height - 16) / row)));
            float height = count * row + 12, x = Math.Min(View.Right - width, Math.Max(View.X, pointer.X + 18)), y = Math.Min(View.Bottom - height, Math.Max(View.Y, pointer.Y + 24));
            HintPanel = new F5Rect(x, y, width, height);
            for (int i = 0; i < count; i++)
            { string text = i == count - 1 && count < lines.Count ? "更多说明可在详情中滚动查看" : lines[i].Item1; HintLines.Add(new F5Element(F5ElementKind.Text, new F5Rect(x + 8, y + 6 + i * row, width - 16, row), text, shell.Layout.TextSize(text, .7f), .7f, F5Command.None)); }
        }
        private void AddWrapped(List<Tuple<string, int, int>> lines, string text, float width)
        {
            if (string.IsNullOrEmpty(text)) return;
            int[] boundaries; try { boundaries = TextElements.Boundaries(text); } catch { return; }
            int start = 0;
            for (int i = 1; i < boundaries.Length; i++)
            {
                int end = boundaries[i];
                if (shell.Layout.TextSize(text.Substring(start, end - start), .7f).Width > width - 12 && boundaries[i - 1] > start)
                { lines.Add(Tuple.Create(text.Substring(start, boundaries[i - 1] - start), 0, -1)); start = boundaries[i - 1]; }
            }
            if (start < text.Length) lines.Add(Tuple.Create(text.Substring(start), 0, -1));
        }
        private void Label(string text, float x, float y, float w, float h) { Add(0, 0, 0, text, x, y, w, h, true, false); }
        private void Button(int command, int argument, string text, float x, float y, float w, float h, bool enabled = true, bool selected = false)
        { Add(command, argument, 0, text, x, y, w, h, enabled, selected); }
        private void Icon(int type, float x, float y, float size) { Add(19, 0, type, "", x, y, size, size, true, host.Workspace.Selected == type); }
        private void Add(int command, int argument, int type, string text, float x, float y, float w, float h, bool enabled, bool selected)
        { string full = type > 0 ? host.Native.Catalog?.Find(type)?.Name ?? "" : text; text = Fit(text, w - 12);
            Parts.Add(new BrowserPart { Command = command, Argument = argument, Type = type, Enabled = enabled, Selected = selected, FullText = full,
            Element = new F5Element(command == 0 ? F5ElementKind.Text : F5ElementKind.Button, new F5Rect(x, y, w, h), text, shell.Layout.TextSize(text, .7f), .7f, F5Command.None) }); }
        private string Fit(string text, float width)
        {
            if (shell.Layout.TextSize(text, .7f).Width <= width) return text;
            int[] edges = TextElements.Boundaries(text); int lo = 0, hi = edges.Length - 1;
            while (lo < hi) { int mid = (lo + hi + 1) / 2; if (shell.Layout.TextSize(text.Substring(0, edges[mid]) + "…", .7f).Width <= width) lo = mid; else hi = mid - 1; }
            return text.Substring(0, edges[lo]) + "…";
        }
        internal string HoverText { get { if (Hovered == null) return ""; var item = host.Native.Catalog?.Find(Hovered.Type); return item == null ? Hovered.FullText : item.Name + "  #" + item.Type + " · 左键获取 / 右键用途"; } }
        public void Dispose() { Suspend(); renderer.Dispose(); }
        public void Draw() { if (ready && shell.Visible && shell.Page == 3) renderer.Draw(this, matrix); }
    }
}

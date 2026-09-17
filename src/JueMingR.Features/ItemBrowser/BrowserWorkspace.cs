using System.Collections.Generic;

namespace JueMingR.Features.ItemBrowser
{
    public sealed class BrowserWorkspace
    {
        private sealed class Position
        {
            internal string Query;
            internal int Category, CatalogOffset, RelationOffset, Selected, Kind, DetailScroll, Group, GroupPage;
            internal bool Uses, SortByName, Detail;
        }
        private readonly List<Position> history = new List<Position>();
        private int cursor;
        public string Query { get; set; } = "";
        public int Category { get; set; }
        public bool SortByName { get; set; }
        public int CatalogOffset { get; set; }
        public int RelationOffset { get; set; }
        public int Kind { get; set; } = -1;
        public int DetailScroll { get; set; }
        public int Group { get; set; } = -1;
        public int GroupPage { get; set; }
        public int Selected { get; private set; }
        public bool Uses { get; private set; }
        public bool Detail { get; set; }
        public bool CanBack { get { return cursor > 0; } }
        public bool CanForward { get { return cursor + 1 < history.Count; } }
        public BrowserWorkspace() { history.Add(Capture()); }
        private Position Capture() { return new Position { Query = Query, Category = Category, SortByName = SortByName, CatalogOffset = CatalogOffset,
            RelationOffset = RelationOffset, Kind = Kind, Selected = Selected, Uses = Uses, Detail = Detail, DetailScroll = DetailScroll, Group = Group, GroupPage = GroupPage }; }
        public void Navigate(int type, bool uses)
        {
            if (type <= 0) return;
            if (Selected == type && Uses == uses) { Detail = true; return; }
            history[cursor] = Capture();
            if (CanForward) history.RemoveRange(cursor + 1, history.Count - cursor - 1);
            Selected = type; Uses = uses; RelationOffset = 0; Kind = -1; Detail = true;
            DetailScroll = GroupPage = 0; Group = -1;
            history.Add(Capture());
            if (history.Count > 64) history.RemoveAt(0);
            cursor = history.Count - 1;
        }
        public void Back() { if (CanBack) Move(cursor - 1); }
        public void Forward() { if (CanForward) Move(cursor + 1); }
        // Only an explicit page command resets browsing. Locator expiry and
        // world changes do not own this state; Back can recover the prior view.
        public void ResetToCatalog()
        {
            if (Query == "" && Category == 0 && !SortByName && CatalogOffset == 0 && Selected == 0 && !Uses && !Detail &&
                RelationOffset == 0 && Kind == -1 && DetailScroll == 0 && Group == -1 && GroupPage == 0) return;
            history[cursor] = Capture();
            if (CanForward) history.RemoveRange(cursor + 1, history.Count - cursor - 1);
            Query = ""; Category = CatalogOffset = RelationOffset = Selected = DetailScroll = GroupPage = 0;
            SortByName = Uses = Detail = false; Kind = Group = -1;
            history.Add(Capture());
            if (history.Count > 64) history.RemoveAt(0);
            cursor = history.Count - 1;
        }
        private void Move(int target)
        {
            history[cursor] = Capture(); cursor = target; Position p = history[cursor];
            Query = p.Query; Category = p.Category; SortByName = p.SortByName; CatalogOffset = p.CatalogOffset;
            RelationOffset = p.RelationOffset; Kind = p.Kind; Selected = p.Selected; Uses = p.Uses; Detail = p.Detail;
            DetailScroll = p.DetailScroll; Group = p.Group; GroupPage = p.GroupPage;
        }
    }
}

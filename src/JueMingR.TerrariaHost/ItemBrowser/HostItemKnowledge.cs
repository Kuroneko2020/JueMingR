using System;
using JueMingR.Features.ItemBrowser;

namespace JueMingR.TerrariaHost.ItemBrowser
{
    internal sealed class HostItemKnowledge : IDisposable
    {
        internal readonly NativeItemCatalog Native = new NativeItemCatalog();
        internal readonly NativeRelationSources Sources = new NativeRelationSources();
        internal readonly NativeShopSnapshot Shops = new NativeShopSnapshot();
        internal readonly BrowserWorkspace Workspace = new BrowserWorkspace();
        internal long Revision { get { return Native.Revision + Sources.Revision + Shops.Revision; } }
        internal void Step(bool requested)
        { Native.Step(requested); Sources.Step(requested, Native); Shops.Step(requested); }
        public void Dispose() { Native.Dispose(); }
    }
}

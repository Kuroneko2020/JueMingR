using System;
using System.Collections.Generic;
using System.Diagnostics;
using JueMingR.Features.ItemBrowser;
using JueMingR.Platform.ItemCatalog;
using Terraria;

namespace JueMingR.TerrariaHost.ItemBrowser
{
    internal sealed class HostItemKnowledge : IDisposable
    {
        [Flags]
        internal enum ChatDemand { Items = 1, Recipes = 2, Sources = 4, Shops = 8 }
        internal readonly NativeItemCatalog Native = new NativeItemCatalog();
        internal readonly NativeRelationSources Sources = new NativeRelationSources();
        internal readonly NativeShopSnapshot Shops = new NativeShopSnapshot();
        internal readonly BrowserWorkspace Workspace = new BrowserWorkspace();
        private readonly Func<long> mapping;
        private long chatUntil, lastStep;
        private ChatDemand chatDemand;
        private object chatWorld;
        private bool browserDemand;
        private BrowserCatalog chatPagesCatalog;
        private readonly Dictionary<(string Filter, string Purpose, int Power, bool AliasPending), IReadOnlyList<CatalogItem>> chatPages =
            new Dictionary<(string Filter, string Purpose, int Power, bool AliasPending), IReadOnlyList<CatalogItem>>();
        private readonly Queue<(string Filter, string Purpose, int Power, bool AliasPending)> chatPageOrder =
            new Queue<(string Filter, string Purpose, int Power, bool AliasPending)>();
        internal HostItemKnowledge(Func<long> mapping = null) { this.mapping = mapping; }
        internal long Revision { get { return Native.Revision + Sources.Revision + Shops.Revision; } }

        internal void RequestChat(ChatDemand demand)
        {
            if (!ReferenceEquals(chatWorld, Main.ActiveWorldFileData)) chatDemand = 0;
            chatWorld = Main.ActiveWorldFileData;
            chatDemand |= demand;
            chatUntil = Stopwatch.GetTimestamp() + 90 * Stopwatch.Frequency;
        }

        internal IReadOnlyList<CatalogItem> MatchChatItems(string filter, string purpose, int power, bool aliasPending,
            Func<CatalogItem, bool> match)
        {
            if (!ReferenceEquals(chatPagesCatalog, Native.Catalog))
            {
                chatPagesCatalog = Native.Catalog;
                chatPages.Clear(); chatPageOrder.Clear();
            }
            var key = (filter ?? "", purpose ?? "", power, aliasPending);
            if (chatPages.TryGetValue(key, out var cached)) return cached;
            var result = new List<CatalogItem>();
            int type = BrowserCatalog.ClassifyQuery(filter);
            if (type != 0)
            {
                var item = chatPagesCatalog.Find(type);
                if (item != null && match(item)) result.Add(item);
            }
            else foreach (var item in chatPagesCatalog.ByType)
                    if (match(item)) result.Add(item);
            if (chatPages.Count >= 8) chatPages.Remove(chatPageOrder.Dequeue());
            chatPageOrder.Enqueue(key);
            return chatPages[key] = result.AsReadOnly();
        }

        internal void StepChat()
        {
            if (chatDemand != 0) StepCore();
        }

        internal void Step(bool requested)
        {
            browserDemand = requested;
            StepCore();
        }

        private void StepCore()
        {
            bool requested = browserDemand;
            if (chatDemand != 0 && (!ReferenceEquals(chatWorld, Main.ActiveWorldFileData) || Stopwatch.GetTimestamp() > chatUntil))
                chatDemand = 0;
            Native.ObserveContext();
            if (requested || chatDemand != 0)
            {
                // An open browser and a cold chat share the same real
                // sampling opportunity. An idle browser claims no work.
                long frame = mapping?.Invoke() ?? 1;
                if (mapping != null && (frame <= 0 || frame == lastStep)) return;
                lastStep = frame;
                bool needSources = (chatDemand & ChatDemand.Sources) != 0;
                bool needCatalog = (chatDemand & (ChatDemand.Items | ChatDemand.Recipes | ChatDemand.Sources)) != 0;
                Native.Step(requested || needCatalog);
                Sources.Step(requested || needSources, Native);
                if ((chatDemand & ChatDemand.Shops) != 0 && !Shops.Busy && !Shops.ChatCurrent) Shops.Request();
                Shops.Step(requested || (chatDemand & ChatDemand.Shops) != 0);
                if (Native.CatalogCurrent || Native.CaptureUnavailable) chatDemand &= ~ChatDemand.Items;
                if (Native.Ready || Native.CaptureUnavailable) chatDemand &= ~ChatDemand.Recipes;
                if (Native.Ready && Sources.Done || Native.CaptureUnavailable) chatDemand &= ~ChatDemand.Sources;
                if (Shops.ChatCurrent && !Shops.Busy) chatDemand &= ~ChatDemand.Shops;
            }
        }
        public void Dispose() { Native.Dispose(); }
    }
}

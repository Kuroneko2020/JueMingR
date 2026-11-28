using System;
using System.Collections.Generic;
using System.Linq;
using JueMingR.Platform.Companions;
using JueMingR.Platform.Llm;
using JueMingR.TerrariaHost.ItemBrowser;
using JueMingR.TerrariaHost.World;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Companions
{
    internal sealed partial class HostCompanions
    {
        internal WorldMemory SharedWorldMemory { get; set; }
        private string LastTaskMemoryShimmerReference() => SharedWorldMemory?.LastSavedShimmer();

        private bool TryTaskMemoryDestination(string reference, out Point point, out string name, out string reason)
        {
            point = default;
            name = null;
            reason = "当前世界找不到这个记住的地点。";
            if (SharedWorldMemory?.TryGetPlace(reference, out var place) != true) return false;
            if (place.Invalid) { reason = "这个地点上次复核时已经失效。"; return false; }
            point = place.Point;
            name = place.Title;
            return true;
        }

        private IEnumerable<(string Name, string Ref)> FindTaskMemoryDestinations(string filter, Point origin) =>
            SharedWorldMemory?.FindPlaces(filter, null, origin, 8).Where(place => !place.Invalid)
                .Select(place => (Name: place.Title, Ref: place.Reference)) ?? Enumerable.Empty<(string Name, string Ref)>();

        internal CompanionTaskOutcome FindWorldMemory(string filter, string kind, Point origin, int limit)
            => FindWorldMemoryPage(filter, kind, origin, limit, 0);

        private CompanionTaskOutcome FindWorldMemoryPage(string filter, string kind, Point origin, int limit, int offset)
        {
            if (SharedWorldMemory?.Ready != true) return TaskOutcome("waiting", "当前世界身份或地形尚未就绪。");
            if (!SharedWorldMemory.DocumentsLoaded) return TaskOutcome("waiting", "正在读当前世界的共享文档。");
            var results = SharedWorldMemory.FindPlaces(filter, kind, origin, limit, offset);
            string detail = results.Count == 0 ? "当前世界没有匹配的可读地点。" :
                String.Join("；", results.Select(place => place.Reference + " · " + SharedWorldMemory.DescribePlace(place)));
            return TaskOutcome("succeeded", detail + (SharedWorldMemory.DocumentIssue == null ? "" : " · " + SharedWorldMemory.DocumentIssue), results.Count,
                results.Count == 1 ? results[0].Reference : null);
        }

        internal CompanionTaskOutcome ReadWorldMemory(string reference) =>
            SharedWorldMemory?.ReadPlace(reference) ?? TaskOutcome("waiting", "当前世界文档暂未就绪。");

        internal CompanionTaskOutcome WriteWorldMemory(Guid profileId, string reference, int revision, string title, string note,
            string observedTarget, string category, Point? reportedPoint)
            => WriteWorldMemoryWithAliases(profileId, reference, revision, title, note, observedTarget, category, reportedPoint, null);

        private CompanionTaskOutcome WriteWorldMemoryWithAliases(Guid profileId, string reference, int revision, string title, string note,
            string observedTarget, string category, Point? reportedPoint, string aliases)
        {
            if (!actors.TryGetValue(profileId, out var actor) || !SupplyActorReady(actor) || SharedWorldMemory?.Ready != true)
                return TaskOutcome("waiting", "队友或当前世界尚未就绪。");
            WorldMemory.Place fact = null;
            if (!String.IsNullOrEmpty(observedTarget))
            {
                // Location authorities are never serialized. Recheck the
                // actor's real current tile before producing durable facts.
                if (actor.TaskTiles?.TryGetValue(observedTarget, out var observed) != true ||
                    (fact = SharedWorldMemory.ObservePlace(category, observed.Point)) == null)
                    return TaskOutcome("failed", "地点的现场依据已变化或不属于这名队友，请重新观察。");
                var tile = Main.tile[observed.Point.X, observed.Point.Y];
                var current = new TileFact(tile);
                if (current.Active != observed.Fact.Active || current.Type != observed.Fact.Type || current.Wall != observed.Fact.Wall ||
                    current.LiquidKind != observed.Fact.LiquidKind || (current.Liquid == 0) != (observed.Fact.Liquid == 0))
                    return TaskOutcome("failed", "旧网格观察与目前不符，未用旧证据写入。");
            }
            return SharedWorldMemory.SavePlace(reference, revision, title, note, category, reportedPoint, fact, aliases);
        }

        internal CompanionTaskOutcome QueryWorldMaterials(string layer, int type, Point origin, int radius, bool report) =>
            SharedWorldMemory?.FindMaterials(layer, type, origin, radius, report) ?? TaskOutcome("waiting", "当前世界物质索引暂未就绪。");

        private bool TryWorldMaterialOrigin(Actor actor, FishingAnchor anchor, string reference, out Point origin, out string label)
        {
            origin = default;
            label = "参考点不可用。";
            if (reference == "companion")
            {
                origin = CompanionNativeContact.Feet(actor.Body).ToTileCoordinates();
                label = "队友脚下";
                return true;
            }
            if (String.IsNullOrEmpty(reference) || reference == "here")
            {
                if (!anchor.Valid) return false;
                origin = anchor.Feet.ToTileCoordinates();
                label = "发话位置";
                return true;
            }
            if (reference.StartsWith("memory:", StringComparison.Ordinal))
                return TryTaskMemoryDestination(reference, out origin, out label, out _);
            string markerId = reference == "selected" && anchor.MarkerSession == session ? anchor.SelectedMarkerId :
                reference.StartsWith("marker:", StringComparison.Ordinal) ? reference.Substring(7) : null;
            if (markerId == null) return false;
            var marker = TaskMarkers()?.FirstOrDefault(candidate => candidate.Id == markerId);
            if (marker == null || marker.X >= Main.maxTilesX || marker.Y >= Main.maxTilesY) return false;
            origin = new Point((int)marker.X, (int)marker.Y);
            label = marker.Name;
            return true;
        }

        private string ResolveWorldMaterialType(string layer, string name, out int type)
        {
            type = -1;
            if (layer == "liquid")
            {
                switch (name.Trim().ToLowerInvariant())
                {
                    case "水": case "water": type = 0; break;
                    case "岩浆": case "熔岩": case "lava": type = 1; break;
                    case "蜂蜜": case "honey": type = 2; break;
                    case "微光": case "shimmer": type = 3; break;
                }
                return type >= 0 ? "ready" : "没有匹配的原版液体。";
            }
            if (layer != "tile" && layer != "wall") return "请选择 tile 或 wall 图格层。";
            if (ChatKnowledge == null) return "未接入原版物品目录。";
            name = name.Trim();
            bool aliasPending = NativeItemAliases.IsPending(name);
            ChatKnowledge.Native.ObserveContext();
            if (!ChatKnowledge.Native.CatalogCurrent || aliasPending)
            {
                if (ChatKnowledge.Native.CaptureUnavailable) return "当前原版物品目录不可用。";
                ChatKnowledge.RequestChat(HostItemKnowledge.ChatDemand.Items);
                return "waiting";
            }
            int Placed(Item item) => layer == "tile" ? item.createTile : item.createWall <= 0 ? -1 : item.createWall;
            var matches = ChatKnowledge.MatchChatItems(name, "material-" + layer, 0, false, item =>
                ContentSamples.ItemsByType.TryGetValue(item.Type, out var sample) && Placed(sample) >= 0 &&
                NativeKnowledgeQuery.NameMatches(sample, name));
            var exact = matches.Where(item => String.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase) ||
                String.Equals(ItemID.Search.GetName(item.Type), name, StringComparison.OrdinalIgnoreCase) ||
                NativeItemAliases.MatchesExact(Lang.GetItemName(item.Type).Key, name)).ToArray();
            var ids = (exact.Length != 0 ? exact.AsEnumerable() : matches)
                .Select(entry => Placed(ContentSamples.ItemsByType[entry.Type])).Distinct().Take(6).ToArray();
            if (ids.Length != 1) return ids.Length == 0 ? "没有找到对应的原版可放置物。" :
                "对应多个原版图格类型，请指定其中一个：" + String.Join("、", ids);
            type = ids[0];
            return "ready";
        }

        private static readonly LlmToolDefinition[] WorldMemoryTools =
        {
            ChatTool("find_world_memory", "查此存档共享地标、文档和稳定 memory: 引用，可分页。",
                "\"filter\":{\"type\":\"string\"},\"kind\":{\"type\":\"string\"},\"limit\":{\"type\":\"integer\"},\"offset\":{\"type\":\"integer\"}"),
            ChatTool("read_world_memory", "读世界地标、现场或转述状态、实际修订和备注。", "\"reference\":{\"type\":\"string\"}", ",\"required\":[\"reference\"]"),
            ChatTool("write_world_memory", "写新地点或用原修订补记。可信地点使用当事队友 observe_tiles 引用；x/y 只会标为转述。kind 可用 shimmer/ore/temple/dungeon/biome/place。",
                "\"reference\":{\"type\":\"string\"},\"revision\":{\"type\":\"integer\"},\"title\":{\"type\":\"string\"},\"note\":{\"type\":\"string\"},\"aliases\":{\"type\":\"string\"},\"observed_target\":{\"type\":\"string\"},\"kind\":{\"type\":\"string\"},\"x\":{\"type\":\"integer\"},\"y\":{\"type\":\"integer\"}"),
            ChatTool("query_world_materials", "查区域物质或最近矿点。layer 为 tile/wall/liquid；type 是图格、墙或液体 ID（不是物品 ID），可改传原版物品名称 name。reference 可用 here、companion、selected 或已知 memory:/marker:。report=true 保存报告。",
                "\"layer\":{\"type\":\"string\"},\"type\":{\"type\":\"integer\"},\"name\":{\"type\":\"string\"},\"reference\":{\"type\":\"string\"},\"radius\":{\"type\":\"integer\"},\"report\":{\"type\":\"boolean\"}")
        };

        partial void AddWorldMemoryChatTools(string playerText, List<LlmToolDefinition> tools)
        {
            if (SharedWorldMemory == null || String.IsNullOrWhiteSpace(playerText)) return;
            if (new[] { "记", "上次", "之前", "地标", "微光", "神庙", "地牢", "矿点", "矿脉", "矿物", "群系", "报告",
                "哪里", "在哪", "哪片", "分布", "物质", "shimmer", "landmark", "memory" }
                .Any(word => playerText.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0))
                tools.AddRange(WorldMemoryTools);
        }

        partial void DispatchChatWorldMemoryTool(ChatCommand command, Actor actor, ref bool dispatched)
        {
            string name = command.Call.Name;
            if (!WorldMemoryTools.Any(tool => tool.Name == name)) return;
            dispatched = true;
            var args = command.Call.Arguments;
            var anchor = command.Run.Anchor;
            var origin = (anchor.Valid ? anchor.Feet : CompanionNativeContact.Feet(actor.Body)).ToTileCoordinates();
            CompanionTaskOutcome outcome = null;
            switch (name)
            {
                case "find_world_memory":
                    if (ChatFields(args, "filter", "kind", "limit", "offset") && ChatString(args, "filter", 100, out var filter) &&
                        ChatString(args, "kind", 16, out var kind) && ReadChatInt(args, "limit", 6, 1, 16, out int limit) &&
                        ReadChatInt(args, "offset", 0, 0, 2000, out int offset))
                        outcome = FindWorldMemoryPage(filter, kind, origin, limit, offset);
                    break;
                case "read_world_memory":
                    if (ChatFields(args, "reference") && ChatString(args, "reference", 45, out var lookup) && lookup != null)
                        outcome = ReadWorldMemory(lookup);
                    break;
                case "write_world_memory":
                    if (ChatFields(args, "reference", "revision", "title", "note", "aliases", "observed_target", "kind", "x", "y") &&
                        ChatString(args, "reference", 45, out var edit) && ReadChatInt(args, "revision", 0, 0, Int32.MaxValue, out int revision) &&
                        ChatString(args, "title", 100, out var title) && ChatString(args, "note", 3000, out var note) &&
                        ChatString(args, "aliases", 400, out var aliases) &&
                        ChatString(args, "observed_target", 64, out var observed) && ChatString(args, "kind", 16, out var category) &&
                        ReadChatInt(args, "x", -1, 0, 8400, out int x) && ReadChatInt(args, "y", -1, 0, 2400, out int y) &&
                        (x < 0) == (y < 0))
                        outcome = WriteWorldMemoryWithAliases(actor.Profile.Id, edit, revision, title, note, observed, category,
                            x < 0 ? (Point?)null : new Point(x, y), aliases);
                    break;
                case "query_world_materials":
                    if (ChatFields(args, "layer", "type", "name", "reference", "radius", "report") && ChatString(args, "layer", 8, out var layer) &&
                        ChatString(args, "name", 64, out var materialName) && ChatString(args, "reference", 45, out var from) &&
                        ReadChatInt(args, "type", -1, 0, 65535, out int type) && ReadChatInt(args, "radius", 0, 0, 400, out int radius) &&
                        ChatBool(args, "report", out var report))
                    {
                        layer = layer ?? "tile";
                        if (!TryWorldMaterialOrigin(actor, anchor, from, out var referenceOrigin, out string referenceLabel))
                            outcome = TaskOutcome("failed", "本世界没有可用的指定参考点。");
                        else
                        {
                            if (!String.IsNullOrWhiteSpace(materialName) && type < 0)
                            {
                                string status = ResolveWorldMaterialType(layer, materialName, out type);
                                if (status != "ready")
                                {
                                    outcome = TaskOutcome(status == "waiting" ? "waiting" : "failed",
                                        status == "waiting" ? "原版图格名称仍在准备，请稍后查询。" : status);
                                    break;
                                }
                            }
                            var result = QueryWorldMaterials(layer, type, referenceOrigin, radius, report == true);
                            outcome = TaskOutcome(result.State, "参考点 " + referenceLabel + " (" + referenceOrigin.X + ", " + referenceOrigin.Y +
                                ")。 " + result.Detail, result.Completed, result.Value);
                        }
                    }
                    break;
            }
            if (outcome == null) { RejectChatTool(command, "世界文档参数不完整。"); return; }
            if (outcome.State == "waiting" && outcome.Value != null && SharedWorldMemory.WaitForPlace(outcome.Value, outcome.Completed, Finish)) return;
            Finish(outcome);

            void Finish(CompanionTaskOutcome result)
            {
                if (command.Cancellation.IsCancellationRequested) return;
                CompleteChatQuery(command, result.State + " · " + result.Detail +
                    (result.Value == null ? "" : " · " + result.Value), error: result.State == "failed" || result.State == "conflict");
            }
        }
    }
}

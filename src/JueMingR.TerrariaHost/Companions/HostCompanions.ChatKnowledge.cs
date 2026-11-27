using System;
using System.Collections.Generic;
using System.Linq;
using JueMingR.Features.Llm;
using JueMingR.Platform.Companions;
using JueMingR.Platform.ItemCatalog;
using JueMingR.Platform.Llm;
using JueMingR.TerrariaHost.ItemBrowser;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Companions
{
    internal sealed partial class HostCompanions
    {
        internal HostItemKnowledge ChatKnowledge { get; set; }
        private const string KnowledgeItemField = "\"item_type\":{\"type\":\"integer\",\"minimum\":1}";
        private const string KnowledgeOffsetField = "\"offset\":{\"type\":\"integer\",\"minimum\":0}," +
            "\"revision\":{\"type\":\"integer\",\"minimum\":0}";
        private static readonly LlmToolDefinition[] KnowledgeChatTools =
        {
            ChatTool("find_game_items", "Find current-version item definitions by localized name, stable English name, #type, or purpose. " +
                "Purposes include teleport, coast_teleport, home_teleport, world_spawn_teleport, heal_life, heal_mana, buff, light, bait, " +
                "fishing_rod, mine, chop, hammer, flight, grapple, weapon, armor, and place. Definitions do not imply ownership.",
                "\"filter\":{\"type\":\"string\",\"maxLength\":64},\"purpose\":{\"type\":\"string\"}," +
                "\"minimum_power\":{\"type\":\"integer\",\"minimum\":0}," + KnowledgeOffsetField),
            ChatTool("get_game_item_sources", "Read actual recipes, ingredients, alternative groups, stations, drop declarations, " +
                "shop sample or shimmer relations. Pending/partial coverage does not mean no source exists.",
                KnowledgeItemField + ",\"uses\":{\"type\":\"boolean\"}," +
                "\"kind\":{\"type\":\"string\",\"enum\":[\"recipe\",\"drop\",\"shop\",\"shimmer\",\"container\",\"fishing\",\"world\",\"other\"]}," +
                KnowledgeOffsetField, ",\"required\":[\"item_type\"]"),
            ChatTool("observe_world", "Read the actor's native biome, depth, weather, time, boss flags, and personal Angler quest."),
            ChatTool("choose_game_strategy", "Use live observations to choose a route for explore, gather, prepare_boss, fight, arena, " +
                "supplies, angler, improve, services, craft, hardmode or lunar. This recommends a route and does not launch actions. " +
                "Include quantity, destination, reference, return_destination, stay and allow_nearby only when resolved from the actual player goal. " +
                "Action can explicitly turn in an Angler quest, build an observed projection, buy, sell or heal. Buy/heal require a maximum budget. " +
                "Deliver crafted items to the owner only when requested. Boss keys include eye, brain, eater, skeletron, mechanical, destroyer, twins, prime, plantera, golem, moon_lord.",
                "\"goal\":{\"type\":\"string\"},\"boss\":{\"type\":\"string\"}," + KnowledgeItemField +
                ",\"budget_copper\":{\"type\":\"integer\",\"minimum\":0},\"need_window_seconds\":{\"type\":\"integer\",\"minimum\":1}," +
                "\"quantity\":{\"type\":\"integer\",\"minimum\":1},\"destination\":{\"type\":\"string\"}," +
                "\"reference\":{\"type\":\"string\"},\"return_destination\":{\"type\":\"string\"}," +
                "\"allow_nearby\":{\"type\":\"boolean\"},\"stay\":{\"type\":\"boolean\"}," +
                "\"action\":{\"type\":\"string\",\"enum\":[\"turn_in\",\"build\",\"buy\",\"sell\",\"heal\"]}," +
                "\"npc\":{\"type\":\"string\"},\"deliver_to_owner\":{\"type\":\"boolean\"}", ",\"required\":[\"goal\"]"),
            ChatTool("submit_strategy", "If the player explicitly wants action, submit a freshly chosen live strategy by its returned " +
                "strategy_id. Does not invent additional steps. Changed inputs require a new strategy query.",
                "\"strategy_id\":{\"type\":\"string\"}", ",\"required\":[\"strategy_id\"]")
        };

        private static string DescribeItemTeleport(int type) => type == ItemID.WormholePotion
            ? "队友身边，需多人且满足虫洞原版条件" : ItemTeleportDestination(ItemTeleportKind(type));

        private bool DispatchChatKnowledgeTool(ChatCommand command, Actor actor)
        {
            var args = command.Call.Arguments;
            switch (command.Call.Name)
            {
                case "find_game_items":
                    if (!ChatFields(args, "filter", "purpose", "minimum_power", "offset", "revision") ||
                        !ChatString(args, "filter", 64, out var filter) || !ChatString(args, "purpose", 24, out var purpose) ||
                        !NativeKnowledgeQuery.IsPurpose(purpose) || !ReadChatInt(args, "minimum_power", 0, 0, 10000, out int power) ||
                        !ReadChatInt(args, "offset", 0, 0, 10000, out int offset) ||
                        !ReadChatInt(args, "revision", -1, 0, int.MaxValue, out int itemRevision)) break;
                    if (ChatKnowledge == null) { RejectChatTool(command, "原版物品目录尚未接入当前运行。"); return true; }
                    string items = NativeKnowledgeQuery.Items(ChatKnowledge, filter?.Trim(), purpose, power, offset, itemRevision, DescribeItemTeleport);
                    CompleteChatQuery(command, items, items.Split('\n').First());
                    return true;
                case "get_game_item_sources":
                    if (!ChatFields(args, "item_type", "uses", "kind", "offset", "revision") ||
                        !ReadChatInt(args, "item_type", 0, 1, ItemID.Count - 1, out int type) || type == 0 ||
                        !ChatBool(args, "uses", out bool? uses) || !ChatString(args, "kind", 8, out var kind) ||
                        kind != null && !new[] { "recipe", "drop", "shop", "shimmer", "other", "container", "fishing", "world" }
                            .Contains(kind, StringComparer.Ordinal) ||
                        !ReadChatInt(args, "offset", 0, 0, 10000, out int sourceOffset) ||
                        !ReadChatInt(args, "revision", -1, 0, int.MaxValue, out int sourceRevision)) break;
                    if (ChatKnowledge == null) { RejectChatTool(command, "原版来源目录尚未接入当前运行。"); return true; }
                    string sources = NativeKnowledgeQuery.Sources(ChatKnowledge, type, uses == true, kind, sourceOffset, sourceRevision);
                    CompleteChatQuery(command, sources, sources.Split('\n').First());
                    return true;
                case "observe_world":
                    if (!ChatFields(args)) break;
                    CompleteChatQuery(command, ChatWorldFacts(actor, refresh: true));
                    return true;
                case "choose_game_strategy":
                    if (!ChatFields(args, "goal", "boss", "item_type", "budget_copper", "need_window_seconds", "quantity", "destination",
                        "reference", "return_destination", "allow_nearby", "stay", "action", "npc", "deliver_to_owner") ||
                        !ChatString(args, "goal", 32, out var goal) || !CompanionGameStrategy.Goals.Contains(goal) ||
                        !ChatString(args, "boss", 32, out var boss) ||
                        !ReadChatInt(args, "item_type", 0, 1, ItemID.Count - 1, out int targetItem) ||
                        !ReadChatInt(args, "budget_copper", -1, 0, int.MaxValue, out int budget) ||
                        !ReadChatInt(args, "need_window_seconds", 120, 1, 540, out int seconds) ||
                        !ReadChatInt(args, "quantity", 1, 1, 100, out int quantity) ||
                        !ChatString(args, "destination", 24, out var destination) || !ChatString(args, "return_destination", 24, out var back) ||
                        !ChatString(args, "reference", 120, out var reference) || !ChatBool(args, "stay", out bool? stay) ||
                        !ChatBool(args, "allow_nearby", out bool? allowNearby) ||
                        !ChatString(args, "action", 12, out var action) || !ChatString(args, "npc", 64, out var npc) ||
                        !ChatBool(args, "deliver_to_owner", out bool? deliver) ||
                        deliver == true && goal != "craft" ||
                        !StrategyActionFits(goal, action, targetItem, budget, args.ContainsKey("quantity")) ||
                        npc != null && (action == null || action == "build")) break;
                    var intent = new ChatStrategyGoal { Goal = goal, Boss = boss, ItemType = targetItem, Seconds = seconds,
                        Budget = budget, Quantity = quantity, Destination = destination, Reference = reference, Action = action,
                        Npc = npc, ReturnTo = back, Nearby = allowNearby == true, Stay = stay == true, DeliverToOwner = deliver == true };
                    string strategy = EvaluateChatStrategy(command.Run, actor, intent, out string publicSummary, out var steps);
                    if (steps.Count > 0 && command.Run.StrategyCandidates.Count < 8)
                    {
                        Guid id = Guid.NewGuid();
                        command.Run.StrategyCandidates.Add(id, new ChatStrategyCandidate { Intent = intent, Steps = steps });
                        strategy += "\n可提交候选 strategy_id=" + id.ToString("N") + " · " +
                            String.Join(" → ", steps.Select(step => step.Operation));
                    }
                    CompleteChatQuery(command, strategy, publicSummary);
                    return true;
                case "submit_strategy":
                    if (!ChatFields(args, "strategy_id") || !ChatString(args, "strategy_id", 32, out var token) ||
                        !Guid.TryParseExact(token, "N", out Guid candidate)) break;
                    if (!command.Run.StrategyCandidates.TryGetValue(candidate, out var issued))
                    { RejectChatTool(command, "需要本轮根据实际状态生成的可执行候选。"); return true; }
                    if (command.Run.SubmittedPlans.TryGetValue(PlanFingerprint(issued.Steps), out Guid priorTask))
                    {
                        CompleteChatQuery(command, FormatTask(GetTask(command.Run.Boundary.Owner, command.Run.Boundary.Profile, priorTask)));
                        return true;
                    }
                    EvaluateChatStrategy(command.Run, actor, issued.Intent, out string current, out var verified);
                    if (verified.Count == 0 || PlanFingerprint(verified) != PlanFingerprint(issued.Steps))
                    { RejectChatTool(command, "当前条件已变化，请重选路线。" + current); return true; }
                    SubmitChatTask(command, TaskIdentity(command.Run.Boundary), current, verified);
                    return true;
                default: return false;
            }
            RejectChatTool(command, "查询参数不完整或不是现有的游戏分类。");
            return true;
        }

        private static bool StrategyActionFits(string goal, string action, int item, int budget, bool hasQuantity)
        {
            switch (action)
            {
                case null: return true;
                case "turn_in": return goal == "angler";
                case "build": return goal == "arena";
                case "buy": return goal == "services" && item > 0 && hasQuantity && budget >= 0;
                case "sell": return goal == "services" && item > 0 && hasQuantity;
                case "heal": return goal == "services" && budget >= 0;
                default: return false;
            }
        }

        private string ChatWorldFacts(Actor actor, bool refresh)
        {
            var p = actor.Body;
            bool known = refresh ? RefreshActorEnvironment(actor) : actor.Environment.Known;
            string region = known ? String.Join("、", new[]
            {
                p.ZoneBeach ? "海岸" : "", p.ZoneJungle ? "丛林" : "", p.ZoneDungeon ? "地牢" : "",
                p.ZoneCrimson ? "猩红" : "", p.ZoneCorrupt ? "腐化" : "", p.ZoneHallow ? "神圣" : "",
                p.ZoneSnow ? "雪原" : "", p.ZoneDesert ? "沙漠" : "", p.ZoneLihzhardTemple ? "神庙" : "",
                p.ZoneSkyHeight ? "太空" : p.ZoneUnderworldHeight ? "地狱" : p.ZoneRockLayerHeight ? "洞穴层" :
                    p.ZoneDirtLayerHeight ? "地下层" : "地表"
            }.Where(value => value.Length != 0)) : "部分地形或角色环境尚未确认";
            int quest = ActorQuestType(actor);
            var feet = p.Bottom.ToTileCoordinates();
            int clock = ((int)(Main.time / 60) + (Main.dayTime ? 270 : 1170)) % 1440;
            string difficulty = Main.IsJourneyMode ? "旅途" : Main.masterMode ? (Main.getGoodWorld ? "传奇" : "大师") :
                Main.expertMode ? "专家" : "经典";
            string seeds = String.Join("、", new[] { Main.zenithWorld ? "天顶" : "", Main.remixWorld ? "颠倒" : "",
                Main.getGoodWorld ? "FTW" : "", Main.skyblockWorld ? "空岛" : "", SpecialSeedFeatures.Mechdusa ? "机械美杜莎" : ""
            }.Where(value => value.Length != 0));
            return "世界与队友观测（更新 " + Main.GameUpdateCount + "）。脚部图格 " + feet.X + "," + feet.Y + "，" + region +
                "；" + difficulty + (seeds.Length == 0 ? "" : "，特殊世界：" + seeds) +
                "；当前" + (Main.dayTime ? "白天" : "夜晚") + " " + (clock / 60).ToString("00") + ":" + (clock % 60).ToString("00") +
                "，时钟倍率 " + Main.dayRate + "，本时段游戏秒 " + (int)Main.time +
                (Main.dayTime ? "" : "，剩余夜间游戏秒 " + (int)Math.Max(0, Main.nightLength - Main.time)) +
                "；" + (Main.raining ? "下雨" : "无雨") + "，" + (p.honeyWet ? "接触蜂蜜" : p.lavaWet ? "接触熔岩" :
                    p.shimmerWet ? "接触微光" : p.wet ? "接触水体" : "未接触液体") +
                "；困难模式 " + Main.hardMode + "，骷髅王 " + NPC.downedBoss3 + "，机械 Boss " + NPC.downedMechBossAny +
                "，世纪之花 " + NPC.downedPlantBoss + "，石巨人 " + NPC.downedGolemBoss + "，月总 " + NPC.downedMoonlord +
                "；队友当前" + (quest <= 0 ? "没有已确认可接的任务鱼。" : "可接任务鱼 #" + quest + "。");
        }

        private string EvaluateChatStrategy(ChatRun run, Actor actor, ChatStrategyGoal intent, out string summary, out List<CompanionTaskStep> steps)
        {
            var p = actor.Body;
            string goal = intent.Goal, boss = intent.Boss;
            int quest = ActorQuestType(actor);
            int objective = goal == "angler" && quest > 0 ? quest : intent.ItemType;
            var inventory = p.inventory.Take(CompanionBehavior.MainInventorySlots).Where(item => item?.IsAir == false).ToArray();
            var owned = inventory.Concat(p.armor.Where(item => item?.IsAir == false)).Concat(p.miscEquips.Where(item => item?.IsAir == false));
            int ownedCount = objective <= 0 ? 0 : inventory.Where(item => item.type == objective).Sum(item => item.stack);
            int[] bossTypes = StrategyBossTypes(boss ?? (goal == "lunar" ? "moon_lord" : ""));
            bool firstVictory = FirstStrategyVictory(boss ?? (goal == "lunar" ? "moon_lord" : ""));
            bool night = boss == "skeletron" || boss == "eye" || boss == "mechanical" || boss == "twins" ||
                boss == "destroyer" || boss == "prime";
            // Game-time units are not wall seconds. The native clock rate may
            // change again during the task, so this is only the current window.
            bool tooLate = night && (Main.dayTime ||
                Main.nightLength - Main.time < 60.0 * Math.Max(0, Main.dayRate) * intent.Seconds);
            bool terrain = RefreshActorEnvironment(actor);
            var center = p.Center.ToTileCoordinates();
            bool blocked = terrain && WorldGen.InWorld(center.X, center.Y - 2, 2) && WorldGen.SolidTile(center.X, center.Y - 2);
            // The companion already has its own observed NPC table in multiplayer.
            // The owner's native table may omit threats visible to the companion.
            bool inCombat = bossTypes.Length > 0 &&
                (TaskNpcTable?.Any(npc => npc != null && bossTypes.Contains(npc.type) && TaskNpcObserved(npc)) ?? false) ||
                goal == "lunar" && NPC.MoonLordCountdown > 0;
            bool piercing = inventory.Any(item => item.damage > 0 && item.shoot > 0 &&
                ContentSamples.ProjectilesByType.TryGetValue(item.shoot, out var projectile) &&
                (projectile.penetrate == -1 || projectile.penetrate > 1));
            bool mobile = p.wingsLogic > 0 || p.rocketBoots > 0 || p.dashType > 0 ||
                p.miscEquips.Any(item => item?.IsAir == false && NativeKnowledgeQuery.Matches(item, "grapple", 0, null));
            var candidateGear = goal == "prepare_boss"
                ? inventory.FirstOrDefault(item => item.type == intent.ItemType && StrategyMobilityItem(item)) ??
                    inventory.FirstOrDefault(StrategyMobilityItem)
                : inventory.FirstOrDefault(item => intent.ItemType > 0 && item.type == intent.ItemType &&
                    (item.accessory || item.headSlot >= 0 || item.bodySlot >= 0 || item.legSlot >= 0)) ??
                    inventory.FirstOrDefault(item => !mobile && StrategyMobilityItem(item));
            var stock = FindStrategyResource(run, intent, intent.Quantity - ownedCount);
            bool nearby = stock != null;
            var craft = FindStrategyRecipe(run, intent, goal == "craft" ? intent.Quantity : intent.Quantity - ownedCount);
            StrategyDependency dependency = DependencyStatus(intent.ItemType, owned, bossTypes);
            bool redirectedMech = SpecialSeedFeatures.Mechdusa &&
                (boss == "mechanical" || boss == "twins" || boss == "destroyer" || boss == "prime");
            var facts = new CompanionStrategyFacts(Main.hardMode, firstVictory, inCombat, NPC.LunarApocalypseIsUp, night, tooLate, mobile,
                inventory.Any(item => CompanionItemRules.WeaponEligible(item)), piercing, inventory.Any(item => item.healLife > 0),
                inventory.Any(item => ItemTeleportKind(item.type) == ChatItemTeleport.Home),
                inventory.Any(item => NativeKnowledgeQuery.Matches(item, "light", 0, null)), inventory.Length < CompanionBehavior.MainInventorySlots,
                inventory.Any(item => item.bait > 0 && item.type != ItemID.TruffleWorm), inventory.Any(item => item.fishingPole > 0),
                quest > 0, ownedCount >= (goal == "angler" ? 1 : intent.Quantity), nearby || craft?.RequiresNearby == true, craft.HasValue,
                terrain, blocked, terrain && p.ZoneJungle, intent.Budget >= 0, p.statLifeMax, dependency);
            var chosen = CompanionGameStrategy.Choose(goal, boss, facts);
            var installed = new HashSet<string>(GetTaskCapabilities().Select(capability => capability.Name), StringComparer.Ordinal);
            var available = chosen.Capabilities.Where(installed.Contains);
            var absent = chosen.Capabilities.Where(capability => !installed.Contains(capability)).ToArray();
            string result = chosen.Card + " (" + chosen.Sources + ") · " + chosen.Reason + "\n候选路线：" + chosen.Route +
                "\n结束条件：" + chosen.Finish + "\n现有执行/观察能力：" + String.Join("、", available) +
                (absent.Length == 0 ? "" : "\n候选路径缺少执行定义：" + String.Join("、", absent)) +
                (dependency == StrategyDependency.Unknown ? "\n物品来源尚未完整建立；首胜路线不能将它当前置承诺。" : "") +
                (redirectedMech ? "\n当前特殊世界使用机械美杜莎规则，单个机械召唤物不可用。" : "") +
                "\n角色事实：基础生命 " + p.statLifeMax + "，装备后防御 " + p.statDefense + "，有机动 " + mobile +
                "，主包目标数量 " + ownedCount + "，授权近源可补 " + nearby + "。" +
                (craft.HasValue ? " 原版配方 #" + craft.Value.Id + " 可备产物 " + craft.Value.Production +
                    (craft.Value.RequiresNearby ? "（使用已确认的附近材料）" : "（现有材料可满足）") + "。" : "") +
                "\n" + ChatWorldFacts(actor, refresh: false);
            summary = chosen.Card + " · " + chosen.Reason;
            steps = BuildStrategySteps(intent, chosen, facts, quest, ownedCount, craft, stock, candidateGear);
            return result;
        }

        private StrategyDependency DependencyStatus(int type, IEnumerable<Item> owned, int[] bosses)
        {
            if (type <= 0) return StrategyDependency.NotSpecified;
            var counts = owned.GroupBy(item => item.type).ToDictionary(group => group.Key, group => group.Sum(item => item.stack));
            if (counts.ContainsKey(type)) return StrategyDependency.Held;
            if (ChatKnowledge == null || bosses.Length == 0) return StrategyDependency.Unknown;
            ChatKnowledge.Native.ObserveContext();
            if (!ChatKnowledge.Sources.IsCurrent(ChatKnowledge.Native) || ChatKnowledge.Sources.Drops == null)
            {
                ChatKnowledge.RequestChat(HostItemKnowledge.ChatDemand.Sources | HostItemKnowledge.ChatDemand.Shops);
                return StrategyDependency.Unknown;
            }
            // A recipe declaration is not an independent path. Walk its
            // material groups and the source of conditional container loot.
            // Cycles or unindexed routes leave an honest unknown.
            var visiting = new HashSet<int>();
            var known = new Dictionary<int, StrategyDependency>();
            int remaining = 96;
            return TraceDependency(type, 0, bosses, counts, visiting, known, ref remaining);
        }

        private StrategyDependency TraceDependency(int type, int depth, int[] bosses, Dictionary<int,int> owned,
            HashSet<int> visiting, Dictionary<int,StrategyDependency> known, ref int remaining)
        {
            if (known.TryGetValue(type, out var cached)) return cached;
            if (depth >= 8 || remaining-- <= 0 || !visiting.Add(type)) return StrategyDependency.Unknown;
            try
            {
                var host = ChatKnowledge;
                if (host.Shops.ChatCurrent && host.Shops.Index.Find(type, false).Count > 0)
                    return known[type] = StrategyDependency.AvailableWithoutTarget;
                bool gated = false, unknown = false;
                foreach (var drop in host.Sources.Drops.Find(type, false, (int)RelationKind.NpcDrop))
                {
                    string[] fields = drop.Id.Split(':');
                    if (fields.Length < 2 || !Int32.TryParse(fields[1], out int npc)) unknown = true;
                    else if (!bosses.Contains(npc)) return known[type] = StrategyDependency.AvailableWithoutTarget;
                    else gated = true;
                }
                foreach (var route in host.Native.Recipes.Find(type, false)
                    .Concat(host.Sources.Curated.Find(type, false)))
                {
                    var dependency = TraceMaterials(route, depth, bosses, owned, visiting, known, ref remaining);
                    if (dependency == StrategyDependency.AvailableWithoutTarget)
                        return known[type] = dependency;
                    if (dependency == StrategyDependency.Unknown) unknown = true;
                    else gated = true;
                }
                return known[type] = gated && !unknown ? StrategyDependency.NeedsTargetVictory : StrategyDependency.Unknown;
            }
            finally { visiting.Remove(type); }
        }

        private StrategyDependency TraceMaterials(ItemRelation route, int depth, int[] bosses, Dictionary<int,int> owned,
            HashSet<int> visiting, Dictionary<int,StrategyDependency> known, ref int remaining)
        {
            bool uncertain = false;
            if (route.Ingredients.Count == 0 && route.Kind != RelationKind.Recipe) return StrategyDependency.Unknown;
            foreach (var ingredient in route.Ingredients)
            {
                int shortage = ingredient.Count;
                foreach (int type in ingredient.Types)
                    if (owned.TryGetValue(type, out int count)) shortage -= count;
                if (shortage <= 0) continue;
                bool other = false, unknown = false;
                foreach (int alternate in ingredient.Types)
                {
                    var option = TraceDependency(alternate, depth + 1, bosses, owned, visiting, known, ref remaining);
                    if (option == StrategyDependency.AvailableWithoutTarget) { other = true; break; }
                    if (option == StrategyDependency.Unknown) unknown = true;
                }
                if (!other && !unknown) return StrategyDependency.NeedsTargetVictory;
                if (!other) uncertain = true;
            }
            return uncertain ? StrategyDependency.Unknown : StrategyDependency.AvailableWithoutTarget;
        }

        private static int[] StrategyBossTypes(string boss)
        {
            switch (boss)
            {
                case "eye": return new[] { (int)NPCID.EyeofCthulhu };
                case "brain": return new[] { (int)NPCID.BrainofCthulhu, NPCID.Creeper };
                case "eater": return new[] { (int)NPCID.EaterofWorldsHead, NPCID.EaterofWorldsBody, NPCID.EaterofWorldsTail };
                case "skeletron": return new[] { (int)NPCID.SkeletronHead };
                case "mechanical": return new[] { (int)NPCID.TheDestroyer, NPCID.Retinazer, NPCID.Spazmatism, NPCID.SkeletronPrime };
                case "destroyer": return new[] { (int)NPCID.TheDestroyer };
                case "twins": return new[] { (int)NPCID.Retinazer, NPCID.Spazmatism };
                case "prime": return new[] { (int)NPCID.SkeletronPrime };
                case "plantera": return new[] { (int)NPCID.Plantera };
                case "golem": return new[] { (int)NPCID.Golem };
                case "moon_lord": return new[] { (int)NPCID.MoonLordCore };
                default: return Array.Empty<int>();
            }
        }

        private static bool FirstStrategyVictory(string boss)
        {
            switch (boss)
            {
                case "eye": return !NPC.downedBoss1;
                case "brain": case "eater": return !NPC.downedBoss2;
                case "skeletron": return !NPC.downedBoss3;
                case "mechanical": return !NPC.downedMechBossAny;
                case "destroyer": return !NPC.downedMechBoss1;
                case "twins": return !NPC.downedMechBoss2;
                case "prime": return !NPC.downedMechBoss3;
                case "plantera": return !NPC.downedPlantBoss;
                case "golem": return !NPC.downedGolemBoss;
                case "moon_lord": return !NPC.downedMoonlord;
                default: return false;
            }
        }
    }
}

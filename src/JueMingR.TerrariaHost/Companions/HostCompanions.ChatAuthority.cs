using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using JueMingR.Platform.Companions;
using JueMingR.TerrariaHost.ItemBrowser;
using Terraria;
using Terraria.Enums;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Companions
{
    internal sealed partial class HostCompanions
    {
        // Only the freshly sent player text grants authority. Model output,
        // tool descriptions, history, and observed object names never do.
        // This recognizes a small number of direct command phrases. Ambiguous
        // sentences and unrecognized wording leave game tools read-only.
        private sealed class ChatAuthority
        {
            internal const string Denied = "本条消息没有明确委托这项游戏操作。此次请求未提交。";
            private readonly string[] clauses;
            private readonly int budget;
            private readonly Cost committed = new Cost();
            internal sealed class Cost
            {
                internal long Copper;
                internal string Denial = Denied;
                internal readonly Dictionary<(string Domain, int Item), long> Items = new Dictionary<(string, int), long>();
            }
            private static readonly Regex Sentence = new Regex(@"[^。！？?!；;\r\n]+[。！？?!；;]?", RegexOptions.CultureInvariant);
            private static readonly Regex ReadOnly = new Regex(
                @"^(?:请问|想知道|我想知道|去查|去看一下|查|看看|看一下|告诉我|说明|介绍|如果|假如|假设|例如|比如|只是|先别|别|不要|不许|无需|不必|不用|能否|能不能|可以吗|是否|怎么|怎样|如何|要不要|会不会|为什么|什么|how\b|what\b|why\b|if\b|suppose\b|tell me\b|show me\b|check\b|do not\b|don't\b|can\b|could\b|would\b)",
                RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
            private static readonly Regex Prefix = new Regex(
                @"^(?:(?:那你|那么你|那就|请|麻烦|拜托|帮我|让队友|你先|你|先|然后|再次|再|就|现在|直接|实际|可以|please\b|then\b|go and\b)\s*)+",
                RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
            private static readonly Regex Question = new Regex(@"怎么|如何|怎样|是否|有哪些|有没有|能不能|会不会|是不是|要不要|有什么",
                RegexOptions.CultureInvariant);
            private static readonly Regex Hypothesis = new Regex(
                @"^(?:如果|假如|假设|要是|例如|比如|if\b|suppose\b|for example\b)", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
            private static readonly Regex Negative = new Regex(@"不要(?!超过)|不允许|不许|不准|不能|禁止|别动|别用|别拿|无需|不必|\bdo not\b|\bdon't\b|\bwithout\b",
                RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
            private static readonly Regex BudgetPhrase = new Regex(
                @"(?:预算(?:是|为)?|花费上限|最多|不超过|at most\b|up to\b|budget(?: of)?\b)\s*[:：]?\s*((?:[0-9一二两三四五六七八九十百零]+\s*(?:个)?(?:铂金币|金币|银币|铜币|铂金|金|银|铜|platinum|gold|silver|copper)(?:\s+coins?)?\s*)+)",
                RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
            private static readonly Regex Currency = new Regex(
                @"([0-9一二两三四五六七八九十百零]+)\s*(?:个)?(铂金币|金币|银币|铜币|铂金|金|银|铜|platinum|gold|silver|copper)",
                RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
            private const string QuantityPattern = @"(?<count>[0-9一二两三四五六七八九十百零]+)\s*(?:个|块|根|支|瓶|颗|份|只|条|组)?\s*";
            private static readonly Regex OwnerTake = new Regex(
                @"^(?:从我(?:的)?(?:背包|身上)\s*(?:拿|取出?|搬|转移)\s*" + QuantityPattern + @"(?<name>[^。！？]+?)(?:给队友|给你|到队友背包)?|"
                    + @"(?:take|transfer|withdraw)\s+" + QuantityPattern + @"(?<name>.+?)\s+from my (?:inventory|backpack))[。.!]?$",
                RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
            private static readonly Regex NamedUse = new Regex(QuantityPattern + @"(?<name>[^。！？]+?)[。.!]?$",
                RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
            private static readonly Regex Sale = new Regex(@"^(?:去)?(?:卖(?:掉|出)?|出售|sell)\s*" + QuantityPattern +
                @"(?<name>[^。！？]+?)(?:给商人)?[。.!]?$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
            private static readonly Regex Coast = new Regex(@"^(?:去|到|在)?(?:左|右)?海(?:岸|边)(?:就)?(?<action>钓|等|待命)",
                RegexOptions.CultureInvariant);
            private static readonly Regex RadiusPhrase = new Regex(@"(?:半径|周围|附近)\s*(?<count>[0-9一二两三四五六七八九十百零]+)\s*(?:图格|格)",RegexOptions.CultureInvariant);
            private static readonly Regex WorkCount = new Regex(@"(?<count>[0-9一二两三四五六七八九十百零]+)\s*(?:块|格|面|棵|根|件)",RegexOptions.CultureInvariant);
            private static readonly Regex SpatialWork = new Regex(
                @"^(?:前往|去|到|在)\s*(?<site>.+?)\s*(?:帮我|给我)?\s*(?<verb>开采|采矿|挖|安放|放|摆|铺|拆墙|敲墙|砍|锤|涂|布线|倒水|舀)(?<object>.*)$",
                RegexOptions.CultureInvariant);

            internal ChatAuthority(string raw)
            {
                var statements = Sentence.Matches(raw).Cast<Match>().Select(match => match.Value.Trim())
                    .Where(line => !line.EndsWith("?", StringComparison.Ordinal) && !line.EndsWith("？", StringComparison.Ordinal) &&
                        !line.EndsWith("吗", StringComparison.Ordinal) && !line.EndsWith("吗。", StringComparison.Ordinal) &&
                        !Hypothesis.IsMatch(Prefix.Replace(line, "")));
                var asserted = statements.SelectMany(line => line.Split('，', ',', '、')).Select(line => line.Trim())
                    .Where(line => line.Length > 0 && !ReadOnly.IsMatch(line)).ToArray();
                clauses = asserted.Select(line => Prefix.Replace(line, ""))
                    .Where(line => !ReadOnly.IsMatch(line) && !Question.IsMatch(line) && !Negative.IsMatch(line)).ToArray();
                budget = ReadBudget(asserted);
            }

            private bool Starts(params string[] words) => clauses.Any(clause => words.Any(word => StartsWith(clause, word)));

            private static bool StartsWith(string clause, string word) =>
                clause.StartsWith(word, StringComparison.OrdinalIgnoreCase) &&
                (word[0] > 127 || clause.Length == word.Length || !Char.IsLetterOrDigit(clause[word.Length]));

            private bool Contains(params string[] words) =>
                clauses.Any(clause => words.Any(word => !String.IsNullOrEmpty(word) &&
                    clause.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0));

            internal bool Stop => Starts("停", "取消", "结束", "收线", "stop", "cancel", "end");
            internal bool Pause(bool paused) => paused ? Starts("暂停", "先停一下", "pause") : Starts("恢复", "继续", "resume", "continue");
            private bool CoastAction(string action) => clauses.Any(clause => Coast.Match(clause).Groups["action"].Value == action);
            private bool Fish => Starts("钓", "fish", "cast", "去钓", "到这里钓") || CoastAction("钓") ||
                Contains("改成钓鱼", "到了就钓", "到了钓", "在这里钓", "在这钓", "开始钓");
            private bool Move => Starts("去", "到", "回", "走", "跟", "带路", "go", "move", "walk", "return", "follow", "come") ||
                Contains("停止后回来", "停下后回来", "完成后回来", "结束后回来", "回来跟我", "回来跟着我");
            private bool Wait => Starts("等", "待命", "留在", "站着", "wait", "stay", "stand by") || CoastAction("等") || CoastAction("待命") ||
                Contains("在这里等", "在那里等", "位置等我", "到达后等", "到达后待命", "回来等我");
            private bool Attack => Starts("攻击", "打", "战斗", "守", "保卫", "attack", "fight", "guard", "defend");
            private bool ClearArea => Contains("清场","clear out") || Contains("怪","敌人","生物") &&
                Contains("清掉","清理","杀了","杀掉","打掉");
            private bool IncludeFriendly => Contains("包括友好","连同友好","连友好","也打友好","including friendly");
            private bool Mine => Starts("挖","采矿","开采","mine","dig") || Move && Contains("挖","采矿","开采") ||
                Starts("用","使用") && Contains("挖掉","挖开","开采") || HasSpatialWork("挖","采矿","开采");
            private bool Prepare => Starts("准备打", "准备挑战", "备战", "prepare for");
            private bool Use => Starts("用", "使用", "喝", "吃", "挥", "丢", "投", "use", "drink", "eat", "swing", "consume") ||
                Contains("连续用", "连续使用");
            private bool Summon => Starts("召唤", "开启事件", "触发", "开血月", "开南瓜月", "summon", "trigger");
            private bool Deliver => Starts("给我", "交给我", "送给我", "give", "deliver") || Contains("成品给我", "成品交给我", "交给我", "带给我");
            private bool Transfer => Deliver || Starts("转移", "搬", "存", "取", "拿", "从", "transfer", "store", "take", "withdraw");
            private bool SpendNearby => Contains("附近箱", "旁边的箱", "附近储存", "nearby chest", "nearby storage");
            internal bool Recall => (Starts("召回", "传送到我", "回我身边", "recall") || Use) && Contains("虫洞", "wormhole");
            internal bool FishActivity => Fish;
            internal bool ReferencesHeld => Contains("这件物品", "这件东西", "手里这件", "手上这件", "this item");
            internal bool NamesActorHand => Contains("你手里", "你手上", "你拿着", "you are holding");
            internal bool ReferencesLastShimmer => Contains("上次那个微光", "上次的微光", "之前那个微光", "last shimmer");
            internal bool ReturnsToOwner => Contains("回来跟我","回来跟着我","完成后回来","挖完回来");
            internal ChatWorldSite WorldSite;
            internal string PriorShimmer;
            internal Microsoft.Xna.Framework.Point? PriorShimmerPoint;

            internal string WorkLocation()
            {
                foreach (var clause in clauses)
                {
                    var spatial=SpatialWork.Match(clause);
                    if (spatial.Success) return spatial.Groups["site"].Value.Trim().Trim('“','”','"','「','」','『','』');
                }
                // A comma may separate the travel clause from the mining verb.
                if (Mine)
                    foreach (var clause in clauses)
                        if (clause.StartsWith("去",StringComparison.Ordinal) && clause.EndsWith("矿点",StringComparison.Ordinal))
                            return clause.Substring(1).Trim();
                return null;
            }

            private bool HasSpatialWork(params string[] verbs) => clauses.Any(clause=>verbs.Contains(SpatialWork.Match(clause).Groups["verb"].Value));

            private string SpatialPlacementMaterial()
            {
                foreach (var clause in clauses)
                {
                    var spatial=SpatialWork.Match(clause);
                    if (!spatial.Success || !new[]{"放","摆","铺","安放"}.Contains(spatial.Groups["verb"].Value)) continue;
                    return NamedUse.Match(spatial.Groups["object"].Value.Trim()).Groups["name"].Value;
                }
                return null;
            }

            internal void PreparePlacementName()
            {
                string material=SpatialPlacementMaterial();
                if (!String.IsNullOrEmpty(material)) NativeItemAliases.IsPending(material);
            }

            private bool AuthorizedPlacementMaterial(IReadOnlyDictionary<string,object> args)
            {
                string material=SpatialPlacementMaterial();
                return material==null || args.TryGetValue("item_type",out var raw) && raw is int id && NameMatches(id,material);
            }

            internal bool Setting(string label,int value)
            {
                if (value==0) return HasSettingDirective(label,"关闭","关掉","关上","关了","停用","禁用");
                if (label=="生命恢复") return value==2?HasSettingDirective(label,"设为智能","改为智能","切到智能"):
                    HasSettingDirective(label,"设为快速","改为快速","切到快速");
                if (label=="自动存放鱼") return value==2?HasSettingDirective(label,"设为任务鱼","改为任务鱼","只存任务鱼"):
                    HasSettingDirective(label,"设为所有","改为所有","存放所有");
                return HasSettingDirective(label,"开启","打开","启用","设为普通","改为普通");
            }

            private bool HasSettingDirective(string label,params string[] verbs) => clauses.Any(clause =>
                clause.IndexOf(label,StringComparison.Ordinal)>=0 &&
                (StartsWith(clause,"以后") || StartsWith(clause,"把") || StartsWith(clause,label) ||
                 verbs.Any(verb=>StartsWith(clause,verb))) &&
                verbs.Any(verb=>clause.IndexOf(verb,StringComparison.Ordinal)>=0));
            internal bool Mode(string movement, bool? attack) => (movement == null || movement == "follow" && Move ||
                movement == "stay" && Wait) && (!attack.HasValue ||
                (attack.Value ? Attack : Stop || Starts("停火", "cease fire", "hold fire")));

            internal bool Item(int type)
            {
                if (!ContentSamples.ItemsByType.TryGetValue(type, out var item) ||
                    !(Use || !item.consumable && ItemTeleportKind(type) != ChatItemTeleport.None &&
                        Move && Contains("回家", "出生点", "go home", "spawn"))) return false;
                if (EventLabel(type) != null || type == ItemID.MechdusaSummon) return false;
                // Rare consumables and permanent upgrades need the actual
                // item name, rather than an inferred item or substitute.
                return !IsRareUse(item) ||
                    Contains(item.Name, ItemID.Search.GetName(type)) ||
                    clauses.Any(clause => NameMatches(type, NamedUse.Match(clause).Groups["name"].Value)) ||
                    type == ItemID.LifeCrystal && Contains("生命水晶", "life crystal") ||
                    type == ItemID.ManaCrystal && Contains("魔力水晶", "mana crystal") ||
                    type == ItemID.LifeFruit && Contains("生命果", "life fruit");
            }

            private static bool IsRareUse(Item item) => item.consumable && (item.rare >= (int)ItemRarityColor.LightRed4 ||
                item.type == ItemID.LifeCrystal || item.type == ItemID.LifeFruit || item.type == ItemID.ManaCrystal);

            private static bool NameMatches(int type, string name)
            {
                if (String.IsNullOrWhiteSpace(name) || type <= 0 || type >= ItemID.Count) return false;
                name = name.Trim();
                return name == "#" + type.ToString(CultureInfo.InvariantCulture) ||
                    String.Equals(Lang.GetItemNameValue(type), name, StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(ItemID.Search.GetName(type), name, StringComparison.OrdinalIgnoreCase) ||
                    NativeItemAliases.MatchesExact(Lang.GetItemName(type).Key, name) ||
                    type == ItemID.LifeCrystal && String.Equals(name, "生命水晶", StringComparison.Ordinal) ||
                    type == ItemID.ManaCrystal && String.Equals(name, "魔力水晶", StringComparison.Ordinal) ||
                    type == ItemID.LifeFruit && String.Equals(name, "生命果", StringComparison.Ordinal);
            }

            private bool Reserve(Cost cost, string domain, int type, long amount, int cap)
            {
                var key = (domain, type);
                cost.Items.TryGetValue(key, out long current);
                committed.Items.TryGetValue(key, out long prior);
                if (amount <= 0 || prior + current + amount > cap) return false;
                cost.Items[key] = current + amount;
                return true;
            }

            private int AuthorizedCount(Regex pattern, int type, int fallback = 0)
            {
                int answer = fallback;
                foreach (string clause in clauses)
                {
                    var match = pattern.Match(clause);
                    if (!match.Success || !NameMatches(type, match.Groups["name"].Value)) continue;
                    answer = Math.Max(answer, CoinNumber(match.Groups["count"].Value));
                }
                return answer;
            }

            internal bool Plan(IReadOnlyList<CompanionTaskStep> steps, out Cost cost)
            {
                cost = new Cost();
                foreach (var step in steps)
                {
                    if (step.Each.Count == 0)
                    {
                        if (!Step(step.Operation, step.Arguments, step.Repeat, cost)) return false;
                    }
                    else foreach (var binding in step.Each)
                        if (!Step(step.Operation, MergeTaskArguments(step.Arguments, binding), 1, cost)) return false;
                }
                if (Mine && steps.Any(step=>step.Operation=="mine_area" || step.Operation=="world_use"))
                {
                    if (Contains("在那里等","在那等","留在那里") && steps.Last().Operation!="stand_by") return false;
                    if (Contains("回来跟我","回来跟着我") && (steps.Count<2 || steps.Last().Operation!="follow_owner" ||
                        steps[steps.Count-2].Operation!="move_to" || !steps[steps.Count-2].Arguments.TryGetValue("destination",out var back) ||
                        (back as string)!="owner")) return false;
                }
                return cost.Copper == 0 || budget >= 0 && cost.Copper + committed.Copper <= budget;
            }

            internal void Commit(Cost cost)
            {
                committed.Copper += cost.Copper;
                foreach (var item in cost.Items)
                {
                    committed.Items.TryGetValue(item.Key, out long count);
                    committed.Items[item.Key] = count + item.Value;
                }
            }

            private bool Step(string operation, IReadOnlyDictionary<string, object> args, int repeats, Cost cost)
            {
                switch (operation)
                {
                    case "move_to": return Move || Fish || Prepare;
                    case "follow_owner": return Move;
                    case "stand_by": return Wait;
                    case "fish_at": return Fish;
                    case "attack_npc": return Attack && !ClearArea;
                    case "guard": return Attack && !ClearArea && RadiusWithin(args,"leash_tiles");
                    case "clear_area":
                        return ClearArea && repeats==1 && RadiusWithin(args,"radius",require:true) &&
                            Flag(args,"include_friendly")==IncludeFriendly && Reserve(cost,"area clear",0,1,1);
                    case "cease_fire": return Stop || Starts("停火", "cease fire", "hold fire");
                    case "recall_to_owner": return Recall && Reserve(cost, "recall", 0, repeats, 1);
                    case "use_item":
                        return args.TryGetValue("item_type", out var item) && item is int type && Item(type) &&
                            (!IsRareUse(ContentSamples.ItemsByType[type]) || Reserve(cost, "consume", type, repeats, AuthorizedCount(NamedUse, type, 1)));
                    case "summon_duke": return Summon && Contains("猪鲨", "duke fishron") && Reserve(cost, "event", 0, repeats, 1);
                    case "trigger_event": return Summon && Event(args) && Reserve(cost, "event", 0, repeats, 1);
                    case "transfer_item":
                        if (!args.TryGetValue("source", out var source) || !(source is string from) ||
                            !args.TryGetValue("destination", out var destination) || !(destination is string to)) return false;
                        if (!Transfer)
                            return Prepare && from.StartsWith("self_", StringComparison.Ordinal) && to == "self_inventory" && PreparationSupply(args);
                        if (from == "owner_inventory" &&
                            (!args.TryGetValue("item_type", out var transferred) || !(transferred is int transferType) ||
                             !args.TryGetValue("amount", out var quantity) || !(quantity is int requested) ||
                             !Reserve(cost, "owner", transferType, (long)repeats * requested, AuthorizedCount(OwnerTake, transferType))))
                        {
                            cost.Denial = "本条消息没有明确委托取用这一物品或数量。请写明主人背包中的原版物品名和最多可取数量。";
                            return false;
                        }
                        return !from.StartsWith("$", StringComparison.Ordinal) && !to.StartsWith("$", StringComparison.Ordinal) &&
                            (to != "owner_inventory" || Deliver);
                    case "equip_item": case "unequip_item": case "switch_loadout":
                        return Starts("装上", "装备", "换上", "卸下", "切换配装", "equip", "unequip", "switch loadout") ||
                            operation == "equip_item" && Prepare;
                    case "craft_item":
                        return (Starts("做", "制作", "合成", "制造", "craft", "make") ||
                            (Use || Transfer) && Contains("制作", "合成", "craft") || Prepare && PreparationSupply(args)) &&
                            (!args.TryGetValue("allow_nearby", out var nearby) || nearby is bool permitted && (!permitted || SpendNearby));
                    case "buy_item": return Starts("买", "购买", "buy", "purchase") && Spend(args, repeats, cost);
                    case "sell_item":
                        // Sale can permanently remove a companion asset. The
                        // NPC payout does not authorize changing type or selling more.
                        if (!args.TryGetValue("item_type", out var sold) || !(sold is int saleType) ||
                            !args.TryGetValue("amount", out var saleAmount) || !(saleAmount is int sellCount) ||
                            !Reserve(cost, "sale", saleType, (long)sellCount * repeats, AuthorizedCount(Sale, saleType)))
                        {
                            cost.Denial = "本条消息没有明确委托出售这一物品或数量。请写明原版物品名和最多可卖数量。";
                            return false;
                        }
                        return true;
                    case "reforge_item": return Starts("重铸", "重锻", "reforge") && Spend(args, repeats, cost);
                    case "nurse_heal": return Starts("治疗", "找护士", "去找护士", "去护士", "heal", "visit the nurse") && Spend(args, repeats, cost);
                    case "turn_in_angler": return Starts("交任务", "交鱼", "提交", "找渔夫", "turn in") || Contains("交给渔夫", "提交给渔夫");
                    case "build_projection": return Starts("建", "施工", "照蓝图", "按蓝图", "build", "construct");
                    case "mine_area": return AuthorizedMining(args,repeats,cost);
                    case "world_use":
                        return args.TryGetValue("action",out var action) && action is string verb && World(verb) &&
                            (verb!="place_tile" && verb!="place_wall" || AuthorizedPlacementMaterial(args)) &&
                            Reserve(cost,"world edit",0,repeats,RequestedNumber(WorkCount,1));
                    case "open_container":
                        return Starts("打开", "开袋", "开匣", "拆袋", "open") &&
                            args.TryGetValue("item_type", out var container) && container is int containerType &&
                            args.TryGetValue("amount", out var number) && number is int count &&
                            Reserve(cost, "container", containerType, (long)count * repeats,
                                AuthorizedCount(NamedUse, containerType, NamedWithoutCount(containerType) ? 1 : 0)) &&
                            (!args.TryGetValue("allow_key", out var key) || key is bool useKey && (!useKey || Contains("钥匙", "key")));
                    case "extract_item": return Starts("提炼", "萃取", "extract");
                    case "shimmer_item": return AuthorizedShimmer(args,repeats,cost);
                    case "use_furniture": return Starts("坐", "睡", "起身", "设置床", "sit", "sleep", "set spawn", "stand up");
                    case "catch_critter": return Starts("捕捉", "捉", "抓", "catch");
                    default: return false;
                }
            }

            private static bool Flag(IReadOnlyDictionary<string,object> args,string name) =>
                args.TryGetValue(name,out var value) && value is bool flag && flag;

            private bool AuthorizedShimmer(IReadOnlyDictionary<string,object> args,int repeats,Cost cost)
            {
                if (repeats!=1 || !(Starts("微光","投入微光","丢进微光","shimmer") || Move && Contains("微光") &&
                    Contains("丢进去","投进去","处理这件")) || !args.TryGetValue("item_type",out var item) || !(item is int type) ||
                    !args.TryGetValue("amount",out var raw) || !(raw is int count)) return false;
                if (ReferencesHeld) return count==1 && Flag(args,"held_at_send") && Reserve(cost,"held shimmer",0,1,1);
                return !Flag(args,"held_at_send") && Reserve(cost,"shimmer",type,count,
                    AuthorizedCount(NamedUse,type,Contains(Lang.GetItemNameValue(type),ItemID.Search.GetName(type))?1:0));
            }

            private int RequestedNumber(Regex pattern,int fallback)
            {
                foreach(string clause in clauses)
                {
                    var match=pattern.Match(clause);
                    if (match.Success) return CoinNumber(match.Groups["count"].Value);
                }
                return fallback;
            }

            private bool RadiusWithin(IReadOnlyDictionary<string,object> args,string name,bool require=false)
            {
                int radius=RequestedNumber(RadiusPhrase,-1);
                return args.TryGetValue(name,out var raw) ? raw is int requested && (radius<0 ? !require && requested<=18 : requested==radius) :
                    radius<0 && !require;
            }

            private bool AuthorizedMining(IReadOnlyDictionary<string,object> args,int repeats,Cost cost)
            {
                if (!Mine || repeats!=1 || !args.TryGetValue("amount",out var raw) || !(raw is int amount) || amount!=RequestedNumber(WorkCount,1) ||
                    !args.TryGetValue("tile_type",out var tile) || !(tile is int tileType)) return false;
                bool collect=args.TryGetValue("mode",out var mode) && (mode as string)=="collect";
                bool wantsBlocks=Contains("挖掉","破坏","拆掉","清掉") && !Contains("带回","拿到","收集","入包");
                if (collect==wantsBlocks) return false;
                if (collect && (!args.TryGetValue("product_type",out var item) || !(item is int product) ||
                    !MiningProductMatches(tileType,product) || !Contains(Lang.GetItemNameValue(product),ItemID.Search.GetName(product),
                        product==ItemID.CopperOre ? "铜矿" : null))) return false;
                return Reserve(cost,"world edit",0,amount,amount) &&
                    (!args.TryGetValue("radius",out var radius) || radius is int r && r<=RequestedNumber(RadiusPhrase,8));
            }

            private bool NamedWithoutCount(int type) => clauses.Any(clause => new[] { "打开", "开袋", "开匣", "拆袋", "open" }
                .Any(prefix => StartsWith(clause, prefix) && NameMatches(type, clause.Substring(prefix.Length).Trim().TrimEnd('。', '.'))));

            private static bool PreparationSupply(IReadOnlyDictionary<string, object> args) =>
                args.TryGetValue("item_type", out var value) && value is int type &&
                ContentSamples.ItemsByType.TryGetValue(type, out var item) && item.rare < (int)ItemRarityColor.LightRed4 &&
                EventLabel(type) == null && (item.healLife > 0 || item.healMana > 0 || item.ammo > 0);

            private bool Event(IReadOnlyDictionary<string, object> args)
            {
                if (args.TryGetValue("event", out var natural))
                    return natural is string name && (name == "martian" && Contains("火星", "martian") ||
                        name == "empress" && Contains("光之女皇", "光女", "empress"));
                return args.TryGetValue("item_type", out var item) && item is int type &&
                    EventLabel(type) is string label && Contains(label, ItemID.Search.GetName(type));
            }

            private bool World(string action)
            {
                switch (action)
                {
                    case "mine": return Mine;
                    case "axe": return Starts("砍","伐","cut") || Move && Contains("砍树","伐木");
                    case "hammer": return Starts("敲","锤","hammer");
                    case "remove_wall": return Starts("拆墙","拆这段墙","敲墙","去掉墙","remove wall");
                    case "place_tile": case "place_wall":
                        return Starts("放", "摆", "铺", "建", "搭", "place", "build", "lay") || HasSpatialWork("放","摆","铺","安放");
                    case "paint_tile": case "paint_wall": case "coat_tile": case "coat_wall":
                        return Starts("涂", "染", "上漆", "paint", "coat");
                    case "scrape_tile": case "scrape_wall": return Starts("刮", "去漆", "scrape");
                    case "place_wire": case "place_actuator": return Starts("布线", "接线", "安装制动器", "wire", "install actuator");
                    case "cut_wire": return Starts("剪线", "剪电线", "拆线路", "cut wire");
                    case "actuate": case "switch": return Starts("拨", "拉开关", "启动制动器", "flip", "actuate", "toggle");
                    case "liquid": return Starts("倒水", "舀", "排水", "灌", "倒熔岩", "倒蜂蜜", "pour", "drain", "fill");
                    default: return false;
                }
            }

            private bool Spend(IReadOnlyDictionary<string, object> args, int repeats, Cost cost)
            {
                if (budget < 0 || !args.TryGetValue("budget_copper", out var value) || !(value is int limit) || limit < 0) return false;
                cost.Copper += (long)limit * repeats;
                return true;
            }

            private static int ReadBudget(string[] asserted)
            {
                int cap = -1;
                foreach (string clause in asserted)
                    foreach (Match amount in BudgetPhrase.Matches(clause))
                    {
                        long total = 0;
                        foreach (Match coins in Currency.Matches(amount.Groups[1].Value))
                        {
                            int number = CoinNumber(coins.Groups[1].Value);
                            if (number < 0) return -1;
                            string unit = coins.Groups[2].Value.ToLowerInvariant();
                            total += (long)number * (unit.StartsWith("铂", StringComparison.Ordinal) || unit == "platinum" ? 1000000 :
                                unit.StartsWith("金", StringComparison.Ordinal) || unit == "gold" ? 10000 :
                                unit.StartsWith("银", StringComparison.Ordinal) || unit == "silver" ? 100 : 1);
                        }
                        if (total <= Int32.MaxValue) cap = cap < 0 ? (int)total : Math.Min(cap, (int)total);
                    }
                return cap;
            }

            private static int CoinNumber(string value)
            {
                if (Int32.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var number)) return number;
                const string digits = "零一二三四五六七八九";
                int total = 0, digit = 0;
                foreach (char character in value)
                {
                    if (character == '十' || character == '百')
                    { total += Math.Max(1, digit) * (character == '十' ? 10 : 100); digit = 0; }
                    else
                    {
                        digit = character == '两' ? 2 : digits.IndexOf(character);
                        if (digit < 0) return -1;
                    }
                }
                return total + digit;
            }
        }
    }
}

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
                @"^(?:(?:请|麻烦|拜托|帮我|让队友|你先|你|先|然后|再次|再|就|现在|直接|实际|可以|please\b|then\b|go and\b)\s*)+",
                RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
            private static readonly Regex Question = new Regex(@"怎么|如何|怎样|是否|有哪些|有没有|能不能|会不会|是不是|要不要|有什么",
                RegexOptions.CultureInvariant);
            private static readonly Regex Hypothesis = new Regex(
                @"^(?:如果|假如|假设|例如|比如|if\b|suppose\b|for example\b)", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
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
            private bool Fish => Starts("钓", "fish", "cast", "去钓", "到这里钓", "去右海边钓", "去左海边钓", "去海边钓", "右海边钓", "左海边钓", "海边钓", "海岸钓") ||
                Contains("改成钓鱼", "到了就钓", "到了钓", "在这里钓", "在这钓", "开始钓");
            private bool Move => Starts("去", "到", "回", "走", "跟", "带路", "go", "move", "walk", "return", "follow", "come") ||
                Contains("停止后回来", "停下后回来", "完成后回来", "结束后回来");
            private bool Wait => Starts("等", "待命", "留在", "站着", "wait", "stay", "stand by") ||
                Contains("在这里等", "在那里等", "位置等我", "到达后等", "到达后待命", "回来等我");
            private bool Attack => Starts("攻击", "打", "战斗", "守", "保卫", "attack", "fight", "guard", "defend");
            private bool Prepare => Starts("准备打", "准备挑战", "备战", "prepare for");
            private bool Use => Starts("用", "使用", "喝", "吃", "挥", "丢", "投", "use", "drink", "eat", "swing", "consume") ||
                Contains("连续用", "连续使用");
            private bool Summon => Starts("召唤", "开启事件", "触发", "开血月", "开南瓜月", "summon", "trigger");
            private bool Deliver => Starts("给我", "交给我", "送给我", "give", "deliver") || Contains("成品给我", "成品交给我", "交给我", "带给我");
            private bool Transfer => Deliver || Starts("转移", "搬", "存", "取", "拿", "从", "transfer", "store", "take", "withdraw");
            private bool SpendNearby => Contains("附近箱", "旁边的箱", "附近储存", "nearby chest", "nearby storage");
            internal bool Recall => (Starts("召回", "传送到我", "回我身边", "recall") || Use) && Contains("虫洞", "wormhole");
            internal bool FishActivity => Fish;
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
                    case "attack_npc": case "guard": return Attack;
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
                    case "sell_item": return Starts("卖", "出售", "sell");
                    case "reforge_item": return Starts("重铸", "重锻", "reforge") && Spend(args, repeats, cost);
                    case "nurse_heal": return Starts("治疗", "找护士", "去找护士", "去护士", "heal", "visit the nurse") && Spend(args, repeats, cost);
                    case "turn_in_angler": return Starts("交任务", "交鱼", "提交", "找渔夫", "turn in") || Contains("交给渔夫", "提交给渔夫");
                    case "build_projection": return Starts("建", "施工", "照蓝图", "按蓝图", "build", "construct");
                    case "world_use": return args.TryGetValue("action", out var action) && World(action as string);
                    case "open_container":
                        return Starts("打开", "开袋", "开匣", "拆袋", "open") &&
                            args.TryGetValue("item_type", out var container) && container is int containerType &&
                            args.TryGetValue("amount", out var number) && number is int count &&
                            Reserve(cost, "container", containerType, (long)count * repeats,
                                AuthorizedCount(NamedUse, containerType, NamedWithoutCount(containerType) ? 1 : 0)) &&
                            (!args.TryGetValue("allow_key", out var key) || key is bool useKey && (!useKey || Contains("钥匙", "key")));
                    case "extract_item": return Starts("提炼", "萃取", "extract");
                    case "shimmer_item": return Starts("微光", "投入微光", "丢进微光", "shimmer");
                    case "use_furniture": return Starts("坐", "睡", "起身", "设置床", "sit", "sleep", "set spawn", "stand up");
                    case "catch_critter": return Starts("捕捉", "捉", "抓", "catch");
                    default: return false;
                }
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
                    case "mine": case "axe": case "hammer": case "remove_wall":
                        return Starts("挖", "砍", "拆", "敲", "矿", "mine", "dig", "cut", "break", "remove");
                    case "place_tile": case "place_wall":
                        return Starts("放", "摆", "铺", "建", "搭", "place", "build", "lay");
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

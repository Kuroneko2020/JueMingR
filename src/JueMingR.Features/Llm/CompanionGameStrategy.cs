using System;
using System.Collections.Generic;

namespace JueMingR.Features.Llm
{
    public enum StrategyDependency { NotSpecified, Held, AvailableWithoutTarget, NeedsTargetVictory, Unknown }
    public enum BossPreparationChoice { None, Combat, Prerequisite, Window, Mobility, Healing, Weapon, Ready }

    public readonly struct CompanionStrategyFacts
    {
        public CompanionStrategyFacts(bool hardMode, bool firstTargetVictory, bool inCombat, bool lunarEvent,
            bool windowKnown, bool windowTooShort, bool mobile, bool hasWeapon, bool piercing, bool hasHealing,
            bool hasReturn, bool hasLight, bool hasRoom, bool hasBait, bool hasRod, bool questAvailable,
            bool targetAlreadyHeld, bool targetNearby, bool recipeReady, bool terrainKnown, bool nearObstacle,
            bool inJungle, bool budgetKnown, int maximumLife, StrategyDependency dependency)
        {
            HardMode = hardMode; FirstTargetVictory = firstTargetVictory; InCombat = inCombat; LunarEvent = lunarEvent;
            WindowKnown = windowKnown; WindowTooShort = windowTooShort; Mobile = mobile; HasWeapon = hasWeapon;
            Piercing = piercing; HasHealing = hasHealing; HasReturn = hasReturn; HasLight = hasLight; HasRoom = hasRoom;
            HasBait = hasBait; HasRod = hasRod; QuestAvailable = questAvailable; TargetAlreadyHeld = targetAlreadyHeld;
            TargetNearby = targetNearby; RecipeReady = recipeReady; TerrainKnown = terrainKnown;
            NearObstacle = nearObstacle; InJungle = inJungle; BudgetKnown = budgetKnown;
            MaximumLife = maximumLife; Dependency = dependency;
        }
        public bool HardMode { get; }
        public bool FirstTargetVictory { get; }
        public bool InCombat { get; }
        public bool LunarEvent { get; }
        public bool WindowKnown { get; }
        public bool WindowTooShort { get; }
        public bool Mobile { get; }
        public bool HasWeapon { get; }
        public bool Piercing { get; }
        public bool HasHealing { get; }
        public bool HasReturn { get; }
        public bool HasLight { get; }
        public bool HasRoom { get; }
        public bool HasBait { get; }
        public bool HasRod { get; }
        public bool QuestAvailable { get; }
        public bool TargetAlreadyHeld { get; }
        public bool TargetNearby { get; }
        public bool RecipeReady { get; }
        public bool TerrainKnown { get; }
        public bool NearObstacle { get; }
        public bool InJungle { get; }
        public bool BudgetKnown { get; }
        public int MaximumLife { get; }
        public StrategyDependency Dependency { get; }
    }

    public sealed class CompanionStrategy
    {
        internal CompanionStrategy(string card, string sources, string reason, string route, string finish, params string[] capabilities)
        { Card = card; Sources = sources; Reason = reason; Route = route; Finish = finish; Capabilities = Array.AsReadOnly(capabilities); }
        public string Card { get; }
        public string Sources { get; }
        public string Reason { get; }
        public string Route { get; }
        public string Finish { get; }
        public IReadOnlyList<string> Capabilities { get; }
        public BossPreparationChoice BossPreparation { get; internal set; }
    }

    public static class CompanionGameStrategy
    {
        public static IReadOnlyList<string> Goals { get; } = Array.AsReadOnly(new[]
        {
            "explore", "gather", "prepare_boss", "fight", "arena", "supplies", "angler",
            "improve", "services", "craft", "hardmode", "lunar"
        });

        public static CompanionStrategy Choose(string goal, string boss, CompanionStrategyFacts facts)
        {
            switch (goal)
            {
                case "explore":
                    if (!facts.HasRoom) return New("K01", "G02/G04", "主背包没有空格。", "确认可达存储及可放物品；先腾出这趟需要的空间。", "达到约定容量后出发。", "resource_sources", "transfer_item");
                    if (!facts.HasLight || !facts.HasReturn || !facts.HasHealing)
                        return New("K01", "G02/G04", "照明、回程或生存物资仍有缺项。", "优先从已持有的装备或合法存储补缺；有较近路线可避开困难地形。", "取得目标或达到容量边界后回到约定地点。", "has_item", "move_to", "stand_by");
                    return New("K01", "G02/G04", "基本探索物资已备。", "核对目标地点，按实际可达路径前往。", "实际到达并按玩家要求恢复或待命。", "observe_destinations", "move_to", "stand_by");
                case "gather":
                    if (facts.TargetAlreadyHeld || facts.TargetNearby)
                        return New("K02", "G05/G15", "发现目标库存或附近可用来源。", "优先核对任务预留和来源权限，按需要使用现成资源。", "实际可用数量达标即停。", "has_item", "resource_sources", "transfer_item");
                    return New("K02", "G05/G15", "尚未证实有现成目标资源。", "比较实际采集、商店、钓鱼、合成、掉落；工具或水域不符时换合法来源。", "数量达标或本次成本/时间边界即停。", "world_use", "fish_at", "buy_item", "craft_item", "observe_npcs");
                case "prepare_boss":
                    if (facts.InCombat) return Boss(BossPreparationChoice.Combat, "目标已开始，远行补装备会错过作战。", "先处理当前威胁，使用实有装备和急需补给。", "按真实目标与存活状态重评。", "observe_npcs", "guard");
                    if (facts.FirstTargetVictory && facts.Dependency == StrategyDependency.NeedsTargetVictory)
                        return Boss(BossPreparationChoice.Prerequisite, "建议物品的已证来源依赖目标首胜，当前人物未持有，形成前置循环。",
                            "替换为真实可取得的同用途装备，或调整机动、场地和作战方式。", "前置可独立成立后再准备。", "has_item", "craft_item");
                    if (facts.WindowKnown && facts.WindowTooShort)
                        return Boss(BossPreparationChoice.Window, "当前窗口不足以完整实施计划。", "把剩余当前可完成的补给、机动或场地准备留给下一完整窗口。", "夜间窗口合适再考虑开战。", "has_item", "move_to");
                    if (!facts.Mobile)
                        return Boss(BossPreparationChoice.Mobility, "有效机动手段不足。", "优先用已持有的钩爪或升空/位移配件，或选容易转向和脱身的场地。", "实际装备和场地可用后停止可选刷取。", "equip_item", "move_to");
                    if (!facts.HasHealing)
                        return Boss(BossPreparationChoice.Healing, "需要加强可消费补给。", "先找合法库存，再按原生配方或当前商店条件补齐。", "确认准备数量与原版用药条件。", "has_item", "transfer_item", "craft_item", "buy_item");
                    if (!facts.HasWeapon)
                        return Boss(BossPreparationChoice.Weapon, "未识别可用输出来源。", "先检查已有可用武器、弹药和法力，再考虑目标可得性。", "确认角色能使用选定配装。", "has_item", "equip_item");
                    return Boss(BossPreparationChoice.Ready, facts.FirstTargetVictory && facts.Dependency == StrategyDependency.Held
                        ? "队友已持有所需装备。世界首胜状态不改变合法持有事实。" : "已观察到基本输出、机动与补给。",
                        boss == "mechanical" && facts.Piercing ? "可优先评估多段目标，先核选定场地与时段。"
                        : "优先沿当前武器的安全距离与实际场地准备。", "准备完成后等待玩家的战斗指令。", "observe_destinations", "move_to", "stand_by");
                case "fight":
                    return New("K04", "G16/G18/G20", facts.InCombat ? "原生观察已发现活动 Boss。" : "当前未确认目标正在活动。",
                        facts.InCombat ? "根据实际目标引用和可受击情况攻击或守卫。短暂丢失目标时保留当前任务。"
                            : "先找真实目标或核对开战条件；按玩家授权推进。", "实际目标结束后复核战利品与残留危险。", "observe_npcs", "attack_npc", "guard");
                case "arena":
                    return New("K05", "G01/G16/G19", !facts.TerrainKnown ? "当前地形资料尚未就绪。" :
                        facts.NearObstacle ? "身体上方附近有实际地形阻碍。" : "当前脚点附近未看到上方阻碍，整个战场仍需核查。",
                        boss == "plantera" && (!facts.TerrainKnown || !facts.InJungle)
                            ? "先确认有效地下丛林及施工后仍满足原生环境，再考虑改变战场。" :
                            "优先复用已有空间，按可用机动扩大必要路线，避免不必要消耗。", "复核实际路线、环境和施工效果。", "observe_tiles", "move_to", "world_use", "observe_blueprints", "build_projection");
                case "supplies":
                    return New("K06", "G06/G10/G18", facts.HasHealing ? "已持有可用于生命补给的物品。" : "主背包未发现治疗药品。",
                        facts.WindowKnown && facts.WindowTooShort ? "先做当前可完成的储备与整理，在下一合适窗口出发。"
                            : "核对实有材料、设施与合法来源，再从制作、交易、钓鱼选择。", "真实产物或适用增益达到约定数量即停。", "has_item", "craft_item", "buy_item", "fish_at");
                case "angler":
                    if (!facts.QuestAvailable) return New("K07", "G05/G22", "当前无可提交的个人渔夫任务。", "按真实任务日状态重新核验，仍可整理库存。", "新任务可接后重新定位钓点。", "observe_npcs");
                    if (facts.TargetAlreadyHeld) return New("K07", "G05/G22", "队友已持有实际任务鱼。", "核对提交者、渔夫和原版当天资格，再决定提交。", "回执确认实际消耗与任务日状态。", "observe_npcs", "turn_in_angler", "move_to", "stand_by");
                    if (!facts.HasBait || !facts.HasRod)
                        return New("K07", "G05/G22", "缺少普通可用鱼饵或鱼竿。", "先补合法钓鱼装备，保留特殊鱼饵。", "钓具齐备并核准原版地点与水体。", "has_item", "craft_item");
                    return New("K07", "G05/G22", "发现当前任务和基本钓具。", "核当前鱼及合法钓位，再按请求提交或返回。", "实际入包、提交或完成约定回程。", "observe_npcs", "fish_at", "turn_in_angler", "move_to", "stand_by");
                case "improve":
                    return New("K08", "G11/G22", facts.BudgetKnown ? "本次有明确的花费边界。" : "本次没有明确的消费上限。",
                        "先检查已有未用配件、较低成本合成与设施。随机重铸按实际费用和可接受结果停止。",
                        "满足当前短板后停，保留任务预留资产。", "equip_item", "shop_quote", "reforge_item", "craft_item");
                case "services":
                    return New("K09", "G07–G10", facts.TerrainKnown ? "能读取当前地点基本地形。" : "服务地段尚未加载完整地形。",
                        "分别核对住房、NPC 在场、交易与晶塔使用条件，再选已确认的交通或步行。",
                        "以服务可用、真实到达或同步后的设施为准。", "housing_status", "use_furniture", "observe_npcs", "shop_quote", "buy_item", "sell_item", "nurse_heal", "move_to");
                case "craft":
                    if (facts.RecipeReady)
                        return New("K10", "G09/G23/G24", "现有材料或合法来源与原版配方可核对。", "先用原版可接入材料源，确需时才搬运。",
                            "核对实际扣减、产物类型及数量，按要求交付。", "craft_availability", "craft_item", "transfer_item");
                    return New("K10", "G09/G23/G24", "配方、足量材料、站点或原版条件尚需核实。",
                        "查询原版材料组、站点与角色实际可达来源，逐步补缺。", "前置变化时重查，结果未知时保留原回执。", "craft_availability", "has_item", "craft_item");
                case "hardmode":
                    return New("K11", "G17/G18/G21", facts.HardMode ? "世界已进入困难模式。" : "世界仍处于困难模式前。",
                        facts.MaximumLife >= 400 ? "人物成长已达较高基础生命，先检查真实工具链和新解锁收益。" :
                            "分别检查人物基础成长、当前装备和世界已经解锁的资源，优先补最大的短板。",
                        "现有方案足以推进时，结束可选刷取。", "has_item", "craft_item", "equip_item");
                case "lunar":
                    return New("K12", "G20", facts.InCombat ? "最终战目标已开始。" : facts.LunarEvent ? "世界处于原版月亮事件。" : "当前未观测到原版月亮事件。",
                        facts.InCombat ? "改为当前就能完成的战斗与补给，保留已发生的阶段。"
                            : facts.FirstTargetVictory && facts.Dependency == StrategyDependency.NeedsTargetVictory
                                ? "首次不能依赖目标独占掉落，先找已可取得的碎片或装备替代。"
                                : "按真实塔盾、碎片、场地和补给判断当前阶段，留意最后一柱实际触发。",
                        "目标实际结束并取得战利品后结束收集。", "observe_npcs", "guard", "attack_npc", "has_item");
                default: return null;
            }
        }

        private static CompanionStrategy New(string card, string sources, string reason, string route, string finish,
            params string[] capabilities) => new CompanionStrategy(card, sources, reason, route, finish, capabilities);

        private static CompanionStrategy Boss(BossPreparationChoice choice, string reason, string route, string finish,
            params string[] capabilities) => new CompanionStrategy("K03", "G12/G17", reason, route, finish, capabilities)
            { BossPreparation = choice };
    }
}

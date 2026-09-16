# NPC服务与连续加工

## 0. 本专题范围、固定证据与结论边界

本专题覆盖原执行指令附录 13–14：自动护士、自动家具增益、自动收税、快速重铸、连续开袋、自动提炼，并核对真实附加入口、消费、停止、恢复及与现有物品处理的关系。

证据记号：

- **Z**：`同级 Legacy 仓库 JueMingZ`，固定提交 `6ac6356c0c564f43284590c168855e96c50e13f7`；下列 Z 路径以 `src/JueMingZ/` 为根。
- **R**：`当前 R 仓库 JueMingR`，生产基线 `df8a029873176276d0f620d32b0ac02ae9ea7744`。本次调查产生的文档提交不代表生产实现变化。
- **V8**：已锁定 Terraria 1.4.5.8，`external/TerrariaRefs/Terraria.exe` 的 SHA-256 为 `960A03BFF6050CF7BE16DFC1A7B19E10FC2C4F8F835A6A3B135A50DD9E6BA2F3`。
- **V8-Main**：R `.local/death-history-world-time/Terraria.Main.cs`，SHA-256 `558166C0FC8F15B24B25EF62A0F94A877976C52C1BD5928B14527453CDFB8B1F`。
- **V8-Player**：R `.local/death-history-world-time/Terraria.Player.cs`，SHA-256 `2C983B62766C713D96904B617E916A831518C38F0158556DE7F8FA392885D40C`。
- 本文的“已确认”仅指固定源码和原版静态提取；没有构建、运行测试、游戏执行、实机、多人或性能测量。目录 `Implemented(true)`、`SupportedByOriginalAction` 不是这些验证的替代品。
- 本专题只读调查生产实现，没有新功能实施。R 候选复用和产品问题均不表示已经接受或授权实施。

### 总览

| 分支 | Legacy 默认与真实入口 | 当前静态结论 |
|---|---|---|
| 自动护士 | 默认关闭；增益页“自动护士”及可配置功能开关键 | 缺血或有符合条件的 Debuff，自动找护士并调用原版治疗；收费确在原版治疗函数内；会改写并关闭 NPC 对话 |
| 自动家具 | 默认关闭；增益页“自动家具”及功能开关键 | 支持固定七种已放置家具；一次请求可处理多个缺失 Buff；无背包携带家具生效分支 |
| 自动收税 | 默认关闭；杂项页及功能开关键 | 找可交互税收官；调用原版领取函数，原版生成世界金币并清空个人 `taxMoney` |
| 快速重铸 | 默认关闭、词缀列表空；杂项页“自动重铸” | 鼠标在原版重铸按钮上并持续使用输入；命中自填词缀后本次按住停止；自动路径调用的内层原版函数没有收费，不能沿用旧文档“每次扣钱”的结论 |
| 连续开袋 | 默认关闭；物品页“持续开袋” | Shift + 右键持续输入；队列八次批量和原版右键 Hook 脉冲两个入口；完整资格由 `OpenableBag` 决定 |
| 自动提炼 | 默认关闭；物品页“自动提炼” | 开启后自动选择材料和附近机器，生产请求是 `ItemUse → ItemCheck`；并不要求玩家持续右键；遗留 `InventorySlot` 右键分支不应冒充当前生产链 |

默认值：Z `Config/AppSettings.cs:830–831,917–919,933–935`；别名字段 `503–518,574–591`。目录声明：`Features/Catalog/NpcServicesFeatureRegistrar.cs`、`BuffAndRecoveryFeatureRegistrar.cs`、`InventoryAndItemsFeatureRegistrar.cs`。实际 UI：`UI/Legacy/LegacyMainWindow.Pages.cs:34–36`、`LegacyMainWindow.Items.cs:34–38`、`LegacyMainWindow.Misc.cs:52–58`、`LegacyMainWindow.Misc.QuickReforge.cs:12–54`。

## 1. 自动护士

### 1.1 玩家能力、资格与全部选择规则

自动护士没有可见血量百分比滑块、收费上限、护士白名单或 Boss 情境模式。判断是：

1. 玩家 `statLife < statLifeMax2`；或
2. 至少一个 Buff 同时满足 `type > 0`、`time > 60`、`Main.debuff[type] == true`、不在 `BuffID.Sets.NurseCannotRemoveDebuff`。

第二项是完整动态定义，并非所有负面 Buff 都可治。资格使用 `time > 60`，原版实际治疗成功后会清除允许清除且 `time > 0` 的 Debuff，所以一次收费治疗还能顺便去掉剩余不足一秒的可治 Debuff。

护士全集为 `Main.npc` 中 `active && type == 18`。可达优先使用 `TileReachCheckSettings.Simple.GetTileRegion` 与 NPC 所占 Tile 矩形相交；兼容失败退为中心距离不超过 192 像素。遍历所有合格护士取中心平方距离最小，距离相同保留数组中先遇到的护士；没有“房屋归属”“同一城镇”“不穿墙”的另加规则。

证据：Z `Compat/NurseServiceCompat.cs`，`NeedsNurse:43–52`，`TryFindReachableNurse:54–110`，`CountRemovableDebuffs:183–217`，可达判断 `329–405`；`Automation/AutoRecovery/AutoRecoveryService.Decisions.cs:259–319`。

### 1.2 实际触发链、UI 与停止

链路为：

`RuntimeAutomationDispatcher → AutoRecoveryService.Tick → TryBuildNurseDecision → NpcInteract 请求 → NpcInteractActionExecutor → NurseServiceCompat.TryOpenAndHeal → Main.GetNurseHealCost / NPCChatText_DoNurseHeal`。

普通恢复同一 Tick 的优先级是 **回血药 → 回蓝药 → 护士 → 家具 → 增益药**。护士需要恢复条件、可达目标、90 Tick 冷却以及空闲动作队列；请求为 Low、CoalescePending、排队超时 350 ms、执行超时 3 s。护士可达扫描在 90 Tick 冷却判断之前；“处在冷却”不等于不扫描 NPC。

实际特殊 UI 规则：

- 自动恢复一般阻挡主菜单、聊天、NPC 对话等。
- **如果唯一阻挡是 NPC 对话并且护士开关开启，恢复服务允许护士分支运行**；并非旧说明中的“任何 NPC 对话均阻止”。
- **如果唯一阻挡是箱子界面，护士分支仍可运行**；家具分支在此情形被跳过。
- 普通恢复还避让最近 150 ms 的 F5 控件操作和已有队列工作；护士决策没有药水分支的 `PlayerIsUsingItem` 门。
- 开关改变会 `SaveAll` 并应用设置；开启时强制到期。关闭只阻止后续生产，不应把已执行的治疗理解为可撤销。

执行时先 `dropItemCheck`，`SetTalkNPC` 或兼容字段写入，调用护士 `GetChat` 并写 `Main.npcChatText`。**没有保留之前交谈对象和文本**；最后清为无对话状态，包括 `talkNPC=-1`、聊天文本、角标物品、焦点与释放标志。若此时原本正和别的 NPC 对话，会被替换并关闭。

执行器没有重新核验所有服务层快照门。`TryOpenAndHeal` 重读 NPC index 的存活和 type，但不重新验证到达范围，也没有用 NPC generation 固定“就是提交时那一个对象”。此外，护士代码不像税收代码那样把 `ChatOpened` 作为继续收费调用的硬前提。上述是明确的执行前条件缺口，不等于本次已复现错误治疗。

证据：Z `AutoRecoveryService.Tick.cs:25–104,193–246`；`AutoRecoveryService.Controls.cs:110–153`；`AutoRecoveryService.Enqueue.cs` 护士请求；`Actions/Executors/NpcInteractActionExecutor.cs:14–70`；`NurseServiceCompat.cs:112–181,220–302`。

### 1.3 原版费用、资源变化与确认

V8 原版 `Main.GetNurseHealCost:40777–40825`：

- 基础费用 = 缺失生命值 + 每个 `time>60` 且护士可移除 Debuff 的 100。
- 进度倍数按最高命中条件选择：石巨人 200、世纪之花 150、任意机械 Boss 100、困难模式 60、骷髅王或蜂王 25、第二 Boss 10、第一 Boss 3，否则 1。
- 专家再乘 2；有折扣先乘 0.8 并截整；再乘 `currentShoppingSettings.PriceAdjustment` 并转换为整数。
- Z 为计价临时取得该护士的购物系数，之后恢复原购物设置。若读取费用失败，Z 返回 0 并当作“不需要治疗”，没有独立“计价不可用”玩家状态。

V8 `NPCChatText_DoNurseHeal:40827–40902` 负责 `BuyItem(cost)`；付费成功后才满血、显示 HealEffect、清可移除 Debuff、播放声音、触发护士成就和对话/头像跳动。余额不足走原版拒绝文本。**不是 Z 直接改血量或免单治疗。**

`Player.BuyItem` 会使用背包的钱与 **四个个人银行**的钱；背包计价排除槽 54–58。没有“存入猪猪后护士就不能使用这笔钱”的产品语义。将钱自动存银行只改变存放处，不构成护士预算隔离。V8-Player `BuyItem:35675–35760`。

Z 成功条件为治疗后血量增加或可治 Debuff 数下降。原版返回但未观察到这两种变化，执行器报 `AttemptedButUnverified`；未调用则 `NotApplicable`。它没有专门的多人服务器回执、扣款凭据和到账核对，不能以本地血量观察证明各多人拓扑均已支持/验证。反射异常也可能在部分副作用之后发生，不能把 `HealInvoked=false` 当成“肯定未扣费”。

### 1.4 数据、生命周期、性能与 R 接入问题

Z 持久数据是进程级配置中的护士开关；生命、Buff、钱、NPC 属原版。冷却和诊断为静态服务状态，不是独立护士账本；请求保存 NPC index 等元数据，不保存可靠的 NPC generation。没有护士专属世界文件或角色独立预算。

关闭时调度器不进入恢复服务的前提是所有恢复功能都关闭；其他恢复开启时仍会进入共享恢复服务并跳过护士分支。开启稳态成本包括需求判断、可治 Debuff 列表与 NPC 查找；大量 NPC 时查找随 NPC 表长度变化。此为静态工作量，不是 FPS 结论。

R 当前已有：

- `src/JueMingR.TerrariaHost/Npcs/NativeNpcObservation.cs:23–74`：同完成 Tick 的 NPC 观察；`Direction` demand 提供对象引用、type、netID、generation、whoAmI、中心位置。可作为后续目标观察候选，当前还没有“护士服务”操作。
- `src/JueMingR.TerrariaHost/Items/ItemPendingGuards.cs:126–137`：原版 `BuyItem` 已受现有物品待定范围保护；普通货币在受保护金币或空槽会冲突，特殊货币范围更广。护士费用必须接这条实际冲突边界，不能旁路为“只改生命”。
- `src/JueMingR.Platform/Items/ItemOperationOwnership.cs:5–70` 仅拥有存/卖/扔三种现有操作，不能把护士任务硬塞为第四种未定义语义。

**产品待决定**：护士何时触发、是否保留现有对话、是否需要费用上限或付费反馈、与药水的优先级、费用未知时怎么提示、多人已请求与最终完成如何区分。以上尚未构成 R 产品契约。

## 2. 自动家具增益

### 2.1 完整支持集合与结果

| Tile type | 家具 | Buff ID | V8 持续 Tick | Z 物体尺寸 | Z 标记可能打开背包面板 |
|---:|---|---:|---:|---|---|
| 125 | Crystal Ball / 水晶球 | 29 | 108000 | 2×2 | 是 |
| 287 | Ammo Box / 弹药箱 | 93 | 108000 | 2×2 | 否 |
| 354 | Bewitching Table / 施法桌 | 150 | 108000 | 3×2 | 否 |
| 377 | Sharpening Station / 利器站 | 159 | 108000 | 3×2 | 否 |
| 464 | War Table / 战争桌 | 348 | 108000 | 3×2 | 否 |
| 621 | Slice of Cake / 蛋糕块 | 192 | 7200 | 2×1 | 否 |
| 699 | Potion Station / 药水站 | 366 | 108000 | 3×2 | 是 |

这是完整七种。没有便携家具、篝火等被动光环的自动点击分支，也没有按名单挑其中几种的实际 UI。目录虽写 `StrategyConfigWindow`，本专题找到的实际入口为增益页开/关与功能开关键。

Z `Compat/StationBuffCompat.cs:56–65,288–292`；V8-Player `TileInteractionsUse:32833–32873`。六种 108000 Tick 在 60 Tick/s 下为 30 分钟，蛋糕 7200 为 2 分钟；这是原版设置时长，不是实测刷新周期。

### 2.2 发现、排序、缓存与每次处理量

每次先检查 10 Tick 冷却，再用当前活动 Buff 生成七位缺失掩码；七种均已存在则跳过扫描。Buff 只要仍活动就视为拥有，不设“快到期提前刷新”阈值。

扫描优先用 `TileReachCheckSettings.Simple`，失败退为玩家 Tile 中心 X±10、Y±8 的 21×17 矩形并裁到世界内。只收活动 Tile、类型在表内且 Buff 缺失的候选。按 frame 和各家具尺寸归一化物体原点，对每种家具保留最近目标，最终最多七个，按交互点平方距离排序，再以 Tile type 破平局。不会因附近有十张同类桌而重复施加同一 Buff。

扫描缓存键包含 Tile 集合对象哈希、世界尺寸、可达矩形、玩家 Tile 中心和缺失掩码；缓存 30 Tick，正结果和空结果都缓存。**没有 Tile 变更 revision**；同格站立时家具刚放下/拆掉，可能等 TTL 或其他键变化才重新发现。对象哈希也不是 R 的 Session generation。执行候选点使用当前 Tile/frame 作补查，但不等价于完整重新发现。

证据：Z `AutoRecoveryService.Decisions.cs:322–400`；`StationBuffCompat.TryFindMissing:114–284`，缓存 `682–719`，签名 `999–1034`。

### 2.3 原版交互、失败与界面恢复

一次 Low `TileInteract` 请求编码整个目标列表。执行器捕获原鼠标/目标输入和背包打开状态；每个目标生成多个去重点：原目标、物体底部中心、原点/角点、物体内符合类型的格子，以及目标周围 5×5 同类型格子。每点先调用 `TileInteractionsCheck`，Buff 仍缺时再直接调用 `TileInteractionsUse`，每次调用后检查 Buff。

V8 原版会 `AddBuff` 并播放相应声音，没有扣家具堆叠或药水。七种完全到位才是全成功；部分未到位则保留一个短暂交互状态，在至少下一次 Update 检查；最终报 `AttemptedButUnverified` 并给成功数量。原本已有的目标 Buff 也算该目标完成，不再点击。

Z 为水晶球/药水站恢复先前 `Main.playerInventory` 值，结束或取消时释放 `controlUseTile`、清理 request ID 的目标覆盖并恢复鼠标/智能交互输入。覆盖还带 2 秒 UTC 到期。**恢复失败有诊断字段，但不是全部成功状态的否决条件**；它也没有恢复所有原版 UI 子状态的完整快照。

需要保留的静态边界：

- `TileInteractionsUse` 的直接回退不是再次完整调用原版发现和可达验证。
- 首个候选点总会加入；执行器没有以 NPC/Tile generation 与期望 type 为统一操作前置条件拒绝陈旧目标。目标发生变化时不能仅凭请求里的旧 Tile type 推断调用必然只影响原家具。
- 观察本地 Buff 到位不是多人权威确认。尚无各多人拓扑验证。
- 原版本身还拒绝电线 UI 打开、拥有特定投射物 651 等条件；自动队列有目标，不代表原版必会加 Buff。

证据：Z `Actions/Executors/TileInteractActionExecutor.cs:25–390`；`Compat/TerrariaInputCompat.TileInteraction.cs:26–195`；`StationBuffCompat.BuildInteractionPoints:295–363`；V8-Player `TileInteractionsUse:32352–32362,32833–32873`。

### 2.4 所有权、频率与关联

设置属于 Z 全局配置；目标列表/冷却/扫描缓存在服务；Buff、Tile、实际效果属于原版；鼠标和交互覆盖由执行器/Compat 持有至完成、取消或到期。没有家具收藏名单或地图持久文件。

关闭时不再请求该分支；稳态先冷却/活动 Buff 快速跳过，缺 Buff 才扫描；变化包括玩家跨格、Buff 消失、Tile 集合改变、TTL 到期。最多七种候选控制了结果规模，但每次未命中扫描仍与可达区域格子数相关，候选点尝试还可能逐个调用原版与反射。缓存/Stopwatch/诊断的存在不足以证明性能好坏。

R `WorldTileObservation.cs:21–28,56–72` 已有按 Tick 观察缓存、Session 清理、世界/section 身份变化与客户端未加载 Tile 拒绝，可作为窄事实来源候选。它不拥有家具优先级、Buff 列表、交互输入或成功判定。**家具与增益药共享的是效果/选择顺序，与提炼/采矿共享的是 Tile 事实和鼠标使用冲突；不能合并成一个负责所有世界动作的服务。**



## 3. 自动收税

### 3.1 能力、默认、资格和目标排序

默认关闭。杂项页“自动收税”及功能开关键修改 `NpcAutoTaxCollectEnabled`，其持久别名是 `MiscAutoTaxCollectEnabled`。没有领取额度门槛、税收官名单、范围滑块、自动存钱目标或领取次数配置。开着功能且 `player.taxMoney > 0` 即有需求，不等满额。

完整目标集合是 `Main.npc` 内活动且 `type == 441` 的税收官，优先原版 `TileReachCheckSettings.Simple` Tile 区域相交，失败退为中心 192 像素。第一次/缓存失效时取平方距离最近、同距离先数组索引；**缓存中的税收官仍活动、类型正确且可达时直接复用，不因另一个更近而重排**。

调度器和服务都是 30 Tick 节奏。无税款时即返回，不扫描 NPC；有税款且缓存可用时避免全表查找，缓存失效才扫描所有 NPC。

证据：Z `Automation/NpcServices/AutoTaxCollectorService.cs:32–40,100–223,253–290`；`Compat/TaxCollectorServiceCompat.cs:57–124,353–367`；`Runtime/RuntimeAutomationDispatcher.cs:103–104,497–503`。

### 3.2 UI、输入、真实调用与失败恢复

服务需有效世界、活动且非死亡/幽灵的玩家、无鼠标浮动物品；阻挡主菜单、聊天、箱子和 NPC 对话，背包打开本身不阻挡。队列有任意 pending/running 或 `ItemUseBridge` 待处理时让行。请求为 Low、CoalescePending，排队超时 500 ms、执行超时 3 s，带 NPC index/type/whoAmI/name 和提交时税款。

链路：

`AutoTaxCollectorService → NpcInteract → TaxCollectorServiceCompat.TryOpenAndCollect → Main.NPCChatText_DoTaxCollector`。

执行时再次检查本地玩家、目标索引仍活动且 type=441、当前可达和税款仍大于零。先 `dropItemCheck`，打开目标 NPC 对话并获取 `GetChat` 文本；只有 `ChatOpened` 成功才继续。临时应用这个 NPC 的购物设置，调用原版领税，再恢复原购物设置，并清空自己打开的 NPC 对话状态。没有恢复之前对话；服务层原本会阻挡已有 NPC 对话，但排队到执行之间并非完整 UI 身份事务。

请求携带 whoAmI，却没有以原对象引用及 generation 证明执行时仍是提交时的同一个税收官。兼容层缓存 index 为静态值；服务 `ClearTracking` 清的是自己的诊断/时间戳等，没有清该缓存，缓存有效性靠下次读 NPC/type/reach 复核。

原版函数返回且 `taxMoney` 下降才报成功；函数返回而余额未下降报 `AttemptedButUnverified`；不满足执行条件则 `NotApplicable`。这里验证的是“税款账户减少”，**不是金币已经进背包，更不是服务器最终确认**。异常可能发生在生成一部分金币之后，不能把异常当作安全重放的凭据。

证据：Z `TaxCollectorServiceCompat.cs:126–228,269–322`；`Actions/Executors/NpcInteractActionExecutor.cs:14–70`；`AutoTaxCollectorService.ClearTracking:262–275`。

### 3.3 原版税款增长、领取结果和多人边界

V8 原版：

- `Player.taxRate=3600`。`Main` 在有税收官、非专服、非主菜单时累加 `taxTimer += dayRate`，到阈值扣 3600 并调用 `CollectTaxes`。因此不能把自动领取 30 Tick 与原版税款产生频率混为一谈，也不能把有时间倍率时简单说成固定现实一分钟。
- `CollectTaxes` 每次对活动、有住房、非 TownPet 且有默认头像的 NPC 计数，每个计 50 铜币，普通上限 25 金币；十周年世界计额和上限均翻倍。
- 领取金额为 `(int)(taxMoney / currentShoppingSettings.PriceAdjustment)`。
- 领取函数通过 `Item.RequestNewItem` 在本地玩家中心生成金币 71–74，`EntitySource_Gift` 来自当前对话 NPC，归属 `ReserveForLocalPlayer`，之后把 `taxMoney` 清零、产生原版对话和头像跳动。
- 金币分面额条件实际用严格 `>`：`>1000000`、`>10000`、`>100`，剩余铜币；不要擅自改写成数学上最少堆叠的 `>=` 分解规则。
- `Item.RequestNewItem` 在客户端 `netMode==1` 时调用原版 `NetMessage.SendData(21,...)`。这说明存在原版同步出口，不证明这一自动功能有回执核对。
- `taxMoney` 原版写入/读取角色存档；Z 不另建税收账本，不直接写领取金额。

证据：V8-Player `811–815,24318–24343,55447,55929`；V8-Main `65976–65983,40904–40949`；R `.local/研究/item-processing-1.4.5.8/Item.Request.cs:4–10`，SHA-256 `67C0080FBEFC804B7D47DE10FD0828B28CEA5B14AF6CA5CB7BC33023329D2ECA`。

### 3.4 与存钱、来源和 R 的具体关系

收税并不直接存银行。链条是 **税款清零 → 生成世界金币 → 原版实际拾取 → 自动存钱另行判断容器和取出保护**。满包、金币堆叠、银行容量、未确认的世界物品同步，都不能用 `taxMoney` 减少替代验证。

R `ItemSourceHooks.GetBefore:53–60` 明确排除币 71–74；所以税收金币不会变为 R 自动卖/扔/存普通物品的来源票据。未来钱币模块应消费原版钱币/银行事实和自己的操作出口，不能因有现成 `RegisterAcquisitions` 就给币伪造普通物品来源。R 对世界非币拾取的观察并不表示自动收税已实现。

R 可考虑复用 `NativeNpcObservation` 的对象、generation 与位置事实，不能复用 Legacy 的“静态上次索引”作为 Session 权威。正常税款增长和保存继续归原版，自动领取功能只拥有是否领取、请求和结果状态。

**待决定**：是否每有一点就领、是否设置阈值、是否显示领取/到账差异、与自动存钱的配合反馈、多人未确认时的暂停或重试方式。当前只确认 Legacy 行为，不将旧行为提升为 R 决定。

## 4. 快速重铸

### 4.1 实际 UI、全部模式与名单语义

默认关闭、名单为空。实际杂项页行名是“自动重铸”，目录和诊断常称“快速重铸”。界面只有开/关、词缀输入、添加、名单逐项移除及功能开关键；没有速度、预算、重铸次数上限、按武器类推荐词缀、最佳词缀自动列表或数值属性目标模式。

- 双击输入框才进入文本编辑，可接 IME。
- “添加”取整个输入字符串并 `Trim`，空白和忽略大小写的重复被拒绝；不查询 `PrefixID` 验证拼写、语言、是否适用于当前物品。
- 名单按添加顺序保存；移除按当前列表 index。
- UI 提示“输入完整词缀名”“不支持只填单字模糊匹配”，实际匹配的是原版 `Item.AffixName()` 返回的完整显示文本：
  - 与目标文本完全相等；或
  - 以目标文本开头，后一个字符是空白、冒号 `:`、连字符 `-`。
  - 比较忽略大小写；多条均匹配时最长目标文本胜出。
- 不能把列表理解为优先追求第一项；**任何一条匹配都停止**。
- 服务把名单用英文逗号拼入 metadata；执行器再按英文逗号分割。UI 接受含逗号的一条字符串，但执行时可变成多条目标，是序列化语义差异。
- 名单是显示语言文本，不是跨语言稳定词缀 ID；换语言或填不可能匹配的词缀没有校验兜底。

证据：Z `UI/Legacy/LegacyMainWindow.Misc.QuickReforge.cs:12–88,124–240`；`Input/LegacyUiActionService.NpcQuickReforgeHandlers.cs:29–172,187–245`；`Automation/NpcServices/QuickReforgeService.cs:268–291,465–479`；`Actions/Executors/ReforgeActionExecutor.cs:100–128`；`Compat/ReforgeCompat.cs:205–289,347–376`；`Config/ConfigService.cs:1085,1537`。

### 4.2 开始、继续、命中、释放和失败

服务每 Tick 检查，要求有效活玩家、非主菜单/聊天/箱子；NPC 对话不是阻挡，因为玩家须处于原版 Goblin 重铸菜单。真实按住条件读取 `player.controlUseItem`，并合并受抑制的使用输入与 OS 左键 fallback；读取方法首先要求 `TerrariaMainCompat.AllowsInputProcessing`。不是独立固定“重铸键”。

还须 `Main.InReforgeMenu`、`Main.mouseReforge` 表示鼠标在原版重铸按钮上，且 `Main.reforgeItem` 非空。全部队列/桥空闲时提交 High `Reforge`，CoalescePending，排队 300 ms、执行 2 s。高优先级不代表可以插队打断运行操作。

- 当前物品原本已匹配：记录“already matched”，不生产自动重铸；**不锁原版冷却，也不替玩家禁止这次原版手动点击**。
- 自动重铸未匹配：清 `Main.reforgeCooldown=0`，允许快速继续，包括原版刚滚到顶级但不在用户名单的情况。
- 自动重铸匹配：写 60 Tick 冷却，服务用该 request 的 `Succeeded` 结果文本识别命中；在这次使用输入仍按住期间，每 Tick 继续维持 60 Tick 并不再自动重铸。
- 松开使用输入、名单变空、玩家不可用或相关阻挡会清 hold 状态。常规关闭由调度器的关闭边缘清理进入服务后清 tracking。
- 命中 hold 是一次按住会话，不是物品身份/词缀 generation。非空名单修改、替换重铸槽物品或重新进入菜单，不等于一定重新武装；该状态应靠源码顺序分析，不能宣称以“当前物品始终匹配”为不变量。
- 无预算/次数/累计时间上限。无效目标可能一直尝试到玩家松手、离开或开关停止。
- 执行时只检查菜单、悬停和当前重铸槽非空，不重新检查真实按住、期望物品身份或提交时词缀；请求中的 `CurrentAffix` 主要供诊断。
- 原版调用返回即 `Succeeded`，即使随机结果与之前相同、或未达到名单目标；这是 Z 将“成功滚一次”与“命中目标”分开的现有判断，不能据此声称手续费/物品/多人状态全部核验完成。

证据：Z `QuickReforgeService.TickCore:134–245`，命中会话 `328–451`；`ReforgeCompat.cs:40–182,195–203`；`ReforgeActionExecutor.cs:15–67`；`Compat/TerrariaInputCompat.UseItem.Read.cs:9–40`。

### 4.3 关键发现：自动调用缺少原版外层收费

这不是“调用了原版所以必然收费”的实现。

- Z `ReforgeCompat.TryInvokeReforgeItemInSlot:291–304` 用反射直接执行零参数 `Main.ReforgeItemInReforgeSlot()`。
- V8-Main `ReforgeItemInReforgeSlot:42649–42680` 只做 `reforgeItem.ResetPrefix()`、`Prefix(-2, out rolledPrefixIsTopTier)`、弹字、声音和顶级词缀粒子/60 Tick 暂停；**没有 `BuyItem`**。
- V8-Main 原版按钮外层 `42325–42334,42401–42403` 才计算费用与扣费：`item.value * item.stack`，折扣 0.8，购物系数，除 3，然后在 `mouseLeftRelease && mouseLeft && reforgeCooldown<=0 && player.BuyItem(num55)` 成立时调用内层。

因此固定 Z + 当前 V8 自动路径中，**Z 发起的每个内层调用没有自动收费保障**；原版用户自己的点击仍可能另走外层收费，不能据此说整个交互期间绝无任何花费。旧功能文档“快速重复原版重铸、消耗金币”的表述不足以覆盖这个实际边界。应作为 Legacy 事实偏差进入调查，不是本次修复授权。

同理，内层随机调用确会改变词缀及相应物品属性，顶级结果会播放声音/粒子。不能把“无实际费用”误写成“无副作用”。未对多人确认、经济一致性或用户实机行为做验证。

### 4.4 数据、性能、R 复用和待决定

Z 保存全局开关和词缀字符串列表，`ConfigService` 的 `appsettings.json` 承载设置，功能目录状态另有 `features.json`，快捷键另行保存。名单不是每个角色的重铸目标；临时 hold、最后 request、诊断和反射 MethodInfo 是静态进程状态，不落为重铸历史。

开启空闲每 Tick 归一化列表；大列表成本包括分配/去重/匹配，实际调用还会扫描 `AffixName` 方法并反射、生成原版声光及动作诊断。名单没有本分支数量上限。关闭稳态调度跳过，正常开关边缘有一次清理；这是代码工作量分类，不是 FPS 结论。

R 当前没有重铸 Feature 或 Operation。已有 `ItemPendingGuards.Buy:126–137` 确实保护原版购买使用的金币和可能写找零的空槽，但 **只直接调用免费内层函数会绕过这条真实购买边界**。迁移时应明确外层费用/资格/购物设置与随机改词缀的完整受控出口；不能让“原版函数”这个标签掩盖所选函数层次。`ItemOperationOwnership` 也不应因重铸涉及物品就被泛化成未经定义的全局动作队列。

**待决定**：R 是否要求每次严格原版收费、无效名单的可见反馈、使用 ID 还是本地化文本、预先命中时是否允许手动重铸、命中后何种输入重新开始、预算和异常部分完成策略。上述均是需要明确的产品问题。



## 5. 连续开袋

### 5.1 完整支持集合、默认及玩家操作

默认关闭，物品页“持续开袋”只有开/关与功能开关键；玩家需打开背包，按住 Shift 与右键。没有可开名单编辑、保留数量、只开指定 Boss、每秒次数或自动跨容器搜袋的配置。

Z 资格的完整定义是 **`item.type>0 && stack>0 && ItemID.Sets.OpenableBag[type]`**。没有收藏、出售/丢弃名单、稀有度或最大堆叠的额外排除。V8 `OpenableBag` 共 58 个 ID：

| 分类 | 完整 ID 与名称 |
|---|---|
| Boss 袋 21 个 | 3318 KingSlime、3319 EyeOfCthulhu、3320 EaterOfWorlds、3321 BrainOfCthulhu、3322 QueenBee、3323 Skeletron、3324 WallOfFlesh、3325 Destroyer、3326 Twins、3327 SkeletronPrime、3328 Plantera、3329 Golem、3330 Fishron、3331 Cultist、3332 MoonLord、3860 Betsy、3861 Ogre、3862 DarkMage、4782 FairyQueen、4957 QueenSlime、5111 Deerclops 的 BossBag |
| 普通钓鱼匣 13 个 | 2334 WoodenCrate、2335 IronCrate、2336 GoldenCrate、3203 CorruptFishingCrate、3204 CrimsonFishingCrate、3205 DungeonFishingCrate、3206 FloatingIslandFishingCrate、3207 HallowedFishingCrate、3208 JungleFishingCrate、4405 FrozenCrate、4407 OasisCrate、4877 LavaCrate、5002 OceanCrate |
| 困难模式钓鱼匣 13 个 | 3979 WoodenCrateHard、3980 IronCrateHard、3981 GoldenCrateHard、3982 CorruptFishingCrateHard、3983 CrimsonFishingCrateHard、3984 DungeonFishingCrateHard、3985 FloatingIslandFishingCrateHard、3986 HallowedFishingCrateHard、3987 JungleFishingCrateHard、4406 FrozenCrateHard、4408 OasisCrateHard、4878 LavaCrateHard、5003 OceanCrateHard |
| 其余 11 个 | 3093 HerbBag、4345 CanOfWorms、4410 Oyster、1774 GoodieBag、6142 PalworldChilletEgg、3085 LockBox、4879 ObsidianLockbox、1869 Present、599 BluePresent、600 GreenPresent、601 YellowPresent |

这些是资格表，不表示每种物品在正常游戏进度中都能取得，也不表示产物相同。Boss 袋、钓鱼匣和其余专门分支由原版各自决定随机产物、世界条件和模式差异。

原版表证据：R `.local/全功能调查/原版/Terraria.ID.ItemID.cs:1086,1088,1215–1217`，SHA-256 `CF9385C54A1E1C7399C09FCF2158D82C643364B59B0FAADD321799D3B88FFB9A`。Z `Compat/QuickBagOpenCompat.cs:381–419,555–588` 一次解析并缓存原版数组；解析为 null 后也缓存“已解析”，本进程没有这条路径的自动重试刷新。

### 5.2 队列批量入口：搜索与消费

链路：

`RuntimeAutomationDispatcher → QuickBagOpenService.Tick → InventorySlot 请求 → InventorySlotActionExecutor.StartQuickBagOpen → QuickBagOpenCompat.TryRapidOpenSlot → ItemSlot.RightClick → TryOpenContainer`。

生产层每 Tick 检查，使用冷却 1 Tick。需要有效活玩家、背包已打开、无鼠标浮动物品、无菜单/聊天/NPC 对话/箱子，队列及 ItemUseBridge 空闲。输入允许原版 `Main.mouseRight` 或 OS 右键仍按住；Shift 允许 `Main.keyState` 或 OS 左/右 Shift。OS fallback 自身检查 `AllowsInputProcessing`，而已有原版布尔值不是一套新鲜物理输入证明。

搜索只查背包 **0–49**：

1. 读取 `Main.HoverItem.type` 作为偏好；
2. 同类型若有多个堆叠，选低索引第一个；
3. 没有该类型或鼠标没有悬停合格袋，选全背包低索引第一个合格袋。

因此玩家持续手势时，**并非只操作鼠标正压着的那一堆**；当前袋用完后可能接着选择其他袋。不会从个人银行、箱子、鼠标槽 58 搜索。

生产请求 Low，排队 100 ms、执行 700 ms，只显式要求 `InventorySlot` 通道，批量次数固定 8。执行器允许 metadata 次数 1–30，但不是 UI 可选速度。

执行时每一轮重新读当前槽是否仍为任何合格袋，借用四个原版鼠标字段：左键/左释放为 false，右键/右释放为 true，调用一次 `ItemSlot.RightClick`，finally 恢复原四字段。**没有把请求保存的袋 type 作为执行约束**；它可对该槽后来出现的另一种合格袋继续开。执行器也没有再次检查 Shift/右键是否已松开，快照复核允许的 UI 范围比生产层窄：其 combat gate 阻挡菜单/聊天/NPC，未重新要求背包打开、未统一拒绝箱子界面。

每轮 type 变化或 stack 下降计一次 `openedCount`；**读取 after 字段失败也加一**。无变化通常会继续下一次尝试，直到最多八次；全部未消耗则 `NotApplicable`。部分已开后下一次反射调用失败，仍可返回成功并报告已计数次数。这里不是按真实总减少数量计数，也没有产物完整性或多人最终确认。

证据：Z `QuickBagOpenService.cs:33–36,154–272,275–357`；`QuickBagOpenCompat.cs:25–57,201–378,510–552`；`Actions/Executors/InventorySlotActionExecutor.cs:387–425`；`InputActionExecutorBase.cs:46–60`。

### 5.3 实际额外入口：原版 `RightClick` 前置脉冲

第二入口是 `Hooks/QuickBagOpenItemSlotHookCallbacks.Prefix:11–23`，直接接原版 `ItemSlot.RightClick(Item[],int,int)`。服务读取开关和当前清理让行状态后进入 `TryApplyItemSlotRightClickReleasePulse`：

- 仅 context=0；背包打开。
- 右键与 Shift 按住；没有原版 pending inventory actions；本地玩家可读、itemAnimation 不忙；传入槽合法、属于 OpenableBag；鼠标浮动物品为空。
- 它不遍历背包选替代物品，使用原版本次传入的槽。
- `_hookPulseCooldown=1` 按 **Hook 调用次数** 递减，使成功脉冲与一次跳过交替；不能擅自称作稳定“每两帧一次”。
- 设置 `Main.mouseRightRelease=true` 后让原版 RightClick 继续处理；不通过动作队列。
- 配置 UI 门通过旧全名 `TerrariaHelper.UI.Backends.Terraria.TerrariaUIBootstrap.IsConfigUIVisible` 反射查询，找不到时返回 false。仅有这段代码不能证明它覆盖当前 JueMingZ F5 界面。
- 禁用仍有这个已安装 Hook 的开关检查和脉冲冷却重置；所以“关闭稳态无全量扫描”可以确认，“关闭零调用/零成本”不能确认。

这一入口和队列批量不是同一个执行状态机，控制输入、频率、保护门和来源发布都需要分别讨论。Z `QuickBagOpenService.cs:121–152`；`QuickBagOpenCompat.cs:68–199,617–657`。

### 5.4 原版实际消费、钥匙、产物与多人

V8 `ItemSlot.RightClick` 先拒绝 `Main.LocalPlayerHasPendingInventoryActions()` 和忙碌 itemAnimation；普通鼠标分支要求非 Gamepad UI、context=0、OpenableBag、右键释放脉冲。该路径没有“收藏袋禁止打开”的检查。

`TryOpenContainer` 先调用 `TryOpenContainer_GrantItems`，成功才扣袋 `stack--`，归零 `SetDefaults(0)`，播放声音、`Main.stackSplit=30`、清 `mouseRightRelease`。因此正常“不满足钥匙条件”不会扣袋，且不会凭 Z 资格表绕过原版钥匙规则：

- **3085 LockBox**：先 `ConsumeItem(327, reverseOrder:false, includeVoidBag:true)`；耗一个 GoldenKey，成功后 `OpenLockBox`。
- **4879 ObsidianLockbox**：需 `HasItemInInventoryOrOpenVoidBag(329)`；ShadowKey 是持有资格，不在这里消耗。
- 其他 56 个各走 BossBag、FishingCrate、HerbBag、CanOfWorms、Oyster、GoodieBag、ChilletEgg、Present、LegacyPresent 相应原版分支。

产物经原版 `QuickSpawnItem → GetOrDropItem → GetItem` 入背包；剩余装不下的部分走 `RequestNewItem` 在玩家中心生成保留给本地玩家的世界物品。因此无空位不等于没有产物，也不等于整批停开。世界物品在客户端走原版消息 21，而 Z 没有每袋/每产物的服务器确认票据。

几个容易被省略的专门结果：

- Oyster 总给 4411；另有 1/4 概率进入珍珠分支：其中 1/10 给 4414，否则再按 1/3 给 4413，否则 4412。
- CanOfWorms 给 Worm 5–8，另有 30% 给 3191 的 1–2 个、5% 给 2895。
- ChilletEgg 在 5665/5666 之间等概率给一个。
- 奖励先生成、袋/钥匙后续扣减或异常并不是跨所有步骤的可回滚事务；异常后不能自动补造奖励或返还袋子。

证据：R `.local/研究/item-processing-1.4.5.8/ItemSlot.cs:648–805`，SHA-256 `BDF8CFD535FFA8AE6EEFF04AFDFCDDE8D39AC5F96579EDE4DA1775D7B9298D2B`；同目录 `Player.Openables.cs:142–843,844–2128,2129–2301`，SHA-256 `95F70F0F01729792C975E5EC9C82C5BB543601B5AB1F08EDE5465EA584B8EE1D`；V8-Player `QuickSpawnItem/GetOrDropItem:7174–7203`；`Item.Request.cs:4–10`。

### 5.5 与存、卖、扔的真实协作边界

只要 Z 自动存/卖/扔任意一个启用，批量请求**入队成功时**就开始至 `tick+24` 的清理让行窗口；在这个窗口，队列批量入口和 Hook 脉冲入口都停。三个功能都关则清窗口。让行与“已有产物”“消费成功”“列表是否匹配”“附近是否有容器”无关，也没有等清理完成的回执；这是 Legacy 的固定时间耦合，不是 R 必须继承的产品依赖。

`QuickBagControlledBatchSourceService` 在执行边界采集背包兼容堆叠总量前后差，以 `PlayerWorldIdentity`、物品事实、净增量、已有基数、来源 `"QuickBagOpen.ControlledBatch"` 发布堆叠物品来源票据。它不是把之后 24 Tick 中所有背包增长都认定为开袋产物；世界溢出物稍后真实拾取应走拾取来源。没有 before 快照就不凭猜测补来源。

证据：Z `QuickBagOpenService.cs:163–167,207–250,352–384`；`QuickBagControlledBatchSourceService.cs:7–79`；`InventorySlotActionExecutor.cs:408–416`。

R 当前的接口已经更窄、更明确：

- `ItemSourceHooks.cs:19–22,42–52,77–98` 接真实 `TryOpenContainer/GrantItems/GetItem` 因果范围，要求正常返回、grant 成功、袋恰好少一个，并在同一个完成范围采样来源。
- `ItemSourceHooks.cs:99–113` 原版异常可能已给物品时保护相关范围，不把部分完成重新当成普通待卖/扔对象。
- `ItemPendingGuards.cs:24–39,74–85,101–102` 对被当前操作占用的源槽拒绝手动/右键开袋和物品使用；钥匙消费还走 `ConsumeItem` 受保护选择。不能为连续批量绕过这些已实现的边界。
- `ItemOperationOwnership` 为真实存/卖/扔保护范围，不能简单拿全局“队列空”替代所有冲突；也不能仅因开袋需要清理就让卖/扔无条件阻塞全部存储。

**产品待决定**：持续操作是否必须锚定玩家当前悬停堆、用完是否自动换类型、是否保护收藏、预算包括袋还是钥匙、批量大小/公平调度、多人未确认期间如何暂停、关闭/释放是否取消尚未执行批次。这些不是现有自动存卖扔的重审或 #48 的重新授权。

### 5.6 生命周期与性能

全局开关落配置；扫描 tick、使用 tick、24 Tick 让行、诊断和 Hook 冷却为临时静态状态；OpenableBag 数组和反射方法为进程缓存。无独立连续加工 Session generation，没有“重启续开任务”的持久作业格式。

关闭边缘可清服务状态，关闭稳态调度跳过；已经入队的批次并未因此得到本功能专用取消。活跃时每 Tick 最多扫描 50 个背包槽、一次最多八次原版开袋，每袋还可能生成多件随机物品、产生声音/转移记录和网络请求；入包来源快照按兼容总量做两个字典，成本随背包类型数和产物数变化。Hook 还有独立原版右键调用次数成本。静态上应关注大批量袋、满包溢出、无钥匙反复尝试、开启却没有可行清理目标的让行，尚无本次实测性能结论。



## 6. 自动提炼

### 6.1 当前玩家能力与完整输入资格

默认关闭，物品页“自动提炼”只有开/关及功能开关键。开启后在玩家可用、UI 不阻挡、队列空闲且材料与机器符合条件时自动生产请求，**没有要求玩家按住右键或 Shift**。目录/Compat 注释中的“快速重复右键”不符合当前生产请求实际 kind。

资格完整定义为背包前 50 槽中 `type>0 && stack>0 && ItemID.Sets.ExtractinatorMode[type]>=0`。没有收藏、白/黑名单、最低保留数量、Boss 情境、耗材价格、每批上限配置，也不选鼠标槽 58、钱币/弹药栏、银行或世界箱子。

V8 完整输入表如下，共 **16 种、7 个模式**：

| 原版 mode | 完整输入 ID / ItemID 名 | 产品含义 |
|---:|---|---|
| 0 | 424 SiltBlock；1103 SlushBlock | 泥沙块、雪泥块的基础随机提炼 |
| 1 | 3347 DesertFossil | 沙漠化石；额外化石矿分支，其他掉落权重也有差别 |
| 2 | 2339 TinCan；2338 FishingSeaweed；2337 OldShoe | 三种钓鱼垃圾转为虫/蜗牛/鱼饵 |
| 3 | 4354 LavaMoss；4389 ArgonMoss；4377 KryptonMoss；4378 XenonMoss；5127 VioletMoss；5128 RainbowMoss | 六种发光苔藓转换 |
| 4 | 5395 PoopBlock | 便便块转土，偶有三类草籽 |
| 5 | 1124 Hive | 蜂巢块转蜂蜜块 |
| 6 | 4090 ShellPileBlock；173 Obsidian | 贝壳堆、黑曜石转沙块 |

证据：R `.local/全功能调查/原版/Terraria.ID.ItemID.cs:1110`，SHA-256 `CF9385C54A1E1C7399C09FCF2158D82C643364B59B0FAADD321799D3B88FFB9A`；Z `Compat/AutoExtractinatorCompat.cs:23–45,48–220,355–372`。

搜索顺序完整为：**当前选择槽（若 0–49 且合格）→ 其他快捷栏 0–9 升序 → 普通背包 10–49 升序**；先尝试已请求的背包快照，找不到再读取原对象。不是最高价值/数量最少/优先化石排序。运行中会逐渐消耗可见候选，当前槽耗完后能切向别种材料。

### 6.2 机器全集、距离、排序和扫描

机器仅两种：Tile 219 普通提炼机，Tile 642 叶绿提炼机。当前自动选择没有“优先叶绿”“只用叶绿”的模式。

从所选材料读 `tileBoost` 与玩家 `blockRange`，取 `max(0, tileBoost+blockRange)`；优先 `TileReachCheckSettings.Simple.GetTileRegion(player,...,reachBoost)` 并裁世界边界。兼容失败退为玩家 Tile 中心 X/Y 各 ±12 的 25×25 矩形。

扫描所有活动且 type 为 219/642 的格子，取**格子中心**距玩家 mining center 的平方距离最小；近似同距离以 Y 小、再 X 小优先。不把多格家具归一成一个整体，也不额外按种类优先，结果是某个机器组成 Tile 的中心。普通机器较近时可以抢在叶绿机器前面。候选发现不等于原版最终允许消费；真正范围原版还会核对。

Z 服务自带 1 Tick 检查/使用冷却，但**外层调度器为 3 Tick 节奏**，不能写成每 Tick 必执行一次提炼，也不能把“三 Tick 扫一次”当作“三 Tick 消耗一个”的吞吐保证。

证据：Z `AutoExtractinatorService.cs:36–38,166–227`；`AutoExtractinatorCompat.cs:223–348,350–352,374–447`；`Runtime/RuntimeAutomationDispatcher.cs:97–98,470–475`。

### 6.3 生产调用、选槽/鼠标恢复、停止与失败

真实生产链：

`AutoExtractinatorService → ItemUse 请求 → UseSelectedItemActionExecutor → ItemUseBridge → Player.ItemCheck Hook → 原版 PlaceThing_ItemInExtractinator`。

请求 Low，排队 250 ms、执行 900 ms，显式借用 `UseItem | MouseTarget | InventorySlot | HotbarSelection | BridgeItemUse`。metadata 设置世界鼠标为目标 Tile 中心，`ApplyMainMouseLeftForItemCheck=true`、允许剩余 2 Tick 的 early ItemCheck 窗口。没有连续批量 repeat=8 参数；那个参数属于另一条遗留执行分支。

生产层拒绝死亡/幽灵、菜单、聊天、NPC 对话、箱子和鼠标浮动物品；背包开关本身不阻挡。队列或桥有任意其他工作就让行。功能关闭/玩家失效清本服务 tracking；失败/无机器/无材料会记录状态并等后续扫描，不建立“整批失败后永久停机”的作业。

执行器首先切换到目标槽，必要时等待 Terraria 原版选择生效；Bridge 临到 ItemCheck 时检查玩家有效、槽仍被选中、物品类型仍与 Bridge 记录一致、剩余堆叠大于零，并等待动画/使用时间/重用延迟。鼠标位置、Tile target、使用键等输入在 Hook 中捕获/恢复。

**选槽恢复的实际差异**：生产请求没有 `RestoreSelectedSlotAfterItemUse=true`；通用执行器的这个选项默认 false，Bridge 的 restore override 为 -1，因此 Hook 恢复其捕获时的槽，而该捕获发生在服务已经选择提炼材料之后。源码不能支持“提炼结束必回到启动前武器槽”的说法；正常链可留下提炼槽。与快捷工具临时选槽后返回的体验不同。

还有两个执行边界不能掩盖：

1. 请求里的 `AutoExtractinatorItemType` 没有被通用 `UseSelectedItemActionExecutor` 当作类型前置条件；它在执行时读**当下槽中的物品**并将其作为 Bridge expected type。若提交到执行之间槽内物品已换，服务原始候选和最终使用对象可能不一致。
2. 目标 Tile type/X/Y 主要用于瞄准/诊断，通用 executor 不再次证明该 Tile 仍为所选提炼机；原版执行当下才决定走提炼、其他物品使用或放置行为。不能把陈旧瞄准请求当作“只会提炼”的证明。

证据：Z `AutoExtractinatorService.BuildRequest:230–260`；`Actions/Executors/UseSelectedItemActionExecutor.cs:13–105,120–248,265–315,406–431`；`Actions/ItemUseBridge.cs:216–382`；`Hooks/ItemUseHookCallbacks.cs:379–394`；`Compat/TerrariaInputCompat.UseItem.ItemCheckBridge.cs:373–409`。

### 6.4 原版真实消费、完整输出规则与额外交易边界

V8 `Player.PlaceThing_ItemInExtractinator:41981–42023` 在当前目标 Tile 活动、真实可达、itemTime=0、itemAnimation>0、controlUseItem 为真时才处理。叶绿机使用时间乘 0.33。原版先尝试叶绿 `ItemTrader` 交易，再尝试 `ExtractinatorMode` 随机提炼：

- 交易成功：播放声音、设置使用时间、标记 `SkipItemConsumption=true`，扣 `TakingItemStack`，归零变空气，生成 `GivingItemStack`。
- 随机提炼：应用使用时间、声音，调用 `ExtractinatorHelper.RollExtractinatorDrop(mode,tileType)`；物品后续通过 ItemCheck 的通用 consumable/`CanConsumeConsumableItem`/`ForceConsumption` 规则消耗，Z 没有自行减堆叠。
- 产物 `DropItemFromExtractinator` 在鼠标世界位置生成；智能光标/手柄情形取玩家中心；`EntitySource_TileInteraction`、`ReserveForLocalPlayer`，客户端经原版消息 21。**不是必定即时进包。**

完整模式输出集合与规则：

| Mode | 完整结果边界 |
|---:|---|
| 0/1 | 可能产生 71/72/73/74 钱币，1242 AmberMosquito，六宝石 181/180/177/179/178/182，999 Amber，基础矿 12/11/14/13/699/700/701/702；叶绿机且 `Main.hardMode` 时矿石池扩为上述八种加 364/1104/365/1105/366/1106。mode1 还先有 3380 FossilOre 分支，并改变后续昆虫/宝石/琥珀参数。 |
| 2 | 2674 ApprenticeBait、2006 Snail、2002 Worm、2675 JourneymanBait，各一次输出 1；判断顺序是 `Next(4)!=1`，否则 `Next(3)!=1`，否则再 `Next(3)!=1`，最后余支。相应概率为 3/4、1/6、1/18、1/36。 |
| 3 | 普通机在 4349–4353 五种普通苔藓中均匀给 1；叶绿机先以 1/10 进入五种发光苔藓池 4354/4389/4377/5127/4378，否则仍为普通五种。输入可为 RainbowMoss 5128，但这个结果池不回给 5128。 |
| 4 | 1/50 进入三种种子 62/195/194 的等概率池，否则给 2 DirtBlock；数量 1。 |
| 5 | 1125 HoneyBlock，数量 1。 |
| 6 | 169 SandBlock，数量 1。 |

mode0/1 的完整随机判定顺序可复原为：

1. mode1 先以 `Next(10)==0` 给 3380；
2. 尚未命中时以 `Next(2)==0` 进入第一钱币池；
3. 尚未命中时测 AmberMosquito：mode0 `Next(5000)==0`，mode1 为整数 `5000/3=1666`；
4. 尚未命中时测六宝石：mode0 `Next(25)==0`，mode1 `Next(50)==0`；
5. 尚未命中时测琥珀：mode0 `Next(50)==0`，mode1 `Next(20)==0`；
6. 尚未命中时以 `Next(3)==0` 进入第二钱币池；
7. 最后从上述实际机器/世界对应矿池均匀选择。

这些概率是**按顺序的条件概率**，不能直接当作全局掉率相加。宝石、琥珀和矿石初始数量 1，再独立以 1/20、1/30、1/40、1/50、1/60 概率分别加均匀 `0..1/0..2/0..3/0..4/0..5`，总量 1–16。化石矿初始 1，独立以 1/5、1/10、1/15 概率加 `0..1/0..2/0..3`，总量 1–7。

两套钱币规则也不同：

- 第一池按 `1/12000` 铂、否则 `1/800` 金、否则 `1/60` 银、否则铜；数量初始 1。铂三次各 1/14 机会加 0–1；金五次各 1/6，前四加 1–20、末次 1–19；银四次各 1/4，前三加 5–25、末次 5–24；铜四次各 1/3，前三加 10–25、末次 10–24。
- 第二池按 `1/5000` 铂、否则 `1/400` 金、否则 `1/30` 银、否则铜；数量初始 1。铂五次各 1/10 加 0–2；金五次各 1/5，增量区间同第一池金；银四次各 1/3，区间同第一池银；铜四次各 1/2，区间同第一池铜。

完整证据：R `.local/全功能调查/原版/Terraria.GameContent.ExtractinatorHelper.cs:5–525`，SHA-256 `1DFCA5AE7DA7E83571752422FFA7CA9CA4CD2D0D3BABF54E9259E9258E192277`；V8-Player `41981–42023,42389–42406,43608–43647`。

**叶绿机额外交易全表**如下，都是 1 换 1：

- 双向：`12↔699, 11↔700, 14↔701, 13↔702, 56↔880, 364↔1104, 365↔1105, 366↔1106`。
- 双向：`20↔703, 22↔704, 21↔705, 19↔706, 57↔1257, 381↔1184, 382↔1191, 391↔1198, 86↔1329`。
- 单向循环：`134→137→139→134`。
- 单向净化：`{61,836,409}→3`；`{370,1246,408}→169`；`{833,835,834}→664`；`{3276,3277,3339}→3271`；`{3274,3275,3338}→3272`。

`TryGetTradeOption` 按添加顺序取第一个类型相同且数量够的规则。**这些 52 条有向交易不属于 Z 当前 16 种自动材料选择集合**；不能写成“开启自动提炼就会自动交换所有矿石/锭/污染物”。它们是原版同出口支持的额外手动入口，也是未来扩大范围需明确授权的分支。

证据：R `.local/全功能调查/原版/Terraria.GameContent.ItemTrader.cs:17–24,31–79,82–108`，SHA-256 `1491EEA7752E2491990819E302562E74C709F38D5698FEAB8CD14AB9F53BE2E2`。

### 6.5 “成功”并不等于产物已确认；遗留分支另列

ItemUseBridge 的 `Succeeded` 是观察到任意使用相关变化：动画/使用时间开始、reuseDelay 增加、类型改变、堆叠减少、生命/魔力/最大值增加、Buff 数或总时长增加。它不专门要求材料减少，更不要求提炼产物生成、拾取或多人确认。无变化才 `AttemptedButUnverified`。因此目标无效但原版起手动画发生，也不能凭通用 `Succeeded` 宣称已经成功加工。

证据：Z `Actions/ItemUseBridge.cs:385–412,499–516`。多人原版请求和本地动作返回应分别呈现；本次无多人运行证据。

另有遗留 `InventorySlotActionExecutor.StartAutoExtractinator:428–462`，会调用 `AutoExtractinatorCompat.TryRapidExtractSlot:460–546` 连续 `ItemSlot.RightClick`，默认八次、上限 30、检查 expected type，但不重新检查 `ExtractinatorMode`，after 读取失败亦计一次消耗。当前生产 `BuildRequest` 是 `ItemUse`，查找未见该自动服务生产 `InventorySlot` 的入口。原版普通材料右键可能只是向鼠标拿取物品，不能把这段未接生产链的“stack 变少”统计当作提炼成功证据。它应记录为遗留技术分支，不列为用户可选的另一种提炼模式。

### 6.6 数据、工作量、跨功能与 R

Z 开关落全局配置；所选材料/机器坐标/时间戳仅诊断状态；`ExtractinatorMode` 数组与右键 MethodInfo 进程缓存，解析失败也缓存，不含程序集更换/角色切换的版本号。产物、材料堆叠、机器 Tile、钱币归原版；没有可断点续做的加工清单。

关闭稳态外层跳过。开启无材料可先做最多 50 槽选择而不扫 Tile；有材料无机器时每外层 3 Tick 可再扫描可达区域，兼容退路最多 625 格，没有该分支的机器目标 TTL 缓存。进行中还借用动作队列、选槽等待、桥/Hook、原版随机产物与同步；大量材料延长总运行次数，机器扫描区域随真实 reach 增大。没有实测 FPS、吞吐或分配结论。

与其他功能的具体两端关系：

- 与武器/快捷工具/药水：争用选槽、`controlUseItem`、鼠标世界目标与 ItemCheck 时隙；提炼没有完整旧槽恢复选项，不能只说“共享背包”。Z `AutoExtractinatorService.BuildRequest:230–260` 对接 `UseSelectedItemActionExecutor:33–105,147–151`；R 未来应使用 `HostInputState.CanStartActions:27–29` 和唯一操作输入 owner，不能恢复已经被别的功能接管的新输入。
- 与家具/采矿：共享可达 Tile 事实与世界鼠标，但发现策略和成功证据不同。R `WorldTileObservation:21–28,56–72` 是候选事实接口，不能将它升级为“提炼成功”或全局世界操作状态机。
- 与自动卖/扔/存：非币产物真正被拾取后，R 现有 `PickupItem → GetItem` 因果来源可以观察；原版产物刚生成在地上还不是背包来源。R `ItemSourceHooks:19–22,36–40,53–98` 两端与 V8 `DropItemFromExtractinator:42398–42405` 相接。提炼中没有连续开袋专属的 24 Tick 让行或受控批次来源快照，不应凭时间窗口补票。
- 与自动存钱：提炼币先经过世界生成/拾取，之后钱币功能才可存；R 普通来源排除 71–74；钱币银行保护仍由钱币主题处理，不能由提炼强行解除手动取出保护。
- 与保持收藏：Z 材料选择没有 favorite 排除，收藏在此不提供“不可消耗”的保证；R 未来若承诺保护收藏，必须在执行端再核验并和武器/药品/工具规则一致。
- 与手动取出、装备：手动拿到的材料不因“类型可提炼”变成普通物品处理的新来源；把当前槽装备/武器换成提炼材料或反过来，需要当前对象身份与期望类型检查。R 现有 `ItemPendingGuards.StartUse:101–102` 和选择/消费保护是安全事实边界，不能绕过。

**待决定**：哪些材料默认允许被自动消耗、是否区分普通机/叶绿机、是否保留数量/收藏、是否自动换材料、加工时是否抢当前武器槽及怎样还原、是否扩展 52 条交易、成功如何以实际消费和产物界定、多人不确定时如何停机。均未授权实现。

## 7. 六分支共同的输入、关闭、数据和跨组边界

### 7.1 关闭、失焦、世界/角色变化

Z `RuntimeServiceScheduler.Evaluate:42–102` 对启用→关闭有一次 `ExecuteDisableCleanup`，关闭稳态 `SkipDisabled`。`RuntimeAutomationDispatcher.PerformanceMeasurement.cs:111–136` 还按 action lane 的世界、菜单、聊天、`GameInputAvailable` 门调度；失焦不是“结束世界并撤销已发出的动作”。

本专题六种服务的 `ClearState/ClearTracking` 和停止生产，不能自动等同于取消队列或原版部分副作用。本次查到 `InputActionQueue.CancelBySource`/`ItemUseBridge.CancelBySource` 的实际调用者在采矿、部分武器主题，本专题服务没有使用这些 API 的取消链。已入队请求有短 TTL 和执行端各自门，但缺统一开关版本/Session generation 的最终消费核验。报告应同时写“关闭后的后续生产停止”和“已排队/已发生结果如何处理”，不能合并成一句“关闭立即恢复”。

R `HostInputState.cs:11–12,27–29,35–54,74–95` 已把失焦、原版输入许可、最终样本和真实物理释放重新武装统一管理；焦点切换不是 Session 边界。对本专题按住手势、F5 输入和已知来源的后续自动加工，应复用该门，不再各自 OR 一个 OS 按键源使被消费输入重现。

R `ItemOperationOwnership.SetSession:20–32` 的新 Session 是结束旧范围的解释，不是撤销原版转移；现有未确认结果在原 Session 仍保留保护。护士/重铸消费与开袋/提炼不能用“切了世界所以回滚”解释已经扣掉的钱或材料。

### 7.2 数据谁所有、保存什么

| 数据 | Z 所有者/保存 | R 调查结论 |
|---|---|---|
| 六功能开关、重铸字符串名单 | `AppSettings`，`ConfigService.SaveAll`，全局 `appsettings.json`；目录启用与快捷键另有各自配置文件 | 后续设置归具体 Feature/正式配置合同，不复制巨型 AppSettings 为模块依赖 |
| 护士生命/Buff/钱、税款、重铸物品词缀、袋/钥匙/材料、产物、Tile/NPC | Terraria 原版；角色/世界自己的保存 | 自动功能只观察和请求原版操作，不拥有第二账本/第二背包 |
| 冷却、上次目标、hold、让行、扫描缓存、反射句柄 | 各静态服务/Compat；不作为可恢复任务持久化 | R Session 生命周期提供唯一 generation；共享观察不能带业务启用和保鲜策略 |
| 控制输入、鼠标覆盖、选槽与临时 UI 状态 | 动作 executor、ItemUseBridge、Hook/Compat 多段协作 | 必须明确谁归还什么、何时不再有权恢复；不能以通用 finally 覆盖后来用户状态 |
| 来源票据/未确认写范围 | Z 开袋批次有明确边界；R 已有因果来源和三操作 owner | 可复用既有事实/保护，不把所有加工视作“背包净增”来源 |

配置路径证据：Z `Config/ConfigService.cs:27–30`；分支字段/默认前文已列。所有设置默认关闭并不代表已入队动作必定被撤销。

### 7.3 按关系类型整理，避免错误依赖

| 两端 | 关系性质 | 具体证据和应保留语义 |
|---|---|---|
| 护士 ↔ 回血/回蓝药/家具/增益药 | 用户体验顺序、运行期竞争 | Z `AutoRecoveryService.Tick/Decisions` 普通分支顺序 Heal→Mana→Nurse→Station→Buff；不是护士功能必须依赖增益药功能开启 |
| 护士/原版重铸购买 ↔ 钱包/银行 | 真正消费前提与冲突 | V8 `BuyItem` 读背包及四银行；R `ItemPendingGuards.Buy:126–137` 保护可能被消费或写找零的待定范围；存银行不是预算隔离 |
| 收税/提炼 ↔ 钱币自动存入 | 两阶段结果协作 | V8 先 `RequestNewItem`，实际拾取后再存；Z 自动存钱独立门，R 来源明确排除钱币 |
| 连续开袋 ↔ 自动存卖扔 | 来源事实复用；24 Tick 是 Legacy 偶然耦合 | Z `QuickBagControlledBatchSourceService:21–79` 与 `QuickBagOpenService:352–384`；R `ItemSourceHooks:77–113` 是因果完成/异常范围，不继承整个全局队列和固定时间窗 |
| 开袋 ↔ 钥匙/虚空袋 | 真正资源/资格前提 | V8 `ItemSlot.TryOpenContainer_GrantItems:777–791` 分别消耗 GoldenKey、检查 ShadowKey；不能只锁袋槽而漏钥匙消费 |
| 家具/提炼 ↔ Tile 观察 | 可复用事实 | Z `StationBuffCompat` 七种发现、`AutoExtractinatorCompat` 两种发现；R `WorldTileObservation` 提供可读 Tile/世界身份，业务结果仍在功能内 |
| 护士/税收 ↔ NPC 观察 | 可复用事实与目标身份 | Z 目标 index 查找；R `NativeNpcObservation` 对象与 generation 可防索引复用，不能把“最近 NPC”一律缓存为共享权威 |
| 提炼/药品/快捷工具/武器 ↔ 输入与选槽 | 运行冲突和恢复一致性 | Z ItemUse 通道与 Hook；R `HostInputState` 统一输入门，必须区别输入返回和初始槽返回 |
| 加工 ↔ 已完成 R 存卖扔 | 既有安全接口，非重审授权 | R `ItemOperationOwnership` 仅三真实操作、`ItemPendingGuards` 和 `ItemSourceHooks`；#48 接受/延期边界不因本调查扩大 |

### 7.4 性能和验证证据的实际强度

- 关闭稳态：外层调度保留有界开关/节奏判断，业务主体跳过；连续开袋已安装 Hook 仍有最小检查。
- 开启稳态：护士 NPC 查询、税收缓存、家具缺 Buff/TTL、开袋 50 槽和八次批量、重铸名单每 Tick 处理、提炼每三 Tick 局部扫描，各自成本不同。
- 状态变化：NPC 换索引、家具拆装、玩家跨格、配置/名单变化、Buff 到期、满包/世界溢出、原版网络未确认，需要对应事实失效，不宜每 Tick 全部重建，也不能无限沿用旧缓存。
- 大数据：重铸名单无本分支数量上限；开袋/提炼长堆叠主要增长运行次数与产物/网络数量；家具结果最多七类但可达区扫描大小仍会增长。
- 失败路径：反射/原版调用异常有吞异常、节流日志及诊断；原版部分完成不可被“失败”或“超时”自动反向补偿。
- 已见 Z 测试源码包括默认关闭、请求 metadata/通道、重铸前缀匹配/冷却、家具冷却快速跳过/全 Buff 跳过/缓存复用/反射 fallback。`tests/JueMingZ.Tests/Program.AppSettingsDefaultTests.cs:44–54,106–107`、`Program.ActionChannelResolverTests.cs:233`、`Program.AutoStationBuffPerformanceTests.cs:19–167`、`Program.cs:2330–2346,2748–2752` 提供静态测试覆盖入口。**本轮未执行；这些测试也不能证明重铸收费、多人到账、实机手势或 FPS。**

## 8. 可直接进入产品讨论的主要决定清单

1. 护士是否允许打断已有 NPC 对话，是否设费用/血量触发限制，药品与付费治疗的选择顺序。
2. 家具仅刷新缺失 Buff 还是临近到期刷新；是否纳入携带家具；执行时目标变更与 UI 恢复失败怎样呈现。
3. 收税是否零星即领，玩家看到的是领取税款、金币生成还是实际到账，之后自动存钱如何反馈。
4. 重铸必须补齐哪一层原版付费语义；词缀用 ID 还是文本；预先已命中、错误名单和按住会话重新开始规则。
5. 连续开袋是否始终锚定当前堆叠/类型，是否跳过收藏、保留袋/钥匙，产物满包与多人待定时是否继续。
6. 提炼是否可自动耗掉全背包材料、是否优先叶绿机、是否归还原武器槽；52 条交易是否另外定义模式。
7. 每个不可逆操作怎样以真实消费和结果确认结束；异常、关闭、死亡、换角色/世界时哪些动作能取消、哪些只能停止重试并保留未确认。
8. 先复用 R 已有输入、Session、NPC/Tile 观察、来源与物品保护的具体职责；对尚未存在的 NPC 服务/加工操作保持“候选设计”，不以 Legacy 的类名、队列和目录标签作为实施合同。

**专题结论**：六项 Legacy 能力均有明确源码入口，但原版收费层次、批量开袋与 Hook 双入口、提炼的当前 ItemCheck 路径和选槽恢复，是本轮必须保留的实际差异。R 目前具备若干安全与观察基础，没有本专题六种自动功能的完整生产实现；本次调查不宣称可用性、多人、性能或所有者验收已通过。

# #114 算法改造：机制覆盖对账

TASK_BASE `97ceecf7c8706779bc2cf9acfff795830eb449da`。下表保留初始机制索引与调查边界；当前逐成员源码分类、真实消费者及适用原件见 `attack-lifecycle-map.csv`、`weapon-mechanisms-{A,B,C}.csv`、`native-event-closure.csv` 和 `native-player-callers.csv`，不是由本表的旧“将核/待核”文字推断仍未实现。当前算法固定ap，120特殊出生映射优先按真实caller适配，再进入同机制shared batch；117非selected、5348→1006；功能/普通/成本旧身份与新增证据分别保留、功能累计/普通机会/成本/类型归属与尚未准入状态均以 `07-complete-candidate.md` 前部为准。索引和字节解析不自动升级为全组合运行PASS。

轻量枚举原件 `.local/aim-algorithm-rework-20261015/driver/profile-base-magic-both-r01/out/`；实际锁定原版完整 Main 初始化后读取 ContentSamples，调用生产 `HostAttackAim.Eligible`、`AttackAmmoSnapshot.Capture`、`HostAttackModels.TryRead` 和真实分流谓词。每个符合资格的物品枚举当前版本所有匹配 `useAmmo` 的弹药及 alt0/2；无弹药物品仍一条每模式。没有调用有RNG/消费副作用的PickAmmo或为每物品运行AI。2944行是输入组合，不是2944种能力。纯元数据不能证明可使用右键、所有出生模式、派生或实际命中。

[攻击附表](attack-route-inventory.csv)给出真实解析后的弹种、普通模型参数及候选机制谓词；`mechanismHandlers` 只是匹配实际弹种的候选谓词，最终优先分流按源码。stageNote仅style99明确标持球/释放，其余仍须源码对账。[NPC附表](npc-route-inventory.csv)枚举762个netID（含别名），默认接收资格、结构谓词与已知机制谓词。默认状态与所有战斗阶段不同；`StructuredModel=false`、`Handles=false`均不能单独认定不支持。

## NPC

| 机制／实际分支 | 成员集合或可复算谓词 | 必要阶段／关系 | 生产路径 | 本轮处置／验证去向 | 边界 |
|---|---|---|---|---|---|
| 接收与身份 | `CombatSelection.Receives`：active/life/dontTakeDamage；dummy独立；普通非friendly/immortal | slot/generation/type/netID，实际易受击阶段 | Selection→Source→Rolling→Cache→同身份消费者 | 所有NPC附表逐项进入实际Known/Step分流对账；保不接收阶段反例 | 默认资格不替代后续暴露阶段 |
| 地面／跳跃 | NpcMotion style1及NpcGroundMotion.Known（按具体type谓词） | 重力、地形、随机决定界限、实际玩家阶段 | Source.PrepareSample→Rolling.Prepare→NpcMotion.Step | C将直接保护自然NPC1及相关分支；源级／原版Basic/Foundation对照 | 不将未发生随机动作声称逐帧已知 |
| 飞行／追踪／分段 | NpcMotion所有有限Known谓词，含NpcFlying/Phased/Ambient/Finite/TerrainFlight | 相位、target、运动／玩家前提 | 同上，具体motor | 复用现有各机制原版检查；改公共核心时按实际依赖传播 | aiStyle共享不等于成员等价 |
| 关联／多节／Boss | NpcMotion和Source关系捕获的worm、attached、brain、wall、tower、cultist、moon等具体谓词 | 身份／槽序／关系、玩家、相位／事件闭包 | Source关系闭包→Rolling私有实体组→发布 | C分类并标明未变化可复用记录；不把结构false统一回线性 | 关联／未知事件保原停止语义 |
| 准备／续算／发布 | 同sample完整state/environment/player/roles/terrain；跨sample须独立许可 | BeforeNpc／Projectiles／CompletedWorld | Source、Rolling.Progress、HostObservation | 已证真实重复扩窗热点；新策略只在前提完全成立时复用 | 输入许可仍由当前consumer独立验证 |

## 攻击／阶段

| 机制／实际分支 | 可复算成员／输入 | 出生／持续／派生消费者 | 解法／本轮处置 | 验证去向／未决 |
|---|---|---|---|---|
| 普通离散弹道 | 实际Capture后 `HostAttackModels.TryRead=true`，扣除先行专用分流 | 原版ItemCheck_Shoot实际初速；extraUpdates/首更新、delay/重力/阻尼/风/限速 | AttackIntercept；B增可分离离散响应候选，保全部适用成员 | 同输入＋实际原版Update/Damage；普通/重下坠近中远高低弧 |
| 参数化特殊拓扑 | HostFiniteThrow/FiniteShot/Explosive/MagicBounce/Crystal实际Handles | 初态、反弹、穿透、寿命、爆炸／子体／真实slot顺序 | B共用输入提案，原机制精算保留；不以无直撞当无派生 | 现有finite-throw/finite-shot/explosive/ai008/crystal叶＋新增候选原版回放 |
| Betsy／瓶类 | 711；Bottle实际Handles全部成员 | 三火焰各local免疫／拖尾／140Kill，瓶高低slot子体 | 保原专用motor；B按可证明接收独立性缩相关场景 | 方向/环境免疫/延迟/槽位反例及原版父子命中；不能仅靠owner当前0推未来 |
| 其他有限专用 | 附表effect/attached/melee与HostEffectAttack有序分流 | 冰/火/云/音符/宝石/天空等实际原版阶段 | 保正确专用路径；新候选按实际适用接入 | 对应NativeCombat*Checks既有叶，不机械统一算法 |
| 近战／混合派生 | Swing、spear(ai19)、arc/whip、boomerang等具体Handles | 真实身体/挥砍/矛/鞭/派生伤害；Facing与鼠标消费者不同 | 保Basic合法方向与远端派生提前量 | Mechanics＋MixedSwing/derived/ownerHitCheck；不能用父体命中代子体 |
| 天空／光标生成 | HostSkyAttack按真实物品（不能按projectile ID判断） | 地形门、真正出生、多个源点／分支 | 保现行六类位置依赖分流 | 现有sky/roof/多发真实消费者 |
| 持续引导／束／手持 | Guided/held/laser/flail及C新核真实读取点 | 起手、持续AI、松手、自动锁定后独立 | C核全原版鼠标读取点和源注册；释放后不持续干预 | Control/Beam/源receipt与新增漏路由原版消费者 |
| 普通悠悠球 | 所有实际ai99并适用Held角色，球组/配重 | 真尺寸、惯性、绳长、导航/实际Damage | 保原完整导航；A显示Replay不是该Main主耗，未验证前缀原型撤回 | 原contact/窄口/封口/角色/取消检查，不缩球或假Contact |
| 省力魔法绳 | 实际普通球+magicString阶段 | 真实起手/最后持球→自由父/分体→封闭输入 | 保现行有限释放响应；与NPC新共享策略整合 | 原release精度/低弧反例＋自然父子/每4更新/取消 |
| 哨兵／工具／其他操作 | 实际Eligible排除项及placement/summon规则 | CanUse/Shoot真实鼠标；鞭/右键指定单独目的 | 保现行不重定向，不能一起删除召唤 | Placement/Basic/资源/相邻consumer |
| 待核分流集合 | 附表ordinary-or-control且motion=false共204行；NULL_CAPTURE共6行 | 包含上面合法控制、同物品后转换及真实旧缺口 | B核非控制、C核持续/释放，最终逐集合闭合 | 初表不是204个缺陷；也不把default/null统一填不适用 |

## 选择／显示／生命周期

两中心×最近/清线、dummy、Aim-only/Path-only/Both及G11A节奏分别保原实际输入许可。最近不能因难解换目标，清线区分可达/未知/已证不可达。显示不作为瞄准前置；Draw只读发布事实，多Draw与实际GUC分母分别记录。关闭、松手、换物/弹药/目标、死亡、失焦/UI、换世界各撤对应工作与输入，不用一个失败停止全体。

当前准入：覆盖对账与实施进行中；没有全范围PASS，也没有普通候选/安装授权放行。

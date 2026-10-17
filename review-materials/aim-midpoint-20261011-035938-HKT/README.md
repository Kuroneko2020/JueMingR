# 辅助瞄准全面返修｜中场暂停｜2026-10-11 03:59:38 HKT

**这是未完成工作的检查点，不是交付、验收或可合并结论。** 本轮尚未产出可交给玩家使用的新包。已有选敌、只读预览、实际弹药转换、若干武器和 NPC 运动家族的生产改动，以及部分真实原版消费者对照；它们值得保留，但尚未组成完整可用结果。

暂停时源码还有明确的破口：南瓜王草稿引用了尚未加入状态结构的 `PumpkingModeUnknown`，**当前源码不可编译**；Harpy/Bee 的 R24 产品修改没有同步旧测试期待。最后可编译的是更早的 **R23b**，它又有反射接收、火焰红框两项失败，三个检查被正常取消。没有为了提交检查点补修或补测。

当前最影响玩家使用的缺口是：部分武器仍没有完整运动／派生／已出生来源链，闲置红框还没有全家族持续可见及最终绘制证据，NPC 默认分派仍有未闭合家族。最近一次有效减少不确定性发生在暂停前约两分钟：R23b 宝石运动专项完成；反射和火焰专项则把问题推进到了更具体的接收／标记断言，并没有解决它们。

最反复的工作是“新增运动家族 → 原版对照 → 发现状态／夹具前提不一致 → 返修再跑”。其中有必要的真错误定位，也有验证投入超前于玩家整合结果的情况。具体区分见第 3、5 节。以下只使用已有记录和暂停快照，不追加实验。

审阅源码：[4850a290e7ec7f3d086de5e4efe48694adc4c821](https://github.com/Kuroneko2020/JueMingR/commit/4850a290e7ec7f3d086de5e4efe48694adc4c821)。
本轮产品增量：[223c121 → 4850a29](https://github.com/Kuroneko2020/JueMingR/compare/223c121dd3ff3a62eb5640edaae8d26add9495f3...4850a290e7ec7f3d086de5e4efe48694adc4c821)。材料提交的 SHA 由唯一 Issue 评论给出，不能当作被测产物身份。

## 1. 真实起点、Git 和暂停现场

| 项目 | 已核事实 |
|---|---|
| 仓库根目录 | `C:/Users/kongd/Desktop/JueMingR`，由 Git 确认 |
| 分支 | `codex/aim-attack-modeling-and-integration`；未新建工作树 |
| 接手会话起点 | 2026-10-10 17:12:31，Asia/Hong_Kong；当时工作区在文档任务提交 `f470072446d4879da838748d06e214bf63813943` |
| 原辅助瞄准任务分支 BASE | `80c0fdde7d1451aa9d3ae844c20387611bb36dcd` |
| 正常接回及文档合入 | 17:17:19 切回原任务分支，并以 `cherry-pick -x f470072…` 合入为 `223c121dd3ff3a62eb5640edaae8d26add9495f3`；不是重置到旧参照 |
| 本轮产品修复基底／暂停 HEAD | `223c121dd3ff3a62eb5640edaae8d26add9495f3`；文档整合提交当时已提交未推送 |
| 暂停时远端 HEAD | `80c0fdde7d1451aa9d3ae844c20387611bb36dcd`，`git ls-remote` 实查 |
| 统一安全暂停时刻 | **2026-10-11 03:59:38.3612303 +08:00** |
| 此后授权检查点 | `4850a29…`，普通 WIP 提交；未通过最终验证，明确不可编译 |

不使用长期 PR 与 main 的共同祖先充当本轮 BASE。`f470072` 是进场时另一个文档任务的 HEAD，`80c0fdd` 是原任务分支基底，`223c121` 是保留文档成果后的实际产品修复起点，三者含义不同。

暂停时本轮文件统计如下，仅说明现场规模：

| 类别 | 已跟踪未暂存 | 新增未跟踪 | 合计 | 新增／删除行 |
|---|---:|---:|---:|---:|
| 产品源码 | 50 | 67 | 117 | +12377 / -586 |
| 测试 | 24 | 58 | 82 | +10020 / -53 |
| 开发工具 | 2 | 0 | 2 | +69 / -1 |
| 文档（含 AGENTS） | 5 | 1 | 6 | +118 / -7 |

暂停时暂存区为空。这 207 个归属明确的文件已保存到源码 WIP；没有把文件数或行数当进度。原有 `docs/设计/本地参考资料使用说明.md`、`.workbuddy/memory/2026-10-03.md`、`issue112.txt`、`pr113.json` 四项资产均排除，暂停快照核对其 SHA-256 与入场记录一致。[完整暂停状态](status-before.txt)、[文件归属／哈希／取消记录](snapshot.json)可查。

本地完整恢复材料保留在 `.local/aim-comprehensive-repair-20261010/midpoint-20261011T035938+0800/`：`unstaged.patch`、空 `staged.patch`、文档整合的 `base-to-head.patch`、新增文件文本。在线直接读取固定源码提交，无需再应用补丁，也不复制第二份已提交源码。未接入生产的 R24 直线查询和 Bee oracle 草稿另放 [unintegrated](unintegrated/)，明确未编译／未测试；来源和哈希见 [copied-materials.json](copied-materials.json)。历史草稿和全量日志仍留本地。

现装只报告已知事实：**本轮全面返修没有打包、安装、恢复或写入游戏目录，没有启动／操作游戏，没有修改真实存档或用户配置**。所有者此前自行安装并测试的事实有效；旧包、恢复资料保留，本报告没有重查安装状态或生成新回执。

## 2. 用户要求推进到哪一步

统一原反馈配置：清线优先、玩家中心、开启追踪人偶及目标标记；弓用**灵液箭**，枪用**无尽之弹袋普通子弹**。未记录的其它装备／开关不补造。表中“原版消费者”指隔离的实际 Terraria 方法调用，不等于真实完整游戏或所有者体验。

| 领域 | 已查清的原因／实际改动 | 现有证据和代码 | 未解决部分／当前等级 |
|---|---|---|---|
| A 武器与弹药 | 显式有限模型漏了实际转换后的射弹；旧测试混淆 Item 278 与 Projectile 278，不能证明灵液箭。补实际 ammo capture、模型分派、原版出生时钟、子更新及派生家族接收；多种控制型武器另走实际输入入口。 | [Ammo][ammo]、[Models][models]、[Effects][effects]。R10 `weapons` exit 0：枪 95 + 3104 → 射弹 14，首伤 20；弓 1201 + 1334 → 278，首伤 14；还包含 Hellwing、散射等真实 Shoot→Update→Damage。 | 目录、模型、生产接入、实际消费四层没有全部闭合。部分家族仍无模型，部分已出生／派生来源只在组件中验证。**局部整合验证，全面未完成。** |
| B 选敌 | 基础排序与武器求解拆开。最近模式直接用已排序目标；清线模式才尝试实际武器 Contact，并将最终身份提供给路径、标记及输入。真实鼠标在输入租约前采样。 | [Selection][selection]、[ResolveSelection][resolve-selection]。R08 Integration：玩家／鼠标 × 最近／清线四组，挡住的近 A 与可达的远 B；开火前后身份一致，超出悠悠球／近战射程仍保留最近目标。 | 仍有明确基础筛选：active/life/friendly/dontTakeDamage/immortal、人偶开关与半径。距离公式是所选中心到受击矩形最近点的平方距离。清线每阶段最多尝试两项，并优先检查已知可达目标；**尚不能用四组测试证明每帧全体候选严格全排序**。这些细节未完成最终契约复核。 |
| C 红框 | `PreviewDemand` 与实际攻击许可分离；`ExpectedPreview` 是观察结果；命中 `ReceiveTick` 映射回同一 NPC timeline 的中心，`AimInput` 单独用于原版输入。增加冷却延迟、换身份／武器／地形／免疫变化失效。 | [Preview][preview]、[ControlPreview][control-preview]、[Marker][marker]、[SelectionPreviewChecks][selection-tests]。R08 从正常 Update 起步，Aim OFF、不按键，连续四次产生预览并 Capture/Project 到同一路径中心，且无出生／资源消耗；静态免疫到期恢复通过。 | CPU 的 Capture/Project 位置断言有了；**本轮闲置红框的实际 Draw 图像／完整时序图形证据没有提供**。控制族时序并非全连续：3930 原叶只有 1/9 可见；庆典后续是 55/80。R23b 火焰同路径红框断言失败。首次、持续、切换在全部家族上未闭合，不能称“已完全可见”。 |
| D 日耀及同类成本 | 旧 Solar 多个未来种子重复推进相同玩家姿态，尾部退出不足；准备、资格检查和消费重复求解。改为职责内捕获姿态、有限推进、同阶段未消费结果复用及只读租约；Draw 不启动求解。 | [Melee][melee]、[Aim][aim]、[Costs][cost-tests]。R07／R19 完整准备周期组件计数：45 个自然动作、43 个活体动作、6 次实际伤害、36 次预览、180 次模型调用／1057 次姿态；无目标、切换、普通预览等也有断言。 | 不止检查 Draw。但**没有真实 FPS、实机帧时间或同场景全量修前／修后耗时对照**；这些计数不能换算 FPS 提升。R23b 相关成本回归未启动，扩大后的全部模型成本没有最终结论。 |
| E NPC 路径 | 初始目录发现大量可分派类型仍走通用趋势；按阶段、AI 时钟、目标／玩家前提、关联 NPC、地形与采样恢复补具体模型，并贯穿 Source/DTO/滚动一致性。 | [NpcMotion][npc-motion]、[Source][npc-source]、[Rolling][rolling]；[初始分派目录](coverage/npc-initial-dispatch.csv)、[R15 静态阶段表](coverage/npc-r15-static.csv)。初始表 688 条、531 默认项、101 个 aiStyle 是当时目录数据，不是当前可选 NPC 的完成分母。QueenBee R22 八个连续场景 403347 匹配点；Attached R21 八场景及 395 曲率；其它多步／跨阶段证据见原阶段入口。 | 默认生产分派核查**未完成**。Brain 精度修后正式对照未跑；Wall 长前缀后的玩家生命前提差异未定位完；Tree 仅一自然场景完成；Tower 未跑；Pumpking 未接且破坏编译；IceQueen、73/78/79/83/84 等仍欠缺。未知随机和前提变化会截断，剩余默认趋势也不能因非空就当曲线正确。 |
| F 悠悠球 | 抽象格子可达不等于球身能穿过；起点在格内偏移会影响一格通道入边。路径点改为受真实球体／障碍几何约束的通路，并保留上一轮 R02 入边与 R03 持续控球修复。 | [YoyoNavigation][yoyo]、[GapChecks][yoyo-tests]。R04 Navigation exit 0，15 个窄缝原版运动／实际 Damage 场景，覆盖格内偏移、横竖方向、拐角、平台／斜坡；另有真实收回再发和低速失效恢复。 | 局部原版整合验证；不是所有地图几何或真实玩家复验。较早 Control 整组失败仍保留，不能用其中悠悠球有效前缀宣布整组 PASS。 |

### A 的原点名样本与未点名遗漏

| 原反馈对象 | 本轮到达的边界 |
|---|---|
| 海啸、钛金连弩 | 灵液箭真实转换模型已补；散射出生／首伤已有原版证据。随机散布仍为 Representative，不能把代表轨迹称为每支箭必命中。原三弓 × 原配置 × 首发／持续／松开重按／切换矩阵没有全部交付。 |
| 地狱之翼弓 | 实际灵液箭的 launch-X 重力时钟及代表方向原版 first-Damage 对照已有；不是木箭成功替代。完整人偶配置的全部操作周期仍欠。 |
| 漩涡击碎者、外星散弹枪 | 通用实际普通子弹链和多发代表出生／伤害已有；没有足够材料证明这两把在原配置下所有首发、持续及恢复已稳定。保留“有时不锁定”为未验收问题。 |
| 水晶风暴、吸血鬼刀 | AI024 反射／缩放／水中生命周期和刀的整数伤害衰减窗口已有模型与原版专项；不是覆盖全装备状态的最终整合证明。 |
| 日耀喷发剑 | 有实际弧段、未来姿态、真实消费和整个准备周期计数；掉帧的实机结论尚无。 |
| 真铜短剑 | 保留原称，已有 phaseblade / 混合挥击专项及实际正伤害证据；不能由此宣称该原名称的全流程已验收，更没有用普通铜短剑代替。 |
| 附魔回旋镖 | 运动／返回模型已扩展；R22 普通回旋镖专项被 NPC 遗留中毒状态干扰，R23 夹具修改未执行，仍待闭合。 |
| 叶绿镋 | 已识别本体之外的派生远程攻击，加入 222→228 等派生入口；仅近身本体的旧证明不足。全部派生已出生来源的实际观察链仍有缺口。 |
| 幽灵法杖、星星炮 | 对应有限射弹／真实 ammo 路线已有处理；幽灵法杖静态免疫自然到期恢复有 R08 证据。未交付两者原人偶配置的完整周期验收。 |

“目录列出”来源于本地锁版 `SetDefaults` 静态抽取和 `ContentSamples` 实际 Eligible／ammo capture；“有模型”来自分派；“生产入口”与“实际消费者”须另查。现存 [R08 缺口表](coverage/weapon-r08-gaps.json) 是历史表，不是暂停时覆盖认证；`ResolvedCatalog` 中也存在已过时的分类文字，暂停后未修。初始静态物品全集只在本地保留，本次不重新枚举。

已主动发现且仍未闭合的家族包括 Seedler、IceRod、Piranha、Cloud/Rainbow/Magnet/Beam/Inferno、Molotov、Flairon、Typhoon、Bubble、Arkhalis、Clinger、Hex、Nail、Toxikarp、Medusa、LifeDrain，以及新 phaseblade 变体、AI201/203、Cluster II 改地形后阶段。火焰 R23b 部分对照完成但红框失败。Bee、Scourge、Cluster 等局部模型不自动等于已出生／Kill／AI 派生都已接入正常 observer。

NPC 方面，普通飞行、Boss／关联部件、城镇／小动物、DD2／入侵、液体、受玩家运动影响的家族都有新增处理。实际比较曲率、多步和阶段的例子是 Attached 395、PhasedFlight 50 个连续场景、QueenBee 自然阶段和 Golem 多场景，而不只是“路径非空”。日食飞蛾所在机制已纳入分派／阶段工作，原用户场景未实机复验；“毁灭球”“叶绿dang球”名称继续未确认，本次不另行考古。未来玩家临时改输入、外来攻击、未发生随机／网络仍是条件边界，不是取消原覆盖要求的理由。

## 3. 时间去了哪里，哪些反复值得质疑

本轮推进墙钟约 **10 小时 47 分钟**：10 日 17:12:31 至 11 日 03:59:38。下表是有记录的活动区间；同一时间可同时编码、跑测试和等待。不能相加成工时，也无法据现有材料给“编码／测试／等待”精确比例。[日志时间记录](timeline-records.json)保留文件创建／末写时间；进程自身 seconds 另在 [test-identities.json](test-identities.json)，两者不混用。

| 时间（HKT） | 人员／并行情况 | 活动与新结果 | 依据／限制 |
|---|---|---|---|
| 10 日 17:12–17:42 | root；17:18 起武器、NPC、选敌三线 | 接回工作区、核授权／模型、文档整合、共同入口调查和初批编码；17:42 首次构建 | session 创建、Git reflog、build-debug-r01；准备与编码不能精确拆分 |
| 17:42–18:44 | root 串行构建；子线并行源码；选敌完成后换 ambient，之后换独审 | R01–R05 反馈；最近／清线、闲置预览、窄缝几何；独审发现缓存方向／免疫时间／未来 noTileCollide 缺口 | integration-r03/r04、navigation-r04；18:15–18:33 阶段独审 NEEDS_FIX |
| 18:44–22:08 | 武器、NPC、关联三线；root 整合与检查 | R06–R12：实际弹药／控制族、Solar 工作量、派生与地形／液体；R08 Integration 完整退出 0；R10 Aim 完整退出 0；Bubble 到 R12 才完成专项 | 各轮固定身份、相应 receipt；Control 和 Mechanics 中仍有反面证据 |
| 22:08–11 日 01:37 | 同上；构建输出单写，多个隔离 CPU 检查并行，GPU 场景互斥 | R13–R18b 持续扩展 NPC／武器家族；处理原版初始化、生命周期、液体、关联对象和首伤对照 | build-r12/r18b、r18b-closeout；编写和夹具返修重叠，不能全算编码 |
| 01:37–03:20 | 三条子线；root 汇合／复跑选定叶 | R19–R22：DarkMage、Blood、Golem、Attached、QueenBee等局部收敛；同时 Brain、Wall、宝石、普通回旋镖反复 | QueenBee R22 单进程 430 秒；Wall 105 秒；Gem natural 195 秒；三者有重叠，不合计成主耗时 |
| 03:20–03:54 | root 做接收／Flame与整合；NPC做 Brain/Tree；relations做 Tower/Wall；weapon做 Harpy/Bee | R23 编码、x86 精度诊断、夹具前提返修；汇合等待冻结 | 原子线 checkpoint；没有可靠的“纯等待分钟”记录 |
| 03:54–03:59 | root 管构建，5 CPU＋1 GPU 已固定产物检查并行；子线转 R24 草稿 | R23 首编译 6 错→R23b 编译成功；03:57:43 开始检查，完成 1 PASS/2 FAIL，3 正常取消；03:59:38 安全暂停 | build-r23/r23b、最新原始日志、snapshot cancellation |

文档和取证贯穿其中（每轮固定产物、日志、阶段记录），无可核精确独占时长。独审只发生在早期阶段，不是跑了十几小时最终独审；最终集中独审尚未开始。没有运行本轮最终 Related 累计、完整 Release 包或安装链来消耗这些时间。

### 反复最多的具体活动

1. **武器逐家族原版对照（A/C）**：要证明真正的运动、有效伤害和派生接收，先后用通用 Effects，再用 `JMR_WEAPON_EFFECT_ORACLE_ONLY` 限定 Harpy、Scourge、冷射弹、Gem 等叶。发现过真产品漏口，例如宝石 Damage_GetHitbox 的 4 像素膨胀、刀伤害归零及原版液体尾部顺序；也反复处理 NPC buff 残留、场地地板／薄墙被错误设成负控等夹具问题。通用大组失败后拆叶是已采用的窄化；但在未闭合全部 observer 的情况下持续扩展很多族，产出没有及时落到玩家完整使用链，这是调度上的不足。
2. **NPC 连续原版轨迹与关联状态（E）**：只比较一步会漏曲率、选目标、阶段、退出和恢复，所以补长自然场景。实际增加了玩家生命／舌头、Boss 部件、塔护盾灵魂等依赖。QueenBee／Attached 等确有多步新信息；Wall 却多轮在长正确前缀后生命前提不同，尚未分清产品或夹具。R23 改为原生 `UpdateLifeRegen` 阶段前后对照，未执行，不能据代码认为定位完成。
3. **Brain 浮点（最近约两小时，E）**：R20–R22 正式原版边界失败。早先 x64 PowerShell 标量结果不能证明实际 x86/XNA；后来的私有 x86 对照发现原模型 16384 中 8398 不一致，保留较宽倒数中间值后 0 不一致。它确实减少了“为什么现场仍错”的不确定性，但只是私有算术诊断；R23 正式模型／自然路径复跑未发生。不能用放宽容差或该小实验代替正式路径证据。大规模逐字段矩阵是工程选择，用户没有要求每个模型都扩到同样规模。
4. **Gem 碰撞（最近一小时，A/C）**：R22 motor 的 oldPos 精度断言、natural 的实际首伤与预测差一拍同时失败；另一个 collider PASS 却误用了裸 Hitbox。R23 改产品损伤框并改对照接缝；5 个薄墙场景原版确有合法命中，因此原“墙必负控”期待错误。R23b motor 448 组完成，新的 collider 784 组有效前缀出现，但取消前没有正常终结；natural 没有结论。不能选择性沿用旧 collider PASS。
5. **重复基建／资格反馈**：多轮 `build-round` 使用 Debug PrepareOnly＋现有 fixture，未重打包；部分轮因新字段初始化、遗漏 using、原版不存在的 API 或 scope 参数失败，返修没有玩家结果。R23 的 `Projectile.summon`、局部变量名冲突和 `identity` 是实际接口错误，修后 R23b 可编译；R24 新草稿又打破当前源码编译。这不是可接受的交付状态。Workload routing 的 R09/R10/R13/R19/R20 重跑约 49–55 秒一次，主要检查增加的选组接线，不是游戏性能，也不是每次都带来玩家新结果。

继续做的依据确实包括原指令第 119 行“以上是回归入口，不是范围上限”、第 238 行要求跨转向／阶段及足够未来区间、第 335 行要求共同入口先修、覆盖核查与修复交错，以及[测试规则][test-rules]和[性能规则][perf-rules]的真实接缝／工作量责任。但这些要求**没有规定每个家族都先建很大的标量矩阵，也没有要求把所有可读原版分支都做完才检查原样本的整合体验**。具体矩阵大小、23 个轮次的扩展次序和多轮等待由 Agent 选择，不能归因给规则或插件。openai-docs 技能只用于模型／工具核实，没有要求这些业务测试。

我的判断：需要保留已经定位的机制成果，同时承认投入重心偏向“模型和对照不断扩大”，原配置下完整选择→输入→首伤→可见红框链的验收跟进不足。不能用沉没成本继续证明所有扩展都值得，也不应把所有精度／前提修复一概称为低价值。

## 4. 实际 Agent、模型、并行和等待

实际 `turn_context.model/effort` 已按已知的六个子会话定向读取，全部为 **gpt-6.1-sol / high**；root 为 **gpt-6-astra / max**。不是仅依据请求参数。没有遍历机器全部会话。原始字段、会话标识、行号见 [agent-model-facts.json](agent-model-facts.json)，创建参数及回执见 [agent-creation-facts.json](agent-creation-facts.json)。root 身份与活动摘要另见 [agent-notes.md](agent-notes.md)。

以下区间是创建至最后交回状态的外包络，内部有等待，不声称持续占满模型：

| 角色／标识 | 区间（HKT） | 主要范围／文件／产出 | 暂停状态 |
|---|---|---|---|
| root `01a12515…` | 17:12–03:59 推进，之后仅交接 | Selection/Preview/Input、共享攻击时钟／接收、几何、ToolHooks、Git、串行固定构建及证据、文档整合 | 产品工作暂停；交接完成后停止 |
| weapon_models `01a1251a…` | 17:18–03:59 | Ammo/Models/Effects、Harpy/Bee/Scourge/Gem 等，配套 NativeCombat 叶；R24 两处未测编辑 | completed/frozen；无测试或写入进程 |
| npc_prediction `01a1251b-36d5…` | 17:18–03:59 | NpcMotion、NpcPredictionSource、DTO/Rolling 共享分派及多个运动 kernel；R24 南瓜王草稿 | completed/frozen；明确不可编译草稿未续写 |
| selection_preview `01a1251b-86d4…` | 17:18–17:56 | 选敌／预览共享状态和初批 NativeCombatSelectionPreview；随后交 root 继续整合 | 已完成原子任务；没有后台工作 |
| npc_ambient `01a1253e…` | 17:57–18:14 | AmbientFlight kernel／对应 oracle，独立文件集交回 | 已完成原子任务；没有后台工作 |
| integration_review `01a1254f…` | 18:15–18:33 | 只读早期整合审查；三项 P2，NEEDS_FIX | 已完成；本次未重启或新开审查 |
| npc_relations `01a12562…` | 18:36–03:59 | Plantera/关联 Boss/DD2/Tower/Wall 等 kernel 与测试；共享 Source 交 npc_prediction；R24 AI73 仅读计划 | completed/frozen；R24 无代码修改 |

开始时武器、NPC、选敌真正并行；同一个第三子槽随后用于 ambient、阶段独审、relations，受并发槽位和工作交还约束。root 保留 Git／公共构建输出的单写权，子线不独立构建覆盖产物。多个隔离 CPU 检查可并行，GPU／共享场景互斥；R23b 已运行 5 CPU＋1 GPU。构建冻结等待和共享 DTO 交接是实际串行点，不是永久单实施者规则。没有可靠记录可把这些等待拆成精确分钟。

同一共享文件通过交还写权整合；已记录的失败是 API／字段不一致和“新源码已变、测试仍读旧固定产物”，没有已确认的互相覆盖事件。当前 R24 与 R23b 不同是明确事实，测试不能挂到 WIP 头上。暂停时三条活跃子线均已交回状态；`gem-collision`、`gem-natural`、`tree` 用自身会话 Ctrl+C 正常取消，backend exit 1，非测试断言失败；确认无 `NativeWorldTextProbe.exe` 残留，没有碰游戏或无关进程。

并行规则已落在 [AGENTS][agents]、[工程治理规则][engineering] 和 [ADR-0019][adr19]：主调度 Astra/Max，子 Agent Sol/High；同文件单写，独立文件可并行，公共 Git／构建串行。ADR-0019 仍 Proposed／未合并，但当前任务按所有者明确裁决执行；ADR-0013 历史原文仍保留并有有限替代说明。ADR-0018 的分层冷启动候选没有擅自改为生效。没有做一次全仓旧措辞清除，本次也不继续改规则。

## 5. 代码与测试是否解决同一个问题

### 模块和实际职责

| 模块 | 解决的问题／仍有的边界 |
|---|---|
| CombatSelection + HostAttackCandidates + ResolveSelection | 基础距离排序、有限可达性求解、共享选中身份；清线的有限轮转不等于已验证全局瞬时最优 |
| PreviewPlan / ExpectedPreview / ImpactMarker | 从攻击许可中拆出观察需求；用接收时间和相同路径显示中心；旧实际消费 ExpectedImpact 仍存在，各自职责不能混用 |
| AttackAmmoSnapshot / Clock / PlayerFrame / Receive | 实际弹药与出生时机、前后 NPC 阶段、来源／实例／阶段及免疫依赖；多种射弹仍需各自运动模型 |
| FamilyLease / BirthSlots / ProjectileTargets / Obstacles | 重复候选复用有限派生接收历史，防同槽换代／资源／目标变化继续消费；这些共享机制不能代替真实已出生观察注册 |
| NpcMotion kernels + Source + DTO + Rolling | 把具体家族状态接到默认预测入口；保留未知随机、外部变化、无完整依赖时的截断／回退，不能用近似路径冒充完整原版未来 |
| NativeCombat* + Workload.Definitions | 扩充原版方法、自然多步状态和工作量断言；临时 build/run 脚本只调现有工具，写隔离输出。不是新增常驻遥测或新的长期调度平台 |

旧有限白名单漏项和部分错误运动路径被替换，但旧通用 fallback、条件拒绝以及不完整的已出生来源路线仍并存。`HostAttackAim.FlightPreview` 目前主要覆盖回旋镖／phaseblade；Control 对 guided/bubble/yoyo/whip/solar/pulse/harpoon/held 等有现成观察。Bee／Scourge／Cluster 等“有局部求解器”仍不能据此称整个真实因果来源已贯通。

扩大模拟范围确实发生了：从自身运动扩大到相关玩家生命、Boss 部件、背景受击者／免疫、塔灵魂阶段。这些来自实际依赖，但已增加很多状态和测试成本，尚无完整交付证明收益。修测试期待有明确合法例子（原版 immortal 人偶正伤害不等于 life 减少；薄墙不是必然负控；NPC buff 残留不是射弹命中），也留下明确未完成例子（R24 foreign Bee 产品允许、旧 `foreignUnsupported` 期待仍拒绝）。未对未知的新失败假定“都是夹具问题”。

### 执行身份、命令与最新结果

完整精选原始片段见 [evidence.md](evidence.md)，包含旧失败、旧 PASS 的适用限制及最新失败；每段有原路径、行号、完整原件 SHA-256、候选和进程结果。固定构建记录、DLL／probe 身份及 receipts 摘录见 [test-identities.json](test-identities.json)。源码 WIP 是审阅版本，**它没有执行过构建或测试**。

R23b：Debug / net472 / x86，SDK 10.0.203，HEAD `223c121` + dirty 源码；source fingerprint `B8AFF31C07932A38EBF6AEA4C71A4136DE4F7ED5F4216EC3E24BD9A84FA0E12A`；input fingerprint `5284882E49E712318240B76BEDCFA5FC74F46040F52558BCBD7657D557CE00A6`；build-record SHA `5CA3C9D194AF548E28D57BB1897690E42264BA091836CFF48CB0121D68D34D5F`。Probe 3737600 bytes，SHA `C936FEA33A6EA898AF8E1C291CB36464486C6F2004D2E84644F0EB6FC9B61A10`。暂停快照核实该轮四个产品 DLL 尚未变化。R24 三个产品文件在这次编译之后修改，因此当前源码与该指纹不同。

已有调度方式如下，只供理解历史执行，**暂停交接没有重跑**。脚本原文本见 [build-round](commands/build-round.ps1.txt) 和 [run-round](commands/run-round.ps1.txt)：

```powershell
& '.local/aim-comprehensive-repair-20261010/build-round.ps1' -Round r23b
$env:JMR_WEAPON_EFFECT_ORACLE_ONLY = 'gem-staff'
$env:JMR_GEM_ORACLE_ONLY = 'motor' # 另两个隔离进程分别为 collision / natural
& '.local/aim-comprehensive-repair-20261010/run-round.ps1' -Round r23b -Name gem-motor -Scope CombatEffectsCpu
# reflection / flame 使用对应 JMR_WEAPON_EFFECT_ORACLE_ONLY 值和同名 -Name
# tree 使用 -Name tree -Scope NpcTreeBoss
```

build 脚本调用 `scripts/build.ps1 -Configuration Debug -PrepareOnly -ApprovedRetainedAssets <已有保留资产清单>`，再准备现有 NativeWorldTextProbe。PrepareOnly 日志明确产品检查 `NOT_EXECUTED`。实际 runner 是 `artifacts/build/Debug/checks/bin/NativeWorldTextProbe/x86/Debug/net472/NativeWorldTextProbe.exe <repo> F:/Games/GamePlats/Steam/steamapps/common/Terraria/Content <隔离输出目录> <scope>`。Content 只读，使用测试隔离数据；没有启动真实游戏。receipt 的 `environmentKey` 不包含所有外层筛选变量，因此不能将空值解读为执行整个父组。

| R23b 项 | 进程结果 | 关键事实 |
|---|---|---|
| gem-motor | exit 0，85.34 秒 | 448 motors / 28270 actions / 224 retired / 596 bounces / 1344 settled |
| reflection | **exit 1**，41.26 秒 | 40 接收、4 派生租约、128 pre-NPC 边界前缀通过；随后 `damage-only reflection falsely survives an invulnerable guard into its later receive window` 失败；暂停后未查原因 |
| flame | **exit 1**，37.85 秒 | 16 births、40 motors、60 colliders、53/54 伤害尾部通过；随后 `flame marker uses the same target path and receive epoch` 失败；产品／夹具尚未分清 |
| gem-collision | **取消**，backend exit 1 | 日志到 784 collider PASS，但无正常最终回执，不登记整进程 PASS |
| gem-natural | **取消**，backend exit 1 | 仅 composition 记录，无专项结果 |
| tree | **取消**，backend exit 1 | 200 标量例、自然 scene 0 的 47080 匹配点／31165 长前缀／4652 跨阶段通过；其余七场景没完成 |
| Brain / Wall / Tower / Common / Bee / Harpy / Costs | **未启动** | 原计划不是执行证据 |

R22 的 12 项是 7 退出 0、5 退出 1；最新两个失败也不被旧反射 PASS 掩盖。R22 collider 的测试接缝错误已明确，不沿用它给新伤害框背书。R03/R04 的预览失败、R08 Control 的 Bubble 失败、R10 Mechanics 的闲置挥击失败，以及后续部分修后结果都保留在精选材料中。

人偶证据有实际 native dummy 和静态免疫恢复，也有普通敌怪组件与人为布置的静止接收者；R10 ammo 行使用实际灵液箭／普通子弹转换。**并不是每一项都使用原反馈“清线＋玩家中心＋追踪人偶＋目标标记”的完整配置**。一些 oracle 人工准备 projectile/state 或固定 RNG，证明运动或伤害接缝；正常 Update→输入→实际出生／接收测试另有独立条目；CPU Capture/Project 不冒充真实 Draw。未验证覆盖的部分没有改名为已验收。

早期独审原文在 [independent-review.txt](independent-review.txt)：结论 **NEEDS_FIX**，三项 P2 为方向缓存、静态免疫相对时间依赖、未来 `noTileCollide`。后续源码／测试覆盖了相关接缝，R08 有静态免疫自然恢复证据，但没有最终独审销项，不能写整体 PASS。该 reviewer 未参与实现或起草期待；本次暂停没有新开团队。

最终 Related 累计、统一最新候选、完整普通 Release 包、独立包复核、实机／FPS／多人／所有者验收均未完成。不存在本轮可复用全旧 PASS 的结论。源码和传播接缝持续变化，旧候选的身份／范围不能直接覆盖新候选；没有为本报告补一轮证据复用认证。

材料缺口也明确保留：没有本轮完整 Draw 截图／实机帧时间；没有精确分项工时；没有逐轮所有历史源文本快照（有原指纹和产物身份，不保证可从当前 WIP 重建每个旧候选）；R23b 取消叶没有正常最终回执；原配置下全部点名武器的统一操作矩阵没有完成。没有用汇总表填补这些证据。

## 6. 续办建议——仅建议，等待所有者决定

1. 保留选敌／预览权限拆分、真实弹药入口、悠悠球几何修复，以及有原版多步消费者证据的模型。不要因本轮未完成整体回退这些成果，也不要直接把 WIP 当包安装。
2. 若获准恢复，先处理明确编译阻塞与 R24 源码／测试分歧，再围绕原配置贯通少量代表性“选中→首次输入→原版首伤→同路径持续红框”场景，优先给图形和正常时序证据。反射、火焰最新失败应先定位所属接缝；Wall、Brain 则只执行已有针对性诊断，不先扩新族。
3. 把 NPC 具体 kernel、武器独立模型与 root 的观察／预览整合继续按文件并行；共享 DTO、构建、Git 串行。每次扩大模型前明确它补哪一个尚缺玩家结果或范围缺口。
4. 建议停止“没有新问题却重复已有标量全矩阵”的做法；同机制有可靠原版证据时保留必要变体／反例，把新增预算优先放真实默认入口、真实消费和连续可见性。若要缩减某条验证或延期原范围，由所有者明确裁决，本报告不执行。

当前未完成的原要求全部保留：全相关武器／弹药阶段、NPC 机制默认接入、预览持续可见、日耀实际卡顿体验、悠悠球原玩家场景。随机 Representative、未知阶段截断和输入条件是明确的实现边界，并不把已知正常场景无辅助／无框重新定义成范围外。

**交接完成后保持暂停。** 不自动恢复、不安排定时器、不轮询、不继续修报告揭示的问题。Issue #114 保持 OPEN，PR #115 保持 Draft；不合并、不发布。

[agents]: https://github.com/Kuroneko2020/JueMingR/blob/4850a290e7ec7f3d086de5e4efe48694adc4c821/AGENTS.md
[engineering]: https://github.com/Kuroneko2020/JueMingR/blob/4850a290e7ec7f3d086de5e4efe48694adc4c821/docs/规范/工程治理规则.md
[adr19]: https://github.com/Kuroneko2020/JueMingR/blob/4850a290e7ec7f3d086de5e4efe48694adc4c821/docs/决策/ADR-0019-受控并行协作与模型分工.md
[test-rules]: https://github.com/Kuroneko2020/JueMingR/blob/4850a290e7ec7f3d086de5e4efe48694adc4c821/docs/规范/测试与审计规则.md
[perf-rules]: https://github.com/Kuroneko2020/JueMingR/blob/4850a290e7ec7f3d086de5e4efe48694adc4c821/docs/规范/性能治理规则.md
[ammo]: https://github.com/Kuroneko2020/JueMingR/blob/4850a290e7ec7f3d086de5e4efe48694adc4c821/src/JueMingR.TerrariaHost/Combat/AttackAmmoSnapshot.cs
[models]: https://github.com/Kuroneko2020/JueMingR/blob/4850a290e7ec7f3d086de5e4efe48694adc4c821/src/JueMingR.TerrariaHost/Combat/HostAttackModels.cs
[effects]: https://github.com/Kuroneko2020/JueMingR/blob/4850a290e7ec7f3d086de5e4efe48694adc4c821/src/JueMingR.TerrariaHost/Combat/HostEffectAttack.cs
[selection]: https://github.com/Kuroneko2020/JueMingR/blob/4850a290e7ec7f3d086de5e4efe48694adc4c821/src/JueMingR.TerrariaHost/Combat/CombatSelection.cs
[resolve-selection]: https://github.com/Kuroneko2020/JueMingR/blob/4850a290e7ec7f3d086de5e4efe48694adc4c821/src/JueMingR.TerrariaHost/Combat/HostAttackAim.Selection.cs
[preview]: https://github.com/Kuroneko2020/JueMingR/blob/4850a290e7ec7f3d086de5e4efe48694adc4c821/src/JueMingR.TerrariaHost/Combat/HostAttackAim.Preview.cs
[control-preview]: https://github.com/Kuroneko2020/JueMingR/blob/4850a290e7ec7f3d086de5e4efe48694adc4c821/src/JueMingR.TerrariaHost/Combat/HostAttackControl.Preview.cs
[marker]: https://github.com/Kuroneko2020/JueMingR/blob/4850a290e7ec7f3d086de5e4efe48694adc4c821/src/JueMingR.TerrariaHost/Combat/CombatImpactMarker.cs
[selection-tests]: https://github.com/Kuroneko2020/JueMingR/blob/4850a290e7ec7f3d086de5e4efe48694adc4c821/tests/NativeWorldTextProbe/NativeCombatSelectionPreviewChecks.cs
[melee]: https://github.com/Kuroneko2020/JueMingR/blob/4850a290e7ec7f3d086de5e4efe48694adc4c821/src/JueMingR.TerrariaHost/Combat/HostMeleeAttack.cs
[aim]: https://github.com/Kuroneko2020/JueMingR/blob/4850a290e7ec7f3d086de5e4efe48694adc4c821/src/JueMingR.TerrariaHost/Combat/HostAttackAim.cs
[cost-tests]: https://github.com/Kuroneko2020/JueMingR/blob/4850a290e7ec7f3d086de5e4efe48694adc4c821/tests/NativeWorldTextProbe/NativeCombatAttackCostChecks.Natural.cs
[npc-motion]: https://github.com/Kuroneko2020/JueMingR/blob/4850a290e7ec7f3d086de5e4efe48694adc4c821/src/JueMingR.Features/Combat/NpcMotion.cs
[npc-source]: https://github.com/Kuroneko2020/JueMingR/blob/4850a290e7ec7f3d086de5e4efe48694adc4c821/src/JueMingR.TerrariaHost/Combat/NpcPredictionSource.cs
[rolling]: https://github.com/Kuroneko2020/JueMingR/blob/4850a290e7ec7f3d086de5e4efe48694adc4c821/src/JueMingR.Features/Combat/RollingNpcPrediction.cs
[yoyo]: https://github.com/Kuroneko2020/JueMingR/blob/4850a290e7ec7f3d086de5e4efe48694adc4c821/src/JueMingR.TerrariaHost/Combat/HostYoyoNavigation.cs
[yoyo-tests]: https://github.com/Kuroneko2020/JueMingR/blob/4850a290e7ec7f3d086de5e4efe48694adc4c821/tests/NativeWorldTextProbe/NativeCombatYoyoGapChecks.cs

# #114 / PR #115：去冗余与增强供给返修的集中审查入口

原件 `references/JMR_AIM_REPAIR_20261014/`，ID `JMR-AIM-REPAIR-20261014-v1`，14 原件 SHA 已核，保持本地只读；网页未直接取得这些原件。TASK_BASE `c38b1f941d64b73971a3380aff5d7c75fff24cb7`；对照原包来源 `1ae9965081cddffd2af41cf50c807f6144e6adfb`，1ae→TASK_BASE 的 src 差异为空。

f9691a2 的完整增量独审结论是 NEEDS_FIX；原件 `.local/aim-dedup-repair-20261014/independent-review-f9691a2.md` 保留。下述 F 为其成组返修与作者自查，等待独立窄复核；后面的 A/B 段保留原执行身份与当时未决状态，不能用旧 NEEDS_FIX 冒充新结果，也不能用新结果抹掉旧失败。当前尚无本轮普通完整包或安装，最近现装来源仅为1ae线索。长期资格 INCOMPLETE/releaseEligible=false，红框 OWNER_WITHDRAWN，owner/FPS/multi NOT_RUN；#114/#7 OPEN、#115 OPEN Draft。

## F：两个缺陷的因果与有限替代方案

**F1 的引入点为 a7f5705。** 唯一候选从旧的共同分支移出后，保留了 `CandidateProgressRequired`，却漏掉原 `CandidateFact` 的显著变化处理。自然 Slime 的 AI/受击阶段或显著位移使旧未知范围不再相关时，唯一目标仍沿旧 requested 范围继续扩张。恢复这项原有边界后，普通运动仍保持探索进度；没有削去单目标冷却预备或改变实际 Source 的有效性验证。原观察对照 `F-work-f969-r01.log → F-work-range-r01.log` 的自然矛 NPC 步12658→9127、selected world120请求98→72（两份日志各行40）；125来自另一原因窗 `F-f969-reason-r01.log:63`，不能混成同窗。该配对只证明观察窗实际工作减少，其时间不充作正常性能；正常成本另由下方固定 Release ABBA 证明。

**F2 同样源于 a7f5705 的新唯一候选分支，但不是把 F1 自动归成同因。** BeforeNpc 原来反复用旧 `actionHintRequired` 抬高 urgent demand，且稳定单目标绕过多候选在真实 admission 的提示退休。修订只在真实 urgent admission 移交一次；冷却回调不提前消费第一次有效提示，新 admitted warm 的 miss/暂停也替换旧提示。`F-hint-final-r03.log` 与 `F-xhigh-costs-r01.log` 的三个实际 native birth 都 Enhanced：首次旧 warm25移交为 ActionRequired25/Source26，后两次 miss/暂停后的机会为 ActionRequired0/Source17。旧 f969 负控 `F-hint-red-f969-r02.log` 保留按期待失败。

**R2 采用机制范围和求解内共享的有限等效方案。** AI019 按既有出生/飞行提示直接取得当前需要范围；普通 primary whip 一次请求完整原版动画并至少33步，包含原全部九个方向种子，避免同样本16→32→64重进。1035特殊派生鞭由原控制职责处理。最终普通 Release 的 `F-max-final-single-start-r01.log` 中，矛/普通鞭四场共14个实际获准的同样本手部求解入口全部为1；两次首帧 Source 空没有被记为一次求解。旧 `F-work-all-r01.log` 的普通鞭同样本入口2保留。

这没有建立通用跨调用武器 checkpoint，也不宣称任意武器/任意候选软暂停都可保存完整父子历史。方向相关姿态、Projectile/AI/localAI/oldPos、命中/免疫/派生独立；已有 owner-prefix 只共享同一 Solve 的方向无关完整动作值，部分动作异常不提交。使用更简单方案退出的依据是已证重复链、供给与整体成本，而非将“LIMITED”改名。后继真实新样本仍按当前前提重建，6ms总额/3ms候选累计/optional比例不变。

## F：固定普通 Release 的真实供给与正常成本

原测量 Host SHA-256 `BE12C7363FA0412986650FA73AD0F718289360BED253A459DF04ED86D9EF2343`，MVID `f7e2aad0-4792-4df3-ad51-8cd8418290a8`；原编译记录 `F-xhigh-release-build-record.json` 是 **f969 dirty / Release / PrepareOnly**，不改署未来提交。666项生产编译输入已与返修源码逐项核同。Probe SHA `61BE579AD1AC6BF55B70E0DB64C4FF94EF45AAFAB0FD0DE41963BF73A6E5FD07`；四轮起止/日志/脚本/载荷hash由 `F-xhigh-natural-ABBA-receipt-r01.json` 固定，原窗口为UTC06:31:19—06:32:13。无步骤级/原因 observer，原6ms，正式窗口无本任务重型并发。

每轮六场各360次真实 CPU 更新，预定前30冷段完整保留，暖330用于下表。A为原1ae（与TASK_BASE的src相同），B为该固定修订；下面是**两运行均值的平均**，不是配对差的分位数。实际范围为 Player.Update/ItemCheck/Shoot、NPC.UpdateNPC、Projectile.Update/Damage及Runtime/Shell；未运行 Main.Run/完整Draw，不报告实机FPS。

| 场景 | A暖均值ms | B暖均值ms | 变化 |
| --- | ---: | ---: | ---: |
| 枪95，静止控制 | 0.538739 | 0.517158 | -4.01% |
| 枪95，自然Slime | 0.715432 | 0.661840 | -7.49% |
| 矛1228，静止控制 | 0.504001 | 0.467912 | -7.16% |
| 矛1228，自然Slime | 0.851781 | 0.704960 | -17.24% |
| 普通鞭4672，静止控制 | 0.603075 | 0.562154 | -6.79% |
| 普通鞭4672，自然Slime | 1.673995 | 1.029940 | -38.47% |

F1的自然矛：A1/A2均值0.848234/0.855328，B1/B2为0.714068/0.695853；p95为A3.0935/3.0343→B2.4100/2.4325，p99为A3.7338/3.7014→B3.2529/3.2177，最大为A6.8097/6.5113→B5.9173/5.6870ms。每窗真实父体4、1E3B、伤害事件3保持，持续逆向在此有限正常窗已消失。其余场景并非所有尾部都改善：自然鞭p95/p99混合，静止枪最大值也混合；全部数值和冷段见[完整摘要](F-natural-time-summary.csv)，原始8640样本仍在本地日志，不挑最快一轮或删除尖峰。

[24场实际消费者](F-natural-consumer-summary.csv)和[96次真实父体输入/方向](F-natural-births.csv)给出完整分母。枪静止/Slime均3E1B、矛静止3E1B/Slime1E3B保持；鞭静止4E0B保持、自然Slime0E4B→1E3B，伤害事件1→2。E/B是父体输入类别，不能当命中率。静止控制与自然闭环分开；命中改变后续状态时，不把同GUC硬配为同输入。两A因旧自然鞭0Enhanced断言exit1，六场数据完整；两B exit0，不能说四进程全PASS。首次Source空与旧未知原因不回填。

## F：相邻、测试修订与包准入边界

- `F-seeds-model-r01.log`：普通优化Release的56行模型结果与原基线全等；另有普通鞭静止/运动、正常/加速四个正 Contact，将有限完整手部+全九种子前缀与121点参考逐项比较，Aim/Impact/Tick/Receive/Subupdate/Confidence全同。旧56行中无Contact的鞭/130行仍不当正命中证据。
- `F-seeds-default-costs-r01.log`：原默认预算/optional/普通工作责任、新去冗余/模式/hint、owner-prefix、模型与真实来源拒绝通过；相关探针修改仅改变其他独立选择叶的前置，旧运行身份不重签。`F-max-basic-r01.log` 当前Basic/原版动作、`F-max-whip-terrain-r01.log` 当前5480/1035未来owner/墙门通过。
- `F-max-candidate-full-r02.log`：固定普通Release的既有完整CandidateDemand.Run及五个180更新场景通过，含原BudgetPlanRetry、中断/完整miss/同样本恢复和实际多候选公平机会。原r01仅因未附带固定Harmony而在加载前失败，修正为已锁定五文件组件载体，不称完整包加载。
- `F-max-selection-r01.log` 原失败保留：固定60步内有49次实际远路径、最大gap3，却在任意最后一点要求farready。该断言来自abc0789，四更新显示租约与五更新optional cadence不保证这一任意相位；本次三候选路径未进入新的单候选修订。修订保留全部60观察，按已有有界 `RankedAdvance` 取得当前正证明后才做变化测试，不扩大产品预算、重置账本或移动性能窗口。`F-max-selection-r02.log` 两中心实际排序/路径、开闭墙、换弹药、移动与最近模式通过；原失败不改签。
- `F-max-feedback-plan-r01.log` 真实拒绝未分类公开材料；精确登记本任务既有Markdown/CSV/JSON名称后，`F-max-routing-r01.log` 的真实Git正反例与未登记可执行文件拒绝通过，r02 PlanOnly成功，仍是NOT_EVALUATED。阶段reader沿既有接口新增本任务schema/固定c38基底/精确增量和21项责任；旧阶段记录不能改署新候选。独立复核、当前阶段准入正反例、普通完整包/实际解ZIP消费者及安装尚待执行，未签绿。

最小[责任与证据映射](validation-map.json)及[失败处置](failure-dispositions.json)供独审逐项核适用性；尚无完整长期187资格。支持原版的完整包正常加载仍由所有者本人执行，自动组件和文件安装不能代证；第一张卡仅“能进入、F5能打开、正常退出”。

## 以下为A及f969阶段的原执行证据

## A 的源码职责与安全边界

- `CombatSelection` 清除无目标的旧可达事实。`HostAttackAim.Selection` 对唯一合法候选跳过身份排名，0/1/多候选、实例、物品与设置边界退休旧队列/提示；稳定单候选不清全容量。冷却工作仍在原 warm 以及合法可选周期内由同一目标工作保留，delay 不授当前输入；单目标 Path-only 不解闲置手部。
- `HostAttackAim.HandReuse` 只接受当前 BeforeNpc、零 delay、完成的普通手部正结果，拒绝 flight/controlled、纯/混合挥砍、phaseblade、Harpy、Book 原生连发阶段。Control/cadence/真实窗口先执行；不同实例、玩家、物品/弹药、动画/计时/起点、receive/免疫、环境/障碍/地形/phase/采样及完整共同前缀都重新验证。
- 候选只在同一同步 pass 只读借用；Source 对应 publication 尚拥有读集时冻结 NPC 地形（支持 Cache0/Cache1），选定计划尚拥有武器读集时冻结武器地形。执行保有 immutable Contact 和独立 leases；清候选/Reset 不替旧结果背书。EndPreparation 和实际消费退休借用；既有 BeginShot 一次许可保持，不增加每 tick 一发限制。
- `HostAttackAim.Preview` 完成复用时只在重新验证后的真实采样上更新 PreparedTick；没有改旧 trajectory 的 SampleTick/CaptureTick。新增持有者均为本功能有界状态，没有全局缓存/后台读活世界/真实 RNG 修改。

## A 的验证与反例

本机原件位于 `.local/aim-dedup-repair-20261014/`，详细日志网页不可直接读取；选定摘要公开如下。

| 责任 | 原件 / 实际结果 |
|---|---|
| 暖唯一候选工作量负控 | `base-dedup-red-r01.log` 旧载荷暖排名 solve=1，断言 exit1；新 `A-final-validation.log` solve=0，reachability 未伪造 |
| 同次手部重复负控 | `base-duplicate-red-final.log` 旧排名2、执行后总3、share0，exit1；新排名1、总1、share1，实际 AttackMotion.Advance 95→95，无第二遍步骤 |
| 依赖与一次性许可 | `A-final-validation.log`：真实弹药数、动画/冷却、玩家起点、目标实例、免疫和武器通路 cell 改变拒绝；借用后免疫/弹药变化退 Basic；候选 Clear 后独立执行结果保留，消费清结果/借用；恢复须重新取得真实 Source 前提 |
| 启停/生命周期 | `A-final-validation.log`：Aim/Path 四模式、Marker 保存、1→多→1→0；单目标闲置无手部排名/预览、无父需求不选敌，无目标不留 Contact |
| 多候选与距离 | `selection-r02.log`：固定冷11完整保留、另固定暖60；两中心、近受阻远可达、墙开闭、弹药耗尽/恢复、近者移动与最近模式通过；实际 DisplayPath 消费而非临时 Cache0 reader |
| 正常默认工作量接线 | `default-costs-r05.log`：可选预算/同GUC/外层/退出、真实预算中断 Basic、Path-off 及九普通场景，再实际调用去冗余叶；后继 A 最终新增身份门由 `A-final-validation.log` 补验 |
| Basic 与原版动作 | `basic-aim-r01.log`：枪/弓/叶绿镋、移动玩家、重力、首按/持续/再按/换物、纯挥砍及 DD2 真实消费者和 OFF/UI/焦点/目标/故障拒绝通过 |
| 纯逻辑与路由 | `architecture-combat-valid.log` --combat failures0；`routing-r02.log` 正常 Git diff/计划/失败路由通过，新叶正式消费者为 `native-CombatCosts`（其真实调用 CombatAttackCosts），专项 selector 不冒充新正式 ID |

计数窗带有限 observer，不能用其时间当正常性能。真实输入/出生统计使用 Player.Update→ItemCheck/Shoot→NPC.UpdateNPC→Projectile.Update/Damage→Runtime/Shell；不手调 Begin/End 代替真实出生。静止/固定横向速度目标共四场、每场360步和4父体；玩家站立。NPC AI 被隔离为固定速度轨迹，这是原版消费者/外部轨迹对照，不是自然 NPC AI 闭环。移动玩家由 Basic 专项另覆盖。

## 固定普通 Release 的有限时间/供给对照

实际 net472/x86 优化 Release Host 的 SHA/MVID、probe、起止和退出原件为 `A-final-time-ABBA-receipt.json` 与 `A-final-time-A1/B1/B2/A2.log`；A=原1ae，B=A修复。无步级 observer，无本任务并发构建/重测试，原6ms/3ms预算。每场360原始帧全保留，前30为单列冷段，暖330仅用于时间统计；输入统计始终保留全部4父体。

[16场时间摘要](A-time-summary.csv)保存逐运行平均/p95/p99/最大；[64个真实父体记录](A-birth-directions.csv)保留输入、方向、Sample/Capture/Receive。下表是两个运行的均值平均，不是配对差的分位数，也不是 FPS：

| Both 场景 | A 两运行均值平均 ms | B 两运行均值平均 ms | 全部父体 E/B 与伤害事件（每运行） |
|---|---:|---:|---|
| 枪95 静止 | 0.66746 | 0.57570 | 3E1B / 4父体，4事件 |
| 枪95 横向速度 | 0.99305 | 0.81479 | 1E3B / 4父体，1事件 |
| 叶绿镋1228 静止 | 0.58779 | 0.53848 | 3E1B / 4父体，8事件 |
| 叶绿镋1228 横向速度 | 0.77791 | 0.63540 | 3E1B/4E0B / 4父体，13事件 |

四场 p95 的逐运行范围均减少；尾部仍混合：移动枪 p99 4.052–4.124→4.137–4.885ms，静止叶绿镋某轮 max 6.089ms 高于A的3.524ms。原冷段/尖峰/暖 Basic 不删除。不宣称所有尾部更好或现场 FPS 恢复。

32个成对父体都具有当前 receipt，出生节奏/输入分母/伤害事件保持；A2移动叶绿镋最后一手Basic、B2为Enhanced，其余类别对应一致；移动叶绿镋最后一手方向分量差约0.087862，候选输入和接收时刻也不同，未签精度全等。1E3B 的移动枪缺口仍存在，Basic 原因未采、为 unknown；这些不算命中率。B/最终动态质量必须继续复核，A 不据此宣布 R3 完成。

实际步骤计数另窗：原 `base-work-steps-final.log` 枪静止8027、移动44901次 AttackMotion.Advance，叶绿镋静止1280、移动4833次 private spear offset；A 有界去冗余窗为7790/30603及812/3178。观察计数与正常 Release 时间各自保留载体身份，不混署。

## 失败、身份与下一步

全部早期原件保留。测试修正包括：缺少初始化的成就末端出口（旧1ae `base-optional-failure-r01.log` 同样失败）；旧单候选排名预算注入改为两个真实候选，原中断/Basic/累计额度断言保持；临时 Cache0 在 BeforeNpc 释放，显示断言改核实际 DisplayPath；恢复 live fields 不等于恢复 Source generation，负控后重新取得真实 Source；新叶实际正常 dispatcher 是 CombatCosts，原两ID期待被执行路由拒绝后修正。一次 stdout 解析错把 sample tick 当方向分量，已按命名列修正，CSV保留实际原数。`final-time-*` 启动前缺 probe 的工具错误也保留，不算时间运行。

A 最终检测/Release 编译均为 PREPARED + 指定专项，完整资格 NOT_EVALUATED；它们的编译元数据仍署执行时 c38 dirty，不冒称在随后文档/整合提交重新实跑。完整包、实际ZIP、最终独审、安装和所有者体验尚未执行。C 四文档已完成作者自查，ADR-0019 保持 Proposed、ADR-0018/#117 未接受，随本轮完整增量交独审。M1 后由主调度集中核A，再续B；当前不继续写 B。

A固定Release Host SHA-256 `95EC0F01AA5B4C057B0A267FB57B421B23CA988988065E0CD31648E2E9B51795`；sourceFP `CA34A4D86BE7481679CB9D064EEBAF69EE1FA27DFC84FC839D4941A93FF8637F`、inputFP `2C72564DDA2616184C23840E1DF3D564FE72C7112457BA646A530501482798AD`。原件 `A-delivered-identity.json`；本入口提交与原编译元数据分开。

## 执行入口与候选定位

源码候选为本入口所在 Git 提交；完整 SHA 在 #114/PR #115 的 M1 评论及本地 STATE 登记。不能把下面隔离测量的 c38 dirty 编译元数据改签为此提交。

实际专项均沿 `scripts/test-world-object-text.ps1`，使用匹配原版 Content、各自新 OutputDirectory、`-ApprovedRetainedAssets .local/aim-priority-recovery-20261013/retained-assets.json`；原件输出目录与起止由各日志保留：

- `-Scope CombatAttackCosts` + `JMR_ATTACK_COSTS_ONLY=dedup`：最终去冗余叶；不设 ONLY 为原默认工作量 + 新叶。
- `-Scope CombatIntegrationCpu` + `JMR_COMBAT_INTEGRATION_ONLY=selection`：选敌；`usage` + `JMR_USAGE_ORIGINAL_FEEDBACK=basic-aim`：Basic/消费者。
- `scripts/build.ps1 -Configuration Release -PrepareOnly`：普通 Release 编译和结构；不是正式完整资格。
- `tests/Workload/Invoke-WorkloadRoutingChecks.ps1`：路由；ArchitectureTests.exe `--combat`：单一参数的战斗纯逻辑。
- 时间窗调用已准备的 NativeWorldTextProbe.exe（repo、Content、新输出目录、`CombatAttackCosts`），设置 `JMR_ATTACK_COSTS_ONLY=dedup`、`JMR_DEDUP_TIMING=1`、`JMR_DEDUP_BASELINE=1` 跳过组件强制预算；以 `JUEMINGR_COMBAT_GEOMETRY_CANDIDATE` 指向 A 或 B 的固定 Host。四轮 Host/probe 哈希相同或对应固定版本见 receipt；不重新构建间隔中的载体。

参数中的 Content 是 `F:/Games/GamePlats/Steam/steamapps/common/Terraria/Content`，仅作为隔离原版资源读取。原始日志含完整命令输出与失败；本表不补签正式缓存。
## M1 修订与 B 的实际方案

旧 PreviewPlan 在 Rebind/覆盖 Timeline 与 PreparedTick **之前**校验旧 RelationVersion、GravitySources、WallSources，避免旧 Contact 的未来玩家前提先被抹掉再获 hand donation。几何完全相同、未来一次真实重力动作使玩家方向1→-1的负控，已通过真实 PreparePlan 强制新 weapon solve；不只改 key。TryCandidatePresentationPrefix 的注释明确只证明纯计算等价，hand/presentation 各自负责许可。实际生产生成的正 delayed/controlled/原版出生 flight 预览，均经 TryCandidateHand 拒绝并退休借用；改变真实 phase/sample/capture 的调用也拒绝。

B 定位为范围提示与求解内共享：当前 AI019 派生手部依据同一原版私有 offset、严格整数出生时刻、228/131的逐步衰减及目标速度给工作范围提示；不签可达性、不剪方向/120希望范围。冷却候选沿原有渐进范围。宽提示最初用于冷却，移动矛耗时逆向且无供给增益，已撤该接线，原数据留在 `B-final-natural-time-*`。`HostAttackPlayerPrefix` 只在一次 SolvePrepared 内拥有完整 PlayerMotion 值，方向/RawCursor、私有弹体、姿态、免疫/其他接收与派生历史仍分开。方向/父子体读取同一玩家未来，不再各自推进相同动作。AI019缓存容量只覆盖实际动画内的owner动作，脱离玩家的子体仍不依赖无关后段玩家；其他机制仍以当前有限prefix为上界。

该方案没有跨真实更新武器状态缓存；不同方向/seed仍按自身真实输入从出生起点求解，NPC同样本扩展沿已有实现。完整动作才提交owner值，硬退出的部分scratch不提交；测试第4动作中断后只补7动作取得第10动作。没有新增全武器checkpoint平台，也没有声称所有武器前段均已消除。预算、原版窗口、Basic和红框撤回保持。

## B 原件、真正失效和未执行

- `B-bounded-default-costs-r03.log`：现有默认成本责任 + 去冗余/模式/前提/ownerprefix + 56模型行 + 实际源拒绝，专项PASS；正式累计资格NOT_EVALUATED。后继仅测试观察器OFF空receipt的修正由 `B-final-sources-r03.log` 与模式窗覆盖。
- `B-bounded-release-model-r03.log`：最终容量载体普通优化Release/net472/x86，枪、4种派生AI019、普通鞭、RawCursor，静止/运动以及17/33/65/121点，共56行。输入/Contact/tick/receive/subupdate/置信度对照原1ae；最终结果见 [模型CSV](B-model-equivalence.csv)。Debug对Release曾有4行RawCursor impact约0.0001差异，优化Release对照已排除该载体差异，不把Debug行强签全等。
- `B-fixed-oracle-r01.log`：旧移动枪三次暖Basic取得121点后仍无解；目标沿竖直固定轨迹穿到y=688地板下，原版LOS=False，独立完整前缀参考也无Contact。这是该夹具的实际不可达，不是该三次都预算饥饿；旧A窗原因仍unknown，不将后测原因逐事件回贴旧窗。
- `B-natural-oracle-r01.log` / `B-moving-player-reason-r02.log`：普通Slime保留原版AI/默认受击框、非零飞行提前量，实际Player/ItemCheck/Shoot/NPC/Projectile/Damage链。移动玩家由真实Right trigger在采样后交原版Update，最终X700→800.8；第一版仅改controlRight未真正移动，已留为失败的证据设计，不签移动覆盖。暖远端矛/鞭Basic的同机会完整参考无Contact；首次Source为空仍unknown。参考的1000ms只用于分离oracle，不是产品供给/性能。
- `B-basic-r01.log`、`B-architecture-combat-r01.log`、`B-routing-process-r02.log`通过；routing首个包装命令的LASTEXITCODE=2是有意负控Git的残留，真实独立pwsh进程exit0，保留两原件。移动玩家和新源拒绝均未触及真实存档/游戏启动。
- `B-spears-r01.log` 和 `base-spears-r01.log`：原版矛/鞭预览旧专项在两载体均失败（完整窗口preview、sealed preview及whip other-hit等）。现有 PreviewDemand 产品入口为OWNER_WITHDRAWN，测试仍期待该已撤显示能力；未恢复红框、未改期待签绿。它们不能证明本轮通过完整资格，也未作为仅凭失败名改生产的理由；实际输入/原版动作由本轮消费者与Basic责任另证。

## 固定 B 的普通 Release 消费与有限时间

最终普通Release Host SHA `66CAD2D933AF93A08FF18FB83E96822140C6697664F089CCD1AE99E88B3AF6B1`；实际编译身份 `B-bounded-optimized-build-record.json`，仍a7 dirty，普通PrepareOnly；后续Git/文档提交与原编译元数据分开。B窗口及启动/结束/退出、实际probe哈希查 `B-bounded-natural-time-ABBA-receipt.json`。A=原1ae（与TASK_BASE生产等价），B=此修订，不是A/B两个代码工作包的命名。

[24场时间摘要](B-natural-time-summary.csv)和[96个真实父体](B-natural-births.csv)保留每运行/每场完整4父体、当前receipt、方向与Sample/Capture/Receive。每場360真实步骤，预定前30冷段原始仍留，暖330用于时间；静止控制冻结NPC AI，运动Slime为自然原版闭环；玩家在这组静止，移动玩家另窗。闭环伤害/buff/免疫可改变后续状态，不把版本分歧后同GUC硬配为同输入；父体E/B不是命中率。

基线自然鞭0E4B使原assert退出1，但6场各360帧已完整记录；候选退出0。原失败保留，不改阈值，不能说四进程全部PASS。供给中枪自然Slime3E1B保持、矛1E3B保持、鞭0E4B→1E3B；鞭损伤事件1→2，但不是全游戏命中改善证明。方向/晚期事件以CSV原数为准，不能替代同输入模型oracle。

计数/原因窗与正常时间分开。`B-reason-r02.log`：静止源108.9ms、移动源134.4ms self（整个360窗）；移动矛PlayerMotion.Advance6399次/47.8ms。早期共享窗 `B-shared-owner-r01.log` 同条件1609次/14.6ms、SolvePrepared inclusive56.1→22.8ms；nested inclusive不相加，带观察器不宣称普通性能。源仍是主要剩余成本。最终计数另列真实weapon step和范围hint的原版offset调用，未把hint成本藏在求解入口之外。

当前性能 **LIMITED / NEEDS_FIX**：六场有限总体有改善、动态鞭的可解机会供给改善；静止矛/鞭变化小且尾部混合，自然Slime矛的均值稳定逆向，不能宣称R4整体达标。一次较早ABBA有较长中间空档/其间路由任务，采用最后连续固定窗；原件都保留。后续集中独审核该成本、源码状态与供给边界，必要继续收窄或返修；当前不得据此安装为“已修复”。普通完整包/实际ZIP/完整加载、整批独审和实机/FPS/多人均未完成。
最终连续窗口两个运行的均值平均（ms，非配对差分位）：枪静止0.64809→0.52241、自然Slime0.79626→0.75051；矛静止0.53371→0.54151、自然Slime0.93437→1.07359；鞭静止0.63376→0.66543、自然Slime1.71932→1.15986。六场合并解释只作代表组合，不掩盖矛的持续逆向；不称全武器性能通过。普通Main/Draw/实机FPS均未测。

补充完整正常时间窗：`B-mode-time-receipt.json`（Off/AimOnly/PathOnly各A/B一轮，全部exit0）、`B-bounded-late-time-ABBA-receipt.json`（晚派生接收，静止/外部移动）、`B-final-moving-player-receipt.json`（原版Right trigger的真实移动玩家/自然Slime，A/B一轮）。它们是有限补充，没有虚构稳定p99/全拓扑质量；原始全部帧、冷段、失败和Unassisted分母保留。模式关闭的原版出生属于Unassisted，不与Basic混分母。

[44行补充结果](B-supplement-summary.csv)公开模式、晚派生、移动玩家的实际父体分母/Unassisted/伤害事件；原始退出及时间边界仍以各receipt为准。两种非Both模式不机械要求Enhanced；移动玩家旧基线仍保留exit1，候选exit0，不能将失败进程改称PASS。

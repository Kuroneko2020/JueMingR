# #114 / PR #115：重复求解返修的 A 工作包

原件 `references/JMR_AIM_REPAIR_20261014/`，ID `JMR-AIM-REPAIR-20261014-v1`，14 原件 SHA 已核，保持本地只读；网页未直接取得这些原件。TASK_BASE `c38b1f941d64b73971a3380aff5d7c75fff24cb7`；对照原包来源 `1ae9965081cddffd2af41cf50c807f6144e6adfb`，1ae→TASK_BASE 的 src 差异为空。

本入口对应本轮 A 与同时完成的 C；B 尚未实施。A 已实施和自查，交 M1 集中判断；这不是最终整批独审、完整资格、普通包或安装批准。#114/#7 OPEN、#115 OPEN Draft，红框 OWNER_WITHDRAWN；owner/FPS/multi NOT_RUN，本轮未操作现装，最近来源线索仍为 1ae。

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
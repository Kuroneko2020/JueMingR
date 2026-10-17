# 当前需求、清线稳定性与持续成本返修

任务 `JMR-AIM-TARGET-PERF-20261014-v1`，#114 / Draft PR #115。TASK_BASE 为 `72970569c11dd09e3e210894c25cc06e4cfc1a7f`；旧直接对照为实际 e262 完整包。主调度实际 Astra/Max；实施、测量与独审实际 Sol/High，未改变全局配置。固定候选 `9f0e7569fac23be89378c537a0baac90be9b9322` 已完成集中独审、真实准入正反控及04一次包／安装窗口；后继纯文档提交不改变此包来源。

## A：历史探索与当前攻击需求

引入点为 `7576da5` 的候选进展接线：`candidateDepths` 记录同身份曾生产的最大长度，选中后却直接进入 `SetActionDemand`。旧路径把过去更长探索当作当前必须再准备的范围；原断言只验证短需求和至少一次增强，没有同一多候选身份长→短→长反例。

修订保留探索队列，只将本次成功 Plan 的完整 `Timeline.Count - 1` 与当前机制需要交给 ActionRequired。接触时刻没有代替完整求解前缀；Hand 的 phase、身份、完整前缀、Gravity/Wall、Source owner 与独立地形租约验证未删改。

新测试用至少两个实际可受击 NPC、相同 NPC/held-item/选项，240更新内真实位置长→短→长，经过原版 Player/NPC/Projectile 更新和实际 RecordInput；不切武器、不重建目标、不清队列。先有真实长距离完成Plan，然后frame80移近、160移远。旧实际 e262 定向断言红，新普通优化 Release 绿。精选原始行见 [A-native-uses.csv](A-native-uses.csv)。Path OFF 历史33；frame80本次请求26、PlanCount27/ReceiveTick8，旧 ActionRequired/selected准备33，新26，selected新增NPC步7→0；frame120完整19点/Receive10，需求33→18，新增步15→0，实际 Enhanced 与手部借用保留。Path ON 同样回落；距离变长后真实范围重新增长，没有缩短正常长尾或长机制种子。

最终原件：`.local/aim-target-perf-repair-20261014/A240-red-r01.log`、`A240-green-r02.log`；对应 `.local/ir-38f23aa6/current-demand/native-uses.csv` 与 `.local/ir-8e6410c8/current-demand/native-uses.csv`。前期180窗和较早少原版重用机会的夹具失败均保留。强化测试曾额外要求“长距离先实际Enhanced”，但候选frame40已完成34点/Receive24的长Plan，实际仍Basic；该旧失败保留，精确消费拒绝原因unknown。按任务允许先有较长预测／探索，测试改为先证明真实长Plan完成，短距离仍须实际Enhanced、完整前缀／需求／真实步数回落及再次变长。没有把早期Basic改签为Enhanced或性能通过。

## B：新定位的重复探查与旧事件边界

旧 receipt 的20个 moving24原版机会及其来源哈希已核。B2 frame80/120确为 target2/Basic，另外三窗为target26/Enhanced，见 [old-B-native-events.csv](old-B-native-events.csv)。它们不是已选26仅失去提前量；外层frames.selected、farActionAttempt旧值、被清掉openingContact的sourceCount0均不能定位首因。**这两次旧事件的首失败仍unknown。**

新有限原因窗在当前真实调用边界记录 admission、Source publication、Plan完成/中断与最终输入，仅测试安装Harmony并末尾输出。旧 e262 新窗GUC124先完成far4正解（0.935ms，35点/Receive26），已核近2/3短miss，随后又对近2追加32步公平probe，Plan预算退出4.3108ms；最终实际far4/Basic，scope6.7221ms。它证实“已有正解后可选重复工作挤掉本次增强”的独立缺陷；**不是把旧B2的target2事件倒写为同一首因。** 原始关键行见 [B-key-events.csv](B-key-events.csv)。

同样引入点 `7576da5` 把BeforeNpc近期正解优先和公平probe接在同一pass。原测试只检查最终至少一次Enhanced和队列最终进展，未核当前正解后再次进入同一近者。修订只在BeforeNpc已有新正解并核过更近前缀后停止追加公平probe，World公平探索与新的更近事实重新排名保留。短miss仍是其范围内无解，不缓存为整武器不可达；不扩3/6ms预算、不保旧输入、不锁远者。

旧同输入完整五场原因窗 `B-reason-baseline-r02.log` 为 repeatedNearerAfterPositive=5，定向断言红；新 `B-reason-green-r01.log` 为0，五场真实消费、近者变化后的恢复与此前Source owner反控通过。诊断反射会影响时间，因此这些毫秒只解释该新事件，不作为正式性能比较。正常无详细钩子的Release压力与完整Main结果另列；没有将 `stressEnhanced>0` 当整段质量结论。

## C：计量载体与边界

普通优化 Release/net472/x86，组件 build-record 为 `.local/aim-target-perf-repair-20261014/component-build-r02.json`，编译来源72970569 dirty。固定Host SHA256 `B1F85D8A746A0389EA46A76A522B8075B245469A78985A032D497843AB224408`，MVID `82300129-611f-4a58-98b4-4a2baea98be7`；不改署为后继提交或clean包。全部666份当前生产编译输入仍与该组件逐字节一致，见本地 `production-proof-r02.json`；工具／测试／文档改变没有被改签为这份PE的来源。

正式载体走原版 constructor/Initialize/LoadContent/Main.Run/Update/Draw/EndDraw、真实Bootstrap及完整Runtime交接。Intel i7-11800H / Windows 11，原版Terraria1.4.5.8，1920×1080、FrameSkip Off、Immediate；独立合成房间及保存根、SocialMode.None。场景为普通95/真实静态Dummy60，普通95/较远自然Slime加原版左右移动，1228/真实Dummy派生接收；设置原版AutoReuseAllItems且各版本／模式一致，不改useTime、动画、弹药、物理或RNG。Slime生命设100000以保留全窗，其原生AI、受击与击退照常。闭环伤害改变后续状态，不把之后同GUC当固定同输入oracle。

34个有效Main窗全部exit0：候选3场景×Off／Aim／Path无攻击／Path手动／Aim+Path×两轮反向交错=30，e262关键动态Both及派生Both前后各一=4。每窗900世界更新，120静置、660连续原版攻击、120释放；正式窗无本任务构建／重型测试／详细原因钩子并发，不关闭所有者后台程序。正常压力另作A1/B1/B2/A2，只调原RunStress，共3600更新／100真实机会，四进程exit0。

完整分布、样本n及慢段见 [C-main-key-time.csv](C-main-key-time.csv)；首世界／首发及较晚慢事件保留于 [C-main-slow-events.csv](C-main-slow-events.csv)。以下是attack窗口两轮均值范围，毫秒；Main推进、combat与Draw分别计量，不嵌套相加。

| 场景／模式 | 完整 advancing Main.Update | combat scope | Draw |
| --- | --- | --- | --- |
| 普通静态 Off / Aim / Both | 0.954–0.959 / 1.058–1.092 / 1.236–1.242 | 0.001 / 0.145–0.152 / 0.314–0.315 | 1.397–1.400 / 1.403–1.413 / 1.472–1.504 |
| 普通动态 Off / Aim / Both | 0.940–0.957 / 1.267–1.275 / 1.774–1.810 | 0.001 / 0.326–0.327 / 0.800–0.816 | 1.677–1.766 / 1.706–1.728 / 1.892 |
| 派生1228 Off / Aim / Both | 0.880–0.899 / 1.047–1.072 / 1.203–1.266 | 0.001 / 0.142–0.148 / 0.301–0.318 | 1.337–1.447 / 1.347–1.388 / 1.454–1.596 |

Path无攻击／手动的全部数字同在CSV。动态Both相对自身Off额外Main推进约0.82–0.87ms，Aim约0.31–0.34ms；同present周期实际累计2.886/2.932ms、p99 7.025/7.180，advancing Update p99 5.378/5.483。e262动态Both1.783/1.797ms与候选1.774/1.810ms，派生对照也混合；**不宣称普遍提速**，实用判断来自当前Off/On成本与同时保留的功能。

首次与尾部未删：普通world0 Update62.7–74.6ms、首次真正推进32.9–44.4ms、首发常25–30ms；attack combat最大12.139ms。不能统一归因JIT、GC或电脑卡。world180–779的18000个候选推进只有1次>8ms（派生Path无攻击step735／11.636ms，原因unknown），未见持续慢段。旧字段 `sameFrameCpuMs` 实际为同present周期scope累计流逝，全部毫秒均非线程独占CPU或所有者FPS。

真实分母见 [C-main-quality-summary.csv](C-main-quality-summary.csv) 与 [64条原生关键事件](C-key-native-events.csv)。12个候选开Aim窗452父体=433Enhanced+19Basic，0错误目标、0丢发／推迟；全部Basic在本窗首E之前，首E后0次再退Basic。普通95每窗42父体，间隔16GUC；1228每窗29父体，间隔23GUC，均与各自Off／Path手动一致。自然动态每窗42=41E1B、36原版接收，SampleAge1、ReceiveTick中位45，和e262质量同数。1228每窗29=27E2B，另29派生／73派生伤害，分母不混。19个Basic的精确原因仍unknown，不用B诊断倒填。

全部Path无攻击窗0出生／0消费；Path手动全部Unassisted，未由Path接管攻击。正常最长121点保留，动态Both约95.6%有效Draw有路径；不把逐帧121点当硬门。原版接收事件可能多于父体，E/B比例和伤害事件数均不是逐发命中率；固定原始鼠标的Off结果不代表玩家手动瞄准能力。

压力的 [完整scope分布](C-pressure-time-summary.csv)、[质量摘要](C-pressure-quality-summary.csv) 和 [全部100机会](C-pressure-native-events.csv) 保留：候选50机会41E9B，e26250机会40E10B；warm30+错误目标0。moving24 frame80/120均target26/Enhanced，frame140近者变可达后下一合法frame160均target2/Enhanced。旧B2错误目标未在本四窗重现，旧首因仍unknown。2候选/Path ON首E仍可能到80，压力并非100%E。候选压力scope均值依场0.686–1.928ms，实际出手帧均值3.09–6.17ms、最大12.887ms；n=5出手帧p99仅等同最大，不当可靠尾部分布，也不把6ms预算当速度合格证。

本次当前能力下的持续质量和整体成本支持有限范围实用判断；没有新证据要求继续盲调阈值或削减预测。完整原始Main、出生／伤害／世界／Draw文件在 `.local/aim-target-perf-repair-20261014/measurement/formal-*/out/`，压力在 `measurement/pressure-*/user/candidate-progress/`；公开保留必要scope与关键事件，未上传游戏程序集或用户数据。

载体一次profile接线失败原件保留：[C-excluded-run-status.csv](C-excluded-run-status.csv)。local full-payload-r02的packageId漏了 `continuous-processing-`，Host按profile未装配CombatObservation。full-payload-r03仅纠正该manifest前缀、DLL同字节；已完成两个A1不重跑，失败的新候选进程不冒充完成测量。该错误不属于生产算法。首次A1结束后外层收集脚本错误读取未设置LASTEXITCODE，只修收集并复用同一次运行；没有重跑挑样。早期静态Slime夹具不构成正式结果，正式改用原版Dummy。

## 工程验证与剩余项

当前普通Release：完整candidate-demand（含A）、默认attack-costs（含Source owner/退休、完整前缀、delayed/controlled/flight拒借与输入反控）、basic-direct、whip-terrain、navigation均exit0。原始 `check-*-r01.log/.exitcode` 保留本地。

额外selection-preview的固定3idle后单一冷帧display非空断言，旧e262与新候选同处失败；仅增加末尾诊断、保持原3idle的后续r07四组合都通过。原失败保留。现行显示是5更新optional生产/有效lease，预算可能在射击更新撤lease；测试改为固定两周期核所有可见path的owner并要求真实可见结果，避免把null冷帧当单帧显示合同。最终新旧载荷均四组合exit0（`selection-final-r01.log` / `selection-final-baseline-r01.log`）；生产显示没有改动。

实际Git差异路由、共享消费者／排除／索引工作树／删除改名／缺失基线／执行计划检查exit0，见 `workload-routing-r01.log`；本轮仅在原阶段reader登记真实新基底／修订白名单／11项责任，保留未知可执行内容拒绝。独立审查后以真实报告生成准入记录，实际reader正例与16项指定反控全部通过，原记录未变。不运行全187、全NPC×武器或旧worker全矩阵。完整长期资格INCOMPLETE / releaseEligible=false。源码、工程测量、完整包、文件安装、所有者真实加载/体验/FPS/多人、合并分别报告。

## 固定候选审查与一次文件交付

独立Sol/High已审TASK_BASE→9f0e756完整24文件增量、必要相邻调用与A/B/C原始证据，结论APPROVED_FOR_STAGE；另独立重算34个Main窗与压力分母。同一独审对真实reader执行、准入记录和16反控作一次机械窄复核PASS。原文分别保留于本地 `independent-review-9f0e756-r01.md`、`independent-review-stage-guards-r01.md`；主Astra集中判断认可限定范围内的成本／质量。stage记录SHA256 `824C51C6703FC2B7AC99C7450AC06B891891A3A5F7F0B80E96EB8455C2836D9B`，来源9f、sourceFP `5D9625E3C7E133EDAF17C0CF6D683D5B034A9E139D8B381574CEF009AC8E12A0`、inputFP `49DB19F83B7C2F62B86F4654E685AA6A5D8454910AB79975FE125E938099AD22`。未签全资格／发布／所有者体验。

普通优化Release/net472/x86 ContinuousProcessing完整包只构建一次：`outputs/aim-target-perf-delivery-20261014-9f0e756/JueMingR-Continuous-Processing-9f0e7569fac23be89378c537a0baac90be9b9322.zip`，**2,167,651 bytes**，SHA256 **8B7A6C47160241E97F9BE357F692B7E739819AFDDA2B0874588E775835CA8EF7**。一次现有verify入口核17成员／10payload及manifest／source／Release平台；构建回执SHA256 `E199FB4360C4936AE06250981E184CBF6AA8F96482843EC155D422BA14344DE3`。最终包Host SHA256 `6D66EBE10D55A625467FEA705A0D4AFFA1DA8A74C9A963D4CDB3EEF7A098469E`、MVID `38938423-08ce-409f-8dee-9ce0e3aca07d`，与C原组件PE身份分列，不将C改签为最终包。全部666生产输入仍与C核合。

一次代表A240消费者使用构建器同字节包目录，通过Path OFF/ON各6真实机会、2次短Plan，hand借用4／6；exit0，原件 `package-representative-r01.log` 与 `.local/ir-692758ce/current-demand/`。它是同字节代表消费，未冒称另一次实际ZIP解包全套业务／完整Main／FPS验证。

既有授权目标 `F:\Games\GamePlats\Steam\steamapps\common\Terraria` 新核所有权、无游戏／worker占用后，实际11旧R受控文件备份（本次无旧evidence，不套历史12数量）。官方旧Restore→新Install各一次退出0，分别RESTORE_COMPLETE／INSTALL_COMPLETE。一次读回新10payload与安装manifest一致、原版exe未变、11备份仍匹配。完整本地回执 `.local/aim-target-perf-repair-20261014/install-9f0e756-20261014-225631/installation-receipt.json`，备份同目录 `backup/controlled/`，旧完整包与官方恢复材料路径在preflight及回执的recovery字段；未实际回装旧版验证。不扫描用户／Steam／云目录，不加第二安装审计。

[简短机器交付回执](delivery-receipt.json)记录包、目标和边界。红框OWNER_WITHDRAWN；所有者游戏加载／进入／F5／正常退出、现场FPS与多人NOT_RUN，不启动用户游戏、不写真实角色/世界、不合并/关闭/Tag/Release。外来四项保持原字节并排除Git提交。#7/#114保持OPEN、PR115保持Draft；本轮安装成功不能代替所有者体验。

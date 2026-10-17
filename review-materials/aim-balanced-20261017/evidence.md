# 提前量与平衡瞄准候选

原完整独审与 R1 增量独审合并为 **PASS（05范围）**，root 已准入07；源码 `a283747` 的一次完整普通 Release 包、受控备份、正常换装与读回已完成，现可由所有者实测。首候选 `d5cfe16` 的 **NEEDS_FIX** 与全部失败保留。本轮继续 #114 / Draft PR #115，固定基底 `112e9d6`、原普通 Release/Main 受测生产 `7a8f18a`，默认代表主弹与必要少量补查；完整 Contact 不再是一般预瞄前置。

独审抓到 `EvaluateCandidate` 在本次 Source 返回 null／不足两点、没有执行 PreparePlan 时仍读旧计划的 Reachable/Contact，可能把未知近者当成当前正解并关闭重试。此前模式保护只有有效未来，没有覆盖“旧正→本次无未来”的交接。原生产 `candidate-recovery-r16-before` 已真实复现：Source 自身读取故障返回 null，以及 AI18 必要格缺失产生一点真实 publication 后被 Cache 拒读，均错误返回正解；平衡路径还把 Unknown ticket 退休。旧独审与失败身份保留。

B 只在本次无有效前缀或预算中断时清旧计划，保留 FaultGate、Unknown 和 changed ticket，不触独立 NPC 显示/缓存；有效前缀复用不变。`candidate-recovery-r19-fixture` 三组平衡／旧 Contact／真实一点 publication 均撤旧正、排队重试，恢复输入后均重新得到实际正解；两次真实排名都选当次可达远者。中间 r17/r18 的排名组件漏掉调用者 Selection.Update，远者也只在缺格前被证明，原失败保留；补真实调用顺序并在缺失仍在时先证远者可达后通过。`modes-r19-after` 16组、architecture combat及新测试入口路由通过。该组件没有注入轨迹、角度、Contact或Source返回，不是Main成本测量。

后继生产只有这一清线候选交接变化。原八个普通 Main 窗口实际均设置 ClearLine=false，ResolveSelection 在该处之前早退；代表弹、出生/真实消费、NPC motor、悠悠球及绘制均未修改，因此原 Main 和相关专项按真实身份有效复用，没有重跑或改署新候选成绩。R1后继及独审已完成的身份与复核结果另列[验证索引](validation-map.json)。

[机制去向](mechanism-coverage.md)连接旧 2944 输入组合与当前生产分派；[玩家实测卡](owner-test-card.md)用于最终候选。原始运行、失败及身份留在 `.local/aim-balanced-20261017/`，现装已为 `a283747`，旧 `4c7ac5c` 的受控文件及必要回执备份保留。

已运行的实际边界：

- `balanced-r06`：19 个公开准备→原版 Player/NPC/Main.Projectiles→实际输入/Strike 场景。特殊弓远距、移动高低、倒重力/强化弹速、海啸侧路、持续主通道、710/485、北极与剑鱼远/近；四个旧整组入口调用为零。剑鱼 450 像素场景只有一次 1109 主要碎片命中，250 像素有十次；三种合法主要碎片 1107/1108/1109 分别保留，不承诺散射逐发必中。
- `modes-r10`：两中心×两排序×四模式共 16 组，合法原版放置人偶；清线远者、开墙回近者、路径与关闭行为。Aim 组实际自然攻击，Path-only 不攻击；不把无未来的闲置未知当成正解。
- `safety-r13`：哨兵 46 族/50 次原生出生、魔法绳真实父/自由子体、普通悠悠球原版 motor/寿命/一格出口/导航和原生子体保护。六取消条件当时未打开，已由 `boundaries-r15` 独立跑四个原生受击场景和 release/off/replacement/focus/ui/session 六边界；每项同更新重入四次、撤销意图且不改原版弹体/计时。组件含高预算机制 oracle，不能代普通 Main 成本。
- `phase-r13` / `shot5382-r13`：真实旧手剩余计时只在私有新手清零，日暮同动画保留并只扣一次时域；5382 实际 8 起手/8 出生、7 Enhanced/1 Basic、8 主通道接收。
- `motion-r14` / architecture combat：8 个实际原版弹种×两方向共 16/16 局部接收，2160 个离散端点；中心优先可比旧边缘接触晚一更新，但仍在原版实际 35 更新放置接收体的局部窗口内。架构 combat 零失败。
- `ammoswap-r15`：漩涡真实松手、改选银弹、第二次原版起手，11 次 981 主弹 Enhanced 消费、4 次该主弹实际接收；原先普通弹与火箭分开，旧整组入口零调用。`r06` 后的窄新手/调度/共享依赖修订分别由 phase、5382、当前源码审核及本项承担，不将旧运行改署为新跑全组。

失败原件不改署：首片源码并发指纹拒绝；r03 漏真实 Main.Projectiles Hook；r04 旧派生需求恒 16；r05 漏两种主要碎片外观；r07 人工 AI 组合、r08 空 identity 误判及 r09 闲置未定；safety-r11 装备泄漏、r12 基底反射三/四参数不符；motion-r13 旧 earliest 期待。各有明确夹具/产品/新合同处置，最终验证映射另记。原有受保护导航测试原字节未改，反射兼容在它调用的测试 helper 修复。

普通 Main 采用旧已交付 `4c7ac5c` 与新 `7a8f18a` 普通 Release，完整原版初始化/Update/Draw/EndDraw、隔离世界、脚本物理输入、正常六毫秒预测预算；每窗 600 次真实 game update。三对运行共用驱动 SHA `FE8893C3…`，后续 Path/Off 使用 `D8B2F500…`，细计数使用 `ABAA7B57…`，均独立列身份，不混同驱动。详细绝对值及冷首/稳定/停止四段见 [Main 摘要](main-summary.csv)，载荷/驱动/原始 CSV 哈希、适用性及失败原件见[验证索引](validation-map.json)。

| 实际场景 | 主通道真实输入/接收（旧→新） | 600 个推进 Update 平均 / p95 ms（旧→新） | 稳定段记账 elapsed ms/更新（旧→新） |
| --- | --- | --- | --- |
| 连弩 1229/普通箭，中远距离 | 43 箭，Enhanced 0→38，主接收 8→25；起手间隔均 19 GUC | 1.651 / 7.061 → 1.303 / 2.115 | .797→.293 |
| 漩涡 3475/普通弹 | 72 发，每 5 GUC；Enhanced 44→69；主14接收均18；10火箭均0接收 | 1.858 / 7.267 → 1.746 / 2.716 | 1.213→.456 |
| 移动 NPC/省力悠悠球 | 均90父+90自由子、29实际接收、583移动步，位移一致，路径最长121 | 2.407 / 4.807 → 2.177 / 4.291 | 1.522→1.507 |

全 Update 调用均值另列：1229 .341→.286 ms，漩涡 .343→.468 ms；后者因同600 GUC的 Present 4123→2866，不能按每调用平均宣称退化或按进程CPU宣称净收益。1229 Present4007→3986；悠悠球3179→3369。三对进程全线程 CPU分别23640.6→24375.0、22906.3→20953.1、22015.6→22375.0 ms；这是包含呈现及后台线程的进程窗口，不是瞄准净CPU。`sameFrameCpuMs` 历史列实为 Update/Draw/EndDraw 成对 elapsed 和；记账 elapsed 是现有预测工作计时，均非纯CPU。结论限于主通道有用且替换工作及慢尾下降，未见所测场景实质退化，不声称 FPS。

细诊断与普通成本分开：两新场景分别生成102/210个数学候选方向、执行34/70次单代表 Replay、1564/2910次实际代表 motor 子更新；旧整组 `CaptureForSolve` 为36/71次，新均零；其他被观察旧整组入口新均零。连弩相同36次 `PrepareCore` inclusive elapsed218.168→30.629ms；独立 recorded exclusive212.901→13.156ms，不能把父子 inclusive 相加。漩涡 hand PrepareCore非主要AI链，不冒称这5.957→5.639ms代表整条链；普通预算记账已单列。诊断首个冷请求未完成Generate/Replay的边界保留，其后不是按降低实际出手频率换成本。

Path-only 和 Off 各600GUC：无武器出生/输入，仅一个原版背景720；此事实由独审逐行核对两份完整出生CSV成立（各仅type720/inputKind=-1，241字节、SHA `64A2E767…`），不是由 `nativeBirths-unboundBirths` 宽松守卫单独证明；该守卫不足以作为未来无武器判据。路径单开3462次实际Draw绘线、最长112，关闭0选敌/0路径。推进Update平均1.419/.900ms、记账elapsed有界；不把NPC路线成本说成武器整组成本。停止后相关弹体仍在场，1229最多5个、漩涡28个，记账均约.20ms而未继续整组重演。

新增载体失败也保留：首次新Main包标识前缀选成最小Runtime（加载五步完成、测量窗未开始）；修回生产 `continuous-processing-` 前缀后原DLL/同驱动通过。Path首窗误把新增全出生观察的背景720算作武器，守卫修正后新身份通过；细计数首窗重复Bind异常也只修隔离驱动。统计曾把 `endDraw` 大小写写错，按原CSV重算，没有重跑挑数。

原独审已按R1/新增影响/精确身份完成复核并PASS，root随后准入；没有用作者PASS覆盖旧NEEDS_FIX。剑鱼450外沿低命中、有意不精算的随机附加/次要效果保留；真实多人、所有者物理键鼠/体验、FPS及全项目资格未测。一次07交付已完成，详见[实际交付回执](delivery-receipt.json)；现装为a283747。
交付事实：源码/已装包为 `a283747627ad9159225481b0705d9aec7336b7b4`，ZIP 2,310,828 字节、SHA `B73405E4E0F6FBFA44616E383FC136A86414EB99A0F088C9555C8E66AAAD3BD6`。正式入口一次完整 ContinuousProcessing Release/net472/x86，stage reader正例与七范围锁定通过；826条完整编译输入等实际r19，746条生产源码逐项对应。实际SourceRevisionId产生新的正式程序集身份，未称新旧DLL字节相同或重跑原版。

实际目标 `F:\Games\GamePlats\Steam\steamapps\common\Terraria`。只备份11个旧受控文件（含installed receipt）及必要旧交付回执；旧Restore、新Install均exit0，一次10载荷/manifest/ownership读回通过，原版EXE未变。备份在 `.local/aim-balanced-20261017/install-a283747-20261017-143801/`；没有失败、额外包消费者/整包审计/安装独审或回装演练，没有启动所有者游戏或读取真实角色/世界/用户设置。后续文字/回执提交只记录事实，包和现装代码仍为a283747，不为元数据HEAD重打包重装。交付后停止写入，等待[短实测卡](owner-test-card.md)反馈；Issue/PR仍OPEN/Draft，不合并/关闭/Tag/Release。

# 07 最终普通窗口：事先固定输入与判据

状态：待完整固定候选；不是运行结果或 READY。只复用现有隔离原版 Main 驱动，不操控所有者游戏。

- 对象 A：TASK_BASE 97ceecf7 的生产源码等价基线，既有完整消费者 payload（来源回执357df9b、生产936f865；src/tests/scripts 字节核对记录见M1）。对象 B：最后完整候选四个普通 Release 模块，实际 SHA/MVID 在运行回执写明；不混用诊断 DLL。
- 普通驱动固定 SHA256：24E7F9812E87F42ADE42D0647BB1D2867E4D25009D44DB5003D24680E82E882D。锁定游戏 SHA256：960A03BFF6050CF7BE16DFC1A7B19E10FC2C4F8F835A6A3B135A50DD9E6BA2F3。
- 固定1920×1080、原版完整4200×1200隔离房间、seed7193；每窗600实际GUC，保全Draw/EndDraw分母。物理鼠标在目标反向；输入120–479持续按住、480以后原生松手，包含冷启动/连续/松手后恢复；前100、连续100–499、尾100另列。无额外预热、修改预算或推迟原生出手。
- 主普通批次：moving gun98的Off/Aim/Path/Both各一个ABBA；standing Terrarian item3389 / projectile603 Both一个ABBA；moving Betsy3870 Both一个ABBA。A/B/B/A都保原始冷窗与同一完整驱动。各窗串行，作者与调度静默，不并行构建/原版检查。
- 新增复杂机制普通可用代表：B-only moving Both，item1947（有限矛/周期子体）、3476（有限母子与Kill）、3858（真实持续中段/派生）、190及46（自主近邻出生与独立近战本体）、4270（地形位置出生），各600GUC。根据原生 mana/资格实际停止保留，不补魔、不伪造Enhanced。每把报告真实准备/消费、出生/Strike分母及没有消费者的原生分类，不以组件1000ms代普通机会。
- 在首个最终普通窗前补定新增共享持枪/多发代表3475和3788，各B-only600GUC，原生选弹97；190/46使用现成Dummy49近手部布局，其余保默认移动史莱姆。纯手部不会在远端无原生接触机会的空场中被计作成功；近场与默认移动场各自标明，不作同输入跨布局成本比较。
- 解算困难/失败性能复用已有普通Release组件成本入口，同既有固定场景同输入ABBA：Betsy困难无解+密集环境；普通Main给实际完整链成本，组件调用没有外层预算，二者分别标明。不是每帧成本或FPS。
- 最初177 Basic原因：用已存在稀疏实际消费数值驱动单独base/final对照，记录Valid是否执行/结果、当前准备GUC/phase、current/next、timeline、预算Exceeded，保无诊断普通窗为性能结论。177早于193轨迹分叉，不能以闭环分叉或总hit抵销。

判据沿A事前固定：重要热点≥20%有归因改善；完整准备/验证/调度成本纳入后收益不能被抵销；普通持续成本>10%未解释退化需处理。p95/p99/max、GC、真实出生/方向/第一接收和冷/连续/恢复一起判断，不靠最快一次或Enhanced计数抵销有效质量。一次性JIT/初始化与持续开销分别解释。未知硬件/所有者手感与多人现场不冒称本地PASS。

相关功能矩阵在这批前/后独立串行执行；性能窗本身无函数级Harmony计时。正式M2还需206事件、2944武器输入和762NPC的消费者/机制/复用证据闭合及新Sol独审。未满足07不执行08。

## 最后完整相位影响补窗（执行前锁定）

原32窗及t-Yoyo已执行，事实与不利结果见07-ordinary-results。OnlyFlight公共修订的逐窗实际config复算均clearLine=false、退役PreviewDemand未进入PreparePlan，原gun/Betsy窗口不触达该分支，按此路径依据复用；不以程序集身份相同与否机械判定。

新Held全NPC后pass在原8复杂代表中实际触达3475/615，因此最后完整Release固定后，仅追加3475/Both moving的A/B/B/A四窗，每窗仍600GUC、相同seed/输入/cold100/steady400/tail100/实际Draw分母及原预算。A仍TASK_BASE完整payload，B的四模块/manifest/receipt必须匹配最后固定。另OFF与无finiteHeld来源相邻的功能工作量证据须明确无未来解算；不为这些关闭门再新建普通Main平台或重跑原32。窗口串行并事先通知主调度和作者安静，不与编译/原版测试并行。等待功能尾部和最后固定，不提前把载体称正式开发包或安装候选。

## 原热点 CPU 归因补窗（执行前方法冻结）

现有sameFrameCpuMs仅elapsed，无可复用同世界窗口CPU累计。按07§5补一个独立测量副本，源码位于.local/aim-algorithm-rework-20261015/cpu-window，Framework x86/LAA驱动SHA256 B40AF45B244B19412A9D40424F998558E0E8F0E579B432BB3ED0F796451B6626。未改生产/原预算，未安装函数级profile，StageProfile/RouteInventory调用从该副本移除。

仅原主热点standing Terrarian3389/Both，同TASK_BASE A与最后完整B，A/B/B/A各600真实GUC，同1920×1080/seed/按下120..479/松手480..，无预热。驱动在原世界建立后、首个原版Update前记录一次Process.TotalProcessorTime与Stopwatch；第600个原生GUC更新结束、调用Exit前记录一次。两读间包括进程所有线程、GC及全部其间Draw/EndDraw/等待；启动/内容初始化和退出序列不纳入。读取不是每帧采样，CSV在Main.Run结束后写盘；实际GUC/Update/Draw/EndDraw分母和GC另列。相同新驱动用于A/B，不与旧普通驱动elapsed作同身份合并。

主比较为四窗全部保留后的A均值/B均值进程CPU总量及每600GUC归一量；同时列各窗离散、wall、Draw次数、原Update阶段elapsed、GC、出生/Strike/实际输入机会。CPU累计不计进程睡眠/等待时间，但不据此识别具体GPU或等待原因；不宣称FPS改进。只有原热点工作减少、完整Update改善与本次CPU净消耗证据可同时支持时才签节省未被抵消；如净CPU未减或离散不足以辨明，保NEEDS判断，不能把约10秒固定世界wall当CPU结论。不得删慢窗/尖峰/GC或调整质量分母。与3475最后普通补窗同一安静期串行执行；不复跑质量矩阵。

## 原生呈现工作量可比补窗（有效批次执行前重锁，m2z）

原uncapped四窗和归因四窗全部保留。归因驱动E808B060…07BF的Main线程Update CPU实际减少414.06ms，但B在600GUC内多9.86% Draw。原uncapped驱动强制Immediate/每次Update后取消固定步长；原Main17156–17228保持60GUC而可执行非world Draw，不能把同世界数混同同呈现工作。产品WorldLayer/Draw相对TASK_BASE无变更，功能Draw零新增预测不等于净成本已过。

测量链前置失败均保留、未作CPU比较：早装Present观察导致Bootstrap HARMONY_LOAD PRELOADED，140秒无world；45秒最小标量确认GUC0/menu真。移回真实Host handoff后的既有测试Harmony入口后600GUC正常，但mixed-mode public Present包装观察为0；锁定manager显式IGraphicsDeviceManager.EndDraw的IL后，改在唯一Present调用成功返回处插整数计数，保早退/null/异常门。Native FrameSkipOff短诊断有600GUC/1606实际Present；On有600GUC/563，原fixed-loop因初始化/首次使用追赶而跳Draw。两种只作载体/调度诊断，不挑其中CPU值签收益。Subtle原版默认短诊断600GUC/599实际Present，作为唯一有效公平比较条件。

最终独立cpu-native驱动SHA 3E715E2A5E99675EF1181097AFA00A8BE3A91151DFEA1CA8E3D7D1131F775641；执行前方法82423A04520B129C269061D76A11355CB5C829D9B23924EF20012536C64A483D绑定所有源码/runnerSHA。新A/B/B/A各600真实GUC，A仍TASK_BASE完整payload，B为m2z四普通Release模块；standing item3389/projectile603 Both、1920×1080、seed7193、按下120..479/480原生松手同原输入。仅该隔离新config用原版FrameSkip Subtle与原生One，不强制Immediate/FixedTimeStep，不改变产品/所有者配置，不预热、重置时钟、扩大预算或超时。

呈现计数来自真实XNA manager唯一Present成功调用，不是Draw/EndDraw override数，也不声称GPU已显示完成。实际interval不得Immediate，四窗成功Present max-min<=1，否则保持不可比/NEEDS，不挑窗补到通过。净消耗主比较仍为全部四窗进程CPU总量与600GUC，保离散/GC/实际Update/Draw/Present/出生方向及Strike，不除以Draw制造收益。

同一驱动保三个粗阶段Main线程GetThreadTimes（原Update/Draw/EndDraw override边界，无Core/Source内部计时）和两端进程CPU。原Subtle EndDraw62629有显式Idle自旋/OptimizedGC；若吸收Update节省则如实分解并保净CPU边界，不把阶段节省直接替代进程总量。观察器开销全部保留、不作扣除。CPU边界外各读一次仅本进程线程起址/描述/已加载模块，未知/退休为UNKNOWN，不扫描其他进程或安装profiler。粗计量/固定原生设置的新身份与旧无profile普通窗分开，作者/root停止编测/批读后串行一次；同时执行z3475原普通driver受影响四窗，两个结论分开。

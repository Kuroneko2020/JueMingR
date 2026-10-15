# #114 / Draft PR #115：接触导航与准备成本（集中独审前）

TASK_BASE `69ffc7bfd5223069081c848f1646b49e958b2eea`。现装d177b0a的生产src与base相同；本轮仅看base→当前候选，原件 `references/JMR_AIM_COST_NAV_20261015/`只读。主Astra/Max只读调度，三名Sol/High实施，导航整合者独占Git、公共构建、正式证据和安装。固定候选1ababaf集中独审为NEEDS_FIX（唯一C低弧阻断）；C修订936f865已获独立窄复核PASS，主Astra接受生产候选并批准进入04一次轻量交付；正式完整包、换装尚未执行；**INCOMPLETE / releaseEligible=false**。不merge/close/tag/release，不启动所有者游戏。

## 七项结果

| 项目 | 实施结果与实际边界 |
|---|---|
| 1 A 接触终点与真实穿洞 | 接触区域终点容纳完整球身；保原中心合法直路，有限空气中心走廊仅生成转向候选，真实尺寸／惯性／TileCollision／SlopeCollision与Replay决定消费。旧木球坏Goal同测RED；用户Terrarian3389/603真实18×18/range400，手动连接线本体确实通过16px孔，自动16/32px各三亚格本体Damage通过，封孔各三反控拒绝。旧15个横纵／转角／平台／斜坡亚格场景首Damage=Contact，8px侧孔拒绝，封路2次重试／重开／自然退休恢复通过。未缩球、改碰撞开关、扩大接收框、Basic或射程。图示平移到合法世界边界，未声称等同用户逐帧坐标。 |
| 2 B NPC路径连续性 | 唯一链Host.DisplayPath→CombatObservationWorldLayer→Phase0S DrawEntityLabels已对应。十字相位绑定SampleTick+TickOffset，等价短刷新保有效长尾。旧原包同测试RED，新真实GPU8次Draw与换样本GREEN，Draw未来推进0；真实几何、终止、身份、生命、年龄、段、退休与OFF反控通过。同轨多Draw静止与换sample跳动分别证明；字面箭头形状未见现场画面，不声称新增流动动画或每Draw预测。 |
| 3 C 魔法绳求方向 | 8粗窗、实际响应／canonical细化与双侧固定锚点，最多42次线性探测；同输入同canonical时间的完整四轮只算一次。256组旧177有效/新177有效，推进1,603,510→771,559（-51.88%）。原版30父30子、每4更新机会、镜像／移动／自由窗／取消通过。修订后普通Release r11的90父90子、89E1B、29接收与base相同、0延迟。有限候选非全局最优证明，仍有3个误差较大有效例，见下。 |
| 4 D 有界角度与困难路径 | 2048角桶邻域去重，最多8代表，保旧9参API、直指+32全圆兜底、高抛、三成员映射、瓶高低slot派生顺序。完整原版Ballistic/Betsy/Bottle GREEN，高抛43tick、真实3成员／两瓶六派生正Strike保持。 |
| 5 D 场景与分支成本 | 同样本只捕获实际读集Receiver policy；immutable事实可共享，可变owner/static/local历史各方向独立，同family依原版语义共享。返修删除全方向Branch[]长期存活、无消费者的独立根、匹配policy前的重复immutable数组Clone；新反例对原固定候选RED、repair2 GREEN。 |
| 6 E 扩窗 | SolvePreparation仅一次同步PreparePhase调用链，完整前缀与各自lease和调用输入一致才共享纯响应、terrain读union及最多4个完整outer checkpoint。17+33对直接、121分段、响应追加、真实免疫／generation／武器prefix／朝向／owner位置变化拒旧通过。producer局部epoch数字不替代内容相同；每次ClearExecution撤许可，不跨真实更新复活输入。 |
| 7 A 球组 | 一次确定Prepare捕获Main池关系与安全寿命一次；与旧逐球Remaining/Secondary逐项一致，同GUC真实早slot新出生立即重捕获。私有未来玩家不借真实组，不改随机寿命或手套语义。 |

## C的实际反例、修法和未证明事项

初候选虽177/177、推进262830，实际Main29→25接收；r06为27，r08为31但自查发现4个精确早弧变晚，r09为28。这些窗口及源码身份全留，不把旧最佳窗签给最终源。被审r10为29；C修订后r11仍为29，90次出生方向vx/vy、600步NPC位置／速度、29次hit的时间/type/damage在既有CSV精度下全部与base相同，不由总次数推断每个方向完全等价。

直接链在实际337 BeforeNpc、Age0、FirstTick1、count64：旧t53合法（raw单位误差平方21.08727；返回世界点量化后21.11397），旧候选False且预算未Exceeded，继而扩121；同121新旧均应选t103精确高弧。真实24×18接收框曾误转录为32×18，近似夹具会假绿。当前 `tests/NativeWorldTextProbe/Fixtures/MagicReleaseBeforeNpc.bits` 来自真实同一调用宽121，前64点与短64逐字段相同；所有float保IEEE位型、全future原始／owner／整数框和flags、motion、start／launch／melee／range均读回相等。回归重绑隔离host身份及等价时钟，不导入旧lease。三相位×63/64/65/121共12边界例，旧合法方向与量化中心误差逐项相同。

实际搜索曾停在t55，Bound=5.50>4。不得先要求nearest合法才允许找邻canonical；canonical重演后最近时刻还会移，双侧必须固定在最终nearest。仅删门使64绿但65仍红，该失败保留；最终同时修锚点，上界仍42。删除门后的857409推进未满足原减半保护；同输入完整canonical缓存去掉重复四轮后771149通过，没有修改工作量期待。初版83.61%、r08的79.87%及中间69.53%均不是最终收益。

修订后177有效中174误差相当、3较差：H32/64/120、target(100,200)、vx1为0.9278→9.6975、均t15，最大平方误差损失8.769718；双方精确(<16)的更晚接收0。旧第四例t37→96是独审NEEDS_FIX阻断，不能以仅大于16排除，现已修复并追加实际接收依据（见下）。全轨迹扫描允许seed time≠receive time，候选集合不同于旧逐time canonical；按rank先得到<16不再全局退出，而以较早精确接收比较，但有限选时仍不能证明覆盖所有旧候选。宽窗复用已有合法短窗纯提案保机会，并非宽窗全局重新择优的质量等价声明。

## 集中独审唯一P2及C返修

独立Sol/High对69ffc7b→1ababaf的集中独审结论是 **NEEDS_FIX**，原消息本地完整保存在 `independent-review-1ababaf-original.md`，不改成PASS。其它A/B/D/E及球组无新阻断，66文件身份全吻合，旧性能不利项披露充分。该结论只要求C及实际传播窄复核；随后936f865的窄复核已PASS，无新增阻断，原NEEDS_FIX仍保原署。主Astra结合六项原集中审查与本次复核接受生产候选，沿已有授权进入04；完整资格仍INCOMPLETE。PASS原文和主调度判断分别保本地independent-review-936f865-PASS-original.md、delivery-decision-936f865.md，验证映射绑定hash。

唯一P2为H120、target(500,-100)、vx0，真实18×40接收体与16×16球下旧有效低弧模型nearest t37/error²16.00157，被候选换成t96/29.23829；不能因为略高于16而忽略。trace确认低弧窗 seed33→nearest35→canonical35→nearest36→canonical36→nearest37，旧实现随后重复36并检查38，漏了37自身canonical方向，晚96的29.238先进入Best，38的31.147落败。不是需要改变16阈值或单坐标特判。

修订每窗仍最多5次线性Probe，去重后优先跟进尚未评估的nearest canonical，达到固定点/循环后用余下名额检查同一锚点双侧；复杂分支耗尽5槽有界停止，总上界仍8×5+2=42。新最短断言按原生整数接收框首次交叠和中心误差保护，不能用“更晚且更差”的方向替代旧已有效方向，不再只保护双方<16。20个镜像／相邻远近高低／H64和120例保原接收，24×18原位型三相位×四窗和六个镜像尾段全部保持。

新增明确初始化离体出生状态的有限原版回放，由实际AI99／碰撞／NPC免疫／Damage推进。被审旧Host同测试明确RED：旧参考首Damage37，被审方向95；修后37/37，镜像36/36。这里是新实跑的出生态回放，不把先前模型nearest37/96虚称“真实Damage晚59帧”。完整自然起手→持球输入→父/分体出生→受击、每4更新和取消仍由原MagicStringAimChecks检查，修后30父30子及正常Release消费通过。新推进771559，旧候选771149(-51.91%)保为历史；没有放宽工作量断言。
## 普通Release成本和功能分母

同一普通优化Release、锁定原版1.4.5.8/XNA、seed7193、1920×1080，既有MainLoopSmoke SHA `24E7F9812E87F42ADE42D0647BB1D2867E4D25009D44DB5003D24680E82E882D`，每窗600真实GUC、固定前100/中400/后100。原14窗及必要A/C后继均列[小摘要](main-summary.csv)，原CSV和Draw/EndDraw/同帧elapsed、GC/首用/输入/出生/接收留本机。嵌套区间不相加；sameFrameCpuMs是Stopwatch elapsed（含驱动／GPU等待），不是线程CPU或所有者FPS。

| 范围 | base→最终适用候选 | 限定结论 |
|---|---|---|
| A Aim / Both | Aim更新均值2.805→3.134ms、p99 6.27→8.47；Both3.440→3.500、p99 10.29→10.82；两者真实13接收均保持。Aim steady400 3.409→3.741/p99 7.596→8.472；Both3.977→4.071/p99 10.294→10.821 | 不隐去持续不利项。Aim同帧elapsed10.047→9.191，Draw1004→1093；Both10.263→9.548、Draw978→1048，阶段等待与分母变化不能归因算法CPU全面下降。 |
| A 有限工作量 | 80原版Player/NPC/Projectile updates：Aim NpcMotion2544→2512、Find8→0、Read23540→23352、Passage126→154；Both4224同、Find7→0、Read31379→31635、Passage94同 | 直路避免重复搜索；不是每项都减少，也不把合成计数当自然Main毫秒首因。初候选11接收和较低Both成本保为旧失败窗。 |
| C r11修订后 | 更新均值2.779→2.766、p95 5.419→5.558、p99 7.621→8.838；steady400均值3.084→3.107/p99 7.621→9.531；first100 2.380→2.268；tail100 1.962→1.900；首攻54.707→60.139，无GC | 29接收、90/90、89E1B均保持，出生方向在CSV精度下全部与base同。均值近持平，首用和尾部分位偏高照列；不归因GC或算法唯一首因，不重复测量选值。旧r10各值保持在CSV，不能用较优旧窗覆盖修订。 |
| C r11绘制与总窗 | Draw1044→919，Draw均值6.760→7.764、EndDraw1.098→1.211；同帧elapsed均值9.631→10.941、p99 14.936→17.107；GC0/1均2/1 | 同600更新的同帧elapsed总和约10.055→10.055秒，渲染帧分母不同；每帧尾部偏高并列，不签线程CPU／FPS改善。与旧r10同帧总9.424秒、GC4/2有差异，原记录全留，不把等待阶段移动归因算法。 |
| D/E repair2组件 | 四场mean1.566/3.073/57.168/1.368ms；紧邻prior1.888/3.044/59.129/1.397，base1.896/3.267/87.071/1.362；困难场GC0/1各10→8 | 3预热+24样本，Generate+真实Replay总时序，无motor Harmony计时。小可达仍+0.029ms，Ale+0.006；高抛cold25.858未称解决。首轮candidate2.320/3.883/60.389/1.576及base1.949/3.718/91.511/1.411全部保留。profile未唯一解释原+0.37，不归罪角桶。 |
| D普通Main | 39出生、18E21B、8接收相同；更新均值2.35→2.38/p99 10.47→11.08 | 该窗为原D候选，repair2只签已复核同候选/原版消费和组件收益的适用复用，没有重签成repair2新Main。 |
| 普通枪保护 | 45出生/20接收/0延迟相同，44E1B→43E2B | 额外Basic在129，记录combat5.660→6.223ms接近原6ms门，仅与冷预算一致，精确内层首因unknown；不能称完全等价，不以更少正常机会换速度。 |

A首攻Aim58.216→50.992ms、Both59.942→56.230；Both首攻都有GC0/1各1。A新Aim最大90.79在step1无攻击／无GC；这些不能统称算法或GC。Off均值1.53→1.67、Path约2.03→2.03保原窗，不另为每武器复制四组合。B Draw未来推进0与Source五更新节奏保持。

## 字节身份、失败处置和审查入口

最终C及整体普通Release四模块：Host MVID `37b7ffd8-855b-4f57-a21f-e5c9cd24bc7b`，SHA `E536FC4D99882305A0C53EC6C2BF6443725370CB386B0EF94025F0C5EE27A743`；本机 `measurement/review-repair-build-record.json` 与 `review-repair-payload-receipt.json`。此有限载体不是正式完整包。不同A/B/D证据保原MVID/SHA及精确未受影响输入关系，见[验证映射](validation-map.json)；不因最终commit/PE元数据不同而改历史结果身份。

A尺寸推断、封孔中心嵌入、旧侧孔motor卡住及6ms首次冷准备等失败均保留；最终A-navigation-r8完整绿，物理正确性1000ms隔离scope不当作普通预算性能。一条本地注释编辑命令曾误写空导航文件，构建拒绝；恢复后整个导航与嵌套所有方法IL逐字节等于已验证A-r8 Debug DLL。`recovery-byte-proof.txt`及已验证Host SHA `E49683CDB4A80183E222E47F6112F74E32236E6DDEBB0E78E7D4127320364C74`、恢复源SHA `BBAB2FB667109261D5AFB6A812BA6B0DF61C58D42DAC3AE0DD2F675DEB01A017`供独审。后继7行直路早退另签13接收新窗，不冒充同源IL证明。

B baseline首次误用完整Player.Update触未初始化成就，改既有Sample+Fresh，仅测呈现。D/Betsy/Bottle旧idle ExpectedPreview与已接受PreviewDemand=false冲突，baseline同败；窄修为idle无hypothetical／无solve，真实出生／Damage断言全留。一次载体packageId失去ContinuousProcessing前缀导致最小profile初始化失败，r07空MVID拒载，精确诊断r1误传Dummy=1无有效场景，均不进正式性能分母。Workload routing最终PASS，WindowsPowerShell5.1既有证据脚本PASS；pwsh7旧Add-Type ConsoleApplication错误保留，不修无关工具。最终architecture combat=0failures。

外来dirty参考说明、.workbuddy/、issue112.txt、pr113.json按接管hash保护，不纳入提交。大日志／PNG／CSV只留 `.local/aim-cost-nav-20261015/`，小材料提供路径/hash与用途。未核所有者实际加载、FPS、多人或游戏内体验。

04交付窗口：原NEEDS_FIX经C修订窄复核PASS闭合，主调度已作接受判断。沿已有授权，仅一次普通完整包、目标/拥有权核对、旧受控文件备份、官方Restore→Install各一次、一次长度/hash读回及真实回执；不全用户目录hash、不第二安装审查、不反复包消费者。停止本任务进程，不替所有者启动游戏。

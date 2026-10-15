# #114 / Draft PR #115：接触导航与准备成本（集中独审前）

TASK_BASE `69ffc7bfd5223069081c848f1646b49e958b2eea`。现装d177b0a的生产src与base相同；本轮仅看base→当前候选，原件 `references/JMR_AIM_COST_NAV_20261015/`只读。主Astra/Max只读调度，三名Sol/High实施，导航整合者独占Git、公共构建、正式证据和安装。当前生产已冻结，独立审查、正式完整包、换装尚未执行；**INCOMPLETE / releaseEligible=false**。不merge/close/tag/release，不启动所有者游戏。

## 七项结果

| 项目 | 实施结果与实际边界 |
|---|---|
| 1 A 接触终点与真实穿洞 | 接触区域终点容纳完整球身；保原中心合法直路，有限空气中心走廊仅生成转向候选，真实尺寸／惯性／TileCollision／SlopeCollision与Replay决定消费。旧木球坏Goal同测RED；用户Terrarian3389/603真实18×18/range400，手动连接线本体确实通过16px孔，自动16/32px各三亚格本体Damage通过，封孔各三反控拒绝。旧15个横纵／转角／平台／斜坡亚格场景首Damage=Contact，8px侧孔拒绝，封路2次重试／重开／自然退休恢复通过。未缩球、改碰撞开关、扩大接收框、Basic或射程。图示平移到合法世界边界，未声称等同用户逐帧坐标。 |
| 2 B NPC路径连续性 | 唯一链Host.DisplayPath→CombatObservationWorldLayer→Phase0S DrawEntityLabels已对应。十字相位绑定SampleTick+TickOffset，等价短刷新保有效长尾。旧原包同测试RED，新真实GPU8次Draw与换样本GREEN，Draw未来推进0；真实几何、终止、身份、生命、年龄、段、退休与OFF反控通过。同轨多Draw静止与换sample跳动分别证明；字面箭头形状未见现场画面，不声称新增流动动画或每Draw预测。 |
| 3 C 魔法绳求方向 | 8粗窗、实际响应／canonical细化与双侧固定锚点，最多42次线性探测；同输入同canonical时间的完整四轮只算一次。256组旧177有效/新177有效，推进1,603,510→771,149（-51.91%）。原版30父30子、每4更新机会、镜像／移动／自由窗／取消通过。最终普通Release r10的90父90子、89E1B、29接收与base相同、0延迟。有限候选非全局最优证明，仍有4个误差较大有效例，见下。 |
| 4 D 有界角度与困难路径 | 2048角桶邻域去重，最多8代表，保旧9参API、直指+32全圆兜底、高抛、三成员映射、瓶高低slot派生顺序。完整原版Ballistic/Betsy/Bottle GREEN，高抛43tick、真实3成员／两瓶六派生正Strike保持。 |
| 5 D 场景与分支成本 | 同样本只捕获实际读集Receiver policy；immutable事实可共享，可变owner/static/local历史各方向独立，同family依原版语义共享。返修删除全方向Branch[]长期存活、无消费者的独立根、匹配policy前的重复immutable数组Clone；新反例对原固定候选RED、repair2 GREEN。 |
| 6 E 扩窗 | SolvePreparation仅一次同步PreparePhase调用链，完整前缀与各自lease和调用输入一致才共享纯响应、terrain读union及最多4个完整outer checkpoint。17+33对直接、121分段、响应追加、真实免疫／generation／武器prefix／朝向／owner位置变化拒旧通过。producer局部epoch数字不替代内容相同；每次ClearExecution撤许可，不跨真实更新复活输入。 |
| 7 A 球组 | 一次确定Prepare捕获Main池关系与安全寿命一次；与旧逐球Remaining/Secondary逐项一致，同GUC真实早slot新出生立即重捕获。私有未来玩家不借真实组，不改随机寿命或手套语义。 |

## C的实际反例、修法和未证明事项

初候选虽177/177、推进262830，实际Main29→25接收；r06为27，r08为31但自查发现4个精确早弧变晚，r09为28。这些窗口及源码身份全留，不把旧最佳窗签给最终源。最终r10为29；前73次出生方向与base一致，第一次方向差413，600步NPC位置／速度、29次hit的时间/type/damage在既有CSV精度下全等，不由总次数推断每个方向完全等价。

直接链在实际337 BeforeNpc、Age0、FirstTick1、count64：旧t53合法（raw单位误差平方21.08727；返回世界点量化后21.11397），旧候选False且预算未Exceeded，继而扩121；同121新旧均应选t103精确高弧。真实24×18接收框曾误转录为32×18，近似夹具会假绿。当前 `tests/NativeWorldTextProbe/Fixtures/MagicReleaseBeforeNpc.bits` 来自真实同一调用宽121，前64点与短64逐字段相同；所有float保IEEE位型、全future原始／owner／整数框和flags、motion、start／launch／melee／range均读回相等。回归重绑隔离host身份及等价时钟，不导入旧lease。三相位×63/64/65/121共12边界例，旧合法方向与量化中心误差逐项相同。

实际搜索曾停在t55，Bound=5.50>4。不得先要求nearest合法才允许找邻canonical；canonical重演后最近时刻还会移，双侧必须固定在最终nearest。仅删门使64绿但65仍红，该失败保留；最终同时修锚点，上界仍42。删除门后的857409推进未满足原减半保护；同输入完整canonical缓存去掉重复四轮后771149通过，没有修改工作量期待。初版83.61%、r08的79.87%及中间69.53%均不是最终收益。

最终177有效中173误差相当、4较差：H32/64/120、target(100,200)、vx1为0.9278→9.6975、均t15；H120、target(500,-100)、vx0为16.0016→29.2383、t37→96。最大平方误差损失13.23672；双方精确(<16)的更晚接收0。全轨迹扫描允许seed time≠receive time，候选集合不同于旧逐time canonical；按rank先得到<16不再全局退出，而以较早精确接收比较，但有限选时仍不能证明覆盖所有旧候选。宽窗复用已有合法短窗纯提案保机会，并非宽窗全局重新择优的质量等价声明。

## 普通Release成本和功能分母

同一普通优化Release、锁定原版1.4.5.8/XNA、seed7193、1920×1080，既有MainLoopSmoke SHA `24E7F9812E87F42ADE42D0647BB1D2867E4D25009D44DB5003D24680E82E882D`，每窗600真实GUC、固定前100/中400/后100。原14窗及必要A/C后继均列[小摘要](main-summary.csv)，原CSV和Draw/EndDraw/同帧elapsed、GC/首用/输入/出生/接收留本机。嵌套区间不相加；sameFrameCpuMs是Stopwatch elapsed（含驱动／GPU等待），不是线程CPU或所有者FPS。

| 范围 | base→最终适用候选 | 限定结论 |
|---|---|---|
| A Aim / Both | Aim更新均值2.805→3.134ms、p99 6.27→8.47；Both3.440→3.500、p99 10.29→10.82；两者真实13接收均保持。Aim steady400 3.409→3.741/p99 7.596→8.472；Both3.977→4.071/p99 10.294→10.821 | 不隐去持续不利项。Aim同帧elapsed10.047→9.191，Draw1004→1093；Both10.263→9.548、Draw978→1048，阶段等待与分母变化不能归因算法CPU全面下降。 |
| A 有限工作量 | 80原版Player/NPC/Projectile updates：Aim NpcMotion2544→2512、Find8→0、Read23540→23352、Passage126→154；Both4224同、Find7→0、Read31379→31635、Passage94同 | 直路避免重复搜索；不是每项都减少，也不把合成计数当自然Main毫秒首因。初候选11接收和较低Both成本保为旧失败窗。 |
| C r10 | 更新均值2.779→2.725、p95 5.419→5.103、p99 7.621→8.202；steady400均值3.084→2.918/p99 7.621→7.579；first100 2.380→2.972；tail100 1.962→1.705；首攻54.707→54.271，无GC | 29接收、90/90、89E1B均保持。新max90.507在step1无攻击且GC0=1，不能归因C求解；step293有13.461ms无GC，内层首因未知，不再追加单点诊断。 |
| C绘制与总窗 | Draw1044→909，Draw均值6.760→6.946、EndDraw1.098→1.540；同帧elapsed均值9.631→10.368、p99 14.936→19.194；GC0/1为2/1→4/2 | 同600更新的同帧elapsed总和约10.055→9.424秒，渲染帧分母不同；总窗降低与每帧尾部偏高并列，不签全面性能改善或FPS。GC增加及绘制分段首因未唯一归因。 |
| D/E repair2组件 | 四场mean1.566/3.073/57.168/1.368ms；紧邻prior1.888/3.044/59.129/1.397，base1.896/3.267/87.071/1.362；困难场GC0/1各10→8 | 3预热+24样本，Generate+真实Replay总时序，无motor Harmony计时。小可达仍+0.029ms，Ale+0.006；高抛cold25.858未称解决。首轮candidate2.320/3.883/60.389/1.576及base1.949/3.718/91.511/1.411全部保留。profile未唯一解释原+0.37，不归罪角桶。 |
| D普通Main | 39出生、18E21B、8接收相同；更新均值2.35→2.38/p99 10.47→11.08 | 该窗为原D候选，repair2只签已复核同候选/原版消费和组件收益的适用复用，没有重签成repair2新Main。 |
| 普通枪保护 | 45出生/20接收/0延迟相同，44E1B→43E2B | 额外Basic在129，记录combat5.660→6.223ms接近原6ms门，仅与冷预算一致，精确内层首因unknown；不能称完全等价，不以更少正常机会换速度。 |

A首攻Aim58.216→50.992ms、Both59.942→56.230；Both首攻都有GC0/1各1。A新Aim最大90.79在step1无攻击／无GC；这些不能统称算法或GC。Off均值1.53→1.67、Path约2.03→2.03保原窗，不另为每武器复制四组合。B Draw未来推进0与Source五更新节奏保持。

## 字节身份、失败处置和审查入口

最终C及整体普通Release四模块：Host MVID `895a6086-787c-408e-b8d7-4a512b1dee24`，SHA `97651AC11CCF948AAF3BD79D82FD6501D07BFBA5AE40A8850675DCA5058825C9`；本机 `measurement/closed-build-record.json` 与 `closed-payload-receipt.json`。此有限载体不是正式完整包。不同A/B/D证据保原MVID/SHA及精确未受影响输入关系，见[验证映射](validation-map.json)；不因最终commit/PE元数据不同而改历史结果身份。

A尺寸推断、封孔中心嵌入、旧侧孔motor卡住及6ms首次冷准备等失败均保留；最终A-navigation-r8完整绿，物理正确性1000ms隔离scope不当作普通预算性能。一条本地注释编辑命令曾误写空导航文件，构建拒绝；恢复后整个导航与嵌套所有方法IL逐字节等于已验证A-r8 Debug DLL。`recovery-byte-proof.txt`及已验证Host SHA `E49683CDB4A80183E222E47F6112F74E32236E6DDEBB0E78E7D4127320364C74`、恢复源SHA `BBAB2FB667109261D5AFB6A812BA6B0DF61C58D42DAC3AE0DD2F675DEB01A017`供独审。后继7行直路早退另签13接收新窗，不冒充同源IL证明。

B baseline首次误用完整Player.Update触未初始化成就，改既有Sample+Fresh，仅测呈现。D/Betsy/Bottle旧idle ExpectedPreview与已接受PreviewDemand=false冲突，baseline同败；窄修为idle无hypothetical／无solve，真实出生／Damage断言全留。一次载体packageId失去ContinuousProcessing前缀导致最小profile初始化失败，r07空MVID拒载，精确诊断r1误传Dummy=1无有效场景，均不进正式性能分母。Workload routing最终PASS，WindowsPowerShell5.1既有证据脚本PASS；pwsh7旧Add-Type ConsoleApplication错误保留，不修无关工具。最终architecture combat=0failures。

外来dirty参考说明、.workbuddy/、issue112.txt、pr113.json按接管hash保护，不纳入提交。大日志／PNG／CSV只留 `.local/aim-cost-nav-20261015/`，小材料提供路径/hash与用途。未核所有者实际加载、FPS、多人或游戏内体验。

待执行04：未参与实施的Sol/High对本轮固定候选集中只读独审；主调度据此作整体交付判断。通过后沿已有授权，仅一次普通完整包、目标/拥有权核对、旧受控文件备份、官方Restore→Install各一次、一次长度/hash读回及真实回执；不全用户目录hash、不第二安装审查、不反复包消费者。停止本任务进程，不替所有者启动游戏。

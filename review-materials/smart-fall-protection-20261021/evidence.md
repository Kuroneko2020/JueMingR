# 智能防摔定向证据入口

任务 [#130](https://github.com/Kuroneko2020/JueMingR/issues/130)，Draft [#131](https://github.com/Kuroneko2020/JueMingR/pull/131)，起点 `62bd6e4ef42eacd7a22b898ee538650a7b920747`。集中独审对候选 `ba096176269e047a776788a44c527cf632f52077` 给出NEEDS_FIX；下述修正与定向执行已完成，增量复核及后续交付状态回任务Issue。本文证据冻结时尚未打包、换装或启动所有者游戏。

## 集中独审与增量责任

独立报告 `.local/smart-fall-protection-20261021/independent-review/review.md` 覆盖起点到ba096的47文件及直接接缝。五项确定缺口分别为：名义shootSpeed错误排除实际及时的钩、临时拒绝解除后未恢复候选、接触缓存漏掉新增更早接触面、临借不可用被误判ready及未消费实际干扰事实、物品使用门误挡已装备的合法诅咒跳跃。原七叶PASS支持其已执行代表，不覆盖上述组合和变化反例，也不代签独审通过。

返修由原责任作者在原有叶补直接反例。整合者另修本轮新增GetItem hint的真实缺口：原版完整执行但未取得物品不得推进Revision；沿真实满包拒收／空位取得正反例核对。保留原报告与失败，只复审增量及真实传播，不重开全量审计。下列canonical2仍保留其原始执行身份，不冒充返修后的新执行。

返修公共PrepareOnly为 `.local/smart-fall-protection-20261021/prepare-review-repairs1.log`／`prepare-review-repairs2.log`，均编译和结构零警告、零错误。批次 `751611e2936245df8704def8dbb8c8dd` 实际执行Ownership叶 `802b88296fac4b87acedef03a92d370c`、RiskActions叶 `564316a94f844d6696f958af948c1631`，均PASS。原生叶 `f85b663ed07c45c695ef9922e2e5b41f` 保留总FAIL：新增busy后备误用仅Queen及时的100px窗内Bat，以及旧钩向下先附着而不退休。按原生因果将前者改为已证及时的Bat240px窗口，仍保留被动Busy阻塞与真实Grapple ready断言；后者仅改旧钩初始向上，由原版射程／返回自然退休，不放宽成功或无重复期待。生产在该第二次初态修正中未变。

最后只重跑同一canonical原生叶：批次 `36095839b83849fda2ef1c2c3ff6fdb6`，执行 `f6dee964cd9e4e439f2233ad98f51666`，实际PASS／EXECUTED；两次执行源／输入前后一致、buildMatch为true，第一批两个架构叶继续复用。真实原始日志为各批次目录的 `<执行ID>.json.log`，receipt及qualification同目录，失败产物也归档。最后原生日志SHA256 `A7578D2CE0AB4B8D37324DAC892897DBDE6285FB387FCBA9A820B4FD042DE8E7`；执行Git标签是ba096加本次待提交修正，执行源码指纹 `4F661B0BECB42B213E77C2694A68C5C25A4B039867170BC6DFC68C97C575D409`，可执行输入指纹 `3DA7D2DC23F7AA7FF7905288B58F45DA0F2899DFC689112D167EB3A46468A44D`，不将ba096裸提交称为这次新源码。

| 缺口 | 实际修正与直接原生结果 |
| --- | --- |
| R1 | 缓存全部有限合法钩来源，按实际离散可行性比较；装备84＋携带Queen100px，Queen出生1／附着8／保护9，物品归还并保钩；普通同窗接触9／保护11且受伤为独立迟到对照 |
| R2 | 真实busy／buffer／端点布尔变化重核准备；原生容量循环记录已知旧钩key，不额外扫弹幕池。保持64次条件不变无重试；旧钩原生退休后一次重试并保护，原ItemCheck解除busy后一次刷新、真实落地不受伤 |
| R3 | 保留本次实际有限碰撞走廊的格事实，含原空格；更近新平台使缓存当次失效，无关格不唤醒。8次稳定快照合计256次原格重读、零新计划／来源扫描；非全世界变更扫描 |
| R4 | 只读GetPreparation与执行TryBorrow共享真实门，比较借还／替换／选择／资源／轨迹等事实。被动Busy不可借时既装Bat仍真实及时保护；真实资源与所选源事实改变共同候选选择／成本 |
| R5 | 已装合法能力不走库存动作门；原版buff23诅咒下同起态手动Jump与辅助均真实消耗跳段，杖实际use仍被禁止 |
| 取得hint | 仅真实GetItem接受余量变化推进Revision；满包返回原量保持hint，空位实际取得推进hint；原causal获取逻辑未替换，无额外背包轮询 |

R3稳定带快照的0.0271／0.0317ms p50／p95与11520线程字节含测试反射／夹具，仅是8次有限样本，不能称helper零分配或真实帧时。最后同叶保留四稳定路径各20,000更新与独立分配窗，稳定分配零且来源／计划／tile增量零；危险无能力20,000次合计11.9278ms，包含新增O(1)阻塞门采样。Rocket24＋4首次点火配对manual p50／p95=0.0021／0.0054ms、assisted=0.0158／0.0321ms，两侧分配零。上述与首审前分母各保真实源身份，不扩为完整救援／归还／渲染或FPS。

配置、布局、Jump和普通传送四份canonical2历史证明不因movement-host粗分组机械重跑。`.local/smart-fall-protection-20261021/review-repair-historical-inputs.json` 保存其原执行ID及直接行为／期待字节未变比对；阶段记录沿现有taskAcceptance取本次三必要叶，其余四标为HISTORICAL_ONLY_NOT_REASSESSED，不改签为新候选重跑通过。

增量复核 `independent-review/repair-review.md` 已接受R1—R5／GetItem及两项原生初态修正，仅单列R1新增来源后的R6重复几何成本。R6修正在同次候选构造内复用shoot／shootSpeed完全相同的成功或失败Find结果，每个真实端点仍独立准备／比较并将Plan.Item重绑最终对象；不跨tick缓存，不恢复不同飞行参数间的名义速度筛选。

R6只执行既有canonical原生叶：PrepareOnly日志 `.local/smart-fall-protection-20261021/prepare-r6.log` 编译／结构零警告错误；批次 `3ef8093f6ca14785a5ce9b839cde6a33`，执行 `beb677ce679f4808a82e1a2b441ca3e1` 实际PASS／EXECUTED，源／输入前后稳定、buildMatch=true。该原始日志SHA256 `1B1F281FE68F914B827D80077C4BDD3B8FC45607EDE580BC4F08072B4AD6228A`，执行标签4ec9加此次待提交修正，sourceFP `924661BD6A152815199ED097B7E0A4F6E7CDBACB83C0CABD732B1D03509CD0DB`／inputFP `EA38DE08F3728AE60C7588EA5FB21BA209E719D84B4F48636ED7DC6F2592550E`。

真实单1／多4端点：及时240px均一次Find、123射线子步／797读格；不及时100px均一次Find、51子步／360读格，失败结果同样共享。不同准备成本的首副本selected0与真正被选17并存，真实临装17、出生8／附着22／保护23、精确归还和保钩／手动解除均通过；既有84／Queen差异不变。四个first-plan计时只含该有限fixture样本，不报告FPS或全程救援收益。两个纯Decision／Platform架构叶及其期待本波未改，保留802b／5643原执行与未变直接SHA，不因Host粗组重跑。最终Stage当前必要入口为该原生叶，另外六份保HISTORICAL_ONLY_NOT_REASSESSED；这不把本任务分阶段的真实执行笼统改签成“最终七叶全新PASS”。

## 原件、合同与责任

`references/JMR_SMART_FALL_PROTECTION_20261021_v1/` 七份Markdown逐项SHA匹配，本地核验为 `.local/smart-fall-protection-20261021/task-package-integrity.json`。ZIP容器未定位，未补造或改写正文。原件01／03映射Features/Movement及Host风险、动作、几何、抓钩；02映射BorrowOwner、有限ItemOperationOwnership及FishingEquipment同格协调；00／04映射Session、F5／codec、证据及交付。

正式契约为 `docs/功能介绍/移动辅助行为合同.md`，阶段／状态所有权／失效设计为 `docs/设计/移动辅助实现设计.md`；体验卡为 `scripts/phase0s/Continuous-Processing-Owner-Test-Card.zh-CN.md`。源码、期待与正式合同设计一并冻结后独审，不以作者合同和作者测试相互代签原件。

## 首审前定向执行与来源

`.local/smart-fall-protection-20261021/prepare-canonical2.log` 记录正式PrepareOnly：Debug编译及结构0警告、0错误。没有执行Related／Full。

首审前批次 `artifacts/build/workload-checkpoints/a6f45e92f02748e58ae7668e26132a50/targeted-results.json`：七精确叶全部实际执行PASS。

| 叶 | 执行ID |
| --- | --- |
| MovementPreferenceChecks | 673f5b233dc24580ae3d324cd06e0200 |
| SmartFallBorrowOwnershipChecks | 86add9ddea764603b27a3998a4a622cc |
| SmartFallRiskActionsChecks | 6bee7ff7d6734088962aee3c2ff475aa |
| fixture-movement-layout | 2eaa475cc2de425c8107c6c75ed52bcb |
| native-MovementJumpCpu | 0ad6c8d07ba445889f53bd4d1e3659a5 |
| native-MovementTeleportCpu | 790c533721fa483aa00206d332f18c50 |
| native-MovementSmartFallCpu | b118b5c99688447c801e4f49dcda19e7 |

同目录 `<ID>.json`、`.json.log`、`.qualification.json` 保存命令、原始日志、稳定资格及归档产物SHA。执行时Git标签为首合同提交 `a08d4ee786dcc7bec962428c9fe85fc7cc6e92cb` 加待提交源码／期待；源码指纹 `F862F3EE0CED0C463435A35A0827812E8A38BDC775ABADE752986E7331ECFF4A`，可执行输入指纹 `C1B4E55E56006DC2CDC228783D786A2EFD14A7F25F71F44A9F5F034537FFF499`。a08本身不是被测完整源码；正式提交后按同字节输入／产物及既有复用函数核对，不改写旧执行身份。Debug Host归档SHA `179284C39885CAD86DB98D044534745017CA829B669F6C18AD024CFD2E0D0E2A`。

原生主日志为同目录 `b118b5c99688447c801e4f49dcda19e7.json.log`，默认完整canonical，临时selector已删除。实际覆盖十二族原生消费者／资源，Horse／Wings落地与early Hurt归还，Rod动画／选槽归还，Gravity归还，Safe关闭保持保护，Flying在平台flags前退骑保原平台，原骑待还用户接管；几何含左右迎坡／顺坡、反重力坡、半砖、Down、侧墙。

普通84、Bat1800临借、Queen4980均真实发射→Projectile.Update→Player保护。计划Delay与独立原生附着／立即登记／首次保护相位严格相等；普通／Bat保护时尚未首次触底，保持挂钩穿平台，成功后无持续规划，真实手动Jump有效。另有gap0已触底才发钩的原版Hurt负例。

借用覆盖真实前缀／收藏、第一写owner／第二写故障、同签名接管、原物移位／移出域、持续伞／单次杖token、替人／代次／线程、message5编码、真实wet-bobber A→F→S→F→A及真实ItemSlot接管／hover hint正反例。手柄仅执行安装后的真实guard prefix，不声称完整UI或物理控制器。共享scope含仅防摔ON预约、remote／clone拒绝且不撤本机owner、OFF／输入门及原异常finalizer。Jump／Teleport两叶保留其原版资源／消费者和直接接缝回归。

## 开发失败与准确因果

开发失败留在本任务 `native-feedback1/2/6/7.log`、`targeted-feedback3/5.log` 及checkpoint，不改签。反射helper、重复Aglet非法初态、跳过Main的七钩元数据／WingStats／Gore、无关Steam成就出口均据准确callee／原生前置修正，不扩fake游戏或全Main。AFS复用已有原生fixture及成就出口隔离；原版同签名occupied点击本不互换，真实反例先腾空再穿入新对象，守恒期待未放宽。

生产修正含inactive Mount属性门、Rod误把“合法无需改”的false当非法、Safe的next-velocity CanMount窗口、关闭不得借自身免摔提前卸保护、原骑待还的新骑接管、Double对应unused族，以及许可／tile／真实物品变化失效、自身逆还Revision不重开失败。space原本安全初态不通过改Risk绕过，有限场景移入真实危险带。

曾漏读AI7立即登记而误推普通钩+2，现恢复真实+1。`canonical1`批次 `2bf38dce2dd048729aad1b5525246ea4` 虽全绿，仍不作为最终时机证明；canonical2增加计划Delay与独立原生相位等值、首次接触及迟到伤害断言，击穿错误+2并绑定修正源码。

## 成本与实际分母

最终主日志 `SMART-FALL PAIRED-COST`：等价Rocket首点火原生组件与生产scope／近探／首次动作finalizer，预热4、正式24对；manual p50／p95 0.002／0.0032ms，assisted 0.011／0.0145ms；两侧线程分配总0，helper24次近探／768次tile读取。仅首次动作，不代表完整救援／归还全程。

OFF、ON闲置、ON原本安全、ON危险无能力各20,000次强委托更新，另独立20,000次分配窗，稳定新增来源扫描／计划／tile读取均0、线程稳定分配0。反馈5原分母／产物继续保留于 `artifacts/build/workload-checkpoints/9158587822cf48a3a62938a010934bb7/73606adfadfc4787830d68eb4cf6a871.json.log`；最终因共同时机、资格及合法初态传播执行本功能叶，不为漂亮数重测。

恢复受阻另有100次机会不变不重找原物／空位；真实在途单钩拒发、临借逆还后64次无进展不重借／重动作，防自身Revision唤醒。hint正例为真实物品获取、slot/cursor真变、配装成功与已请求观察，反例为hover及稳定无能力下降。仅检查此依赖，没有启动物品全组。上述为helper线程／原生组件，非完整渲染帧时、FPS或联网体验。

## 资格与交付边界

修正增量复核通过与主调度准入前不包／安装；动态结论回任务Issue。本文证据冻结时完整Main／图形、物理键盘／手柄、真实房主／客机联网、FPS、所有者启动体验均NOT_EXECUTED。普通好友本机作用域与原生同步是生产责任，没有按连接方式禁用；message5编码／发送不是服务器ACK或多人验收。

普通完整包沿现有ContinuousProcessing StageDevelopmentRecord，保留全部功能及范围外历史FAIL／NOT_RUN，fullQualification仍INCOMPLETE；不Tag／Release／合并／关闭任务。核真实现装b949回执后按本次原目标授权做一次预检、轻量受控备份、正式Restore→Install与必要读回，不启动／操作所有者游戏或存档。实际候选提交、包／换装身份回Issue130及本任务本地交付回执，完成后等待所有者体验与网页复审。

## 2026-10-22 五项返修增量（bff556a 起，旧记录原样保留）

原委托 `references/JMR_SMART_FALL_REPAIR_20261022_v1/` 五 Markdown/SHA256SUMS 已核；延续 #130/Draft #131，原开发包在 `references/归档文件/JMR_SMART_FALL_PROTECTION_20261021_v1/`。本节只覆盖本轮 F1—F5 与直接接缝，不把昨日旧 PASS 改签为修后新执行。

根因追踪：`ba096176269e047a776788a44c527cf632f52077` 引入临借 misc 保护却缺 owned-native-use、FlyingMount Delay1 未纳前更新 stairFall、自己的失败钩整族拒绝及当前距离 Risk 门；原物品 guard 更早已存在。`4ec9cac6` 补准备/旧容量钩与脚下接触带、`bff556a3` 补同飞行参数共享，都未覆盖这五缺口。原 fixture 换 Player 未重绑 Items session，已装备／失活 guard PASS 无法代证受保护背包临借；坐骑固定 false flags 和平平台、旧容量钩、长坠落也分别漏掉前更新斜面、自己的失败钩、临界跨阈。

修复与独立判据：

- F1：准确 Player/session/generation/token/Item/端点/动作族的同步一次使用许可，仅真实 StartUse prefix 消费，try/finally 撤销；保护对象移动／出售等守恒门保持。专属段实证 guard Active=true、AutomaticOperation=false，真实 QuickMount/QuickGrapple 成功及无许可／异Player同保护引用／错误身份、一次消费、拒绝、异常和归还负例。异Player原先提前放行是旧接缝，本轮按同因身份要求补真实prefix阻断，不能只依helper拒绝。
- F2：斜平台且原版骑乘会穿台时完整 Delay2，平平台仍1。真实原生前更新及flags留下stairFall，Hover→退骑→尚未触坡的真实斜碰撞自然清理；左右斜面有水平速度均保住原平台并无伤。独立 gap15 晚召骑控制确实留下旧stair并漏坡，gap25当前阶段成功；不清字段、不补完整PlayerFrame。
- F3：第一发前无旧钩；准确自己的 ProjectileKey 自然返回，失败后16个不变危险快照无重复Find／发射，原生自然退休后重开且第二次真实GrappleMovement免伤。快照稳定探测有意不推进Player物理，不声称完整连续Main；恢复阶段执行原生运动/碰撞/投射物消费者。
- F4：初始普通钩无及时抓点；2次稳定／无关地形变化不重搜，脚下带不变、位移小于128px时在实际读过的上侧空格新增锚点，恰1次Find恢复，真实免摔，位移59.67811px。4096格独立锚点／实际射线依赖含空格与液体，不裁已越过Y的上侧点；已知钩观察不全池扫。
- F5：gap7初始整数距离25，OFF原生落伤扣10血、ON真实已有Cloud charge消费且免伤；相邻gap15/fallStart+1安全，两边0动作/0伤害。gap25真实坐骑准备OFF扣10、ON免伤。慢84钩另在当前距离仍安全且Delay大于3时取得完整准备，真实及时免摔；入口／最终／发钩门共用预计接触风险，不全面减阈值。保守48更新包络仅准入有限缓存查询，实际接触及各方案窗口决定发动。
- 社交53瓶无charge保留无跳跃，合法马掌后备真实生效；抓钩成功后仍保活钩穿平台、借还不松钩、模拟连跳不解、玩家真实Jump解除，成功后无继续规划。扩大临界前视曾触发活钩后再规划，真实失败保留并在便宜门排除当前grapCount后关闭。

最终三叶新执行均 PASS/STABLE，批次 `artifacts/build/workload-checkpoints/7163cc28defa4b7697385c5061349726/targeted-results.json`：

| 责任 | executionId |
| --- | --- |
| SmartFallBorrowOwnershipChecks | 63deeb0c99a44d4f96a431d2df5a2ee6 |
| SmartFallRiskActionsChecks | b2abc223f5304c83b87737a7f06207b8 |
| native-MovementSmartFallCpu（含共享scope、全部本轮代表与既有有限消费者） | c5446b6570994d2898c9c60a16e51883 |

同目录各ID `.json`、`.json.log`、`.json.qualification.json` 保原始命令、产物、失败/原生断言与稳定资格。执行提交标签 bff556a 加本轮明确dirty；源指纹前后 `71DFF57B9EB5E343BE791DA8AFEA3B5164EB8BB8F8A92C4BB69556E24D852420`，可执行输入前后 `0768110381A5115826ED81860206A4612AFA330F487560524395AD6A83B3B9D7`，buildMatch=true。native STABLE 时间 `2026-10-21T17:33:08.2143225Z`，receipt SHA256 `1DF235D3E5AA41A294D78828CFD2975DF3997B7A178DB2A3098CD47C06CA1EA5`；后续提交不改原执行身份。

PrepareOnly 只签编译/结构。开发失败原件在 `.local/smart-fall-repair-20261022/` 与对应checkpoint保留：F1独立入口未初始化MountData、OFF对照误要求enabled scope、测试误读.8已改为key的旧identity字段均修准确fixture；PS7的null环境恢复生成空断言上下文造成输入漂移，改用一致Windows PowerShell宿主，未改全局/工具/产品；e206是补异Player真实prefix前的有效原执行，仍保原身份，c544补齐后不冒签旧覆盖。

成本只签有限方法和分母：OFF、ON闲置、ON原本免摔、ON危险无能力各20,000次更新及另20,000次分配窗，稳定新增来源扫描／计划／tile读均0、稳定线程分配0；自己的不变失败16次与旧容量不变64次无盲发，局部上侧变化1次重搜。Rocket原生等价动作预热4、正式24对，手动p50/p95 0.0012/0.0035ms，辅助0.0097/0.0365ms，线程分配两侧0，辅助24计划/768 tile读；不是FPS收益、完整救援耗时或所有路径零成本。

本轮自查核许可 finally／owner 与准确引用、原生斜面阶段、失败重开前早退、依赖容量/退出及临界事实最终门，评分与资源不改。移动合同／设计和短体验卡同次维护；四外来资产与原哈希一致不提交。集中独审与主准入、普通完整包和一次原目标正常换装尚在后续交付边界，动态实际身份回 #130。Main／图形、物理设备、真实好友房主/客机、FPS与所有者体验仍未测；历史范围外FAIL/NOT_RUN不改签，无Related/Full或全仓审计。

## 集中独审 R1：普通安全短落的制动成本

固定 `bff556a→a07bb238` 独审结论为 NEEDS_FIX，仅R1：48更新保守准入与旧的精确速度失效组合，使背包有合法来源的普通安全短落在原生每更新水平制动0.28时连续完整Contact。旧ON-safe仅已有免摔，不能覆盖；此前功能PASS保原身份，不代签此路径成本。原完整报告在 `.local/smart-fall-repair-20261022/independent-review/review-a07bb238.md`，未修改其结论。

返修保留48更新准入和慢钩准备，仅给已经实际查询到的普通、安全落差、平整非半砖接触增加有限连续支撑证明。普通步行的竖向到平面标量时间和原版run-speed上界圈定最多64格支撑行；当前重读成立时，无须因制动改动精确X曲线而完整Contact。危险接触、坐骑／可发动冲刺／滑轮／钩、坡／半砖、尺寸／方向／Down或支撑缺口／超界仍回精确路径；不持久化新数据、补资源或设重试定时器。较早碰撞只缩短已安全落差；新洞不能继续借旧接触证明安全。

新增真实32次下降更新、10次原生正速度反向制动的12格短落：合法马掌仍在背包、无已有免摔，来源扫描1、完整Contact1、tile读954、动作0，生命5000安全着地。954包含首次查询和随后有限支撑行核验，不声称安全运动无条件零读格。旧同场景没有实跑计数；不为对照额外跑旧包，旧路径重复查询的静态因果保留为独审结论，不伪造旧测量/FPS收益。支撑移除负例在当前整数距离仍安全时当次重探，对更低危险落点由原生被动保护实际免伤；原临界危险／安全邻点、gap25坐骑准备、当前安全且Delay>3的慢84钩、已成立F1—F4接缝均在同叶保留PASS。

R1最终仅新执行 `native-MovementSmartFallCpu`，批次 `artifacts/build/workload-checkpoints/ee4ba25604e04d3081bb99edc123646e/targeted-results.json`，execution `df297ceb7547442ab6adcf4390f2ca2a`，PASS/STABLE；同目录原receipt/log/qualification保留。实际执行为a07bb238加R1明确dirty；源指纹前后 `471855FD10AF0FAEE6832E88C664FCCDF254C0449AF0462E3C23E7BF0FD2D0E6`，输入前后 `60F0781BE9C34C1B0F21FF2696E3B63374ECCF53E76CA9652CFD47B8CCB82946`，buildMatch=true。STABLE时间 `2026-10-21T17:55:36.2602112Z`，receipt SHA256 `D493CB624A2EE4D79AD4EEB9C4134521ED744452CC9AB1302577727C3BA4BA05`。PrepareOnly仍只签编译/结构；两项未改纯层检查保留7163批次原执行和已成立审查，不改签为本轮重跑。首轮R1运行及补失效负例的运行均保原身份，最终普通步行适用边界冻结后沿本次原件签结果；无新范围或全PR重审。

自查覆盖新增支撑证明独立于旧精确X曲线的原因、最多64格/48标量步边界、真实支撑失效出口和危险慢钩正例；R1修订只涉及Host/Geometry/专属原生代表及本设计/证据。四外来资产长度/hash匹配，等待同一审查者R1增量复核与主准入，尚未最终包/安装，既有Main/FPS/联网/所有者未测边界保持。

## 五项修复最终普通包与一次同目标换装

初审有效结论、R1修订及E1身份机械校正的最终独审PASS，组合覆盖 `bff556a→4a5f8bee21b739fe3d0d3eb0edeaa10637ab5101`；主调度准入后，包与本机现装均来自该固定源码。本文随后追加交付记录，不改变该包的source commit，也不因此重测／重包／重装。普通完整ContinuousProcessing/Release/net472/x86保留全部现有功能及辅助瞄准；不是诊断包，`clean=false`只因已声明四外来非执行资产，产品变更已提交，资格仍INCOMPLETE/releaseEligible=false。

普通完整ZIP：`outputs/smart-fall-repair-20261022-4a5f8be/JueMingR-Continuous-Processing-4a5f8bee21b739fe3d0d3eb0edeaa10637ab5101.zip`，2,351,886字节，SHA256 `2386B9E2827E7A3BDE6E784C014701FB7BEC3F57384B9D08519E8EC12D475504`。同目录 `phase-0-s-build-record.json` SHA256 `3C84FDE1791FBEE0AD88068A5D8134D1A43FE094DC08A589A335BC4B3D2F6C90`；正式构建器已核完整17文件包树/ZIP及10载荷、源码/Release身份。PrepareOnly和当前native原执行有效复用后，经现有候选职责记录／StageDevelopmentRecord打包；207项目录保留范围外 `native-NpcSkeletonBoss` 原FAIL与94项未执行，不裸跑Related/Full，不补历史测试树。最终必要native仍为df297原身份，另两项本轮纯层检查及其它历史原件不改签新SHA实跑。

同一已确认目标：先核旧bff556a source、原receipt SHA256 `65BE87971AE7E6C8B489E177F3473CF790295985B043451150F795B71FBC1B43`、准确受控owner及无活动游戏/server/worker；备份12项（10载荷、旧receipt、已有evidence日志）。旧包正式Restore exit0/RESTORE_COMPLETE，新包Install exit0/INSTALL_COMPLETE，一次正式owner/payload/receipt读回PASS，完成时间2026-10-22T02:08:46+08:00。新receipt SHA256 `7686326849533952D9D9C917C216205067C34C1198C020CC0E62337794B5E3C3`；本地结果 `.local/smart-fall-repair-20261022/smart-fall-install-4a5f8be-result.json`，轻量备份 `smart-fall-backup-4a5f8be-20261022T020843/backup.json` SHA256 `ED0C669D91BC8C5659CA9411F3DE29CC5B51A3346F09B6629420004B9C6DF256`。原bff包、旧受控备份、正式Restore/Install恢复材料保留，没有回装演练或第二轮安装审计。

回执的 `installationAuthorized=false` 沿用候选资格字段，表示候选记录本身不自动授予安装权；本次实际安装依据所有者20261022同目标明确委托及主调度准入，与回执 `installationAuthority` 分列，不改原工具回执。没有启动／操作真实游戏、角色、世界、存档或强杀进程，JueMingRData未触碰；包、文件安装不代签完整Main／图形／物理设备／FPS／真实好友房主客机或所有者体验。当前停止自主开发，等待所有者按包内短卡体验及网页复审；Draft/open保持，不合并／关闭／Tag／Release／版本推进。
## 2026-10-22 社交装备闭环纠正 checkpoint

起点 b816b5f346fa5869f32bd5acb3e8bed60d240ead；同分支 codex/smart-fall-protection。本次完整委托为 references/JMR_SMART_FALL_SOCIAL_ACCESSORY_FIX_v1/ 四份原件，有限替代旧 C1 对真实临借三类激活的绝对禁止；模拟连跳不补资源。

social_integrator 是唯一实施作者，独占本包生产/直接检查/合同设计/evidence、公共构建、Git/GitHub 及准入后的完整包/同目标安装。四项既有外来资产保持且不提交。实际子会话 rollout-2026-10-22T02-48-56-01a15fcb-870e-7392-90ba-8753a1440a88.jsonl 已由主核 gpt-6.1-sol/high。

当前已确认选材→真实临借→原版装备选项与资源脱节；尚未签发修复PASS。下一步固定七bit自然周期账本、真实loan/token绑定激活、七机制真实消费者与反滥用验证；候选冻结后集中独审，准入前不正式打包安装。既有4a5f8bee包/安装及历史FAIL/NOT_RUN保原身份。
### 社交闭环本轮实现与冻结证据（2026-10-22）

本轮起点 b816b5f346fa5869f32bd5acb3e8bed60d240ead；原生产/现装4a5f8bee不回滚。旧C1“普通社交瓶永远不补能力”由所有者最新完整委托有限替代。根因是候选仅接收已有unused、火箭/飞毯资源门在真实临借前拒绝自然空资源；原版换装只建立能力option，不在空中补起动机会，旧SocialBottleNoCharge又以背包马掌代救接受错误结果。

实现将五族静态映射独立，优先使用真实原版余量，再在真实合法借用后激活一族二跳、一个原生火箭点火单位或一次canCarpet起动。BorrowOwner冻结交换前有效装备族；消费者前复核准确Player/generation/token、两端引用/关键值及合法有效槽。七bit账本记录真实消费/授予，以原版RefreshDoubleJumps只读事件清周期；归还、开关、配装、短暂安全均不清，OFF/首次开启保留原生消费事实。未消费异常仅撤同周期且仍有所有权的贡献，保留原异常和已耗额度。没有主动全刷新、手写option/翼时/carpetTime/运动/fallStart/生命/免伤。

独立原版社交换装反例确认Restore直接逆交换的互斥风险：功能Hermes临借后停放到背包，玩家通过原版SwapVanityEquip在另一未管功能槽合法穿上另一Hermes，旧Restore会再装回互斥原装。原始FAIL见本轮targeted4.log与其checkpoint，未改署。现直接归还也核CanSwap，冲突则保留真实三件对象和favorite、保住玩家合法新装备并以TakenOver退owner；未改共享CanSwap。普通归还仍精确回端点。

最终精确执行batch `e354104a27d34b1b9e891c129f799db0`：SmartFallBorrowOwnershipChecks `5c88b07520964c8282c51dbed60a1a8d`、SmartFallRiskActionsChecks `c38c839319974d639fa83813fc697004`、native-MovementSmartFallCpu `c5e2ee53f8f848c2aca3d6f0dbc418e9`、native-MovementJumpCpu `f7f234c6f80f40b2b004d0d037f4a096`，四叶实际PASS。原件在 `artifacts/build/workload-checkpoints/e354104a27d34b1b9e891c129f799db0/`；日志 `.local/smart-fall-social-20261022/targeted-final.log`。源码前后 `F5F1A474ABF9081CB065F73977FB83036BD6EB483BA94A968E498B117CFD8561`、输入前后 `E3F5A59AE254B1F5F8989D4A7DA693E2B3E45EF59DEACE4B0797987EAF2072FA`一致，buildMatch=true。仅后续本段证据说明改变时沿正式reader复核适用，不重签上述原件。

真实消费者覆盖：三不同族普通社交瓶（无其他救援装备代救）、背包瓶；空燃料社交火箭、空起动社交飞毯；零翼时翅膀、马掌组合5331、重力球去回、神灯腿12→2。均选中真实源、登记/交换、正常UpdateEquips、原版消费者、实际接触无伤、精确所有引用及prefix/stack/favorite归还；独立关闭帮助原版Hurt对照真实掉血。五族及升级/组合全映射表；耗尽功能Cloud和已记账社交Cloud不遮后方Sand，Bundle只消费剩余一族且不重开已用族；真实自然恢复、全部关闭时原生消费/刷新、首次开启保留历史、clone隔离；原消费者异常可见、未用贡献撤销和32个稳定更新不重开。原三功能Jump相邻消费者1037案例及84个wire案例保持通过。原飞骑/背包钩/左右斜平台/失败钩代表在同叶保持。

工作量（同Windows/.NET x86隔离CPU环境，与上一轮同入口比较）：OFF、ON-idle、ON-safe、ON-danger-no-capability各20000次，稳定新增来源/计划/tile均0、steady线程分配0；OFF分别约1.96ms、ON无能力约9.36ms，仅helper窗口非FPS。关闭期间原生消费/刷新单独证明来源/geometry扫描0。一次资格/原版恢复按revision有界重开，失败32更新动作重试0；安全制动32实际更新仍1来源扫描/1完整Contact/954tile/0动作，未扩大地形或pool扫描。激活后还装完成无新增持续借用工作。没有真实FPS收益声明。

第一轮失败中的“落地后wingTime必须仍0”是测试误把原版落地恢复当补充；改为落地前正常装备翼时仍0。周期夹具复置相同位置/速度却推进tick违反接触缓存连续性，改为明确新X场景触发现有失效；未改生产缓存。真实失败、旧aggregate/范围外FAIL/NOT_RUN和未完成资格保留。完整Main、图形、物理设备、真实好友主客机、真实FPS及所有者体验仍未测。此处不表示独审/普通完整包/新安装已完成；候选冻结后集中独审，再按已授权流程交付。
### 社交闭环最终交付：独审、普通完整包与同目标换装（2026-10-22）

本包 `b816b5f → 3cffcbcbfc9144c8d3b9b73f9470838e99d813e0` 的12文件增量及直接传播，经未参与实施/期待起草的 social_reviewer（实际gpt-6.1-sol/high已核）集中独审PASS，主调度准入。原报告 `.local/smart-fall-social-20261022/independent-review.md`，SHA256 `DFAEBCCF224EE3AFF96AF96CB8C38176FAACC17F50A105CB35EEC16A69DC1EAE`。无需要本包返修的确认阻塞或证据缺口，未额外扩验证。

四叶原执行和其STABLE资格保原身份，提交后正式reader四项applicable=true/buildMatch=true；本次普通完整候选记录 `.local/smart-fall-social-20261022/candidate-final-3cffcbc/development-candidate.json`，207项目录中4项必要责任PASS，1范围外历史FAIL及94未执行保持，fullQualification=INCOMPLETE、releaseEligible=false。StageDevelopmentRecord不授予安装权；实际安装权来自所有者社交闭环明确委托及主对固定3cffcbc准入。

普通完整ContinuousProcessing / Release/net472/x86包已生成，全部既有功能及辅助瞄准保留，17文件/10安装载荷。包/Release/本次现装source均为 `3cffcbcbfc9144c8d3b9b73f9470838e99d813e0`；后续纯交付记录HEAD不冒称新载荷。ZIP位于 `outputs/smart-fall-social-20261022-3cffcbc/JueMingR-Continuous-Processing-3cffcbcbfc9144c8d3b9b73f9470838e99d813e0.zip`，2,354,929字节，SHA256 `CACA0E84F5545AC3FB7219B06768A9DDE9157005BCB21DF8868F47B5B772E957`。同目录正式 `phase-0-s-build-record.json` SHA256 `9D682187653B25ABA97E77D7D869DFD717365C488E9A81AC3F01898CD001B3AF`。构建器核匹配来源、完整目录与ZIP成员/字节；clean=false仅四声明外来非执行资产，产品已提交。

同一已确认F盘Terraria目标先核真实旧4a5f8bee receipt、owner/受控载荷、准确游戏身份和无活动游戏/server/worker；12项旧受控文件轻量备份后，官方旧Restore exit0/RESTORE_COMPLETE → 新Install exit0/INSTALL_COMPLETE → 一次正式owner/payload/receipt读回PASS，完成2026-10-22T03:27:15+08:00。旧receipt `7686326849533952D9D9C917C216205067C34C1198C020CC0E62337794B5E3C3`；新receipt `754BDEB8F6B6D5A4D2E063C7FA92C58CDDBB17F114E495F4125696D3C48ECA2F`。备份 `.local/smart-fall-social-20261022/smart-fall-backup-3cffcbc-20261022T032712/backup.json`，SHA256 `3B1897B71CB83A2615E9CC7BC7693294347E70DA75B5DC605AE7ED5612F821A9`；结果 `smart-fall-install-3cffcbc-result.json` 与正式Restore/Install日志在本轮.local根。恢复入口是新完整包的Restore；如所有者后续要求回旧4a，先新Restore再旧4a包Install，本轮未执行回装。

没有启动/操作真实游戏、角色或世界/存档，没有改JueMingRData，没有强杀、回装演练、第二轮独立安装审计或全数据扫描。实际安装读回不等于正常完整Main启动、图形/物理设备/FPS、真实好友房主/客机或所有者体验，这些保持未测；隔离原版消费者和准确引用归还证据已完成。短体验卡保留三步原场景、有限周期与正常恢复；已授权必要工作收口，Draft/OPEN保持，不merge/close/Tag/Release/version bump。

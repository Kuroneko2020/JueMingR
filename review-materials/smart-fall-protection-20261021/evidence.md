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


## 2026-10-22 原版能力与合法操作修复候选

本次唯一任务仍为 #130 / Draft #131，同分支起点 `5df866f7a8040d047d3f1b2e2ee54a2ad5c8e1c7`；完整产品 BASE 仍为 `62bd6e4ef42eacd7a22b898ee538650a7b920747`。所有者原件 `references/JMR_SMART_FALL_VANILLA_ACTION_FIX_20261022_v1.zip`（ZIP SHA256 `BEC582454AC83F3748F63CF8E4B43FFB4F88F29AEEEE975134BE212992A4351A`）已检查全部成员无根路径／父目录越界，在新的 `.local/smart-fall-vanilla-action-fix-20261022-154207/JMR_SMART_FALL_VANILLA_ACTION_FIX_20261022_v1/` 解压，README 与00／01／02全文读取，原件不改。主核实施会话 `01a1628d-395e-7763-9792-48bea90363a0` 为 gpt-6.1-sol / high；一个实施者承担所有本轮写入，独审尚待未参与实现／期待的上下文。

本次明确纠正：自动化只能获得相同初态下原版手动合法操作能够取得的能力。前节“有限激活一次／周期账本／OFF观察”与其 PASS 期待是已撤销政策的历史；不是本次 PASS，不再解释为用户曾批准造资源。`3cffcbc` 引入 PrepareResource 三条授予、CanGrant 候选兜底及七bit账本，测试又以“无预充社交瓶必须救人”为成功，因而未对照原版空中换装本身不产生资格。更早 `ba096176` 已引入 Jump／Rocket／Up release 字段直接置真，以及原坐骑按类型 CanMount→SetMount；旧检查覆盖消费或新坐骑接管，却没有同初态持续按住必须先真实 Release 的对照，也没有 CCed／物品缺失等完整重新激活门禁。当前按行为删除与修复，保留3cff中正反向CanSwap归还冲突修复，不整提交回退。

实现：移除 SmartFallResourceCycle、专属 RefreshDoubleJumps hook／reader、AllowsActivation／OriginalResources、SetJump／EffectiveResources、虚拟候选额度及贡献撤回。二跳只认真实 unused 匹配族；火箭认真实 fuel 或真正 empressBrooch 原生补给；飞毯认 canCarpet 或实际残余 carpetTime。资源消费不因取消／还物恢复，未消费资格不被撤销。固定 readiness 布尔使一次真实余量出现重新核来源，倒计时／release／pending 不逐帧扫库存。马掌组合、零时间翼被动免摔、腿12→2及原有真实借还／guard lease 保留。

Jump／Rocket 需要重武装时，动作局部 Release 预留共享 owner，既有十一处有效 Jump 读取保持到整更新结束；原版消费者自己写 release，再在下一合法 Press 消费。已经 rearm 直接 Press；Carpet 原版只需 held Jump，未人为增加 release 门；上升／jump tail 不截断。Up 两次 Press 中间经历完整 UpdateControlHolds 释放更新，forcedGravity／手动 Up 退出旧意图。原坐骑借入前记录真实原物与选择，精确还物后再做一次真实 QuickMount，完整原生条件继续决定结果；无空间、权限、缺物、同type新物、选槽／新骑乘接管都不会反复复活。默认矿车只使用原生 rail-hook fallback，不直接 SetMount。

### 实际执行与可判别证据

最终必要叶以 Windows PowerShell 5 正式准备和既有精确 runner 执行，batch `7166c71d87f9490dbf995796576dfd31`，原结果 `artifacts/build/workload-checkpoints/7166c71d87f9490dbf995796576dfd31/targeted-results.json`，日志 `.local/smart-fall-vanilla-20261022/targeted-freeze.log`。这批运行发生于起点 HEAD 加当前未提交修复，原执行身份不改署后继提交；提交后须由正式 reader 再核适用，不重签实跑。

| 责任 | 原 executionId | 实际耗时 | 结论 |
| --- | --- | --- | --- |
| SmartFallBorrowOwnershipChecks | 28739f047fb24287bad149ec5947ea1a | 467 ms | EXECUTED / PASS |
| SmartFallRiskActionsChecks | fb56b74d626d4a719eb5550ef32d3b80 | 321 ms | EXECUTED / PASS |
| native-MovementSmartFallCpu | b3a3d4fe407f471bb603ed31f866740e | 13360 ms | EXECUTED / PASS |
| native-MovementJumpCpu | 902ed0a4b6164b4485f3ce231a49433e | 12803 ms | EXECUTED / PASS |

判别内容：社交／背包的普通Cloud、空火箭、空飞毯六反例均不借物、不额外动作；独立 ItemSlot 原生手动换装→装备／eligibility／motion也不能产生机会。真实 fuel 与 `canCarpet=false/carpetTime>0` 从稳定无解缓存变化后，各只重核一次来源并被原生消费；真实 empressBrooch 补给仍可用。原功能装备unused跳、正资源翼靴／毯、无次数马掌组合及零翼时翼走原有真实伤害／保护链与精确还物；关闭帮助的原版落地确实损血。Jump与Rocket未rearm的手动 Release→Press和辅助二更新轨迹一致，Release更新无早跳／无下游点火；已rearm同一合法更新消费，无统一等待。另核Carpet不需release、上升tail不断、OFF取消真实loan不补／撤fuel。球首翻后完整松Up更新、第三更新返回；forcedGravity／手动Up退出旧意图。普通原坐骑合法QuickMount有正例，CCed／tongued／反重力／dead／noItems／缺物／同type新物／手动选槽／无空间拒绝且后续不复活；默认矿车产真实403 rail-hook、不伪称已挂载。真实借还的引用／prefix／stack／favorite、第二写故障、反向CanSwap冲突、一次native-use lease、背包钩及飞骑左右斜平台均仍由同native叶覆盖。nativeJump的1037个原版Jump／flight阶段代表及84个wire状态保原窄结论，不代签全面连跳重审。

没有对旧包另跑同一新fixture；旧实现会被无资源原生状态、Release首更新无动作及坐骑门禁断言击穿，依据当前新增断言、旧静态赋值与旧交付“空资源被激活”的原始记录，不虚称实际A/B重跑。

### 工作量、原始失败与边界

上一稳定批 `2b78ae7d8f96480e9198e11b4f718a3f` 四路径各20,000次：OFF／ON-idle／ON-safe／ON-danger-no-capability耗时1.7676／3.5867／4.7148／9.7429ms，稳态当前线程分配0、新source／plan／tile工作0；最终冻结批原始日志另保对应实际数值；以上是有限helper工作量和该机局部计时，非完整Hook、FPS或速度提升声明。Rocket首点火24对加4预热：manual p50/p95=.0012/.003ms、assisted=.010/.019ms，两侧测量线程分配总0；真实动作／资源匹配。稳定空资源八更新每初态只一次source scan；释放／按下不重新规划来源。原生正常短落32更新／10制动、source1／complete Contact1／tile954／动作0及支撑移除恢复保留。源码删除OFF账本工作是窄结构事实，不外推整机性能。

早期 `prepare1.log` 编译通过但结构读取已删除、尚在Git索引的旧Cycle路径失败，已准确暂存本任务删除；未修改结构检查。`targeted1.log` 新fixture给只读selectedItem赋值导致Probe编译失败，已改真实SelectedItemState.Select。`targeted2.log` 保留三个新反例FAIL：真fuel正例选到原生来不及的5px窗口、DoubleJump释放例初25px尚未到发动窗口、选槽事件未立即退休原坐骑意图；前两项按原生Delay／Due校准为及时窗口，后一项修生产事件收尾。PowerShell7该次source稳定但input／buildMatch漂移，不可用作资格；沿仓库前轮已用WindowsPS5后最终source/input前后相等、buildMatch=true，原失败不改签。后续`targeted3/4.log`及最终原记录保留，不弱化原生断言。

完整Main／图形／物理设备输入／真实好友房主客机／FPS／owner体验均未测，clone身份和message5编码／发送只证明其自身隔离，不是服务器ACK。未Related／Full、全仓／全功能／全NPC扩扫；外来四资产未改未提交。当前仅源码候选／定向证据；尚未完成本轮独审准入、普通完整新包或实际安装。下一步一次集中独审；通过及主准入后由原整合者完成普通完整Release/net472/x86 ContinuousProcessing包、原F盘目标受控备份／官方Restore→Install／一次必要读回，分别回填真实包和安装身份。Draft保持，不merge／close／Tag／Release／升版，INCOMPLETE／releaseEligible=false。


## 2026-10-22 集中独审 R1／R2 有限返修候选

首次集中独审对 `5df866f→cd4be2c` 完整增量结论 NEEDS_FIX，两项 P2 为当前直接退出／接管边界，原报告 `.local/smart-fall-vanilla-20261022/independent-review.md` SHA256 `3F73C9E10E03E703996C11C4F2824F556A1812BC1130CE41CA04A374BB4E3A28` 及 `reviewer-probe/probe3.log`、前两次独立fixture启动失败均原样保留。本次只修这两项和直接传播，不重新全审。

R1原因：新增的 Release pending 用 releaseTick／jumpPressed 区分正常等待，但 EndPlayer 未把原异常交给该动作，故未完成的异常更新仍可保留真实借用。现在真实 Player.Update 最终器沿 EndUpdateAfterNative 将同一个原异常传给唯一 Session／FallHost；有待收尾动作时先 Failed／撤意图并请求精确还物，再关闭本scope，真实资源不补／不撤。正常 EndUpdate 保原出口；最终器仍归还 Jump／Dash，并以原异常为准。关键就地注释解释异常 Release 不等于已完成等待。

R2原因：手动Up接管原只位于 returnGravity 分支，尚未首次Press的球动作可能把玩家的原生翻转记作自身Consumed。现在整个未结束球动作入口都先识别实际用户Up（区别本scope自己的upOwned）或forcedGravity并退休旧动作／返回意图、准确归还借用，玩家输入与原生结果保留；首次Press前和翻转后遵循同一规则。未采用可选自然归还球优化。

先在生产仍为 cd4be2c、仅加入正式新反例的状态实际运行红例：batch `bdffd446483742b0866aba49a3beb543`，原始 `.local/smart-fall-vanilla-20261022/r1-r2-red.log` 与对应checkpoint保留。Release异常明确断言采到 action=True/pending=True，球首次准备期手动Up未退休；已rearm Press异常通过。该runner原始分类为 HostException，内层两个定向断言／原生状态按上述独立边界确认，不改写成协议BusinessAssertion或成功。不是仅静态推断旧候选会失败。

修后原生绿例 `0d28ec67e3a047cf90563372211da481` 保原身份；最终加入已原装球同初态代表后，仅重跑受本轮传播的两叶，batch `a6ee9ba0a0ab413eb8ef9460063a42e1`：

| 责任 | executionId | 结果 |
| --- | --- | --- |
| native-MovementSmartFallCpu | 74dcda4008414439a9de6f268594706f | EXECUTED / PASS / STABLE |
| native-MovementJumpCpu | 645942d30e48404ab6af21fe528f6e35 | EXECUTED / PASS / STABLE |

日志 `.local/smart-fall-vanilla-20261022/r1-r2-final.log` 与该batch/targeted-results.json，source/input前后稳定、buildMatch=true。原执行sourceCommit为cd4be2c加当前未提交返修，不改署后继SHA。生产／期待未改的Borrow/Risk先保原身份并尝试正式复用；提交后reader对共享变化投影实际拒绝（两项applicable=false），因此仅补跑这两个既有精确廉价叶，没有改reader或期待。batch `d46fb70b0eb94a3c88760443a1ad731c` 实际PASS/STABLE：Borrow `7535667961df497dbfef0a23fc06ed11`、Risk `f8eebe06d8a04e0a814eaa96507c9793`，实际sourceCommit `7472fc978295fedb0bb0d5da1bdbce67549c74e4`。机器读回 `r1-r2-proof-final.log`／`proof-applicability.json` 确认当前四项applicable=true、buildMatch=true；原纯叶身份仅留历史，不冒称旧证明当前可复用。两native保上述实际执行ID，未重跑Related/Full。

R1新回归通过真实 BeginWorld、Player.Update BeginPlayer/EndPlayer 自动最终器状态：只在既有Motion片段省略的输入／装备接点补真实CaptureInput／ResetEffects／UpdateEquips，再在JumpMovement最后优先级抛原异常。Press和未rearm Release均保同一异常对象、动作／输入／真实loan精确收尾、fuel保持3，引用回原槽，下一scope不推进旧意图。R2新回归含真实借入社交球和已原装球：首次releaseUp=false→原生松开rearm→用户Up即TakenOver、借球准确归还；通过真实vanity swap保持同一球持续功能装备后，玩家原生翻转保持-1，后续三个正常松键更新无辅助Up／强翻回。既有正常三更新往返、翻后手动／forced接管、合法Jump/Rocket Release→Press、原生资源和实际保护／借还／钩／斜平台代表保留PASS。相邻nativeJump原版1037／wire84保持原窄范围。

当前新增仅有限字段／异常参数传递与动作入口O(1)接管检查，无新扫描／框架／资源写入／日志。既有稳定工作量在本native叶继续通过，不外推FPS。源码／定向证据不等于本轮独立增量复核、主准入、新包或安装；尚未包／装。四外来资产保持，Draft／OPEN／INCOMPLETE／releaseEligible=false，不merge／close／Tag／Release／升版或操作真实游戏。下一步仅同一审查上下文增量复核与主准入，通过后原整合者继续已授权完整包和同目标备份换装。


## 2026-10-22 原版边界修复最终交付：普通完整包与原目标换装

产品、普通完整包、Release及实际现装source均为 `632b90499a858c791074c686cf2a5113a9f00ece`，后继纯交付记录提交不改变包或现装身份。本轮 `5df866f→632b904` 经初审完整增量＋R1/R2有限返修独立复核组合PASS，主调度固定候选整体准入；最终独审 `.local/smart-fall-vanilla-20261022/independent-review-r1-r2.md` SHA256 `0FD64E2DBB7A65024C41DA612892203AB60EEDE98F3B0986013460F425310FAF`。初审NEEDS_FIX报告、独立失败探针、正式红/绿及所有原执行身份保留。

沿现有 DevelopmentCandidate／StageDevelopmentRecord 正式reader生成 `.local/smart-fall-vanilla-20261022/candidate-final-632b904/development-candidate.json`，完整产品BASE62bd6e4、实际207目录、四必要责任适用；两native来自a6ee9ba0、两纯叶来自d46fb70b，保各sourceCommit/执行ID。207目录范围外原1 FAIL（native-NpcSkeletonBoss）和94 NOT_RUN保留，其余历史成绩不重签当前全绿。任务验收PASS、fullQualification=INCOMPLETE、releaseEligible=false、installationAuthorized=false保持；安装权来自所有者本轮同目标明确授权及主632b904整体准入，记录自身不授予权限。

实际执行普通完整 `scripts/build-phase0s-validation-package.ps1 -Profile ContinuousProcessing -StageDevelopmentRecord <上述record> -ApprovedRetainedAssets .local/smart-fall-protection-20261021/retained-assets.json -OutputDirectory outputs/smart-fall-vanilla-20261022-632b904`，成功生成Release/net472/x86，SDK `10.0.203`，完整17成员／10安装载荷，包含全部既有功能及辅助瞄准，非诊断包或DLL补丁。新ZIP `outputs/smart-fall-vanilla-20261022-632b904/JueMingR-Continuous-Processing-632b90499a858c791074c686cf2a5113a9f00ece.zip`，**2,353,049字节**，SHA256 `5F8BF50B7E5494CB50DAB9F0A78E307838B05882D6252D07DE2826AA3A4622DD`。同目录 `phase-0-s-build-record.json` SHA256 `B50830C7B76233FE78776FF743BF56286EBB7377C5D83289EAB5037A73165DBF`；普通payload、manifest、依赖和实际Release身份沿正式构建／包读取器核验。clean=false仅四声明外来资产，产品已提交；没有Related/Full或无关历史维修。

同一已确认F盘Terraria目标现场核准确旧3cffcbc包、旧receipt SHA256 `754BDEB8F6B6D5A4D2E063C7FA92C58CDDBB17F114E495F4125696D3C48ECA2F`、owner／payload、原版EXE身份及无Terraria／server／PredictionWorker进程。12项旧受控文件轻量备份至 `.local/smart-fall-vanilla-20261022/smart-fall-backup-632b904-20261022T170805/`，每项复制后读回，backup.json SHA256 `D9D52CF24F70519D4DCE30EF84BA155B1A8E872F60D347CF73F9102332BD7E40`。随后官方旧Restore exit0／RESTORE_COMPLETE→新Install exit0／INSTALL_COMPLETE，一次正式owner／10载荷／receipt读回PASS。实际完成 `2026-10-22T17:08:07.7568259+08:00`，新receipt SHA256 `A7E426310E943DD81EE077A23DB341EDE38A94CE9BC0AB1C79CB6FF34786206B`；结果 `.local/smart-fall-vanilla-20261022/smart-fall-install-632b904-result.json`，两原生日志及install-execution.log同目录。

恢复入口为新完整包的 `Restore-Phase0S.ps1`；若明确需要回到前包，先新Restore，再旧 `outputs/smart-fall-social-20261022-3cffcbc/JueMingR-Continuous-Processing-3cffcbcbfc9144c8d3b9b73f9470838e99d813e0/Install-Phase0S.ps1`。本次未回装演练、第二轮独立安装审计或全用户目录哈希；没有启动／操作所有者真实游戏、存档或JueMingRData，没有强杀／修改系统音量。保留新安装供所有者体验，不默认卸载。

源码已撤造资源、保合法能力和真实物品借还；新自动证据为原生组件／输入／身份／有限工作量，不代替普通新包由所有者启动。完整Main／图形／物理设备输入／真实好友房主客机／FPS／owner体验仍未测。包／安装身份与下一次实际体验分别记录，不能因文件安装PASS宣称产品已验收。短体验卡为普通包内及仓库 `scripts/phase0s/Continuous-Processing-Owner-Test-Card.zh-CN.md`：先自行确认能进入/F5打开/正常退出，再用可丢弃角色世界分别看无资源瓶不造次数、明确合法资源或被动保护及归还、球／坐骑原生恢复与手动接管。

本轮已授权交付工作完成，停止自主开发并等待所有者短卡体验／具体反馈。Issue130与Draft131保持OPEN/Draft，#7保持INCOMPLETE/releaseEligible=false；不merge／close／Tag／Release／升版，不启动下一功能，四外来资产原样保留未提交。

## 2026-10-22 防摔性能、重铸空epoch与合法操作原则续修候选

本次所有者新委托原件为 `references/JMR_FALL_PERF_REFORGE_FIX_20261022_v1/`，README及00—04全文读取、六份SHA与SHA256SUMS一致，原件只读持久保留，不入Git。本轮明确替代上节等待体验的停止状态，沿同Issue130/Draft131/分支续修；研究锚点与本轮增量基线为 `13107fbd131856dc2c2d9bd21916189efd6e7982`，产品原现装仍为632b904。任务完整产品BASE继续62bd6e4，不把最后记录提交当新的已装产品。

主调度Astra/Max只读；Processing实施整合者Sol/High独占Processing四生产文件、直接检查、共享layout fixture、原则/Processing文档、公共构建/Git/正式证据；SmartFall实施者Sol/High独占SmartFallHost/Geometry、MovementControls、SmartFall/MovementUi检查及移动合同设计。模型均核实际turn_context；整合者01a16321-81ce-7913-85da-290f2c7b7aab、SmartFall01a16322-1295-7eb2-bb5e-32864a0e511f，model=gpt-6.1-sol/effort=high；未改全局配置。最小checkpoint、原SHA核验与文件责任在 `.local/fall-perf-reforge-20261022/`。四既有外来资产原样保留不提交。

重铸同因链：首次实现ed294fa（主线squash b873fbe）已把真实按下/意图与CanStartActions合用，quote及press按outer Frame计龄；后继be72de加自动执行世界步门时未分离存续。原版1.4.5.8 Main.DoUpdate在输入前早退，宿主BeginUpdate仍清mapped/finalized，postfix业务仍到达；空epoch便错误造down或撤销owned/pressReady，OwnPress/手动收费仍被执行门否决，外层Frame又使合法报价/按下老化。旧Sample每次AfterMapping/AfterKeyboardRefresh，只漏Draw的“跳帧”无法覆盖。只定向核当前Main早退、DrawInventory按钮门/移出清cooldown及原Roll，不重做历史调查。

现在HostProcessing将共同真实安全上下文与新手势、前台保留、既有后台意图、自动执行分开；无全局放宽Admit或Runtime早退，无公共Input owner修改。重铸仅可靠样本可建按下/认可松手，报价/原按下改Mapping时钟；空Draw可记报价/确认旧press，原版手动单次在合法按钮release/cooldown内可消费，不套自动world-step/通用paused门。紧贴BuyItem前消费manual/quote防重；自动重复仍世界步、真实BuyItem金额/原Roll/命中tail/unknown/窄Items许可。原版移出Draw清cooldown再合法松开/按下的有限消费者序列保留，不因单一外层cooldown差异推断约定节奏不可达，未写release/cooldown。

开袋Holding/ProtectIntent在空epoch保已有袋/钥匙及claimed尾部，真正新认领/开奖仍可靠Shift右键+世界步；下游实际公平序列与未知范围原检查保留。提炼仅Update存续改保留准入并重验真实材料/已选机器，消费入口不放宽；空epoch保选槽，后台支持保留，新玩家选择/材料机器失效/更高所有者照常收尾，不夺新选择。

SmartFall两根因由ba096176269e047a776788a44c527cf632f52077首次实现引入：OppositeClear只消费前2tick却计算固定48；退骑失败只以位置/库存revision唤醒，漏空间及CCed独立恢复。Contact可选horizon默认48，Opposite显式2，复用原几何，1/2（含恰好2）拒绝、3/20/48空正反边界覆盖。首次真实退骑时机不变；失败后以真实本机Player tick去重原版CanDismount资格，五scratch仅额外观察try/finally隔离，资格通过才一次TryDismount。仅DontDismountWhenCCed集合可CCed常数等待；OFF仍归还，原坐骑一次性恢复不复活，手动/死亡/世代清观察，真实还物仍守现有dead保护。

统一合法操作/TAS原则进入Legacy规则基本原则，AGENTS功能底线和网页Chat第3节只引用/明示提醒责任；移动与Processing合同设计同步。原则当前依用户委托适用，合并main后生效，不另造审批。防摔许可入口仅源码 `// "配置"`，有效按钮开/关/键及原命令、ConfigurationExpanded/十二许可/持久化保留；既有保存值不重置。

### 本轮实际定向证据

PrepareOnly/现有精确叶，不运行裸Related/Full。最终批 `77c68db6a2724b4bb0b7334dc9ab587b` 原始sourceCommit仍13107fb加当时未提交的本轮增量，不改署后继SHA；source/input前后相等、buildMatch=true。六必要叶：

| 检查 | 原 executionId | 结果 |
| --- | --- | --- |
| ProcessingChecks | a8bbe53f4c294d0bb090e0d722d6f78e | EXECUTED / PASS / STABLE |
| native-ProcessingCpu | f9555ee46cc8474ea5b4375335f5f32b | EXECUTED / PASS / STABLE |
| SmartFallRiskActionsChecks | c78181429cc04fdc8b0d15e144403895 | REUSED / PASS，原731c1149批的真实纯叶 |
| native-MovementSmartFallCpu | b601f576528a406ba744de25aac53ad6 | EXECUTED / PASS / STABLE |
| fixture-movement-layout | 01b8bf8cdb144f01a8c50749734c2271 | EXECUTED / PASS / STABLE |
| SmartFallBorrowOwnershipChecks | a3d8c9a353ff41458e4d49d54c14c0b9 | EXECUTED / PASS / STABLE |

`EmptyOuterUpdate`只真实BeginUpdate，不AfterNativeMouse/Mapping/Keyboard，不推进GUC，先断言新动作/自动执行false、保留true后走HostProcessing.Update。R1首按/空样本不造边沿；R2空Draw首次报价下一world step一次消费；R3已真实付费未命中owned插空保持/零重复收费Roll/下步继续；R4物品prefix/value/stack/discount/adjustment/scale/viewport及真实Mapping超龄反例；R5初始命中可靠按下后空epoch（含paused）单次真实BuyItem/原Roll、同hold零第二次；R6空epoch的菜单/换物/失焦撤销并防旧意图复活。CPU只供应报价/手动接缝，不冒称完整Draw。开袋两组核新来源0、已有来源/保护/claimed保持、实际消费、输入接管/松手撤销与公平；提炼两组核真实消费产物、前后台Active空epoch保持0新消费/产物/归还、新选择优先，既有原生机器失效/16输入双机器/52手动交易反例继续PASS。

现有 `ProcessingVisual` 精确专项实际PASS，日志 `.local/fall-perf-reforge-20261022/processing-visual1.log`、图像/原build/input在同名目录。真实原版DrawInventory覆盖空Update/空Draw首次报价→下一世界步一次自动消费，以及已匹配真实press→多个只BeginUpdate→真实Draw恰好一次手动付款/原Roll、重复Draw零第二次；保留150%报价坐标、top-tier自动与原版手动冷却及F5现有三个视口。隔离XNA/原版组件证据不等于所有者物理输入实机。

SmartFall实际危险、有球尚未Due的Capture每pass96次：原horizon48反向访问88704，短2访问3648，native-actions=0；Plans不是该分母，不称24倍整功能或FPS。真实5597背包借入Bat56、低顶8tick资格观察8/真实失败动作0，同tick3收尾入口合计1；只开空间/只解CCed各下一tick退骑归还一次，非Dont普通12在CCed不一刀拒绝。成功后0观察、OFF无责任0、手动新骑/死亡/世代清理、五scratch保持及原生真实动作scratch正对照、旧原骑空间开放后不复活、旧飞骑斜平台均PASS。正常已实例化场景有界计数，不称null Tile绝对零分配。既有四路径各20000次稳态0新扫描/plan/tile/线程分配保持原窄意义。

原失败保留：targeted1新增fixture误给只读Main.npcShop赋值；targeted2误用FocusHelper命名空间，均仅编译失败并按真实API窄修。targeted3的SmartFall PendingMountRecovery第二case没重新开主功能，Begin正确拒绝OFF无责任；改case前提后targeted4通过，未改生产迎合错误期待。原HostException/编译日志及checkpoint不签PASS。没有重演旧产品红跑；修前失败机制有旧代码/新增强反例依据，实际运行结论只签本候选。

当前源码/期待/正式文档准备冻结，一次集中独审及主准入尚待。准入前未新打正式包/安装。原目标只读preflight确认632b904普通包拥有权/EXE身份/receipt A7E426…与无进程，未备份/恢复/写装。最终taskAcceptance记录沿已有DevelopmentCandidate/schema/全目录观察，保历史FAIL/NOT_RUN/INCOMPLETE/releaseEligible=false；没有为本轮改通用判定、schema或新治理系统。完整Main/真实输入/真实主客机/FPS/owner体验未测；MovementOriginal重图形新增UI断言未跑，仅轻量layout+静态标签/命令/旧十二许可实现核验。Draft保持，不merge/close/tag/release/升版。

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

# 移动辅助候选证据入口

任务 [#126](https://github.com/Kuroneko2020/JueMingR/issues/126)，Draft [#127](https://github.com/Kuroneko2020/JueMingR/pull/127)。整批 BASE `749e86925763f8a5754d6999ea4056b50de58d65`；最终候选 SHA、累计执行/复用回执、独审和包/安装状态以 Issue 对应收口评论为准。本文件是实现者自查和原件导航，不签独审或发布资格。

Effects 续办原件为 `references/JMR_MOVEMENT_DELIVERY_UNBLOCK_20261019_v1/`，原三移动包现位于 `references/归档文件/JMR_MOVEMENT_ASSISTS_20261019_v1/`。两者均为已存在的完整只读目录，清单/成员 SHA 已核，不宣称原 ZIP 字节身份或本轮重新解压。续办授权与文件分工见 [Issue 登记](https://github.com/Kuroneko2020/JueMingR/issues/126#issuecomment-6267533460)；下文 `ce91` 收口、Related05 和停止结论保留原执行时态，后继实际状态只由 Issue 记录。

## Effects 续办：合同、原因与固定原件

续办以现行《战斗辅助行为合同》区分代表方向、实际消费和完整接收。共享读者先封存current/next原字段，再读Solution及真实许可，不用可能ClearExecution的ExpectedImpact getter诊断一般准备；实际ItemCheck_Shoot消费须绑定本次GUC/phase/weapon/target/point，手部Source可空。机制1000ms与真实Main普通6ms证据分列，不互相冒签。

移动功能仅把连续代按意图、版本及释放/关闭失效接入既有预测；装备移动物理仍为原版。本次必要检查暴露的旧武器接缝及测试维护，不构成新增装备物理或重写移动预测。

| 原因 | 有限维护与保留责任 | 固定原件入口 |
| --- | --- | --- |
| 公共完整Contact旧读者、旧producer计数与代表Solution错位 | 六代表保当前解→真实Enhanced消费；七专用及已出生Known/Replay仍保完整root、原版出生/伤害与精确时钟；不恢复一般完整世界逆算 | `unblock-selfcheck.md`，01/03与各精确Issue登记 |
| 已证旧生产缺陷，共五文件/四原因 | 蜂蜜候选使用真实有限介质scale；Sky原生225复用必要targets快照；FixedSky最近水平整数光标+唯一回放outer不退化；Sharp内联原Opacity getter算式并保setter/Clamp边界 | 04/05/06、09、37—43对应固定manifest与IL原件 |
| 本轮维护自身错误 | 撤无合同依据的小样本quota，修Moving本地动作与sample轴比较、反射可选实参及可变出生引用封存；19观察写out整轮作废，23仅编译失败NOT_EXECUTED | R30/R43等独审原件及`.local`固定索引；不归为旧生产缺陷 |
| 原专用夹具前提与阶段 | Medusa单次命中批仍严格5，更早paid-empty批各严格4；资源/源身份/同步Damage与末态零伤逐批核。Phantasm复用NPC须在夹具建立时清外来buff，保原飞行零伤反例。BeforeNpc首真实动作同准备GUC，CW首Begin保持；不改报表±1 | Medusa46—49固定原件、R47/R50及五叶精确登记 |

四项生产缺口均早于完整移动BASE。Honey/225的原分支引入为`7a8f18a475e43f985cc761295f03a4cfbe844e05`；FixedSky/Sharp为`4c7ac5cddfaa0e79cef2749250086732a0ab09e7`；主线承载均为`f50646b083b27248731dbf7afdc53a3876ff4309`（#115 squash，BASE祖先）。原分支提交与主线承载分别记录，不声称这些分支提交本身是BASE祖先或某更早发布版本已验证通过。

13行原矩阵保全部主伤、指定派生出生/来源及各行既有实伤门。下表为固定50原件的首次受击更新号，不是伤害量；它不等于完整Effects通过。

| 主弹 | 当前路线 | 主弹首次受击更新 | 派生出生数／派生首次受击更新 |
| --- | --- | ---: | --- |
| 91 | 代表Solution | 41 | 2／69 |
| 89 | 代表Solution | 18 | 2／0 |
| 639 | 代表Solution | 22 | 1／53 |
| 207 | 代表Solution | 12 | 0／— |
| 20 | 代表Solution | 14 | 0／— |
| 955 | 代表Solution | 28 | 0／— |
| 94 | 专用Contact，随机Representative | 28 | 0／— |
| 297 | 专用Contact | 33 | 0／— |
| 15 | 专用Contact | 54 | 0／— |
| 27 | 专用Contact | 86 | 0／— |
| 95 | 专用Contact | 39 | 0／— |
| 253 | 专用Contact | 44 | 0／— |
| 502 | 专用Contact | 32 | 0／— |

94的未出生代表时刻31与真实随机出生首伤28分别保留，已出生精确Replay承接后者。91→92父子链、首次主伤41和派生首次受击69由真实出生/Strike证明；独立出生后Known的sample/First/Receive与自然执行分别核时钟，不因数字偶合同就互相替代。89两派生真实出生但首次受击记录为0，不补签其派生伤害。原 RNG、实体池、资源、接收历史、有效时钟和双轴cursor归还门保持。

九个直接下游的迁移界面均为公共读者片段，原专用段保留：

| 叶 | 保留的专用/真实责任 |
| --- | --- |
| ProjectileEnvironment | dry/water/honey/ignoreWater位移与stored velocity、FireArrow转换、液体边界/风/墙/地表反控 |
| Obstacle | 真实先前receiver、穿透退休及原8槽精确oracle；目标本体资格仍撤Enhancement |
| SpecialArrow | 原出生数/AI向量/16 Hellwing种子、Phoenix派生与已出生完整Replay/自然时钟 |
| NativeArrowBirth | 原完整出生矩阵、pose/origin/ordinal及全批；退休公共Flight与独立Known分责；四公共Blocked零伤不冒称绕墙，四固定Bounce真实正例另列 |
| AmmoImpact | finite Kill、完整接收/slot/static/crystal/Quiver；Eventide原8非central真实AimOFF出生与Known精确时钟 |
| AmmoSteering | retarget/Confidence/原版AI及真实消费/Strike；装备资格反控 |
| OnyxBlast | 实际5出生/4选弹/661倍伤、完整爆破与真实Strike |
| NorthPole | 343→344后继、原槽位/完整时钟及过期输入反控 |
| SpearShard | 原合法primitive/type/damage/lifetime/RNG分母；手部BeforeNpc与CW接收分流，已出生Known及自然Damage |

后续传播按真实路由限定：Mixed16仅5382两行、Dart13仅267代表行；Costs预算注入接实际PrepareMotion，SameEpoch原版更新后失效应Basic；Dedup观察当前内联donation而非旧函数计数，单穿透14的selected-owner免疫例外与其他receiver历史分开。Phoenix第三706、Held Births自然42与预定固定42、Consumers21/合法Enhanced6、Moving28=ordinary13/component15、Slot13=4/3/4/2和原窗口均保留。合法随机miss逐行记，完整actual-born Known/null与自然first严格对应，整组保真实Enhanced正伤；物理Born正例不冒充某个注册Enhanced随机样本命中。

FixedSky配对12零/.4姿态的Launch/Update精确首伤与额外真实6ms Shoot消费分别证明；41/42普通机会4.361/5.0997ms均Enhanced36+真实三出生，其追加消费不评自然伤害。40旧fallback候选实际6.1132ms/Basic保RED，39本轮额外Subupdate准入错误也保原件。Sharp真实PE/IL/R4边界与算术重构支持x86中间舍入解释，但未取得JIT机器码；41/42带临时AI157观察，43无该包装的实际Release完整矩阵/后继另封身份。Medusa49默认无临时AI/LOS/Mana包装的两局部同样独立，不改署46—48旧红。

Phantasm52在保留旧阶段的完整前缀中复现原生命不变门失败：selected初有323（OnFire3）12时长，第13动作在NPC.Update中由10000降到9997，631无selected Strike且first为0；victim的189和终止受击另列，不混称飞行伤。原版SetDefaults不清buff数组，最小修正在本专用静态receiver建立时隔离外来buff，原90/1、位置、锁定/终止反例及生命强门不变。52四其他尾叶阶段已变，不冒称与50全输入相同；50无raw的现场不倒填。五叶修正与临时观察默认退出各由固定增量和最终候选审查核定。

固定执行与审查入口：完整Aim41、Costs24及所有局部各保实际输入/Probe/Host/日志身份；默认Effects50到最后Phantasm反例RED，不能由前段或局部PASS签全scope。最终源码增量、五叶修订和默认完整scope的实际资格由Issue固定候选回执及`.local/movement-assists-20261019/delivery-review-package.md`指向的manifest给出。原Related05仍为201必要：88实际STABLE/0复用、Effects失败1、112未运行；最终runner沿完整BASE判执行/复用，不手改原资格或预定仍复用88。

本导航及条件体验卡属于候选冻结输入。最终Related/Release、普通完整ContinuousProcessing、备份Restore/Install及所有者体验分别准入；最终动态结果只写Issue和已排除的`.local`原件，不在累计之后再补tracked提交制造新身份。

## 原 ce91 轮次封存记录

以下各节保留 ce91/d5 原轮次的实施、审查、失败与停止时态；“尚未通过”“不改武器生产”“停止扩张”和旧安装均指该轮次。后续已经取得补充包正式授权，续办范围、生产修复及新原始证据见上节；最终动态资格仍只查 Issue，不能用下列历史停止句覆盖续办授权。

### 实现和证据边界

三项默认关、冲刺默认双击，enabled/mode独立持久化；共享真实输入/Session owner、实际原版消费者、普通完整 ContinuousProcessing 组合及三标准 F5 行。正式行为和实现分别见[合同](../../docs/功能介绍/移动辅助行为合同.md)与[设计](../../docs/设计/移动辅助实现设计.md)。未推进产品版本；跨进程预测协议39/诊断139是实际 AssistedJump 序列化字段变化，旧端拒绝，不是产品版本。

本地原件根 `.local/movement-assists-20261019/`，所有隔离宿主静音，使用自有可丢弃世界/玩家/保存目录；没有操作所有者游戏、真实存档、系统/RDP/驱动/音量。最终图形完整原件 `original-07-solar-earned.log` 与同名目录；`native-inputs.json` 绑定原版/Host/Probe，`source-inputs.json`、`source-build-record.json` 保留执行时源码和准备身份。Host SHA256 `199ED58B399DFFF2C4CF3BCAB0B54779E59FD22AF91E19A446B7355848AAC0D5`；执行 Probe（只补测试副本LAA）SHA256 `4635ED3FA4E4F44ED663125CD493CB3DB1A8F435A17F1FF57B11BA6689A017A5`。源码后续窄改须由 Issue 明确受影响重验/复用，不能把07身份改写为最终提交。

### 已实际执行

| 入口 | 原件 | 实际层级 |
| --- | --- | --- |
| 四项 Movement Architecture checks | 早期Debug执行输出 | 默认/16字段组合/严格codec、隔离保存重启与保护文件、手势18/19/2 tick、Jump owner、排序；累计回执补最终身份 |
| MovementDashCpu | `dash-cpu-05.log` | 原版 DashMovement/CommonDash，32能力组合及原生键/冷却/坐骑/异常归还；不冒充Main映射 |
| MovementJumpCpu | `jump-cpu-02.log` | 346 stage案例，9族实际消费，4坐骑原生SetMount/UpdateEffects来源；4×20000 helper测量零新增分配 |
| MovementTeleportCpu | `teleport-cpu-01.log` | 两杖原版Use→ApplyItemTime→局部目标→Teleport/SendData入口/混沌代价；真实缩放/反重力；合法0/近邻10/耗尽2120候选有界及零查询分配 |
| MovementOriginal | `original-07-solar-earned.log` | 真Main输入/world/Player.Update/装备/Jump/Draw；180步前封存预测误差0/3次地跳；5装备族各1次；4坐骑各150更新、各2地跳2额外跳；地面资源回满/蛙腿/远端意图/原异常传播 |
| 同一完整scope中的Dash/UI | 07日志与PNG | 双键最近左右真实映射→原版归零→CommonDash实际速度；克盾真碰撞与太阳自然产盾→碰撞耗一次；F5三行、关闭改模式、100/150%命中和渲染、三公共录键保存/切换反馈、真world文本/录键/模态/指针拒绝及普通F5/背包允许 |
| 工作量路由 | `routing-03.log` | 实际Git变化/混合/删除/缺BASE/未知负例；Movement叶/公共输入存储传播；CPU累计不自动启动图形 |

07截图为 `movement-page-ui-100.png`、`movement-page-ui-150.png`、`movement-mode-off-hold.png`，实现者已逐张查看文字、三行与模式。公共累计尚须以最终 Issue 回执区分本次执行、有效复用和未验。

### 性能原件与限度

07保留完整逐帧CSV：关闭/开启闲置、原版人工/辅助地面与复杂组合，两轮反序。每地面窗共240更新/5次地跳，复杂窗共420更新/地跳1+额外5、五族各1/翼142 tick；前40为暖场，计时和分配只取后200/380，动作计数含暖场，因此不将尾窗耗时除全窗动作数声称精确每动作成本。rocketDelta只是实际字段减少量；无装备35包含原版清理/刷新，不能称燃料消费，翼靴组合7含翼转换；独立靴毯场景才证明实际靴燃料消费。持跳段1→0时原版可先处理1tick翼，下一次JumpMovement才接仍剩余的多段族。

`movement-idle-pairs-settled.csv` 包含8对反转顺序的48更新block，先在测量外等真实设置保存完成、固定日时、暖16；数组预分配且日志/CSV在block外。07 update on-minus-off毫秒为 `+.02098,-.08899,-.08728,-.00176,+.01573,+.01199,+.04976,-.00677`，4正4负，差均值-.010792/中位+.005118ms；整链world+Draw差均值-.028181/中位+.012730ms。OFF/ON各384有效样本的AppDomain总分配72190868/72123992B，GC0各16；settled未记录GC1/2。06有6正2负且负均值，不能以负均值宣称提速。本场景增量未能稳定量化；保留均值/中位/p95/max和GC/分配，不丢尖峰、不立统一预算、不宣称字面零耗时或普遍性能接受/FPS。微测查询/owner零分配也不等于整Main零分配。最终有限性能判断和分母分析见 Issue。

### 失败、期待和夹具修正

全部旧日志保留。dash02原期待“太阳发动即耗盾”错误：原SolarDashStart只立solarDashing，首次实际NPC碰撞才ConsumeSolarFlare；05仅发动原期待已纠正，07补真实碰撞和第二次不再耗盾，不能互相冒充。dash03修原版ArmorSetBonuses初始化；dash04移除原版DashMovement未保证的direction写入期待，继续核实际速度。

original01夹具将全部角色inactive后等待Session，自阻塞；在等待前建立真实active本机。02/03完成Jump/性能后Mapped失败：新Player落地点未设fallStart导致原版摔死，随后UpdateDead抄原始双键，且专家盾完整Update资格与孤立UpdateEquips不同；改安全落地事实并以Tabi验证普通双键，07再以真实专家资格验证克盾。

04/05初始化/首帧链未完成，原因未定，不签“GPU不可用”。05 stop时快照before但最终log有ready，不能当同一等待点。只读准确XNA发现缺设备/音频异常可被默认模态消息吞掉，隔离ViewGame现override记录原异常并return false、StartLoop后核Initialize/Device，避免空Tick等待；06/07真实初始化成功未采到该异常，不能倒填04/05根因。仅05按精确自有PID/path/start核验后停止并保留stop回执。

06太阳新Player预种Buff在setSolar前被原版删除；07用真实装备/180tick自然产盾，不人工恢复盾。路由01/02仅测试期待遗漏：实际路由原已有view/journey公共传播，新Movement传播使精确断言暴露旧遗漏，补期望集合而未改view/journey生产或扩大到无关业务；原精确正反例保留，03通过。

### 待交付门与现场资产

后续最终UI原件为 `original-09-mode-left.log` 与同名目录：所有者要求模式移到开启左边，09在100/150%及按住/双击两态断言模式/开启/关闭顺序、三行开启/关闭同列，并真实点击关闭状态模式，完整Main/Dash/UI专项再次PASS。实现者已查看最终两张PNG。该次源码 `74511837217522b1af3496bed46abf6082f7c2ca`，Host SHA256 `ADAA0A83F4EA6CC8B4AC68060C6B51BC17A1F486D24CBF344EB07B6EBC2E52BC`，执行Probe `50F8D3E38AE6A691C290476E6FC10E305B25385540F2CF84C7CFC4C8000C2FB8`，日志SHA256 `076BC882E4A48BCABB10738412783EBEA8022902BE0ED8F173D49EF8A48C1749`；最终仅证据导航提交不冒称此旧执行commit。08则证明补充options/fancy/capture预采样取消门后的完整主链，精确身份保留各自目录；窄取消反控与最终累计结果以Issue原回执为准。

09再次记录完整动作/资源配对及8对settled：Update与full均4正4负，update差均值-.048066/中位+.009745ms，full差均值-.168619/中位+.073914ms，离散范围仍大。结合07/08和零分配有限helper，本场景没有可重复稳定额外损失的证据；也不能证明普遍零性能损失、负均值提速或所有者长期FPS。所有尖峰保留。累计01在剪裁F5夹具字段无赋值的警告即错误处停，显式可选attachment=false修复；累计02为所有者新UI决定在准确自有build及artifacts后代边界中断，保留stop回执，无累计PASS。最终整批Related从BASE核对执行/有效复用，不能借旧Stage记录。

真实物理焦点/IME/所有者手感、长期FPS、真实多人客户端/权威结果尚未验；SendData入口不是网络同步闭环。独审、正式普通完整Release候选包、受控一次备份换装待对应准入，当前没有新包/安装成功结论。恢复体验入口见[体验卡](../../scripts/phase0s/Movement-Assists-Owner-Test-Card.zh-CN.md)。

四项外来资产保持：tracked `docs/设计/本地参考资料使用说明.md`，untracked `.workbuddy/memory/2026-10-03.md`、`issue112.txt`、`pr113.json`。哈希声明 `.local/movement-assists-20261019/approved-retained-assets.json`，沿现有合法通道：`clean=false/releaseEligible=false`，提交本任务源码后仅这四项保留，不借旧Stage例外，不伪造clean。禁止合并/关闭/Tag/Release/版本推进。

### 独审修复与当前累计阻塞

原冻结 `7475454` 的独审原件为 `.local/movement-independent-review-20261019/review-7475454.md`，三项P2和累计失败保持历史身份。R1为精确比较后端恢复Profile后再次编码读取live Jump owner；新增请求/准确Player对象scope、正常/异常清理及仍占用receiver重入拒绝。Complex每帧按当前Mechanism与恢复意图重算，不能冻结初始dashDelay派生结果。`jump-cpu-08-thread-allocation.log`实际通过8组合初/后继完整alignment、直接及A→B→A重入/异常/clone/下一请求反控、348原版stage及4×20000同步helper零分配。`snapshot-review-01.log`与`worker-player-review-01.log`分别通过完整快照/codec/age/前缀以及独立worker玩家推进。c1累计04已准确收录并实际执行跟踪后的新Wire叶，原来源缺口在该执行身份闭合；后继候选按正常累计规则核对新增影响。旧06/11的git-ls-files来源清单不包含当时未跟踪的新叶，不能补签为完整来源清单。

Jump07在wire通过后，旧helper把AppDomain全线程分配误称自身同步分配，steady=8192失败。修订以实际CLR一次绑定线程计数、明确64B对象正控（实际76B）、严格steady==0，AppDomain另列且不归因；08实际观察一窗线程0而域级8192。保留07失败，不追填8192的具体线程/调用栈来源，不扩大为整Main零分配。

R2/R3由 `original-11-mode-retirement.log` 完整Main/UI专项证明。Host SHA256 `1A14904328C036EBC4502C6DA14E62E15D911BAA21562A9B53509A4B198B7339`，执行Probe `3A1926844B13D10AA8752502E232104B69CD1CD710C442478FE1765148AF8A92`，日志SHA256 `6BD27126C43665F8398A0F345C85BF47415D497FFFC71A2387F921795AFCEDD7`。两模式真实热键开/关带实际/前态模式；关闭状态F5改模式不Clear旧结果而立即退休旧提示，F5自身静默。后续仅R1占用guard及测试维护，不冒称Main11执行了后来的所有源码。

R3的Main10/11均为实际Debug隔离构建与harness场景，不是普通Release包或所有者FPS测量。原件均保留：`movement-dash-pairs.csv`每轮4对反序，原生F19人工/辅助各暖32（1次发动）+测240（4次发动），冷却[-1,20]、火箭7/太阳0及速度/位置逐帧相同；每臂总960测量更新/16次实际发动。`movement-fixed-hook-pairs.csv`每臂4×96=384测量更新，暖32、无动作，只撤装三个自有Movement Hook owner，Session/input/prefs/其它Hook仍在，非重建BASE。Main11 Dash update辅助-minus-人工为 `+.069619,-.152039,+.056699,+.031098ms`，Hook安装-minus-撤除为 `+.029664,-.104639,+.027415,-.337573ms`；Main10 Dash四负而Hook四正，不能以均值或负值宣称提速/零成本。每block均值/p50/p95/max、分配与GC0/1/2见原log，逐帧原CSV和原尖峰不删除；有限重算导航 `performance-review-pairs.json`。

累计03在NpcRollingCpu首个旧TerrainBounds期待退出1。准确引入链为917a531统一越界拒绝测试，f50646b/#115生产改原版nativeClip/FreeBody后未同步；BASE→本批相关生产diff为空，f506新增42个free例未盖colliding边缘。Boundary维护实际Derpling177/58×44，10干空气边缘加6实体/水/蜂蜜碰撞oracle，保留player/projectile/非法几何/缺格/重采样/4096限额；Collision scratch及Dust/random实例finally归还。其后ShortPrefix同源迁移缺漏：f506 World改消费Host.DisplayPath当轮租约，旧测试只source.Prepare，66870f3只改引用期待。真实owner修订在rolling04/05实际取得同帧sample/lease、CanDraw true、strokes2，未知格无未来/死亡退休、独立strict reader通过；严格reader使用独立slot2，避免真实Attack合法释放其slot1。旧strokes0具体gate未采到，保持未知。

Rolling05把连续呈现消费接到当前DisplayPath、完整身份/当轮lease/有限age及World同引用；原始Source另列，剩余future扣实际age，quality只在fresh源采样并记录缺源/非当帧跳过和非零样本门。当前候选缺租约ABI拒绝，认证历史f8路线保留其直接Cache合同。CPU不签Draw；当前固定绘制只能在真实121点当帧冻结事实反复Draw，不能cache.Publish或私填租约冒充历史frozen128比较。图形固定绘制新分支尚未执行。

`rolling-cpu-05-display-consumer.log`（SHA256 `6F855233BD8E08AF24768CECD2447B4979846FE928743F5CD4CB27CA7B0B7CF0`）完整执行11场1478更新，原Frozen f8数值门保持后报7场RED：stable180/180可见但剩余60步137；hornet-open360/360可见但60步300；guardian-A-select-B合法90/选中79/可见76/最长11。原summary/actors/updates/red均保留。该历史数值门的后继现行适用范围已核对，见下段逐项映射；05原RED身份不变，不改生产，也不据专项签最终累计。独审增量、普通包和安全换装仍待必要准入。

后继已按现行权威完成维护：正式[战斗合同](../../docs/功能介绍/战斗辅助行为合同.md)的2026-10-17更新接受有用短未来、不以恢复约两秒长尾验收；[阶段当前入口](../../docs/计划/辅助瞄准阶段文档.md)接受保留最新接续、不追所有NPC零空档，且实际普通冷首5/8GUC不承诺T+2/T+3。旧门最初确实适用于普通rolling候选，不能说它只属exact/native；后继替代也不来自单纯取消毫秒预算。当前候选保留全部原数值与预定B分母为 `rolling-historical-f8-reference-red.txt`，真实身份/租约/有限age/World同引用、未知/死亡/OFF/独立需求和整场完全未取得或未显示B继续硬检查；认证历史f8路线仍按其旧拒绝门，不改署旧结果。

c1增量独审 `.local/movement-independent-review-20261019/increment-c1e97f3-rolling.md`认可其余维护，新增R4仅为oracle清理遗漏：准确Derpling原版UpdateCollision/Wet/Tile/Slope调用写Collision.up/down/honey/shimmer/stair/stairFall/sloping七bool，可见入水Dust.NewDust还可写静态dCount；原件未见证具体污染，不能回填“06已污染”。现scope准确归还七bool/dCount及临时camera，保护原dust数组/random实例；`rolling-cpu-08-complete-scratch.log`（SHA256 `15656C83082E78CD787E5ECE469606F204AB2B87BA9912C7A21F3A8E8B09E571`）实际使七flag全true、dCount=.625，从可见水/蜂蜜原版splash观察到dCount写0，再归还.625及调用者原值，完整11场1483更新PASS。07只含七flag修订、08才含dCount闭合，不能互相改署。

最终累计04-c1e97f3已退出1，85项实际执行且新Wire叶当前字节已被准确收录执行，NpcRollingCpu通过；首个新分歧为CombatAimCpu→NativeCombatAmmoChecks:40，武器95/弹药278的ExpectedImpact为空。后继7a8f18a使BalancedHand经代表解入口，成功Point/Reachability/ReceiveTick与可空Contact分离，旧矩阵漏同步；combat-aim-01实际19项Ammo及后续纯Replay已越过该首错。其后Sky3029与父ordinary原版结局仍在有限因果核验，尚无完整CombatAim或累计成功，不签普通包/安装成功。16项原冻结与新测试修订、独审/窄执行/累计资格分别保留，不借前项补签整批。

`rolling-cpu-06-current-contract.log`（SHA256 `E80F09FF5CDBE8ED973FA6B4AB1030247AC000CDFEDE91FFFCA1F89E64DFB0BD`）完成11场1478更新并通过当前声明scope，原各统计及历史RED仍落盘。新增有限CPU `rolling-selection-geometry.csv`（SHA256 `6E5518E7E32E48662FB1E43505EDD442FD50F7D838799B4ADE627846A1A64DBF`）见证guardian frame43–53/GUC381–391：A16/B17都具接收资格、各受击框到RealMouse的d²均0，候选数2，实际/独立首位均16，与BASE继承的先RetireTarget再按低slot实现一致，11帧不是World无线。正式战斗合同第97行仍要求平局保留当前目标；当前未找到明确替代该条的更高裁决，因此不能据此称合同正确选择，也不在本批改战斗生产或正式合同。3个B已选而未显示的单帧和其它真实空档仍保留，不泛化为零空档。`rolling-negative-01-current-contract.log`实际两场360更新：gun-B合法180/选中0/显示0/最长180仍被硬拒绝，专项期待此真实拒绝而PASS，不能靠缩小分母变绿。最终提交后的累计与原独审增量尚待完成。

### 既有辅助瞄准累计接缝维护（原 ce91 轮次，当时尚未完整通过）

本批不新增自动开火或修改武器瞄准算法。共享输入与玩家预测使必要Related累计进入既有CombatAim；以下仅为旧测试与已接受入口迁移、准确原版阶段的维护。7a8f18a将普通BalancedHand改为代表Point/Reachability/可选ReceiveTick，成功时完整Contact可空；Ammo旧矩阵未同步。`combat-aim-01-current-solutions.log`实际越过19项弹药及其后完整Replay/装备窗口，95/278实consumed Enhanced与保存Point一致、首Damage19严格对应ReceiveTick换算；不是用query或代表结果冒充实伤。其它专用完整Contact和原版结局门保留。

`combat-aim-parent-03-origin.log`保留旧非法场景RED：原本400/400直接Shoot，实际origin410/421；准确原版Player.Update先执行BordersMovement，再ItemCheckWrapped，真实无射击反控把位置归一到640/640、速度0/.11、发射原点650/661；预测保存的nextOrigin正是650/661。f50646b新增真实边界前提后旧夹具漏掉这一原版阶段。后继父矩阵移到既有tile43地板上的700/646并整体平移原目标/物理指针/相机，保380/10相对目标几何、枪/弓×静/动、原类型/物理/RNG/资源/cursor与真实Damage门。`combat-aim-parent-04-grounded.log`四组实伤19/20/41/46更新通过，当前/下一原点均710/667，两个真实首次点击/释放也通过。

04随后停在本次适配引入的ActionPhase无条件solution要求；旧用例仅在Contact存在时核发布引用。射后冷却本可合法无solution，不能要求伪造计划，也不能因Contact可空跳过实际存在的solution。后继读者保两份真实NPC前后采样身份必核，并分别验证实际solution引用与Enhanced能力或已退休/冷却无Enhanced；该修订在父05窄反馈实际通过：solution不存在时真实Basic合法、Enhanced已退休，permission=true、consumed=true、itemTime16/animation15。

`combat-aim-02-sky-witness.log`（SHA256 `2E7307B85BE696DF946849D1EBCE9F8E9E455CA59FC1536F286B7973C63F600E`）保持3029原7123单手/每箭120更新硬门的RED。BeforeNpc实际Enhanced消费Point1153/1020与计划一致，Movement全关；三原版随机箭各120次Damage、生命损失0，最近实际/未来接收框gap²分别196/1312/49，接收资格和immune均正常。独立私池同Point合法ordinal0原版motor/Damage在43更新造成44生命损失，严格匹配计划43，不能计入自然群实伤分母。原池/RNG/时钟/Collision/actor状态归还实际通过，内部异常归还仅静态覆盖。该单状态证明自然随机几何miss，不证明整个Aim或所有随机未来。

后继3029有限窗口在执行前固定9手、19更新间隔、总上限272、每箭最多120，单一7123流不按手重seed；保单手miss反例与合法代表正例分述，要求自然原版Damage与真实出手/出生/命中分母。它只是有限直接Shoot组件，不冒称真实Player.Update连续开火；其它五种Sky、3570原自然伤害及屋顶阴阳、Scatter原门不因此降低。尚未得到完整CombatAim与最终累计结果，普通包和安装仍待必要检查及独审准入。
`combat-aim-03-fixed-window.log`（SHA256 `5F899C0CDCA9A1333EB654F2D19CFCEAF40561DAD6EDBB00E337682E5FE13EE4`）完整原入口实际越过Ammo/全部六种Sky及屋顶阴阳/Scatter七行及16seed/父矩阵与点击/动作阶段/原版跑跳射手。固定9手自然窗口实际9次允许及Shoot、29箭、9弹药、1箭/1事件真伤34、首伤world81，272总更新/每箭最多120不因命中提前结束；合法代表不计自然分母。其后NPC初始横坐标850的严格几何负例通过，847正例首伤9与绝对ReceiveTick等式通过；退出原因是本次适配新增断言误把连续浮点NPC位置与整数伤害框作.15近似比较，不是原伤害或时钟失败。原版Damage先临时加netOffset再读整数Hitbox，NpcTrajectoryPoint同样明确整型截断；后继只改为对应ReceiveTick四条整数边逐项相等，没有增加epsilon。

`combat-aim-parent-05-integer-receive.log`（SHA256 `845DA0385EDD2ED779CAF50B75324980E5F20D2252B397818D51126458EB44F4`）使用BASE已存在的显式ordinary selector，实际通过剩余父族：四矩阵/点击释放/动作发布与退休/跑跳/850几何负例/847真实Damage9及精确接收框/失败锁存与恢复、实例与generation、致命异常、缺格保护和cursor。它是局部反馈，不改签完整Aim。BASE已有ordinary selector保留，最终Related累计明确清除此环境变量并核完整Sky/Scatter执行；最终整批执行/有效复用与资格仍以提交后回执为准。此维护终点不新增瞄准算法、自动开火或生产观察设施，普通包和换装继续等待必要累计与独审准入。
### 原 ce91 收口（历史）：代码增量通过独审，整体资格失败，未制作包或换装

移动源码候选固定为 `d5ff6e70c8b18fffe7b08dde084f0adca29b54c6`；后继仅证据文档提交身份见Issue最终回执。原独审的完整BASE与逐次增量，以及 `increment-d5ff6e7-aim.md`，代码/测试增量结论PASS且无新增P0/P1/P2；不等于整批工程准入。

最终Related05从BASE运行，201必要项实际88项执行通过、0复用，88项sidecar均STABLE；native-CombatEffectsCpu在NativeCombatEffectChecks:134的row91完整Contact非空要求失败，112项未运行。正式整体状态FAILED，完整Aim/Control/Mechanics已实际通过的稳定回执不被抹去。原batch `eb135def78d24bda95f2c5f46b112dcd`、执行/失败日志及资格文件原样保存在 `.local/movement-assists-20261019/cumulative05-checkpoints/`；FAILED顶层构建记录另存 `cumulative05-build-record.json`。逐项失败/未运行表 `cumulative05-failed-summary.json` SHA256 `BE773DFE011A76A623525C83DD01E187A7335B42A19037B0B9C54534D706D59E`；88稳定资格索引 `cumulative05-stability-addendum.json` SHA256 `AA1F3FB45511A06DAB40E6B50CF7BEB99DB135328929112D89694D6C6BDE427E`。05日志SHA256 `FE8D4F0D5D0A45B0975B7E5DE243FA3149703920832611A33CBE39128A4DA0AC`。

Effects完整只读范围原件 `effects-readonly-scope-20261019.md` SHA256 `27FD3E648A9313AC9FA0E09DB171735E95C6AF4B2684378F9A5B616491BB2A84`：本叶13行中6条走可空Contact的代表solution、7条仍专用完整Contact；首91旧门与7a8f18入口结构不相容，但没有该行真实solution/Enhanced/Reachability见证，不能据此称当前准备成功。至少本叶及9个下游叶涉及多机制已退出的一般完整root合同，不能只换getter闭合。默认派发84叶+FiniteTail11与文本84处Contact读取是不同统计，均不等于已证84缺陷。Root裁决停止这批范围扩张：不改Effects期待或武器生产、不加观察设施，保全部RED/稳定单项/未运行事实，另行明确测试合同迁移才可继续。

普通完整Release候选包、本批轻量备份、Restore、Install全部未执行，没有替代窄包或旧Stage借用。既有安装未被本批操作；此前同目标只读回执核到 `01aa9eb8f58aeecdf8a0ec2fffc0f441b4d27aed`，并保留正规原包Restore入口。安装阻塞为必要累计未通过，不是已证明移动代码故障或已通过所有剩余检查。体验卡 `scripts/phase0s/Movement-Assists-Owner-Test-Card.zh-CN.md` 仅供后续合格新包；当前旧安装不能据此体验本批新功能。Issue126与DraftPR127保持OPEN/Draft，无merge、Tag/Release或版本推进。

# 第三保存点：正确性修复已核，核心性能与自然显示仍拒门

**NEEDS_FIX / INCOMPLETE，不得准入打包安装。** 本批保留g13完整玩家固定点修复、g14已证实的首坡面扫描省算、Observation与浮水终门只读摘要以及全部有效失败。正常现装仍75a，未启动/更换正常游戏或接触真实存档；不合并、关闭、Release。当前没有足够证据支持另一个可直接删除的错误工作边界，本轮停止继续试探优化。此前冻结2ms/尾部/相邻帧/6ms合作安全预算/Path120/Draw294合同不放宽。

## 源码、载荷与复跑入口

本批源码相对a702b6b，七条实现/测试路径：RollingNpcPrediction.cs、PredictionTerrain.cs、NativeCombatPlayerFixedPointChecks.cs(新)、NativeCombatPlayerPolicyChecks.cs、NativeCombatTerrainChecks.cs、NativeCombatObservationChecks.cs、NativeCombatOriginalFeedbackChecks.cs。`source-over-a702b6b.patch`保跟踪路径差分，`new-fixed-point-checks.cs`保当时新增文件完整内容；实际正式路径见同提交src/tests。文档只更新当前入口，长历次记录另存stage-history-through-a702b6b.md。外来四资产不在本批。

| 实际固定身份 | SHA256 / MVID或说明 |
| --- | --- |
| g13 Host，优化Release/x86 | F1072D318426809C9360CB45C7B85B7013BE865E8C13637C8B0B344A71AC6685 / 64eed61e-f77b-4ce3-bfa1-6232764b4178 |
| g13 Features | 99B10CB658251BE90863AB68AD4AF1EB7CE2C3BC9EAE250C80EADD82DF892E2D / fa1a7ba6-b3bb-4c9e-a9ec-77b50bff5b61 |
| g14 Host，优化Release/x86 | 45E9231EE3AE8E5790430E3CAD654DD61148A499F736209CAB0100D36C725E44；完整四模块MVID/SHA在每原log LOAD行 |
| 原共用probe-g13 | 5770027B129FFDBC3F6AC4C179092E9CAD2AD106C3DC5C3E16D678C921E07656 |
| 共用probe-g14 | B026E6C6B85B6DB9D0A3AE87993DE4B1C4788D2524963B478716E4E07A5E2CB4 |
| 后继仅失败摘要probe | 894DFF6253A5E5335F67577E4688F284C5C957C8D2792A9251A5B72C9255E8B7，只Observation/Seams补摘要复核；不追绑旧577成绩 |
| 锁定原版实际1.4.5.8 | 960A03BFF6050CF7BE16DFC1A7B19E10FC2C4F8F835A6A3B135A50DD9E6BA2F3 / 2c29f6c3-4bd9-4add-9c58-da159804e083 |

私有g13/g14各四Release模块的source标签仍为各自uncommitted实验标签，不能倒写成后来Git提交或完整包SourceSHA。Debug probe只是测量/测试载体，严格加载上述真实优化四模块。`RunIncidentRegression.cs`(ACEAAA61...)是冻结既有七叶的反射carrier，不新增fake/stub/期待；其exeAFD518FC...。参数为repo、fixedProbe、payloadDirectory、ownOutput、锁定ContentDirectory、leaf，各叶独立新进程；leaf为basic-aim/candidate-demand/observation/geometry/spears/shape-visual/sealed-child-draw。正式母scope/专项入口仍在NativeChecks及项目构建设计，私有carrier不替代最终包资格。

CPU用既有RunNaturalCpu B64191...无Draw/FirstChance/逐调用探针、原房间/同输入、四case各300+60真实退出；分段n不足360，只有拒门/定位用途，不能据某分段通过签最终准入。原子数据按case+frame行序关联，不能以重复GUC join。图形使用RunNaturalDrawPlain.cs，仅从B641适配将RunCpu换为原RunCandidate；无FirstChance、无业务参数钩子，exe77D9DD12...。完整Content来自所有者已拥有的锁定原版，不上传程序集或游戏资产。

## 正确性与仍失败的准入

旧g12同新probe固定点叶有效拒门15动作，新g13全部14机制PASS，比较的是真实Rolling.AdvancePlayer与未shortcut的纯PlayerMotionContinuation，并非独立完整原版Player.Update oracle。WetSlime/SwimTime/GfxOffset为同forecast自然时钟/状态冻结的实证；TrackBoost/Down是主动改输入的失效反控，不能冒称真人现场复现。旧XY/V+无按键shortcut源于917a531a6416f23c826ab84ce1cee5bf555196f3(2026-10-09，已核a702祖先)，不是本轮新增。证书只在完整post-prefix→post-fluid/border SameSample成立后授予；每步Gravity/Wall三个prefix及Borders/ItemCheck保留；只历史值副本两已消耗索引局部比较，不改Platform.SameSample/实际时钟，所有Reset/restart/Clear释放证书引用。

g14仅省全首scan已证每Cell.Slope==0的同bounds/epoch/当前尺寸重复扫描；inactive非0也拒证，unknown/失败不授证，Ice capture/guard禁用，首/尾Area及head/stair/V/gravity照旧。热命中必须实计cold CellAt>0且第二相同rectangle不再进scan，并对照原SlopeCollision；实际新坡、body/world尺寸、unknown partial、Ice读集/guard与原5103坡面、1782实体、1792视线、882边界、Dry/Wind均PASS。epoch回卷测试只观察重采，漏清数组也可能过该例，显式数组清理只有静态已核，不能夸为动态独立证明。

| g13同固定577七叶 | 结果及边界 |
| --- | --- |
| 正规Basic Usage | PASS；真实消费者/身份/安全/纯挥剑/Starfury反重力/Leaf增强/Bow相容/DD2 receipt/Yoyo gap0/8/16 |
| CandidateDemand | PASS；实际Enhanced prefix17/样本18，Path OFF/ON真实首发、同GUC Completed121、nearest/同障碍身份与marker-only零未来 |
| Observation | FAIL；仅Path/持CopperPickaxe/终末371首窗未取得future。后继894摘要同失败：GUC1、预算6.812ms/Exceeded、Required120/min1、rawnone、Source尚未owns/记录outcome、pathFailed=false。母scope不先Aim.Initialize，standalone只多初始化原assets/staticcontent/空player槽，未Reset当前玩家或额外Prepare。不能仅按检查点归热点或默认夹具错误。 |
| Geometry | exit0/PASS原600 OFF、1000自然Damage/130混合burst、取样scope/保留/cleanup；native日志643/644 invariant原件保留，见下述前提边界 |
| Spears | FAIL4；1228近OFF/ON首帧预算12.713/7.3291ms，远OFF/ON7.4026/8.2701，目标正确、无持久Fault、Read0/1none、parent1/child0；75帧期待未改，其他家族/冷却尾结果在原log |
| ShapeVisual | PASS实际全像素/no-resample/倒置和离屏/点线/事件/1200对象容量；不是FPS |
| Floating sealed-child Draw | FAIL；qualified70但published66/70。后继摘要确认parent/child出生各1、bodyDraw/childDraw/memberChecked成立、44 detached previews/4 receive时间、真实child伤3、receive122匹配。子体独立功能成立不能删4帧sharedPath缺口来绿总门；既有family异常栈观察本身可能有开销，非纯计时。 |

Geometry原Invariant来自锁定Projectile.cs34859：RainbowCrystalExplosion644 AI用ai1 key读643 parent，ProjectileKey.cs106 Assert后返回false。既有Geometry98/125直接构造644+AI而未建真实643 key，可解析到另一644；行blame917a531a，非本轮采样改动、非六prism链。未发现旧raw同句，故只认源码既有前提，不能代真实643→644出生链PASS，也不扩大新家族。

无图形CPU完整原窗仍拒门：g13 moving idle180/attack90/release60净+2.66796/+2.86550/+2.49760ms；g14分别+2.74136/+2.89154/+2.11045ms。原冷预算、首次native与R+native尖峰、GC和所有尾部均保留，不分类删除。g14小窗未证持续mean收益，不能单靠退出变便宜概括优化达标。

正常g13实际Draw第一次/仅一次，原600完整保留：standing289/294 missing5(最长3，idle2/3/4、attack180/238)，moving292/294 missing2(attack243/restore292)，全部GUC预算退出。两场出生各15、命中16/15、原AI/落地16/15/实际移动91.32及首失败/正常PNG保留。**额外negative12未确认**：该plain adapter最终只打印外层TargetInvocationException，无法区分negative提前失败与最终aggregate失败。CSV expected=false12只是startup4+switch8，绝非negative12；之前聊天里negative PASS表述已撤回。以后有新候选需Draw时才补negative计数与最终inner链，本次不重跑600求绿。

## 工作量与最后有限归属

同B026/旧RunNaturalCpuCount(EB74D...) g13/g14各完整1440行；全窗触限12/7保留。stable idle10..179：standing g13完整169/170、g14 170/170，moving168/170和170/170。对共同完整未裁断行逐case+frame匹配：standing169行CellAt7802.284→5583.036(-28.44%)，moving168行13450.768→10169.405(-24.39%)，两个窗口真实Read分别完全152.154/174.560、Advance120不减。故g14确证省scan工作可保留，但该子集只证明同完整工作的减少，不能删除纯CPU失败、时间签门或把更早预算中止当收益。

原ADD26D RunNaturalPhaseBreakdown只8父边界；继后完整复制FC593E5...为RunNaturalRollingBreakdown，独立HarmonyId，仅再加AdvancePlayer与NpcMotion.Step(7/8args分列)，不读/回写业务ref/out/__args。两个诊断各1440、missing0/overflow0、自身CPU行一一对应，初始化/Reset/其它线程另列。父边界moving稳定idle170(预算3保留)：Source2.52345、Rolling2.49702，Plan.09718、Solve.06542ms；说明主要剩余仍单Rolling，非候选/最终重复或weapon求解。最后内部观察stable170保预算5：Rolling2.87202、Player.55281、Step8.98037，Step7观测0；其差额约1.34ms包含未细分必要工作、inline/外层/观察开销，**不是已证业务热点或可删除exclusive成本**。附加120级hook本身可影响预算；不相加inclusive，不以零观测证明无工作。

原诊断source/summary的“up to120/frame each”只适用于单Prepare窗，作为整真实更新上限是错误说明；attack实测max Advance135/Step8 136(同真实更新可有BeforeNpc短prefix+Completed)。执行source/log冻结原样保留，这里更正，不回写旧原件。最后窗口13预算帧全部保留，未升级为纯计时或FPS证据。

Slime干燥空中AI部分步不读玩家，但每step CheckActive仍用当步玩家矩形影响TimeLeft/Despawn，特殊射击/metal/wet/随机亦有消费；没有已证合法跳后段/延迟玩家计算的边界。本轮不据单一场景扩大模型、不压短120/缩294或6ms、不继续试探性优化。

## 独审与原件保全

`review/`独审原样保存，实施者未参与意见起草：g13 CA6907C5...、g14 81DA96A7...、回归/Draw/诊断9CC1961A...、最后附录253B6BA5...。静态没有新增确定实现P1/P2；整体NEEDS_FIX / INCOMPLETE，准入失败仍为实质阻断。补摘要不改任何期待或分母。raw-manifest核原始复制件length/SHA；source差分、Build日志、完整逐帧CSV、PNG、原private代码公开，未上传DLL/ZIP/私有配置世界/会话内容。外来四资产保持原字节。本保存点是有证据的阶段进展，不是最终包、安装、真人体验、FPS或多人验收。
# g13 回归/正常 Draw 与 g14 归属诊断独立复核

2026-10-12；`/root/independent_review`，已核实际 GPT-6.1 Sol / High 未变。本次只读适配器、测试差分、原版参考、原始日志/CSV 和三张 PNG；未参与实现或期待起草，未运行构建、测试、CPU/GPU。仅写本报告。

结论：g13 自然正常 Draw 及三项回归有真实失败，整体仍 **NEEDS_FIX**；最终完整准入 **INCOMPLETE**。已有局部 PASS、g14 首 scan 工作减少和父边界诊断均不能关闭连续 Draw 或冻结持续性能门。

## 身份与载荷分离

- 七叶原 g13：Host `F1072D318426809C9360CB45C7B85B7013BE865E8C13637C8B0B344A71AC6685` / MVID `64eed61e-f77b-4ce3-bfa1-6232764b4178`；Features `99B10CB658251BE90863AB68AD4AF1EB7CE2C3BC9EAE250C80EADD82DF892E2D`；probe `5770027B129FFDBC3F6AC4C179092E9CAD2AD106C3DC5C3E16D678C921E07656`。
- `RunIncidentRegression.cs` SHA `ACEAAA61A8ADAE98B5411817B38E0F0BDB4CF831A8D6B004C38EB2CC5F56094C`，compiled `AFD518FC1ECCA246F0D080A3F6FC0AA29C265195194538521A953AA566A8ED80`。显式叶选择、payload 与 context 绑定核验；清除继承 item 子集，只复用既有 achievement 隔离，graphics 叶实际原版 Content/XNA Draw。未注册 FirstChance 或业务 ref/out/__args 观察器。
- 两项新增摘要仍同 g13 产品，probe 改为 `894DFF6253A5E5335F67577E4688F284C5C957C8D2792A9251A5B72C9255E8B7`，没有倒填原577日志。
- 正常 g13 Draw 的 plain adapter source `8B8A89A01A64EB5F0748C35B06120FF7B217E1806389E778309877466E9B035B`、compiled `77D9DD12FB10FDDC0BC3D15E7EB1567C969857D43759260FB3234ECA4EADCCA2`；调用原 RunCandidate，probe577，真实原版资产；仅未处理异常出口，不注册 FirstChance。
- 后续 integer count 两产品使用共同 B026 probe。phase 实际是 **g14** Host `45E9231EE3AE8E5790430E3CAD654DD61148A499F736209CAB0100D36C725E44` / MVID `972264b8-e8c6-4b3d-baaa-098667526e9e`，probe `B026E6C6B85B6DB9D0A3AE87993DE4B1C4788D2524963B478716E4E07A5E2CB4`，不能归到 g13。
- 这些原件的实际 GAME 均为 Terraria1.4.5.8，SHA `960A03BFF6050CF7BE16DFC1A7B19E10FC2C4F8F835A6A3B135A50DD9E6BA2F3` / MVID `2c29f6c3-4bd9-4add-9c58-da159804e083`。

## g13 七叶与新增摘要

原 Basic、CandidateDemand、Geometry、ShapeVisual 原日志 PASS，保留各自有限范围。Basic 有真实 native starts/assisted/manual 消费与拒绝，CandidateDemand 含 PathOFF/ON 短实际 Enhanced 和 Completed Path120；不能据这些 PASS 宣称所有连续输出通过。

Observation 原 Path-only371 首结果空 FAIL。新增摘要 diff 只在失败后读一次真实状态，不重试、不重新准备、不改断言：GUC=1/worktick=1，Used6.812ms/Exceeded=true，Required120/minimum1，rawResult none，rollingFailure/outcome None、outcomeTick−1、pathFailed=false，实际371/style70/life1/ai0,0,0,1。它支持本次预算退出未发布，不能由最后抛点或 failureNone 推出热点已定位、状态正常发布或物理 miss。

Spears 四组1228首帧缺 preview FAIL，原预算12.713/7.3291/7.4026/8.2701ms；两个不同近场目标与 Aim OFF/ON 都记录，未把首帧删作预热。这组后续其它能力 PASS 不抵消四项缺口。

sealed-child-draw 原 qualified70/published66 FAIL；新增 diff 仅补最终已存在字段，Require仍保持完整70/70。新摘要实际 parentBirth1/childBirth1，parentDamage0/childDamage3，bodyDraw/childDraw/memberChecked=true，detachedPreviews44、receiveCount4、detachedHit122且hitMatched=true。可确认封存 child 独立预测与实际命中匹配的局部行为；完整 qualified70 只有 published66，四缺口不能删掉。日志的 family budget exception/UnsupportedMechanism 已保留；仅凭聚合数尚不能定位四个缺口各自帧与因果。

Geometry 的643/644 Invariant不是六棱镜：实际1.4.5.8 `Projectile.cs:34854..34861` 的644分支调用 ai[1] 的 ProjectileKey.TryGetActive(643)，`ProjectileKey.cs:106` 对类型断言；当前 GeometryChecks:98/125 直接造644、没有真实643 parent，棱镜循环:100..105 则造632。只读 blame 已核这两个孤立644 fixture在917a即存在。其日志 PASS限于孤立几何事件抽取，不证明643→644原生生子链完整；重复 Invariant 原件保留，不作为新产品回归或棱镜失败归因。

## 正常 g13 Natural Draw

CSV独立核对：standing/moving各300唯一frame0..299，完整600行。Should由frame>=2且非switch声明，独立于Contact；两组各294应显，所有缺帧保留：

| 场景 | 合格/应显 | 缺帧 | 最长连续缺帧 |
|---|---|---|---:|
| standing | 289/294 | 2,3,4,180,238 | 3 |
| moving | 292/294 | 243,292 | 1 |

七缺帧全 contact=false/visible=false/redPixels0/budgetExceeded=true，Used6.8749..15.0555ms。standing2/3/4有121点 path，缺红不是没有黄色路线的替代判定；其它缺帧 path0。facts显示standing首 shot实际已经消费 Enhanced，随后Completed预算退出令preview不发布。native births均15且assisted均15、hits16/15、landings16/15、正常gravity steps69/69、moving delta91.32001；实际出生/伤害/跳落存在，不替代红框连续门。

亲看三张原PNG：standing-first-failure 有黄线无红；standing-idle 有红框；moving-first-failure 无红。与逐帧 CSV一致；图片只作定帧佐证，不代替全窗计数。实际 Render调用 world.Draw、XNA buffer读回，plain未加诊断 FirstChance，不用旧受污染 r12/r13 做反证。

负例证据边界：CSV的12个 expected=false 行是两组 startup0/1加switch270..273；其中 moving startup1可提前有34红，并非失败，两个switch共8行红0。它们不是另12步不可达短刃窗口。源码在完整600窗后确实安排独立blade12，再最终检查 failures；但 plain当前只打印外层TargetInvocationException，日志/CSV未记录该12步或内层最终断言理由。因此本独审不能从现原件单独签该额外12步已完成PASS；这是证据缺口，不是额外产品缺陷。不能把启动排除行误作12步负例证明。

## g14 首 scan 工作量归属

亲读旧integer carrier和两套原件：g13/g14各CPU1440、counter1440，自己的tick对应逐行一致，共同case/frame也一致。全窗预算裁断12/7完整保留。仅为定位工作量取双方真实预算键匹配且未截断的共同idle10..179子集，未修改任一完整性能分母：

| 完整共同子集 | 帧数 | CellAt均值 g13→g14 | Read均值 g13→g14 | Advance returns均值 |
|---|---:|---:|---:|---:|
| standing ON | 169 | 7802.284→5583.036 | 152.154→152.154 | 120→120 |
| moving ON | 168 | 13450.768→10169.405 | 174.560→174.560 | 120→120 |

这支持首 scan 重复 CellAt 工作减少，同时本子集没有减少新格读取和完整120玩家推进。它不是每毫秒唯一归因或持续性能准入。整数 hook 自身会影响预算，已截断样本只在这个有名诊断对比中不配对，仍完整在原CPU/counter文件中。

旧carrier仍有局限：InstallCounts在try外、CpuStep只有postfix（异常step不会留下该counter行）、WriteCounts先Unpatch失败会阻断输出、计数不是thread-local严密scope。当前实际完整1440窗可核可用，不能因此泛称它任意异常/并发情况下都完整安全；不能将未知counter行当0。此既有私有诊断不改变产品评判。

## 父边界 phase 诊断

私有源 `RunNaturalPhaseBreakdown.cs` SHA `ADD26D6EFF3D54FD2F7A306328182C6711C2F0E05B150BE5DF645E950F807C2E` 静态已读：只绑定八个父边界名，CpuStep内预分配 calls/inclusive/max/异常退出，用私有 __state/finalizer；不绑定业务 ref/out/__args，不注册FirstChance、不按cell/player/future action读钟。BCL声明和实际payload预加载避免提前typed程序集绑定；DynamicMethod getters只有ldsfld/ldfld/ret，property getters沿真实纯读接口，未把clock归零。

case按实际WorldFileData对象引用替换，每次真实CpuStep递增frame；1920槽容纳原四case各最多480，自然退出仍由未改RunCpu控制。异常/零调用/缺getter/missing/overflow均输出，inclusive嵌套不能求和，传播异常边界退出不能当不同异常数。Install/Run在try内，独立Unpatch/Write收错误，finally清活跃状态，保留原runFailure；未作动态cleanup故障注入。

实际 g14 phase summary/CSV为四组各360共1440、八边界、requestedMissing/rowOverflow/caseOverflow/nested/runFailed均0，来源是g14而非g13。observed moving Rolling.Prepare inclusiveMean/frame约idle2.516184/attack2.237602ms，主要成本归在这些调用内；这个数含其子树、预算截断及观察开销，不能与PrepareSample/UpdateSampleCore等再加，也不能判2ms准入或直接锁定内部原子热点。已内联调用可漏观，零计数不等于零工作；outside324个UpdateSampleCore另列。

## 原件哈希

路径相对 `.local/aim-performance-recovery-20261012/`，旧失败原件均保留：

| 原件 | SHA-256 |
|---|---|
| g13-natural-draw-r01/natural-slime-frames.csv | A5E5366570840E9E5BF2B15FBB15FBC6DC29F4E665D99FE184C30B0D3E1C1E8C |
| g13-natural-draw-r01.log | 9BB8DB95EFB58641C299C5F012CDA09083FEEB69AC2942274D332CBE51D817D6 |
| g13-regression-observation-diag-r02.log | 91FA8C71BA8B38A7387A4B3B11B62C51B540EB1F4E65B7DAEA00D490106369BF |
| g13-regression-sealed-child-diag-r02.log | 712B9F973D0BC71307812078C6CBB8A7D6CE8B8746C4627F6E13E2AE40D1AB69 |
| g13-regression-basic-aim-r01.log | 455A57EA05BA98AB61A466CDFA14D48072A5C285F87CA06A4B1112CD2A09179B |
| g13-regression-candidate-demand-r01.log | 22F2D6414D99A086E7D4C3846A61CB6733C077C86D5E9CA013836575678C0C16 |
| g13-regression-geometry-r01.log | 58B1A1B0427992B22CC1047BBDB4B0C302AED9EB21251C78F69DB4C8611F9FB1 |
| g13-regression-shape-visual-r01.log | 80089EB2347C72002D59DF215ACB0E3C23DC221C140D5E095622BD0C769DD6CA |
| g13-regression-spears-r01.log | 6A2A10315B82C6E9E1E580F6C3B0ACFD77050F3758FE257E7653507D8DA1D682 |
| g13-natural-count-r01/natural-cpu-counts.csv | C920F63F9CD6DC44B4102F901E6641DC3214E349CFF00EAEE90670BED64A4ADE |
| g14-natural-count-r01/natural-cpu-counts.csv | 9876FC090F9F30E946689D223683DFC0900B475398B963041F031692A2CF13D0 |
| g14-natural-phase-r01/natural-phase-frames.csv | C8DC8920032FBF2ED29932591B746522E229A1CD1404E4910DFB0FE96BF27352 |
| g14-natural-phase-r01/natural-phase-summary.txt | CD694F5D49D211365F0761FF8CA6130E9E2EE6F747A52AAA750FF704E43BF103 |
| g14-natural-phase-r01.log | 6065FB9A02C3909FEC5C8AB560CAE5B5F854CE2DB42B108D6D1DE5BA589C8E92 |

本报告保留阶段/身份/证据边界；没有替代最终固定产品身份、持续性能、正常完整Draw或所有者接受。

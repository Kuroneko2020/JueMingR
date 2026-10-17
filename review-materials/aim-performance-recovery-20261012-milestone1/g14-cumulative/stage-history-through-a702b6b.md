<a id="aim-performance-recovery-20261012"></a>

## 重大性能事故恢复（2026-10-12，第二个实施里程碑，尚未准入交付）

所有者再次恢复 #114 / Draft PR #115，授权修复75a实际现装的持续更新性能事故、基础瞄准与预测Contact解耦，以及普通自然敌人的红框/实际Draw一致性。事故产品是75a1072，升级前实际产品是b038a1e；3504仅历史中间对照。本轮允许必要实现、隔离原版测试、文档和正常commit/push，最终按安全门与独审交付；不合并、关闭或发布，不操作正常游戏和真实存档。四项原有外来dirty资产继续保留且不提交。

同机锁定原版1.4.5.8、优化Release/x86的75a持续人偶基线已经拒门：叶绿镋1228仅路径、静止且不攻击，预热60/采360，总更新mean10.094553ms，对应OFF0.103540ms；水晶风暴518同源约10ms。另跑计数定位到候选与主Source重复120步、NpcMotion家族分派的巨大按值状态复制。持续Path需求最早见4850a29历史WIP，随后多次集成扩大分派，f28b的现生矛/鞭又放大成本；不能把共同10ms只归因最后一次修改。旧日志与固定载荷身份保留，不用旧API失败或伪造慢算法替代反例。

本轮冻结持续增量mean≤2ms、开启总loop p95≤对应OFF p95+4ms、p99≤OFF p99+6ms、max≤OFF max+12ms，不得连续两帧比OFF均值多≥8ms；至少预热60/采360并覆盖完整攻击/退出。OFF变化容差为max(0.1ms,旧OFF均值20%)；b038已有行为不得明显退步。超门样本不删、不为候选放宽；计数与计时分开，夹具分配只能按同harness差额解释。这是本次准入门，不是项目统一预算或FPS结论。

已实施廉价aiStyle路由保留原Known谓词/顺序、同目标同真实样本共享发布路径、独立派生攻击取消无关未来player依赖、同纯准备pass现生读取复用，以及普通冷却无下一真实消费者时避免BeforeNpc重复增强。Basic输入按当前完整目标身份/实际原版消费者借点，有效Enhanced优先；预测故障、不可达或无Contact不再否决Basic，真正选择器故障及焦点/UI/死亡/换物/失效身份仍拒绝。预览只显示与实际输入相容的假设，idle仅路径与现生sealed事实保留。唯一HostCombatObservation所有者还提供同真实更新累计6ms合作安全上限，临时耗尽退场且不记持久fault；有限检查间隔不能称硬实时上界。

私有g03四Release程序集持续叶绿镋实际移动720更新：攻击540段mean2.335479ms/p95 3.2051/p99 3.463/max5.337，退出180段mean1.113301ms，预览720/720、现生626、退出末现生0。尚缺配对OFF，不能混入退出均值宣称mean门已过。g04及最终本批私有g05基础消费者与新增Bow实际Basic/自由弧线不相容、idle仅路径保留检查全部通过；g05另通过增强失效DD2原生CanUse/Shoot与OFF/receipt身份拒绝（Host SHA173863731C388FD5B88CED965CE4DE0DBED9EE0EB41A5AD1905BE60545A7DCDC，probe SHA08298DD79D6FF6511D083961B728F773467E5FE5CFBD850BA0D6471BD99A66DA，child exit0）；它们各自只绑定对应私有载荷，不代签后续源码。私有输出与原件在`.local/aim-performance-recovery-20261012/`，不是开发包或安装事实。

整体仍进行中：最终固定候选与旧b038/OFF对照、冷启动尖峰分类、真实consumer全scope、既有工作量检查纠偏及集中独审尚待完成。自然史莱姆原版reference已有效，后继多轮候选实际Draw仍失败；不能再归为夹具未到产品。现装仍是上次安全安装的75a，尚无本轮新包、换装、FPS、真人多人或所有者验收。

首个源码里程碑已正常提交/推送380132e，外部可读原始日志、CSV、测量源码及有效自然Draw失败保全在`review-materials/aim-performance-recovery-20261012-milestone1/`（[仓库证据入口](https://github.com/Kuroneko2020/JueMingR/tree/ad0ca82/review-materials/aim-performance-recovery-20261012-milestone1)）。同g05自然原版reference600帧有效，候选r10站定291/294、移动294/294。后来发现预算所有者混用Runtime私有updateTick与Main.GameUpdateCount，同一真实世界更新完成阶段会洗掉BeforeNpc预算；g05帧末false不能排除首发预算耗尽，早期“非预算首发”归因撤回。r12/r13参数诊断钩子曾改写out输入，全部证据排除；原件及排除理由保留。g06内部ref只读传递实验同room Rolling629次1741.99ms，对应g05同629次1742.78ms，无超噪声收益，实验已撤；其固定私有产物保留，后续源码不再含该实验。

后继g07优化Release、真实天气变化与连续移动720/720，叶绿镋及日耀分别攻击540/退出180分段配对：攻击增量+1.124071/+0.954260ms，两窄场景符合冻结时间门、预算退出0/720；不据此签自然场景或旧b038退步门。原始配对CSV与日志、g07真实Draw FAIL18、无全Audit清线预算FAIL在[后继证据](https://github.com/Kuroneko2020/JueMingR/blob/ad0ca82/review-materials/aim-performance-recovery-20261012-milestone1/g07-evidence/evidence.md)，私有g07身份不冒称已提交源码最终包。

分阶段需求候选g08在BeforeNpc仅先尝试实际普通输入所需正向prefix，候选无解及最近模式无解均须同次扩满原120后才可退Basic/走远候选；Completed仍完整120展示，真实消费者独立Solve/Valid，主Source不交换候选历史。g08强制预算时钟叶能拒旧g05、接受新同GUC累计，并证不完整plan不会成为永久miss、Basic自然出生及次帧恢复；清线/noAudit仍因预算退出失败，不能调整远目标期待。g08正常Draw站定287/294、移动285/294。g09另仅在正尺寸且严格零坡面修正时省去已证无响应的第二次TileContact扫描，保全部最终区域/收尾；原版坡面5103、实体1782、视线1792、边界882对照PASS，低频同room Rolling约下降8.5%仅为诊断收益。g09一次正常Draw站定284/294、移动294/294仍整体FAIL10，预算异常观察可能有开销但未证噪声，不删冷帧或缩294；后继同自然房间无图形、无逐调用诊断的OFF/ON配对单列cold/idle/attack/退出和逐帧GC。以上实测各自绑定固定隔离载荷，不冒称最终准入包。

g09后继无全Audit清线双目标反例已恢复正确远目标并真实命中，nearest、强制预算partial/no-miss/Basic/同GUC累计/下一GUC恢复通过；六移动候选轻计数仍FAIL，不外推为全部清线已完成。共同CPU入口复用原房间、同OFF/ON物理输入，不Draw/回读、不逐调用计时、不格式化异常栈，预分配行且窗外读GC/预算；原cold与全部300+60自然退出保留。原g09稳定moving idle170增量约+2.958ms、attack90约+3.243ms，GC0且预算未裁工作，仍拒均值门；standing cold4次触限，整native+R首次大尖峰也全部保留。19个实际攻击准备方法预JIT另计启动6.8789ms后冷触限仍4次，未支持生产预JIT，实验不纳产品。分段n不足360，不代签最终正式矩阵或像素294。

风依赖专项在锁定原版核1136射弹谓词（346真受风、790免疫）、真实UpdateWeather变化60次、未来风开关切换、玩家windPushed许可正反与纯零shoot手部，并核普通slime/371当前Wind/594 WindTarget的真实Source捕获值；g09/后继probe执行PASS。首版把3507当零shoot手部的夹具断言失败保留，改为原版WoodenSword24且明确shoot<=0；未改产品或放松风依赖合同。实际未来受风机制的更广覆盖、最终正式包及独审仍有各自义务。

g12只将CellAt几何检查间隔有界为首格立即、随后最多32格，owner同GUC/nesting累计不重置，已耗尽立即抛；Read/lease/future/纯CPU仍立即检查。强制跨同GUC短scope反例、预算/Basic恢复和原版Terrain/Wind均PASS。自然CPU首次仍拒门，唯一预声明相邻g09→g12 A/B支持standing稳定idle与moving攻击局部改善，但moving idle/尾部与cold尚未过门；小候选保留不概括为整体PASS。完整未来玩家状态只读观察在moving稳定idle的20400步中有8165步B==C==D，后继固定点须据完整状态与同forecast依赖证明，不能凭XY/V跳步。新证据入口见`review-materials/aim-performance-recovery-20261012-milestone1/g12-increment/evidence.md`，独审原文及失败样本完整保全。

实际升级前b038固定旧包同原版/优化Release/预热60采360：1228 OFF移动mean0.096424ms，原有Path静止/移动mean0.279154/0.265588ms，两路径360/360且移动360/360。旧版缺新Preview/预算API明确unavailable，私有驱动适配与原失败保留，不把旧缺字段当性能反例。最终候选的OFF容差、自然场景全窗口/Draw294及冷首用仍有单独义务，未准入打包安装。
必要新检查走既有入口，不依赖本轮私有runner：`CombatAimCpu`默认调用Basic消费者/预览/安全回归；`CombatIntegrationCpu`可用`JMR_USAGE_ORIGINAL_FEEDBACK=basic-aim`取得自然使用窄反馈。工作量窄叶在`CombatAttackCosts`下以`JMR_ATTACK_COSTS_ONLY=budget-selection`（真实合作预算partial-plan/Basic/次帧恢复及仅Aim读者清线）、`moving-player-candidates`（真正连续玩家移动+候选）、`solar-natural`（完整日耀多次自然使用与退出）选取。例：在仓库根设置对应环境变量后，运行`./scripts/test-world-object-text.ps1 -ContentDirectory <锁定原版Content绝对目录> -OutputDirectory <新隔离证据目录> -Scope CombatAttackCosts`，结束删除本次设置的环境变量。脚本沿既有Debug准备/观察入口，必须与公共构建/正式Release计量串行；计数窗口可能触6ms合作门，须记录触顶事实，不能把被裁掉的工作算优化。专项PASS不代签其母scope、Release逐帧成本或完整包身份，当前新叶尚未全部验证通过。


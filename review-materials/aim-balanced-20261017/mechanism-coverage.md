# 平衡瞄准：当前机制去向

本轮基底 `112e9d629e3caf75c5d2ed9d021ac9c255b68e34`；继承[旧原版输入表](../aim-algorithm-rework-20261015/attack-route-inventory.csv)的 2944 个输入组合，不把它们当成 2944 次运行通过。下表按生产分派归类，旧表中 `motionKnown=false` 或候选 handler 名称均不单独表示不支持。当前边界来自所有者 `JMR_AIM_BALANCED_20261017` 的 01/04，历史整组精算报告保持原身份。

| 机制与实际入口 | 当前去向 | 主要阶段/参数 | 退出的普通重路径与验证落点 |
| --- | --- | --- | --- |
| 普通离散弹，`BalancedHand` 扣除先行专用后 `HostAttackModels.TryRead` | 平衡接入，`HostRepresentativeAttack.Prepare` | 当前 Capture 选弹、速度、子更新、重力/阻尼、实际发射延迟与姿态 | 在 root/ambient/完整受击捕获前返回；普通下坠及共享运动原版保护 |
| 普通箭/子弹转换、steering/impact/arrow-family/special-arrow、Onyx 主弹 | 平衡接入，`TryMotion` 和有限 `Replay` | 当前实际转换结果；主要转向、重下坠、明显反弹/爆炸接触；不预测完整随机子体 | 实际输入与清线共用代表路径；不回退 KnownFamily/ExplicitBirthBatch/CaptureForSolve |
| 1229/2624/3029/4953 四类特殊弓 | 平衡接入，`HostNativeArrowBirthAttack.Prepare` | 中央箭、海啸必要少量侧箭、天空 RAW 光标/倒重力、日暮实际阶段与出生关系 | 原版保留数量/散射；公开准备到真实消费，远距、移动、高低、强化弹速与倒重力 |
| 持续枪弓 615/630/705 | 平衡接入，真实 `BeforeProjectileWorld` → `PrepareRepresentative` → 原版 AI 读点 | 真实弹药、主弹/主箭、705 第三次 706 阶段及寿命；615 不等待 616 追踪结果 | 单主弹替代原全场弹幕快照/整组候选；真实主弹输入、阶段、出手和松手分别计数 |
| 北极 342→343、黑曜石剑鱼 367→1107/1108/1109 | 平衡接入，`PrepareSpear` | 真实父体近距、必要出生延迟/位置与子弹运动；剑鱼中值优先，必要一个合法快代表 | 不要求雪花/次要效果或完整免疫历史；远距子弹与近距父体分开验证。三种剑鱼碎片均属主要通道 |
| 其他天空/远生、Betsy/瓶、有限投掷/反弹/爆炸/引导、其余近战派生/鞭/光束/连枷 | 保留合理专用模型，实际先行 Sky/attached/HostEffect/HostMelee/Control handler | 各自决定方向、主要可达性与输入时域；不能统一当直线枪 | 不因未统一反求认定缺失；共享候选数学受影响部分补窄原版运动保护，未受影响实现/原件保留 |
| 普通/省力悠悠球 | 保留专用导航/持球/自由释放模型 | 一格出口、真实尺寸/惯性、父球最后合法输入、自由球不再牵引 | 新普通箭结果不获得悠悠球控制权；复用原保护并核共享输入接缝 |
| 清线、Path-only、Aim-only、Both | 平衡接入相同 `PrepareBalancedPlan` | 最近严格距离；清线在有限工作中取最近正解；受阻/未知区别，无正解保距离兜底 | `CandidateMissCurrent` 和 `ReadFlightCore` 对迁移机制早退；同阶段候选结果可共享，实际许可重新核验 |
| NPC 未来、滚动续算与绘制 | 本轮不改 NPC motor；保留现有曲线/长尾/身份与停止合同 | `Source`→`Rolling`→`Cache`→`WorldLayer`；短刷新不挤掉有效长显示 | 绘制只消费 NPC publication；不为展示展开武器世界；本轮模式/普通 Main 保护 |
| 哨兵、工具、非资格输入 | 本轮不影响既有分流 | 哨兵 CanUse/Shoot 保真实鼠标；UI/失焦/取消/关闭撤销输入 | 原版放置和已有安全检查，关闭退出领域工作 |

平衡点、实用可达性、完整 Contact 与实际伤害各自独立。已接受的散射、伴生追踪与次要碎片近似不列作欠交；真正未接入、主通道反复 Basic/打偏或持续触限仍是核心缺口。实际运行状态见[证据入口](evidence.md)，此表自身不签运行 PASS。

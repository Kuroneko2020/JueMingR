# #114 / Draft PR #115：核心瞄准优先恢复交付

**阶段源码与实际 ZIP 均获独审批准，已备份换装并核验。** 完整187资格仍为 `INCOMPLETE`、`releaseEligible=false`；#114/#7保持OPEN，PR115保持Draft。未启动所有者游戏，体验、完整原版帧循环/FPS和多人未验收。

## 固定产品、包与安装

产品 **A2 `92289a1d65e6dfa6068d12d89d2eeec768d26f87`**；基线 `01b03386cc288fe8f8a773cecd3a0de564182472`。source fingerprint `4818A20177E1099BE939658E46198C0AC3ECEB75B824505C33333A8869119990`，input fingerprint `70909951125248E373B91AF4072BB29421725D91CB2E5A2A181731B6E4F224A9`，Release Host SHA256 `A2BA4A442354A9FADA89D13B9631B584D23592CA8025774BC4D412BB69DC1557`。完整普通优化Release/net472/x86阶段包包含Bootstrap、Worker、四模块、Harmony与安装/恢复工具，无测试probe或原版游戏资产。

ZIP位于 `outputs/aim-priority-recovery-20261013-92289a1/JueMingR-Continuous-Processing-92289a1d65e6dfa6068d12d89d2eeec768d26f87.zip`，**2,123,762 bytes**，SHA256 **`DB59720908271C9C9A0DFA84C74111B6614C52C879913BD9F8F4AF8895E5D208`**。实际17成员、10payload逐stream核验；正规PS5 reader正控和14拒绝、stage build与原包verify均通过。源码独审 `APPROVED_FOR_STAGE`，实际ZIP独审 `APPROVED_FOR_INSTALL`，两原文分别保存，批准边界没有合并。

原75a的12件受控文件已备份到唯一 `.local/aim-priority-recovery-20261013/install-a2-20261013-052559/backup/` 并逐件核长/hash。原75a包正规Restore返回 `RESTORE_COMPLETE`，实际ZIP解包安装器返回 `INSTALL_COMPLETE`，均exit0。目标仍 `F:/Games/GamePlats/Steam/steamapps/common/Terraria`；新10payload/所有权门吻合，manifest SHA `0E1984A27CA83E91D8240E7D352FF996E8DB19213BB431D2909694DCB2031750`。真实回执同目录 `installation-receipt.json`，SHA `495A3A5E51D9121F5BAD776820B9E2665E4427A3B0194B9420D8B0CE1D01AE18`。1745项角色/世界/设置/R数据及相关保护文件前后相同、保护根未新增文件；158份历史原件和4外来资产保持原路径原字节。没有强杀或操作所有者游戏。

## 实现与关键实际消费者

同样本预测按真实动作所需前段延长，发布不可变完整点；预算暂停不冒充机制终点或完整无解，尾段中断不破坏已完成前段。120仍为希望范围，显示满尾每5更新刷新、capture最多4龄，显示窗口不授攻击权限。公平候选工作在3ms软限/6ms同GUC总额度内轮转，重大变化的未完成工作持续排队；实际BeforeNpc仍新验证identity、phase、依赖和输入许可。r11恢复aged idle `WhipOwner.Current` 与出生输入撤销门，保护无live Flight时的手部负控。

实际A2 ZIP四Release模块/Harmony直接从解包payload加载，固定私有probe单独绑定；`candidate-demand-full`、`basic-aim`、`natural-slime-pixels`均exit0，真实原版Update/ItemCheck/Shoot/AI/Damage未被代替。

- 五压力场景是“2阻挡候选+1远者 /24阻挡候选+1远者”，Both/Aim-only及24个初始墙后候选持续+.04移动；第140帧仅near2移近，真实击退继续保留。各180窗5机会，远者Enhanced实际3/2/2/1/1，不称5/5；移近后的frame160均实际near2 Enhanced，Contact Tick21/21/21/23/23，当前GUC receipt/contact/point/identity关联全真。
- 叶绿镋1228近Contact6、派生39/39、玩家移动33/26实际Enhanced并产生原版伤害；最远Basic和安全负控保留。弓的适用旧证据为40/80/120远target4 Contact57/57/62，160新近target2 Contact46，预测Tick不是实际首击时刻；原r02失败保留处置。
- 实际ZIP像素组：站定**287/294**，missing7、最长5、3缺口串，idle172/178、attack89/90、restore26/26；移动**294/294**。standing idle2硬退出7.4517ms与缺失未删。严格身份、接收几何、8px红框、Draw零future/terrain与不可达反控通过；有限短缺按本轮合同接受，未恢复逐帧满额门。
- 自然枪98增强补证仅为先前固定适用字节上的 **moving Both15机会/15出生Enhanced**，Contact2..5，同GUC完整关联；正式长计量的57次坐标辅助借用不称57Enhanced。新实际ZIP关键消费者另有自己的身份。

## 持续成本与冷边界

公开两份summary.csv是产品 **A `444e28b0265a81514f8ba2303feaab0381db9f77`** 的普通优化Release原窗口；A2仅为既有阶段reader增加UTF8 BOM。生产compile inputs完全相同、10713方法IL/tokens/signatures/locals/EH记录零差，独审明确 `REVIEWED_REUSE`，不声称A2重测或PE/metadata/resources同字节。Debug保留真实7576/r11原record/compile输入身份。

统一p95/p99采用nearest-rank `sorted[ceil(n*q)-1]`。原raw、冷峰、GC和完整分母保留；旧G13/b038原floor日志不改，先前未封存floor派生被明确从raw重建为历史副本，不称原始封存文件。

| 普通场景 | 窗口及实际结果 |
| --- | --- |
| 枪98八模式Update | 8×1290=10320行；每case idle/use/release均420，全原窗保留，稳定段各360。moving Aim attack fullUpdate均值0.5566ms/p99 2.1379；Both1.4201/p99 5.3961。所有case首次按下420与出生420同更新。 |
| 实际Draw分量 | 3×1290=3870行，moving OFF/Path/Both；Both attack同帧Update+render均值1.1767ms/p99 3.8518。包含World.Draw callback和图形批次提交，无GetData/PNG读回；不能把两个p99相加，也不是完整原版Main.Draw/FPS。 |
| same-b038参照 | 1228+Dummy(2,55,40)，warm60/采360/不攻击；当前Path站定约0.419、移动约0.486ms，旧0.279154/0.265588，有限增加约0.14/0.22ms。不是枪史莱姆场景，旧未暴露preview字段不算零。 |
| 普通鞭类窗口 | 4672→841，原版训练人偶NPC488，Dummy(2,49,40)为slot/x/y。实际配置Both（私有mode字符串aim），Path/Aim/Marker/ClearLine开启；OFF关闭Path/Aim、Marker仍开且人工朝目标瞄准。1080帧idle/use/release各360，5自然press/5出生/5命中。 |

鞭类idle均值0.5206/p99 3.0721，use完整冷窗2.3352/p99 6.1385，release0.4646/p99 2.7127ms；use红框358/360、最长空白2，其余720/720。真实活跃145帧均值4.2824、p99 **99.071**，包含冷成本；首周期之后真实活跃116帧均值1.9931/p99 5.5062/max6.1385，显示116/116，无连续超16.667ms。没有以单次出生后长持的空闲均值代替持续使用。

冷首出生frame360 total99.071/completed10.9224ms，下一帧completed6.6288，两次硬退出；首命中376 total239.3638/completed2.9364。OFF同武器实命中反控出生73.6784、首伤187.3549ms，说明原版冷路径存在，不能严格相减归因。后续press435/510/585/660 total4.889/5.506/5.095/4.681，动作仍当更新完成。枪Aim420/427/428冷尖峰和站定Path306的GC0/GC1增加、10.8233ms准备scope/11.0784ms整步均保留；不宣称全程零尖峰。

主调度与独审接受受测持续成本和恢复表现；依据是实际按需工作/持续窗口/真实Enhanced和Draw，不以6ms预算、旧2ms门或75a降幅签准入。

## 必要13范围与失败处置

[validation-map.json](validation-map.json)绑定13项精确执行原件、40修订、5当前检测消费者及复用限制：[failure-dispositions.json](failure-dispositions.json)保留16项处置和全部旧失败，未执行旧整组不冒称PASS。architecture-combat/candidate-demand/basic-aim/natural-draw/natural-cpu/continuous-reference/draw-cpu/integration/attack-costs/navigation/impact-draw/whip-terrain/original-feedback均具当前或获审适用复用依据。

AttackCosts仅九ordinary-work+SameEpoch、预算/Incident、完整导航、有限beam旧原件及当前母反馈的精确组合，不称旧whole PASS。冷PathOFF首轮原4idle/首按0：0/17/34当前合法Basic且far unknown，第51帧首次far Enhanced；同进程第二轮无暖射、原4idle后首按0直接far Enhanced并实命中。不称冷清线首发全增强。导航分场只用真实新WorldStep区分独立前提，同前提4次Prepare的零重搜门不变。合成纯值路径的Terrain空读集只初始化尺寸，不签原版Source/地形正确性。

A2工具失败处置明确：PS5把无BOM脚本的4中文白名单读错；仅增加EF BB BF，余14991字节完全不变。PS7诊断有不同source fingerprint，未拿换shell或旧FP绕门。所有审批原文及actualZIP/安装回执均保存于本任务.local，公共资料不复制个人数据、原版DLL或整会话。

由所有者自行启动游戏。先检查能进入、F5能打开、正常退出，再体验移动瞄准、1228派生提前量、红框/路径通常可见及关闭后归还真实鼠标；这不是已完成验收。

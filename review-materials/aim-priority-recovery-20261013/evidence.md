# #114 / Draft PR #115：优先级恢复外审入口草稿

**PENDING / INCOMPLETE；releaseEligible=false。** 这是可提交的开发事实快照，不是 stage 准入记录或独审批准。#114、#7保持open，PR #115保持Draft；原187完整资格未跑。

## 固定身份和授权范围

授权产品基线为 `01b03386cc288fe8f8a773cecd3a0de564182472`。保存点为 `7576da5c7b7b6e455bbe8466b7623571ee2d103a`，当前r11是此提交加dirty改动的固定载荷，不能称最终commit。

r11 Host SHA256：`AC8F1AA5570C1C11676F0AD90B31ECD547B4A39097155BFD3BD5DBA2228C6402`；MVID：`8ff98d66-d40d-436b-b65c-36071e186b69`。相对7576的产品修复仅Preview aged分支恢复完整 `WhipOwner.Current` 和 `Obstacles.BirthInputsCurrent`；idle hand的Flight为空，先前live-flight负控未覆盖这个独立owner。

最终产品A、source/input fingerprint、r11当前性能及 `APPROVED_FOR_STAGE` **PENDING**。delivery先固定产品A，最终文档B再记录A的实证；公共文件不自指hash/commit，不因纯文档更新重包。

实际发射/持续控制优先同样本按需prefix。可选Path计划120未来，5更新刷新、原capture最大年龄4；显示window不进入攻击Cache或授输入。候选短无解/预算pause仍unknown；显著未完成工作换新尾票并保工作资格，普通公平仍保。旧hint只调度，下一真实BeforeNpc须重验证。

## 已观察的有效范围

r10 architecture-combat、完整CandidateDemand、Basic、自然Draw与sealed-child seams局部通过。压力包含2/24阻挡、Both/Aim-only、持续移动及新近者**真实Enhanced消费**，不是只看World选中。

7576普通CPU八模式与same-b038参考原件可作适用性复用候选。update、Draw callback、render和paired CPU分开；全部冷尖峰/GC/硬退出/尾部保留。自然输入补证仅moving Both的15机会/15出生均Enhanced。弓40/80/120远target4的Contact Tick为57/57/62，160新近target2为46；预测Tick不是实际首击时刻。**不把这些旧载荷数字称为r11当前性能或最终成绩。**

r11 `candidate-r11-integration4.log` 完整CPU母组EXIT0，含5Usage、Hand、Spear、Whip、6Ranged。保原出生/方向/伤害/孩子/Solar约束和固定75/45等分母，显示短缺与最长空白完整记录；成员、输入和Draw反控不宽放。idle-whip graphics成员负控与WhipTerrain12组合通过。

PathOFF同进程有限两轮对照各保原4idle、首按0和160/160观察：第一轮0/17/34为Basic、far未知且硬退出，51首次far Enhanced（新BeforeNpc18点、Contact8，Used2.8349ms），4机会4birth/hit1；第二轮首按0直接far Enhanced（fresh18点、Contact8、Used1.3705），1机会1birth/hit1，无暖射。**不证明cold首发清线PASS，也不代表全部冷环境。**

## 13项映射

[validation-map.json](validation-map.json)列准确scope、原件相对路径/length/SHA256、已观察结果与待补。全部最终disposition仍PENDING；CURRENT/REVIEWED_REUSE为提案，需独审绑定产品A。

| Scope | 当前事实与未决 |
| --- | --- |
| architecture-combat / candidate-demand / basic-aim | r10局部通过，最终复用适用性待审 |
| natural-draw / natural-cpu / draw-cpu | r10/7576固定原件；r11当前性能PENDING |
| continuous-reference | same-b038准确结果路径/身份绑定待delivery补齐 |
| integration / original-feedback | r11完整CPU母组通过；最终身份与独审PENDING |
| attack-costs | 仅明确通过叶；旧whole失败保留，替代scope及剩余原反例待映射 |
| navigation | 原15场/真实Damage/同tick4Prepare无重搜/age15退役与+5恢复EXIT0；最终绑定待审 |
| impact-draw | r11完整实际图形入口EXIT0；主像素/故障/控制消费者/自然反馈与显示负控均保留，最终绑定待审 |
| whip-terrain | 12组合直接机制oracle通过，非全部NPC motor |

## 失败处置和交付前待补

[失败处置](failure-dispositions.json)保留：aged idle-owner产品缺口、bool Draw delegate、SetDefaults初速残留、可选显示/当前执行身份、family不同请求与绝对时钟、Capture合法地形读取与strict Project/Draw0、cold/no-demand、导航Basic/helper及尚未闭口whole scope。旧失败原件不删除、不覆盖、不重标为PASS。

导航分场只把多个独立恢复前提按既有BeginWorldStep/Sample/Combat.Sample区分；不抬预算/清Work/放宽HasPoint，原15场真实Damage、同tick4Prepare无重搜及自然15/+5恢复均EXIT0。

Impact使用RunImpactGraphics调用NativeCombatImpactVisualChecks.Run，属组件图形验证，旧log header不定义scope或性能。最新失败的低Used与DisplayFalse由合成Cache.Publish未初始化地形lease尺寸解释，正式CellLease.Current拒绝；这是TEST_CORRECTED夹具前提，不能归因冷预算。初始化lease后主marker四相机/重力/缩放、8px hollow、真实Shot、局部fault恢复及receiver/ammo/wind负控已通过；随后Controller旧失败仍保留。execution旧marker撤销与独立Preview重新Capture已分别验证；tile/string只允许真实当前Basic或null、拒旧Enhanced，红框立即撤销且Draw0。完整入口EXIT0，后部自然Draw站定294/294、移动292/294（最长1、2缺失串）。

正式本地准入按 `scripts/workload/AimStage.Development.ps1` 的 `aim-priority-recovery-development-20261013-1`；本稿的PENDING不能满足准入。delivery补最终A身份、当前性能、13scope收口/替代映射、raw哈希锁和独审，再填写最终证据。

原件保留于忽略目录 `.local/aim-priority-recovery-20261013/`。公共文件不复制个人绝对路径、原版DLL、用户配置/世界或整会话；原日志含本机环境路径，不能原样视为公开资产。

本稿不授权合并、关Issue、Release、包/安装；不宣称完整资格、正常游戏FPS、真人体验、多人或所有者验收。

当前ordinary-work明确九场+SameEpoch20通过：关闭/无目标/不适用零solver与future，Aim-only idle零future，同样本NpcStep只24、Solve2；同真实input epoch后20回调零PrepareCore/Solve/NpcStep。旧switching即时world Contact失败不改写，真实switch/repress/recovery由完整母组及正式自然窗口承接。两份公开summary.csv目前仅7576原候选快照，产品A正式字节的最终结果仍PENDING。

# 独立窄增量审查原文：测量与观察测试（08616eb）

结论：**NEEDS_FIX / INCOMPLETE**。自然 CPU 移动持续增量已越过冻结2ms门，实际 Draw 正窗此前仍失败。本次另外确认一项测试观察器失败清理 P2，修正尚未在本报告中签字关闭。

审查者沿用实际已核 `gpt-6.1-sol / high` 的 `/root/independent_review`。未参与实现或期待起草，没有运行 CPU/GPU、build、测试或 Git 写入，仅保存本独立原文。七个产品文件复用先前 `incremental-review-g09-20261012.md`（SHA B2D803C60BB3C34741B5B05526A34EEEAB8CF29FE8A826F4065958DBBE68ED66），不重演事故源码全史。本轮读取固定提交 `08616eb8797a5ab9f9124f0d3d794fc2c9fa26d0` 的新增测试接缝。

本次范围：`NativeCombatNaturalImpactChecks.RunCpu`、`ProbeGraphics` 像素复用、`NativeCombatWindDependencyChecks`、`NativeCombatGeometryChecks.SampleObservation`、`NativeCombatShapeVisualChecks`，及这些接缝直接使用的 Usage future-Step observer/原版方法和原始 CPU summary/CSV。成功 CPU 原件使用 probe SHA `0DBA75003B0D19C3163E51E892F1FAD96280BA3605E4E887CE2D7F17072FF5E0`；后继共同 probe 的 C5E25 标识属于后继载荷，不能回写原 r01 身份。产品仍实际 g09 Host SHA `957A5463BD9399C597E24A4218BEEAB94268759528C0313F69765FCF879372C5`、MVID `a220125c-f55e-4fb9-acdd-3343b96c1b30`；源码提交、私有测试载荷与普通包/安装分别记录。

## 确定 P2：异常路径可能保留观察 Hook

`tests/NativeWorldTextProbe/NativeCombatNaturalImpactChecks.cs:340-344` 的 finally 先 `WriteCpu`/`File.WriteAllLines`，再恢复 film、清静态 player/receiver/cpuMode、`audit.UnpatchAll`。输出文件被锁或磁盘写失败时，342抛出会跳过343的观察器清理；已安装的 MoveBefore 可继续修改该 player 的原版输入。另154-158已安装 Hook，而165构造 CpuBindings、167初始化 Monitoring 等仍在168的 try 之前，绑定前提失败同样无法进入该 finally。主调度已确认并交原 owner 修正，独审未起草代码或期待。该问题不否定已经成功写出的 r01 计时原件，但不能签成失败生命周期已闭合；须对固定修正差分增量复核。

## CPU 窗口与同条件事实

- Stopwatch 237-239完整包住 CpuStep：真实输入映射/Trigger更新、CombatSample/物理鼠标采样、真实 GUC 单次推进、原版 Player.Update、全部 active NPC.UpdateNPC、实际槽序 Projectile.Update、真实 UpdateRuntime/UpdateShell。绑定 setter 仅代替已有 BeginWorldStep 的同一次 GUC 增量，不替换 AI 或增加世界更新。
- OFF/ON 都使用物理世界点 `(930,1199)`、相同使用区间180..269/274..299、同40帧移动节奏、同原版地图/seed/武器/生命与保护前提。OFF未偷偷瞄目标来压低差异，也没有把 ON 真实伤害后的 NPC 状态还原；伤害与击退造成的自然差异属于该运行场景。
- CpuStep 没有 player/RNG 状态克隆、逐出生字符串、逐模型计数探针或 Draw/GetData。原生 Birth/Hit 的轻量标量观察保留并在说明中可见。GC、budget、原始 preview fact和CSV格式化都在计时窗之外。私有 RunNaturalCpu 适配器不订阅 FirstChance；正常成功 r01 的实际载荷头可核。
- CPU 模式返回前不执行像素断言。非CPU路径仍是300帧/独立 Should294和不可达12帧负例，未按 Contact 是否存在缩分母。复用 Render-owned canvas和一份完整960x640 Color[]仍执行真实 Draw/GetData，不能将 CPU rawContact 当实际红框通过。
- 首冷0..9、全部180idle、90attack、4switch、26restore以及至少60原生release均保留；若未自然退出可到180release并如实输出未退出，没有强制 Kill 或清计时器。80空 outer callback不是玩家/NPC/预测预热，报告没有冒充已暖场。
- BudgetTick 不等于 GUC 明确输出 unavailable，不能把旧末段预算值套在当前帧。`rawPreviewContact` 仅为已准备原始事实，不是通过全部当前许可后的 display verdict。

直接读取 r01 `natural-cpu-frames.csv`：四case各360行、frame0..359，release60且 exitComplete=True；每case原生出生15，ON辅助出生15/15与伤害16/15，OFF反向物理鼠标伤害0。站定ON保留4个budgetExceeded，未删冷帧或触限行。summary 与CSV读取哈希分别为 `FCAF552D3B7B6C945FA11E5DB900B424ED9B08A80D808ACF50A74368DDBB6A3F`、`367C9792105AE8691B9FCF4421EF8814F738B6A3D21BD73AEC45A6CF0FFDDBCE`。

移动 idle：ON3.048046 − OFF0.088486 = **2.959560ms**；移动 attack：ON3.340586 − OFF0.097856 = **3.242730ms**。二者均超2ms。站定OFF的681.2298ms首冷、105.6114ms首次攻击与ON185.5764ms攻击长尾全部原样保留，不能借较大 OFF 冷成本把 ON 持续工作签成合格。此专题是事故有效诊断和失败证据，不因驱动 exit0自动成为冻结性能门 PASS，也不是完整 Main.DoUpdate/FPS。

## 原版风与观察计数

Wind r01 的错误零shoot夹具失败保留。r02使用原版 WoodenSword24并明确shoot<=0；实际1136原版 ContentSamples 对照中346敏感、790免疫，逐一核 ShouldUseWindPhysics type override/style；实际 UpdateWeather有60次变化。潜在风 gate OFF→ON失效、相关/无关 lease、真实 player WindPushed 正反、普通史莱姆无关风、371现风和594目标风 capture 都有实际原版/Source支撑。覆盖不能外推全NPC的动态实机体验；其余 Wind读点由已审产品静态范围说明。

SampleObservation 以实际 `CombatGeometry.Projectile` scope和 `ProjectileCollisionGeometry.TryCapture` 接缝计 projectile，与原 DEBUG增量位置对应；Melee取成功 Sample返回与Attacks玩家槽，不把拒绝调用计入。ThreadStatic caller owner与自身 __state栈分开，观察器不绑定业务ref/out或__args；唯一 out 是 Harmony 自己的 __state，不回写生产参数。读取计数要求活跃scope，缺DEBUG字段不当0。Geometry/Shape的using在正常返回和RunCore抛出时调用Dispose，局部 Harmony ID独立卸载；Draw未来步数也要求安装了明确观察scope。未发现这些计数替代削弱行为/像素断言或主动多运行AI。

## 后继复核

当前保留自然持续成本失败和原 Draw正窗失败，等待对应窄修及冻结实证。只需复核 NaturalImpact 必达清理修正、后继载荷影响和仍缺的必要准入；不重跑或重审已有效无关部分。未完成独审/性能/自然正窗不能准入新普通包或安装，不授权合并、关闭或发布。

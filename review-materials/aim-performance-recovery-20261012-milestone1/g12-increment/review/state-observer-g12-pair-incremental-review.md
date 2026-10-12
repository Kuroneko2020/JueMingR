# 私有状态观察器及 g09/g12 r02 配对独立复核

2026-10-12；`/root/independent_review`，沿用已核实际 GPT-6.1 Sol / High。本次只读源码与已有原件，未运行、构建、计量、修改产品或测试期待；仅写本独立报告。不审后续 g13 候选实现。

结论：在当前一次性独立 receiver 的实际范围内，未发现观察器写回业务状态、隐藏已观察失败/缺样或卸载链的确定 P1/P2。状态计数可作为有限诊断原件；不能证明通用缓存等价或性能准入。g09→g12 同 probe 配对收益混合，持续门仍 **NEEDS_FIX**；最终产品准入仍 **INCOMPLETE**。

## 观察器静态范围

所读私有源 `.local/aim-performance-recovery-20261012/RunNaturalStateCount.cs` SHA-256 `65CB4FF8A51367EDB2F4953EE45DAB06B1F8BC9734945D7C146C2386376B64B0`。

对照真实 `RollingNpcPrediction.AdvancePlayer`：三项 BeginWallFall/BeginGravity/BeginWall 之后，普通/hover 分支调用 Horizontal；碰撞和 AfterFluid 后调用 ClampBorders；通过后调用 CompleteItemCheck。四个采样分别为 Horizontal prefix 的 B、ClampBorders prefix 的 C、CompleteItemCheck prefix 的 D 和其 postfix 的 E，位置符合输出声明。提前 handled、已有 settled、复杂分支未执行 Horizontal 等不能产生完整 BCDE，观察器以未观测列表示。

所有业务 `PredictionPlayerMotion p` 均按 typed value 读取，不绑定业务 out/ref 或 `__args`。只有 Harmony 自有 `__state` 使用 out；Returned 按值读取 `__result`，Leave 返回收到的原 `__exception`。状态只复制到独立栈/计数数组，并调用完整 `SameSample`；没有归零 action clocks、改变预测策略、替换预测状态或吞业务异常。详细字段反射/格式化在窗口之后。

AdvancePlayer 的 prefix/postfix/finalizer 管理独立 scope；最终清理清除对应栈项并回退 depth。非 owner 线程被明确计为 otherThreadCalls，不写此 scope。Install 和 RunCpu 都在 try 内；部分 patch 安装失败也走 Unpatch，卸载与 Write 独立收集异常，清理栈/引用必经 finally，原 runFailure 在 cleanup Aggregate 中保留。观察器 patch 有实际运行成本，本运行绝不能作为性能门的计时证据。

已知限制仍须保留：Harmony 目标身份存在不证明所有已内联调用都可见；前 16 条详细差异样例只有有限诊断价值，不能当全部变化清单。2048 行容量及 16 层栈均有显式边界：row overflow 会明示；栈溢出由观察器抛异常，会影响该异常运行，不能泛称任何执行下都纯无扰动。本次原件 overflow=0、otherThreadCalls=0、stepFailed=0，没有发生该边界。此工具为单次 receiver，不是已证明可反复装卸的常驻产品观察功能。

## 启动与实际分母

首启动 r01 的 CLR before-Main 依赖装载失败原件仍保留，不能算完成窗口。限定 `StateBootstrap.cs` 只预加载指定 payload 的真实 0Harmony/JueMingR DLL 和实际 Terraria.exe，再调用同一适配器 Main；没有改状态观察器语义。bootstrap SHA `06007D636BFD40119B6EBCB5EC073EED964B32BF1DAB3234A4FEA3ECCE52E4AA`。

r02 日志实际装载 g12 Host `440FAE345E3C066887EA5C83B4382D1AA8410FBAD1F709938D26CE342941B5D2` / MVID `ed65a495-20c8-459e-a48b-227011eb6aff`；probe `FA8F05FE09A38DDCDB96F0BB9C73AE5AF2D94EF21623E3216924EC25AFAA94DB`。实际 GAME SHA `960A03BFF6050CF7BE16DFC1A7B19E10FC2C4F8F835A6A3B135A50DD9E6BA2F3` / MVID `2c29f6c3-4bd9-4add-9c58-da159804e083`。

独立读取 counts 和本运行 CPU CSV：四组各 360 个唯一 frame0..359，按 case+frame+tick 对齐无差异，四组 stepFailed 均 0；每行 calls 都等于 complete+unobserved+failed+missingTail，没有漏记到未分类集合。原预算截断/零调用帧仍在分母中；该 scope 不单独命名预算异常，而以 failed/原 CPU budget 列共同解释，不可把 failed 当物理 miss。

idle10..179 的有限结果：

| 组 | calls | 完整 BCDE | 无 Horizontal | failed / missingTail |
|---|---:|---:|---:|---:|
| standing OFF | 0 | 0 | 0 | 0 / 0 |
| standing ON | 20400 | 170 | 20230 | 0 / 0 |
| moving OFF | 0 | 0 | 0 | 0 / 0 |
| moving ON | 20400 | 20400 | 0 | 0 / 0 |

moving 完整调用中 fullBC=fullBD=8165、fullDE=20400，pressed=20400、pressedZero=8165，pressedZeroFullBC=pressedZeroFullBD=8165。这证明该实际子集观测到完整 sample 不变，并非所有按键/速度为零都可省略运动。standing 的 20230 无 Horizontal 已如实单列，不能将其全数写作四边界不变；前16详细差异全部来自 moving 首帧，不能代表后续全窗。OFF 零调用也不能证明未被调用的其它函数无成本。

## g09/g12 r02 同 probe 配对

读取 `g09-natural-cpu-r02-paired` 与 `g12-natural-cpu-r02-paired` 原 summary、CSV、父日志，双方 probe 均为上述 FA8F05…，分别实际 Host957A5463… 和440FAE345…，原版身份一致。双方各四组 ×360、frame0..359，完整保留 cold、idle180/attack90/switch4/restore26/release60，末帧 nativeExitComplete 均 true。状态观察运行另列，不混入这对无状态 hook CPU 原件。

| moving 段 | g09 ON−自身 OFF mean ms | g12 ON−自身 OFF mean ms |
|---|---:|---:|
| idle 全180 | +3.294181 | +3.400000 |
| attack90 | +3.836070 | +3.100550 |
| release60 | +4.262712 | +2.997535 |

idle 增量略升，attack/release 降低，三者均超过冻结 +2ms。g12 moving attack p95=4.3611 对自身 OFF0.2701，增4.0910ms，仍越 +4ms；moving idle max14.1685 对 OFF1.1463，增13.0222ms，仍越 +12ms。该 GC 帧原样保留，未取得独立噪声分类。standing attack 原冷 shot241.9501ms 等也保留。

standing idle10..179 补充增量从 +2.267075ms 降到 +1.713296ms，可以说明这个已命名子窗口的变化，但不能替代完整 cold 主分母或判整个准入。总体仅可称有限 A/B 的混合收益，不把动态 native 伤害后世界未来强制改成一致，也不据一次配对穷尽机器噪声/作用量根因。旧 g12-r01 的失败和本次新的失败均保留。

## 原件哈希

路径均相对 `.local/aim-performance-recovery-20261012/`：

| 原件 | SHA-256 |
|---|---|
| g12-natural-state-count-r02/natural-state-counts.csv | B0E1C34ADB3E8E7CA66E627679CDD999E55FE917B45ACCCD4189EA4C7687BD53 |
| g12-natural-state-count-r02/natural-state-differences.txt | BE58016528E8CC4F8E396675A97F26FF527F47687180C6C3F9188013E5AA36A2 |
| g12-natural-state-count-r02.log | 75733D6C501E5B4566E2367C7BF1601438613CCC29897238451A386A366F96A2 |
| g09-natural-cpu-r02-paired/natural-cpu-summary.txt | 35D759C954926B39B2F93A3604C81B83DFB64DD48D78D14A43CD4930FC0E327F |
| g12-natural-cpu-r02-paired/natural-cpu-summary.txt | 4025DE047D414F24B9CBC2B39406BFF6E318B5BCB6B074B996550212ABC00ABD |

本报告只保全观察器及该有限配对事实，不替代后续冻结 g13 成品独审、自然 Draw 全分母或最终性能准入。

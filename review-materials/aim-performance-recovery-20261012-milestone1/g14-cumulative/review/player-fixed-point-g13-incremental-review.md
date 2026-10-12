# g13 玩家物理固定点独立增量审查

2026-10-12；`/root/independent_review`，实际 GPT-6.1 Sol / High 已核且未改变。本次只读审查，不参与实现或测试期待起草，没有运行构建、测试或 CPU/GPU；仅写本报告。

结论：当前 g13 增量未发现新的确定 P1/P2。旧 g12 固定点遗漏 timer/Gfx 推进已有真实候选反例，g13 的完整状态证书在静态和 14 个有限机制叶中可关闭这个窄问题。持续自然 CPU 仍明确失败，不能判整体通过：**NEEDS_FIX / 最终准入 INCOMPLETE**。

## 固定范围与身份

基点 HEAD `a702b6b17fb1814804f364ac52058b9ad84a1152`。仅审其后未提交的 `RollingNpcPrediction.cs` 固定点增量、新 `NativeCombatPlayerFixedPointChecks.cs` 及 `NativeCombatPlayerPolicyChecks.cs` 正规入口一行接线，并补读直接依赖的 Continuation、PlayerMotionContinuation、PredictionEnvironment、Ice 接缝。未重审旧全仓或未变批次。

| 源文件 | 本次读取 SHA-256 |
|---|---|
| src/JueMingR.Features/Combat/RollingNpcPrediction.cs | AB25AD9E44B3754C6FC03314E63A0C16E097129D9A252A4E593E4D2BC81B45AC |
| tests/NativeWorldTextProbe/NativeCombatPlayerFixedPointChecks.cs | D526F3BF943321A2C78CEBEC52E4EECA3006469F95D1C0C4D66D3F7101D0B1BD |
| tests/NativeWorldTextProbe/NativeCombatPlayerPolicyChecks.cs | D69F554461474A3C396A81DCD1745446197854CF5F51BC17FEDDAD336B01C4E9 |

实际 g13 日志 Host SHA `F1072D318426809C9360CB45C7B85B7013BE865E8C13637C8B0B344A71AC6685` / MVID `64eed61e-f77b-4ce3-bfa1-6232764b4178`；Features `99B10CB658251BE90863AB68AD4AF1EB7CE2C3BC9EAE250C80EADD82DF892E2D` / MVID `fa1a7ba6-b3bb-4c9e-a9ec-77b50bff5b61`。sourceinfo 是 `uncommitted-g13-player-fixed-point`，不是正式提交身份。CPU probe `5770027B129FFDBC3F6AC4C179092E9CAD2AD106C3DC5C3E16D678C921E07656`，与以前 FA8F05… 载荷分开记录。原版 GAME SHA `960A03BFF6050CF7BE16DFC1A7B19E10FC2C4F8F835A6A3B135A50DD9E6BA2F3` / MVID `2c29f6c3-4bd9-4add-9c58-da159804e083`。

## 代码判断

旧 settled 条件只检查位置/速度及释放控制；达到此条件后跳过 Horizontal/Vertical、terrain 和 AfterFluid，会冻结同样静止身体上的 WetSlime、SwimTime、GfxOffset 等实际逐步状态。g13 `RollingNpcPrediction.cs:373..375` 改为完整 post-prefix 输入和 post-fluid+border 输出 `SameSample` 相等，才建立证书；普通非 Complex/非 Hover 限制保留。已按住运动但被真实墙体挡住的状态允许由实际完整证明取得资格，没有按物种或按按键直接授权复用。

`325..328` 的 BeginWallFall、BeginGravity、BeginWall 每步先执行，失败/handled 清证书。`SamePlayerPhysicalInput:312..320` 只在历史 VALUE 副本对齐已消费的 GravityAction/WallAction；产品实际 p 和一般 SameSample 未改。当前代码这两索引的读取属于相应 prefix 的已消费时间线，Horizontal/Vertical/terrain/AfterFluid/Borders/ItemCheck 不再读它们；gravity、Wall、health、fluid、control 结果仍完整比较。

复用条件 `336..339` 同时要求 terrain 引用相同、完整 env.Equals，并补比较原 Equals 特意不含的 PlayerTimelineActive。源/几何/环境不从一份旧 trajectory 冒成新事实。body 不变证书成立后仍执行 ClampBorders 和 CompleteItemCheck；ItemCheck 改变任一实际状态，下一步 post-prefix 比较便不相等，回完整身体路径。Clamp 失败清证书。body 失败路径在执行前已经 Clear；预算异常仍通过原 owner 通路处理，不转换物理 miss。

`Clear:38` 以及实际完整 Prepare:100、三个 restart:132/153/222 都释放 playerFixedInput、env、terrain 引用。same-result 返回不再推进任何 action；候选 Continuation 的 head/tail 重算均先 owner.Clear 再 Prepare。因此证书不跨真实新采样、阶段重算或候选 continuation 持续使用。这里的 terrain 同步不可变前提来自实际 owner 接线，不是给任意第三方 IPredictionTerrain 赋予可变安全承诺。

Ice：NpcPredictionSource.Ice 的 capture 跨整个 Prepare/restart，首次切入捕获先 rolling.Clear；第一次完整物理查询已通过 CellAt 记录全部所需读格，后续相同输入/环境/不可变本地 terrain 的同核查询集合无需重复 HashSet 插入。旧读集 Include 和未知拒绝未改。HostIceRodAttack 的 changed-cell guard 仍走公共未 shortcut PlayerMotionContinuation，本次未绕过冰租约或改变该消费者。未新增独立 Ice 固定点反例；结论限于上述静态接线和此前 Ice 证据。

## 测试与反例的有效范围

新测试用 delegate 调实际私有 Rolling.AdvancePlayer，对照未 shortcut 的公共 PlayerMotionContinuation.Advance；双方使用两个独立真实 Host PredictionTerrain，在同一隔离地图上执行。不是手写第二个 Player/terrain solver，也没有 Harmony 业务参数回写。它只证明优化等价；原版完整 Player.Update/geometry 的证明仍由正规 Policy/Geometry/Borders/Umbrella 组承接，不能把此测试称完整 native 玩家 oracle。

14 叶覆盖三个独立 timer/Gfx、真实墙体挡住按键、jump/release、TrackBoost 输入变化、gravity prefix 时钟及方向、border、umbrella tail、Wall missing、platform Down、slope、fresh Prepare 后 liquid/removed floor。每步成功/stop 和完整 SameSample 对比，差异累积到最终拒门；未通过缩字段或改期待放行。Policy 正规入口在自身原版小 fixture 的 finally 恢复后调用此组。

旧真实 g12 `g12-fixedpoint-reject-r01.log` 共 15 个 actions mismatch 后拒门；其中 WetSlime/SwimTime/GfxOffset 是初始合法 fixture 内部自然推进遗漏，不能用 XY/V 不变解释成已知完整状态不变。TrackBoost 和 Down 是测试主动改变输入的依赖反控，不能宣称本任务已录得真实玩家自然操作故障。新 g13 `g13-fixedpoint-r01.log` 实际 14 叶 PASS。

待核范围：这组未直接统计每叶有多少次进入 shortcut，停用优化的实现也能通过等价检查，因此 PASS 不能独立证明性能收益。它也没有逐条运行四个 restart、独立 env/terrain identity 切换或 Ice fixedpoint 的动态故障注入；这些清理/依赖结论当前来自代码审查，不伪称矩阵覆盖。两份 fixedpoint log 记录了真实产品装载身份，但未打印 probe SHA，不能从其日志独自宣称两者的测试载荷哈希锁定；CPU 的 probe 身份另如上记录。

## 无状态 hook 自然 CPU 原件

读取 `g13-natural-cpu-r01` summary、CSV、父 log。四组各 360 个唯一 frame0..359；每组 idle180/attack90/switch4/restore26/release60，全部预算键匹配；末帧 exitComplete 全为 true。standing ON10 次、moving ON2 次预算触限、初冷、两组 release GC 都在原始分母中，未归零或移除。CPU 不测 Draw/GetData；原 Draw294 分母仍不能由 rawPreviewContact 替代。

| moving 段 | OFF mean ms | ON mean ms | 增量 ms |
|---|---:|---:|---:|
| idle 全180 | 0.150903 | 2.818863 | +2.667960 |
| attack90 | 0.143593 | 3.009090 | +2.865497 |
| release60 | 0.106930 | 2.604530 | +2.497600 |

三段均超过冻结 +2ms。moving attack p95 4.8517 对 OFF0.213，增4.6387ms；release p99 9.7484 对 OFF0.223，增9.5254ms，分别越 +4/+6 门。standing attack 原245.9347ms、OFF首帧873.4223ms、持续触限等保持；没有独立分类依据，不能删除或用冷 OFF 降低持续判断。

不能从单次 g13 与旧不同载荷、不同时间窗口的测量量化唯一根因收益。机制叶 PASS 不关闭性能门，当前也未有 g13 完整正常自然 Draw 的独立反证。本报告不撤销前序失败，不签最终 PASS。

## 原件哈希

路径相对 `.local/aim-performance-recovery-20261012/`：

| 原件 | SHA-256 |
|---|---|
| g12-fixedpoint-reject-r01.log | 3A5D7FD91A7F40BD37B5F76F6E6963A160084120F4BE5B7AD29FB47143A84761 |
| g13-fixedpoint-r01.log | A2DEEBCA23979CBAB5DB7FB47AA61D2C3FEB16DC132ED14EADDEAC07DA2372BC |
| g13-natural-cpu-r01/natural-cpu-summary.txt | 7D890EE12A0FF4D0C119C1BD8C53EE7C590F7AB59D13CEB89C4BC3CBB71DE9E7 |
| g13-natural-cpu-r01/natural-cpu-frames.csv | B5F309B7E02A6F530C8027EDDB7E26A1916178EBAFD9D0AC54F73A4C8E085FC2 |
| g13-natural-cpu-r01.log | 3CECB29701E533275996799F6E042E25891061FBAAD2EA568489707AA3CE2AE9 |

本报告写入独占 review，其余产品、测试、Git 及 unrelated dirty 资产均未修改。

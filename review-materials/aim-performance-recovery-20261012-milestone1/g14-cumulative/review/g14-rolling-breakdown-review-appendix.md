# g14 最后 Rolling 归属观察独立附录

2026-10-12；`/root/independent_review`，沿用已核实际 GPT-6.1 Sol / High。只读小差分及既有原件，不运行、不修改实现/期待、不延伸产品调查；仅写此独占报告。

结论：未发现新增确定观察污染或生命周期 P1/P2。原件可用于有限相对归属；**不能签性能准入**，此前持续 CPU/正常 Draw 的 NEEDS_FIX 与最终 INCOMPLETE 不变。

对照仍保留 SHA `ADD26D6EFF3D54FD2F7A306328182C6711C2F0E05B150BE5DF645E950F807C2E` 的原 carrier，新源仅改类名/独立 HarmonyId，追加 RollingNpcPrediction.AdvancePlayer 与 NpcMotion.Step 绑定、按参数数区分 Step 重载，以及诊断说明。读取/计时/数组/逐帧/异常/cleanup 语义未变；新增 prefix/finalizer仍只绑定自有 TimerState、MethodBase 和原异常，不绑定业务 ref/out/__args。没有 FirstChance、每 cell 时钟或新测试期待；真实程序集预加载和只读 DynamicMethod getters 复用已审边界。

实际装载是 **g14** Host `45E9231EE3AE8E5790430E3CAD654DD61148A499F736209CAB0100D36C725E44` / MVID `972264b8-e8c6-4b3d-baaa-098667526e9e`，probe `B026E6C6B85B6DB9D0A3AE87993DE4B1C4788D2524963B478716E4E07A5E2CB4`。GAME SHA `960A03BFF6050CF7BE16DFC1A7B19E10FC2C4F8F835A6A3B135A50DD9E6BA2F3` / MVID `2c29f6c3-4bd9-4add-9c58-da159804e083`。不能倒填 g13 或不带这些 hook 的时间窗。

亲读 CSV：四组各360个唯一 frame0..359，共1440；与自身 CPU CSV 的 case/frame/endTick 逐行匹配。所有 stepCompleted=true，stepFailed/statusMissing/budgetUnavailable=0；13个 budgetExceeded帧保留。11个已绑定边界，无 missing、row/case overflow、nested CpuStep 或 runFailure。原自然最多480尾部机制没有改，本运行实际均360；原预算退出被业务接住不等于删去 frame。新边界 outside/other-thread计数为0，原 UpdateSampleCore 的324次 scope外调用仍另列。

这次带新增 hook 的完整窗口观察（单位：inclusive ms/原始frame）：

| ON 场景/段 | Rolling.Prepare | AdvancePlayer | Step(8 args) | 数字差额约值 |
|---|---:|---:|---:|---:|
| standing idle180 | 1.844104 | 0.080284 | 0.758359 | 1.005461 |
| moving idle180 | 2.892413 | 0.568740 | 0.983845 | 1.339828 |
| moving attack90 | 2.384523 | 0.486213 | 0.831719 | 1.066591 |

只能说当前观察到的 player/NPC 子调用未解释 Rolling 的全部 inclusive。差额没有按每个父调用独占配对，包含分派、其它循环/比较/复制、子 hook自身开销及可能漏观；不能称为已定位的纯业务热点或准确 exclusive 时间。两个 Step重载可嵌套，其 inclusive不得相加；当前7-arg observed0也不是一般零NPC工作证明。与前8边界 carrier 的2.5ms不能直接解释为产品成本涨跌，增加hook自身会改变预算退出和实际完成工作量。

需保留一项文案边界：summary的“up to120/frame each”不是真实 world frame 总上限；本CSV attack实际最大 AdvancePlayer135、Step8 136（idle为120）。同world frame可同时有短输入准备和Completed完整准备，不能按120低估hook次数。它不影响1440行数据，但引用时应以上述实际总数为准。

源码与原件 SHA-256（路径相对 `.local/aim-performance-recovery-20261012/`）：

| 原件 | SHA-256 |
|---|---|
| RunNaturalRollingBreakdown.cs | FC593E5F8FDA6F271C38F48EDE2BE5F664375962EE0AB16661EE0B169CB01B28 |
| RunNaturalRollingBreakdown.exe | A2558717E59659E046B0EABABB778A97F3BBFCA2CBAE04E14C7C6565E7B7A41A |
| g14-natural-rolling-r01/natural-phase-frames.csv | 099E2FDAE390FD98E61D84E270DC5FE09245ECBBFD3F702C1C8CC74F0F1E80C2 |
| g14-natural-rolling-r01/natural-phase-methods.csv | 12A76C2608B500519CE9EA3DA3B6C2DA655FE0BF158B784A1B7F6E245276C3C8 |
| g14-natural-rolling-r01/natural-phase-summary.txt | 62DAD8D079B3EAD8BC72F1C827DA031D1EFE17A86C606E6102827BCB9EF8AECC |
| g14-natural-rolling-r01/natural-cpu-frames.csv | ED84592C5175BEAB0732C7E13443C98A7AE4A5C0617FA2461C3E23201AA794F3 |
| g14-natural-rolling-r01.log | E9385196756D42211880774D66FA57E28D228652F2CE61DF9A868AEA4FC0A1BE |

本附录不增加产品修复或性能准入结论；当前授权范围内的窄复核到此结束。

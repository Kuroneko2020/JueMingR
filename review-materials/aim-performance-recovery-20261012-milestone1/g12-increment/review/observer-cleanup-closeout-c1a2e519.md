# 独立观察器清理增量复核

本复核有限关闭 `observer-measurement-review-08616eb.md` 中确定的 NaturalImpact 清理 P2；原审查报告 SHA `83E1297542BFA6E22EA2240205103FDD2951E2D55B64868EC8DFD76DDA69706F` 保持原件。**整体仍 NEEDS_FIX / INCOMPLETE**：自然 CPU 移动闲置/攻击增量约2.960/3.243ms超冻结2ms门，真实站定红框正窗失败仍未被后继有效证据替代。

同一独立审查会话已核 Sol/High，未参与实现或期待起草；未执行测试、构建、CPU/GPU或 Git 写入，仅保存独立报告。

本次只读取 `NativeCombatNaturalImpactChecks.cs` 相对 `08616eb8797a5ab9f9124f0d3d794fc2c9fa26d0` 的清理差分，文件实读 SHA-256 **C1A2E519F7BDB108FE1322BAF443A51C14AC7D70DB0933702AB35438A25DCB59**。这是修订源码身份，尚未绑定新的已执行故障注入或测试载荷。

已独立核实：

- 首个 Harmony Patch、后续 Patch、cpuMode、CpuBindings和Monitoring初始化都进入同一 try；部分安装/绑定失败也进入 finally。
- 输出、Unpatch、film恢复、两次Settings Save分别捕获清理异常，不因输出I/O失败跳过卸载；cpuMode与静态 player/receiver在内部finally必达清理。即使Unpatch API本身报错，残余 MoveBefore也不再取得该player引用，并且报错没有被签成成功。
- 原运行异常先保留；仅存在清理失败时才Aggregate，包含原运行和各清理错误。无清理失败时保留原异常传播；正常成功窗口不被伪造为异常。
- 没有改变计时窗、OFF/ON物理输入、300帧Draw/独立Should294、原始记录格式、出生/伤害、自然release或成本门；先前成功CPU原件仍绑定其旧probe，不改写为新执行。

**判定：P2在源码静态层关闭。** 本轮未做运行时故障注入，也未冒称该轴已执行。其余窄测量/观察审查及失败事实沿用前一报告；后续仅复核新候选和未决准入，不重审无变化产品代码。

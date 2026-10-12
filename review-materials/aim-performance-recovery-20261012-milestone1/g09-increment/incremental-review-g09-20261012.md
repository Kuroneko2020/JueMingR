# 独立增量审查原文：g09 接缝（2026-10-12）

结论：**NEEDS_FIX / INCOMPLETE；不得据此准入打包或安装。** 本次增量未新增确定的代码 P1/P2，但自然站定窗口仍有实际功能失败，最终配对成本和累计相关验证尚未闭合。

审查者为同一 `/root/independent_review`，实际会话 `01a12f80-6775-7e53-81b9-b0fbf5caea34` 已核 `gpt-6.1-sol / high`，未改变。未参与实现或测试期待起草；没有运行 CPU/GPU、构建、测试、打包或 Git 写入。本轮唯一写入是这份独立原文。

## 身份与范围

首轮已审事故产品 `75a1072cba59fbb84f587ec5615b8f32a9cedcb5` 至产品 `380132ecfe93c5daa79a5fcd6f2a62463d4fe76a` 的完整增量及必要上下游。本次复用该有效审查，只检查其后七个产品文件的未提交增量、直接相关 ShortDemand 判定和固定原始结果。读取时 HEAD 为 `ad0ca821049d9356c6d118627919a95d3454c4b6`；这不是七个 dirty 产品文件已经提交的证明。

g09 原件实际 Host SHA-256 `957A5463BD9399C597E24A4218BEEAB94268759528C0313F69765FCF879372C5`，MVID `a220125c-f55e-4fb9-acdd-3343b96c1b30`，源码标识 `uncommitted-zero-correction-g09`；不能改写为正式产品 commit、完整包或安装身份。原版 SHA-256 `960A03BFF6050CF7BE16DFC1A7B19E10FC2C4F8F835A6A3B135A50DD9E6BA2F3`、MVID `2c29f6c3-4bd9-4add-9c58-da159804e083`。

七个实际读取文件的 SHA-256：

| 文件（相对仓库根） | SHA-256 |
| --- | --- |
| src/JueMingR.TerrariaHost/Combat/HostProjectileEnvironment.cs | D03470A7D79272E5A72807BF5EF97C70A067775BFC4FC41E5CF4E0C757FD2CA2 |
| src/JueMingR.TerrariaHost/Combat/NpcPredictionSource.cs | 0837CDA5B694DAE001025B5097619D220EDAB519CE165BC9CB8074A582BCAEA4 |
| src/JueMingR.TerrariaHost/Combat/HostCombatObservation.cs | 9ABDCB0CC8621AD8E10ACC851F5C1ED9938DB13B80AA21166FF0271092E8FB87 |
| src/JueMingR.TerrariaHost/Combat/HostAttackAim.cs | 42072D0BDC092CF8FB58A4A0A3197E1461DB625BF916379E0C656D273C2D90D6 |
| src/JueMingR.TerrariaHost/Combat/HostAttackAim.Selection.cs | 3A093624AA40AF7C1D6F21903057BE2FE2811C935AB02A20E09AD474519F56F9 |
| src/JueMingR.TerrariaHost/Combat/HostAttackAim.Preview.cs | 78AF37F66DC038779FF317573BE14E069B19A2ECB8CD258710261237116A44C2 |
| src/JueMingR.TerrariaHost/Combat/PredictionTerrain.cs | BE659FD49CEB519ED5AEE905CB793307E7035FEE7C79CA21FB6DF46577BB7C5F |

原有 `docs/设计/本地参考资料使用说明.md`、`.workbuddy/`、`issue112.txt`、`pr113.json` 不计入本任务交付差异，未修改。

## 已核接缝

1. **真实预算时钟。** `HostCombatObservation.Update` 使用 GUC，动作前、姿态、出生和 PreparePhase 的 scope 也使用 GUC；旧 Runtime 私有计数导致同帧 spent/exceeded 重置的 P1 已在当前代码修正，没有修改通用 Runtime。预算异常退休 scratch、保留一般目标和 Basic，下一真实 GUC 可恢复；它不等于物理 miss 或永久 fault。
2. **动作前短窗口。** Observation 仅在 BeforeNpc 释放 display reader0，CompletedWorld 重新要求 Path120；Attack 是 reader1 的写入所有者。`ActionPreferred(identity)` 按该候选自身身份作纯提示，不改其他候选历史。短正结果限定 hand、非 Controlled、无 Flight、Delay0；非终止短无解同次扩到120，之后才继续远候选。最近模式在 PrepareCore 内同 action 扩完整窗口；preferred120 使递归不重复扩张。实际输入仍独立 Solve/Valid，没有把只读候选 Contact 直接铸成能力。
3. **miss 与状态所有权。** 预算异常清 partial plan 后传播，不保留 Prepared 作为已知 miss。终止路径没有被填尾成120。Source 的 OwnsTarget 只是提取原 incumbent guard，不切换或恢复 candidate/main 历史，不暗写 Attack demand；正式发布仍重核完整样本、玩家、阶段和单元租约。
4. **风依赖。** PotentialWind 的 type override/style 与锁定原版 `ShouldUseWindPhysics` 匹配；风开关本身仍被相关机制租约保护。NPC 组按真实 Wind/WindTarget/HappyWindy 读点采集，相关 balloon writer 保留；玩家被风推动时仍采集风值。未发现此增量遗漏已知风读点；各真实动态反控仍须以最终候选结果闭合。
5. **DryRegion。** 32槽 proof 仅对完整扫描矩形中所有液体量为零、且无 RawActive 坡的区域成立；不是仅 sensor miss。epoch、全单元 lease、边界、预算、ice read capture 和 player guard 继续生效。
6. **零校正 Slopes。** 仅 exact zero correction、正宽高省第二遍 TileContact。严格重叠条件与四个响应分支互斥；首轮已读同一扫描矩形，左右邻格分支不可能执行。IceReadSet 是幂等集合。最终 Area/FreeBody/SignedProjectileArea、head flags、stair 与速度收尾仍保留；非零修正、负/零尺寸继续原路径。关键就地注释与该条件一致。

## 测试判定与已读原件

`NativeCombatCandidateDemandChecks.ShortDemand` 仍要求动作前实际首发消费 Enhanced，窗口16..39、路径至少 window+1；在同一 GUC 天然 NPC/Projectile 更新后，Path ON 必须恢复121点、当前 sample tick 和同一目标身份，Path OFF 的 reader0 必须为空。marker-only 仍断言无残余未来步数。没有发现以更短显示路径或 Basic 代替旧 Enhanced 的期待缩水。此处是判定源码审查，未声称该完整入口本轮已经执行通过。该测试文件读取 SHA-256 `F9FFDB8497C56CAE0D52D8C3B4899DC1A8F694F98E64E0D5F1F3FBB3FEA2A3CE`。

直接回读 `.local/aim-performance-recovery-20261012/input-validation/budget-g09-r01/run.log`（SHA-256 `BB782DC38C48F9DD853933F7AFE55C300F8CF67C662252199C273724F7060B9C`）：真实 Runtime/GUC 自然错位；强制 partial-plan 退出、不转 miss、同更新原生 Basic、同 GUC Completed 保留耗尽和下一真实 GUC 恢复均 PASS。Path OFF 最近 selected2/nativeBirth1/nativeHit0；清线 selected3/nativeBirth1/nativeHit1。g08 7.0444ms 后退回 nearest 的失败仍是旧候选原件，不抹去；g09 只闭合这个反例，不外推所有多候选场景。

直接回读 g09 terrain 原件：原版坡/碰撞5103例、SolidCollision1782例、CanHit1792例、NPC clipped edge882例的对应 PASS 保留，只证明实际专题范围。g07 DryRegion 原件先前已核，不能据此宣称所有性能门通过。

直接回读 `.local/aim-performance-incident-20261012/impact/g09-r17-natural-draw.log`：同原生场景/独立 Should 分母，站定 **284/294**、missing10、longest5；移动 **294/294**。两窗各300更新，birth15/15、assisted15/15、hit16/15、landing16/15、normalGravity69/69、玩家位移0/91.32001。站定 idle2 首缺失仍为 path121、Contactfalse、Visiblefalse、red0、budgetExceededtrue。该实际正窗失败不因移动窗口通过、不可达负例或初次冷成本而自动放行；阶段/噪声归因须有对应独立证据，不能改 Should 分母造绿灯。此记录为功能 XNA/native 原件，不是正式计时或 FPS。

## 当前阻断与边界

- 自然站定正窗仍实际 FAIL；最终候选必须补有效连续 Draw 结果，不能以 CPU Contact 或移动294/294代签。
- g07 天气 Leaf/Solar 的特定配对通过，只绑定 g07 指定原件；七个 dirty 增量后的最终持续成本、尾部、OFF回归、b038升级对照与普通少量候选仍要按冻结门判断。正常预算触顶不能被计成优化成功。
- 正式最终 SourceSHA、完整累计相关义务、必要相邻消费者、最后实际包和安装尚未闭合。新增 Natural CPU 模式、Wind 检查及其他尚在写入的测试不属于本次已冻结判定，须在后继身份上增量审。
- 本轮无新增确定代码 P1/P2不等于三核心门 PASS。本报告不授予构建包、安装、合并、关闭 Issue 或发布权限；未声称用户游戏体验、FPS、真人多人或所有者接受。

本次增量只读审查结束；后继只复核新修改、失效证据与未决项，不重演已审完整批次。

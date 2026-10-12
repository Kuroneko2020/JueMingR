# 性能事故恢复首里程碑证据

源码固定提交 `380132ecfe93c5daa79a5fcd6f2a62463d4fe76a`，相对事故产品 `75a1072cba59fbb84f587ec5615b8f32a9cedcb5` 的完整当前增量供独审。`source-tree-380132e.txt` 是该提交 src/tests/scripts/eng 的 Git tree，SHA256 `7C48F59D4BF61ED0DFFEA26007A16CDD3BC6C42CE16A73BA1B110F386AB3EAA0`。它不是含外来dirty资产的正式构建指纹；后续完整包仍需正规身份生成。

本目录只保留可审文本、逐帧CSV及私有测量源码，没有原版DLL、包、角色世界或用户配置。`manifest.json` 核原始复制件长度/SHA。文件保留实载MVID、SHA、原版身份和原失败；源码由单Git writer保存，原四项dirty资产未纳入提交。

| 实际载荷与窗口 | n | mean/p95/p99/max（ms） | 判定边界 |
| --- | ---: | --- | --- |
| 75a 1228 OFF 静止 idle | 360 | 0.103540 / 0.1291 / 0.1883 / 0.4244 | 同武器关闭基线 |
| 75a 1228 仅路径 静止 idle | 360 | 10.094553 / 10.9199 / 11.4962 / 12.0553 | 比OFF约+9.991ms，实际事故拒门 |
| 75a 1228 仅路径 连续移动 idle | 360 | 10.226675 / 11.0268 / 11.7761 / 14.501 | 移动360/360，持续路径需求 |
| 75a 518 仅路径 静止 idle | 360 | 10.108063 / 10.8755 / 11.7477 / 14.7328 | 原反馈普通武器同根成本 |
| g03 1228 路径+Aim 连续移动 攻击段 | 540 | 2.335479 / 3.2051 / 3.463 / 5.337 | 尚缺同条件OFF，不宣称mean门已过 |
| g03 同一周期 自然退出段 | 180 | 1.113301（余指标见原日志） | 不混入攻击均值摊薄成本 |

75a计数另跑，120次Rolling准备/60真实更新，合计NeedsPlayerMotion43,440/StructuredModel14,520/Step14,400；深探针含嵌套与反射开销，不能相加当生产耗时。计时包含自然Player/NPC/Projectile阶段和真实UpdateRuntime/UpdateShell，非完整Main.DoUpdate/FPS； witness与CSV写入在计时边界外。早期75a CSV记录分配包含窗内文本构造，后继harness改预分配数值、窗后写CSV，不能把早期分配直接当纯生产量。计数与计时分批；后续新6ms预算需记录正常时触顶量，不能因探针触顶把裁掉工作称优化。

`ContinuousCombatMeasure.cs` 是本轮最新隔离源码，`prepare-measure.ps1` 从固定ZIP核验并构建优化x86包装器；`run-measure.ps1` 是一轮固定载荷驱动。早期源码增量未另存独立版本，原始日志/CSV不回写，本源码可用相同边界复测，不能宣称它逐字等于最早执行源码。真实移动来自原版Triggers/Input，不直接挪位置；攻击窗按原版冷却周期重复使用，含派生现生与自然退出。g03天然UpdateWeather已调用，但记录windChanges=0，不能充当动态风变化覆盖。

g05最终本批私有Release/x86四程序集与Debug观察probe都构建0warning/0error，`g05-basic-r01.log` child exit0：铜短剑远目标、Starfury世界坐标/反重力、叶绿镋近父/远派生/连续移动/重按/松手/OFF，纯无shoot挥剑在原版真实StartActualUse消费方向，未来增强故障仍刷新新目标并自然出生，焦点/UI/死亡/换物/身份/真选择器故障拒绝；Bow实际Basic不显示另一自由弧线，idle仅路径保留；DD2原版CanUse/Shoot在增强故障仍借有效Use point，OFF/receipt身份拒绝；Yoyo整墙/8px洞停止、16px洞通过。g02旧私有载荷的Yoyo失败保留，与g03/g04/g05新短段body实现分开。g05 Host SHA `173863731C388FD5B88CED965CE4DE0DBED9EE0EB41A5AD1905BE60545A7DCDC`、MVID `c4954de5-9fb9-43e8-a819-b88247e3de2a`；probe SHA `08298DD79D6FF6511D083961B728F773467E5FE5CFBD850BA0D6471BD99A66DA`。

本批仍未完成最终准入：持续性能配对OFF/b038、冷启动171ms尖峰配对分类、预算强制退出/Basic仍合法/次帧恢复、既有工作量检查纠偏、Source仅Aim consumer共享与ClearLine反例、自然史莱姆实际候选Draw、集中独审、完整包及正常安全换装都还待取得有效证据。此前Draw reference失败为隔离世界维度/资产/footprint身份前提，不能说候选已验证。现装仍75a，不创建Release，不合并或关闭任务。

新增有效自然Draw失败原件在`natural-g05-r10/`：同g05、优化Release四模块/固定r10 probe，站定应画294、实画291，最长blank2；移动294/294，天然birth15/15、hit16/15、landing16/15、移动91.32px。站定idle2/3预算耗尽7.5022/7.5496ms；另attack180/tick261预算未耗尽、Used2.228ms却Contact/visible/red均空，是独立真实首发断点。首失败PNG及6正常帧图、完整600行CSV和有效原版reference原日志保留。图像入口当时每帧分配RenderTarget和2.4MB读回缓冲，后继会复用缓冲定向区分，不能把此GPU诊断窗称正式性能计时或直接归因GC。

`RoomPhaseMeasure.cs`及`g05-roomphase-r02.log`为固定g05同room另跑低频9边界诊断，预算异常只计类型以免每次构造StackTrace：600窗UpdateSampleCore792次inclusive1945.5369ms，Source.PrepareSample630/1770.9738，Rolling629/1742.7768；PreparePlan591/122.9358，HostProjectileEnvironment.Solve680/84.8717，AttackIntercept.Solve679/82.1468。嵌套不能相加，探针不代签成本门；主要持续成本仍是NPC未来准备，Factor末尾异常检测点不等于热点。第一私有适配尝试把Harmony放exe邻接导致Default/LoadFrom上下文TypeLoad前提失败；保留移出这一新增DLL后r02进入实际场景，未补ABI。

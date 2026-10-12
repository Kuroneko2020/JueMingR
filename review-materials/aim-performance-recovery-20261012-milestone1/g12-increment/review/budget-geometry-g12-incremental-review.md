# g12 几何预算候选独立增量审查

日期：2026-10-12。审查者 `/root/independent_review`；实际会话元数据已核 GPT-6.1 Sol / High，未改变。未参与实现或测试期待起草；本次只读取源码、差分及既有原件，没有运行程序、构建、CPU/GPU 测量或 Git 写入。仅写本报告。

结论：本窄增量未发现新增确定 P1/P2；静态机制与指定边界相符。候选收益及最终准入仍 **INCOMPLETE**，整体事故修复仍 **NEEDS_FIX**。不能用预算/terrain 检查 PASS 代替持续性能或自然 Draw 门。

## 范围与身份

以已完成的 75a→380132e 全批次和后续 g09 独立报告为基础，本次仅审当前未提交 g12：

- `src/JueMingR.Features/Combat/CombatPredictionWork.cs`：新增 `geometryRemaining`、真实 update 键变化时重置、`CheckGeometry`。
- `src/JueMingR.TerrariaHost/Combat/PredictionTerrain.cs:92`：`CellAt` 改调用 `CheckGeometry`。
- `tests/NativeWorldTextProbe/NativeCombatAttackCostChecks.Incident.cs:116`：新增 `GeometryAllowanceIntervals`，未调整原预算恢复/PathOFF 期待。

未重审未变的七个 g09 生产文件及旧全仓。报告所读源码 SHA-256：

| 文件 | SHA-256 |
|---|---|
| CombatPredictionWork.cs | 64F54E0B6D1387F7AF3A91B5615175D189A4EB8DE44F186C9DA55E15E3761023 |
| PredictionTerrain.cs | FE71CAE04A7A70F582C3C8DA4B9451AF2C1A5AB5ADCE2C110742BBE533ED7CBF |
| NativeCombatAttackCostChecks.Incident.cs | 6A4266BB1B6DC4175A5742428087317B40A86972870D3DC0597BE9C10646D2B9 |

日志实际装载：g12 Host `440FAE345E3C066887EA5C83B4382D1AA8410FBAD1F709938D26CE342941B5D2` / MVID `ed65a495-20c8-459e-a48b-227011eb6aff`；Features `6F787524D905CF6E934AFA038A5162EFDBD05504AC824F5B6CD0320D98658296` / MVID `41c1343a-4fbd-4be0-abf9-5989b598db08`。CPU 父日志 probe 为 `FA8F05FE09A38DDCDB96F0BB9C73AE5AF2D94EF21623E3216924EC25AFAA94DB`。sourceinfo 为 `uncommitted-bounded-cell-check-g12`，不是正式产品提交身份。原版 GAME SHA `960A03BFF6050CF7BE16DFC1A7B19E10FC2C4F8F835A6A3B135A50DD9E6BA2F3` / MVID `2c29f6c3-4bd9-4add-9c58-da159804e083`。

## 静态判断与实际边界

`CombatPredictionWork.cs:24,33..44`：每 owner 的第一次 `CheckGeometry` 读钟，随后第 33、65……次再读钟，时钟检查之间最多 32 次请求。计数初值及新真实 update 重置为 0，重新进入同键短 scope 或嵌套 scope 不重置。`exceeded` 已置位时不等间隔，下一次几何请求立即抛；失败后只有新 update 清除它。原累计 `spent`、6ms allowance、ThreadStatic 活跃 owner、scope 的计时/退出和 `Check` 未改变。

当前真实产品只有 `HostCombatObservation` 持有一个 `PredictionWork=new CombatPredictionWork(6)`；此前已审的 Observation/Attack/Control 均用真实 GUC。候选切换没有新建 budget owner。抽象 API 当然仍允许另建 owner；本次结论来自当前接线，不能泛化成任何调用者都不可另开额度。

`PredictionTerrain.cs:92..105`：每次 `CellAt` 在 bounds、冰 guard、PathReads、hot cache、dictionary、4096-cell limit 之前计入几何间隔；这些业务判断和读取顺序均保持。cache miss 仍经 `Read:75` 的即时 `Check`；`Unchanged`、`CellLease.Current` 经同一即时 Read；Wet 的即时 `Check`、future-action/PlayerContinuation/Rolling/候选循环的原 `Check` 也保持。仅缓存内格子查询减少读钟，不把缺格、未知、失效或预算异常改成已知 miss。

间隔约束是调用次数约束，不是 6ms 硬实时保障。若时钟刚检查后超限、GC/调度停顿，剩余最多 31 次缓存格访问可能先返回；某个小几何查询也可能在下一次时钟检查前结束。因此不能从 `Exceeded=false` 单独推导该帧工作始终低于 6ms。原注释明确合作预算，当前冻结均值与尾部门没有因此变化。

`GeometryAllowanceIntervals:116..139` 调用实际 Release owner 方法。首格以 `spent=allowance` 制造已耗尽边界；另一 owner 首次读钟后置同样 spent，再跨同一真实 GUC 的短/嵌套 scope 请求，原日志在第 32 次中断，并验证已耗尽下一格立即拒绝。它能识别每 scope 重启计数、无限延后或遗漏 exceeded 闩锁；它是合成计时字段反例，不是实际游戏成本或 native CellAt 调用次数证明。真实新 GUC 恢复由原整体 clock/recovery 场景补证，本新增反例没有单独穷举所有嵌套 owner 类型。

## 原始验证复核

`input-validation/budget-g12-r01/run.log` 实际 PASS：首格/跨 scope 32 次边界，强制 partial-plan 不变已知 miss，同 GUC Completed 保留 exhausted、合法 Basic 的 native birth，下一真实 GUC 恢复，以及 PathOFF 最近/清线目标 native birth；清线 nativeHits=1。该原件也保留天然预热最初 6 次预算退出，未删掉。

`g12-terrain-r01.log` 实际 PASS：原版 WetCollision 干区域正反控、Wind 1136（346 sensitive / 790 immune、60 次真实天气变化）、slope 5103（contacts1180）、Solid1782、CanHit1792、clipped NPC882。可复用原测试静态审查；没有把这些计数当持续性能证据。

`g12-natural-cpu-r01/natural-cpu-frames.csv` 独立读取核对四组各 360 行、frame0..359 唯一连续；每组 idle180/attack90/switch4/restore26/release60，budgetCurrent 全为真，末帧 nativeExitComplete 全为真。standing ON 六次、moving ON 四次 budgetExceeded 原样保留，所有 cold0..9 原样在主分母中，补充 idle10..179 不能替代主分母。CPU 不测 Draw，既有 Draw294 分母没有被改为 CPU preview。

持续与尾部仍失败：

| moving 段 | OFF mean ms | ON mean ms | 增量 ms |
|---|---:|---:|---:|
| idle 全 180 | 0.134677 | 3.228804 | +3.094127 |
| attack 90 | 0.164992 | 3.013679 | +2.848687 |
| release 60 | 0.089553 | 2.839708 | +2.750155 |

moving attack p99 为 ON7.2022 对 OFF0.2647，增量 +6.9375ms，超过冻结 +6ms 尾部门。standing attack 的原 271.2396ms、release11.1853ms、OFF 首帧891.3215ms 等都保留；当前没有独立噪声分类依据，不能删样或用 OFF 冷帧摊低 ON 持续成本。两组 ON 均 15 次 assisted birth，原 nativeHits 为 standing16/moving15；它们不证明性能或 Draw 门通过。

## 未决证据

紧邻旧 g09/新 g12 同 probe 的有限 A/B 尚待正式原件，不能从这组混合实测宣称收益或成本根因已证明。预算机制静态及局部反例通过，不足以判准入；自然 Draw 的既有缺帧 FAIL 尚无本候选完整正常 Draw 反证。最终固定源码、完整冻结持续/尾部/OFF回归/冷首用边界仍由正式证据整合给出，本报告不签最终 PASS。

## 原件摘要

以下均相对 `.local/aim-performance-recovery-20261012/`，保留原件：

| 原件 | SHA-256 |
|---|---|
| input-validation/budget-g12-r01/run.log | E5ECE09B5073EBBCA4488FA2FD439A29EAFD4A9076F4B7DFD20B27300A2E9C47 |
| g12-terrain-r01.log | AE621B4410D35D6E8FB71DE9D544E8CFF698E5444D0EA793AF2D0D3496BAC902 |
| g12-natural-cpu-r01/natural-cpu-summary.txt | 4E5D05D350FD4195E2A98670E5D4468E56682B062DD7DE6518E487703E05909B |
| g12-natural-cpu-r01/natural-cpu-frames.csv | AAC22413878C082B9141BF173FAA2D568A2B733E8A275D91F3F869FE014E3C90 |
| g12-natural-cpu-r01.log | 3372B2F1996F13EE377E9ABB3D1AE96842C02C56B5840D8B98BA75797012A1B1 |

此前独立报告原文及失败原件均保持，不以本报告覆盖或撤销。

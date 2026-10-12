# g14 无 slope 区域证明独立增量审查

2026-10-12；`/root/independent_review`，沿用已核实际 GPT-6.1 Sol / High。只读源码、差分、提交沿革及已有原件；未实现、起草期待、构建、运行测试或 CPU/GPU。本次仅写本报告。

结论：未发现 g14 新增确定 P1/P2。首 scan 的零 slope 证明在当前 immutable pass、租约和边界下充分，命中后仍保预算、未知及全部尾部语义。功能专项原件有效；持续自然 CPU 均值仍失败，整体 **NEEDS_FIX**，最终准入 **INCOMPLETE**。

## 范围与身份

基点已读 HEAD `a702b6b17fb1814804f364ac52058b9ad84a1152`。本次仅 g14 两文件增量：

| 文件 | SHA-256 |
|---|---|
| src/JueMingR.TerrariaHost/Combat/PredictionTerrain.cs | 56E8A3841713D041604E655C6CECE22F2C29BB8F70E48362797771906FE9901E |
| tests/NativeWorldTextProbe/NativeCombatTerrainChecks.cs | C646B3F5A37A4878C2E4BC17EA7484D5B64D5C21EF82F0E0E509063ECBE13E9C |

此前 g13 三文件复用原独立审查，不重审旧全仓。g14 原日志实际 Host `45E9231EE3AE8E5790430E3CAD654DD61148A499F736209CAB0100D36C725E44` / MVID `972264b8-e8c6-4b3d-baaa-098667526e9e`；CPU probe `B026E6C6B85B6DB9D0A3AE87993DE4B1C4788D2524963B478716E4E07A5E2CB4`；sourceinfo `uncommitted-g14-slope-region`，不冒称正式提交。GAME SHA `960A03BFF6050CF7BE16DFC1A7B19E10FC2C4F8F835A6A3B135A50DD9E6BA2F3` / MVID `2c29f6c3-4bd9-4add-9c58-da159804e083`。

## 证明与生命周期

`PredictionTerrain.cs:548..579`：必须正宽高、当前采样尺寸与 Main 当前尺寸一致、没有 Ice guard/capture，才可使用 32 槽证明；命中比较完整整数 bounds 和 epoch，槽碰撞只影响命中率。证明由首 scan 的每个 CellAt 返回值逐格验证 Slope==0；即使 inactive 或不相交格有非零 slope，也保守拒绝授证。原 scan 所有 correction、vx/vy、mask、stairFall 的写分支都要求 Slope1..4，故全格 Slope0 时首 scan 对这些状态无影响，不依赖 float 姿态、速度、fall/player/nativeClip/signed 模式恰好相同。

首次授证仍执行完整 acquisition，dictionary 保留全格租约。只有扫描成功、无 unknown/limit/guard 退出并通过原最终 Area/FreeBody/SignedProjectileArea 后，`591` 才写证明。partial scan 直接 return 不会授证。命中只跳首 scan；`545` 即时 Check、原入口边界、`580..590` 零校正/TileContact及最终边界、`594..595` head/stair/velocity/位置尾部均保持。未把未知或超限改为已知无碰撞；预算值和真实 GUC owner 未变。

一般 CellAt 的格子 cache、尺寸、4096-cell 上限和租约规则未改；本证明限于同步 immutable pass，不能推出在未 Reset 的可变世界任意调用也安全。正常 Reset 递增 epoch；long.MaxValue rollover 显式清 hot/dry/slope 证明数组后变为新 epoch。continuing Reset 原返回及 restore lease 的既有语义不变，不能让旧 epoch 的 proof 冒成新 lease。

Ice capture/guard 任一存在都禁用 hit 与授证，首 scan 仍逐 CellAt 记录 PathReads 或遇 guard 拒绝；first scan 完整读集仍留在 owner lease 中。signed/nativeClip/nonpositive 边界不被略去，后者也不授证。没有额外扩张模型支持范围。

## 测试判别力和限制

新增测试直接正常 Invoke Slopes 的 ref body/out stop 并读取返回值；这是测试正常 API 调用，不是 Harmony `__args` 观察器回写。Harmony CellAt prefix 只增加自有整数，不绑定业务参数。cold CellAt>0、同 bounds 新 float pose hot 不增加计数，能拒绝没有实际 hit 的实现；size/dim 变化必须重新扫描。new slope/inactive nonzero slope 必须拒证明；unknown partial 失败后修复必须重扫；Ice capture 必须实际读格且 changed Point 不得被判 Unaffected，Ice guard 必须得到 TerrainUnavailable。每个成功查询还与实际原版 Collision.SlopeCollision 比 position/velocity/head/stair 尾部。

finally 保留独立 Ice 清理、Unpatch、尺寸/TileSolid/全部原 tile 恢复及 terrain.Reset；安装前后失败都有 try/finally 出口。该整数 observer 只在专项中使用，其成本不能进入无 hook CPU 成本证据。

一个覆盖边界已反馈：epoch rollover 用强制 epoch=long.MaxValue 后 Reset 到1；当时旧 proof 的 epoch 本来不等于1，所以漏掉 Array.Clear 也可能因 epoch 不匹配而通过此例。该例证明 rollover 后确实重新 acquisition，不能独立证明所有古老 epoch1 proof 都被清除。产品显式清数组已静态核到，当前不构成生产缺陷；不冒称额外动态反例覆盖。其它首 scan 失败后最终 Area 才授证、signed/nonpositive 分支等也以静态判断和原几何组承接，不将新增少量局部查询泛化成全模式热命中矩阵。

`g14-terrain-r01.log` 原件实际 PASS：新增 slope-free region 正反控、DryRegion、Wind1136（346 sensitive/790 immune、60次真实 UpdateWeather）、原 native slope5103/contacts1180、Solid1782、CanHit1792、clipped NPC882。它证明功能专项，不证明持续性能或 Draw。

## 自然 CPU 原件

独立读 `g14-natural-cpu-r01`：四组各360唯一 frame0..359；完整 idle180/attack90/switch4/restore26/release60；budgetCurrent 全为真；末帧 nativeExitComplete 全为真。standing ON7 次、moving ON1 次预算超限均保留，cold0..9 和首用 shot 未删。两组 ON均15 assisted birth，原 nativeHits standing16/moving15；CPU仍不测 Draw/GetData，不能将 preview 替代原294应显分母。

| moving 段 | OFF mean ms | ON mean ms | 增量 ms |
|---|---:|---:|---:|
| idle 全180 | 0.144471 | 2.885833 | +2.741362 |
| attack90 | 0.143099 | 3.034638 | +2.891539 |
| release60 | 0.130258 | 2.240710 | +2.110452 |

三段仍越冻结 +2ms。standing release2.110347−0.075943=+2.034403ms 也未通过；standing idle10..179 补充增量 +2.038653ms 不能代替完整主分母。moving 尾部本次较好不能救均值失败。原 standing attack291.6658ms、OFF首帧876.5711ms 及触限冷帧都保留，无独立噪声分类则不能剔除。不同时间窗/不同 probe 的单次数字不能当 g13→g14 唯一归因收益。

待决仍为最终固定身份、完整持续/尾部/关闭回归及正常自然 Draw 门。功能专项不关闭这些缺口，旧失败原件不被撤销。

## g13 根因沿革补核

本次另作窄 Git 只读核验：`917a531a6416f23c826ab84ce1cee5bf555196f3` 的 Rolling 新增 diff 已包含 `if(playerSettled)return true` 和只凭控制/XY/V 授证条件；`git merge-base --is-ancestor` 对 a702 返回0。由此可确认旧 shortcut 随该提交进入现基点沿革，不是 g14 新引入。g13 原报告中的 timer/Gfx 冻结因果仍有效；该沿革核验不扩大其自然真实玩家接受证据。

## 原件哈希

路径相对 `.local/aim-performance-recovery-20261012/`：

| 原件 | SHA-256 |
|---|---|
| g14-terrain-r01.log | 5CE2098DBCF8B89687358F4D0BE11337765695D83DD8232AFF206BC9E29EADB0 |
| g14-natural-cpu-r01/natural-cpu-summary.txt | C3D7AE8A4342538D82E0682BEB2313B8F1E89836BBF554A330AE3F87112E7340 |
| g14-natural-cpu-r01/natural-cpu-frames.csv | 608472348DD54E95365F77E98FB868C43623CEA77FDF2B2F94662FB12EB6970F |
| g14-natural-cpu-r01.log | 1D02AFF0D4570481ADE76B03C2DE472892D69C8D18080FCF60141FCCC54951DD |

仅独占 review 报告写入；产品、测试、Git 及 foreign dirty 未修改。

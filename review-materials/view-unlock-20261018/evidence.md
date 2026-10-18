# 视距解锁候选审查入口

任务为 [#124](https://github.com/Kuroneko2020/JueMingR/issues/124)，Draft PR 为 [#125](https://github.com/Kuroneko2020/JueMingR/pull/125)，分支 `codex/view-distance-unlock`，接受基底 `8bba8ffd2d24591e9efb1258ae0eac1e1d7d60df`。本文件所在提交是源码候选；正式检测、独审、准入和普通包身份分别绑定在 `.local/view-unlock` 的原件，不能用本文件代替这些结果。尚未合并，保留 `INCOMPLETE / releaseEligible=false`。

## 产品与实现

- 地图页公开标准行“视距解锁”，提示逐字“可以调整到更大的视野范围”；没有新快捷键、滑条或额外按钮。原版重绑后的 ViewZoomIn/Out 是唯一缩放来源。
- ON 不跳；OFF 保留超范围目标与外围绘制，只允许逐步回普通。原版目标仍是唯一目标，实际相机矩阵决定绘制和关联。50% 是当前基础下限，仍服从原版强制倍率和观察容量；没有任意分辨率、设备或无限视野承诺。
- 原版直接世界绘制复用原消费者；旧光照容量按需增长、稳定复用。只在明确的背景/效果层借用字段与 batch，正常及异常后还原；背景混合 Camera getter 的两个叶接点避免二次平移。液体/瀑布只修实际旧矩形，未建立第二套世界 Renderer。
- 普通客机按最终相机请求合法未知 section：最多一包/更新、16槽、ACK去重、移开释放本地槽、300次更新超时/发送失败局部停止。未修改 section loaded、服务器位置/AI/实体活跃权限或预测模型。
- 发现器保留预算及交集进度，避免连续缩放饿死后段；文字排版失效仍随几何更新。玩家中心候选移除历史140格夹顶，其它距离、武器、交互与碰撞框语义保留。

正式行为与生命周期见[合同](../../docs/功能介绍/视距解锁行为合同.md)、[设计](../../docs/设计/视距解锁实现设计.md)。关键注释跟随状态所有权、早/晚输入阶段、OFF保留、旧光照桶、临时域归还和请求退出同步维护。

## 证据入口与上限

| 责任 | 实际证据 |
|---|---|
| 准备/结构 | `.local/view-unlock/build-core-03.log` 至最新 `build-core-06.log`；PrepareOnly不授予业务资格 |
| F5/配置 | `f5-cpu.log`、独立 `ViewPreferenceChecks`；实际布局/Interaction、精确文案、公开资格、严格bool、默认OFF、不预写、保存/重开和未来字节保护 |
| 首个原版GPU闭环 | `original-11/run.log`、`run-inputs.json` 和PNG；原键到50%、外围tile/NPC、OFF保留 |
| 光照/资源/异常 | `original-15` 至 `original-20`；四光照、墙/水/掉落物/弹体、洞穴火把、遮挡/shimmer/darkness/月亮白屏、真实1280×720↔1920×1080、cave异常恢复及下一次Draw、菜单/重入和Hook退出 |
| 成本与参考探点 | `original-19/run.log`：75/50/30实际矩阵、静止/移动各30次，直接功能Update10万次，分配/托管与私有内存变化、稳定资源；30只在测试宿主覆盖最低getter，不进入产品 |
| Combat/世界消费者 | `outputs/view-combat-evidence.json`、`outputs/view-combat-native-run-3.log`：真实MouseWorld/原版Shoot/MagicMissile、普通/反重力、tile/entity/marker；`.3125`只几何反例，不是产品支持 |
| 发现器因果 | `outputs/view-combat-discovery-red/result.txt` 旧DLL真实RED，`outputs/view-combat-discovery-green/result.txt` 最终GREEN；最大读取2588/4116、100次OFF零source/tile调用 |
| 真TCP→decoder→GPU | `network-full-04/run.log`、`run-inputs.json`、`view-network-pending.png`、`view-network.png`：专服真实握手，冷区159→原版包10解码→Draw；visible mask由中心loaded/两侧pending变为三区全loaded，旧partial边界消失 |
| finite stage入口 | `.local/view-unlock/stage-reader-checks.log` 的22组件正反例；组件fixture不是实际独审或clean候选准入 |

最终固定候选还使用现有 `test-world-object-text.ps1 -Scope ViewOriginal / ViewNetwork / ViewCombatCpu` 正式入口取证；具体执行字节、source/input fingerprint及适用复用由阶段记录承担。旧结果的Host MVID/SHA保留原身份，不随新字节重签。

测试锁定 Windows Steam Terraria1.4.5.8 原版EXE，SHA256 `960A03BFF6050CF7BE16DFC1A7B19E10FC2C4F8F835A6A3B135A50DD9E6BA2F3`。真实XNA Reach、1920×1080及1280×720切换。完整 Initialize/LoadContent，静音、自有路径、无Steam/cloud；没有启动或操作所有者游戏/真实档。

中性测试exe需原版同等LAA，脚本只改其可丢弃副本并绑定补位后SHA。`SkipAssemblyLoad`只跳过原版程序集预热等待；Reach最终输出用物理尺寸capture sink读回，原版中间RT/世界Draw保持。这些是明确的专项宿主前提，不是普通完整包加载证明。TCP使用完整初始化Main+有限原版主线程pump/decoder/Draw，不冒称完整客户端模拟、Host & Play、远程延迟或所有者多人验收。NPC有实际receipt；本场景item/projectile receipt为0，不造生命周期通过。

75%静止/移动Draw均值7.53/7.69ms、50%20.94/18.30ms、30%43.48/42.19ms（原版量化为.296875）；30次AppDomain分配约32.1MB、121.3/123.6MB、198.9MB，含原版和宿主，不全归给R或称为常驻泄漏。保留50%基础下限有实际成本依据。普通功能Update OFF/ON各100000次9.207/9.580ms、分配0，不外推整个Runtime/游戏零成本或FPS。

## 失败、身份与交付闸

`original-01..10` 分别保留原生DLL/Content别名、原版预热等待、初始化时序、测试exe未LAA导致纹理OOM、Reach读回限制及Present仍绑定sink的原件；通过补准确宿主入口解决，没有改原版或扩充伪Terraria。`original-13` 为资源计数器在设备前patch引发CaptureManager提前初始化，后移到完整Initialize后解决。`original-16/17` 是测试未尊重.8上一帧keyState映射，`original-18` 是专项未接普通加载链的键盘finalized阶段；均保留原件并按实际入口纠正，没有降低产品期待。

网络早期无GPU宿主的Cloud/clearWorld失败原件保留，停止补假资源后改用完整Main。`network-full-01` 跳过原版角色创建阶段导致真实49 Spawn读取SavedPlayerData为空，改调用真实UI角色创建入口；专服输入按原版Windows UTF16LE而非ASCII后正常退出。`full-02` 的新context未先UpdateRuntime，纠正尾段调用顺序；`full-03` 是真实partial Draw，不改写为全视口已收，`full-04` 用同场景实际接收mask补齐。

四项外来资产保留：本地参考资料说明的既有修改、`.workbuddy/`、`issue112.txt`、`pr113.json`。`.local/view-unlock/retained-assets.json` 固定原字节；不夹带提交，不声称raw工作区干净。

源码/必要检测/注释/文档固定后一次集中独审；实际独审通过与协调准入后才执行07的一次普通完整ContinuousProcessing Release/net472/x86包和一次轻量备份换装/读回。源码、包、实际安装、恢复路径和所有者实测分开；未接受合并前不合并、不关闭、不开始基础移动。

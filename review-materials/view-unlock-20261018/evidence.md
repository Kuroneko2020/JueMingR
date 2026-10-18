# 视距解锁候选审查入口

任务为 [#124](https://github.com/Kuroneko2020/JueMingR/issues/124)，Draft PR 为 [#125](https://github.com/Kuroneko2020/JueMingR/pull/125)，分支 `codex/view-distance-unlock`，接受基底 `8bba8ffd2d24591e9efb1258ae0eac1e1d7d60df`。本文件所在提交是源码候选；正式检测、独审、准入和普通包身份分别绑定在 `.local/view-unlock` 的原件，不能用本文件代替这些结果。尚未合并，保留 `INCOMPLETE / releaseEligible=false`。

## CACHE-RANGE续办（本提交）

授权为`JMR-VIEW-CACHE-RANGE-20261018-v1`，freeze起点3ea1ba69，同#124/#125继续。基础目标.3—10、每Mapping±.02、双端OFF仅向[1,2]内回，原版选项只显示夹取、真正拖动才写回；实际低端38/128=.296875。现有512/1024观察与未知区、原武器权限、50格最小玩家半径保持。新输入事故只记录，未定位/未定责/未修复、重启后暂不复现且调查暂停。

当前产品有条件使用k=z的tile/tile2/wall三层，保留正确Direct反控：Reach cap2048、HiDef保守4096；不改所有者profile或画质。首选七层1:1扩展RT在当前Reach50/30均超轴，本轮没有实施该扩展组，静态模型不称GPU通过。原生普通七层足够时仍Native；不足时Direct。语义扩展、渲染不足、实际route分别记录。

开发原件统一在`.local/view-cache-range-20261018`；以下是各自真实执行身份，最终正常源码/输入/Host/Probe身份由新的candidate记录另锁，独审与主调度准入仍待完成，不能以开发PASS代替准入。

| 窄责任 | 原件及实际结论 |
|---|---|
| 同环境A/B/缓存 | baseline-01旧3ea50% Draw均值15.915ms；range-direct-01新direct50%15.91、30%31.965ms。full-range-01同进程交错direct/auto各两段30Draw：50%静止14.65/14.60→11.82/11.61ms，30%32.91/32.21→25.25/24.77ms；50%每段约121.4MB→36.3MB，30%199.1MB→46.7MB累计分配。原始CSV含全部慢帧，缓存并非每帧都更快，不称FPS。 |
| 移动/调整/Update | full-range-01保留移动与单向.02调整全CSV/route/alloc/GC/private；30%移动主要Direct，均值与direct约33ms同量级，不外推静止收益。1/2普通OFF/ON分别测量。HostView.Update六态×100000次零分配、约12.46—12.72ms，仅owner回调，不是完整游戏Update。 |
| 点样本/内容/模式 | full-range-01的.5/.3/.32三层普通、1world像素移动和反重力冻结叶：alpha一致；.3仅个别wall RGB相差1码，其余精确，不使用全图差异率掩盖细线。真实放置像素改变及早renderNow快照一次消费，fullmap/menu/noncache清理；White/Retro/Trippy实际.296875、legacy516×340与真实GetColor/Draw。 |
| 水/scene/资源 | full-range-01真实.3 Water Apply四态、已知双水池、冻结shader叶；quality0淡出physicalmask0/原前景1，quality1/tail mask1/前景0；queued3→2一次。1280↔1920设备reset、实际Invert/UI边界与资源计数。活跃水30%Draw成本含全部max与进程内存，不用旧e213数值重签。 |
| 原生容量真反例/足够反控 | forced-old-01加载旧3ea Host84D64D…，实际2560×1172/.75/raw1，原2304宽RT导致两端ROI全黑；test-only强制原direct相同相机/图格后恢复两端像素。native-route-06新Host在合法2560×1080：.75不足走Direct；.9足够保Native；60world移动与.9→.85同Draw，6个实际原生层全部覆盖最终相机。 |
| Native/Direct水路由 | native-route-06中b=P/0换域12次、双重力，双buffer身份/冻结波纹/精确world-origin不变；Native真实液体层兼mask、quality0也刷新。两路四态实际Apply及独立已知水池shader采样14277/15114、11032/11702像素变化。有限活跃水Native z1.203125均值2.75ms/max5.69，Direct z1均值6.28/max7.35，倍率不同，仅分别报告责任成本、不算收益。 |
| 失败与后续真Draw | full-range-01实际preResize queued2/cap1 IO/OOM+次级Dispose、外部lease/shared pending；新RT第二申请失败；owned copy真实End IO/OOM。native-route-06原版吞RenderWater IO被保留、Native scope借值/共享batch/XNA归还；optional batch OOM同Draw direct后20Draw不重试。后续3/20必须fpsCount实际递增。 |
| 关联 | range-consumers-01实际原版量化/MouseWorld/Shoot/CPU marker、512/513查询正反、section几何；最终正常专项另锁。CPU marker不是GPU像素，section几何不是新TCP；原3ea真实TCP/decoder已收区Draw按源相等与实际影响明示复用。 |

Native scope和新copy异常追加一处原版责任：Main.Draw无finally，DoDraw首异常后其guard不归还会短路后续Draw。仅本轮owned scope精确首异常归还进入guard/计时间隔，未知异常仍原对象传播；不处理其它原版异常。copy-01..04、native-route-02的早期后续仅调用记录，不冒称已真实执行。copy-05/full-range-01与native-route-06新增fpsCount增长反控。native-route-01请求1440受桌面窗口限高反复reset；03漏FilterManager.Update及04冻结checker重复反翻转是测试前提错误，原件保留；固定1080、真实manager步骤和正确unflipped shader输入后通过，未改产品采样期待。

prototype-01..12保留第二次linear过滤/相位/反重力等失败及修复；content早期真实renderNow失效反例保留。未知异常不吞，可选cache失败保持倍率/Lighting/map且记住资格，仅必要direct资源失败沿既有一次反馈安全状态。既有R1—R5/旅途/完整TCP未受影响部分按blob/tree与输入证明复用，旧96锁和交付身份不改。

冻结记录使用新路径`.local/view-cache-range-20261018/candidate-review.json`，后续审批必须独立admitted副本并由普通包引用实际路径/hash。当前未执行新普通包/安装；项目仍INCOMPLETE/releaseEligible=false，Draft/open，不合并、不关闭、不基础移动。

## 3ea及此前交付的历史记录

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

## 集中独审后的修复包

56d955c集中独审结论为NEEDS_FIX，未执行包或安装。R1普通NPC粗筛已改实际camera局部矩形；R2在直绘中恢复真实scene capture与WaterDistortion消费，物理scene/sky/swap和mask、world/4波纹对均按需复用；R3额外旧光照容量先在独立原版引擎完整准备，受控OOM不污染活数组并一次反馈/停止解锁/回安全目标。详见正式设计的集中独审修复边界。

开发证据：`repair-r1-red/run.log`为旧Host实际原版NPC拒绘RED；`repair-full-09/run.log`记录新Host09F3A15C…/MVID f4955c0f及probe7B46E00D…，真实普通/宽/Retained NPC，Invert Apply与物理像素/设备尺寸，水波四态完整Draw与冻结shader叶、资源/成本，候选Rebuild部分发布后的实际OOM和后20次native consumer均通过。水波mask为空的首个分歧保留在repair-04，补TileBatch立即变换后05起有真实mask与shader像素；07的基类私有字段反射测试错误保持原件，08及09已按声明类型读取。01的幂等Set(true)测试期待错误修后02已进入真实注入。开发早轮未单独保存所选临时Host哈希，只作为定位过程，不代替最终正式输入锁定。

旧正式GPU实际Host D89F36…的MVID为31277aae；其probe中性017941…、执行LAA B775…。随后CPU准备因临时JUEMINGR_COMBAT_GEOMETRY_CANDIDATE进入编译输入且SourceRevisionId变化，公共Host变为3F4D3C…/MVID8cfb9c16，probe801C88…。827源行相同不能把旧GPU改签成后者CURRENT；只读对照为`.local/view-unlock/identity-chain-analysis.json`。旧candidate-review.json是未准入草稿，八项CURRENT表述已撤回；修复源冻结后在当前子进程清除临时selector的一次PrepareOnly建立正常输入匹配，正式新记录逐项列CURRENT或明确REVIEWED_REUSE，旧GPU/Network不改写。

## 水波复审R4／R5返修

e213086仍未准入，旧阶段记录独立保存在`.local/view-unlock/candidate-review-e213086-unapproved.json`。本次仅改wave容量为足够即保留，以及自有mask批次与XNA设备Begin租约的正常／异常归还。未知异常及第二清理异常保留第一对象；原版共享pending工作不清改，实际OOM后的普通Draw不继承坏队列。

`water-r45-03/run.log`是开发首个完整闭环：真实相邻桶100次往返及.75收窄／再扩展，额外增长仅1次、已有685非中性wave像素保持、RT引用／代际不变；原版shader窄／再扩展／50%／Retained／反重力真实采样、自有batch稳定复用、原共享引用归还、可达Resize前queued>capacity的IO/OOM及第二Dispose异常、后20次普通Draw都通过。01/02的XNA设备Begin租约未归还真实失败原件保留。新正式入口为既有`test-world-object-text.ps1 -Scope ViewWaterRepair`，不把这个有限scope标成完整ViewOriginal/联网已重新执行；未受影响项只明示复用。

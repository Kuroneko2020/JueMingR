# 旅行菜单候选证据

授权与当前状态只查 [#122](https://github.com/Kuroneko2020/JueMingR/issues/122)；固定基底 `0ef3c62dcf0c365aee5d5cfcfbb5492b70b841f3`，原始委托 `JMR-JOURNEY-MENU-20261018-v1` 保存在本地 references 原包00—08。此文件仅提炼本批证据，不替代任务授权。

## 当前结论

这是实现和验证中的工作检查点，**不是已完成的完整候选**。Debug 编译、结构、F5/绑定 CPU、真实原版 Locked/OFF 与非 Active 保存/无 R 回读、有效难度预测接缝通过；完整 Journey 在创建图形设备前因 XNA 无可用适配器停止，尚未运行 Active/UI/保存强杀义务。未进行完整独审、包或安装，不能据此进入07。

实现采用原身份不改的准确读侧资格；借用持续力量在真实原版保存链中写出激活前由原版生成的小型持久子流。设计保护新增物品和正常进度，不依赖退出清理或下次启动修复。其完整运行结论仍待下表闭合。产品边界见正式[行为合同](../../docs/功能介绍/旅行菜单行为合同.md)和[设计](../../docs/设计/旅行菜单实现设计.md)。

## 实际执行与原件

以下路径相对仓库；长日志、原版源码、文件、纹理及二进制仅留本机，不进入 Git。

| 范围 | 实际结果 | 原件 |
| --- | --- | --- |
| Debug 完整生产编译、结构 | PASS，0 警告/错误；不是业务验收 | `.local/journey-menu-20261018/build-reset-20261018.log` |
| F5/动态绑定 CPU | PASS；真实布局/注册/分派，窄 fake 只代表业务 port | `.local/journey-menu-20261018/ui-cpu-build/{build.log,f5-cpu.log,execution.json}`，142 个实际 Compile 输入和运行依赖身份 |
| 原版 JourneyCpu | PASS；准确安装、Locked 拒门、OFF 100 次不构造研究/基线、不改原力量/研究；实际 Player.SavePlayer/WorldFile.SaveWorld 和独立原版读回 | `.local/journey-menu-20261018/native-validation/cpu-final-20261018-0630/` |
| 无 R 独立读回旧 writer | PASS；实际原版 Player.LoadPlayer、世界 GetAllMetadata、原版 LoadFromWorld；人物/世界0、Torch19、原研究和 creative 子流 | `.local/journey-menu-20261018/native-validation/reader-tenth-20261018-0622/`；原 writer 为 `cpu-fifth-20261018-0535`，两端身份分别保留 |
| 有效难度预测接缝 | PASS；真实世界子流与其 GameMode 前置字段捕获→新进程真实 decoder；null/x0.5/x1/x2/x3，原版 Main.Difficulty 和新 Zombie life45/22/45/90/135；稳定前提保留、倍率变化旧未来失效 | `.local/journey-menu-20261018/prediction-second-20261018-0605/{input-hashes.json,probe.log,receiver.log}` |
| 路由与有限 reader 早期反例 | PASS；旅行源路径/两项 CPU/显式图形边界、未知可执行路径保留 unknown；8 个错误身份/未解决失败/发布越级拒绝 | `.local/journey-menu-20261018/route-checks-20261018.log`、`entry-contract-01.log`；完整正向准入仍待独审/主调度与正常打包入口 |
| 完整 Journey 原版/GPU | **NEEDS_GPU**；静音初始化后 ProbeGraphics ctor93 报 `No XNA graphics adapters are available before device creation`，未进 UI helper | `.local/journey-menu-20261018/native-validation/full-first-20261018-0631/{input-hashes.json,probe.log}` |
| 图形环境定向只读 | 当前 pwsh/explorer 都属会话3，`query session` 为 Disc；RTX3070/Intel UHD 仍报正常。未更改会话、显示或驱动 | `.local/journey-menu-20261018/graphics-environment-20261018.json` |

CPU final 与完整图形首跑使用同一固定身份：probe `43650152509213749230A9D1E5DB094685412245CB8A487694902399BE522D10`；Host `8CF79A96EE89BF81AEC3F17C62505FE1E7A675BCF453E481C30445807DDB5A91`；NativeJourneyChecks `C6AB47A666B60BC1603814BD249784FB91D495DC933673ADE429F3349132F5C1`；NativeJourneyUiChecks `33F20DF82B9FA132945A69D3206F38EE1049C2359C05BEE2D789AE9C60079360`。原件另有时间戳及 source 早于 exe 的核对。

检查点最后只校正 NativeJourneyChecks 的四条通用断言说明和三行 reader 注释，当前源 SHA-256 `705A5FFB7F848731CECC6068575D07CCB820F1C9E35669106F04BD623E3C9073`；布尔条件、调用顺序与逻辑不变，不回填旧执行身份。恢复后先由既有 fixture 准备入口窄重编，再继续 Journey。UI CPU 的142个实际编译输入已逐项核当前字节相同；预测生产四文件及实际执行的预测测试同样未变，精确复用依据另存 `.local/journey-menu-20261018/prediction-reuse-20261018.json`。

独立文件 reader 采用原版 `dedServ=true` 的无图形 PlayerFrame 分支；它仍真实执行 LoadPlayer/Deserialize，并在开始和结束断言没有加载任何 `JueMingR.*` 程序集。它证明文件读取，不证明客户端图形启动。力量段观察使用测试 Harmony owner 只记录原版 reader 的原字节和位置，不替换反序列化结果。

## 尚未执行的完整义务

01/06 的真实 Shell 开启、原版搜索、左右键复制、背包交接和实际使用，各类力量消费者、收窗后持续效果、原生3/3、四身份 Active 保存、真实改名、异常保存、A/B在途保存、人物 Reset 归还世界力量、自有 writer 强杀后无 R 读回，以及代表工具/加工/物品/HUD 共存均已编写针对性检查，**尚未因断言存在而算通过**。F5/原版菜单图和首次/稳定打开/收窗/关闭各100帧计时同样尚未产生。恢复可用交互图形前提后，应从当前固定产物继续显式 Journey，不重演有效研究或旧 writer。

自动/退出保存的共同链已从锁定原版源码追到实际串行化入口；当前仅正常非 Active 保存实跑。预测证据限定真实子流 decoder 和一个原版 NPC 属性消费者，不冒称整 worker 管道或全 NPC 家族复测。

## 原始失败与自查处置

| 原件 | 具体原因与处置 |
| --- | --- |
| `cpu-first-20261018-0502` | 安装失败又被 cleanup 异常遮盖；生产保留原目标/原异常，各本 owner 已安装目标独立清理，不影响其它 owner |
| `cpu-second-20261018-0511` | .NET/Harmony 不支持该闭泛型 DrawSelf 包裹；改非泛型 UICreativeItemGrid.DrawSlot 的原生44×44、真实grid hover短接缝，准确 Hook 后续 CPU 已安装通过，实际输入待 GPU |
| `cpu-third-20261018-0518` / `cpu-fourth-20261018-0526` | 最小原版 world 保存缺 Manifest/Bestiary 初始化；按真实 section 异常补原版实例，未跳保存/换假 ABI，后续12段及实体文件成功 |
| `cpu-fifth-20261018-0535` | writer 成功，reader beforefieldinit 先触 Main；拆开路径初始化和 NoInlining 原版读取 |
| `reader-sixth-20261018-0546` | 原版 Item 样本读真实 Player0 衣物色；补真实 Player0 前提 |
| `reader-seventh-20261018-0558` / `reader-eighth-20261018-0606` / `reader-ninth-20261018-0616` | LoadPlayer catch 隐藏 PlayerFrame→Item.GetDrawHitbox 的无客户端实例异常；前置原版 Deserialize/PlayerFrame 获取完整异常，reader 使用原版 headless 分支，原 LoadPlayer 成功期待保留，tenth通过 |
| `prediction-20261018-0602` | 测试漏了 PredictionWire 在世界子流前单写的 GameMode；准确补同一前置字段，未改生产或正常期待，second通过 |
| 生命周期源码自查 | Full Reset 与 Player-only Reset 原先共用弃基线，后者会遗留借用的世界力量。拆精确职责，Player-only 在旧实际归属有效时先归还，再让原版重置人物；真实反例检查已加，仍待 Active 执行 |

实现自查已集中核本批 owner、必要原版读点/保存目标、直接调用者和关键说明：身份/资格分离、不可变31/16字节子流、ThreadStatic只沿实际同步串行化、旧在途持有原基线、UI占用只限制新操作、原版 Reset 生命周期和失败责任。元数据只在初始化解析，稳定注册/布局/研究复用；OFF 100 次探针耗时0.2657ms，包含测试反射，不外推 FPS。完整图形成本和功能结果待补，不以静态自查替代。

文件写权：整合者拥有 Feature/Host Journey、Runtime组装、保存/来源/预测窄接缝、脚本和正式文档；UI同伴拥有 F5四文件、Hotkeys两文件和其三项专用测试；原版研究同伴拥有 NativeJourneyChecks 测试。完整差异仅这批38个授权文件，既有本地参考说明等外来dirty明确排除。未参与者完整独审尚未开展。

## 保留边界

不操作所有者游戏/真实存档；隔离探针只使用自造可丢弃角色世界且全程静音。真实物理掉电、真人多人、所有显示/输入法/设备和量化FPS未获通过结论；工程探针不代替所有者体验或普通完整包正常启动。候选仍 INCOMPLETE/releaseEligible=false，外来原件保留导致真实clean=false，不属于本任务提交。

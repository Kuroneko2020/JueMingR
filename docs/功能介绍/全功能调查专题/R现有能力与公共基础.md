# R现有能力与公共基础

本篇是[调查任务 #83](https://github.com/Kuroneko2020/JueMingR/issues/83)的固定基线知识，不是未来功能合同或实时迁移进度。R生产基线为 `df8a029873176276d0f620d32b0ac02ae9ea7744`，Legacy为 `6ac6356c0c564f43284590c168855e96c50e13f7`。本轮只读核对已接受能力的实际入口及可复用边界，不重做已接受实现的完整审计；所有既有实机、图形、多人、FPS限制保留。

## 1. 普通结论

后续功能已有可用的窗口、输入、快捷键、有限共享观察、受控物品操作和保存基础。它们足以让新功能沿既有入口接入，但尚不存在一个已经实现、可直接处理所有物品使用、装备恢复、施工及持续记录的公共系统。后续应由第一个真实消费者带出必要的窄扩展，不能把架构图中的职责标签当成现有实现。

尤其要区分三件事：**同一会话身份**不是所有记录共用一个文件；**同一输入许可**不是所有动作必须串行；**同一物品类型**不是同一件可安全恢复的物品。

## 2. 固定基线的已接受用户能力

下表的接受依据沿用各自合同与Issue，表示相应用户结果已获接受；本轮没有重新运行这些检查。旧说明中保存的早期阶段描述应结合后面的接受章节阅读。

| 页面或入口 | 基线真实能力及边界 | 主要合同、接受入口 |
| --- | --- | --- |
| F5及配置 | 十二分类导航、内容滚动、拖动和按下释放保护；已验证游戏根下保存开关/窗口位置。选页、滚动及捕获不跨启动保存 | [F5设计](../../设计/F5控制界面样板与UI基础.md)、[配置设计](../../设计/配置持久化与用户数据布局.md)，#30/#34 |
| 笔记 | 多篇编辑、选区、删除、安全保存、悬挂阅读、独立区域与字号；已有R格式升级，首轮无Legacy导入、完整撤销或富文本 | [笔记合同](../笔记功能行为合同.md)，#42/PR #43 |
| 物品 | 可靠拾取/正常手动开容器产物触发的自动堆叠、出售、丢弃；类型名单内联多选、替换/移除；丢弃提示可单独保存 | [物品合同](../自动存放功能行为合同.md)、[实现设计](../../设计/物品自动处理实现设计.md)，#46/PR #47；#48残余风险继续延期 |
| 公共快捷键 | 真实动作注册、统一物理主键新沿、左右修饰键、五鼠标键、唯一录入窗、可靠保存；原版重合在设置时提醒并允许保存 | [快捷键合同](../统一快捷键功能行为合同.md)、[实现设计](../../设计/统一快捷键实现设计.md)，#51/PR #52及后续各项接入 |
| 信息：实体显名 | 敌怪名称/生命、动物名称、NPC名字/类型，独立颜色字号及快捷键 | [三项显名合同](../敌怪动物与NPC显名功能行为合同.md)，#55/PR #56 |
| 信息：附近目标 | 生命水晶、生命果、世界魔力水晶、睡眠碎岩龟、巨型龙蛋，完整对象三箭头及独立颜色；原版有效金属探测能力是资格 | [附近目标合同](../附近目标探测与高亮功能行为合同.md)，#59/PR #60 |
| 信息：世界文字 | 宝箱名字、牌子、墓碑，多种显示模式、独立数量/样式；正常开箱登记角色×世界开过位置，显示关闭仍登记 | [世界文字合同](../宝箱牌子与墓碑显示功能行为合同.md)，#63/PR #64 |
| 信息：摘要 | 群系、感染、幸运、渔夫任务；共享信息窗位置与独立样式。三种NPC资格不同，渔夫资料读取不得引发任务副作用 | [摘要合同](../世界感染幸运值渔夫任务与信息窗位置行为合同.md)，#71/PR #72 |
| 地图/战斗：指引与提醒 | 稀有生物全向指引、旅商方向、固定部分非战斗用品提示；装备提醒不是自动换装 | [方向与装备合同](../稀有生物旅商方向与装备提示行为合同.md)，#77/PR #78 |
| 杂项 | 独立单人游商测试，遵循真实到访前提与确认；不是日常自动生成NPC | 同上合同，#77/PR #78 |
| 地图：死亡/天数 | 当前角色×世界死亡次数、六条分页、直接死因及完整原句、观察累计天数；默认关闭的全屏死亡点，128/256/512/1024、默认256，显示数量不删历史 | [死亡与天数合同](../死亡信息死亡详情世界天数与死亡点常驻行为合同.md)、[实现设计](../../设计/死亡记录世界天数与地图死亡点实现设计.md)，#81/PR #82 |
| 公共提示 | 普通功能名称悬停解释用途；清楚按钮不重复说明；同一提示布局/缓存，复杂窗口仍有自己的上下文帮助 | [F5文案与提示设计](../../设计/F5控制界面样板与UI基础.md)，#73/PR #74及#77/PR #78 |

F5上排为物品、杂项、地图、查询、笔记、关于；下排为蓝图、钓鱼、战斗、信息、增益、移动。分类和压力布局存在不证明查询、钓鱼、增益、移动或蓝图业务已恢复。最终profile实际装配入口见R01，后续需使用完整profile链，不能只看到Feature源码就认定当前包包含该功能。

## 3. 现有公共实物、适用边界和候选扩展

### 3.1 生命周期与身份

`SingleFeatureRuntime`虽然保留历史名字，实际持有多个 `IRuntimeFeature`，首个观察后冻结装配；在会话边沿先调用开始/结束，再推进Update。单项异常交该Feature清理，吞住清理异常只表示隔离，不证明清理成功。`Generation`在新会话开始递增。`ItemSessionProbe`用本机Player对象、ActiveWorldFileData对象、连接Socket和netMode构造运行期token；死亡但仍active不会自己结束会话。（R01、R02）

运行期token不是持久档案身份。死亡/天数只在可靠本地角色文件、非SSC、有效世界GUID及普通客机连接就绪时取得pair；使用 `world-records-v1` 域和cloud/local+实际角色路径+世界GUID。开过位置已有自己的 `opened-v1` 键域与集合语义，二者没有共用一个持久事实库。未知身份不应降级为重名字符串或伪空数据。（R03）

**候选**：足迹、地图标记、蓝图实例可复用可靠角色/世界身份来源和会话代次，但各自决定自然归属、载入准入、迟到结果和文件语义。角色改名、文件移动、云/本地切换及SSC的产品取舍须按组明确；不把现有路径键说成全球永久角色ID。

### 3.2 输入与快捷键

`HostInputState`拥有统一前台状态、重新激活隔离和本帧输入许可，顺序为BeginUpdate→原版映射后→键盘刷新后样本。样本含固定261个键鼠位；被阻止的按下仍维护物理沿，恢复焦点要等真实中立输入，合成释放不能结束占用。F5/Notes/物品/绑定窗消费输入后，不能从另一套设备轮询重新触发玩法。（R04）

最终profile的 `HostHotkeys`注册23个动作：群系1、三项物品3、实体显名3、五目标5、世界文字3、摘要3、信息窗调整1、方向/装备3、死亡点1。这个数只用于核对本基线接线，不是功能数量；其中信息窗调整是一次动作。`HotkeyBindings`创建前注册，持久文件保留合法未知动作而不分派。（R05）

完整按键合同为0—3个不同的左右Ctrl/Shift/Alt加一个主键，修饰掩码精确匹配；主键已按住后才补修饰不会产生新沿。F5保留、Esc取消录入，滚轮不绑定，Win不作修饰。原版冲突仅提交时核对和提醒，不能为了快捷宣告恢复旧三键特例，也不能把文本场景Ctrl+C扩为全局禁绑。

**缺少的实物**：此公共输入并不是已经实现的自动选槽、持续ItemCheck接管、装备临时恢复或地图编辑手势系统。后续需在同一输入所有权下补对应职责；真实冲突才让路，不禁止一切同时动作。

### 3.3 NPC与Tile观察

`NativeNpcObservation`是实际共享NPC事实读取器。每次完成游戏更新换epoch，对槽按Basic/Direction/Danger/Housing demand补缺字段。方向不强迫所有消费者准备名字、血量排版；实际消费者由Composition Root共用同一实例。目标槽的原版对象身份、generation与业务选择策略仍需领域复核。（R01、R06）

`WorldTileObservation`由五目标和世界文字实际共用，每Tick清本轮缓存，按当前视口/缩放形成范围；单块读取检查世界边界和客机TileLoaded。它只提供Readable、Active、Inactive、Type、FrameX/Y，最多保留65536个缓存键，溢出继续正确直读。它不是完整地图/世界索引，也没有墙、电线、油漆、液体或容器内容。（R07）

**候选**：捕捉和瞄准可讨论复用同一阶段的NPC基本事实；收获、挖矿、施工可讨论窄Tile读值。必须先核对这些动作发生阶段与现有完成Update后的窗口是否一致，以及动作前是否需要重新观察。不能把缓存的显示值直接当写操作许可，也不能扩成每Tick全功能快照。

### 3.4 物品资格、操作和选择器

已存在三个typed请求及 `IItemOperationPort`，执行结果区分不适用、拒绝、执行中、完成、部分、失败、取消、超时、未确认。`ItemOperationOwnership`只管理三个真实动作范围：出售保护58槽；丢弃保护来源且有垃圾桶协调；存放保护选择来源及共享批次。执行中/未确认/超时/失败保留相关所有权，确定结果才释放；换会话不恢复库存或强清原版锁。（R08）

`ItemHostObservation`的槽观察包含type/prefix、数量、最大栈、对象实例和保护状态；相同值的新对象仍更新成员身份，但不凭引用变化制造业务revision。原版一次同步拒售的完整等值恢复有单独确认路径，不能推广到任意等值替换。准入门包括手动动作、原版锁、使用忙和交互锚；门解除也会更新观察，不能只看库存值。死亡期间在途身份读取与新动作许可分开。（R09）

可靠来源是实际PickupItem外层或正常TryOpenContainer成功产物进入主包，非币0..49、54..57；卖＞扔＞存由物品领域拥有。三项全关不登记，重开不追溯。新增开袋、提炼、钓鱼产物不能只调用一个通用“库存增加”事件；应核对实际因果来源、材料/产物边界和手动保护，沿该合同接纳真实新消费者。#48旧原始槽报文跨批次可达性仍是延期项，本调查不自行重启修复。（[现行物品设计](../../设计/物品自动处理实现设计.md)）

`ItemSelection`目前是卖/扔名单的临时owner：打开时从合法主包冻结非币类型快照，按第一次出现顺序去重；展开期间不随背包变化刷新，确认合并最新偏好。类型选择不动真实物品。它没有完整物品目录、名字搜索、来源/用途查询或可复用的钓鱼规则语言。（R10）

**候选**：共用图标/度量/选择手势；全目录查询的索引和规则编辑仍归相应领域。不能直接把主包名单选择器当全物品查询，也不能把所有新自动化塞入现有三动作owner。

### 3.5 UI、地图与资源

F5的 `F5RowLayout`、`F5ControlRenderer`、`UiTextMetrics`、`F5HintLayout`、样式窗和绑定窗均已有真实消费者。内容、字体、尺寸、布局代次与命中共用冻结几何；普通移动只改变投影，跨布局旧按下应取消。样式/绑定/死亡详情等模态由壳层互斥。Notes文本与悬挂、Items内联选择仍拥有各自业务草稿。（R11）

死亡地图现有 `DeathMapGeometry.Project`覆盖死亡图标的原版坐标参数及命中范围，`DeathMapHooks`负责全屏地图实际绘制接线；这是可借鉴的已接通入口，不是已经存在的多图层地图编辑器。标记、足迹路线/时间轴、蓝图编辑仍需自己的交互与投影合同，不得把“跳转地图视图”改为玩家传送。（R12）

查询结果/名单、颜色字号、分页详情/全文已有不同交互族；时间轴、地图编辑、蓝图库/材料清单属于后续新内容。它们应复用基本裁切、文字度量、提示、输入和取消机制，内容布局可以独立。需组合验证小视口、大字/长文本、IME、滚轮、Esc、鼠标按下后换布局、失焦、死亡与换世界。

### 3.6 持久化与事实记录

`PreferenceDocument<T>`允许本次内存偏好与可靠落盘状态分开，待写最新值可以合并；`DocumentWorker<T>`承载明确文档命令，快捷键可靠保存后才切有效表。`AtomicFileDocument`提供文件身份、排他锁、同目录临时文件、替换/备份/确认与失败保护，业务格式仍由各领域codec解释。（R13）

死亡历史是不可丢事件及不可变页/索引；世界时间是累计贡献；开过位置是去重集合；Notes是用户正文。它们的数据价值、合并规则和恢复边界不同。死亡显示关闭不停止必要死亡/时间采集，但这不能直接规定足迹记录或标记编辑的生命周期。

普通配置根为验证游戏目录下JueMingRData，偏好在该安装内跨角色/世界共享。没有全产品导入器、自动旧数据迁移或通用数据库。蓝图资产和足迹旧数据是否导入按ADR-0009分别评估；本次不读取真实用户资料验证格式。

## 4. 已有自动验证入口与后续组合

现有正规构建 `scripts/build.ps1`包含工作量核心，按完整diff选择Host组；开发包继承。具体入口和关闭/稳定/真实变化/规模/失败反例见[工作量回归设计](../../设计/高频路径工作量回归.md)。本轮没有运行构建、产品测试、基准、原版受控执行或游戏；这里只定位之后实施应复用的入口，不将既有测试断言算成本轮通过。

| 新消费者触碰处 | 应保护的已有消费者和反例 |
| --- | --- |
| 输入/模态/快捷键 | Notes IME、Items选择、样式/绑定/死亡详情；被拦按下不延迟触发、跨布局释放不点错 |
| 选槽/耗材/装备 | 存卖扔在途来源、鼠标持物、真实手动使用；等值新对象不能恢复为旧对象，人工改动不能被临时还原覆盖 |
| NPC/Tile共读 | 显名/方向/五目标/世界文字；关闭消费者撤销需求，真实变化仍更新，不为少读漏目标 |
| 地图呈现 | 原版拖动/缩放/ping/最近死亡点、新死亡点与后续标记/路线；裁切及命中用同坐标，显示限额不删记录 |
| 文件/后台 | Notes、绑定、偏好、开过集合、死亡事件/时间；坏/未来格式保护、旧结果不串新会话、事件不被最新值合并丢失 |

## 5. 固定源码证据

所有R链接固定到上述生产SHA。行段支持相应窄事实，不代表逐行全面代码审查。

| ID | 位置与支持结论 |
| --- | --- |
| R01 | [Phase0SLoadChainHost.cs 850–912](https://github.com/Kuroneko2020/JueMingR/blob/df8a029873176276d0f620d32b0ac02ae9ea7744/src/JueMingR.TerrariaHost/Phase0SLoadChainHost.cs#L850-L912)：InitializeRuntime/UpdateRuntime实际装配与共读实例 |
| R02 | [SingleFeatureRuntime.cs 6–98](https://github.com/Kuroneko2020/JueMingR/blob/df8a029873176276d0f620d32b0ac02ae9ea7744/src/JueMingR.Platform/Runtime/SingleFeatureRuntime.cs#L6-L98)、[ItemHostObservation.cs 150–170](https://github.com/Kuroneko2020/JueMingR/blob/df8a029873176276d0f620d32b0ac02ae9ea7744/src/JueMingR.TerrariaHost/Items/ItemHostObservation.cs#L150-L170)：Runtime代次与ItemSessionProbe身份 |
| R03 | [HostDeathRecords.cs 112–149](https://github.com/Kuroneko2020/JueMingR/blob/df8a029873176276d0f620d32b0ac02ae9ea7744/src/JueMingR.TerrariaHost/DeathHistory/HostDeathRecords.cs#L112-L149)、[OpenedContainerObserver.cs 50–57](https://github.com/Kuroneko2020/JueMingR/blob/df8a029873176276d0f620d32b0ac02ae9ea7744/src/JueMingR.TerrariaHost/WorldObjectText/OpenedContainerObserver.cs#L50-L57)：持久身份准入与不同领域pair |
| R04 | [HostInputState.cs](https://github.com/Kuroneko2020/JueMingR/blob/df8a029873176276d0f620d32b0ac02ae9ea7744/src/JueMingR.TerrariaHost/Input/HostInputState.cs)：BeginUpdate/AfterMapping/AfterKeyboardRefresh，单一样本与真实释放 |
| R05 | [HostHotkeys.cs 16–73](https://github.com/Kuroneko2020/JueMingR/blob/df8a029873176276d0f620d32b0ac02ae9ea7744/src/JueMingR.TerrariaHost/Hotkeys/HostHotkeys.cs#L16-L73)：最终完整profile的23动作装配 |
| R06 | [NativeNpcObservation.cs 10–77](https://github.com/Kuroneko2020/JueMingR/blob/df8a029873176276d0f620d32b0ac02ae9ea7744/src/JueMingR.TerrariaHost/Npcs/NativeNpcObservation.cs#L10-L77)：BeginTick/TryRead按需字段 |
| R07 | [WorldTileObservation.cs](https://github.com/Kuroneko2020/JueMingR/blob/df8a029873176276d0f620d32b0ac02ae9ea7744/src/JueMingR.TerrariaHost/World/WorldTileObservation.cs)：BeginTick/TryView/Read/ReadCurrent，有限视口事实及容量 |
| R08 | [ItemOperations.cs](https://github.com/Kuroneko2020/JueMingR/blob/df8a029873176276d0f620d32b0ac02ae9ea7744/src/JueMingR.Platform/Items/ItemOperations.cs)、[ItemOperationOwnership.cs 20–77](https://github.com/Kuroneko2020/JueMingR/blob/df8a029873176276d0f620d32b0ac02ae9ea7744/src/JueMingR.Platform/Items/ItemOperationOwnership.cs#L20-L77)：typed请求、相交范围与未知保留 |
| R09 | [ItemHostObservation.cs 35–149](https://github.com/Kuroneko2020/JueMingR/blob/df8a029873176276d0f620d32b0ac02ae9ea7744/src/JueMingR.TerrariaHost/Items/ItemHostObservation.cs#L35-L149)：观察身份/准入门/Matches及同步拒售恢复 |
| R10 | [ItemSelection.cs 24–74](https://github.com/Kuroneko2020/JueMingR/blob/df8a029873176276d0f620d32b0ac02ae9ea7744/src/JueMingR.TerrariaHost/Items/ItemSelection.cs#L24-L74)：Open/Select/Confirm，主包类型选择边界 |
| R11 | [F5Shell.cs 190–279](https://github.com/Kuroneko2020/JueMingR/blob/df8a029873176276d0f620d32b0ac02ae9ea7744/src/JueMingR.TerrariaHost/F5/F5Shell.cs#L190-L279)：统一输入消费者及副窗互斥；完整语义见F5设计 |
| R12 | [DeathMapGeometry.cs 10–22](https://github.com/Kuroneko2020/JueMingR/blob/df8a029873176276d0f620d32b0ac02ae9ea7744/src/JueMingR.TerrariaHost/DeathHistory/DeathMapGeometry.cs#L10-L22)、[DeathMapHooks.cs](https://github.com/Kuroneko2020/JueMingR/blob/df8a029873176276d0f620d32b0ac02ae9ea7744/src/JueMingR.TerrariaHost/DeathHistory/DeathMapHooks.cs)：死亡地图几何和实际Hook |
| R13 | [PreferenceDocument.cs](https://github.com/Kuroneko2020/JueMingR/blob/df8a029873176276d0f620d32b0ac02ae9ea7744/src/JueMingR.Platform/Settings/PreferenceDocument.cs)、[DocumentWorker.cs](https://github.com/Kuroneko2020/JueMingR/blob/df8a029873176276d0f620d32b0ac02ae9ea7744/src/JueMingR.Platform/Persistence/DocumentWorker.cs)、[AtomicFileDocument.cs](https://github.com/Kuroneko2020/JueMingR/blob/df8a029873176276d0f620d32b0ac02ae9ea7744/src/JueMingR.Infrastructure/Storage/AtomicFileDocument.cs)：偏好/命令/机械存储分层，语义及失败边界见配置设计 |

## 6. 后续使用范围

本篇描述固定SHA的实物。开始后续组时只核对该组依赖的接口、原版版本、已接受决定和具体未知是否变化；不默认重新调查所有已完成R功能。依赖关系与开发分组由总报告和各剩余功能专题合成，任何候选扩展均须后续授权。

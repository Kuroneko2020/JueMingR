# 敌怪、动物与 NPC 显名：Legacy 行为与已确认新版目标

**性质：Legacy 静态行为提取与已确认的新版实施目标。所有者于2026-09-11接受第8节用户结果与明确差异；三个新版显名功能尚未实现、未获实机接受。第2–7节旧代码、历史记录与风险没有整体升级为新版施工规定或运行验证。** 需求确认见[任务 #53](https://github.com/Kuroneko2020/JueMingR/issues/53)，当前实施任务与阶段只查[总跟踪 #7](https://github.com/Kuroneko2020/JueMingR/issues/7)。

本批按所有者明确决定成组设计和实现；需求PR只交付提取与目标确认，生产实现另立任务。Legacy 源码固定为 `6ac6356c0c564f43284590c168855e96c50e13f7`，提取时R基础固定为 `bf56e93627e5476e4f1a8f3a1628f107d4e1ea5c`。未跟踪说明另记文件身份。Legacy证据仍为 **V0 静态提取**：C 表示实际代码，B 表示测试源码中的断言，D 表示旧说明/历史记录，A 表示所有者明确决定；“建议”和“未查明”不混入旧事实。提取与需求最终化没有运行旧程序、构建、行为测试、游戏或性能采样，测试源码不记为本轮通过。

## 1. 给所有者的一页摘要

| 旧功能 | 怎样用、看到什么 | 容易误解的地方 |
| --- | --- | --- |
| 敌怪显名 | F5“更多信息”的对应行，配置、开启、关闭及末尾小键盘。对象上方显示名称，下面显示当前/最大生命值 | 血量随显名一起开关，没有独立血量开关、血条或百分比。多段对象有主体/分组规则，不是把所有段生命相加 |
| 动物显名 | 同页独立开关和配置，显示一行名称 | 资格来自原版字段/表及检查顺序，不等于日常语言里的所有动物；不自动捕捉，不点击交互 |
| NPC显名 | 同页“配置、名字、类型、关闭”及小键盘。“名字”和“类型”是两个显示模式 | 城镇对象及骷髅商人为候选；引擎 `NPC` 集合还包含敌怪和动物。旧热键记忆与 F5 模式按钮不完全同步 |

三项各有颜色和字号，只有控件及部分绘制机械过程共用，修改一种样式不会按设计同时修改另外两项；固定名单中的金色动物另用金色，覆盖普通动物颜色。共同底层实际读取同一 `Main.npc` 集合，但敌怪的生命主体、NPC 的名字模式、动物资格仍是独立规则。

**已确认首轮完整交付三项显示、敌怪两行生命值、NPC 两种模式、各自样式配置与默认恢复、可靠保存、现有 F5 真实入口和公共快捷键。** 同时消除旧路径里的箱子记录副作用、模式恢复分叉、过期身份和远处对象挤掉屏内名额风险；这些是新版目标，不冒称旧版已经修好。

提取时 R 可复用 F5 行控件、字体度量、输入仲裁、偏好保存、统一键鼠快捷键及 Session 生命周期。三条显名行仍只是外形；没有真实后端或动作注册。实体观察和世界标签绘制需最小补齐，不建设全游戏实体平台。已确认产品目标集中在[第 8 节](#8-已确认的新版首轮目标)，技术实现选择由后续实施者依据当前版本证据设计。

## 2. 三条真实调用链与用户操作

### 2.1 入口、开关与重新开启

三项注册为 `enemy_name_labels`、`critter_name_labels`、`npc_name_labels`，都接样式配置入口。F5“更多信息”前三行依次为敌怪、动物、NPC；按钮顺序如下。注册元数据的 `Implemented=true` 不是本轮运行或多人证明。[C：Z01、Z02]

| 功能 | 实际行控件顺序 | 状态及首次默认 | 重新开启/重启 |
| --- | --- | --- | --- |
| 敌怪显名 | 行名→配置色块→开启→关闭→小键盘图标 | `InformationEnemyNameLabelsEnabled=false` | 开启/关闭幂等设值，不清样式；成功保存后重启恢复该bool |
| 动物显名 | 行名→配置色块→开启→关闭→小键盘图标 | `InformationCritterNameLabelsEnabled=false` | 同上，独立bool/样式 |
| NPC显名 | 行名→配置色块→名字→类型→关闭→小键盘图标 | `InformationNpcNameLabelsMode="Off"`；有效模式Name/Type/Off | “名字/类型”直接选择并启用该模式；没有另一个已启用bool。已保存模式重启恢复 |

普通配置/模式按钮单击。小键盘是**行尾键盘图案**，不是固定数字小键盘绑键：双击打开旧快捷键小窗，单击不打开；旧窗显示当前/未绑定、开始录入、清除、关闭，Backspace可删除绑定、Esc取消录入。完整旧键位范围转查[统一快捷键的Legacy提取](统一快捷键功能行为合同.md)，新版遵循已接受的公共机制，不能把旧Backspace规则带回R。[C：Z02、Z04]

三条生产链分别为：

- **敌怪**：注册/敌怪行 → `LegacyUiActionService.Information` 设敌怪bool → `AppSettings`及`SaveAll` → 信息世界层开关门 → 同一对象集合过滤敌怪并建立分段信息 → 名称、生命及样式投影 → `DrawWorldLabelWithSubLabel` 两次实际文字绘制 → 关闭后该消费者停止取标签，静态缓存仍驻留。[Z01–Z03、Z07–Z11]
- **动物**：注册/动物行 → 同文件设动物bool → 独立设置及保存 → 同一世界层 → Critter资格（在NPC分支之后）→ 类型名称与普通/金色样式 → `DrawWorldLabel` → 关闭该消费；不捕捉或交互。[Z01–Z03、Z07–Z11]
- **NPC**：注册/NPC行 → `Name/Type/Off`模式命令 → 独立模式及样式/保存 → 同一世界层 → 城镇/骷髅商人资格 → 对应名字解析 → `DrawWorldLabel` → Off停止该消费；名字缓存没有随关闭清空。[Z01–Z03、Z07–Z10]

反向核对：`GetLabels`的实际消费者是上述共同世界标签路径，未发现捕捉/瞄准消费此缓存；不是三个独立服务各扫一份列表，也不是已经存在可服务任意实体业务的Observation平台。测试 facade 调到同一辅助函数不等于另一条生产接线。[C：Z07、Z08]

### 2.2 NPC快捷键恢复与按钮不同步

旧快捷键经 `RuntimeAutomationDispatcher → FeatureToggleHotkeyService.Tick → FeatureToggleProfile.Toggle`，直接改相同配置字段。敌怪/动物反转bool；NPC不是Name→Type循环，而是**当前非Off→Off；Off→历史非Off模式**。历史来自`HotkeySettings.LastNonOffModeByTargetId`，默认空；只有快捷键执行非Off→Off时写入，缺少合法历史则以`noLastNonOffMode`阻止开启。[C：Z04]

F5模式按钮直接保存`AppSettings`，没有同步这份历史。因此“先在F5选类型，再用F5关闭，第一次按热键”可能无法恢复；已有旧热键历史时也可能恢复更早模式。这是实际静态分叉，不写成用户已经遭遇的事故。B测试中缺历史阻断用的是连续冲刺夹具，不能冒称NPC专门实机通过。[B：Z17]

**新版已确认差异**：按钮和快捷键走同一正常模式命令，恢复最后明确选择的名字/类型，首次用“名字”，详见第8节。这保留两个模式，不将其强制压成bool，也不复制旧独立热键状态。[R04]

## 3. 对象、文字与生命值

### 3.1 同一集合上的实际分支

优先取强类型`TerrariaMainCompat.Npcs`的`NPC[]`，失败才反射`Main.npc`。每个槽先要求对象active且锚点可读，`hide=true`跳过；然后按下面顺序，命中并添加后`continue`，一个槽最多加入一个标签。[C：Z08、Z12]

| 顺序 | 命中条件 | 结果/边界 |
| --- | --- | --- |
| 1 NPC | `townNPC || type为SkeletonMerchant`，且NPC模式非Off | 加一份NPC名称；不是所有friendly对象。骷髅商人类型延迟解析，失败用453 |
| 2 动物 | `CountsAsACritter || catchItem>0 || NPCID.Sets.CountsAsCritter[type]`，且动物开 | 加一份动物名称；不额外要求friendly或life>0，不等于只靠虫网可捕捉 |
| 3 敌怪 | 敌怪开，且非上述NPC资格、非friendly、非Critter、life>0、lifeMax>5、非TargetDummy | 再做分段/去重；练习假人类型解析失败用488 |

源码旁“mutually exclusive”注释比实际条件强：若对象同时是townNPC和Critter，NPC开时按NPC显示；NPC关而动物开时可落动物分支。三个开关全开时不会为同一槽叠三份；但不能把资格本身称为与开关无关的互斥分区。当前 .8 是否存在某种重叠实例不能仅凭此条件推断。[C：Z08]

代表边界只用于解释分支，不枚举全部ID。下列原版例子来自**本地1.4.5.7源码**（P01），不是 .8 支持证明；SkeletonMerchant/TargetDummy还有Legacy专项断言（Z17）。

| 例子 | 已核对事实与意义 |
| --- | --- |
| 城镇猫（637） | .7定义设townNPC=true、friendly=true；“动物外观”不使它绕过优先NPC分支；没有由此证明它是Critter重叠实例 |
| 骷髅商人（453） | .7该默认分支设friendly，Legacy专门纳NPC资格；旧漏显修正有记录H02；不是将所有友好者收入NPC |
| 普通兔（46）与金兔（443） | .7分别catchItem=2019/2890、lifeMax=5，满足动物条件；金兔在旧固定金色列表中。腐化兔（47）另有lifeMax=70等定义，不能仅按名字含“兔”归动物，仍须按实际Critter/友好标志判断 |
| 受缚哥布林（105） | .7该分支设friendly；friendly本身只足以排除敌怪，不能自动获得NPC标签。是否另有town/Critter资格须核对完整状态，不从这一段猜全部 |
| 练习假人（488） | Legacy敌怪资格有显式排除，不因生命值高就显示；B断言覆盖该排除，不证明实际游戏画面 |
| 分段敌怪及变形 | 使用下节已知角色表、realLife/ai邻接和组键；类型变化会破坏标签复用，但不代表已验证每种Boss、分裂和变形 |

NPC和动物在重建时**没有life>0门**。生命资格改变会触发缓存重建，但若active/hide及资格仍满足，重建后仍可显示。旧说明把“死亡打断缓存”写成一般边界，不能解释为所有对象生命降到0就必然隐藏。[C：Z08；D：D03]

### 3.2 名字、类型、本地化与回退

| 功能/模式 | 实际名称来源 | 不是哪种能力 |
| --- | --- | --- |
| NPC“名字” | 选择首个非空：GivenName→GivenOrTypeName→FullName→TypeName→name→`Lang.GetNPCNameValue(type)`→数字type | 一般为个人名字；不是固定“个人名＋职业”模板，不提供改名操作 |
| NPC“类型” | `ResolveTypeName(type)`，经Lang取类型译名，失败用数字type | 不直接读对象动态TypeName，也不显示个人名 |
| 敌怪/动物 | 对象TypeName优先；无值且netID非0时，返回对应Lang名或netID数字；仅netID缺失/为0时按type查询或回type数字 | 与NPC Type模式的输入规则不完全相同；不会在netID查询失败后再退type；名称取被选标签对象，不随生命来源自动改成主体名称 |

这些是首个非空值的**选择顺序**；`FirstNonEmpty(params ...)`的实参先求值，不能写成“GivenName有值便停止所有其它读取”。个人名、原版动态类型名及本地化按这些入口分别取得；未发现自定义前后缀、改名或额外名称请求。P01的 .7 `GivenName/GivenOrTypeName/FullName/TypeName` getter是读取/格式化路径，没有发包；不能把这一旧原版证据外推为 .8全部同步语义。[C：Z09；P01]

空白文本在renderer不绘制；缺失类型查询回数字ID，有一次缺失标志/30秒节流日志，普通玩家不会看到完整诊断原因。未发现长名截断、自动换行、字符清洗或缩小适配；中文/特殊字符交公共字体及原版文字绘制，未知字体覆盖保持未知。通常是NPC/动物一行、敌怪两行；若原文本含换行或颜色代码，当前原样传递，不能承诺任意输入仍严格一/两行。[C：Z09、Z10、Z14]

### 3.3 敌怪生命主体、分段与两行

敌怪开时先遍历全部active NPC，建立含whoAmI、realLife、ai[0..3]的分段信息。组键优先`realLife>=0`，否则whoAmI，再否则索引；已知分段表标出Head/Body/Tail，Body不画，Head/Tail可进入后续资格。表覆盖吞噬怪、蠕虫/世界吞噬者、骨蛇、飞龙、毁灭者、血蛭、血鳗、千足蜈蚣、拜月教邪教徒龙等旧类型；未知类型用`groupSize<3 || neighborCount<2`择端点，然后按组键去重。[C：Z11]

**每个GroupKey最多一份，取索引顺序第一个合格对象；并非所有多段Boss都保证“头上一份”。** 不同组键仍可多份；已知Body即使分组不完整也会被排除。`realLife`/邻接规则正确覆盖哪些实际Boss仍需后续 .8 与实机验证，不把测试辅助函数推广成全部Boss合同。

生命源选择有效范围内的GroupKey，退whoAmI，再退当前index；所选槽active且生命可读时取其life/lifeMax，失败用当前对象快照。**没有遍历相加生命**。名字来自标签对象的类型规则，位置也跟该对象，不是把标签统一搬到生命源位置。[C：Z08、Z11]

正常两行：第一行名称，第二行`当前/最大`，无空格或其它前后缀，例如`238/500`。数字用InvariantCulture整数字符串，负值夹0；lifeMax<=0返回空血量，renderer退一行。没有缩写、百分比、独立血条或血量关闭选项；生命变化不等待12Tick到期，而是在下一次实际绘制时复核，变化使整份标签重建。未绘制期间不会额外刷新此服务。[C：Z08、Z10；B：Z17]

## 4. 样式、配置面板与保存

### 4.1 真实默认、范围与共享关系

| 项目 | 敌怪 | 动物 | NPC | 可调/单位及来源 |
| --- | --- | --- | --- | --- |
| 首次状态 | 关闭 | 关闭 | Off（关闭） | 三项独立，AppSettings DataMember；Z03、B Z17 |
| 名称颜色 | `#CD5C5C` | `#5DADEC`；金色例外见下 | `#90EE90` | 各自RGB6位；Z03、Z05、Z08 |
| 名称字号 | 0.70 | 0.70 | 0.70 | 字体缩放倍数，非pt；范围0.50–1.80，缩小/加大步0.10，夹限并round2；Z05 |
| 血量颜色 | 与本项名称相同 | 不适用 | 不适用 | 不独立配置；Z08 |
| 血量字号 | 名称−0.13，再round2并夹0.50–1.80；当前默认0.57 | 不适用 | 不适用 | 无独立控件；按名称UI范围可达0.50–1.67；Z05、Z08 |
| H/S/L | 三项各自当前RGB换算的值 | 同左 | 同左 | H 0–360度、S/L 0–100%，整数滑动值；没有逐级加减按钮，鼠标映射可能跳值；Z05、Z06 |
| HTML色码 | 各自6位RGB | 同左 | 同左 | 只接收十六进制字符/数字区；无Alpha输入；Z06 |
| 透明度/字体/阴影/背景/间距 | 无本项独立设置 | 同左 | 同左 | RGB解析使用alpha255；公共字体/阴影由绘制决定；不把全局主题设置当本项控件；Z05、Z10、Z14 |

金色动物使用固定`#FFD700`（255,215,0,255），覆盖普通动物颜色，字号仍用动物项；判据是固定ID集合`442,443,444,445,446,447,448,539,592,593,601,605,613,627`，不是读名字或rarity。不承诺后增金色类型已覆盖。“回归默认”只恢复当前功能颜色和字号，不改开关/模式，不影响其它两项。[C：Z05、Z08]

旧《敌怪显名》说明将0.80/0.67称为默认；当前代码和通用世界字号默认是0.70/0.57。历史0.80是可用样式样本，不能从旧说明倒填当前默认。不同阶段血量字号也经历−0.10、×0.75、−0.13，见历史表。[C：Z03、Z08；D：D01、H03–H06]

### 4.2 配置完整流程

1. 单击对应行“配置”色块，打开唯一该项样式面板，标题为“功能名 配置”。面板含“回归默认”、H/S/L滑条、色块预览、HTML框、“缩小字号/加大字号”，没有整体应用/取消按钮，也未发现关闭X。[Z02、Z06]
2. 拖H/S/L时只改变滑条与面板色块预览，**世界文字仍用原设置**；松手排一个命令，执行时更新内存RGB并`SaveAll`。不是每帧写配置，也不是拖动即时改世界文字。[Z06]
3. 单击HTML框，以当前色码进入草稿，首个字符替换原值；输入满6位即提交并保存，无须Enter，Enter也可触发提交。Esc只丢弃未提交草稿/清焦，不能撤回已满6位提交的颜色。字号按钮/默认按钮则立即更新内存并请求保存。[Z06]
4. 再次点击同项配置关闭面板；点另一项切换面板并清HEX焦点。未发现点外自动关闭或Esc关闭整个面板。普通关闭不回滚此前提交；不是带事务的取消。[Z06]
5. 切页仅换page/滚动并保存页面设置，相关路由不清样式featureId或HEX草稿；面板只在信息页且原配置行可见时绘制，返回可见行时仍可出现。不能用快捷键窗的切页关闭机制推断样式窗。[Z06]
6. 关闭F5会ResetInteractionState，清滑块active/pending和HEX草稿，样式featureId仍保留；重开原页/原行可见时可继续显示该面板。失焦且绘制收到回调、`AllowsInputProcessing=false`时也清未完成输入；HEX自身的失焦判断只暂停。无Draw时何时清理未实测，不承诺即时。[Z06]

样式面板确有自己的阻挡/命中，B测试覆盖下层hover及“回归默认”仍可点；**世界标签本身没有命中或输入捕获**，这两种UI不能混为一谈。[B：Z17；C：Z10]

### 4.3 内存生效与存盘不是同一结果

旧配置默认根由源码定位到系统“我的文档”下`My Games/Terraria/JueMing-Z/config`，可由旧初始化参数调整；这不是R的`JueMingRData`。这里只读取定位代码，未打开实际用户目录。[Z16]

开关、模式、HSL提交、HEX满6位、字号及默认命令均先修改内存，再调用`ConfigService.SaveAll`，入口不检查返回汇总，失败不回滚内存新值。保存层会记录失败；部分命令的诊断仍记`Succeeded`，未发现这些控件把保存失败明确告诉玩家的路径。因此只能说“新内存值生效、已尝试保存”，不能说“已可靠保存”。[C：Z06、Z16]

`SaveAll`同步尝试`appsettings.json`、`features.json`、`hotkeys.json`、`unified-hotkeys.json`，并在条件允许时同步feature设置。并非只保存本项；临时文件、flush和替换/重试由旧配置层执行。DataMember使三项开关/模式/颜色/字号可持久化，但跨重启保留以实际写入成功为前提；保存失败时下一次启动可能仍读旧值。旧热键历史也是独立持久字段，不是UI瞬时模式自动镜像。没有读本机真实设置来冒充默认或证明保存。[C：Z03、Z04、Z16]

## 5. 世界绘制、生命周期、多人和成本

### 5.1 位置、缩放、层级与相邻UI

标签锚点取对象有效Hitbox顶部中点；不可用时退`position.X+max(width,0)/2, position.Y`。不使用gfxOffY或贴图头顶高度。先算世界位置减screenPosition，再由所在Game scale图层使用宿主世界缩放；标签没有单独UI缩放补偿。它不同于F5的UI坐标和悬挂笔记的屏幕坐标。[C：Z08、Z10、Z13]

单行按测量宽度居中，顶部Y为锚点减估计文字高再减1。敌怪两行各自居中，总高为`max(名称高, 行进+血量高)`，行进为`max(6,15×max(两个字号))`；当前0.70/0.57时行进10.5，旧0.80/0.67样本为12。这是两行起点间隔，不是额外空白行距；没有可调间距控件。[C：Z10]

| 边界 | 旧版实际机制 |
| --- | --- |
| 距离 | 以本地玩家Center为距离锚，NPC1800、动物1200、敌怪1400世界像素；这三项调用不启用relaxedDistance，非全世界显示 |
| 屏边 | 用未经过世界矩阵的screen锚点在屏幕矩形外扩80裁切，仅测锚点，未测完整文字包围框；不移回屏内、不远近淡出 |
| 标签重叠 | 无自动避让、排序堆叠或点击展开；长字可能出屏，标签可以互相覆盖 |
| 遮挡/隐身/光照 | 有active/hide条件，但没有墙体射线、NPC alpha、光照采样或额外隐身识别；不能概括为保证隔墙隐藏或任何情形无条件透视 |
| 图层 | 优先插在Map/Minimap之前，找不到依次尝试Resource Bars、Inventory、MouseText；失败仍有原较晚overlay回退且避免双绘。正常路径早于主要UI/F5/Notes，但回退不能保证相同遮挡 |
| 原版UI | 没有按背包/容器矩形遮字，也没有“打开背包就停显名”；半透明UI可透下方字。生命/魔力、背包、容器、小地图的旧分层意图有.6历史依据，当前.8全部实际顺序未核实 |
| 聊天/F5/Notes | 标签无输入操作；F5和Notes各有自身输入/坐标。聊天没有专属anchor保证，不能承诺所有UI恒在标签之上 |
| 地图/相机/离屏/调试 | 该标签renderer无专门地图投影、离屏生成或调试视图；外层图层/资源门可能决定是否调用。全屏地图、拍照、调试、极端缩放等实际分支结果未查明，不列为已支持 |

字体由公共`UiTextRenderer`读取`FontAssets.MouseText`，快路用`ChatManager.DrawColorCodedStringWithShadow`（spread1.5），失败后可退`Utils.DrawBorderString`。没有本项独立阴影颜色/描边粗细设置或标签纹理。公共guard仍需可用SpriteBatch/原版UI资源/primitive准备，不因此能说整条链没有图形资源；renderer本身不逐标签Begin/End或新建Texture。[C：Z10、Z13、Z14]

### 5.2 缓存、身份与关闭后的实际工作

| 状态/工作 | Owner、键与频率 | 失效/退出及未覆盖风险 |
| --- | --- | --- |
| 三项标签数组 | `InformationNpcLabelService`；三项开关/模式/颜色/字号hash，通常12Tick重扫 | 每次Draw复核已有槽的active、type、whoAmI、town/friendly/hide/Critter、生命资格及位置；有变化可提前重扫。无world/player/session/ref键、正常clear/dispose入口 |
| 位置/生命 | 复用标签时仍逐个重读；生命源按索引再读active/生命，并重新拼`当前/最大`比较 | 血变化重建整份标签，不是只改一行。健康源未核type/whoAmI/ref/session；同槽复用风险仍在 |
| 新生与普通名称变化 | 新生等12Tick到期或其它原因重扫；空缓存也可在窗口内复用 | 未读名字变化不会主动使标签列表失效；不承诺同帧出现 |
| NPC个人名缓存 | `InformationNpcNameCompat`，whoAmI:type:Name，24Tick绝对到期，FIFO最多512 | 无Session/对象ref/语言键；外层重扫也可能复用旧名。tick回退时仍比较旧绝对期限，不能宣称跨世界最多24Tick旧值 |
| 类型译名缓存 | 同类，以type/netID整型键，最多1024，满后不新增 | 未见语言变化清理，可能保留旧译名；对象TypeName直读路径与此路径不同 |
| 世界文字度量 | renderer本地FIFO384，文字+格式化字号键 | 无字体/语言identity，也未见清空；底层公共UiTextRenderer有字体签名/每秒检查与清理，但外层命中可绕过重测，不能称整体已覆盖换字体 |
| 启停 | 三项全关时`DrawNpcLabels`直接返回；若其它信息世界功能仍开，广义context及其它消费者继续 | 标签/构建buffer/类型解析/名称/度量缓存保留，订阅层不卸载。全部信息世界功能关闭才在外层门早退，不建context，不扫NPC；仍有已注册回调和配置判断 |

换世界时tick回退可令外层标签重扫，但没有显式WorldKey；同槽同type/同whoAmI/同资格的新对象仍可能通过复用。这里指出具体风险机制，**没有证据证明本机曾发生串标签**。死亡/消失的active变化能使已有标签重建；本地玩家只要求active，没有dead门，所以死亡但active时仍有静态可达显示路径。[C：Z07–Z10、Z15]

### 5.3 成本形态和异常

三个功能共享主标签循环，最多缓存120个候选，按Main.npc索引先入选；距离/屏边剔除在绘制期，远处候选也占容量，不是“最近120个”。敌怪开启还先遍历全部active对象建立segment字典，对每个实体再遍历字典算组大小/邻接；存在嵌套扫描，以及每实体分段对象、int[4]、邻居List等分配。不能写成“一次公共扫描消除了重复工作”。[C：Z08、Z11]

`GetLabels`整轮在锁内，复用位置直接改缓存数组；当前实际调用在绘制路径，未发现专用后台观察worker。重建用List/HashSet缓冲并ToArray，GetLabels每次仍做样式hash、颜色规范化；每次生命复核仍可能拼字符串；度量每次仍拼缓存键。名称查找有反射及params实参/数组，测量/资源有内外两层缓存。此处只报源码形态，未测每帧次数、分配量、耗时、FPS或多人性能。[C：Z08–Z11、Z14]

集合不可读时30秒节流警告并跳过；guard资源缺失/overlay异常有10秒节流，异常被吞并更新诊断。资源类异常有公共reset，但内部吞异常不一定再经过外层清理，不能声称所有失败都清全部缓存。旧版多为日志/跳帧，没有三项独立的普通用户“不可用原因”投影证据。新版按既有R失败边界设计，不搬旧诊断系统。[C：Z07、Z08、Z14]

### 5.4 实际副作用与多人证据边界

**标签采集和renderer没有查到写NPC名称/生命、捕捉、攻击、对话或发网络请求；但整个显名Draw链不是纯只读。** 只开任意一项显名也使`DrawWorldOverlay`建立FullRecord context，在画标签前无条件调用`ImportLegacyKnownChests`、`RecordOpenChest`。[C：Z07、Z15、Z16]

这会读取用于角色/世界身份的上下文并可能加载玩家×世界行为JSON；合法当前打开箱坐标首次出现会加入记录，匹配当前世界的旧已开箱键会导入记录，再从内存旧键列表移除并`SaveAll`。行为store有临时文件/Flush/Replace；写失败记录日志而保留已加内存，调用仍可能报告added/导入数量。这里没有读箱内物品，也没有本轮读取真实用户JSON。其保留价值是提醒审查完整调用链，**不是新版显名必须迁移箱子记录**。[C：Z15、Z16]

| 拓扑 | 本轮能确认 | 不能确认 |
| --- | --- | --- |
| 单人 | 旧代码读取本机Main.npc与本地玩家上下文 | 未运行三项实际画面、特殊对象、缩放/语言/死亡场景 |
| 本机房主 | 同一客户端读路径可处理客户端当前对象；没有显名专用服务器组件 | 客户端观察与同进程主机权威不是同一结论；没有绑定场景的同步/最终生命验证 |
| 普通客机 | 读取客户端已有对象与字段，不补全全图对象 | 生成/消失、名字与血量的新鲜度依原版同步；未同步不等于世界不存在，字段可读不等于服务器最终权威 |

P01只核对 .7 名称getter和若干默认分支，未重新建立完整网络同步链；当前R锁定.8的实体字段/名字同步/分段和世界矩阵原始证据仍是后续设计输入。本批不提服务器插件、全图同步、后台网络探测、新实体协议，也不把tModLoader等其它框架当实现证据。

## 6. 相关历史修正与可复用经验

以下H为真实可访问的未跟踪旧记录，字节身份见附录。记录转述所有者反馈与本轮直接确认分开；旧本地测试声称、测试源码与实机结果不相互代替。

| 案例：当时现象 | 依据与原因/限制 | 当时修法 | 保留价值、不照搬处与后续入口 |
| --- | --- | --- | --- |
| 信息文字盖主要原版UI | H01与.6基准记录指向晚层绘制；不是对象文字本身错误 | 1.7.448插入Map/Minimap等较早锚点，失败回退，防双绘 | 由正确层级承担遮挡；不照搬.6图层保证到.8，不加遮挡矩形/避让。Z13＋未来.8层级场景 |
| 骷髅商人未归NPC显名 | H02转述用户反馈；townNPC筛选漏特殊商人 | 1.7.450显式纳入NPC并从敌怪排除 | 特殊业务资格在分类处明确，不扩大所有friendly；Z08、Z17 |
| 敌怪血量两行及视觉过大/过疏 | H03加入两行；H04转述用户指出仍大/疏，完整文字高度不合视觉需要 | 1.7.452保留主体取值并−0.10；1.7.453改×0.75与紧凑行进 | “两行”要追到实际绘制；旧比例已被替代，不复制过期数值。Z10、Z17 |
| 血量字号与行距后续调整 | H05是用户新要求固定差值；H06转述字号调大后挤 | 1.7.461改−0.13；1.7.462保留差值，0.80样本行进10→12 | 用户偏好调整与bug分开；当前默认0.70不倒填为0.80。Z03、Z08、Z10 |
| NPC缓存hidden/Critter失效补核 | H07明确本轮未改NPC生产，仅复核并补seam；结果留“以最终回复为准” | 增加相关静态测试入口，不是证明又修过运行事故 | 不把记录标题/测试存在当PASS；Z17及同槽/语言/世界候选 |
| 本轮静态发现：模式记忆分叉、保存假成功风险、嵌套扫描、缓存身份/字体缺口、箱子副作用 | 当前固定C证据Z04、Z07–Z16；未发现同一问题的完整已修复运行记录 | 本轮没有改旧版或新版运行代码 | 写成风险/新版建议与验收候选，不杜撰“Legacy已踩且修复”故事 |

定向Git历史还确认`80dafbc1f02f155570ff08aa37012c52e74909f1`（“显示血量”）修改世界文字renderer；服务后经拆分，旧记录中的`InformationOverlayService`相关方法今天部分在`InformationNpcLabelService`。引用当前行为必须用当前固定文件，不能只套旧路径。

## 7. 共同基础、独立规则与当前 R 对照

下表的 R 栏来自实际代码，不以架构图中的职责名称证明实现存在。固定 R 来源见 R01–R08。

| 职责/数据 | 真实使用者 | Legacy 实际提供方式 | 当前 R 实码 | 后续处理与理由 |
| --- | --- | --- | --- | --- |
| 原版对象集合、活动性、槽位、类型、位置 | 三项显名 | 同一 `Main.npc`，同一显名扫描；敌怪还先做分段观察 | 只有群系和物品专用观察；物品读一个交谈 NPC 不等于实体观察平台 | 最小补齐一个面向本批需求的只读观察来源；同一时点公共字段只取一次，业务分类不下沉成万能扫描器 |
| 观察有效性与新鲜度 | 三项及将来真正出现的其它消费者 | 12 Tick 名称快照加绘制时复核，没有 Session/对象引用键 | `SingleFeatureRuntime` 已有 generation、进入/退出、故障隔离；尚无实体快照或 demand 协调器 | 复用唯一生命周期；观察结果明确 Session、样本时点与不可用。是否缓存及失效粒度在设计时决定，不照抄槽位缓存 |
| 名称、生命值和位置的更新 | 名称三项；生命值仅敌怪；位置三项 | 名称主要随重扫；缓存复用时仍读对象位置/分类，敌怪生命变化会重扫 | 没有显名投影 | 位置和生命及时更新；名称/字体/样式按变化重算；不强制统一每 Tick 大快照或统一低频 |
| 世界坐标到标签与绘制层级 | 三项 | 同一世界文字 renderer，Game scale；单行/两行分别排版 | 群系为固定屏幕 UI layer；Notes 便签用屏幕 Identity，F5 用冻结 UI matrix | 最小补齐世界文字呈现职责及正确层级/缩放；不能把便签坐标直接套到世界对象 |
| 字体度量和资源借用 | F5、Notes、Items、快捷键，未来三项文字 | 旧世界文字缓存与反射兼容路径 | `UiTextMetrics` 已使用真实字形回调，保留偏移，修正受支持空格的垂直占位；`UiSurface` 不拥有游戏资产 | 复用可适用的机械度量/资源规则；世界内容和布局缓存独立，不把动态实体名字塞进 F5 的 1024 项固定文案缓存 |
| 配置面板控件 | 三项样式面板 | 共用样式编辑器，独立字段 | `F5RowLayout`、`F5ControlRenderer`、`UiSurface` 已被真实信息/物品等页面使用；没有显名颜色/字号面板 | 在现有呈现方式最小补齐必要颜色/字号控件，不建立任意表单系统；共用控件不共用偏好值 |
| 开关、模式、分类及生命主体 | 各具体功能 | bool、NPC模式、分段服务分别决定，再进共用标签缓存 | 三项无 Feature/配置/命令 | 由对应功能拥有规则和语义投影；敌怪生命归敌怪，动物资格归动物，NPC名字模式归NPC；不能揉进 Runtime 或总 Manager |
| 设置与磁盘可靠保存 | 群系、UI、物品已有真实消费者；本批将加入 | 内存先变，`SaveAll` 结果被入口忽略 | `PreferenceDocument<T>` 管内存期望/revision/有界合并；`AtomicFileDocument` 处理原件保护与机械提交；`DocumentWorker<T>` 另服务明确提交 | 复用已有机制，功能定义自己的格式和偏好。具体文件划分待设计，不增加全局超级配置；不将保存失败显示为成功 |
| F5 三个入口 | 本批三项 | 真实行与模式按钮 | `F5Layout.BuildInformation` 的前三行已经有外形，但 `Row(..., i == 12)` 只有群系有命令；前三行没有 HotkeyTarget | 接真实配置/启停/模式命令和结果，不新增重复页面，不用“看得到按钮”代表完成 |
| 公共快捷键 | 当前群系、自动堆叠/出售/丢弃；未来本批 | 旧通用按键入口直接写各配置字段，NPC记忆有分叉 | `HostHotkeys` 仅注册四项；`HotkeyAction` 接受实际命令委托，不强制bool，B测试有非开关命令 | 在冻结注册表前注册稳定动作ID和真实正常命令，同一小窗/输入/文件；设置时原版重合提醒并允许保存，内部重复拒绝，不恢复旧独立服务 |
| 退出、死亡与失败 | 三项及现有功能 | 标签没有会话清理；本地玩家 active 即可，无dead门 | Runtime已有进入/退出与局部故障；当前组合的 `ItemSessionProbe` 要求本地玩家未死亡，身份含玩家/world/socket引用和netMode | 后续设计要核对死亡显示候选与既有Session门的差异；不能直接宣称全满足，也不能另造第二个Session权威或改变物品在途回执 |

新版建议的职责关系是：**Host 在游戏线程按真实需求观察 → 各功能决定资格、名称/生命含义及开关/模式 → 世界文字呈现消费只读结果**。Settings 独占偏好，运行状态不倒写为用户关闭；Presentation 只拥有布局、编辑和资源瞬时状态。全部显名关闭时撤销本批需求；若将来其它真实消费者仍需同类观察，它们独立表达需求，不要求显名必须打开。后台仅处理不可变配置字节，不能访问活 NPC 或 XNA。

这张表不锁定最终类名、程序集、缓存策略、扫描频率或 Hook。未来捕捉、瞄准、宝箱高亮仅用于说明职责边界，不要求现在实现、预留占位或证明全部未来扩展。

### 当前 R 证据的限制

R 实际只有四项快捷键接入（R04）。较早 F5 设计仍有“键盘无业务”“物品无绑定”等阶段表述，配置设计首段、B表及部分路由仍留“#51待实机接受”措辞；它们与 #51/#52 已接受/合并及当前实码不一致。这里并列保留差异：本轮所有者明确的前序接受事实由实际 Issue/PR 再核实，具体快捷键机制查[当前快捷键合同](统一快捷键功能行为合同.md#78-r-已接受实现的使用与边界)与[实现设计](../设计/统一快捷键实现设计.md)。本文不擅改这些超出白名单的旧描述，不以它们推翻已明确接受或扩大三项接入状态。

## 8. 已确认的新版首轮目标

本节按2026-09-11所有者明确决定接受为实施基线，取代原Q1–Q5待确认状态；接受的是用户结果和本节差异，**不接受整份Legacy研究的条件、常数、缓存和回退为新版执行清单**。沿用[Legacy平替规则](../规范/Legacy平替与行为契约规则.md)、[架构](../设计/总体架构与状态所有权.md)及当前公共输入/保存边界。需求确认与实现、实机接受分别记录。

### 8.1 完整能力与默认

- 三项同批交付：敌怪名称及当前/最大生命两行，同一开关；动物名称；NPC“名字／类型／关闭”。无独立血条、百分比、血量开关、捕捉或交互。
- 城镇对象/骷髅商人、Critter/可捕捉动物及敌怪各有规则，重叠结果确定。依据当前`.8`核对字段、名称与特殊类型；旧lifeMax阈值、固定ID、索引选取和反射链不能自动继承。多段共享生命、独立生命、分裂/变形分别处理，不求和全部段，不一律折叠全部Boss。未知来源不伪造正常名字、0/0或死亡事实。
- 三项首次默认关闭，新三个快捷键默认未绑定；不改原四项状态、ID或绑定。NPC明确选名字/类型更新本项最后选择；关闭保留，按钮和快捷键走同一功能命令，再开恢复最后选择，无历史用名字；记忆随本项偏好保存，不归热键模块。
- 各自颜色：敌怪`#CD5C5C`（血量同色）、普通动物`#5DADEC`、NPC`#90EE90`；金色动物保留`#FFD700`例外，依据当前可靠元数据或有版本依据的有限映射辨认，字号仍属动物项。
- 三项名称字号默认0.70，范围0.50–1.80，按钮步长0.10，夹限且避免累计漂移；血量字号为名称−0.13，保留两位并夹0.50–1.80，默认0.57，名称可调范围内血量上限1.67。单位为缩放倍数，不是pt；不要求复制旧行高/测量公式。
- 本轮不导入Legacy显名设置、热键或模式历史，不读旧用户目录。静态高亮、其它显名、信息窗拖动、预测、地图和自动交互不纳入。

### 8.2 现有信息页与一个样式副窗

保留现有信息页顺序、主窗口尺寸/位置/滚动、群系与其它行样式。前三行接真实配置、开关/模式、选中态、可用性与原末尾小键盘；未接入行仍为样板，稳定功能身份不依赖行号。复用已有公共行控件与输入仲裁。

只建一个服务三个独立目标的颜色/字号副窗：单击打开，同项再点关闭，另项切换并结束旧草稿；有清晰关闭入口。紧凑排列目标标题、色块/六位RGB、带标签/数值/滑块的HSL、真实字号及边界按钮、当前项恢复默认和必要错误。H为0–360度，S/L为0–100%；“#”在框外。不增Alpha、字体选择、系统选色窗、颜色轮、装饰皮肤、任意表单或大示例画布。

副窗贴近入口并约束视口，按可见内容和字体度量尺寸；隐藏说明/未来错误不占空白。绘制和命中共用布局，小视口/缩放/字体替换/F5移动后仍清楚可操作。金色动物例外只用短说明。配置色块保留文字对比，不重画信息整页。

### 8.3 编辑、提交、退出与输入归属

- HSL拖动只预览本窗草稿/色块或小样例，世界保持最近已提交偏好；有前台有效捕获时可拖出滑条夹限，**真实释放才提交一次**，先内存生效再异步合并保存。失焦、切页、关窗、换目标、退出世界、捕获/几何失效取消未提交手势，消费尾部，不以合成松手确认、不按帧写盘。
- HEX与HSL是同一草稿的表现。进入HEX编辑支持替换；六位有效RGB自动提交，Enter只提交完整有效值，不足/无效不改变颜色。支持粘贴时先整体验证，不截断或逐字误提交，不记录剪贴板内容。打开/换焦点/显示整数HSL不量化重写RGB；无唯一色相时维持合理编辑连续性，不新增持久字段。
- Esc有文本草稿/拖动时优先取消该编辑，无编辑时关闭副窗；同一个Esc不继续关闭F5/打开原版暂停。完整色码已提交后的Esc不回滚该颜色。字号和恢复默认是单击确认操作；默认仅重置当前颜色/字号，不改模式、开关、绑定或另两项。
- 样式与快捷键窗互斥拥有活动编辑，正常切换取消旧未提交捕获，已提交保存继续完成。重新打开不复活旧草稿或半次手势；不新增焦点检测/永久PopupManager。
- 可见副窗及提示阻挡覆盖区下层点击/悬停/滚轮；入口切换由同一UI owner裁定。捕获期间主窗拖动、滚动条及游戏操作不能抢手势。HEX使用现有文本输入上下文，不重复轮询；保留Notes真实IME/选择复制及已有快捷键保护。

### 8.4 观察、绘制与保存边界

Host在游戏线程按本批真实需求提供只读事实，各功能决定资格与内容，世界文字呈现消费结果。共享必要字段，只有动物/NPC时不做敌怪分组；有界分组不逐对象再扫全列表。位置/血量及时、稳定名字/样式/度量不无理由重建；语言/字体/身份变化可失效。可见性不丢弃屏外必要生命来源，不按数组前项挤掉屏内对象，不建设全实体平台/全图雷达/避让。

三项全关停止本批扫描、标签构建和绘制。复用唯一Runtime/Session，临时不可用不倒写用户开关；单个对象/名称失败隔离，共享能力失败有界反馈。**本地玩家死亡但仍active、世界与对象有效时继续显示**；在正确职责分开观察有效与允许游戏操作，不假报存活、不另建Session、不放宽物品操作/在途保护，局部改变须有相邻反例。

世界标签按当前`.8`真实世界坐标、相机/游戏缩放与图层绘制，位于主要原版UI及F5/Notes之下；不套便签UI坐标、不靠多个晚层回退或重复绘制。用实际字体度量两行、借用资源并保护批次/矩阵/裁切，不逐标签建资源/Begin-End，不注册命中。地图/相机/离屏不扩展新投影，明确实际入口的停显边界。单人/房主/客机仅表达客户端已知事实，不承诺服务端全知或零延迟，不增加实体请求/协议。

除本工具偏好、缓存和有限故障记录外，显示链不写游戏状态，不记录/导入箱子或创建角色地图关系文件。自然偏好放`JueMingRData/config`下独立文件/分组，复用`PreferenceDocument`与原子提交保护，格式/文件归属在实现设计明确；不SaveAll、不在Draw/输入同步写盘、不塞Notes/hotkeys。

普通偏好确认操作后先内存生效、异步合并保存，正常低干扰；损坏/未知版本或字段/冲突/占用/提交未知保护原件，当前副窗及既有有限反馈保留必要失败后果，关窗不丢重要原因。修订/目标与异步完成关联，旧完成不覆盖新值或串到另一目标。快捷键仍可靠保存后替换有效绑定，七个动作共用冲突检查/紧凑小窗和原版设置时重合提醒，不改已有语义、不持续监控原版键位。

### 8.5 实施与接受的边界

所有者已授权另立实现任务：有限设计、先失败反例再实现、相关Debug/Release/Host/存储/图形验证、注释文档、独立审查、固定clean候选完整包与同工作区顺序重复构建、安全安装并保留测试。需求PR可正常收口；实现PR保持Draft/Issue OPEN，待所有者实测接受。新副窗及世界标签要实际查看代表原资源预览；明确设备创建前环境不可用者可按本次许可待补，实际错误不能豁免。

安装仅在已确认目标/EXE/旧载荷和恢复归属匹配、游戏与服务器退出后进行；不强退、不启动游戏、不改变用户数据或绑定、不导入旧数据。具体安装路径、动态候选、包/回执、验证结果留任务Issue。本节不是已安装/已通过声明，旧静态证据不升级为运行证据。

## 9. 后续验收候选

这些是未来设计/实施的验证入口，**全部未在本轮执行**；通过何种自动、实机、多人或所有者确认分别记录，不把下表变成让用户检查JSON的任务。

| 场景 | 应核对的用户结果或安全边界 |
| --- | --- |
| 三项分别开启、全部开启、全部关闭 | 各自真实显示，模式/分类重叠有确定规则；不串样式；全关停止本批观察与绘制 |
| NPC按钮与快捷键交替：名字→关→键开、类型→关→键开、首次直接键开 | 恢复规则一致，提示/模式与实际文字相符；不被旧独立历史值带偏 |
| 城镇角色、骷髅商人、可捕捉城镇对象、普通/金色动物、练习假人、特殊友方对象 | 分别解释为什么显示或不显示；不能按“NPC”统收或按“动物”词义猜测 |
| 普通敌怪、共享生命多段、各段独立生命、变形与分裂 | 名称、主体、当前/最大生命、显示份数正确；不加总错误、不保留消失目标 |
| NPC名字/类型变化、语言切换、长中文/空名/特殊字符 | 内容及时更新，回退可解释；长名与屏边处理明确，字体替换不留旧度量 |
| 调色、字号边界、恢复默认、关面板/切页/失焦 | 生效、退出与保存语义一致；没有不可撤回的假“取消”或假保存；另外两项不变 |
| 游戏缩放、UI缩放、分辨率变化、屏边、远近、多对象重叠 | 世界锚点正确；名称/血量相对排版合理；旧版无避让不自动成为新版必需避让 |
| 原版背包/容器/生命魔力/小地图/聊天、F5、悬挂笔记 | 层级有证据，标签不接管点击/悬停；F5已有输入保护不被破坏 |
| 死亡重生、退出换世界/玩家、断线重连、同槽同型新对象 | 不复用旧身份标签；死亡是否保留显示按确认结果实现，现有物品回执不受影响 |
| 保存失败/外部冲突/未知格式/快速连续修改/退出时在途写入 | 本次有效与已保存分清，原件/恢复材料保留，旧完成不覆盖新偏好 |
| 单人、本机房主、普通客机分别核对 | 仅表示本地客户端可观察字段；不同步不等于全世界不存在，无新服务器组件/请求 |
| 足量实体、三个开关同时开、无显名需求 | 观察共享有真实调用依据；名称/度量不无理由逐帧重算；按需要另做测量，不由静态宣称FPS改善 |

## 10. 来源、覆盖与缺口

### 固定 R 源码

以下全部绑定 R `bf56e93627e5476e4f1a8f3a1628f107d4e1ea5c`。

| 编号 | 入口及证明范围 |
| --- | --- |
| R01 | [F5Layout.cs:203–227,255–278](https://github.com/Kuroneko2020/JueMingR/blob/bf56e93627e5476e4f1a8f3a1628f107d4e1ea5c/src/JueMingR.TerrariaHost/F5/F5Layout.cs#L203-L278)：三行外形、仅群系命令/HotkeyTarget及固定文案缓存 |
| R02 | [F5RowLayout.cs:7–78](https://github.com/Kuroneko2020/JueMingR/blob/bf56e93627e5476e4f1a8f3a1628f107d4e1ea5c/src/JueMingR.TerrariaHost/F5/F5RowLayout.cs#L7-L78)、[UiSurface.cs:9–51](https://github.com/Kuroneko2020/JueMingR/blob/bf56e93627e5476e4f1a8f3a1628f107d4e1ea5c/src/JueMingR.TerrariaHost/F5/UiSurface.cs#L9-L51)：行布局、真实字形度量、资源借用 |
| R03 | [HostPreferences.cs:19–89](https://github.com/Kuroneko2020/JueMingR/blob/bf56e93627e5476e4f1a8f3a1628f107d4e1ea5c/src/JueMingR.TerrariaHost/Settings/HostPreferences.cs#L19-L89)、[PreferenceDocument.cs:17–143](https://github.com/Kuroneko2020/JueMingR/blob/bf56e93627e5476e4f1a8f3a1628f107d4e1ea5c/src/JueMingR.Platform/Settings/PreferenceDocument.cs#L17-L143)：现有偏好内存/保存/失败与退出 |
| R04 | [HostHotkeys.cs:16–32](https://github.com/Kuroneko2020/JueMingR/blob/bf56e93627e5476e4f1a8f3a1628f107d4e1ea5c/src/JueMingR.TerrariaHost/Hotkeys/HostHotkeys.cs#L16-L32)、[HotkeyRegistry.cs:7–39](https://github.com/Kuroneko2020/JueMingR/blob/bf56e93627e5476e4f1a8f3a1628f107d4e1ea5c/src/JueMingR.Platform/Hotkeys/HotkeyRegistry.cs#L7-L39)：实际四注册与普通命令委托 |
| R05 | [SingleFeatureRuntime.cs:8–103](https://github.com/Kuroneko2020/JueMingR/blob/bf56e93627e5476e4f1a8f3a1628f107d4e1ea5c/src/JueMingR.Platform/Runtime/SingleFeatureRuntime.cs#L8-L103)、[ItemHostObservation.cs:146–166](https://github.com/Kuroneko2020/JueMingR/blob/bf56e93627e5476e4f1a8f3a1628f107d4e1ea5c/src/JueMingR.TerrariaHost/Items/ItemHostObservation.cs#L146-L166)：已有生命周期、代次与实际死亡门 |
| R06 | [Phase0SLoadChainHost.cs:616–755,828–832](https://github.com/Kuroneko2020/JueMingR/blob/bf56e93627e5476e4f1a8f3a1628f107d4e1ea5c/src/JueMingR.TerrariaHost/Phase0SLoadChainHost.cs#L616-L832)、[NotesPresentation.cs:150–155](https://github.com/Kuroneko2020/JueMingR/blob/bf56e93627e5476e4f1a8f3a1628f107d4e1ea5c/src/JueMingR.TerrariaHost/Notes/NotesPresentation.cs#L150-L155)：群系UI层/固定位置、实际组合与便签屏幕矩阵 |
| R07 | [AtomicFileDocument.cs:50–205](https://github.com/Kuroneko2020/JueMingR/blob/bf56e93627e5476e4f1a8f3a1628f107d4e1ea5c/src/JueMingR.Infrastructure/Storage/AtomicFileDocument.cs#L50-L205)、[HostItems.cs:42–79](https://github.com/Kuroneko2020/JueMingR/blob/bf56e93627e5476e4f1a8f3a1628f107d4e1ea5c/src/JueMingR.TerrariaHost/Items/HostItems.cs#L42-L79)：真实机械保存及物品偏好消费者，不是实体平台 |
| R08 | [HotkeyCoreChecks.cs:26–64,108–135](https://github.com/Kuroneko2020/JueMingR/blob/bf56e93627e5476e4f1a8f3a1628f107d4e1ea5c/tests/JueMingR.ArchitectureTests/Hotkeys/HotkeyCoreChecks.cs#L26-L135)：非bool命令、分派与失败保留旧绑定的测试源码；本轮未运行 |

### 固定 Legacy 源码与测试

以下路径相对于 `Kuroneko2020/JueMingZ`，全部绑定上列固定SHA；行段与符号用于回查，不将代码全文搬入R。

| 编号 | 入口与范围 |
| --- | --- |
| Z01 | [src/JueMingZ/Features/Catalog/InformationFeatureRegistrar.cs:11–24](https://github.com/Kuroneko2020/JueMingZ/blob/6ac6356c0c564f43284590c168855e96c50e13f7/src/JueMingZ/Features/Catalog/InformationFeatureRegistrar.cs#L11-L24)；同文件108–116注册元数据。 |
| Z02 | [src/JueMingZ/UI/Legacy/LegacyMainWindow.Information.cs:23–118](https://github.com/Kuroneko2020/JueMingZ/blob/6ac6356c0c564f43284590c168855e96c50e13f7/src/JueMingZ/UI/Legacy/LegacyMainWindow.Information.cs#L23-L118)；[src/JueMingZ/UI/Legacy/LegacyMainWindow.Rows.cs:38–171](https://github.com/Kuroneko2020/JueMingZ/blob/6ac6356c0c564f43284590c168855e96c50e13f7/src/JueMingZ/UI/Legacy/LegacyMainWindow.Rows.cs#L38-L171)：真实行与顺序。 |
| Z03 | [src/JueMingZ/Config/AppSettings.cs:879–947](https://github.com/Kuroneko2020/JueMingZ/blob/6ac6356c0c564f43284590c168855e96c50e13f7/src/JueMingZ/Config/AppSettings.cs#L879-L947)：三项状态、颜色、字号默认与DataMember。 |
| Z04 | [src/JueMingZ/Runtime/RuntimeAutomationDispatcher.cs:268–285](https://github.com/Kuroneko2020/JueMingZ/blob/6ac6356c0c564f43284590c168855e96c50e13f7/src/JueMingZ/Runtime/RuntimeAutomationDispatcher.cs#L268-L285)；[src/JueMingZ/Input/FeatureToggleHotkeyService.cs:100–174](https://github.com/Kuroneko2020/JueMingZ/blob/6ac6356c0c564f43284590c168855e96c50e13f7/src/JueMingZ/Input/FeatureToggleHotkeyService.cs#L100-L174)；同文件599–611、764–810模式切换与历史；[src/JueMingZ/Config/HotkeySettings.cs:22–34](https://github.com/Kuroneko2020/JueMingZ/blob/6ac6356c0c564f43284590c168855e96c50e13f7/src/JueMingZ/Config/HotkeySettings.cs#L22-L34)；[src/JueMingZ/Input/LegacyUiActionService.Information.cs:127–175](https://github.com/Kuroneko2020/JueMingZ/blob/6ac6356c0c564f43284590c168855e96c50e13f7/src/JueMingZ/Input/LegacyUiActionService.Information.cs#L127-L175)；[src/JueMingZ/UI/Legacy/LegacyMainWindow.FeatureToggleHotkeys.cs:196–269](https://github.com/Kuroneko2020/JueMingZ/blob/6ac6356c0c564f43284590c168855e96c50e13f7/src/JueMingZ/UI/Legacy/LegacyMainWindow.FeatureToggleHotkeys.cs#L196-L269)；[src/JueMingZ/Input/LegacyUiActionService.FeatureToggleHotkeys.cs:28–52](https://github.com/Kuroneko2020/JueMingZ/blob/6ac6356c0c564f43284590c168855e96c50e13f7/src/JueMingZ/Input/LegacyUiActionService.FeatureToggleHotkeys.cs#L28-L52)。 |
| Z05 | [src/JueMingZ/Automation/Information/InformationStyleHelper.cs:9–180](https://github.com/Kuroneko2020/JueMingZ/blob/6ac6356c0c564f43284590c168855e96c50e13f7/src/JueMingZ/Automation/Information/InformationStyleHelper.cs#L9-L180)；[src/JueMingZ/Automation/Information/InformationColorHelper.cs:34–141](https://github.com/Kuroneko2020/JueMingZ/blob/6ac6356c0c564f43284590c168855e96c50e13f7/src/JueMingZ/Automation/Information/InformationColorHelper.cs#L34-L141)：独立样式、范围、默认、RGB与金色。 |
| Z06 | [src/JueMingZ/UI/Legacy/LegacyMainWindow.Information.cs:307–416](https://github.com/Kuroneko2020/JueMingZ/blob/6ac6356c0c564f43284590c168855e96c50e13f7/src/JueMingZ/UI/Legacy/LegacyMainWindow.Information.cs#L307-L416)；[src/JueMingZ/Input/LegacyUiActionService.Information.cs:423–522](https://github.com/Kuroneko2020/JueMingZ/blob/6ac6356c0c564f43284590c168855e96c50e13f7/src/JueMingZ/Input/LegacyUiActionService.Information.cs#L423-L522)；[src/JueMingZ/UI/Legacy/LegacyHexColorInput.cs:31–141](https://github.com/Kuroneko2020/JueMingZ/blob/6ac6356c0c564f43284590c168855e96c50e13f7/src/JueMingZ/UI/Legacy/LegacyHexColorInput.cs#L31-L141)；[src/JueMingZ/UI/Legacy/LegacyUiInput.Slider.cs:44–80](https://github.com/Kuroneko2020/JueMingZ/blob/6ac6356c0c564f43284590c168855e96c50e13f7/src/JueMingZ/UI/Legacy/LegacyUiInput.Slider.cs#L44-L80)；[src/JueMingZ/UI/Legacy/LegacySlider.cs:17–23](https://github.com/Kuroneko2020/JueMingZ/blob/6ac6356c0c564f43284590c168855e96c50e13f7/src/JueMingZ/UI/Legacy/LegacySlider.cs#L17-L23)；[src/JueMingZ/UI/Legacy/LegacyMainWindow.StateApi.cs:25–52](https://github.com/Kuroneko2020/JueMingZ/blob/6ac6356c0c564f43284590c168855e96c50e13f7/src/JueMingZ/UI/Legacy/LegacyMainWindow.StateApi.cs#L25-L52)。退出补链：[src/JueMingZ/UI/Legacy/LegacyMainWindow.Layers.cs:32–45](https://github.com/Kuroneko2020/JueMingZ/blob/6ac6356c0c564f43284590c168855e96c50e13f7/src/JueMingZ/UI/Legacy/LegacyMainWindow.Layers.cs#L32-L45)、[src/JueMingZ/UI/Legacy/LegacyMainUiState.Window.cs:16–87](https://github.com/Kuroneko2020/JueMingZ/blob/6ac6356c0c564f43284590c168855e96c50e13f7/src/JueMingZ/UI/Legacy/LegacyMainUiState.Window.cs#L16-L87)及173–198、[src/JueMingZ/UI/Legacy/LegacyUiInput.Window.cs:63–90](https://github.com/Kuroneko2020/JueMingZ/blob/6ac6356c0c564f43284590c168855e96c50e13f7/src/JueMingZ/UI/Legacy/LegacyUiInput.Window.cs#L63-L90)、[src/JueMingZ/Input/LegacyUiActionService.CommandRouter.cs:22–56](https://github.com/Kuroneko2020/JueMingZ/blob/6ac6356c0c564f43284590c168855e96c50e13f7/src/JueMingZ/Input/LegacyUiActionService.CommandRouter.cs#L22-L56)、[src/JueMingZ/Compat/TerrariaMainCompat.cs:176–208](https://github.com/Kuroneko2020/JueMingZ/blob/6ac6356c0c564f43284590c168855e96c50e13f7/src/JueMingZ/Compat/TerrariaMainCompat.cs#L176-L208)。 |
| Z07 | [src/JueMingZ/UI/Information/InformationWorldOverlay.cs:19–46](https://github.com/Kuroneko2020/JueMingZ/blob/6ac6356c0c564f43284590c168855e96c50e13f7/src/JueMingZ/UI/Information/InformationWorldOverlay.cs#L19-L46)；[src/JueMingZ/Automation/Information/InformationOverlayService.cs:35–89](https://github.com/Kuroneko2020/JueMingZ/blob/6ac6356c0c564f43284590c168855e96c50e13f7/src/JueMingZ/Automation/Information/InformationOverlayService.cs#L35-L89)、同文件145–168、336–339：调用/全关门/两行消费/FullRecord。 |
| Z08 | [src/JueMingZ/Automation/Information/InformationNpcLabelService.cs:13–89](https://github.com/Kuroneko2020/JueMingZ/blob/6ac6356c0c564f43284590c168855e96c50e13f7/src/JueMingZ/Automation/Information/InformationNpcLabelService.cs#L13-L89)及176–538、540–689、764–785、895–902：扫描/分类/生命/复用/锚点/模式。 |
| Z09 | [src/JueMingZ/Automation/Information/InformationNpcNameCompat.cs:11–153](https://github.com/Kuroneko2020/JueMingZ/blob/6ac6356c0c564f43284590c168855e96c50e13f7/src/JueMingZ/Automation/Information/InformationNpcNameCompat.cs#L11-L153)：名称来源、反射回退、24Tick/512/1024缓存。 |
| Z10 | [src/JueMingZ/Automation/Information/InformationWorldLabelRenderer.cs:9–175](https://github.com/Kuroneko2020/JueMingZ/blob/6ac6356c0c564f43284590c168855e96c50e13f7/src/JueMingZ/Automation/Information/InformationWorldLabelRenderer.cs#L9-L175)：距离、单/两行、384度量缓存和绘制。 |
| Z11 | [src/JueMingZ/Automation/Information/InformationNpcSegmentService.cs:12–176](https://github.com/Kuroneko2020/JueMingZ/blob/6ac6356c0c564f43284590c168855e96c50e13f7/src/JueMingZ/Automation/Information/InformationNpcSegmentService.cs#L12-L176)及179–360：角色表、组键、邻接/组大小与构造成本。 |
| Z12 | [src/JueMingZ/Compat/TerrariaNpcReadCompat.cs:145–158](https://github.com/Kuroneko2020/JueMingZ/blob/6ac6356c0c564f43284590c168855e96c50e13f7/src/JueMingZ/Compat/TerrariaNpcReadCompat.cs#L145-L158)及Z08的565–684：强类型/反射快照与Critter。 |
| Z13 | [src/JueMingZ/Hooks/InterfaceLayerHookCallbacks.cs:183–258](https://github.com/Kuroneko2020/JueMingZ/blob/6ac6356c0c564f43284590c168855e96c50e13f7/src/JueMingZ/Hooks/InterfaceLayerHookCallbacks.cs#L183-L258)及29–33、309–313、799–818；[src/JueMingZ/UI/UiDrawTransform.cs:10–95](https://github.com/Kuroneko2020/JueMingZ/blob/6ac6356c0c564f43284590c168855e96c50e13f7/src/JueMingZ/UI/UiDrawTransform.cs#L10-L95)：实际层级与坐标域。 |
| Z14 | [src/JueMingZ/UI/UiDrawLifecycleGuard.cs:47–103](https://github.com/Kuroneko2020/JueMingZ/blob/6ac6356c0c564f43284590c168855e96c50e13f7/src/JueMingZ/UI/UiDrawLifecycleGuard.cs#L47-L103)；[src/JueMingZ/UI/UiTextRenderer.cs:547–619](https://github.com/Kuroneko2020/JueMingZ/blob/6ac6356c0c564f43284590c168855e96c50e13f7/src/JueMingZ/UI/UiTextRenderer.cs#L547-L619)及698–780、818–838：资源门、字体绘制/公共缓存及失败。 |
| Z15 | [src/JueMingZ/Automation/Information/InformationWorldContextProvider.cs:58–155](https://github.com/Kuroneko2020/JueMingZ/blob/6ac6356c0c564f43284590c168855e96c50e13f7/src/JueMingZ/Automation/Information/InformationWorldContextProvider.cs#L58-L155)及263–335：本地玩家门、距离上下文、FullRecord身份。 |
| Z16 | [src/JueMingZ/Automation/Information/InformationChestRecordService.cs:14–80](https://github.com/Kuroneko2020/JueMingZ/blob/6ac6356c0c564f43284590c168855e96c50e13f7/src/JueMingZ/Automation/Information/InformationChestRecordService.cs#L14-L80)及135–193；[src/JueMingZ/Records/PlayerWorldBehaviorStore.cs:126–205](https://github.com/Kuroneko2020/JueMingZ/blob/6ac6356c0c564f43284590c168855e96c50e13f7/src/JueMingZ/Records/PlayerWorldBehaviorStore.cs#L126-L205)及399–464；[src/JueMingZ/Config/ConfigService.cs:20–34](https://github.com/Kuroneko2020/JueMingZ/blob/6ac6356c0c564f43284590c168855e96c50e13f7/src/JueMingZ/Config/ConfigService.cs#L20-L34)及239–282、615–669、720–755：附带记录与同步保存/失败。 |
| Z17 | [tests/JueMingZ.Tests/Program.AppSettingsDefaultTests.cs:85–87](https://github.com/Kuroneko2020/JueMingZ/blob/6ac6356c0c564f43284590c168855e96c50e13f7/tests/JueMingZ.Tests/Program.AppSettingsDefaultTests.cs#L85-L87)；[tests/JueMingZ.Tests/Program.InformationOverlayUiTests.cs:34–139](https://github.com/Kuroneko2020/JueMingZ/blob/6ac6356c0c564f43284590c168855e96c50e13f7/tests/JueMingZ.Tests/Program.InformationOverlayUiTests.cs#L34-L139)；[tests/JueMingZ.Tests/Program.LegacyUiOverlayCoordinatorTests.cs:408–468](https://github.com/Kuroneko2020/JueMingZ/blob/6ac6356c0c564f43284590c168855e96c50e13f7/tests/JueMingZ.Tests/Program.LegacyUiOverlayCoordinatorTests.cs#L408-L468)；[tests/JueMingZ.Tests/Program.FeatureToggleHotkeyTests.cs:560–581](https://github.com/Kuroneko2020/JueMingZ/blob/6ac6356c0c564f43284590c168855e96c50e13f7/tests/JueMingZ.Tests/Program.FeatureToggleHotkeyTests.cs#L560-L581)；[tests/JueMingZ.Tests/Program.InformationChestLabelTests.cs:1102–1229](https://github.com/Kuroneko2020/JueMingZ/blob/6ac6356c0c564f43284590c168855e96c50e13f7/tests/JueMingZ.Tests/Program.InformationChestLabelTests.cs#L1102-L1229)：测试源码，未运行。 |

### 未跟踪的 Legacy 说明与历史记录

下列真实路径均相对于已核验的 `../JueMingZ` 根，文件未被该仓库 Git 跟踪；**源码 SHA 不绑定这些文档**。标题、路径和本轮 SHA-256 用于识别本次读取内容。未复制原件、日志或截图进 R，也不构造不存在的 GitHub blob 链接。

| 编号 | 路径、标题与内容定位 | SHA-256 |
| --- | --- | --- |
| D01 | `文档/功能介绍/更多信息页/敌怪显名.md`；标题“敌怪显名”，更新标记 `26-06-09-1349`；17、44行保留0.80/0.67旧样本，与当前默认分开 | `CEA821B617C94718B25B9E2EF9E15E3A44BCC16668D342F21829EB7850D9473A` |
| D02 | `文档/功能介绍/更多信息页/动物显名.md`；标题“动物显名”，更新标记 `26-06-10-1309`；入口、只显示名字与样式说明 | `0D51FDEC3ECDF0193C6AB90F24062D73AF68FB454194A6E898E41A8306F59339` |
| D03 | `文档/功能介绍/更多信息页/NPC显名.md`；标题“NPC显名”，更新标记 `26-06-14-0008`；7行“等普通友好NPC”比实际分支宽，26行死亡缓存说明不等于生命正数门 | `2400F608A2CA695044DB560C10F49FB1853E5E3AB02D981CB3AE3299C8787B61` |
| H01a | `文档/更新记录/0538-信息显示图层下沉收口.md`；标题“0538 信息显示图层下沉收口”；11–25行1.7.448层级修正，30–34行历史本地检查，38–42行待用户核对 | `CCB0FC7EC1BF91DEF0001EF22DAB8EA831FC5D871E4DCE5844C6E6732165DAEF` |
| H01b | `文档/归档历史计划/信息显示图层下沉/00-基准.md`；标题“信息显示图层下沉：基准”；46行半透明UI口径，55–57行明确Terraria 1.4.5.6的层级前提 | `E877F561D2CDF959F62FB3B0F56DFCC5378E7C53B0C5060FDEBAECFEB7F1DEE7` |
| H02 | `文档/更新记录/0540-骷髅商人NPC显名.md`；标题“0540 骷髅商人 NPC 显名”；7行旧报告，11–14行1.7.450修法，25–29行本地检查，33–36行待实机 | `D210EB884322785DDC709ED0F65BA3D62CB69CA6F5C60212142B3C98B3D76930` |
| H03 | `文档/更新记录/0542-敌怪显名血量两行.md`；标题“0542 敌怪显名血量两行”；7–18行1.7.452两行、血量来源与当时−0.10字号差 | `5BD6D87C396CEE6B7F6280E7DAB0B41685F8D7003FA80710B7B418C0DADC1D07` |
| H04 | `文档/更新记录/0543-敌怪血量标签视觉收紧.md`；标题“0543 敌怪血量标签视觉收紧”；7行旧视觉报告，11–14行1.7.453的0.75倍与行距修法 | `A6534DA1092E25503B53E040494F62CD1442F4004F89DC67F83F4A253029A5B8` |
| H05 | `文档/更新记录/1.7.461-敌怪血量字号固定差值-2606060112.md`；标题“1.7.461 敌怪血量字号固定差值”；7行当时反馈与后续要求，11–14行−0.13及0.80/0.67样本 | `939EB8074B985CD1CC35BB4F7639E6AC847876CF8ED01D81469193318E7C3786` |
| H06 | `文档/更新记录/1.7.462-敌怪血量行距补偿-2606060128.md`；标题“1.7.462 敌怪血量行距补偿”；7行旧拥挤报告，11–14行保留差值、0.80样本行距10→12 | `14598A8DCFB58BB237EEF9525B2F17E8D4C1837CCFA33C78B47AB8AAFBF716F7` |
| H07 | `文档/更新记录/1.7.658-箱内定位高亮存在性校验-2606140008.md`；标题“1.7.658-箱内定位高亮存在性校验”；仅取18–19行NPC生产未变/补测试缝及49–53行结果口径，不提取相邻箱子功能 | `E00BBB90856012E5553D92C930953A7404FC41C073730E0FA29AF0B0C4530FD6` |

H01 表示 H01a/b 合读。邻近 Git 历史还可定位到 [80dafbc1f02f155570ff08aa37012c52e74909f1“显示血量”](https://github.com/Kuroneko2020/JueMingZ/commit/80dafbc1f02f155570ff08aa37012c52e74909f1)（2026-06-05）；后续文件迁移使当时路径与当前服务路径不同，不能把历史记录的测试/反馈归到本轮固定源码或 R。

### 原版本地参考 P01

仅定向读取 R 已有的 `.7` 精选参考，按[本地参考资料使用说明](../设计/本地参考资料使用说明.md)使用；未新增或改变资料。它们被 Git 忽略，以下是相对 R 根的实际路径及本轮内容身份：

| 路径与范围 | SHA-256 |
| --- | --- |
| `references/terraria/1.4.5.7/Terraria/NPC.cs`：6751–6789名称getter、6911–6923 `CountsAsACritter`、8474/8541/8547默认重置；9364–9387兔/腐化兔、10271–10284被缚哥布林、14290–14302金兔、14427–14442骷髅商人、17239–17257城镇猫狗 | `55757C5FC90EF137C82A6A6B1D5F7CD1A563FA80CB524FCA1D0F558EA4391F81` |
| `references/terraria/1.4.5.7/Terraria/ID/NPCID.cs`：4829静态Critter集合及对应对象常量 | `A91F815C97500929F5655526FB7FFA9CC56F6968BCFB9C4B7B927BED635D50F0` |

资料入口标识版本1.4.5.7；本轮没有重建原版可执行文件到反编译文件的生成链，也没有把这些字节当作1.4.5.8事实。没有读取真实角色、世界或用户配置。没有本轮可用于证明三个功能实机效果的截图。

### 覆盖结论与剩余缺口

本轮完成三条入口→设置/模式→共享观察→各自内容→世界绘制→关闭与缓存的静态追踪；当前 R 对照来自具体消费者/注册和状态拥有者；旧修正、旧测试断言、源码风险和新版建议分别保留。未发现独立血量开关、血条、自动避让或标签交互，不能因此擅自加入新版。

尚未查明或未验证：当前`.8`的完整对象/共享生命/名称与网络生成链；特殊类别在`.8`的所有真实重叠实例；各客户端实际同步范围；地图/相机/资源重建/失焦无后续Draw的现场行为；串名、错血、语言缓存和名额饥饿风险的实际发生情况；任何本轮实机、多人、FPS或所有者接受结果。代表性`.7`对象与旧`.6`层级记录只支撑已标明版本的结论。后续依赖这些缺口的设计与验收须补对应证据，不阻碍本次文档候选交付。

本次提取与需求最终化没有运行构建、测试、游戏、宿主或旧二进制，没有新增测量/诊断能力、公共框架、接口桩、注册占位或实现。按已确认目标另立同批设计/实施任务，不能把本文或文档检查通过视作三个功能已可用。

# F5 控制界面样板与 UI 基础

状态：Phase 0-U 已授权的任务内实现设计；产品视觉、实际输入隔离与材质字体体验仍由项目所有者实机验收。动态进度和第四 Hook 授权见 [Issue #30](https://github.com/Kuroneko2020/JueMingR/issues/30)，阶段路由见 [Issue #7](https://github.com/Kuroneko2020/JueMingR/issues/7)。本文件不改变既有总体架构、加载链或已接受 ADR。

## 产品与布局

沿用已批准的顶部导航样板，不重新比较侧栏。正常逻辑尺寸 580×740；标题栏 34 高，两个导航行分别为：

- 物品、杂项、地图、查询、笔记、关于；
- 蓝图、钓鱼、战斗、信息、增益、移动。

初次进入默认“信息”。唯一真实业务为“群系显示”；其它控制组仅保留外形、不可执行，不显示占位或开发说明，也没有此类 hover 提示。没有内容的分类保留空内容区。信息页按实际名称、控件和必要状态计算紧凑行高；钓鱼页仅承载获批压力布局，包括普通行、多按钮组、空的非编辑输入框外形、364 宽主过滤区、16 间隙及 142 宽右侧特殊区，无钓鱼、保存、改名或过滤业务，不显示虚构的角色名或运行状态。

正常内容面板为 `(12,131,556,597)`，实际内容 viewport 为 `(20,139,522,581)`。1280×720、150% UI scale 时窗口仍为 580 宽，高度为 456，内容 viewport 高 297。标题和两行导航固定，只有内容滚动。内容尺寸由实际字体度量作有界局部排版，不按屏幕比例整体缩字。逻辑宽度不足 604 或高度不足 220 时关闭不可读窗口，保留原版游戏。

窗口原点、选页、滚动及捕获属于本进程的 UI presentation state，不持久化。窗口或 session 关闭取消控制组 armed 状态与拖动；已归属按钮的释放尾部独立完成，不重放世界操作。

显示标题为“决明R”，沿用实际字形度量和字号，直接绘制在主窗口顶部，不绘制独立底框；标题下方、导航之前有一条浅色固定分隔，原标题拖动区域保留。这不改变程序集、仓库或包身份。主窗口的纹理填充与边框一起裁成轻微圆角，固定逻辑轮廓不会被方角皮肤改成直角；指针遮挡仍使用整个窗口矩形。滚动通道保持 10 logical，轨道视觉 4、滑块视觉 6，中心一致且均不进入内容 viewport。圆头和 hover/拖动反馈不改变实际滚动几何；短页不绘制虚假滑块，内容缩短立即夹紧旧 offset。

导航恢复对应十二个几何图标，统一 18 logical 图标盒，与 5 logical 间距和文字作为整体居中。完整 18×18 源域决定绘制比例，非零 alpha 边界仅用于居中，保留原图留白；不把裁紧图案放大至占满图标盒。可见图形约 8–13.5 logical，圆端和细描边按源比例呈现；切页和 hover 只改变颜色。功能名称左对齐；按钮组在功能行内垂直居中，组内按钮等高、等间距；需要换行时由同一个布局缓存计算内容与操作区。

选中页签的短下划线由图标、间距与文字整体宽度派生；功能状态短线由按钮文字宽度派生。二者居中、左右至少避开十个 logical 的角区，并与实际字形留间距、避开底部两单位外缘；不逐帧读取皮肤透明度。字体过高而固定导航容不下文字与短线时，沿用可见的局部失败处理，不压字、缩字或把线画到外缘。页面功能按钮视觉上下至多内缩一单位，hitbox 中心不变；高字形保留边距，必要时按钮高度仅增加所需安全间距。背景、文字与短线从同一布局数据派生，不建立按钮等级。

信息页仅在“显示龙蛋”与“调整信息窗位置”之间加入一条内容分隔，左右内缩 12，计入内容高度并跟随滚动/裁切，不可点击。它与固定标题分隔分别属于内容和窗口。功能行尾部键位入口为较小的自绘键盘几何，以 14 logical 源域居中，可见约 10.5×7 logical，没有按钮底色、额外边框或文字“键”；仅保留图案自身轮廓，仍无绑定/录入/弹窗/持久化业务。按项目所有者查看预览后的追加决定，键盘槽位及命中宽度从 30 收为 22 logical，右侧内边距仍为 8，前方按钮间隔仍为 4；同排左侧按钮整体右移 8，键盘中心右移 4，不带键盘的行不变。键盘复用同一离线图集的圆端和圆弧覆盖率采样，作为末尾第十三项；前十二个导航图标的遮罩字节保留。

## 实现职责

继续现有六个生产项目，没有新依赖、Runtime、Feature Registry 或通用输入系统。

| 文件 | 职责 |
| --- | --- |
| `TerrariaHost/F5/F5Layout.cs` | 窗口/页面本地矩形、当前页固定文本度量、受限布局与有界缓存 |
| `TerrariaHost/F5/F5Interaction.cs` | 同一布局的命中、F5 边沿、捕获、滚动、控件按下/释放及窄群系命令 |
| `TerrariaHost/F5/F5Renderer.cs` | 读取 Terraria 当前字体/材质，绘制缓存内容，维护自身裁切状态 |
| `TerrariaHost/F5/F5IconAtlas.cs`、`F5/Icons/` | 十二导航与一枚键盘的有来源离线图形及 renderer 独占的单一图集 |
| `TerrariaHost/F5/F5Shell.cs` | 采样后的原版输入适配、统一归属、短期状态恢复、UI 生命周期 |
| `TerrariaHost/Phase0SLoadChainHost.cs` | 既有 handoff、精确四目标安装与有界 layer 注册 |
| `TerrariaHost/Phase0TBiomeRuntime.cs` | 将 UI 窄启停命令交给原有唯一 Feature |

群系 Feature/Runtime 仍是唯一开启及故障状态来源。正常关闭立即清理 ViewModel，停止 Zone observation、文本更新和 Overlay 绘制；重新开启在合适的下一次 Runtime Update 立即观察。Feature 故障保持不可用，拒绝后续开启命令，不自动重试。UI 不保存第二个群系布尔值：实际开启只在“开启”下画绿短线，实际关闭只在“关闭”下画红短线；功能名与按钮文字均为常规前景，不因开关或 hover 染色。正常“当前：已开启/已关闭”及其第二行完全删除。失败时两个按钮禁用且无线，复用已有“群系显示暂不可用”悬停提示与 Feature 故障反馈，不永久预留错误行；未接入项不伪造关闭状态。

## 固定 Hook 与已确认时序

固定 Terraria 1.4.5.8 的程序集身份仍由原有 SHA-256/MVID/版本加载门核验；没有扩大游戏或依赖版本。三个 postfix 和一个 prefix 共用既有 owner `JueMingR.Phase0S.MainUpdate`：

| 精确目标 | Patch | 职责 |
| --- | --- | --- |
| `Main.Update(GameTime)` | postfix | 既有一次 handoff、唯一 Runtime 更新与 UI 准备/收尾 |
| `Main.SetupDrawInterfaceLayers()` | postfix | 既有群系层及四个窄 F5 layer 的固定锚点注册 |
| `Main.DoUpdate_HandleInput()` | postfix | 原版完成本帧采样之后、玩家使用/快捷栏消费之前处理 F5 和消费已归属输入 |
| `Main.HoverOverNPCs(Microsoft.Xna.Framework.Rectangle)` | bool prefix | Ready、Visible、当前命中或捕获可靠成立时跳过 NPC 悬停原体，其余执行原版 |

后两目标逐个核验 private、instance、void、参数精确类型/数量、非泛型和实际 managed body，不模糊按名称选重载。固定文件静态证据确认 `HoverOverNPCs` 唯一调用位于 `DrawMouseOver`，紧前由调用方清 `HoveringOverAnNPC`。原体包括绕过普通 `mouseInterface` 检查的 NPC type 685 分支，所以单靠点击消费或 tooltip 标志不能证明纯悬停正确。

当前受控 fixture 中每目标只有本 owner 的准确 patch 类型；原有安装身份/独占检查仍有效。未验证外部补丁组合，不声称返回 false 会阻止其它 owner 的所有 patch，也没有新增通用冲突管理。

## 指针消费者与恢复

`F5Interaction.OwnsPointer` 保存唯一窗口/捕获归属事实；Host 再统一核验当前能否实际呈现 F5。全屏地图、隐藏 UI、Fancy UI、游戏选项、相机、失焦和非单人键鼠模式均关闭 F5；当前地图/相机切换请求也先取消控件与捕获，继续处理已归属按钮的真实释放。NPC prefix 不存第二份遮挡标志，不做反射、日志、文字度量、I/O 或实体扫描。

输入采样使用原版 `MouseInfo`、绝对滚轮派生的 delta、当前 keyState 和冻结 UI matrix；不再次读取真实鼠标或移动鼠标。该 matrix 的逆变换用于指针，正变换用于整个窗口和内容裁切。改变窗口位置只改 origin，滚动只改 offset，页面绘制和控件命中共享同一当前布局。

| 路径 | 前置处理 | 恢复与行为验证 |
| --- | --- | --- |
| 点击/物品/放置 | 在 Triggers.CopyInto、ItemCheck 和 UI 点击之前消费 Current/JustPressed/JustReleased 的已拥有按钮与 Main.mouseLeft/right | 不恢复消费过的按钮；尾部覆盖移出、关闭、失焦合成 release，直到 focused 真实释放；验证实际使用计数 |
| 纯滚轮/快捷栏 | 保存本轮 UI 滚动后清两种 scroll delta，包含短页和顶底边界 | 保留绝对值，不积压或补发；验证实际快捷栏选中格 |
| NPC/特殊交互 | 第四 prefix 阻止整个 NPC 悬停原体 | 关闭/窗外/无捕获即执行；不改 NPC 或玩家位置、不清其它 noThrow 状态；验证原体命中/交互计数 |
| 掉落物 | `Mouse Item / NPC Head` 之后、`Mouse Over` 紧前设当前 mouseText 门 | 直到 `Interact Item Icon` 之后收回自身 lease；验证只读 !mouseText 的实际 dropped-item consumer |
| 上帧气泡/名称 | Update 结束、Emote Bubbles 之前清特定 `currentNPCShowingChatBubble`；经过原版模态早退层后才认领 null pending text；仍分别阻止 NPC/drop 消费者 | F5 自己绘制缓存 hover 文本，正常光标保留；原版本轮 cache 生命周期负责清理；验证旧气泡和最终名称，以及中途开启相机的原版提示 |

四个 F5 layer 分别紧邻 `Achievement Complete Popups` 前、`Cursor` 前、`Mouse Over` 前和 `Interface Logic 4` 前。第一个位于 Capture/Ingame Options/Fancy UI 之后，避免原版中途停止 layer 枚举时留下 F5 的 pending-text 锁。世界绘制会重置 mouseInterface，中间层还会重置 mouseText，因此更新期门和晚绘制期门不能互相替代。后者覆盖窗口下方同一鼠标位置的玩家、标牌、资源条等提示；保留常驻 Overlay、群系、伤害数字、正常光标与世界更新。不干预掉落物生成、拾取或背包。

mouseInterface/mouseText lease 保存进入时值，在有限消费区间后恢复；绘制前置门不横跨其它原版 UI 的合法 mouseInterface 写入。下一次采样及故障路径兜底收回自身 lease。手柄/fast-use 模式不开放 F5 键鼠窗口，不交换库存以处理该模式。

## 字体、材质与成本边界

字体使用 `FontAssets.MouseText.Value`；窗口、内容和按钮使用当前 `InventoryBack` 的自带颜色及 alpha，辅以中性明暗区分层次，普通与未接入控件文字均保持可读。`MagicPixel` 用于少量几何。原版将 `InventoryBack` 用于物品格；在固定九宫格中复用为 F5 大表面是本任务的受控重用，不声称原版菜单本来如此。原版 `SettingsPanel` 是左右 2 像素的横向条，`InventoryBack13` 是需要原版蓝 tint 的底板，均不再以白 tint 错用为大面板。不固定暗色主题、不按材质包名分支、不复制游戏资源或以纹理尺寸改变几何。资源不可用时局部降级为有限中性色块；游戏资产不由 F5 Dispose。

皮肤身份与字体度量分别失效。字体变化时，用公开 `DynamicSpriteFont.DrawCustomFast` 零绘制回调收集与 DrawString 同源的字形矩形，包含 fallback、kerning、字间距和 cropping 偏移。缓存保留宽高与 Left/Top；相同宽高但偏移变化也刷新定位，相同度量保持 layout generation。测量不以 LineSpacing 代替字形高度，不做 GPU readback。当前四方向描边的 ±2 logical 与字体 scale 无关，因此布局另留四个 logical 单位，绘制按相同偏移回到字形原点。

上述 API 提供真实绘制四边形，不能判断矩形内部特殊透明留白；本轮不扫描字体纹理或建设通用文字排版引擎，不能承诺任意字体包均精确到非透明像素居中。正常字号不会为越界整体缩小，真正无法容纳的资源继续走已有可见失败反馈。

固定文本和三个真实群系 hover 提示最多 1024 个缓存项，无隐藏页更新。F5 提示尺寸在布局阶段缓存，由自身 renderer 绘制；不交给会逐帧 MeasureString 的原版 pending-text 消费者。稳态、hover、拖动和普通滚动不重测；纯皮肤变化不重排，UI scale 改变只重建逻辑布局。关闭窗口不布局、度量或绘制。renderer 拥有一个 72×936 图标 atlas、一个 8×8 圆头纹理和 RasterizerState；只在首次需要或图形设备变更时创建，退出 Session/原版模态或局部失败时释放，不释放共享字体或材质。外缘带状裁切使用固定有界几何，不创建 RenderTarget。

图标来自项目所有者明确授权的十二项 Legacy 自制几何，来源、固定 commit、数值图形、离线导出方式和哈希见 [图标来源说明](../../src/JueMingR.TerrariaHost/F5/Icons/NOTICE.md)。构建直接嵌入已提交 alpha 资产和说明，不依赖 Legacy、Python 或 SVG 库；离线导出脚本只供人工维护资产时使用。未引入第三方图标集或字体，也不扩张 Legacy 的整仓许可。

绘制在自身批次中使用冻结 matrix，内容裁切只施加于 viewport。finally 恢复实际 ScissorRectangle、RasterizerState、BlendState、DepthStencilState、SamplerState 及原版 layer 的 SpriteBatch Begin 契约。F5 局部异常关闭窗口、收回自身状态，在后续更新给出一次普通文字提示，不卸载整个 Harmony owner，不拆核心 handoff 或群系功能。

## 验证与交付边界

既有 `scripts/test-phase0s.ps1` 纳入 Phase 0-U 生产布局/输入检查及实际加载 Host 的 fixture 集成：同一次安装的两个独立进程、原有五事件 evidence、群系 cadence/生命周期、准确四目标集合和 TEMP 安装/恢复继续保留。

fixture 消费者严格保留关键顺序：采样 → 生产 input postfix → 实际使用/热栏；Draw 重置 → 旧气泡先绘后清 → 中间 mouseText 重置 → production hover gate → 掉落物 loop → production NPC prefix/原体。断言动作与最终输出，不以 flag 或 prefix 返回值代替行为。

模态回归分别模拟完全跳过 DrawInterface、前置层返回 false，以及 Capture 层内当帧开启；验证新输入到达原版消费者、隐藏的已按下按钮不在松开时执行、原版相机提示仍绘制、结束后 F5 可重新打开。独立进程验证真实群系故障后点击开启仍显示不可用；既有 F5 局部故障保留健康群系的场景继续独立运行。测试 evidence 轮询及并发失败快照以 FileShare.ReadWrite 读取，受控检查在读句柄仍打开时执行生产 writer；使用原有有界状态等待，区分事件 3 写入与 Bootstrap 真正完成，再执行唯一首个受控 Update，事件断言不变。此前一次缺失底层 evidence 的启动超时仍原因未知，不能由共享读取修正倒推其原因。

图形 fixture 创建隐藏测试窗口及真实 XNA GraphicsDevice，使用生成的字体/纹理执行实际 Host renderer，验证偏移与行距差异、普通/异常裁切收尾、输入消费者和状态。独立的 `phase0u-visual <本地 Content 目录> <本地输出目录>` 模式读取合法 XNB 字体和表面，以链接的生产 renderer 生成可人工查看的离线预览；Terraria 的薄 panel/text helper 由保留切片和四方向描边语义的 fixture 提供。它另验证方角暗色资源替换、无重排与 GPU 所有权，但不等于某个真实第三方包实机。该模式不启动 Terraria、不读角色世界、不把原资源或预览纳入包。Debug/Release、架构、fixture 和包字节复现均为本地证据；FPS、慢帧和实际输入隔离仍须分别记录。

沿用现有包 builder 的 `Phase0UF5UI` profile，ID 为 `phase0u-f5-ui-<FULL_SHA>`，ZIP 为 `JueMingR-Phase0U-F5UI-<FULL_SHA>.zip`。只从被审查 clean commit 构建；包内不带游戏、ReLogic/XNA、Legacy、字体材质、私人路径、原始调查或旧 evidence。安装恢复脚本的所有权合同和 Bootstrap evidence 语义保持不变。

唯一 PR 和一次定向独立审查后，按当前授权安全安装，停在项目所有者的测试卡门。不自动启动游戏、合并、关闭 Issue、最终化或进入下一阶段。

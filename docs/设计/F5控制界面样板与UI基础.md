# F5 控制界面样板与 UI 基础

状态：Phase 0-U 已授权的任务内实现设计；产品视觉、实际输入隔离与材质字体体验仍由项目所有者实机验收。动态进度和第四 Hook 授权见 [Issue #30](https://github.com/Kuroneko2020/JueMingR/issues/30)，阶段路由见 [Issue #7](https://github.com/Kuroneko2020/JueMingR/issues/7)。本文件不改变既有总体架构、加载链或已接受 ADR。

## 产品与布局

沿用已批准的顶部导航样板，不重新比较侧栏。正常逻辑尺寸 580×740；标题栏 34 高，两个导航行分别为：

- 物品、杂项、地图、查询、笔记、关于；
- 蓝图、钓鱼、战斗、信息、增益、移动。

初次进入默认“信息”。唯一真实业务为“群系显示”；其它页面和控制组均明确标记“结构占位 · 未接入”，不可执行。信息页保留密集的名称/操作组排列；钓鱼页仅承载获批压力布局，包括多个普通行、长中文提示、多按钮组、364 宽主过滤区、16 间隙及 142 宽右侧特殊区，无钓鱼、保存、改名或过滤业务。

正常内容面板为 `(12,131,556,597)`，实际内容 viewport 为 `(20,139,522,581)`。1280×720、150% UI scale 时窗口仍为 580 宽，高度为 456，内容 viewport 高 297。标题和两行导航固定，只有内容滚动。内容尺寸由实际字体度量作有界局部排版，不按屏幕比例整体缩字。逻辑宽度不足 604 或高度不足 220 时关闭不可读窗口，保留原版游戏。

窗口原点、选页、滚动及捕获属于本进程的 UI presentation state，不持久化。窗口或 session 关闭取消控制组 armed 状态与拖动；已归属按钮的释放尾部独立完成，不重放世界操作。

## 实现职责

继续现有六个生产项目，没有新依赖、Runtime、Feature Registry 或通用输入系统。

| 文件 | 职责 |
| --- | --- |
| `TerrariaHost/F5/F5Layout.cs` | 窗口/页面本地矩形、当前页固定文本度量、受限布局与有界缓存 |
| `TerrariaHost/F5/F5Interaction.cs` | 同一布局的命中、F5 边沿、捕获、滚动、控件按下/释放及窄群系命令 |
| `TerrariaHost/F5/F5Renderer.cs` | 读取 Terraria 当前字体/材质，绘制缓存内容，维护自身裁切状态 |
| `TerrariaHost/F5/F5Shell.cs` | 采样后的原版输入适配、统一归属、短期状态恢复、UI 生命周期 |
| `TerrariaHost/Phase0SLoadChainHost.cs` | 既有 handoff、精确四目标安装与有界 layer 注册 |
| `TerrariaHost/Phase0TBiomeRuntime.cs` | 将 UI 窄启停命令交给原有唯一 Feature |

群系 Feature/Runtime 仍是唯一开启及故障状态来源。正常关闭立即清理 ViewModel，停止 Zone observation、文本更新和 Overlay 绘制；重新开启在合适的下一次 Runtime Update 立即观察。Feature 故障保持不可用，拒绝后续开启命令，不自动重试。UI 不保存第二个群系布尔值；状态用“当前：已开启/已关闭/不可用”表示，故障时两个按钮均禁用。

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

字体使用 `FontAssets.MouseText.Value`；面板/按钮读取当前 `SettingsPanel`、`InventoryBack13`、`InventoryBack` 及 `MagicPixel`。不复制游戏资产，不按纹理尺寸改变几何，不新建通用主题树。绘制跟随当前纹理表面；不可用面板局部降级为有限中性色块。游戏资产不由 F5 Dispose。

皮肤身份与字体度量分别失效。字体值被替换时有界重测已缓存固定文本，只有实际宽高变化才重排；相同度量保持 layout generation。当前页固定文字及四种 hover 提示最多 1024 个缓存项，无隐藏页更新。F5 提示尺寸在布局阶段缓存，由自身 renderer 绘制；不把非空提示交给会逐帧 MeasureString 的原版 pending-text 消费者。稳态无文字测量、布局重建、文件 I/O、反射扫描或逐帧日志。关闭窗口不布局、度量或绘制；释放 F5 自有 RasterizerState 不影响游戏资源。

绘制在自身批次中使用冻结 matrix，内容裁切只施加于 viewport。finally 恢复实际 ScissorRectangle、RasterizerState、BlendState、DepthStencilState、SamplerState 及原版 layer 的 SpriteBatch Begin 契约。F5 局部异常关闭窗口、收回自身状态，在后续更新给出一次普通文字提示，不卸载整个 Harmony owner，不拆核心 handoff 或群系功能。

## 验证与交付边界

既有 `scripts/test-phase0s.ps1` 纳入 Phase 0-U 生产布局/输入检查及实际加载 Host 的 fixture 集成：同一次安装的两个独立进程、原有五事件 evidence、群系 cadence/生命周期、准确四目标集合和 TEMP 安装/恢复继续保留。

fixture 消费者严格保留关键顺序：采样 → 生产 input postfix → 实际使用/热栏；Draw 重置 → 旧气泡先绘后清 → 中间 mouseText 重置 → production hover gate → 掉落物 loop → production NPC prefix/原体。断言动作与最终输出，不以 flag 或 prefix 返回值代替行为。

模态回归分别模拟完全跳过 DrawInterface、前置层返回 false，以及 Capture 层内当帧开启；验证新输入到达原版消费者、隐藏的已按下按钮不在松开时执行、原版相机提示仍绘制、结束后 F5 可重新打开。独立进程验证真实群系故障后点击开启仍显示不可用；既有 F5 局部故障保留健康群系的场景继续独立运行。测试 evidence 轮询及并发失败快照以 FileShare.ReadWrite 读取，受控检查在读句柄仍打开时执行生产 writer；使用原有有界状态等待，区分事件 3 写入与 Bootstrap 真正完成，再执行唯一首个受控 Update，事件断言不变。此前一次缺失底层 evidence 的启动超时仍原因未知，不能由共享读取修正倒推其原因。

图形 fixture 创建隐藏测试窗口及真实 XNA GraphicsDevice，使用生成的测试字体/纹理执行生产 renderer；验证普通/异常裁切收尾。它不启动 Terraria，不证明实际中文资源包视觉。Debug/Release、架构、fixture 和包字节复现均为本地证据；FPS、慢帧和实际输入隔离必须分别记录，未实机时不能报告“彻底修复”。

沿用现有包 builder 的 `Phase0UF5UI` profile，ID 为 `phase0u-f5-ui-<FULL_SHA>`，ZIP 为 `JueMingR-Phase0U-F5UI-<FULL_SHA>.zip`。只从被审查 clean commit 构建；包内不带游戏、ReLogic/XNA、Legacy、字体材质、私人路径、原始调查或旧 evidence。安装恢复脚本的所有权合同和 Bootstrap evidence 语义保持不变。

唯一 PR 和一次定向独立审查后，按当前授权安全安装，停在项目所有者的测试卡门。不自动启动游戏、合并、关闭 Issue、最终化或进入下一阶段。

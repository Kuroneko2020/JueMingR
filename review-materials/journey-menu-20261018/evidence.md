# 旅行菜单候选证据

授权以 [#122](https://github.com/Kuroneko2020/JueMingR/issues/122) 为准，完整差异见 [PR #123](https://github.com/Kuroneko2020/JueMingR/pull/123)。固定基底 `0ef3c62dcf0c365aee5d5cfcfbb5492b70b841f3`；原始委托 `JMR-JOURNEY-MENU-20261018-v1` 的00—08保留在本地 references。候选完整SHA由Issue/PR交付记录承载，避免本文件自包含提交身份。

## 按钮收尾与接受边界

本轮所有者明确委托 `JMR-JOURNEY-CLOSEOUT-20261018-v1`：本人反馈除冗余“打开”外功能实测未发现问题，网页已只读复审 `096da6b8575cb87825b0959f24aacadd8e050223` 相对基底的39文件和关键链。本轮保留这两种来源，不将网页阅读或总体体验写成新的力量/保存/物理断电、全部设备、真人多人或量化FPS实测。

共同标准行撤除“打开”及 OpenJourney 枚举/专属分派，只保留开启、关闭与原 `journey.toggle` 绑键。Enable 仍交给 `SetEnabled(true)`，原 Active 早返回 Open 和底层 Open 方法保持；没有改旅行核心、保存、预测、Runtime 或输入状态所有者。F5Command 为内部瞬态命令，后继战斗命令仅使用相对枚举算术，没有持久化数值身份。关键方法头、接口、直接调用者和测试说明已集中自查；在实际分派处说明重复开启不得 Stop/Activate 重捕基线，安全离页、失败反馈、F5让出和输入尾部保持原职责。

本轮实际执行原件在 `.local/journey-menu-20261018/closeout/`：

| 检查 | 实际执行及边界 |
| --- | --- |
| Debug生产/结构与窄原版UI | `powershell -NoProfile -File scripts/test-world-object-text.ps1 -ContentDirectory <匹配原版Content> -OutputDirectory .local/journey-menu-20261018/closeout/native-reopen-01 -Scope JourneyReopen -ApprovedRetainedAssets .local/journey-menu-20261018/retained-assets.json`，exit0；PrepareOnly及probe均0警告/错误，结构0fail。`native-reopen-01.log` 保留完整日志；不是累计交付资格 |
| 原版重开增量 | 复用同一原版准备/NativeJourneyUiChecks片段：真实首次开启与失败反馈、原冻结按钮选择、F5收窗后真实点击Enable、Active/同一Persistence引用/所选冻结保持、已可见重复Enable不关窗、Disable恢复、原绑键录入及开关、按放尾部均通过。`JourneyReopen` 在现有Run里明确停止于UI后，未调用复制/完整力量/共存/保存/独立reader/强杀矩阵 |
| 既有F5 CPU | `dotnet build tests/Phase0SFixtureTerraria/Phase0SFixtureTerraria.csproj --configuration Debug --nologo -p:Platform=x86 -p:JueMingRBuildRoot=<repo>/.local/journey-menu-20261018/closeout/f5-cpu-build`，随后其 `Terraria.exe f5-cpu`，均exit0，0警告/错误。`f5-build.log`、`f5-cpu.log`：两条建页、两个业务按钮/原绑定、隐藏无占位、滚动底边、100次稳定布局复用及既有输入/绑键断言通过；替身不证明原版恢复安全 |
| 实际呈现 | 已实看 `native-reopen-01/journey-f5-row.png`、`journey-native-menu.png`：旅行行仅开启/关闭/键，未留下旧按钮矩形，同行/相邻间距与底边完整；真实原版时间窗口打开。物理OS键鼠/IME及所有者本人新按钮实测未执行 |

新执行Host SHA-256为 `8F96181E7A6DD0328B5732A6F62FFF1263B19070B3A77B2AEA5B047CD2837763`，probe为 `C05C270F20324994BA4E19BD07482F2397370D68CB9E7ED8A6C559429B38AF0E`，CPU fixture为 `549AC5D27B225BC9BA1F3AB4E0797DA4A1452BB661E19E46B4D568EE0F2261CC`。原版仍为下方锁定1.4.5.8，Content只读。新scope仅自造隔离根，日志记录启动及原生初始化音乐/音效/环境声均0，不操作所有者游戏/真实存档；隔离材料保留便于审查。

旧完整独审 `independent-review-982fdb9.md`、full-resume-26完整工程、保存/强杀/共存和预测接缝继续按下方原身份复用。本轮完整源码增量核对确认核心Journey、保存、预测、Hotkeys/输入实现及其相关依赖未变；直接UI期待和选择分支改动由本轮窄原版与CPU覆盖，不把旧Host执行改签为新Host全矩阵PASS，也不迁成正式缓存成绩。合同/指南/设计/索引/入口同步本批接受与重复开启职责，接受随 PR #123 合并生效，实际合并状态与SHA回到Issue/PR。本轮候选固定后交另一Sol/High窄审与主调度集中审核，原全审不重开。

本轮不打包、不核旧ZIP、不安装/恢复/扫描安装载荷，也不更改旧manifest或回执。现装/原完整包仍为 `982fdb997c2ac43595d8907d6c74f448d630ae16`，现装仍含旧按钮；按钮收尾随仓库合并生效，待以后明确获准的完整包带入。外来四项指纹在 `closeout/retained-before.json` 保留并排除提交。下面全部原工程、失败、原包及安装记录保持历史身份。

## 原982fdb9交付结论与准入

实现者完整专项、自查、未参与实现/期待编写者的独审及主调度集中准入均通过。已按07完成一个普通完整开发包及一次轻量备份换装，停止待所有者实测。发布资格仍为 `INCOMPLETE / releaseEligible=false`，外来保留资产使真实 `clean=false`；不清除它们来取得表面clean。本文件后继纯交付记录不改变实际包/安装来源。

实现保持人物difficulty和世界基础GameMode原值，只在准确原版消费者提供本次资格。持续力量保存采用激活前由原版生成的31/16字节世界/人物子流；真实研究、复制物品和其它进度仍由原版保存，不恢复整份旧文件。详见[行为合同](../../docs/功能介绍/旅行菜单行为合同.md)和[设计](../../docs/设计/旅行菜单实现设计.md)。

## 实际包与一次安装

| 事实 | 实际结果 |
| --- | --- |
| 产品、包、安装来源 | `982fdb997c2ac43595d8907d6c74f448d630ae16`；完整ContinuousProcessing / Release / net472 / x86，包含已接受main、HIDE文案收尾和本轮旅行，并非单DLL补丁 |
| 独审/准入 | `.local/journey-menu-20261018/independent-review-982fdb9.md`，SHA256 `04CF3B9B30C98DB6A4B4EAB23CA123438FE6CAFA3CB5579C9D639F61EDE88DC6`；主调度真实决定为 `coordinator-admission-982fdb9.md`。8项正向reader实际通过，阶段记录SHA `E9EB8CCF551F8D7AED47400033362DDD4CB1DF19E31DFFC2354A723FD4C98403` |
| 唯一ZIP | `outputs/journey-menu-20261018-982fdb9/JueMingR-Continuous-Processing-982fdb997c2ac43595d8907d6c74f448d630ae16.zip`；2334626字节，SHA256 `B8DA6FDA115220FD96F230662AE0F675F0891140440B2A75FBBEF06FE8F3591E` |
| 包核验 | 普通完整包入口执行正向准入、Release/结构及载荷/ZIP核验；一次 `verify-existing-package.ps1` exit0，17成员/10载荷，源码/框架/平台/归属吻合。原件 `package-build-982fdb9.log`、`package-verify-982fdb9.log`，包记录 `phase-0-s-build-record.json` |
| 安装 | 工具确认 `F:/games/gameplats/steam/steamapps/common/Terraria` 的旧 `b4a48f7` 拥有关系及无游戏/Server/worker进程，旧包正规Restore和新包正规Install均exit0；一次新拥有权、回执及10载荷长度读回通过，回执SHA `A6440C6E0D1733AEF1DDF96C281898401ADA372A408E1A635D05545852911598` |
| 备份/恢复 | 只备份12个将删除/替换的旧受控文件到 `.local/journey-menu-20261018/delivery/backup-b4a48f7-before-982fdb9`，清单 `delivery/backup-and-ownership.json`。旧完整包 `outputs/hidden-entry-20261018-b4a48f7-final/`及旧回执保留；正常回退入口为新包Restore、旧包Install，不手拼DLL |

安装原件为 `.local/journey-menu-20261018/delivery/{restore-old.log,install-new.log,installed-982fdb9.json}`。未启动所有者游戏、未读写其角色/世界/研究/用户配置正文，不全用户目录哈希；未安装后再跑业务矩阵。安装成功不等于新包普通游戏启动或所有者体验接受，Issue保持开放、PR保持Draft，不merge/close/Tag/Release。

独审明确校正归档边界：`fixed-full26-runtime/manifest.json`实际仅9文件，没有旧7D Host DLL/PDB；不能称旧Host或完整旧运行集合已归档。旧运行保留执行hash和完整输入关系，支持本任务经过独审的有限手工行为复用，不用新75C冒充旧7D、不迁成正式workload缓存PASS。候选JSON和Issue/PR说明已更正；不为此重跑玩法或改产品。

## 完整原版专项

最终实际入口为 `NativeWorldTextProbe <repo> <Content> <output> Journey`；输出目录 `.local/journey-menu-20261018/native-validation/full-resume-26/`。进程exit0，6319条检查包含逐目录cap断言，不是6319项功能。所有角色/世界/成就文件均自造、位于本轮独立根；音乐、音效、环境声全程0。未操作所有者游戏/真实存档/Steam成就或云账户。

| 范围 | 实际结果 |
| --- | --- |
| 入口/输入 | 原Shell隐藏资格、开启失败回滚/真实失败提示、成功后F5让出、统一打开/关闭及绑定录制；原版分类/搜索队列、左右键复制和原ItemSlot入包；首帧失焦旧映射不变命令，中性回焦重臂；绑定按住只切一次、实际正反短反馈 |
| 复制与来源 | 同一真实复制对象经原ItemCheck放置消耗，随后保存/读回且关闭不回滚；鼠标持物不被抢，入包后普通丢弃；Shift/context29/GetItem只登记真实Dirt来源，原QuickStack库存9999→0/箱1→10000，已有Wood7/1不变 |
| 研究/缓存 | 全部原版正cap规范化集合逐项核；无效ID拒绝；100次查询及虚拟已满提交不改tracker/editid、不耗研究物；同一脱树窗口停R后原生3/3只见真实研究，再启恢复虚拟目录 |
| 时间/天气 | 原时刻命令；实际Main.time按+1/+24推进、冻结不推进；原rain倒计时600/599正反，实际风雨强度及UpdateWeather风冻结/恢复 |
| 其它力量 | 原生成判定默认/高/零；Main有效难度/专家大师规则及原NPC生命伤害；原ResetEffects范围和GetItemGrabRange；真实Player.Update→Hurt无敌/普通伤害正反；原WorldGen感染许可正反；收窗后效果仍生效，关闭还原 |
| 共存 | Active收窗原钓鱼借还/一次重抛及无关丢弃；原提炼产物、回执和归还；开窗hover只拒新动作，收窗恢复；独立HUD保持实际输出；真暂停继续阻新业务 |
| 保存/读回 | 人物/世界0、1、2、原生3/3的Active保存/还原；原版Active改名；原serializer异常不泄漏SaveView；A旧线程已冻结→关闭→同对象新基线B启用→旧保存仍写A、新会话保留B；player-only reset归还world；FullReset真实菜单关闭异常命中后旧基线退休、后续End不回写 |
| 强制终止 | 最后只强杀父进程实际持有的自有writer PID15404，保存时Active，无退出清理；fresh无R原版reader九份manifest全部通过，原身份/真实研究/持续力量基线及新增物品保留 |

完整专项的九份manifest：vertical、mode0/1/2/3、active-rename、interleave、player-reset、killed。三份独立reader日志分别记录纵向、正常完整矩阵和强杀后九份读取；`killed-writer.log`、`kill-ready.txt`保留原检查点。实际 `Player.LoadPlayer` 必须StatusID.Ok；世界原GetAllMetadata及原LoadFromWorld核基础模式与力量段。

最终执行身份（SHA-256）：

- Terraria.exe：`960A03BFF6050CF7BE16DFC1A7B19E10FC2C4F8F835A6A3B135A50DD9E6BA2F3`，固定1.4.5.8。
- Host：`7D9971BF890667611A59DB49920749B36F1343B514107742DE8D2C0F4154F928`。
- probe：`AFE0B0CD293F79A394C2AF680F6F2E308C4F508FF1632E1525400255F19BC6B6`。
- NativeJourneyChecks.cs：`9ADDDF0E321280BB317D74F0C75CA563C93371BAD58D30FB3044B1519D5FD2BB`。
- NativeJourneyUiChecks.cs：`F17C40D344953EACF4A9E8886A615DCCC981DA97D61E9F7C35864B52123D1BA4`。

该目录 `input-hashes.json`另列Platform/Features/Infrastructure和Harmony实际依赖身份。真实Content为 `F:/games/gameplats/steam/steamapps/common/Terraria/Content`，只读实际XNB；没有声明全Content目录逐文件哈希。三图 `journey-f5-row.png`、`journey-native-menu.png`、`journey-coexist-information.png`已实看。用户报告的行距问题仅移除两处重复6px，最终旅行行与相邻标准行一致，控件和内容底边完整。

## 其它必要检查与复用

| 检查 | 原件与适用性 |
| --- | --- |
| Debug生产/结构 | `build-save-admission-20261018.log`，0警告/错误、结构0fail；正式候选固定后由同入口刷新身份。编译通过不单独代业务验收 |
| UI/绑定CPU | `ui-cpu-gap-build/{build.log,f5-cpu.log,execution.json}`，现有检查全PASS，142个实际Compile输入及4个运行依赖身份；EXE `209E46475D86441B1A4C21ADF035F2EB58A7AE8F091AB6E83A24F548845C6EC7`。此后这些CPU输入未改；未因轻量行距修改新增专门测试 |
| OFF保存准入CPU | `native-validation/cpu-admission-first/`：Locked/OFF100、真实Player/World在途首次启用副作用前拒门；内层原serializer退出后外层仍拒门；两个原异常完整保留，Finalizer后恢复准入；正常原版保存及fresh无R读回通过。Host与最终专项相同；之后测试只改Active/图形部分，CPU相关实现/断言未变 |
| 预测接缝 | `prediction-second-20261018-0605/`：真实世界子流及GameMode前置字段→fresh decoder，null/x0.5/x1/x2/x3、原Main.Difficulty与新Zombie生命45/22/45/90/135；稳定前提保留、倍率变化旧未来失效。四生产文件及实际预测测试未变，`prediction-reuse-20261018.json`保留准确字节映射 |
| 工具路由/reader | `route-checks-20261018.log`，准确旅行路径/CPU选组/显式GPU、未知可执行路径仍unknown；`entry-contract-01.log`及最终刷新记录核8项错误身份/未解决失败/发布越级拒绝。完整正向准入仅在真实独审/主调度接受后的正常包入口执行，未伪造成已获批准 |

以上简写路径均在 `.local/journey-menu-20261018/`。早期 `killcheck-first` 独立强杀、原非Active保存/reader等原件保留，最终完整专项已实际重走关键链，不以旧PASS代替新增保存机制。

## 有限成本

最终full26无临时FirstChance/PowerTrace/StorageTrace。首次成功开启4.2226ms，前面真实失败注入已预热共享路，不宣称全冷启动。三段各100帧的registry revision均63、未重注册：

| 状态 | 平均ms | 最慢ms |
| --- | ---: | ---: |
| Active开窗 | 0.224835 | 0.735 |
| Active收窗 | 0.110553 | 0.3644 |
| OFF关闭后 | 0.119058 | 0.2525 |

范围含探针反射、Host输入/runtime/layout和原CreativeUI.Draw，F5关闭；不是完整游戏FPS或纯R增量。CPU OFF100为0.5177ms，含测试反射，核research/baseline仍null及原字段/研究不变。稳定查询不刷tracker，目录/绑定/布局复用；保存只有固定小子流/有界视图，OFF不做力量快照、恢复文件或每Tick I/O。

## 原始失败与处置

所有RED保留原日志和原输入身份，不把已修测试前提回填到旧记录。

| 原件范围 | 原因、最终处置 |
| --- | --- |
| cpu-first/second | 安装失败又被cleanup遮盖；保留准确原目标/异常、分别清本owner；闭泛型DrawSelf的Harmony限制改准确非泛型原DrawSlot接点，最终真实输入通过 |
| cpu-third—reader-tenth | 原world Manifest/Bestiary、reader beforefieldinit/Player0、原LoadPlayer吞PlayerFrame无GPU异常；按原版初始化/真实headless分支补前提，原LoadPlayer成功期待保留 |
| prediction首轮 | fixture漏wire单写GameMode前置字段；按实际包络补，未改正常消费者期待，second通过 |
| full-first、resume03/18 | 会话/实际Reach支持发生变化；32位原生枚举留证，仅外部前提恢复后续跑，未更改系统/驱动/profile |
| resume01—08 | 原导航/recipe与排序/完整item资产handle、slot背景和Coin域启动缺项；使用真实原统一阶段及XNB，未逐ID伪造字典/资源；raw ID转规范type/Deprecated转air的域断言纠正 |
| resume09—15 | 旧UI映射造成首帧未悬停、nullable诊断误读、原QuickStack缺ItemSorting统一初始化；真实中性移动手势/准确观察/原SetupWhiteLists后正常取物与守恒通过 |
| resume16—19 | 原NetManager缺完整模块导致原命令静默丢弃；原NetworkInitializer.Load后原loopback生效；移除场景重复TileObjectData.Initialize，复用已锁定原启动数据 |
| resume20—24 | 天气调用后半无关完整事件/Pylon缺项；非改流收据确证后按有限消费者边界停止；真实surface>50前提补齐，保留原雨/时间全部条件与正反期待，未补stub或跳消费者 |
| resume24/25 | 原Player.Update成就定义未注册、测试new Player缺原Deserialize先建的SavedData；完整原AchievementInitializer（SocialAPI空、owned本地路径先断言）和准确原输入对象补齐，最终全链通过 |
| 生产自查修复 | 脱树目录失效漏项、player-only reset恢复world、FullReset关闭异常仍留旧baseline、OFF已进入保存后首次激活的竞态均有精确修复与最终原版反例。OFF门仅拒未持不可变基线的在途serializer，不封安全A/B或关闭 |

唯一整合者拥有Feature/Host Journey、Runtime组装、保存/来源/预测接缝、脚本和正式文档；UI作者拥有F5五文件/Hotkeys两文件和三项专用测试；原版研究者拥有NativeJourneyChecks。完整差异39个授权文件，既有本地参考说明、.workbuddy、issue112.txt、pr113.json明确排除。关键方法头/直接调用者及相邻旧注释已集中自查，临时定位观察已退出，不另建注释台账。

## 保留的证据边界

独立reader用原版dedServ绘图分支，真实LoadPlayer/Deserialize及世界原元数据/力量段读取均执行，并在前后断言未加载JueMingR程序集；不宣称普通客户端完整LoadWorld启动。雨/时间在原Main.UpdateTime首个后继事件入口受控停止，感染在原Wiring入口停止，均保留所验原消费者，finally撤回；不声称整世界事件/地图感染长期模拟。

自动/退出保存共同链以锁定原源码追到本轮实跑的实际serializer，未宣称真人点击退出或等待完整自动保存周期。工程物理样本/foreground delegate驱动真实组件不等于真实OS键鼠、AltTab、中文IME；不冒称真人多人、真实物理掉电、全部硬件或量化FPS。所有者体验、完整包正常加载/独审和一次安装分别核验，本文件不提前授予它们。

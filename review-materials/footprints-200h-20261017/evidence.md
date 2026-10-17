# 足迹200小时候选：工程证据

任务与权限见 [#118](https://github.com/Kuroneko2020/JueMingR/issues/118)，对应 [PR #119](https://github.com/Kuroneko2020/JueMingR/pull/119)，BASE为已接受main `f50646b083b27248731dbf7afdc53a3876ff4309`。本页保留本批原工程证据边界，固定源码与最终准入见Issue；不把旧 #87 接受、已有完整包或编译代证新体验。

所有者2026-10-18已实测足迹功能与外观无问题，性能暂接受但未量化 FPS。随后授权的注释定点维护、现有同步责任补充和左下角战斗文字撤除及收尾见[本次短证据](../comment-hud-closeout-20261018/evidence.md)；不重跑本页200小时数据、不打包或换装，原包/现装身份 `81057c6` 保留，本页原失败、测试和安装回执不改写。

本轮采用现实游玩TimeSpan ticks，记录SED误差0.125 tile；显示层DP误差0.375屏幕像素、重复覆盖端点量化0.25像素，总显示误差另加记录误差投影。绿线保持LimeGreen/地图Alpha，居中2屏幕像素，1×/0.25×真实XNB像素检查成立。旧格式未接入，新根footprints-v2；旧文件、已有偏好和其它功能数据保留。

## 实际执行

| 检查 | 已取得的限定结果 |
| --- | --- |
| 普通Debug编译/结构 | 六生产项目及架构程序集，0警告/0错误；新增公开DTO/Index精确登记 |
| 足迹核心/真实隔离文件/worker | 固定时间区间、紧凑直线/慢移/停留、分页、保存期限、背压、重入、严格身份/校验、未知提交、清除/链接/相邻哨兵与读取保护通过 |
| 独立总览组件 | 48,000混合区间/200h/187块，早中晚空间、8192以上全史、穿屏/LOD/微闭环、过去游标、增量、坏派生缓存/坏正文、关闭/重开及预算通过 |
| 实际43,200,000观察/200h | 60Hz等效流式混合长直线、圆弧、频繁折返、重复基地路线、停留、极慢漂移和每10分钟断段；10秒保存检查点72,000批。保存732,246区间，另独立重放全部43.2m观察，最大SED=0.1213443724 tile |
| 实际原版Host CPU | 锁定EXE身份；30/60/120Hz一现实分钟、同刻多回调、换算余数、原版暂停/地图/准备/菜单门，真实Hook边界、失败/跳过、开关、物理输入、隔离保存/重入/清除及旧格式根哨兵不变通过 |
| 实际Host/XNB地图 | 同一73.2万区间档案由正式Store/Host/MapLayer打开，真实总览/局部平移缩放/100h游标/关闭重开/Store重入；源覆盖完整，无未来边，稳定图不重投旧几何；配置/确认/紧凑窗口及1px参考与2px实际PNG已目视 |
| 共享链 | 完整ContinuousProcessing上下文中的实际战斗路径Source/Draw与足迹录制Hook同开，世界→地图足迹→世界路径恢复通过；未使用武器，未扩战斗矩阵 |

43.2m离线Release录制+写入63.706s，重入3.305s，独立误差重放8.407s，首次总览23.658s、同一已准备总览重复1.886ms；源及派生136,119,597 bytes，几何缓存8,381,964 bytes、空间元数据250,048 bytes。几何/视口选边缓存分别8MiB、空间元数据16MiB预算，满元数据改编号流查保全史。

实际Host/XNB同档首准备8.904s（已有派生页、新worker校验源）、局部1.369s、100h历史4.312s、重开2.414s、重入含准备11.733s。40次稳定DrawRoutes CPU均4.493ms，最终19,058边；不是整个游戏帧时或FPS。修后20次串行+1s历史Seek的端到端准备均296.6ms/max392.9ms（含5ms等待粒度），DrawRoutes均4.745ms/max6.471ms，约15,570边。这组不能代证连续Playback调度。

## 失败原件与修正依据

首次编译结构门拒绝未登记的新公开DTO/Index，随后按实际合同补精确名单；没有移除结构门。旧Playback/201h/逐微移期待按本轮现实时间与压缩合同改写，保留相邻安全断言。私有长输出目录触发net472路径上限的查询测试夹具已改为唯一短TEMP根。

第一份完整43.2m输入生成5,528,138区间，未通过规模压缩检查；根因是慢移float量化平台被单个原地样本立即拆成Stay/Move。移动段的同端点延长也经SED走廊判断，持续停留仍生成Stay；同源第二份732,246区间，独立全样本误差核实通过。第一份失败与数据保留。

InvalidDataException在.NET Framework不继承IOException，已显式纳入worker保护及坏派生缓存重建异常分类；真实源坏不借缓存洗成完整。晚到UI取消已退休worker不再触发已释放wake句柄。

历史准备323ms超过250ms替换周期暴露真正连续回放风险；已窄修为同视图向前保留在途准备，新partial不替换安全旧完整历史，完整后原子替换；回退/换视图立即换请求。真实Playback.Advance+Layer.Update+worker+Draw的30Hz/60Hz各10秒窗口通过：分别300/600帧、37/39次完整发布、观察到261/540次后台partial但展示历史不退回，完整Until持续前进、早区/中段区域不消失且无未来边。DrawRoutes CPU均4.290/4.115ms，max6.511/7.680ms；原始逐帧CSV保留，不用串行Seek代证连续播放。

原始日志、完整合成档案及PNG留本地 `.local/footprints-200h-20261017/`，不上传原版资产、用户数据或大测试集。受控原版进程在初始化前音乐/音效/环境声全静音，不修改所有者或系统音量；本轮未启动/操作所有者游戏。

正式WinPS5正向包门首次拒绝合法范围：包reader原为UTF8无BOM，中文白名单按ANSI读成乱码；仅补前三字节BOM，余下字节完全相同。负向八守卫、实际中文解码和同环境身份经窄独审确认，保留拒绝原件。最初pwsh7辅助身份侧记与正式WinPS5的中文排序/环境不同，未互相改署；最终门全程使用正式WinPS5锁。

## 交付边界

产品链及必要专项已接通。固定全审候选`36a3daf7187f5a5aebca67bef0b0a350a3e8cb62`通过独立Sol/High全范围审查；最终仅编码修正候选`81057c6accf240b9c12f2946c51fbc16e627eea8`通过窄补审查，主调度更新READY_FOR_PACKAGE。两份报告SHA256分别为`C300918CAE0725660BE0B31059C4FBA06508EE586DAA1D0F69B40E0E56D56276`、`3E353B90B9AEBA4D98A0D7E04895D6DD809FACC6D82E248B86754997DB140F14`；原执行、失败和原包身份均保留。

正式WinPS5最终source锁`1F39B0A2C0134DE464D7DB1C4B789787100151F8EE6EE2F6151CC8B7E20AC171`，input锁`CFADB3ECCE8E393469D9397B663B80B97F26AD250BCF660378D6EE668E82F5C6`。真实正向stage reader通过后，现有完整ContinuousProcessing Release/包入口一次成功，输出`outputs/footprints-200h-20261017-81057c6/`。ZIP为`JueMingR-Continuous-Processing-81057c6accf240b9c12f2946c51fbc16e627eea8.zip`，2,279,760 bytes，SHA256：`8FBD3EAD5A228DB7BE2DF5C8E7E5CF08D98A9BB3142C49C5642CBED0BA80455B`。受控产品源码已提交，外来工作区脏资产按声明保留；资格仍为STAGE_DEVELOPMENT/INCOMPLETE/releaseEligible=false。

按08完成唯一轻量换装窗口：现场回执确认旧来源`f399f761552b7caf5d62cc0e43c9fc1df8eeb0bd`及目标`F:\games\gameplats\steam\steamapps\common\Terraria`，无游戏/预测worker进程；只备份10静态payload及安装回执、归属日志共12文件至`.local/footprints-200h-20261017/installed-backup-f399f76-before-81057c6/`。旧原包正规Restore返回RESTORE_COMPLETE，新完整包正规Install返回INSTALL_COMPLETE；一次读回回执与新包逐字节相同、10载荷长度符合清单，复用安装器的哈希/归属验证。现装来源为`81057c6`，备份及`installation-result.json`保留。不遍历用户数据，不由安装器创建新记录根，不做装后全套重测、回装演练或第二套包消费者。

本轮工程验证及安装完成不是所有者日常正常加载、体验接受、多人或量化FPS；这些仍未执行。Draft PR和Issue保持开放，不merge/close/Tag/Release/推进产品版本。本页随后纯文档提交或未来squash SHA不重写原包/安装身份，也不触发第二次构建安装。

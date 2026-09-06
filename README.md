# JueMingR

JueMingR 是 JueMingZ 的独立完整重写项目。

Legacy 项目 JueMingZ 永久冻结，仅作为只读参考。JueMingR 与 JueMingZ 将长期双轨并存。

项目最终目标是完整覆盖旧版中对用户有意义的功能，但不保留历史错误实现和技术债。

已建立最小项目骨架和可重复构建基线，在 Terraria 1.4.5.8 上完成了最小原版加载链、“群系显示”和可交互的 F5 UI 基础。F5 包含十二个分类入口、窗口拖动、滚动与输入隔离，只有“群系显示”接入真实业务；十二个入口不代表十二类旧功能已经迁移。

项目所有者已接受 F5 基础作为后续开发起点，外观可以继续迭代。尚未实现完整 Runtime、完整 Legacy UI 与功能迁移或正式安装器，未形成多人验证或正式发行。基础接受范围见 [F5 UI 基础设计](docs/设计/F5控制界面样板与UI基础.md)，当前阶段与任务见 [稳定起步总跟踪](https://github.com/Kuroneko2020/JueMingR/issues/7)。

[配置持久化合同](docs/设计/配置持久化与用户数据布局.md)规定：在已验证 `Terraria.exe` 同级的 `JueMingRData/config/` 下，用 `ui.json` 保存 F5 主窗口位置，用 `features/biome-display.json` 保存真实群系显示的用户期望开关。同一游戏安装内跨角色和世界共享偏好；没有 AppData/Documents fallback，不自动导入 Legacy 数据。该功能及正常保存静默、必要异常提醒已独立获项目所有者接受，随 [PR #35](https://github.com/Kuroneko2020/JueMingR/pull/35) 合并进入 `main` 后成为当前主线能力；接受仅覆盖本任务与所有者实际测试场景，不扩大为全部环境、分辨率、材质包、多人或 FPS 验收。

`JueMingRData` 属于用户数据，安装恢复和卸载默认保留。配置损坏、版本不支持或外部冲突时保留原件并停止该文档写回；重置单项配置不删除其它用户内容。后续笔记、蓝图、足迹等分区建议及其未实施边界也保存在上述设计中。

正式文档入口见 [`docs/README.md`](docs/README.md)。

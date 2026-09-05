# JueMingR

JueMingR 是 JueMingZ 的独立完整重写项目。

Legacy 项目 JueMingZ 永久冻结，仅作为只读参考。JueMingR 与 JueMingZ 将长期双轨并存。

项目最终目标是完整覆盖旧版中对用户有意义的功能，但不保留历史错误实现和技术债。

已建立最小项目骨架和可重复构建基线，在 Terraria 1.4.5.8 上完成了最小原版加载链、“群系显示”和可交互的 F5 UI 基础。F5 包含十二个分类入口、窗口拖动、滚动与输入隔离，只有“群系显示”接入真实业务；十二个入口不代表十二类旧功能已经迁移。

项目所有者已接受当前 F5 基础作为后续开发起点，随本次合并进入 main 后成为当前基线；外观可以继续迭代。尚未实现完整 Runtime、完整 Legacy UI 与功能迁移、配置持久化或正式安装器，未形成多人验证或正式发行。接受范围和后续 UI 契约见 [F5 UI 基础设计](docs/设计/F5控制界面样板与UI基础.md)，当前阶段与任务见 [稳定起步总跟踪](https://github.com/Kuroneko2020/JueMingR/issues/7)。

正式文档入口见 [`docs/README.md`](docs/README.md)。

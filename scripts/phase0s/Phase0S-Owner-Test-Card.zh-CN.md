# Phase 0-S 一次性最小实机诊断卡

这不是 V3 修复版，也不是正式发布、Phase 0-S 成功或未来加载设计。它只诊断当前唯一目标 `Terraria.Main.Initialize()` 上为何已有 `HOOK_INSTALLED` 却没有正式 postfix evidence。

Agent 只负责核验诊断包、安装、复制并分析 evidence、以及用同一精确匹配包恢复。Agent 不会启动、关闭或操作 Terraria；游戏内动作仍由项目所有者亲自完成。

## Agent 安装停止门

Agent 只会在以下条件全部成立后安装：

- Terraria 进程为 0；
- `Terraria.exe` 精确身份与 SHA-256 匹配；
- 包来自当前 clean commit，manifest、payload 与 ZIP 身份匹配；
- `Terraria.exe.config`、根目录 Bootstrap、sidecar、staging/temp 均无冲突；
- 隔离 fixture、安装/恢复、Debug、Release、架构和可重复构建门禁通过；
- 唯一一次定向只读复核通过。

安装脚本只应输出一行 JSON。只有 `exitCode` 为 `0` 且 `code` 为 `INSTALL_COMPLETE` 时，Agent 才会明确通知项目所有者进入启动步骤；其它结果立即停止，不覆盖、不合并、不改名、不删除现有文件，也不反复重试。

## 项目所有者只做一次

收到 Agent 明确的“诊断包已安装，可以启动 Terraria”后：

1. 通过平常方式亲自启动 Terraria。
2. 进入主菜单；若可正常操作，再进入一个普通存档。
3. 正常退出 Terraria，并确认游戏进程已经结束。
4. 回传：“诊断测试完成，Terraria 已退出”。若无法启动或无法正常退出，只说明实际现象，不要修改、复制或删除诊断文件，也不要重复启动。

不需要自行查看或解释日志，不需要手工运行安装/恢复脚本，也不要改用 `LoadContent`、`Update`、`Draw`、`DoUpdate` 或其它 Hook。

## Agent 在退出后处理

确认 Terraria 进程为 0 后，Agent 才会：

1. 复制 `JueMingR.Validation\phase-0-s-diagnostic.sentinel` 与 `phase-0-s-evidence.log`（若存在）到真实 Terraria 目录之外；
2. 核对同一 package id，分析 Patch 开始/返回、metadata、prefix、postfix、gate/context 与正式 evidence 写入；
3. 使用同一精确匹配包运行 `Restore-Phase0S.ps1`；
4. 核验 `Terraria.exe.config`、`JueMingR.Bootstrap.dll`、`JueMingR.Validation` 已精确移除，且 `Terraria.exe` 未改变；
5. 只给出 A/B/C/D/E 中的一项诊断结论并停止。

恢复正常且文件身份无异常时，不要求项目所有者额外启动纯原版 Terraria。若恢复脚本失败或文件身份异常，Agent 会保留现场并停止，不会强制删除或递归清理。

## 两类本地证据

- 正式 evidence 仍是原有 `01` 至 `05` 五事件合同；本次诊断不降低或替代该成功判据。
- 独立 sentinel 只允许五类诊断事件：`RELOGIC_ASSEMBLY_LOAD_OBSERVED`、`PATCH_BEGIN`、`PATCH_RETURNED`、`MAIN_INITIALIZE_ENTRY_OBSERVED`、`POSTFIX_ENTRY`。状态字段只用于区分 metadata、gate/context 与正式 evidence 写入结果。

sentinel 是有界、无 BOM、无绝对路径、无用户数据的一次性文件。package manifest schema 2 只因增加了该文件的固定相对路径声明；这不是产品版本、修复版本或配置迁移。恢复脚本只在 package id、格式与 manifest 声明都匹配时删除它。

即使本次得到完整正式 `01` 至 `05`，也不能把结果直接算作 Phase 0-S 成功，因为临时 prefix 和额外观测改变了 Patch 形态。本次不验证多人、性能、玩家功能、广泛平台兼容或未来长期 Hook。

# Phase 0-S V4 项目所有者实机测试卡

这不是正式发布，也不决定未来完整 Runtime 的最终 Tick Hook。V4 保持 Terraria 1.4.5.8 中唯一 `Terraria.Main.Update(Microsoft.Xna.Framework.GameTime)` postfix，只验证 Harmony Patch 从 `AssemblyLoad` 回调内部移到一个无延迟、无重试、只执行一次的回调外 BCL 工作项后，第一次实际命中能否完成一次空 Runtime 交接。该安装时序仍只是候选根因，不能在本次实机前写成已确认根因。

Agent 负责核验包、安装、复制并分析 evidence，以及使用同一精确匹配包恢复。Agent 不会启动、关闭或自动操作 Terraria。

## Agent 安装停止门

Agent 只会在以下条件全部成立后安装：

- Terraria 进程为 0；
- `Terraria.exe` 精确身份与 SHA-256 匹配；
- 包来自已审查的 clean commit，manifest、payload 与 ZIP 身份匹配；
- `Terraria.exe.config`、根目录 Bootstrap、sidecar、staging/temp 均无冲突；
- Debug、Release、架构、fixture、安装/恢复、双 worktree 和双 ZIP 门禁通过；
- 唯一一次定向独立审查结论为 A。

安装脚本只应输出一行 JSON。只有 `exitCode` 为 `0` 且 `code` 为 `INSTALL_COMPLETE` 时才进入实机步骤；其它结果立即停止，不覆盖、不手工补文件、不重试。

## 项目所有者只做一次

收到 Agent 明确的“V4 已安装”通知后：

1. 通过平常方式亲自启动 Terraria。
2. 进入主菜单。
3. 进入一个普通存档。
4. 正常退出 Terraria，并确认游戏进程已经结束。
5. 回复：“V4 测试完成，Terraria 已退出”。

若无法启动、无法进入主菜单或存档、或无法正常退出，只说明实际现象；不要修改、复制或删除验证文件，也不要重复启动。项目所有者不需要查看日志，也不需要手工运行安装或恢复脚本。

## Agent 在收到回复后处理

确认 Terraria 进程为 0 后，Agent 才会：

1. 将 `JueMingR.Validation\phase-0-s-evidence.log`（若存在）复制到真实游戏目录之外；
2. 核对 V4 package id、严格的 `01` 至 `05` 及 managed thread id；
3. 使用同一 V4 包运行 `Restore-Phase0S.ps1`；
4. 核验 `Terraria.exe.config`、`JueMingR.Bootstrap.dll` 和 `JueMingR.Validation` 已精确移除；
5. 核验 `Terraria.exe` SHA-256 未变化。

项目所有者已明确豁免恢复后的纯原版再次启动。若恢复失败或文件身份异常，Agent 会保留现场并停止，不强制删除或递归清理。

## 成功判据与边界

同一 package id 必须严格、有序、各一次地出现：

1. `TERRARIA_ASSEMBLY_READY`
2. `HARMONY_READY`
3. `HOOK_INSTALLED`
4. `MAIN_UPDATE_POSTFIX_FIRED`
5. `RUNTIME_HANDOFF_COMPLETE`

还必须同时满足：02/03 来自同一个唯一安装工作项线程，04/05 来自实际 Update 调用线程；安装为 `INSTALL_COMPLETE`；主菜单和普通存档可进入；`ERROR` 为 0；恢复为 `RESTORE_COMPLETE` 或 `RESTORE_NOOP`；`Terraria.exe` 未变化；项目所有者明确接受。02/03 与 04/05 不要求是同一线程。

自动测试、Patch metadata、前三项事件、游戏窗口出现或只进入主菜单都不能单独算成功。V4 包不包含一次性 diagnostic sentinel，也不包含 Terraria、ReLogic 或 XNA 文件；本卡不证明多人、FPS、广泛平台兼容、玩家功能或未来长期 Hook。

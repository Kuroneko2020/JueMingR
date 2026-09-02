# Phase 0-S 项目所有者实机测试卡

这不是正式发布，只验证 Terraria 1.4.5.8 的一次最小加载交接。请由你亲自运行脚本、启动 Terraria、观察和恢复；Agent 不会操作你的 Terraria。

## 测试前

1. 确认 Terraria 已关闭。
2. 保留完整验证包，不要手工复制、拆分或重排包内文件。
3. 从 `phase-0-s-build-record.json` 核对你收到的 ZIP 文件名和 SHA-256；把验证包解压到 Terraria 目录以外。
4. 准备自己的 Terraria 安装目录路径。若目录中已有 `Terraria.exe.config`，安装脚本会立即停止且不留下任何新文件；不要为了继续测试而让脚本覆盖、合并、改名或备份它。

## 由你亲自执行

在解压后的验证包目录打开 PowerShell，运行：

```powershell
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File .\Install-Phase0S.ps1 -TerrariaDirectory "<你的 Terraria 目录>"
```

脚本只应输出一行 JSON。只有 `exitCode` 为 `0` 且 `code` 为 `INSTALL_COMPLETE` 时才继续。其它结果都立即停止：不要手工复制、覆盖、改名、删除文件，也不要反复重试；保存这一行 JSON 并交给 Agent。

安装成功后：

1. 通过平常方式亲自启动 Terraria。
2. 只观察能否进入主菜单；本次不测试功能、多人或 FPS。
3. 无论能否进入主菜单，都关闭 Terraria，确认进程已经退出。
4. 在恢复前，把 Terraria 目录中的 `JueMingR.Validation\phase-0-s-evidence.log` 复制到 Terraria 目录外的自选位置。若文件不存在，也不要重试或更换 Hook；记录“evidence 缺失”并继续恢复。
5. 不需要自行解释 evidence；把复制出的文件、安装 JSON、验证包 manifest 和 ZIP SHA-256 一并交给 Agent。

仍在同一个验证包目录运行恢复：

```powershell
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File .\Restore-Phase0S.ps1 -TerrariaDirectory "<同一 Terraria 目录>"
```

只有 `exitCode` 为 `0`，且 `code` 为 `RESTORE_COMPLETE` 或 `RESTORE_NOOP`，才表示脚本恢复步骤完成。其它结果请保留现场并把唯一一行 JSON 交给 Agent；不要强制删除或递归清理。

恢复完成后，由你亲自再次按平常方式启动原版 Terraria，确认能否进入主菜单，然后正常退出。

## 需要回传和裁决

请分开记录：

- 安装脚本的唯一 JSON 行；
- 首次启动是否进入主菜单；
- 复制出的 evidence 文件，或“evidence 缺失”；
- 恢复脚本的唯一 JSON 行；
- 恢复后原版 Terraria 是否进入主菜单；
- 你对本次实机结果是否接受：接受 / 不接受 / 需要继续判断。

自动测试、构建成功、安装脚本成功、游戏窗口出现或进入主菜单，都不等于最小交接成功。只有 Agent 核对同一 package id 的 `01` 至 `05` 五项事件完整且顺序正确，才算 Phase 0-S 最小加载交接成功。若第 `04` 或第 `05` 项缺失，保留证据并停止；不要重试，也不要改用 `LoadContent`、`Update`、`Draw`、`DoUpdate` 或其它 Hook。

本卡不能证明 Steam/GOG 的广泛兼容、Windows 10/11 全覆盖、多人、性能、任何玩家功能、未来长期 Hook 或正式发行布局。

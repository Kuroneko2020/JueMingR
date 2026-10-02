# 本次 NPC 路径详细采集卡

此包用于收集“路径和提示文字同时消失”的原因。照常玩即可；采集会增加 CPU、内存和磁盘开销。最长连续时间线 30 分钟，详细资料保留最近约 3 分钟；标记或异常会保留前窗并继续保留后 3 分钟。所有 Host 与 worker 合计累计写入上限 8 GiB，主进程详细排队上限 32 MiB。达到容量或写入失败会停止采集并留下缺测说明；不能保证任何场景都采满 30 分钟。

1. 完全退出 Terraria，按本次交付说明用现有 `Install-Phase0S.ps1` 安装诊断包。将下面 `$game` 改为实际含 `Terraria.exe` 的目录，执行一次：

   ```powershell
   $game = '实际 Terraria 游戏目录'
   .\Aim-Diagnostics-Control.ps1 -Action Arm -GameDirectory $game
   ```

2. 正常启动、选择原角色和世界、打开原来的 NPC 路径选项，正常玩。可以依次尝试原本正常和容易出现问题的场景；无需改配置、重进世界或维持无敌。
3. 路径和文字一起消失时，尽量马上执行以下命令，加一句当前情况。命令只加时间标记，不会重试或重启预测。继续玩约 3 分钟以保留后续变化。

   ```powershell
   .\Aim-Diagnostics-Control.ps1 -Action Mark -GameDirectory $game -Note '路径和文字都消失；当前场景说明'
   ```

4. 收集结束后执行停止。等命令明确提示 Host 和当前仍运行 worker 的完成清单已落盘后再正常退出游戏；若提示仍在收尾，等待该会话 Host 与仍运行 worker 的 manifest.tsv 出现后再退出。把整个 `JueMingRData\logs\aim-diagnostics\本次会话目录` 交回，保留其中 Host、各 worker、时间线和明细。不要只挑日志或删除无完成清单的 worker。

   ```powershell
   .\Aim-Diagnostics-Control.ps1 -Action Stop -GameDirectory $game
   ```

资料含角色/世界名称、必要身份摘要和状态，分享前可先告知需要隐藏的名称。采集不读取私人存档正文。离线分析入口为 `-Action Analyze`，可以用 `-PythonPath` 指定已有 Python 3；无须安装新依赖。

退出后由本次交付代理先校验资料，再用保留的普通包按既有 Restore/Install 流程恢复并核对实际载荷。新偏好、足迹、用户数据均保留。源码关闭或删除 arm 令牌不等于实际安装已经恢复；恢复后下一次普通启动不创建此采集线程。

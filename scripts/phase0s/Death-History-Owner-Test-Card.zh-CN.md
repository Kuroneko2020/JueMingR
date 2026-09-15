# 死亡记录与世界天数体验卡

这是包含此前功能的完整开发包。请按平常方式启动游戏；本包不会替你开启死亡点、改绑定或导入旧记录。

- F5 → 地图：查看“死亡信息”“世界天数”；“详情”窗口按条数收紧，默认显示“死于飞鱼”“死于摔落”等直接死因，点击原因查看原版完整原句，返回保留当前页。时间是死亡当时的本地时间，不显示时区尾缀。旧记录和无法可靠分类的原因继续显示原句。
- 自然游玩发生一次死亡后，查看次数、详情与大地图的位置是否对应。相同地点的再次死亡仍是独立记录。
- “死亡点常驻”默认关闭。开启后只在大地图显示；“配置”可选 128 / 256 / 512 / 1024，默认 256。这里指最近具有有效位置的死亡，屏外点也占数量；调低不删历史。选择后关闭窗口不会撤销，正常重启可查看是否保留。
- 世界天数从安装后观察累计的 0 开始，包含睡觉等时间加速，满一个游戏日才增加整数；直接设时与离线时间不计入。
- 有长原因或超过六条记录时，可顺便体验全文和翻页。方便时再试普通客机；无需专门制造异常、解除帧率限制或做技术计量。

反馈看到的问题和当时操作即可。没有相应自然场景可暂时跳过。此开发包等待体验接受，不是正式发布。

## English

This complete development build includes earlier features. Open F5 → Map for the current character/world death count, paginated details, full reason text, and observed world days. Persistent death markers are off by default and appear only on the fullscreen map. Choose the latest 128, 256, 512, or 1024 valid positions (default 256); lowering the limit never deletes history. Effective game-time acceleration counts; direct clock changes and offline time do not. Please report issues during ordinary play; multiplayer acceptance is still pending.

The compact details window shows a direct cause by default; click it to read the complete original death message. Recorded local time keeps the offset from the moment of death without displaying the suffix. Older records and unclassified custom causes retain their original message.

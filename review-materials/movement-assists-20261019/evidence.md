# 移动辅助候选证据入口

任务 [#126](https://github.com/Kuroneko2020/JueMingR/issues/126)，Draft [#127](https://github.com/Kuroneko2020/JueMingR/pull/127)。整批 BASE `749e86925763f8a5754d6999ea4056b50de58d65`；最终候选 SHA、累计执行/复用回执、独审和包/安装状态以 Issue 对应收口评论为准。本文件是实现者自查和原件导航，不签独审或发布资格。

## 实现和证据边界

三项默认关、冲刺默认双击，enabled/mode独立持久化；共享真实输入/Session owner、实际原版消费者、普通完整 ContinuousProcessing 组合及三标准 F5 行。正式行为和实现分别见[合同](../../docs/功能介绍/移动辅助行为合同.md)与[设计](../../docs/设计/移动辅助实现设计.md)。未推进产品版本；跨进程预测协议39/诊断139是实际 AssistedJump 序列化字段变化，旧端拒绝，不是产品版本。

本地原件根 `.local/movement-assists-20261019/`，所有隔离宿主静音，使用自有可丢弃世界/玩家/保存目录；没有操作所有者游戏、真实存档、系统/RDP/驱动/音量。最终图形完整原件 `original-07-solar-earned.log` 与同名目录；`native-inputs.json` 绑定原版/Host/Probe，`source-inputs.json`、`source-build-record.json` 保留执行时源码和准备身份。Host SHA256 `199ED58B399DFFF2C4CF3BCAB0B54779E59FD22AF91E19A446B7355848AAC0D5`；执行 Probe（只补测试副本LAA）SHA256 `4635ED3FA4E4F44ED663125CD493CB3DB1A8F435A17F1FF57B11BA6689A017A5`。源码后续窄改须由 Issue 明确受影响重验/复用，不能把07身份改写为最终提交。

## 已实际执行

| 入口 | 原件 | 实际层级 |
| --- | --- | --- |
| 四项 Movement Architecture checks | 早期Debug执行输出 | 默认/16字段组合/严格codec、隔离保存重启与保护文件、手势18/19/2 tick、Jump owner、排序；累计回执补最终身份 |
| MovementDashCpu | `dash-cpu-05.log` | 原版 DashMovement/CommonDash，32能力组合及原生键/冷却/坐骑/异常归还；不冒充Main映射 |
| MovementJumpCpu | `jump-cpu-02.log` | 346 stage案例，9族实际消费，4坐骑原生SetMount/UpdateEffects来源；4×20000 helper测量零新增分配 |
| MovementTeleportCpu | `teleport-cpu-01.log` | 两杖原版Use→ApplyItemTime→局部目标→Teleport/SendData入口/混沌代价；真实缩放/反重力；合法0/近邻10/耗尽2120候选有界及零查询分配 |
| MovementOriginal | `original-07-solar-earned.log` | 真Main输入/world/Player.Update/装备/Jump/Draw；180步前封存预测误差0/3次地跳；5装备族各1次；4坐骑各150更新、各2地跳2额外跳；地面资源回满/蛙腿/远端意图/原异常传播 |
| 同一完整scope中的Dash/UI | 07日志与PNG | 双键最近左右真实映射→原版归零→CommonDash实际速度；克盾真碰撞与太阳自然产盾→碰撞耗一次；F5三行、关闭改模式、100/150%命中和渲染、三公共录键保存/切换反馈、真world文本/录键/模态/指针拒绝及普通F5/背包允许 |
| 工作量路由 | `routing-03.log` | 实际Git变化/混合/删除/缺BASE/未知负例；Movement叶/公共输入存储传播；CPU累计不自动启动图形 |

07截图为 `movement-page-ui-100.png`、`movement-page-ui-150.png`、`movement-mode-off-hold.png`，实现者已逐张查看文字、三行与模式。公共累计尚须以最终 Issue 回执区分本次执行、有效复用和未验。

## 性能原件与限度

07保留完整逐帧CSV：关闭/开启闲置、原版人工/辅助地面与复杂组合，两轮反序；地面各200测量更新且5次地跳，复杂各380测量更新且地跳1/额外5、五族各1/翼142 tick。rocketDelta只是实际字段减少量；无装备35包含原版清理/刷新，不能称燃料消费，翼靴组合7含翼转换；独立靴毯场景才证明实际靴燃料消费。持跳段1→0时原版可先处理1tick翼，下一次JumpMovement才接仍剩余的多段族。

`movement-idle-pairs-settled.csv` 包含8对反转顺序的48更新block，先在测量外等真实设置保存完成、固定日时、暖16；数组预分配且日志/CSV在block外。07 update on-minus-off毫秒为 `+.02098,-.08899,-.08728,-.00176,+.01573,+.01199,+.04976,-.00677`，整链world+Draw同样正负变化。保留均值/中位/p95/max和GC/分配，不丢尖峰、不立统一预算、不宣称字面零耗时或所有机器FPS。微测查询/owner零分配也不等于整Main零分配。最终有限性能判断和分母分析见 Issue。

## 失败、期待和夹具修正

全部旧日志保留。dash02原期待“太阳发动即耗盾”错误：原SolarDashStart只立solarDashing，首次实际NPC碰撞才ConsumeSolarFlare；05仅发动原期待已纠正，07补真实碰撞和第二次不再耗盾，不能互相冒充。dash03修原版ArmorSetBonuses初始化；dash04移除原版DashMovement未保证的direction写入期待，继续核实际速度。

original01夹具将全部角色inactive后等待Session，自阻塞；在等待前建立真实active本机。02/03完成Jump/性能后Mapped失败：新Player落地点未设fallStart导致原版摔死，随后UpdateDead抄原始双键，且专家盾完整Update资格与孤立UpdateEquips不同；改安全落地事实并以Tabi验证普通双键，07再以真实专家资格验证克盾。

04/05初始化/首帧链未完成，原因未定，不签“GPU不可用”。05 stop时快照before但最终log有ready，不能当同一等待点。只读准确XNA发现缺设备/音频异常可被默认模态消息吞掉，隔离ViewGame现override记录原异常并return false、StartLoop后核Initialize/Device，避免空Tick等待；06/07真实初始化成功未采到该异常，不能倒填04/05根因。仅05按精确自有PID/path/start核验后停止并保留stop回执。

06太阳新Player预种Buff在setSolar前被原版删除；07用真实装备/180tick自然产盾，不人工恢复盾。路由01/02仅测试期待遗漏：实际路由原已有view/journey公共传播，新Movement传播使精确断言暴露旧遗漏，补期望集合而未改view/journey生产或扩大到无关业务；原精确正反例保留，03通过。

## 待交付门与现场资产

真实物理焦点/IME/所有者手感、长期FPS、真实多人客户端/权威结果尚未验；SendData入口不是网络同步闭环。独审、正式普通完整Release候选包、受控一次备份换装待对应准入，当前没有新包/安装成功结论。恢复体验入口见[体验卡](../../scripts/phase0s/Movement-Assists-Owner-Test-Card.zh-CN.md)。

四项外来资产保持：tracked `docs/设计/本地参考资料使用说明.md`，untracked `.workbuddy/memory/2026-10-03.md`、`issue112.txt`、`pr113.json`。哈希声明 `.local/movement-assists-20261019/approved-retained-assets.json`，沿现有合法通道：`clean=false/releaseEligible=false`，提交本任务源码后仅这四项保留，不借旧Stage例外，不伪造clean。禁止合并/关闭/Tag/Release/版本推进。

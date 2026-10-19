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

07保留完整逐帧CSV：关闭/开启闲置、原版人工/辅助地面与复杂组合，两轮反序。每地面窗共240更新/5次地跳，复杂窗共420更新/地跳1+额外5、五族各1/翼142 tick；前40为暖场，计时和分配只取后200/380，动作计数含暖场，因此不将尾窗耗时除全窗动作数声称精确每动作成本。rocketDelta只是实际字段减少量；无装备35包含原版清理/刷新，不能称燃料消费，翼靴组合7含翼转换；独立靴毯场景才证明实际靴燃料消费。持跳段1→0时原版可先处理1tick翼，下一次JumpMovement才接仍剩余的多段族。

`movement-idle-pairs-settled.csv` 包含8对反转顺序的48更新block，先在测量外等真实设置保存完成、固定日时、暖16；数组预分配且日志/CSV在block外。07 update on-minus-off毫秒为 `+.02098,-.08899,-.08728,-.00176,+.01573,+.01199,+.04976,-.00677`，4正4负，差均值-.010792/中位+.005118ms；整链world+Draw差均值-.028181/中位+.012730ms。OFF/ON各384有效样本的AppDomain总分配72190868/72123992B，GC0各16；settled未记录GC1/2。06有6正2负且负均值，不能以负均值宣称提速。本场景增量未能稳定量化；保留均值/中位/p95/max和GC/分配，不丢尖峰、不立统一预算、不宣称字面零耗时或普遍性能接受/FPS。微测查询/owner零分配也不等于整Main零分配。最终有限性能判断和分母分析见 Issue。

## 失败、期待和夹具修正

全部旧日志保留。dash02原期待“太阳发动即耗盾”错误：原SolarDashStart只立solarDashing，首次实际NPC碰撞才ConsumeSolarFlare；05仅发动原期待已纠正，07补真实碰撞和第二次不再耗盾，不能互相冒充。dash03修原版ArmorSetBonuses初始化；dash04移除原版DashMovement未保证的direction写入期待，继续核实际速度。

original01夹具将全部角色inactive后等待Session，自阻塞；在等待前建立真实active本机。02/03完成Jump/性能后Mapped失败：新Player落地点未设fallStart导致原版摔死，随后UpdateDead抄原始双键，且专家盾完整Update资格与孤立UpdateEquips不同；改安全落地事实并以Tabi验证普通双键，07再以真实专家资格验证克盾。

04/05初始化/首帧链未完成，原因未定，不签“GPU不可用”。05 stop时快照before但最终log有ready，不能当同一等待点。只读准确XNA发现缺设备/音频异常可被默认模态消息吞掉，隔离ViewGame现override记录原异常并return false、StartLoop后核Initialize/Device，避免空Tick等待；06/07真实初始化成功未采到该异常，不能倒填04/05根因。仅05按精确自有PID/path/start核验后停止并保留stop回执。

06太阳新Player预种Buff在setSolar前被原版删除；07用真实装备/180tick自然产盾，不人工恢复盾。路由01/02仅测试期待遗漏：实际路由原已有view/journey公共传播，新Movement传播使精确断言暴露旧遗漏，补期望集合而未改view/journey生产或扩大到无关业务；原精确正反例保留，03通过。

## 待交付门与现场资产

后续最终UI原件为 `original-09-mode-left.log` 与同名目录：所有者要求模式移到开启左边，09在100/150%及按住/双击两态断言模式/开启/关闭顺序、三行开启/关闭同列，并真实点击关闭状态模式，完整Main/Dash/UI专项再次PASS。实现者已查看最终两张PNG。该次源码 `74511837217522b1af3496bed46abf6082f7c2ca`，Host SHA256 `ADAA0A83F4EA6CC8B4AC68060C6B51BC17A1F486D24CBF344EB07B6EBC2E52BC`，执行Probe `50F8D3E38AE6A691C290476E6FC10E305B25385540F2CF84C7CFC4C8000C2FB8`，日志SHA256 `076BC882E4A48BCABB10738412783EBEA8022902BE0ED8F173D49EF8A48C1749`；最终仅证据导航提交不冒称此旧执行commit。08则证明补充options/fancy/capture预采样取消门后的完整主链，精确身份保留各自目录；窄取消反控与最终累计结果以Issue原回执为准。

09再次记录完整动作/资源配对及8对settled：Update与full均4正4负，update差均值-.048066/中位+.009745ms，full差均值-.168619/中位+.073914ms，离散范围仍大。结合07/08和零分配有限helper，本场景没有可重复稳定额外损失的证据；也不能证明普遍零性能损失、负均值提速或所有者长期FPS。所有尖峰保留。累计01在剪裁F5夹具字段无赋值的警告即错误处停，显式可选attachment=false修复；累计02为所有者新UI决定在准确自有build及artifacts后代边界中断，保留stop回执，无累计PASS。最终整批Related从BASE核对执行/有效复用，不能借旧Stage记录。

真实物理焦点/IME/所有者手感、长期FPS、真实多人客户端/权威结果尚未验；SendData入口不是网络同步闭环。独审、正式普通完整Release候选包、受控一次备份换装待对应准入，当前没有新包/安装成功结论。恢复体验入口见[体验卡](../../scripts/phase0s/Movement-Assists-Owner-Test-Card.zh-CN.md)。

四项外来资产保持：tracked `docs/设计/本地参考资料使用说明.md`，untracked `.workbuddy/memory/2026-10-03.md`、`issue112.txt`、`pr113.json`。哈希声明 `.local/movement-assists-20261019/approved-retained-assets.json`，沿现有合法通道：`clean=false/releaseEligible=false`，提交本任务源码后仅这四项保留，不借旧Stage例外，不伪造clean。禁止合并/关闭/Tag/Release/版本推进。

## 独审修复与当前累计阻塞

原冻结 `7475454` 的独审原件为 `.local/movement-independent-review-20261019/review-7475454.md`，三项P2和累计失败保持历史身份。R1为精确比较后端恢复Profile后再次编码读取live Jump owner；新增请求/准确Player对象scope、正常/异常清理及仍占用receiver重入拒绝。Complex每帧按当前Mechanism与恢复意图重算，不能冻结初始dashDelay派生结果。`jump-cpu-08-thread-allocation.log`实际通过8组合初/后继完整alignment、直接及A→B→A重入/异常/clone/下一请求反控、348原版stage及4×20000同步helper零分配。`snapshot-review-01.log`与`worker-player-review-01.log`分别通过完整快照/codec/age/前缀以及独立worker玩家推进。最终累计仍须绑定提交后的新Wire叶；旧06/11的git-ls-files来源清单不包含当时未跟踪的新叶，不能补签为完整来源清单。

Jump07在wire通过后，旧helper把AppDomain全线程分配误称自身同步分配，steady=8192失败。修订以实际CLR一次绑定线程计数、明确64B对象正控（实际76B）、严格steady==0，AppDomain另列且不归因；08实际观察一窗线程0而域级8192。保留07失败，不追填8192的具体线程/调用栈来源，不扩大为整Main零分配。

R2/R3由 `original-11-mode-retirement.log` 完整Main/UI专项证明。Host SHA256 `1A14904328C036EBC4502C6DA14E62E15D911BAA21562A9B53509A4B198B7339`，执行Probe `3A1926844B13D10AA8752502E232104B69CD1CD710C442478FE1765148AF8A92`，日志SHA256 `6BD27126C43665F8398A0F345C85BF47415D497FFFC71A2387F921795AFCEDD7`。两模式真实热键开/关带实际/前态模式；关闭状态F5改模式不Clear旧结果而立即退休旧提示，F5自身静默。后续仅R1占用guard及测试维护，不冒称Main11执行了后来的所有源码。

R3的Main10/11原件均保留：`movement-dash-pairs.csv`每轮4对反序，原生F19人工/辅助各暖32（1次发动）+测240（4次发动），冷却[-1,20]、火箭7/太阳0及速度/位置逐帧相同；每臂总960测量更新/16次实际发动。`movement-fixed-hook-pairs.csv`每臂4×96=384测量更新，暖32、无动作，只撤装三个自有Movement Hook owner，Session/input/prefs/其它Hook仍在，非重建BASE。Main11 Dash update辅助-minus-人工为 `+.069619,-.152039,+.056699,+.031098ms`，Hook安装-minus-撤除为 `+.029664,-.104639,+.027415,-.337573ms`；Main10 Dash四负而Hook四正，不能以均值或负值宣称提速/零成本。每block均值/p50/p95/max、分配与GC0/1/2见原log，逐帧原CSV和原尖峰不删除；有限重算导航 `performance-review-pairs.json`。

累计03在NpcRollingCpu首个旧TerrainBounds期待退出1。准确引入链为917a531统一越界拒绝测试，f50646b/#115生产改原版nativeClip/FreeBody后未同步；BASE→本批相关生产diff为空，f506新增42个free例未盖colliding边缘。Boundary维护实际Derpling177/58×44，10干空气边缘加6实体/水/蜂蜜碰撞oracle，保留player/projectile/非法几何/缺格/重采样/4096限额；Collision scratch及Dust/random实例finally归还。其后ShortPrefix同源迁移缺漏：f506 World改消费Host.DisplayPath当轮租约，旧测试只source.Prepare，66870f3只改引用期待。真实owner修订在rolling04/05实际取得同帧sample/lease、CanDraw true、strokes2，未知格无未来/死亡退休、独立strict reader通过；严格reader使用独立slot2，避免真实Attack合法释放其slot1。旧strokes0具体gate未采到，保持未知。

Rolling05把连续呈现消费接到当前DisplayPath、完整身份/当轮lease/有限age及World同引用；原始Source另列，剩余future扣实际age，quality只在fresh源采样并记录缺源/非当帧跳过和非零样本门。当前候选缺租约ABI拒绝，认证历史f8路线保留其直接Cache合同。CPU不签Draw；当前固定绘制只能在真实121点当帧冻结事实反复Draw，不能cache.Publish或私填租约冒充历史frozen128比较。图形固定绘制新分支尚未执行。

`rolling-cpu-05-display-consumer.log`（SHA256 `6F855233BD8E08AF24768CECD2447B4979846FE928743F5CD4CB27CA7B0B7CF0`）完整执行11场1478更新，原Frozen f8数值门保持后报7场RED：stable180/180可见但剩余60步137；hornet-open360/360可见但60步300；guardian-A-select-B合法90/选中79/可见76/最长11。原summary/actors/updates/red均保留。门的现行适用性正在按正式合同核对，当前不降门、不改生产、不签最终累计。独审增量、普通包和安全换装仍待必要准入。

后继已按现行权威完成维护：正式[战斗合同](../../docs/功能介绍/战斗辅助行为合同.md)的2026-10-17更新接受有用短未来、不以恢复约两秒长尾验收；[阶段当前入口](../../docs/计划/辅助瞄准阶段文档.md)接受保留最新接续、不追所有NPC零空档，且实际普通冷首5/8GUC不承诺T+2/T+3。旧门最初确实适用于普通rolling候选，不能说它只属exact/native；后继替代也不来自单纯取消毫秒预算。当前候选保留全部原数值与预定B分母为 `rolling-historical-f8-reference-red.txt`，真实身份/租约/有限age/World同引用、未知/死亡/OFF/独立需求和整场完全未取得或未显示B继续硬检查；认证历史f8路线仍按其旧拒绝门，不改署旧结果。

`rolling-cpu-06-current-contract.log`（SHA256 `E80F09FF5CDBE8ED973FA6B4AB1030247AC000CDFEDE91FFFCA1F89E64DFB0BD`）完成11场1478更新并通过当前声明scope，原各统计及历史RED仍落盘。新增有限CPU `rolling-selection-geometry.csv`（SHA256 `6E5518E7E32E48662FB1E43505EDD442FD50F7D838799B4ADE627846A1A64DBF`）见证guardian frame43–53/GUC381–391：A16/B17都具接收资格、各受击框到RealMouse的d²均0，候选数2，实际/独立首位均16，符合平局低slot；11帧不是World无线或错误选择。3个B已选而未显示的单帧和其它真实空档仍保留，不泛化为零空档。`rolling-negative-01-current-contract.log`实际两场360更新：gun-B合法180/选中0/显示0/最长180仍被硬拒绝，专项期待此真实拒绝而PASS，不能靠缩小分母变绿。最终提交后的累计与原独审增量尚待完成。

# 2026-10-15 武器兼容完整候选

TASK_BASE 为 `0f93019526aa334877b4cfb4b5d38c3d1c0c55c4`，分支 `codex/aim-attack-modeling-and-integration`；生产末提交 `48651f8`，本证据的纯资料后继不改生产字节。当前 #114 / Draft PR #115 未合并；现装仍为 `9f0e756`。完整工作包已完成实现、自测、局部返修和自查，交集中独审。独审前未打完整新包、未换装。主调度 Astra/Max 只读；Sol-1 与 Sol-2 的实际 Sol/High 已核一次。

原件 `references/JMR_AIM_WEAPON_COMPAT_20261015/` 未修改。Sol-1拥有A/B、共享入口、注册/协定/证据及唯一Git/公共构建输出；Sol-2仅写Betsy、Bottle、纯算术帮助和C专属新测试，C四文件已冻结。TASK_BASE→最终资料候选的28文件清单由本目录 validation-map.json 及Git差异给出。外来 `docs/设计/本地参考资料使用说明.md`、`.workbuddy/`、`issue112.txt`、`pr113.json` 保留并排除提交，未reset/clean、未操作所有者游戏/真实角色世界。

## 三个结果及真实因果

**A是所有者新的放置行为决定，不倒写旧合同或伪造回归引入点。** 原版Item.sentry包括DD2塔。HostPlacementAttack和HostAttackAim全部OR分支外总门均排除sentry，因此CanUse/Shoot无增强租约，也无Basic重定向；既有非哨兵召唤和鞭维持。固定普通Release最终叶覆盖46项放置、50出生、DD2/蜘蛛两镜像、Aim/自动转向、物理位置有墙与NPC位置可用的负例；Basic旧期待同步改为物理鼠标，取消租约负例迁至原非哨兵1309。

A初版fixture把DD2出生期面对方向误按CanUse前方向计算，产生2px差异；原版Shoot自身先变向。修的是测试出生oracle，生产没有为过测改Facing。`A-r02.log`及修正前原件保留，最终真实消费者通过。

**B分裂当次不读鼠标，只复制父球Center/velocity。** 锁定原版AI_099_2_Yoyos的channel=false分支令父球-1，再magicString令-3并生-2子；自由球固定每AI重力0.3、长度上限16、Y<=3时水平加速0.1。频率决定时机，不决定重力。原持球导航及Basic的channel/ai准入不是应全局删除的条件；遗漏在于没有给真实起手/出生当步最后持球消费提供下一释放所需方向。历史`eb7f1ad`接入持球导航，`4829cce`公开cadence Aim接缝但未补kind4继承阶段，`380132e`恢复Basic仍保留真实持球准入；这是组合覆盖遗漏，不将合法释放拒绝门单独指为原版消费回归。

CombatUse继续独占节奏和真实来源；新查询只承认primary/key、物理Left、同操作、原版出生当步。HostYoyoReleaseAttack在准备阶段求代表方向，HostAttackControl仅在合法持球点借坐标，不写channel、AI、时间、出生速度，不牵引自由球。清线opening改为同种子的有限干燥自由重演，保留整数接收、免疫、有序阻挡及最小随机穿透3；首次持球已接收、液体/碰撞及未证明未来侧别保守留空。普通绳长仅限制原鼠标输入，不作自由射程。玩家/null/替换、魔法绳/来源意图、运动参数和地形绑定lease。

完整Player→NPC.UpdateNPC→Projectile.Update链中，左右静态及明确vx±1夹具移动各120更新，30父/30分裂、1/5/9…117出手机会不变，实际自由Damage→Strike闭合；该移动夹具不冒称完整原版NPC家族AI。约500px/220绳长两侧新代表Contact38；Aim关使用该提前角的原版对照与Aim开相反鼠标都30父/30子，实际自由接收分别9、7；墙和卸绳撤销。旧7227在同预览反例返回null。普通导航、无目标物理方向、六取消边界、四同tick重入、手套/配重代表另按原有层级通过。

初始B旧Step仅跑Player/Projectile，冻结了NPC免疫，不能证明自然移动或持续接收；`B-cause-r01.log`保留该分歧。最终自然链及同fixture真实9f旧字节反例分别在`B-natural-boundaries-r01.log`、`B-natural-baseline-r01.log`；旧失败0自由接收只属于该明确fixture，不当人类命中率。最终普通Release原件为`release-f720242-magic-string-aim-r01.log`。

B成本首版暴露两个冗余：无新opening的自然释放仍沿controller持续请求Source；出生点已捕获却未被HasPreparedInput承认，反复扩窗。2fcc5db仅对真实managed魔法绳，将NextAction限定为下一合法opening或出生当步持球读点，并承认其同来源已准备输入；不改NPC路径/目标标记父需求、普通导航或其它Plan共享。`B-demand-red-r01.log`拒绝541旧生产，绿件和最终出手证明没有靠少发/降增强省成本。

**C方向稀疏与昂贵错误角重演。** Betsy `4e513d038e5252d18036abe9e628cfbec378bec9`、Bottle `fcc53ee7f53cba2f6133226b37091f6bf40ff706`初次专用求解使用9个指向种子加32全圆；遗漏了真实离散弧线与矩形接收间的角度反求覆盖。两个真实消费者共用纯算术AttackBallisticCandidates，最多8个优先种子加直指及原32兜底，总上限41保持；各自原motor、三成员、出生及瓶派生完整重演保持。当前hand必要初窗按实际高差、成员/facing、接收矩形估计；仅调度保守提示，不造Contact、不抬统一预算或延迟出手。

C最强静态点tile(80,25)：旧33家族/5624motor完整窗无解，新1/258 Contact43，原版关闭Aim手动3出生1接收，正常2手1Basic1Enhanced/6出生1接收。Molotov旧29/15595 Contact82→新1/44，Ale旧30/1437 Contact101→新1/66；各原版父体确收，不混随机子计数。自然NPC type3原AI位移、手动可达与增强实际接收、近/中远/镜像、低墙/液体、1132px完整窗真无解分别保留。最初纯水平hint、成员fan及半宽容差错误由r03–r09实际消费点定位后修正；冷首发预算暂停仍可Basic，未称100%Enhanced。最终C原件`c-r12.log` SHA256 `2CA53A6A368C9A68EBE6D85F1E7BABBBB1BCBA0459273F5852D2C7ACCB300362`；未改Betsy motor10场313动作、birth4角色、Bottle motor33场924动作exit0。最终固定普通Release C叶另exit0。

## 普通Release完整Main成本与质量

锁定Terraria 1.4.5.8、net472/x86、优化Release、正常Bootstrap/原版Main.Run/Update/Draw/EndDraw；使用上轮载体，独立新保存根、固定种子、真实原版NPC/玩家/投射物推进，模拟设备/焦点输入。正式计时串行，不并行重型构建。13有效窗共7800world更新；CSV含首次、持续、尾段及初始化，原失败/pilot不挑绿删除。统计为每个真实GUC+1完整Main.Update，另存Draw/EndDraw及combat独立区间，绝不相加嵌套均值，不称线程CPU、所有者FPS或所有硬件结果。

| 固定窗 | Update平均 / p95 / p99 / max（ms） | 实际质量 |
|---|---|---|
| C 7227新Both | 1.648 / 3.219 / 7.560 / 39.035 | 39父=18E21B，8父接收 |
| C 9f旧Both | 1.745 / 4.069 / 7.577 / 36.819 | 39父=3E36B，7父接收 |
| C新Off / Aim / Path均值 | 0.914 / 1.186 / 1.425 | Off/Path实际物理出手，未署辅助输入 |
| B 541修前Both | 2.060 / 3.067 / 4.821 / 52.211 | 90父90子，89E1B/34自由接收 |
| B 2fcc最终Both | 1.443 / 2.141 / 3.406 / 58.099 | 90父90子，89E1B/34自由接收 |
| B 9f反向旧Both | 1.797 / 2.642 / 4.933 / 53.889 | 90父90子，88E2B/33自由接收 |
| B最终Aim均值 | 1.234 | 89E1B/34自由接收保持 |
| B Off / Path（541独立父需求窗）均值 | 1.027 / 1.241 | 各90父90子，物理方向 |

C新→旧、B旧→新再因真实热点作受影响新→旧配对，全部保留。B最终与旧父机会worldStep逐项相同，Current消费同GUC、目标2，0错误目标/延迟输入；C手时点同为121/149…457，每手3成员。C不是暖段全增强：13手前2B、6E、末5B；末段原版NPC1真实受击/自然运动远移，约730–850px，5手的逐项拒绝机制仍未诊断，不统一署成不可达。新代表窗口整体成本/质量支持有限实用判断，不宣称普遍提速或所有中远距持续有解；最大尖峰并非全部改善。

单独80次原版Player→NPC→Projectile计数（有观察开销，不作上述速度）：B Source实际NpcMotion.Step旧Aim/Both10760/12544，541修前11621/12870，2fcc修后2633/4303；Off0、Path1920保持。C当前Off/Aim/Path/Both为0/122/2112/2181，较旧0/80/2103/2084增加的必要初窗未用于冒称省步数。原件`weapon-work-old-r01.log`、`weapon-work-new-r02.log`、`weapon-work-final-r01.log`及Main完整数值/哈希总表`measurement/formal-summary.json`保留。

7227 Host SHA `9B3B9268B34D66573082564ABABB7B9E5576C8201E0D40DFF54778D16B391214`承担C五窗；541 Host `FABC7A025B5400F695AB0C311A3A365AEED327BBBF884BC9FBC2B6ECEBC0BF61`承担修前B窗；2fcc Host `01192EC8F07E050599C8A2C8E6B6FD49787D4C4E640EFF9AE71764F339723FF5`承担最终B三窗。f720仅收紧清线lease实际玩家/null/替换门，Main的ClearLine=false采样分支未变；最终实际消费者在f720字节另核。相应复用是交独审的明确适用关系，绝不把原日志署为新MVID。

## 检查、失败与交付边界

13项必要责任及当前/拟复用身份详见validation-map.json。f720六原版消费者A/B/C、selection-preview、navigation、basic-aim exit0；架构combat和叶映射exit0。最后自查将Created的openingAmmo赋值收窄为仅managed yoyo，避免其它控制体继承前一opening未清字段；48651f8恢复其它类型原初始化。现有control整叶通过普通yoyo、receive、guided113/218/495实际AI/首Damage/释放及bubble封存ammo/Kill接收，随后失败于已撤的Knife idle ExpectedPreview期待；laser整叶通过72 motor后失败于同类已撤idle期待。两整叶均exit1，原件按OWNER_WITHDRAWN保留，**不记整叶PASS，不恢复已撤预览**；局部通过只支撑所列相邻消费者。它不改变已采A/C或B managed分支，六叶/成本拟复用且保留原身份。已有候选全前缀/普通枪/鞭/相邻节奏等未改路径按实际源差异拟复用7227记录。原首次命令/测试错误、C各轮期待/供给修正、Main临时驱动缺LAA的原版初始化OOM、未启动的复制根误选和pilot均有原件与处置；驱动仅恢复与锁定原版/既有驱动相同的LAA位，未改游戏EXE/产品/系统。

本轮无新增生产日志、后台采样或常驻诊断；观察仅在独立测试/临时载体，末次一次输出。没有全功能/所有武器/所有设备/多人/所有者新包接受声明，长期完整资格仍INCOMPLETE、releaseEligible=false，红框仍OWNER_WITHDRAWN。C末5手Basic、首次Basic的完整机制覆盖及未证明的自由液体/碰撞分支保留为限制，不隐去。

三个长期协定已写入`docs/网页Chat协作约定.md`、`docs/规范/工程治理规则.md`、`docs/设计/可重复构建与项目骨架.md`：今后已获安装授权的普通交付默认一次固定完整包、相关受控文件轻量备份、官方替换、一次载荷读回；纯资料不重包，仅真实失败重做受影响步骤。它不自动授予未来安装权，不取消路径/所有权/备份/原子性/用户数据底线，分支候选也未冒称main已生效。

下一步由未参与实现/期待起草的Sol/High独审TASK_BASE→最终候选、原件与拟复用关系；通过后唯一整合者生成一次正常优化Release/net472/x86 ContinuousProcessing全包、真实阶段记录与既有包身份核验，按04备份确认Steam目标受控文件、官方Restore/Install、一次载荷读回。最终ZIP/hash、完整包/安装与工程测试分别记录；不merge/close/Tag/Release，不启动所有者游戏、不触碰真实角色世界。

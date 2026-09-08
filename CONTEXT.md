# Battle Core

2D 实时战斗逻辑核：只负责战斗推演，不负责表现层。第一版主场景为 ARPG（类暗黑）。

## Language

**Battle Core**:
外部 tick 驱动的纯逻辑战斗推演核；输入出生与操作，输出移动与技能等结果。
_Avoid_: 战斗引擎（易与渲染/物理引擎混淆）, 表现层

**ARPG Battle**:
连续移动、指向/范围技能、属性成长、大量 Buff/Debuff、多怪物同场的实时战斗形态。
_Avoid_: 回合制, 格斗帧对战, RTS 编队微操

**Deterministic Simulation**:
相同配置、相同初始 seed、相同输入序列，在任意平台上得到完全一致的推演结果。实现按此约束编写。第一版不交付帧哈希、命令日志回放等校验工具。
_Avoid_: 实用可复现, 非确定性随机, 把不做校验工具理解成可以破坏确定性

**Logic Frame**:
战斗核推进的最小时间单位；每次外部 `Tick()` 恰好前进 1 帧，帧长固定为 1/30 秒。
_Avoid_: 可变 dt, 渲染帧, 墙钟时间

**Continuous Space**:
战斗空间为连续 2D，实体位置用 (x, y) 表示，移动与范围判定在连续坐标上进行。V1 为俯视平面：X、Y 同为地面轴。`SideView` 中 Y 为高度，见 **Spatial Mode**。
_Avoid_: 网格移动, Tile 占位为主的空间模型, 把俯视 Y 假装成高度却仍用全向移动

**Spatial Mode**:
一场 Battle Instance 选用的空间后端：V1 默认 `TopDown`（全向移动、圆占用、地形 AABB 为阻挡、缩短/取消位移）。`SideView` 为同一推演核上的横版后端（重力、接地、站在平台顶边、左右朝向），与伤害管道/Buff/技能时间轴共用，不另写战斗框架。一张图一种模式。
_Avoid_: 同场混用两种空间, 宿主用向下 MoveDirection 冒充重力, 为横版复制一套伤害/Buff 核, 把格斗帧对战塞进本核

**Space Service**:
核内空间后端接口：合法落点、本帧位移、扫掠命中、击退如何落地、朝向如何从瞄准得出、gameplay range 如何度量。技能与 Brain 只依赖该接口，不直接解释 Y 的含义。横版施法射程与 Brain 仇恨/攻击只看 X，但目标须在 `VerticalReach` 竖直带内（默认 2 米）；leash/跟随距离只看 X。占位、碰撞和技能命中形状仍按 2D 处理。横版接地 = 向下探针被地板/地形/阻挡单位挡住。俯视行为必须保持不变。
_Avoid_: 在技能/Brain 里散落 if (横版), 未抽接口就改碰撞语义, 无形状的单体技能凭 X 距离隔楼命中

**Spatial Fixed-Point**:
位置、速度、距离、角度等空间量使用定点数表示与运算，以保证跨平台一致。对外用独立定点类型（如 Fix64），内部为 Q16.16；1 逻辑单位 = 1 米。角度用整数档（V1 全圆 1024）+ 查找表。
_Avoid_: 空间量用 IEEE 浮点做逻辑判定, 把伤害整数和定点坐标混用同一裸 int

**Combat Integer**:
生命、伤害、护甲、冷却帧、Buff 层数等战斗数值使用整数。
_Avoid_: 战斗数值用浮点

**Hit Shape**:
用于命中与占用判定的几何形状；单位占用以圆为主，技能可选圆/扇/环或施法者朝向的轴对齐攻击盒（AABB），地形阻挡使用 AABB。
_Avoid_: 第一版引入 OBB/任意多边形判定框

**Faction**:
实体所属阵营；用于敌我判定。同阵营单位移动上可重叠；敌对阵营单位不可重叠。
_Avoid_: 队伍（易与编队混淆）, 阵营与命中过滤混用同一词而不区分

**Movement Blocking**:
移动时的空间占用约束：所有单位不可穿地形 AABB；同阵营单位可重叠；敌对单位圆形占用不可穿透。
_Avoid_: 全体硬阻挡, 全体可重叠

**Collision Resolution**:
`TopDown` 在移动阶段用「缩短/取消位移」消除与地形或敌对单位的非法穿透；不做滑墙与互推。`SideView` 的接地与贴边另论，不得回写进俯视路径。
_Avoid_: 第一版互相挤开, 第一版沿切线滑移, 用俯视缩短位移冒充横版落地

## Entities

**Character**:
可移动、可施法、可受伤的战斗单位；玩家、怪物、召唤物都是 Character，均可由外部操作或 AI 驱动。
_Avoid_: 把召唤物做成非 Character 的特殊实体

**Projectile**:
技能产生的飞行物。V1 按米/秒直线匀速，使用碰撞圆，寿命或出界销毁，默认撞 Terrain AABB 销毁（定义可关闭），并支持可配置穿透次数（0 表示首次有效命中即销毁）。跟踪等转向运动为扩展口，V1 不实现。命中谁由效果 Hit Filter 决定。
_Avoid_: 把所有持续效果都塞进投射物, V1 做跟踪弹/弹射


**AreaEffect**:
附着于地面或跟随目标的区域效果，按间隔或停留施加伤害/Buff。生成时快照来源方战斗贡献；创建者死亡后仍继续。跟随目标进入 Dead 或消失则销毁。每个区域有独立确定性 RNG。
_Avoid_: 法术场, DoT 圈（口语可，文档用 AreaEffect）

**Terrain**:
静态阻挡体，使用 AABB，不参与属性与技能，只参与移动阻挡。
_Avoid_: 可破坏地形（第一版不做）

**Pickup**:
可被 Character 靠近拾取的场上实体。
_Avoid_: 把背包本身当成战斗实体

**Brain**:
Character 的决策源；每帧产出与玩家同构的操作命令。可插拔。V1 内置：Idle、ChaseAttack、HoldPosition、FollowOwner；参数可配置，行为图不开放编辑。
_Avoid_: 把 AI 完全放在核外汇总后再异步灌入（易破坏确定性）, V1 做风筝/逃跑走位

**Command**:
驱动 Character 的原子操作意图（如移动、施放技能、停止）；来自外部输入或 Brain，进入同一管道。
_Avoid_: 直接改内部状态来“遥控”单位

## Attributes & Modifiers

**Attribute**:
由配置声明的战斗数值维度（如 MaxHP、ATK、MoveSpeed）；配置侧用字符串 ID，运行时编译为整数 ID。
_Avoid_: 在代码里写死唯一属性集

**Combat Binding**:
Battle Config 中把管道/移动槽位映射到属性 ID 的对照表。必填：MaxHP、PhysicalDef、MagicalDef、CritRate、CritDamage、DamageDealt、DamageTaken、MoveSpeed。名字可自定，但必须指向已声明属性；MoveSpeed 单位为米/秒。模板基值 MaxHP 必须为正、MoveSpeed 不得为负；运行时被修饰成负的 MoveSpeed 按 0（不动）处理，复活时 CurrentHP 至少为 1。CurrentHP、Shield、Lives 不是 Attribute。
_Avoid_: 在代码里写死属性英文名, 无绑定让管道猜名字, 负速度倒着走

**Modifier**:
对 Attribute 的加值或乘区修正，来源包括 Buff、装备、技能被动等。
_Avoid_: 直接覆盖当前值而不走统一结算（除非显式的 Force 类修饰）

**Modifier Stack**:
同一 Attribute 上多个 Modifier 的集合；按固定顺序结算，例如 `(base + Σflat) * Π(1+pct) + Σfinal`。百分比类字段统一用万分比（10000 = 100%）。
_Avoid_: 未定义顺序的随意乘加, 用浮点表示百分比

## Buffs

**Buff**:
附着在 Character 上的时限状态，包含 Tag、叠层规则与效果列表；Debuff 是敌对向的同类概念，不另建模。
_Avoid_: Debuff 作为与 Buff 平行的另一套类型系统

**Buff Tag**:
用于驱散、免疫、互斥与查询的标签（如 `Stun`、`Bleed`、`Magic`）。
_Avoid_: 仅靠 BuffId 硬编码特殊交互

**Control Effect**:
限制行动能力的独立效果类型（如眩晕、沉默、定身、缴械），有单独机制，不通过把属性改成 0 来模拟。
_Avoid_: 用 MaxSpeed=0 等属性窍门代替控制

**Combat Trigger**:
挂在 Buff 上的事件订阅：在造成伤害、受到伤害、击杀、起手施法等战斗事实之后，对持有者执行一组即时效果（加资源、治疗、上 Buff 等）。永久被动 = 时长 0 的 Buff。不是另一种 Skill。V1 不含获得/失去 Buff 触发；若以后加，须限定 BuffId/Tag、获得仅新 instance、失去默认不含死亡清理、仍禁止嵌套。自身死亡不靠「失去全部 Buff」冒充。
_Avoid_: 另做 PassiveSkill 类型, 触发里再开技能时间轴, 触发再触发（嵌套）, V1 做任意 Buff 获得/失去触发

## Skills

**Skill**:
由配置描述的能力：时间轴（前摇/生效/后摇）+ 目标选择器 + 效果列表；消耗与冷却以逻辑帧计。正 `Range` 由核统一校验，未配或非正值表示不限。V1 无公共 GCD、充能层数与冷却缩减。
_Avoid_: 每个技能一份专用硬编码逻辑作为主路径

**Skill Timeline**:
技能从按下到结束的帧序列；关键帧触发效果。V1 从前摇开始到后摇结束前独占当前时间轴，不允许取消后摇起手另一技能；不含持续引导（按住引导）施法。
_Avoid_: 第一版 Channel / 引导施法

**Carry**:
两个 Character 之间的携带关系：载体移动时被携带者跟随；被携带者移动权暂交；可通过技能效果开始携带、投掷或放下。载体眩晕或任一方死亡时掉落；定身只阻止移动，沉默与缴械不拆除既有关系。载体发生技能位移时，被携带者同帧保持 CarryOffset；携带期间不单独做碰撞，结束关系时校正合法落点。
_Avoid_: 仅用 Buff 塞 carrierId 冒充携带, 把整段抓投写成单一特殊技能类

## Boundaries

**Command Queue**:
外部或 Brain 提交的带逻辑帧戳的 Command；`Tick()` 只消费属于当前逻辑帧的命令。命令携带的坐标（出生点、MoveTo 终点、技能目标点）必须在地图边界内，否则拒绝；方向向量任意大小均安全归一化。宿主输入不得让 `Tick()` 抛异常。
_Avoid_: 命令在提交瞬间直接改世界, 图外坐标进入定点平方运算

**Battle Event**:
Tick 产生的增量事实（移动、技能、伤害、Buff、携带等），供表现层与外部系统消费。
_Avoid_: 仅靠全量快照驱动表现

**Battle Query**:
对当前战斗状态的只读查询（位置、属性、CD、Buff 等），不改变模拟。
_Avoid_: 通过查询接口写状态

**Battle Config**:
创建战斗前加载并校验的只读配置包（属性、角色、技能、Buff、投射物、区域、Brain 等）；战斗中不热更。
_Avoid_: 战斗中换表, 逻辑核直接依赖某种文件格式为唯一入口

## Combat Resolution

**Damage Pipeline**:
伤害结算的固定步骤序列：技能基数 → 暴击 → 按伤害类型做防御减免 → 双方伤害增减修饰 → 护盾吸收 → 扣 HP。步骤顺序不可配置；倍率、类型与能否暴击可配置。
_Avoid_: 运行时解释任意公式字符串作为主路径

**Defense Mitigation**:
Physical/Magical 的递减减免：`mit = min(def * 10000 / (def + K), MaxMit)`，再 `damage = hit * (10000 - mit) / 10000`。`K` 与 `MaxMit`（默认 8000）在 Battle Config；True 跳过本步；最终至少 `MinDmg`（默认 1）。V1 无穿透。
_Avoid_: hit - def 作为主减免, V1 防御穿透

**Crit**:
每个伤害实例独立判定：`roll(0..9999) < CritRate` 则 `hit = hit * CritDamage / 10000`。技能可关闭暴击。CritRate 默认上限 10000。
_Avoid_: 一次技能共用一个暴击骰, 暴击替换整套技能系数

**Skill Base**:
伤害与治疗共用基数：`flat + attr * ratio / 10000`（每个效果只引用 1 个属性 ID；不用的项为 0）。正数除法向 0 截断。
_Avoid_: V1 多属性加权基数

**Heal**:
治疗用同一套基数，可按效果开关暴击；不经防御减免、不经护盾。结算值全部加到 CurrentHP，允许超过 MaxHP。MaxHP 变化不自动削减 CurrentHP。
_Avoid_: 治疗吃防御, 强制截断到 MaxHP, MaxHP 下降时夹紧当前生命

**Spawn HP**:
出生时默认 CurrentHP = 当时 MaxHP；Spawn 命令可覆盖初始 CurrentHP。
_Avoid_: 只能残血出生且无满血默认

**Damage Amp**:
防御之后、护盾之前：`hit *= (10000+DamageDealt)/10000` 再 `hit *= (10000+DamageTaken)/10000`。两属性由配置声明，分别取自攻击者与受击者；可为负。治疗不走本步。
_Avoid_: 把造成与受到加成先加在同一个乘区, 治疗吃易伤

**Shield**:
护盾是 Character 上独立于 Buff instance 的有序 ShieldLayer，可由技能、命令、拾取或 Buff 动作创建。来源 Buff 被移除或驱散不自动拆盾；护盾按自己的到期帧结束。伤害先按「到期帧升序，同时到期按 instanceId 升序」扣盾，剩余再扣 HP。治疗不补盾（加盾是单独效果）。
_Avoid_: 角色上只有一个混合护盾池, 把护盾和 HP 溢出当成同一件事

**Damage Type**:
伤害的分类（第一版：Physical、Magical、True）；True 跳过防御减免。类型由配置声明并接入管道，不在技能里各写一套。
_Avoid_: 每个技能自定义完计算法

**Death**:
HP 降至 0 或以下时：若 Lives > 0，则消耗 1 次并进入复活倒计时（不可被选中、不受伤、不行动），倒计时到期后站起；若 Lives = 0，则立即 Dead。致死时清普通 Buff、护盾、当前技能、移动、软锁与 Carry；保留 PreventRevive Buff、技能 CD 与 ModifierSource。普通单位的尸体保留 Battle Config 配置的逻辑帧后销毁；模板标记 `CanBeRevived` 的单位覆盖该计时并无限保留尸体。
_Avoid_: 死亡当帧立即从世界抹除且无尸体窗口, 用全局尸体展示时间决定复活资格

**Revive**:
把 Dead 且模板允许复活的对象拉回战斗的机制。来源包括：消耗一次 Lives 后倒计时自动站起，以及技能效果复活友方（如主角复活宠物）。复活资格与全局尸体展示时间无关；`PreventReviveBuffEffect` 会跨死亡保留并在有效期间阻止复活。普通伤害/控制选敌仍跳过 Dead，复活倒计时中也不是有效目标。尸体不阻挡移动，站起时复验落点（同 Carry 放下的环形搜索），避免与踩在尸体上的单位互锁。
_Avoid_: 复活只能靠外部换一个新 entityId 的 Spawn, 站起后与他人重叠且双方都动不了

**Lives**:
Character 上的复活次数资源；可被即时 Pickup 或技能改变。致死时若 Lives > 0 则消耗并进入复活倒计时。
_Avoid_: 用外部换新实体冒充实命次数

**Facing**:
Character 独立朝向。俯视可与移动方向不同；由 `SetFacing` 改写，或在带目标点/方向的 `CastSkill` 校验通过并起手时立即转向。技能之后被取消不回滚朝向；受伤不改变朝向。横版瞄准、扇形、冲锋、弹体和 `SetFacing` 只保留左右（step 0 = +X，512 = −X）；纯竖直瞄准保持当前左右，击退/拉人/投掷的位移向量不夹朝向。横版走动会改朝向，俯视走动不会。
_Avoid_: 朝向等于移动方向（俯视）, 受击自动转向攻击者, 横版把竖直瞄准当成朝天

**Map Config**:
独立于技能/角色表的场景配置：矩形边界、地形阻挡、刷怪点等。创建战斗时选用一份 Map；单位不能走出边界，Projectile 出界即销毁。刷怪点关联刷怪表，不直接写死怪物数值。
_Avoid_: 把地图尺寸写死进技能表, 无穷平面不回收弹体

**Spawn Table**:
独立刷怪配置：在何点、刷谁、用何种触发机制。关卡通过刷怪点引用本表。第一版只实现按逻辑帧时间触发；「清波后再刷」「进入区域再刷」保留扩展口，V1 不实现。
_Avoid_: 把波次脚本写进技能时间轴, V1 做齐全部触发器

**Spawn Point**:
地图上的位置标记，引用 Spawn Table 中的条目；开局静态阻挡仍随地图生成。
_Avoid_: 刷怪点与角色模板混在一张表且无机制字段

**Player-Controlled**:
Character 是否接受外部 Command 的标记；与如何被 Spawn 无关。刷怪表与外部 Spawn 都可以生成；开局玩家角色约定由外部 Spawn，刷怪表默认不刷玩家。
_Avoid_: 只有刷怪表能出玩家, 只有外部能出可操作单位

**Rng Stream**:
确定性伪随机流。战斗创建时由 seed 派生：战斗级一流、每 Character 战斗一流、每 Character AI 一流、每 AreaEffect 一流、每 Buff instance 一流；禁止使用语言/平台默认随机。
_Avoid_: 全场单一游标, 非确定性 rand, 让已销毁 Character 的 RNG 继续被场上效果占用

**Tick Phase**:
单帧内固定阶段顺序：Spawn Table 本帧条目入队 → 消费本帧 Command → Brain → 移动 → 技能时间轴 → Projectile/AreaEffect → Buff → 伤害/治疗收尾 → 死亡与销毁 → Pickup → 超时检查 → 派发 Battle Event。阶段内实体按 entityId 升序处理。Brain 能看到本帧已消费的外部 Command 结果，但看不到尚未执行的移动及后续阶段结果。
_Avoid_: 未规定顺序的系统穿插, 依赖哈希表遍历顺序

**Late Command**:
帧戳早于当前逻辑帧的外部 Command；直接丢弃，不回溯补模拟。
_Avoid_: 迟到命令回放进过去的帧

**Move Command**:
常规移动意图，含每帧方向/速度与 MoveTo 终点两种；冲锋等位移属于技能效果而非 Move Command。MoveTo 第一版直线前进、不做绕障寻路；若本帧因地形或敌对阻挡而无法继续接近终点，则取消该 MoveTo。
_Avoid_: 第一版 A* 绕路, 把技能位移做成持续 MoveTo

**Current Target**:
Character 上的软锁目标 ID，供 Brain/UI 与选敌器 `CurrentTarget` 使用；技能是否打该目标由选敌器配置决定，软锁本身不强制改写命中。通过 `SetTarget` Command 设置；目标失效时清除。普通死亡目标会失效；允许复活且当前未被禁止复活的 Dead 可作为复活软锁目标，在复活资格失效前不清除。
_Avoid_: 硬锁强制所有技能必中当前目标, 无 SetTarget、仅靠核内隐式锁敌

**Battle Outcome**:
胜负与是否结束战斗由外部根据 Event/Query 裁定；核不内置胜负编辑器。`FactionWiped` 只记录该阵营第一次出现没有 Alive / Reviving Character；可技能复活的 Dead 不阻止该事件，所以它不是不可逆的最终失败。每个阵营整场至多发一次。核在达到可配置 `maxFrames` 时发出超时事件，但不自动 Destroy。
_Avoid_: 核内写死关卡胜负逻辑并自动销毁战斗

**Battle Instance**:
一次独立的战斗模拟，自有 seed、帧号、实体与命令队列。同进程允许多个实例并行存在，彼此不共享可变状态。
_Avoid_: 全局单例战斗世界

**Instance Threading**:
不同 Battle Instance 可并行 Tick；同一实例上的 Command / Tick / Query 必须串行。核内无共享可变全局；Tick 内部不并行拆系统。
_Avoid_: 核内线程池并行阶段, 依赖可变全局单例

**Hit Filter**:
技能效果上的命中过滤（敌对 / 友方 / 自身 / 全部等），按效果配置，不写死「伤害只能打敌」。第一版不做阵营外交矩阵。
_Avoid_: 全局写死伤害/治疗对象, 第一版完整 Hostile/Friendly/Neutral 矩阵

**Combat Flag**:
Character 上影响交互的独立开关：Targetable（普通选敌器是否可选）、Damageable（是否接受伤害与控制类效果）。`UntargetableBuffEffect` 与 `ImmuneToDamageBuffEffect` 分别提供临时不可选中和全伤害免疫，二者彼此独立，也不改变模板开关。商人/待救等非战斗角色默认不参与移动阻挡（含敌对硬挡）。不可破坏墙体用 Terrain。
_Avoid_: 单一 Invulnerable 混用选中与受伤, 把墙做成 Character

**Pickup Rule**:
靠近拾取在核内判定。即时拾取由核施加战斗内效果；局外物资只发事件并移除场上物体，背包、换武器与再掉出由外部处理。宿主换装后通过具名 ModifierSource 把已算好的修饰注入核。拾取资格由定义上的 Picker 过滤（任意 / 仅玩家操控 / 与生成时阵营相同）。
_Avoid_: 核内解释装备数值与经济, 核内维护局外背包为真源, 捡到之后再问宿主是否销毁

**V1 Scale**:
第一版设计目标为中等 ARPG 规模：约 Character ≤64、Projectile ≤256、AreaEffect ≤64、Terrain ≤128。超出为软顶，不承诺帧时；幸存者海量弹幕不在 V1 范围内。
_Avoid_: V1 按幸存者海量实体做性能承诺

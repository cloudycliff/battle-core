# Battle Core 使用手册

面向要**接入宿主**（自己的游戏、工具、测试）的人：怎么创建一场战斗、怎么灌输入、怎么读输出、配置从哪几块拼起来。

术语和冻结规则以 [CONTEXT.md](../CONTEXT.md)、[V1 需求](v1-requirements.md) 与对应 [ADR](adr/) 为准。本页只说明当前接入方式，不另行定义行为。

当前配置入口是**内存对象**（C# 构造）。也可以用附带的 JSON 加载器：`BattleJson.LoadBattleConfig` / `LoadMapConfig` / `LoadSpawnTable` / `LoadBundle`。Schema 在 [schemas/](../schemas/)，样例在 [samples/arena.bundle.json](../samples/arena.bundle.json)（俯视）与 [samples/sideview.bundle.json](../samples/sideview.bundle.json)（横版推图）。你仍可用任何格式在核外反序列化，再 `new` 这些类型。战斗中不能热更。

---

## 1. 这是什么

Battle Core 是 **.NET 8、无引擎依赖** 的 2D 实时战斗**逻辑核**。它推演移动、技能、伤害、Buff、弹道、区域、AI 和刷怪，不负责渲染、动画、音效、网络传输、背包经济。

- 每次 `Tick()` 恰好前进 **1 逻辑帧**，固定 **30 Hz**（1 帧 = 1/30 秒）。
- 空间用 `Fix64`（Q16.16），**1 单位 = 1 米**。不要用 IEEE 浮点做逻辑判定。默认 `TopDown` 中 X、Y 都是地面轴；`SideView` 中 X 为水平、Y 向上，重力和跳跃由核推进。见 [space-modes.md](space-modes.md)，不要用向下 `MoveDirection` 冒充下落。
- 生命、伤害、冷却、万分比等战斗数值是**整数**（`10000` = 100%）。
- 相同配置 + 相同 seed + 相同命令序列，结果应一致。V1 **不提供**帧哈希、回放器。
- 胜负由宿主根据事件/查询自己裁定。核会发阵营清空、超时等事实，**不会**自动销毁实例。

本仓库只有核和测试，没有 Godot / Unity 工程。引擎只做宿主：创建实例、每帧 `Enqueue` + `Tick`、消费 Event。

---

## 2. 最小可运行例子

下面是一场「玩家站桩、怪物按表刷出并追击」的骨架。复制后按自己的表改 ID 即可。

```csharp
using BattleCore.Brains;
using BattleCore.Combat;
using BattleCore.Configuration;
using BattleCore.Math;
using BattleCore.Simulation;
using BattleCore.Simulation.Commands;
using BattleCore.Simulation.Events;
using BattleCore.Skills;

static FixVector2 P(int x, int y) =>
    new(Fix64.FromInt(x), Fix64.FromInt(y));

var bindings = new CombatAttributeBindings(
    MaxHp: "max_hp",
    PhysicalDefense: "physical_defense",
    MagicalDefense: "magical_defense",
    CritRate: "crit_rate",
    CritDamage: "crit_damage",
    DamageDealt: "damage_dealt",
    DamageTaken: "damage_taken",
    MoveSpeed: "move_speed");

var attributeIds = bindings.AllIds().Concat(["attack"]);

Dictionary<string, long> Stats(long hp, long speed, long attack) =>
    new(StringComparer.Ordinal)
    {
        ["max_hp"] = hp,
        ["physical_defense"] = 0,
        ["magical_defense"] = 0,
        ["crit_rate"] = 0,
        ["crit_damage"] = 15_000,
        ["damage_dealt"] = 0,
        ["damage_taken"] = 0,
        ["move_speed"] = speed,
        ["attack"] = attack,
    };

var strike = new SkillDefinition(
    "strike",
    durationFrames: 1,
    cooldownFrames: 15,
    [
        new SkillDamageEffect(
            0,
            new SkillTarget(SkillTargetKind.CurrentTarget, TargetRelation.Enemies, Fix64.FromInt(2)),
            DamageType.Physical,
            Flat: 0,
            AttributeId: "attack",
            Ratio: 10_000,
            CanCrit: true),
    ],
    category: SkillCategory.Weapon);

var chase = new BrainDefinition("chase", BrainKind.ChaseAttack);

var hero = new CharacterTemplate(
    "hero",
    Fix64.FromRatio(1, 2),
    Stats(hp: 200, speed: 4, attack: 20),
    skillIds: ["strike"]);

var monster = new CharacterTemplate(
    "monster",
    Fix64.FromRatio(1, 2),
    Stats(hp: 80, speed: 3, attack: 10),
    skillIds: ["strike"],
    brainId: "chase");

var battleConfig = new BattleConfig(
    attributeIds,
    bindings,
    [hero, monster],
    skillDefinitions: [strike],
    brainDefinitions: [chase]);

var map = new MapConfig(
    new FixAabb(P(0, 0), P(40, 40)),
    spawnPoints: [new SpawnPoint("gate", P(20, 8), "wave1")],
    playerSpawnMarkers: [new PlayerSpawnMarker("p1", P(8, 8))]);

var spawnTable = new SpawnTable(
[
    new SpawnTableEntry("wave1", "monster", factionId: 2, frame: 30, count: 1, brainId: "chase"),
]);

var battle = new BattleInstance(battleConfig, map, seed: 1, maxFrames: 30 * 60, spawnTable);

var start = battle.GetPlayerSpawnMarkers()[0].Position;
battle.Enqueue(new SpawnCharacterCommand(
    0,
    "hero",
    FactionId: 1,
    start,
    PlayerControlled: true));

for (var i = 0; i < 120; i++)
{
    // 命令的 Frame 必须 >= 当前帧。Tick 结束后 CurrentFrame 会 +1。
    if (battle.CurrentFrame >= 1)
    {
        battle.Enqueue(new MoveDirectionCommand(
            battle.CurrentFrame,
            CharacterId: 1,
            Direction: new FixVector2(Fix64.One, Fix64.Zero)));
    }

    foreach (var e in battle.Tick())
    {
        switch (e)
        {
            case CharacterSpawnedEvent spawned:
                Console.WriteLine($"[{spawned.Frame}] spawn {spawned.CharacterId} {spawned.TemplateId}");
                break;
            case DamageEvent dmg:
                Console.WriteLine($"[{dmg.Frame}] {dmg.SourceId} -> {dmg.TargetId} dmg={dmg.Amount}");
                break;
            case DiedEvent died:
                Console.WriteLine($"[{died.Frame}] {died.CharacterId} died");
                break;
            case FactionWipedEvent wiped:
                Console.WriteLine($"[{wiped.Frame}] faction {wiped.FactionId} wiped");
                break;
            case BattleTimeoutEvent:
                Console.WriteLine("timeout");
                break;
            case CommandRejectedEvent rejected:
                Console.WriteLine($"reject {rejected.CommandType}: {rejected.Reason}");
                break;
        }
    }
}
```

引用项目：`src/BattleCore/BattleCore.csproj`（`net8.0`）。跑测试：

```powershell
dotnet test BattleCore.slnx --configuration Release
```

---

## 3. 原理与帧循环

宿主拥有时钟。推荐墙钟映射：每 1/30 秒调用一次 `Tick()`；也可以更快连 Tick 做加速，或暂停时不 Tick。

```
创建 BattleInstance（配置只读，战斗中不热更）
        │
        ▼
每帧：  把「这一逻辑帧」的 Command Enqueue  ──►  Tick()  ──►  消费 Event
        │                                              │
        │                                              └── Query（只读快照）
        ▼
超时 / 阵营清空 / 你自己的胜负条件 ──► 停 Tick，丢掉实例即可
```

### 3.1 `Tick()` 在干什么

每一逻辑帧顺序固定（阶段内按 `entityId` 升序）：

1. 把**刷怪表**里「本帧该出」的单位变成 `SpawnCharacterCommand` 入队  
2. 消费**本帧**命令（含上面的表刷怪，以及你事先 Enqueue 的外部命令）  
3. **Brain** 产出命令并立刻消费（与玩家同构）  
4. 移动（缩短位移消穿透，不滑墙、不互推）  
5. 技能时间轴  
6. 投射物 / 区域效果  
7. Buff（含周期效果）  
8. 伤害/治疗收尾  
9. 死亡、复活倒计时、尸体消失  
10. 靠近拾取
11. 若到达 `maxFrames`，发出 `BattleTimeoutEvent`
12. `CurrentFrame++`

Brain 看到的是「本帧外部命令已经落地、本帧移动还没发生」的世界。`PlayerControlled == true` 的角色**不跑 Brain**。

### 3.2 命令帧戳

- `Enqueue` 的 `command.Frame` **早于** `CurrentFrame`：丢弃，并在**下一帧**的事件里出现 `CommandRejectedEvent`（`Enqueue` 返回 `false`）。  
- 等于当前帧：本帧 `Tick` 会执行（若你在 `Tick` 之前入队）。  
- 大于当前帧：排队等到那一帧。  
- 同一帧多条命令：先按命令上的角色 id，再按入队序号。  
- **同一帧里**，你在 `Tick` 前入队的外部 Spawn，会排在刷怪表 Spawn **之前**（玩家开局用这个约定）。

`Tick()` 返回后 `CurrentFrame` 已经变成下一帧。下一轮命令请用 `battle.CurrentFrame`，不要沿用旧数字。

核不是回滚净代码：迟到命令不会补进过去。

---

## 4. 配置怎么切

三份配置可以独立替换：同一套战斗表配不同地图，或同一地图换刷怪节奏。

| 对象 | 职责 |
|------|------|
| `BattleConfig` | 属性声明、战斗绑定、角色模板、技能、Buff、弹体、区域、Brain、防御 K / 最大减免 / 保底伤害、默认尸体时长与复活延迟 |
| `MapConfig` | 矩形边界、地形 AABB、刷怪点、玩家出生标记、空间模式及横版重力参数 |
| `SpawnTable` | 刷谁、哪一帧、几只、可选覆盖 Brain；由地图上的点引用 |

创建：

```csharp
new BattleInstance(battleConfig, map, seed, maxFrames, spawnTable);
```

`spawnTable` 可省略（空表）。非法引用（未知模板、未知 Brain、点位出界、点引用了不存在的表条目）在构造时抛 `ConfigurationException`。

JSON：

```csharp
using BattleCore.Serialization;

var bundle = BattleJson.LoadBundle(File.ReadAllText("samples/arena.bundle.json"));
var battle = bundle.CreateBattle();
// 横版推图：samples/sideview.bundle.json（spatialMode SideView，出生点 Y = 地板 + 半径）
```

空间量必须是整数米、`{"n":1,"d":2}` 或 `{"raw":32768}`，**不要写 JSON 浮点**（如 `0.5`）。枚举用 C# 标识符（`True`、`ExplicitTarget`、`ChaseAttack`）；效果 `kind` 用 camelCase（`damage`、`applyBuff`）。

战斗中**不能**换表。要改数值就新建一场。

---

## 5. 配表要点

### 5.1 属性与绑定

属性 ID 是字符串，由你声明。管道不会猜英文名，必须在 `CombatAttributeBindings` 里指出这 8 个槽位分别对应哪个 ID：

`MaxHp`、`PhysicalDefense`、`MagicalDefense`、`CritRate`、`CritDamage`、`DamageDealt`、`DamageTaken`、`MoveSpeed`。

每个角色模板都要带齐这 8 个的基础值，缺则校验失败。可以额外声明 `attack`、`spell_power` 等，给技能基数用。

**不是属性：** `CurrentHP`、护盾额度、`Lives`（复活次数）。

`MoveSpeed` 的基础值按 **米/秒** 理解（内部再除以 30 得到每帧位移）。百分比用万分比。修饰顺序见设计文档：`(base + Σflat) * Π(1+pct) + Σfinal`。

伤害管道见 [第 5 节需求](v1-requirements.md) 与 [ADR 0006](adr/0006-fixed-damage-pipeline.md)。这里只记宿主要知道的：技能配 `flat + attr * ratio / 10000`，能否暴击、伤害类型（Physical / Magical / True）写在效果上。

### 5.2 角色模板

- `Radius`：占位圆（米）。  
- `Targetable` / `Damageable`：选敌与能否受伤。  
- `BlocksMovement`：是否参与敌对硬挡；商人/待救通常 `false`。同阵营可重叠。  
- `Lives`：致死时若 > 0 则扣次数并倒计时站起，否则进入 Dead。
- `CanBeRevived`：默认 `false`。为 `false` 时 Dead 尸体按 `BattleConfig.CorpseDurationFrames` 消失；为 `true` 时覆盖该计时，尸体一直保留到复活。
- 致死时清普通 Buff、护盾、当前技能/移动/软锁/Carry；保留 PreventRevive Buff、技能 CD 与 ModifierSource。
- `SkillIds`：该模板能放的技能，**顺序 = Brain 进攻技能优先级**。  
- `BrainId`：可选；生成时可用命令覆盖。

玩家是否接受你灌的操作，看 Spawn 时的 `PlayerControlled`，**和从哪刷出来无关**。刷怪表永远刷 `PlayerControlled = false`。开局玩家约定用外部 `SpawnCharacterCommand`；`PlayerSpawnMarker` 只给查询，核不会自动刷玩家。

召唤物也是 Character。技能效果 `SkillSpawnCharacterEffect` 会按选敌位置生成（默认在施法者处），继承施法者阵营，可选 `BindOwner`（默认 true）和 `BrainId`。`FollowOwner` 用 `OwnerId` 跟随主人。同阵营可重叠，所以在自己脚下召唤通常能成功；点在地形或敌对占位上则会跳过。

### 5.3 技能

时间轴：`durationFrames` 内，效果写在 `Frame`（`0 .. duration-1`）。无引导/按住引导。

选敌：自身、显式目标、当前软锁、点圆、自身圆、自身扇、点环、自身朝向攻击盒（`BoxAtCaster`）。过滤：敌/友/自身/全部。扇形需要非零 `Direction`；点圆/环需要地图内 `TargetPoint`；攻击盒用当前 Facing，不强制方向。`Radius` 是盒沿朝向的长度，`Height` 是垂直于该轴的全高。普通选敌跳过 Dead 和复活倒计时。`SkillTarget.CorpsesOnly`（如 `ExplicitDeadAlly` / `CurrentDeadAlly`）只选 `CanBeRevived=true` 且当前未被禁止复活的 Dead；可对这类尸体 `SetTarget`，软锁在其可复活期间不会被清掉。复活倒计时中仍不可选。

已实现的效果：伤害、治疗、护盾、上 Buff、创建投射物、创建区域、冲锋位移、召唤 Character、StartCarry / Throw / Drop、复活友方尸体（`SkillReviveEffect`）。Buff 可用 `PreventReviveBuffEffect` 禁止复活；该效果随 Buff 跨死亡保留，直到 Buff 到期或被移除。

`SkillDashEffect(Frame, Distance)` 使用施法 `Direction` 冲固定距离，朝 `TargetPoint` 冲但不越过该点，或朝 `TargetId` 在效果帧的位置冲固定距离。冲锋复用普通移动的地图、地形和敌对占位阻挡，遇阻缩短，不滑墙、不穿过中途障碍。Root 会阻止施放/生效，被 Carry 的单位不能自行冲锋；载体冲锋时被携带者同步跟随。位移产生 `CharacterMovedEvent`，效果生效产生以施法者为目标的 `SkillHitEvent`。

`SkillKnockbackEffect` 把选中目标推离施法者（或 `AlongCastDirection` 时沿施法方向）；`SkillPullEffect` 拉向施法者，在双方半径相切处停下，不穿进施法者占位圆。俯视二者都缩短路径。横版把位移向量拆成当帧水平推挤，竖直分量按 `sqrt(2 * gravity * |Y|)` 变成弹道速度，落地前保持浮空。Root 或被携带者不移动；若被推的是载体，被携带者同帧保持 `CarryOffset` 跟随。

技能可设 `Range`：`> 0` 时 `CastSkill` 校验指向点或显式/当前目标（目标型加目标半径）。未配或 ≤0 不限制。自身、施法者中心范围、只有方向没有点的冲锋/弹体不检水平距离。超距拒绝不扣资源、不进 CD。

带非零 `Direction`、`TargetPoint` 或 `TargetId` 的施法在校验通过、时间轴开始时立刻改 `Facing`（step 0 = +X，逆时针 1024 档），之后取消不回滚。自身技能或没有可用瞄准方向的施法不改朝向。仍可用 `SetFacing`。`SideView` 把瞄准、扇形、冲锋、弹体和 `SetFacing` 夹成左右（0 或 512）；纯竖直瞄准保持当前左右。击退/拉人/投掷仍用未夹的方向，所以向上施法仍能打飞。见 [ADR 0046](adr/0046-side-view-facing-left-right.md)。

冷却按逻辑帧。技能可声明一种或多种资源消耗：`BattleConfig` 声明资源 ID 与上限，模板给初值。起手时原子扣除，不够则拒绝且不进 CD。死亡保留当前值。回复用 `GrantResourceCommand`、即时拾取或周期资源 Buff。默认真言类会被沉默阻止起手，武器类会被缴械阻止起手（`SkillCategory`）。眩晕取消当前施法；受伤不打断。V1 不允许取消后摇，整个时间轴结束前不能再次起手。`CanMoveWhileCasting` 控制前摇/后摇能否走。

投射物 V1：直线匀速、碰撞圆、寿命或出界销毁、**默认撞 Terrain AABB 后销毁**（`CollidesWithTerrain: false` 可穿墙）、穿透次数（0 = 第一次有效命中后销毁）。无跟踪、无弹射。点选/自身范围技能不做墙体视线。区域：地面或跟随目标，按间隔结算；生成时快照来源贡献并自带 RNG，创建者死亡后仍继续。跟随目标进入 Dead 或消失则销毁。

### 5.3.1 Carry

独立关系，不是 Buff。V1：仅同阵营、一名载体同时只带 1 个。被带者移动权交给载体（自己的移动命令不生效，Brain 也不跑）。移动阶段按开始携带时记下的世界坐标偏移跟随。

- `SkillStartCarryEffect`：从选敌结果里抓第一个合法目标。
- `SkillDropCarryEffect`：就地放下。
- `SkillThrowCarryEffect`：沿施法 `Direction` 或朝 `TargetPoint` 做一次缩短位移，然后结束携带。投掷技能施法时必须带方向或点。

冻结规则是载体眩晕或任一方死亡时掉落；定身只阻止移动，沉默/缴械不拆关系。被携带者不参与占位阻挡。快照字段：`CarryingId` / `CarriedById`。事件：`CarryStartedEvent`、`CharacterThrownEvent`、`CarryEndedEvent`。

载体因冲锋、击退、拉人等技能效果位移时，被携带者同帧保持偏移跟随；携带期间不单独碰撞，结束关系时再校正落点。

### 5.4 Buff

Tag + 叠层（`Unique` / `Refresh` / `Stack` / `Independent`）+ 效果列表。到期帧上恰好到期的周期效果先结算再移除，`Duration == Interval` 的 DoT 恰好一跳（区域同口径，[ADR 0052](adr/0052-projectiles-snapshot-source-and-expiry-frame-rule.md)）。`Damageable = false` 的模板拒绝含控制的 Buff；眩晕/定身会清空当前移动意图并拒绝期间的移动命令，控制结束不会自动续走。已实现：属性修饰、周期伤/治/资源、控制（晕、沉默、定身、缴械）、免疫 Tag、替换 Tag、按 Tag 驱散、`ImmuneToDamageBuffEffect`（跳过伤害管道，治疗与控制仍生效）、`UntargetableBuffEffect`（普通选敌与弹/区域跳过；尸体复活选敌不变）、战斗触发（造成伤害 / 受到伤害 / 击杀 / 起手施法）。触发动作是治疗、护盾、资源、上 Buff，不会再开技能时间轴，也不会嵌套再触发；伤害先结算并出 `DamageEvent`，再跑双方触发，然后才判定死亡，所以同帧治疗可以救命。护盾是独立 ShieldLayer，主要由 `GrantShield` / 技能护盾效果创建；移除来源 Buff 不自动拆盾。

### 5.5 Brain

写在 `BattleConfig` 里，模板或 Spawn 引用其 ID。

| 种类 | 行为 |
|------|------|
| `Idle` | 不发命令 |
| `ChaseAttack` | 仇恨半径内锁最近敌对；有就绪且在射程内的进攻技能就放，否则走近。可配 `LeashRadius`：超出则清目标并回出生点，返回期间不选敌、不施法，不回血、不驱散；回程被挡住（例如敌人站在出生点上）视为已到达，恢复选敌（[ADR 0051](adr/0051-leash-return-blocked-counts-as-arrived.md)） |
| `HoldPosition` | 同样选敌和施法，不追；若被位移超出 leash 则回家 |
| `FollowOwner` | 靠近 `OwnerId`；自己或主人已有敌对目标、或仇恨内有敌人时切到 ChaseAttack（不使用出生点 leash） |

射程：技能声明了正 `Range` 时用它走近；否则区域半径优先，显式/当前目标半径为 0 时用 `AttackRange`。只对「敌对/任意」的进攻效果开火，不会拿自我治疗当主循环。`PlayerControlled` 不跑 Brain，也不 leash。

### 5.6 地图与刷怪

单位不能走出 `MapConfig` 边界；弹体出界或默认撞地形后销毁。地形是静态 AABB。

`MapConfig` 缺省为 `SpatialMode.TopDown`。横版地图使用
`SpatialMode.SideView`，并提供正数 `SideViewSettings(Gravity,
MaximumFallSpeed, JumpSpeed)`；单位 X 方向读取 `MoveDirection`，Y 分量
被忽略，重力由核自动推进。落地角色用 `JumpCommand` 起跳。查询
`CharacterSnapshot.Grounded` 和 `VerticalVelocity` 可驱动落地/腾空表现。
横版击退与冲锋见 [ADR 0045](adr/0045-side-view-forced-displacement.md)：水平当帧推挤，竖直为弹道冲量。
横版朝向见 [ADR 0046](adr/0046-side-view-facing-left-right.md)：逻辑上只有左右；走动会改朝向，俯视不会。
横版施法射程与 Brain 的仇恨、攻击距离只比较 X，但目标还须在
`SideViewSettings.VerticalReach`（JSON `verticalReach`，默认 2 米）的竖直带内，
否则视为不可及——没有形状的单体技能不会隔着楼板命中，Brain 也不会站在平台下空放
（[ADR 0049](adr/0049-side-view-vertical-reach.md)）。leash 与跟随距离仍只比较 X。
圆/环/扇/盒命中、占位和弹体碰撞仍是 2D（[ADR 0047](adr/0047-side-view-gameplay-range-is-horizontal.md)）。
接地 = 向下极短探针被地板、地形（含角）或阻挡单位挡住（[ADR 0050](adr/0050-side-view-grounding-is-a-blocked-probe.md)），
所以站在敌人头上也算接地、能起跳；对方走开就掉下来。
推图样例 [samples/sideview.bundle.json](../samples/sideview.bundle.json) 用地图 `Bounds.Min.Y` 当地板、空 `terrain`，出生点 Y 为 `Min.Y + radius`。
基础横版仍使用圆形角色占用与实心 AABB，不含单向台、斜坡和移动平台。

`SpawnPoint.TableEntryId` 指向 `SpawnTableEntry`。条目的 `Frame` 从战斗开始算（第 0 帧是第一次 `Tick`）。同一点 `Count > 1` 会在同一位置刷多只（同阵营可重叠）。

清波再刷、进区域再刷：**不做**。

### 5.7 拾取

靠近判定在核内做，阶段在伤害与生死之后。同帧按 Pickup `entityId` 升序；每个角色每帧最多捡 1 个；同一物品重叠时取角色 `entityId` 最小者。

- `PickupKind.Instant`：核立刻生效（治疗、加 Lives、永久属性修饰、Buff、护盾、资源）。
- `PickupKind.Stash`：只发 `PickedUpEvent` 并移除场上物体；背包、拒收、再掉出由外部处理。

谁能捡由 `PickupPickerKind` 决定（默认 `Any`）：`PlayerControlled` 仅主角标记；`SameFaction` 仅与 `SpawnPickupCommand.FactionId` 相同的阵营（盖拾取方阵营，不要盖死人）。靠近判定仍在核内；不合格角色直接跳过。

用 `SpawnPickupCommand` 生成。查询 `GetPickups()`。换武器不在核内解释，做成 Stash 或只靠 `PickedUpEvent.DefinitionId` 让宿主换装。

---

## 6. 输入、输出、查询

表现层以 **Event 为主、快照为辅**。不要每帧只靠全量 Query 驱动动画。

### 命令（均带逻辑帧戳）

移动：方向 / `MoveTo` / 停止。`MoveTo` 直线走，无寻路；被挡无法接近终点则取消；终点必须在地图边界内，否则拒绝（技能 `TargetPoint` 同理）。方向向量任意长度都安全。横版另有 `JumpCommand`（须接地）。  
朝向：`SetFacing`，或带方向/目标点/显式目标的 `CastSkill` 起手成功时自动转向（取消不回滚）。受伤不转向。全圆 1024 档，step 0 朝 +X。横版只使用 0 / 512，走动会改朝向。  
目标：`SetTarget` 软锁；技能是否打它由选敌器决定。  
战斗：`CastSkill`、`DealDamage`、`Heal`、`GrantShield`、`GrantResource`、Buff 添加/移除/驱散、`Revive`（允许复活且未被禁止复活的友方 Dead）。
生成：`SpawnCharacter`（可 `BrainId`、`OwnerId`、覆盖初始 HP、开局 `ModifierSources`）、`SpawnPickup`。
装备等局外修正：`ApplyModifierSource` / `RemoveModifierSource`。同一 `SourceId` 再 Apply 会先卸旧组再挂新组。核不算装备表；宿主把算好的 Flat/Percent/Final 灌进来。即时拾取仍可加匿名永久修饰，与具名来源互不替代。

### 事件

生成/移动/朝向/目标、技能开始/命中/结束、弹体与区域、Buff 与控制、伤害/治疗/护盾、携带开始/投掷/结束、拾取生成/`PickedUp`、修饰来源挂上/卸下、死亡/复活/尸体消失、阵营清空、超时、命令拒绝。`FactionWipedEvent` 只记录该阵营第一次没有 Alive / Reviving Character；可技能复活的 Dead 不阻止该事件，整场每阵营至多一次，宿主不要直接把它当作不可逆失败。

### 查询

`GetCharacter` / `GetCharacters`、`GetAttribute`、`GetResource`、`GetProjectiles`、`GetAreaEffects`、`GetPickups`、`GetSpawnPoints`、`GetPlayerSpawnMarkers`。快照含 HP、护盾、Lives、资源、Buff、CD、控制、是否可操作、当前修饰来源 id 等。查询**不得**改模拟。

---

## 7. 多实例与线程

同进程可以有多场 `BattleInstance`，彼此不要共享可变状态。不同实例可以并行 `Tick`。**同一实例**上的 `Enqueue` / `Tick` / Query 必须串行。核内不拆线程跑子系统。

---

## 8. 现在能做什么 / 先别指望什么

**已实现（可接入宿主）：** 定点空间与地形阻挡、命令事件查询、移动与软锁、属性修饰、完整伤害/治疗/护盾/暴击/防御管道、Lives 与尸体复活命令、技能时间轴（圆/扇/环/冲锋/击退/拉人）、直线穿透弹（可撞地形；发射时快照来源，施法者死亡后照常飞行）、区域效果（来源快照与独立 RNG）、Buff（含战斗触发、伤害免疫、不可选中）与控制、四种 Brain（含 leash）、技能召唤 Character、Carry、即时/局外拾取、技能复活友方尸体、具名 Modifier 来源、技能资源池、按帧刷怪表、JSON 样例加载器、`maxFrames` 超时。可选 `SideView`（重力、跳跃、左右朝向；样例 `samples/sideview.bundle.json`）。

**V1 写了但还没做：** 战斗级 RNG 用于随机组成（刷怪当前是全确定表）。进度总表见 [status.md](status.md)。

**V1 明确不做：** 引导施法、A*、滑墙互推、跟踪弹/弹射、清波/进区刷怪、外交矩阵、可破坏地形、背包真源、FrameHash/回放、把 Godot 放进本仓。

规模软顶大约：角色 64、弹体 256、区域 64、地形 128。超出不承诺 30 Hz；幸存者海量弹幕不是这版目标。

---

## 9. 还想往下看

- 外围 agent 入口（职责、只读核、阅读顺序）：[host-handoff.md](host-handoff.md)
- 实现进度（已做 / 已设计未编码 / 未决）：[status.md](status.md)
- 规则与非目标：[v1-requirements.md](v1-requirements.md)
- 术语：[CONTEXT.md](../CONTEXT.md)
- 为什么这样切：[docs/adr/](adr/)  
- 行为样例：`tests/BattleCore.Tests/`（比文档更接近「现在代码实际怎么跑」）

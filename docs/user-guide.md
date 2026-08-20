# Battle Core 使用手册

面向要**接入宿主**（自己的游戏、工具、测试）的人：怎么创建一场战斗、怎么灌输入、怎么读输出、配置从哪几块拼起来。

术语和冻结规则以 [CONTEXT.md](../CONTEXT.md)、[V1 需求](v1-requirements.md) 为准。伤害公式、选敌几何、ADR 不在这里重复，避免两处各改一版。

当前配置入口是**内存对象**（C# 构造）。没有 JSON 加载器；你可以用任何格式在核外反序列化，再 new 这些类型。

---

## 1. 这是什么

Battle Core 是 **.NET 8、无引擎依赖** 的 2D 实时战斗**逻辑核**。它推演移动、技能、伤害、Buff、弹道、区域、AI 和刷怪，不负责渲染、动画、音效、网络传输、背包经济。

- 每次 `Tick()` 恰好前进 **1 逻辑帧**，固定 **30 Hz**（1 帧 = 1/30 秒）。
- 空间用 `Fix64`（Q16.16），**1 单位 = 1 米**。不要用 IEEE 浮点做逻辑判定。
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
10. 若到达 `maxFrames`，发出 `BattleTimeoutEvent`  
11. `CurrentFrame++`

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
| `MapConfig` | 矩形边界、地形 AABB、刷怪点、玩家出生标记 |
| `SpawnTable` | 刷谁、哪一帧、几只、可选覆盖 Brain；由地图上的点引用 |

创建：

```csharp
new BattleInstance(battleConfig, map, seed, maxFrames, spawnTable);
```

`spawnTable` 可省略（空表）。非法引用（未知模板、未知 Brain、点位出界、点引用了不存在的表条目）在构造时抛 `ConfigurationException`。

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
- `SkillIds`：该模板能放的技能，**顺序 = Brain 进攻技能优先级**。  
- `BrainId`：可选；生成时可用命令覆盖。

玩家是否接受你灌的操作，看 Spawn 时的 `PlayerControlled`，**和从哪刷出来无关**。刷怪表永远刷 `PlayerControlled = false`。开局玩家约定用外部 `SpawnCharacterCommand`；`PlayerSpawnMarker` 只给查询，核不会自动刷玩家。

召唤物也是 Character。技能效果 `SkillSpawnCharacterEffect` 会按选敌位置生成（默认在施法者处），继承施法者阵营，可选 `BindOwner`（默认 true）和 `BrainId`。`FollowOwner` 用 `OwnerId` 跟随主人。同阵营可重叠，所以在自己脚下召唤通常能成功；点在地形或敌对占位上则会跳过。

### 5.3 技能

时间轴：`durationFrames` 内，效果写在 `Frame`（`0 .. duration-1`）。无引导/按住引导。

选敌：自身、显式目标、当前软锁、点圆、自身圆、自身扇、点环。过滤：敌/友/自身/全部。扇形需要非零 `Direction`；点圆/环需要地图内 `TargetPoint`。普通选敌跳过 Dead 和复活倒计时。`SkillTarget.CorpsesOnly`（如 `ExplicitDeadAlly` / `CurrentDeadAlly`）只选 `CanBeRevived=true` 且当前未被禁止复活的 Dead；可对这类尸体 `SetTarget`，软锁在其可复活期间不会被清掉。复活倒计时中仍不可选。

已实现的效果：伤害、治疗、护盾、上 Buff、创建投射物、创建区域、冲锋位移、召唤 Character、StartCarry / Throw / Drop、复活友方尸体（`SkillReviveEffect`）。Buff 可用 `PreventReviveBuffEffect` 禁止复活；该效果随 Buff 跨死亡保留，直到 Buff 到期或被移除。

`SkillDashEffect(Frame, Distance)` 使用施法 `Direction` 冲固定距离，朝 `TargetPoint` 冲但不越过该点，或朝 `TargetId` 在效果帧的位置冲固定距离。冲锋复用普通移动的地图、地形和敌对占位阻挡，遇阻缩短，不滑墙、不穿过中途障碍。Root 会阻止施放/生效，被 Carry 的单位不能自行冲锋；载体冲锋时被携带者同步跟随。位移产生 `CharacterMovedEvent`，效果生效产生以施法者为目标的 `SkillHitEvent`。

冷却按逻辑帧。默认真言类会被沉默打断，武器类会被缴械打断（`SkillCategory`）。眩晕取消当前施法。`CanMoveWhileCasting` 控制前摇/后摇能否走。

投射物 V1：直线匀速、碰撞圆、寿命或出界销毁、穿透次数（0 = 第一次有效命中后销毁）。无跟踪、无弹射。区域：地面或跟随目标，按间隔结算。

### 5.3.1 Carry

独立关系，不是 Buff。V1：仅同阵营、一名载体同时只带 1 个。被带者移动权交给载体（自己的移动命令不生效，Brain 也不跑）。移动阶段按开始携带时记下的世界坐标偏移跟随。

- `SkillStartCarryEffect`：从选敌结果里抓第一个合法目标。
- `SkillDropCarryEffect`：就地放下。
- `SkillThrowCarryEffect`：沿施法 `Direction` 或朝 `TargetPoint` 做一次缩短位移，然后结束携带。投掷技能施法时必须带方向或点。

载体受到任何控制（晕/沉默/定身/缴械）或任一方死亡时掉落。被携带者不参与占位阻挡。快照字段：`CarryingId` / `CarriedById`。事件：`CarryStartedEvent`、`CharacterThrownEvent`、`CarryEndedEvent`。

### 5.4 Buff

Tag + 叠层（`Unique` / `Refresh` / `Stack` / `Independent`）+ 效果列表。已实现：属性修饰、周期伤/治、控制（晕、沉默、定身、缴械）、免疫 Tag、替换 Tag、按 Tag 驱散。护盾目前主要是 `GrantShield` / 技能护盾效果，不是「Buff 上挂盾」那套独立建模。

### 5.5 Brain

写在 `BattleConfig` 里，模板或 Spawn 引用其 ID。

| 种类 | 行为 |
|------|------|
| `Idle` | 不发命令 |
| `ChaseAttack` | 仇恨半径内锁最近敌对；有就绪且在射程内的进攻技能就放，否则走近 |
| `HoldPosition` | 同样选敌和施法，不追 |
| `FollowOwner` | 靠近 `OwnerId`；自己或主人已有敌对目标、或仇恨内有敌人时切到 ChaseAttack |

射程：技能上的区域半径优先；显式/当前目标半径为 0 时用 `AttackRange`。只对「敌对/任意」的进攻效果开火，不会拿自我治疗当主循环。

### 5.6 地图与刷怪

单位不能走出 `MapConfig` 边界；弹体出界销毁。地形是静态 AABB。

`SpawnPoint.TableEntryId` 指向 `SpawnTableEntry`。条目的 `Frame` 从战斗开始算（第 0 帧是第一次 `Tick`）。同一点 `Count > 1` 会在同一位置刷多只（同阵营可重叠）。

清波再刷、进区域再刷：**不做**。

### 5.7 拾取

靠近判定在核内做，阶段在伤害与生死之后。同帧按 Pickup `entityId` 升序；每个角色每帧最多捡 1 个；同一物品重叠时取角色 `entityId` 最小者。

- `PickupKind.Instant`：核立刻生效（治疗、加 Lives、永久属性修饰、Buff、护盾）。
- `PickupKind.Stash`：只发 `PickedUpEvent` 并移除场上物体；背包、拒收、再掉出由外部处理。

用 `SpawnPickupCommand` 生成。查询 `GetPickups()`。换武器不在核内解释，做成 Stash 或只靠 `PickedUpEvent.DefinitionId` 让宿主换装。

---

## 6. 输入、输出、查询

表现层以 **Event 为主、快照为辅**。不要每帧只靠全量 Query 驱动动画。

### 命令（均带逻辑帧戳）

移动：方向 / `MoveTo` / 停止。`MoveTo` 直线走，无寻路；被挡无法接近终点则取消。  
朝向：`SetFacing`（受伤不转向）。全圆 1024 档。  
目标：`SetTarget` 软锁；技能是否打它由选敌器决定。  
战斗：`CastSkill`、`DealDamage`、`Heal`、`GrantShield`、Buff 添加/移除/驱散、`Revive`（允许复活且未被禁止复活的友方 Dead）。
生成：`SpawnCharacter`（可 `BrainId`、`OwnerId`、覆盖初始 HP、开局 `ModifierSources`）、`SpawnPickup`。
装备等局外修正：`ApplyModifierSource` / `RemoveModifierSource`。同一 `SourceId` 再 Apply 会先卸旧组再挂新组。核不算装备表；宿主把算好的 Flat/Percent/Final 灌进来。即时拾取仍可加匿名永久修饰，与具名来源互不替代。

### 事件

生成/移动/朝向/目标、技能开始/命中/结束、弹体与区域、Buff 与控制、伤害/治疗/护盾、携带开始/投掷/结束、拾取生成/`PickedUp`、修饰来源挂上/卸下、死亡/复活/尸体消失、阵营清空、超时、命令拒绝。

### 查询

`GetCharacter` / `GetCharacters`、`GetAttribute`、`GetProjectiles`、`GetAreaEffects`、`GetPickups`、`GetSpawnPoints`、`GetPlayerSpawnMarkers`。快照含 HP、护盾、Lives、Buff、CD、控制、是否可操作、当前修饰来源 id 等。查询**不得**改模拟。

---

## 7. 多实例与线程

同进程可以有多场 `BattleInstance`，彼此不要共享可变状态。不同实例可以并行 `Tick`。**同一实例**上的 `Enqueue` / `Tick` / Query 必须串行。核内不拆线程跑子系统。

---

## 8. 现在能做什么 / 先别指望什么

**已实现（可接入宿主）：** 定点空间与地形阻挡、命令事件查询、移动与软锁、属性修饰、完整伤害/治疗/护盾/暴击/防御管道、Lives 与尸体复活命令、技能时间轴（圆/扇/环/冲锋）、直线穿透弹、区域效果、Buff 与控制、四种 Brain、技能召唤 Character、Carry（抓/带/投/放）、即时/局外拾取、技能复活友方尸体、具名 Modifier 来源（装备换装）、按帧刷怪表、`maxFrames` 超时。

**V1 写了但还没做：** JSON schema/样例加载器、技能资源消耗、Buff 战斗触发（已设计未编码）、指向技能自动改朝向、后摇取消配置、战斗级 RNG 用于随机组成（刷怪当前是全确定表）。进度总表见 [status.md](status.md)。

**V1 明确不做：** 引导施法、A*、滑墙互推、跟踪弹/弹射、清波/进区刷怪、外交矩阵、可破坏地形、背包真源、FrameHash/回放、把 Godot 放进本仓。

规模软顶大约：角色 64、弹体 256、区域 64、地形 128。超出不承诺 30 Hz；幸存者海量弹幕不是这版目标。

---

## 9. 还想往下看

- 实现进度（已做 / 已设计未编码 / 未决）：[status.md](status.md)
- 规则与非目标：[v1-requirements.md](v1-requirements.md)
- 术语：[CONTEXT.md](../CONTEXT.md)
- 为什么这样切：[docs/adr/](adr/)  
- 行为样例：`tests/BattleCore.Tests/`（比文档更接近「现在代码实际怎么跑」）

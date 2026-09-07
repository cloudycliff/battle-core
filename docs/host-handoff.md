# 宿主 / 外围 Agent 交接

给要**接入**本核、而不是改核的 agent 或人。怎么 Tick、怎么配表见 [user-guide.md](user-guide.md)。本文只定职责、仓库接法和会写错的坑。

能力是否已编码以 [status.md](status.md) 为准。规则真源是 [v1-requirements.md](v1-requirements.md)、[CONTEXT.md](../CONTEXT.md)、[adr/](adr/)。不要另写一套行为。

---

## 你做什么 / 不要做什么

**做：** 引擎或工具宿主——墙钟→`Tick`、玩家输入→Command、Event→表现、组装内存配置、背包与换装、根据事实裁定胜负并停 Tick。

**不要做：** 改本仓源码、把 Godot/Unity 类型引进核、在核里维护背包真源、用 IEEE 浮点做逻辑判定、把 `FactionWiped` 当成不可逆失败。

核缺能力时（见下文「宿主必须自己兜」）：在宿主侧绕开，或把缺口交给核仓，**不要补丁核文件**。

例外：引用裂了、未改核却编译/测试失败——停下来问人，不要顺手改规则。

---

## 仓库怎么接

本仓只含逻辑库和测试（[ADR 0017](adr/0017-core-repo-only.md)）。宿主在**另一个仓库**。

V1 推荐：两个仓**并列 clone**，宿主 csproj：

```xml
<ProjectReference Include="..\battle-core\src\BattleCore\BattleCore.csproj" />
```

目标框架 `net8.0`。不必用 git submodule。需要钉版本时记 core 的 commit SHA，或以后改 NuGet。

外围 agent **只写宿主仓**。把 `battle-core` 当只读依赖。

```powershell
dotnet test BattleCore.slnx --configuration Release
```

---

## 阅读顺序

1. 本文
2. [user-guide.md](user-guide.md)（含最小可运行例子）
3. [status.md](status.md) 的已实现 / 明确不做 / 尚未编码
4. 公开类型（不要读 `BattleInstance.*.cs` 内部阶段当 API）：
   - `src/BattleCore/Simulation/BattleInstance.cs`（`Enqueue` / `Tick` / Query）
   - `src/BattleCore/Simulation/Commands/BattleCommand.cs`
   - `src/BattleCore/Simulation/Events/BattleEvent.cs`
   - `src/BattleCore/Simulation/CharacterSnapshot.cs`
   - `src/BattleCore/Configuration/`（`BattleConfig`、`MapConfig`、`SpawnTable`、`CharacterTemplate`、`ResourceDefinition`）
   - Skill / Buff / Projectile / Area / Pickup / Brain 的 `*Definition.cs`
5. 卡住再查 [CONTEXT.md](../CONTEXT.md)。不要通读全部 ADR。

和宿主写法相关、可按需打开的 ADR：`0011`/`0038` 拾取、`0028` 装备 ModifierSource、`0039` FactionWiped、`0041` 资源。

行为样例：`tests/BattleCore.Tests/`（开局 Spawn、刷怪、Cast、拾取、ModifierSource）。

---

## 循环（必须按这个用）

1. `new BattleInstance(battleConfig, map, seed, maxFrames, spawnTable)`——配置只读，战斗中不热更。
2. 开局玩家：`SpawnCharacterCommand` + `PlayerControlled: true`。刷怪表默认不刷玩家；`PlayerSpawnMarker` 只给查询。
3. 每 **1/30 秒**（或加速连 Tick）`Enqueue` 本帧命令，再 `Tick()`，消费返回的事件。
4. 命令 `Frame` 用 **`battle.CurrentFrame`**。`Tick()` 返回后帧号已经是下一帧，不要沿用旧数字。
5. 表现跟 **Event**；Query 只做 UI/调试补量，不要每帧只靠全量快照驱动动画。
6. 同一实例上 `Enqueue` / `Tick` / Query **必须串行**。不同实例可以并行 Tick。
7. 结束：你自己停 Tick、丢掉实例。核不会自动 Destroy。

空间：`Fix64`，1 单位 = 1 米。`MoveSpeed` / 弹速是**米/秒**。战斗数值是整数；`10000` = 100%。

---

## 宿主必须自己兜（核尚未编码）

完整表见 [status.md](status.md)。接入时这些会直接碰到：

| 缺口 | 宿主怎么做 |
|------|------------|
| 刷怪表随机组成未做 | 表项表现保持确定 |

已能用、不要在宿主再实现一遍：伤害管道、资源起手消耗、射程与起手转向、弹体撞墙、击退/拉人、免疫/不可选中、出生点 leash、战斗触发、Carry、即时/Stash 拾取、按帧刷怪、ModifierSource 换装、JSON 配表加载。

核仍吃内存配置。JSON 只是附带加载器：`BattleJson`（`schemas/` + `samples/arena.bundle.json`）。你也可以继续用别的格式在核外反序列化。

---

## 会写错的坑

- **`FactionWiped`**：该阵营第一次没有 Alive/Reviving。可技能复活的 Dead **不阻止**；整场每阵营至多一次。用 Query 看场上还有谁，自己裁定胜负（[ADR 0039](adr/0039-faction-wiped-is-currently-inactive.md)）。
- **迟到命令**：`Frame` 早于 `CurrentFrame` 会丢，并在之后出现 `CommandRejectedEvent`。
- **Stash 拾取**：核只发 `PickedUpEvent` 并移除场上物体。背包、拒收、再掉出是你的事。
- **换武器**：核不算装备表。算好 Flat/Percent/Final，再 `ApplyModifierSource` / `RemoveModifierSource`。
- **`DealDamage` / `Heal` / `GrantShield` 等注入命令**：给测试/GM 用。正式战斗走技能、弹、区域、Buff。
- **Brain**：`PlayerControlled == true` 的角色不跑 Brain。`MoveTo` 被挡一帧就会取消，无寻路。
- **同帧 Spawn 顺序**：你在 `Tick` 前入队的外部 Spawn 排在刷怪表 Spawn 之前。

---

## 明确不要做的外围工作

引导施法、A*、滑墙互推、跟踪弹、清波/进区刷怪、外交矩阵、可破坏地形、核内背包、FrameHash/回放。见需求非目标与 status「V1 明确不做」。不要用每帧向下的 `MoveDirection` 冒充重力或横版；横版走核内空间后端，见 [space-modes.md](space-modes.md)。

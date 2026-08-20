# Battle Core V1 需求说明

**状态：已冻结（2026-08-19）。** 属性与伤害公式于同日增补进第 5 节并已确认。改硬约束须先改本文、[CONTEXT.md](../CONTEXT.md) 与 [docs/adr](./adr)。Command/Event 精确字段在实现时补 schema。

纯逻辑、外部 Tick 驱动的 2D 实时战斗核。不负责渲染、动画、音效、网络传输。第一版主场景为 ARPG（类暗黑）。术语以 [CONTEXT.md](../CONTEXT.md) 为准。

---

## 1. 目标与非目标

### 目标

- 用有限输入驱动一场战斗：配置、出生、操作。
- 用事件 + 只读查询输出：移动、技能、伤害、Buff、携带、拾取等。
- 角色、属性、技能、Buff、投射物、区域、Brain、地图、刷怪均可配置。
- 同进程多场战斗实例，无共享可变全局。
- 实现遵守严格确定性；V1 不交付哈希对拍与回放工具。
- 实现语言为 **C#**（目标框架 .NET 8），**零引擎依赖**（无 Godot、无 Unity）。引擎只作为宿主：创建实例、调用 `Tick`、消费 Event。
- 本仓库只包含战斗核与测试；Godot 等宿主工程不放在本仓。

### 非目标（V1）

- 表现层、锁步/回滚净代码、任意帧快照恢复。
- 引导/持续施法、A* 绕路、滑墙/互推、OBB/多边形判定。
- 幸存者海量实体与弹幕优化。
- 背包经济真源、商店、经验升级、可破坏地形。
- 阵营外交矩阵、完整关卡胜负编辑器。
- 清波刷新、走进区域刷新（只留扩展口）。
- FrameHash、命令日志回放器。

---

## 2. 运行模型

| 项 | 约定 |
|----|------|
| 推进 | 外部调用无参 `Tick()`，每次恰好 1 逻辑帧 |
| 帧率 | 30 Hz（帧长 1/30 秒） |
| 空间 | 连续 2D；Fix64（Q16.16），1 单位 = 1 米；战斗数值整数；角度 1024 档 |
| 几何 | 单位/技能：圆、扇形、环形；地形：AABB |
| 边界 | 来自 Map Config 的矩形；单位不出界；Projectile 出界销毁 |
| 随机 | 自管 PRNG；战斗一流 + 每角色战斗一流 + 每角色 AI 一流 |
| 实例 | 多实例可并行 Tick；单实例 API 必须串行 |

### 帧内阶段（固定）

1. 消费**本帧** Command（迟到丢弃，不回溯）
2. Brain 产出 Command（与玩家同构）
3. 移动（含 Carry 跟随、阻挡缩短）
4. 技能时间轴
5. Projectile / AreaEffect
6. Buff（含周期效果）
7. 伤害收尾
8. 死亡 / 复活倒计时 / 尸体销毁 / Pickup 消失等
9. 派发 Battle Event

阶段内按 `entityId` 升序。Brain 基于上一帧世界决策。

---

## 3. 对外接口（有限）

核吃已校验的内存配置，不绑死唯一文件格式；附带 JSON schema 与样例加载器。创建后不热更配置。

### 3.1 生命周期

- `CreateBattle(battleConfig, mapConfig, spawnTable, seed, maxFrames, …)`
- `DestroyBattle`
- `Tick()` → 本帧 Battle Event 列表
- 只读 `Query`（位置、朝向、属性、CD、Buff、目标、Lives、地图标记等）

### 3.2 输入 Command（带逻辑帧戳）

至少包括：

- 方向移动 / `MoveTo` / 停止移动
- `SetFacing`
- `SetTarget`
- 施放技能（含指向点/方向，按技能配置）
- 外部 `SpawnCharacter` / `SpawnPickup`（以及必要时补 Spawn 地形）
- `ApplyModifierSource` / `RemoveModifierSource`（创建时可注入；战斗中变更走同一命令管道）

`Tick` 只执行帧戳等于当前帧的命令。未来帧可排队；过去帧丢弃。

### 3.3 输出事件（最小集）

`Spawned`、`Moved`、`FacingChanged`、`SkillStart` / `SkillHit` / `SkillEnd`、`Projectile*`、`AreaEffect*`、`BuffAdd` / `BuffRemove`、`Damage`、`Heal`、`ShieldChange`、`Died`、`ReviveStart` / `Revived`、`CorpseDespawned`、`CarryStart` / `Throw` / `CarryEnd`、`PickedUp`、`FactionWiped`、`BattleTimeout`、控制态变化。

表现层以事件为主，不以全量快照为主。

### 3.4 胜负

核不裁定胜负、不自动销毁。提供死亡、阵营清空、`maxFrames` 超时等事实。结束由外部决定。

---

## 4. 实体

| 种类 | 职责 |
|------|------|
| Character | 玩家、怪、召唤物、商人、待救 NPC 等 |
| Projectile | 飞行物 |
| AreaEffect | 地面或跟随区域 |
| Terrain | 静态 AABB 墙；不可破坏 |
| Pickup | 可靠近拾取 |

召唤物是独立 Character，可挂 Brain。任意 Character 都可被玩家 Command 或 Brain 驱动（由 `Player-Controlled` 等标记区分）。

### 4.1 阵营与阻挡

- 同阵营可重叠；敌对圆不可重叠。
- 所有需挡地形的单位不可穿地形 AABB。
- 非法移动：缩短/取消位移；不滑墙、不互推。
- `MoveTo` 直线走，无寻路；本帧因挡无法接近终点则取消该 MoveTo。
- 商人/待救等：`Targetable`、`Damageable` 独立配置；**不挡路**。
- 不可破坏墙 = Terrain，不是无血 Character。

### 4.2 朝向与目标

- Facing 独立于移动方向；`SetFacing` 或指向技能可改；受伤不改朝向。
- 软锁 `CurrentTarget`；技能是否打该目标由选敌器决定。
- 目标死亡或失效时清除软锁。

### 4.3 战斗开关

- `Targetable`：普通选敌器是否可选。
- `Damageable`：是否接受伤害与控制。
- Dead 对普通技能无效；**复活选敌器**只可选中模板允许且当前未被禁止复活的友方 Dead。
- 复活倒计时：不可选中、不受伤、不行动。

---

## 5. 属性、伤害、死亡

- 属性由配置声明（字符串 ID → 运行时整数 ID）。
- Battle Config **战斗绑定**（必填，缺则校验失败）：MaxHP、PhysicalDef、MagicalDef、CritRate、CritDamage、DamageDealt、DamageTaken、MoveSpeed。样例配置可另给 ATK、SpellPower 等，非强制。CurrentHP / Shield / Lives 不是属性。
- Modifier 固定顺序，例如 `(base + Σflat) * Π(1+pct) + Σfinal`。
- 伤害管道固定：基数 → 暴击 → 类型防御（Physical / Magical / True）→ 增减修饰 → 护盾 → HP。
- 百分比一律万分比（10000 = 100%）。
- 防御减免（非 True）：`mit = min(def * 10000 / (def + K), MaxMit)`，`damage = hit * (10000 - mit) / 10000`。`K`、`MaxMit`（默认 8000）在配置；保底伤害 1。V1 无穿透。Physical/Magical 各绑定一个防御属性 ID，共用同一套 `K`/`MaxMit`。
- 暴击：每个伤害实例独立 `roll(0..9999) < CritRate`，成功则 `hit = hit * CritDamage / 10000`；效果可关闭暴击。事件带 `wasCrit`。
- 伤害/治疗基数：`flat + attr * ratio / 10000`（每效果一个属性 ID）。治疗可暴击、不吃防御与护盾、加血可超过 MaxHP。除法向 0 截断。MaxHP 变化不自动削减 CurrentHP。`CurrentHP >= MaxHP` 视为满血。
- 出生默认 CurrentHP = MaxHP，Spawn 可覆盖。
- 伤害增减（仅伤害）：攻击者 `DamageDealt`、受击者 `DamageTaken` 两个万分比属性依次相乘；属性 ID 可配。治疗不走。
- 护盾：多层，按到期帧升序消耗，同时到期按 instanceId 升序。先盾后 HP。
- True 跳过防御减免。
- Lives：致死且 Lives>0 则扣次数并倒计时站起；否则进入 Dead。
- 普通单位按 Battle Config 的默认尸体帧销毁；模板 `CanBeRevived=true` 的单位无限保留尸体，复活资格不受该默认时长影响。
- 技能可复活允许复活的友方 Dead（如主角救宠物）。`PreventReviveBuffEffect` 跨死亡保留，在有效期间阻止复活。

---

## 6. Buff 与技能

### Buff

- Tag + 叠层规则 + 效果列表。Debuff 不是另一套类型。
- 效果：Modifier、周期伤/治、护盾、战斗触发（造成伤害 / 受到伤害 / 击杀 / 起手施法）、控制。
- 战斗触发只扫当事双方身上的 Buff，同一起因不嵌套再触发；可配是否要求实际扣盾/HP、是否仅暴击、伤害类型、内部冷却。
- V1 不做获得/失去 Buff 触发。以后若加：只匹配 BuffId 或 Tag；获得默认仅新 instance（Refresh 不触发）；失去默认不含死亡清理；仍不嵌套。自身死亡不应用「失去全部 Buff」表达。
- 控制（眩晕、沉默、定身、缴械）为独立机制，不用「速度改 0」冒充。
- 叠层：`Unique` / `Refresh` / `Stack`（有上限）/ `Independent`。

### 技能

- 时间轴：前摇 / 生效 / 后摇；无引导施法。
- 选敌：自身、当前目标、指向点/方向、圆、扇形、环形等。
- 效果过滤：敌对 / 友方 / 自身 / 全部等，按效果配置。
- 效果：伤/治、上 Buff、创建 Projectile / AreaEffect、冲锋位移、击退/拉人、召唤 Character、StartCarry / Throw / Drop、Revive。
- Projectile V1：直线匀速、碰撞圆、寿命/出界销毁、穿透次数；无跟踪、无弹射。
- 冷却与消耗以逻辑帧和整数资源计；控制可打断前摇；后摇可否取消为配置项。

### Carry

- 独立关系，不是特殊技能类，也不是仅靠 Buff 存 carrierId。
- 技能只负责开始/投掷/放下；每帧跟随由 Carry 系统做。
- V1：仅同阵营、同时 1 个；载体受控制则掉落。带示范技能。

---

## 7. Brain

- 可插拔；在 Tick 内跑，输出 Command。
- V1 内置 Brain：`Idle`、`ChaseAttack`、`HoldPosition`、`FollowOwner`（参数可配置）。
- 不在核外异步灌 AI。

---

## 8. 地图、刷怪、拾取

### 配置切分

- **Battle Config**：属性、角色模板、技能、Buff、弹体、区域、Brain。
- **Map Config**：矩形大小、Terrain、刷怪点、玩家出生标记等。
- **Spawn Table**：刷谁、何时、关联点位。

同一套战斗表可配不同地图。

### 刷怪

- 点位引用 Spawn Table。
- V1 触发：按战斗开始起算的逻辑帧。
- 扩展口（不实现）：清波后再刷、进入区域再刷。
- 静态墙/场景物随地图生成。
- 外部 Spawn 始终可用。开局玩家约定外部 Spawn；表默认不刷玩家。`Player-Controlled` 与刷法无关。

### 拾取

- 靠近拾取在核内判定（同帧多人按 Pickup `entityId`；每角色每帧默认 1 个）。
- **即时**：核内立刻生效（属性、换武器、治疗、加 Lives 等）。
- **局外物资**：只发 `PickedUp` 并移除场上物体；收不收、再掉出由外部再 Spawn。

---

## 9. 规模

设计目标（软顶）：Character ≤64，Projectile ≤256，AreaEffect ≤64，Terrain ≤128。超出不承诺 30 Hz。

---

## 10. 配置加载

- 核只跑校验后的内存表。
- Schema 校验引用完整性（技能引用的 BuffId 必须存在等）。
- JSON 样例加载器可替换；战斗中不热更。

---

## 11. V1 明确后做

- 滑墙、寻路、引导施法、区域/清波刷怪、中立外交、可破坏物。
- 空间哈希与弹体池（幸存者量级）。
- 确定性校验工具与快照恢复。

---

## 12. 尚未冻结（实现前可再定）

- Command / Event 的精确字段表（实现时出 schema）。

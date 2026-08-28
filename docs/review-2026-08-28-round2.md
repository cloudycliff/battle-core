# Battle Core 审查报告 · 第二轮（复核 + 新发现）

审查日期：2026-08-28（晚间）· 前置：[review-2026-08-28.md](review-2026-08-28.md)（第一轮，18 条）

审查方法：逐条读码复核第一轮全部关键指控 → 三路并行子代理分块深挖（技能/战斗/Buff、移动/携带/拾取/命令、配置/序列化/文档）→ 高严重度结论再逐条亲自读码验证。测试基线 `dotnet test` 123 通过 / 0 失败。

对第一轮的结论：**15 条属实**（其中 4 条系第一轮探针实证，本轮读码再确认）；**#11 撤回（误报）、#5 与 #16 降级**，修正已就地写回第一轮报告。

本文只收录**本轮新发现**，编号 N1–N20，避免与第一轮 #1–18 混淆。「验证」列注明结论来源：**已读码验证** = 本轮亲自打开代码逐行确认；**子代理确认** = 分块审查代理报告、行号与依据完整但未二次人工复读。

---

## 一、严重（可打崩 Tick / 契约硬约束被绕过）

### N1. 技能/Buff 引用模板缺失的属性 → `Tick` 抛异常 【已读码验证】

`src/BattleCore/Attributes/AttributeSet.cs:20-22,54-59` + `src/BattleCore/Configuration/BattleConfig.cs:169-177`

配置校验只检查技能引用的属性「已在全局声明」（BattleConfig.cs:173），不检查「持有该技能的模板具备该属性」；模板只被强制拥有 Bindings 属性（:117-124）。运行时：

- live 伤害/治疗路径 `ReadAttribute` → `AttributeSet.Get` 对缺失键抛 `KeyNotFoundException`（快照路径反而安全：`Contains` 过滤后按 0 处理，见 `CombatSourceSnapshot.cs`）；
- `AttributeBuffEffect` 命中缺属性目标时 `AttributeSet.Add` 抛 `ArgumentException`。

**一份能通过校验的合法配置即可打崩整场模拟，不需要任何宿主命令。** 外部 `DealDamageCommand`/`HealCommand` 同样可达。与第一轮 #2（NormalizeOrZero）同属「净逻辑核不应崩」的失败模式。

**修复建议**：双管齐下——(a) 配置校验补「模板必须具备其技能直接效果引用的属性」；(b) 运行时把 live 路径对齐快照路径语义（缺失按 0 / 跳过该 modifier），保证 Buff 打到任意目标都不崩。

### N2. `PreventRevive` 挡不住 Lives 复活 【已读码验证】

`src/BattleCore/Simulation/BattleInstance.Combat.cs:393-401,413-419,426-431`

Lives > 0 致死进入 `Reviving` 倒计时、到点 `StandUp`，全程不检查 `PreventsRevive`——而该 Buff 恰恰是死亡清理时特意保留的（:372）。CONTEXT **Revive** 明文：复活来源包括「消耗一次 Lives 后倒计时自动站起」，且 `PreventReviveBuffEffect`「在有效期间阻止复活」。实现只让技能复活的 `IsReviveTarget` 失效，Lives 路径完全绕过。

**修复建议**：`ResolveLife` 站起前检查 `PreventsRevive`；有效期间推迟站起，Buff 到期后再站（贴合「有效期间阻止」语义，不没收已消耗的 Lives）。

### N3. `BlocksMovement == false` 的单位可穿地形 AABB 【已读码验证】

`src/BattleCore/Simulation/BattleInstance.Movement.cs:175-183`

地形检测被 `blocksMovement` 门控；非阻挡单位（商人/待救）过了边界检查直接 `return true`，可穿墙、可在墙内出生。CONTEXT **Movement Blocking**：「**所有单位**不可穿地形 AABB」；**Combat Flag** 的「不参与移动阻挡」只应豁免单位间硬挡。默认模板 `blocksMovement: true`，所以现有测试从未覆盖。

### N4. Carry 结束落点不复验，可能永久卡死 【已读码验证】

`src/BattleCore/Simulation/BattleInstance.Carry.cs:191-204`

`RelocateIfInvalid` 发现落点非法时无条件 `carried.Position = carrier.Position`，不再验证该点对被携带者（半径更大 / 阵营不同）是否合法。落进地形重叠后，`FindMovementFraction` 从非法起点采样恒返回 0，单位挪不出来。违反 ADR 0004「结束关系时校正合法落点」。

### N5. 同帧效果列表顺序被 Tick 阶段拆解颠倒 【已读码验证】

`src/BattleCore/Simulation/BattleInstance.Skills.cs:416-467` + `src/BattleCore/Simulation/BattleInstance.cs:95-99`

同一效果列表里，伤害/治疗经 `QueueCombat` 排队到 `ResolveCombat` 收尾阶段，Buff/护盾却在技能阶段立即生效。配置「先伤害、后上易伤」实际按「易伤已生效」结算；「先伤害、后上盾」则盾会吃到本应先落地的伤害。弹体/区域的 `ApplyHitEffects` 同理。技能定义明确按帧内索引排序执行，设计者按列表顺序配置会被静默算错。

**修复建议**：同帧内统一语义——要么 Buff/盾也排队（推荐，保持「收尾统一结算」阶段模型），要么伤害立即结算；二选一并写进 CONTEXT。

### N6. `Damageable == false` 的单位仍可被控制 【已读码验证】

`src/BattleCore/Simulation/BattleInstance.Buffs.cs:25-29`

`ApplyBuff` 只拒绝非 Alive，不检查 `Template.Damageable`。CONTEXT **Combat Flag**：Damageable =「是否接受伤害与**控制类效果**」；`v1-requirements.md` §4.3 同文。伤害路径有检查（Combat.cs:115），控制施加没有——商人/待救可被眩晕、定身、沉默、缴械。

---

## 二、中等

### N7. 角色位移离散采样可穿薄地形 【已读码验证】→ **修复阶段复核后大幅降级**

`src/BattleCore/Simulation/BattleInstance.Movement.cs:104-160`

原结论：`FindMovementFraction` 沿位移只测离散点，薄 AABB 可落在采样间隙，影响角色移动 / Dash / 击退 / 牵引全部位移。

> **降级说明（修复阶段几何推演）**：采样间距 ≤ radius/2（沿路径最多 ×√2 ≈ 0.707·radius），而每个采样点的检测圆半径就是 radius——相邻两个检测圆必然重叠，其并集完整覆盖位移走廊。任意厚度的墙（乃至任何 AABB）想同时躲开相邻两圆，与几何不可能；真实误差只是走廊边缘约 5% 半径的擦角高估/漏检小月牙。**角色位移实际穿不过地形与敌人，此条不构成隧穿缺陷**，与第一轮 #1（弹体对 Character 完全无扫掠、已实证可穿）是本质不同的两回事。原「与 #1 合并统一扫掠」的建议对角色侧撤回，采样方案保留。

### N8. 被携带者不阻挡敌对移动 【已读码验证】

`Movement.cs:187-190`。他人移动判定把 `CarriedById` 非空的单位当不存在，敌人可穿其占用圆。ADR 0004「携带期间不单独做碰撞」指 carried 自身跟随不做碰撞，不是别人可以穿体。

### N9. 弹体寿命比配置多活一帧 【已读码验证】

`Projectiles.cs:43,184-187`。到期判定在本帧移动之后（`CurrentFrame >= ExpiryFrame`），`LifetimeFrames == 1` 的弹体实际推进 2 帧（2 次位移与碰撞窗口）。AreaEffect 则是到期帧开头即销毁——同核内 Duration/Lifetime 帧语义不一致。

### N10. 眩晕/定身期间移动意图残留，控制结束自动续走 【子代理确认】

`Commands.cs:38-44` + `Buffs.cs:328-342`。控制期间移动命令仍写入 `MovementIntent`（施法命令会被拒，移动不会），移动阶段因 `!CanMove` 跳过、意图残留，控制结束后无需新命令自动续走。`HandleControlChange` 清技能/Carry 但不清移动。与 Cast 路径不对称。

### N11. 致死清护盾/软锁不发事件 【已读码验证】

`Combat.cs:387-390`。`Kill` 里 `Shields.Clear()` 与 `CurrentTargetId = null` 无对应 `ShieldChangeEvent`/`TargetChangedEvent`，而同函数里 Buff 清理走 `RemoveBuffInstance` 会发事件。宿主表现层盾量与目标显示会失同步（CONTEXT：Battle Event 是增量事实通道，「Avoid: 仅靠全量快照驱动表现」）。

### N12. 伤害管线后半程无 `checked`，极端数值静默回绕 【子代理确认】

`CombatMath.cs:17-27` + `Combat.cs:141,262`。基数路径走 `Int128` + `checked`，但防御减免 / 增伤 / 暴击乘区是裸 `long` 乘除，溢出回绕得到错误伤害且不报错——与基数路径的溢出策略不一致（ADR 0001/0006）。

### N13. 自杀致死时 Kill 触发器静默失效 【子代理确认】

`Combat.cs:210-220` + `Triggers.cs:19-21`。先 `Kill`（`CanAct` 变 false）再 `FireCombatTriggers(Kill, source, …)`，触发器入口的 `CanAct` 门禁把 `source == target` 的击杀吞掉；且致死已清普通 Buff，几乎无触发器可扫。ADR 0029 未排除自击杀。

### N14. MaxHP 等绑定属性值可为 0 或负数 【已读码验证】

`BattleConfig.cs:108-124` + `Commands.cs:138-170`。校验只要求绑定键存在，不校验数值。`max_hp: 0` 可通过加载，出生即 0 血却不死（死亡只在受伤路径判定），产生荒谬状态。

### N15. `HitShieldEffect` 缺构造期校验，负时长 = 永久盾 【子代理确认】

`Effects/HitEffect.cs:20-22`，对比 `SkillDefinition.cs:111-116`、`PickupDefinition.cs:79-84`、`BuffDefinition.cs:74-77`。技能盾/拾取盾/Buff 盾都拒绝非法值，弹体/区域的 Hit 盾不校验：`amount ≤ 0` 运行时被拒，负 `durationFrames` 却变成永久盾。同概念多套校验强度。

### N16. 超 int32 的 JSON 整数抛裸 `OverflowException` 【子代理确认】

`JsonParse.cs:107-123`。`RequireInt32` 的 `checked` 转换不在 `try/catch (ArgumentException)` 保护内，超范围数字冒出 `OverflowException` 而非统一的 `ConfigurationException`。

---

## 三、较低 / 文档 / API

### N17. JSON Schema 远松于解析器，且无 schema 校验流水线 【子代理确认】

`schemas/battle-bundle.schema.json:53-58`。schema 对 skills 只要顶层字段、buffs/projectiles 几乎任意 object；宿主若只信 schema 会放过解析器必拒的包。仓库也没有用 schema 校验样例的环节。

### N18. JSON 重复键静默「后者覆盖」 【子代理确认】

`JsonParse.ParseDocument`（`System.Text.Json` 默认行为），无重复键检测。合并出错的配置不报错。

### N19. 公共 API 面三处易误用 【子代理确认，命名项已读码验证】

- `CharacterSnapshot.Targetable`/`Damageable` 命名像模板开关，实为运行时有效态（`CharacterState.cs:105-112`）：`Targetable` = `IsSelectable`（含 Buff 与生死），`Damageable` = 模板可伤 ∧ 可行动 ∧ 非免疫。文档（`user-guide.md:272`）讲的是模板开关，宿主按模板语义读会误判。
- 命令记录体持有宿主可变列表引用（`Commands/BattleCommand.cs` 的 `SpawnCharacterCommand.ModifierSources` 等），`Enqueue` 后、`Tick` 前改列表可改变本帧行为。
- `v1-requirements.md:70-71,90` 的 API/事件名与代码不一致：文档写 `CreateBattle`/`DestroyBattle` 与缩写事件名（`SkillStart`、`BuffAdd`），代码是构造函数/丢实例与 `SkillStartedEvent` 等全名；`ResourceChangedEvent`、`BuffStackChangedEvent`、`PickupSpawnedEvent` 等文档未列。

### N20. ADR 0008 阶段列表过时 【子代理确认】

`docs/adr/0008-tick-phases-drop-late-commands.md` 写 Brain 看「上一帧世界」且缺 Spawn 入队 / Pickup / 超时阶段；实现与 CONTEXT **Tick Phase** 一致但与该 ADR 不一致，属文档漂移。

---

## 本轮核对过、未见新问题的方面

- Tick 阶段顺序与契约逐项一致；实体容器全 `SortedDictionary`，阶段内 entityId 升序
- Late Command 丢弃无 off-by-one；MoveTo 受阻取消；同阵营重叠/敌对硬挡/边界钳制主路径正确
- Carry 眩晕/死亡掉落、定身不拆、载体位移同帧跟随（ADR 0004/0037）
- CombatTrigger 嵌套禁止（`_firingCombatTriggers`）、致死保留项（PreventRevive/CD/ModifierSource）、护盾消耗序（ADR 0025）、驱散确定性、免疫整段跳过（ADR 0032）、沉默/缴械不打断（ADR 0034）
- `Fix64` 经 `Int128` + `checked`，舍入向零一致；`DeterministicRng` xorshift64* 有零态保护
- 配置引用完整性（技能→Buff/弹体/区域/召唤/Brain，拾取→Buff/资源，刷怪表→模板）；畸形 JSON → `ConfigurationException`；样例可加载开打

---

## 合并修复顺序（两轮合并，编号 = 第一轮 #n / 本轮 Nn）

1. **第一批 · 崩溃与硬 bug**：#2 NormalizeOrZero 除零兜底 → N1 属性缺失（校验 + 运行时兜底）→ #1 弹体扫掠（N7 复核后降级，角色侧撤回）→ #4(第一轮编号) 眩晕取消时间轴 → N2 PreventRevive 挡 Lives
2. **第二批 · 契约一致性**：#3 确定性黄金回归测试 → N3 地形检测移出门控 → N4 Carry 落点复验 → N5 同帧效果顺序统一 → N6 控制 Buff 检查 Damageable → #7 Relation 校验 → #8 MinimumDamage → N9 弹体寿命
3. **第三批 · 健壮性与文档**：N11 死亡清理补事件 → N12 管线 `checked` → N14/N15/N16 配置与序列化校验 → #5 资源遍历排序 → #9/N19/N20 文档修偏 → #12 Percent 先累加再乘 → N10/N13
4. **择机 · 性能与测试质量**：#13 属性缓存 → #14 地形空间索引 → #17/#18 补覆盖、合并样板

---

## 修复记录（2026-08-28 晚间，全量 134 用例绿）

| 项 | 修复方式 | 回归测试 |
|---|---|---|
| 一轮#2 | `FixVector2.NormalizeOrZero` 对亚分辨率向量（长度平方下溢为 0）返回 Zero | `Fix64Tests.NormalizeOrZero_ReturnsZeroForSubResolutionVectors`、`BattleInstanceTests.Tick_SurvivesSubResolutionMoveDirection` |
| N1 | 三处：配置校验补「模板必须具备其技能（含所生成弹体/区域 Hit 效果）引用的属性」；live 路径 `ReadAttribute` 缺失按 0（对齐快照语义）；`AttributeBuffEffect` 命中缺属性目标时跳过该 modifier；`GetAttribute` 查询缺失属性返回 null 而非抛异常 | `AttributeRobustnessTests`（3 用例） |
| 一轮#1 | 新增 `SweptGeometry`（线段-圆最近点，定点数），`AdvanceProjectiles` 改为沿本帧位移线段扫掠命中，按路径参数 t → entityId 排序结算穿透 | `ProjectileAreaTests.Projectile_FastBoltSweepsInsteadOfTunneling`（60 m/s 弹体） |
| 一轮#4 | `AdvanceSkills` 循环内与收尾均校验 `ActiveSkill` 仍是本时间轴（`ReferenceEquals`），被取消后不再执行剩余同帧效果、不再重复发 `SkillEndedEvent` | `SkillTimelineTests.SelfStun_CancelsRemainingSameFrameEffectsWithoutDuplicateEnd` |
| N2 | `ResolveLife` 站起前检查 `PreventsRevive`，有效期间推迟站起（不没收已消耗的 Lives），Buff 到期后自动站起 | `SkillReviveTests.PreventReviveBuff_DelaysLivesCountdownStandUpUntilBuffEnds` |
| 一轮#3 | 新增确定性黄金回归：同 bundle + 同脚本（移动/选目标/施法/治疗/中途出生，300 帧）跑两遍比对全事件流 | `DeterminismGoldenTests.SameBundleAndScript_ProducesIdenticalEventStream` |
| N3 | `IsPositionValid` 地形检测移出 `blocksMovement` 门控：地形挡所有单位，开关只豁免单位间硬挡 | `BattleInstanceTests.NonBlockingUnit_IgnoresEnemiesButNotTerrain` |
| N4 | `RelocateIfInvalid` 落点复验：原位 → 载体位 → 以 max(双方半径) 为步长的 8 方向确定性环形搜索（8 环），全失败才退回历史行为 | `CarryTests.DropAgainstWall_RelocatesOversizedCarriedUnitToValidSpot` |
| N7 | 复核后降级为非缺陷（见上），未改动角色位移采样 | —— |

未修项（N5、N6、N9–N20 及第一轮 #5–#18 其余项）按上方顺序排队。

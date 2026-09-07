# Battle Core 实现状态

给换会话 / 换 agent 用的进度入口。规则以 [v1-requirements.md](v1-requirements.md)、[CONTEXT.md](../CONTEXT.md)、[adr/](adr/) 为准。**代码是否已做以本页 + `tests/` 为准**，不要只读需求文头的冻结日期。

宿主 / 外围 agent 入口：[host-handoff.md](host-handoff.md)。怎么 Tick：[user-guide.md](user-guide.md)。测试表示当前代码实际行为；若测试与冻结规则冲突，视为实现或文档漂移，不自动改变规则。

最后更新：2026-09-07。

> **2026-08-28 审查与修复**：两轮全面审查见 [review-2026-08-28.md](review-2026-08-28.md) 与 [review-2026-08-28-round2.md](review-2026-08-28-round2.md)。第一批 8 项已修（弹体扫掠、NormalizeOrZero 除零、属性缺失防崩、眩晕取消时间轴、PreventRevive 挡 Lives、非阻挡单位撞地形、Carry 落点复验、确定性黄金回归测试），其余问题按 round2 文档「合并修复顺序」排队。
>
> **2026-09-07**：横版不另写战斗核。共用推演核、分叉空间后端已拍板（[ADR 0043](adr/0043-shared-kernel-split-space.md)），工作说明 [space-modes.md](space-modes.md)。V1 仍只编码俯视。
> 空间后端基础已落地：`TopDownSpatialBackend` 保持原俯视规则；
> `SideViewSpatialBackend` 支持重力、接地、水平移动、平台顶边/天花板阻挡
> 和 `JumpCommand`。`MapConfig` / JSON 支持 `spatialMode`（默认 `TopDown`）
> 及显式横版参数。规则见 [ADR 0044](adr/0044-side-view-gravity-grounding-jump.md)。

---

## 已实现（可接入宿主）

- 定点空间、地形 AABB、敌对硬挡；非法位移缩短/取消，不滑墙、不互推
- 帧戳命令、事件、只读查询；迟到命令丢弃
- 移动（方向 / MoveTo / 停止）、软锁、独立朝向 `SetFacing`
- 数据驱动属性与固定修饰顺序；具名 `ModifierSource` 成组挂卸（Spawn 可注入）
- 伤害管道、护盾、暴击、防御减免、伤害增减、治疗、Lives、尸体窗口、`CanBeRevived` 永久尸体、`PreventReviveBuffEffect`
- 技能时间轴：选敌（含尸体复活选敌）、伤/治/盾/Buff/弹/区域/召唤/Carry/复活/冲锋/击退/拉人
- 直线穿透弹（默认撞地形）、地面或跟随区域
- Buff：属性、周期伤/治/资源、控制、Tag 免疫/替换/驱散、伤害免疫、不可选中、战斗触发；时长 0 = 永久；周期伤害快照来源
- Brain：Idle / ChaseAttack / HoldPosition / FollowOwner（Chase/Hold 可 leash）
- 即时 / Stash 拾取（含拾取资格过滤）、按帧刷怪表、`maxFrames` 超时
- 技能整数资源池：起手原子消耗、死亡保留、拾取/命令/周期 Buff 回复
- AreaEffect 来源快照与独立 RNG；创建者死亡后区域仍结算
- 指向技能起手更新 Facing（取消不回滚）；`Range > 0` 时核内校验射程
- 弹体默认撞地形 AABB 销毁（可关）
- 技能击退 / 拉人（Root / 被携带不移动；载体位移同帧跟随 Carry）
- `ImmuneToDamageBuffEffect` / `UntargetableBuffEffect`
- JSON 样例加载器（`BattleJson`；战斗中不热更）
- ChaseAttack / HoldPosition 出生点 leash
- Buff 战斗触发：造成伤害 / 受到伤害 / 击杀 / 起手施法（一层、可同帧治疗救命）

---

## 已设计、尚未编码

- **横版后续能力**（[ADR 0043](adr/0043-shared-kernel-split-space.md)、[ADR 0044](adr/0044-side-view-gravity-grounding-jump.md)、[space-modes.md](space-modes.md)）：基础后端已编码；垂直击退速度冲量、自动跳台 Brain、单向台、斜坡和移动平台尚未编码。不扩大 V1 俯视承诺。宿主不得用向下移动冒充重力。格斗帧对战仍不做。

被动技能：核内就是永久 Buff + 触发或 ModifierSource；玩家侧由宿主包装。

---

## V1 写了、未设计完或未编码

| 项 | 状态 |
|----|------|
| 刷怪表战斗 RNG 随机组成 | 未做；表现全确定 |

---

## V1 明确不做

引导施法、A*、滑墙互推、跟踪弹/弹射、清波/进区刷怪、外交矩阵、可破坏地形、背包真源、FrameHash/回放、仓内 Godot、触发里造成伤害（反伤）、任意 Buff 获得/失去触发。

---

## 已拍板规则索引

- 玩家 `CastSkill` **由核校验射程**（[ADR 0030](adr/0030-skill-cast-range.md)）：技能正 `Range` 才限制；未配或 ≤0 = 不限制。
- 弹体 **默认撞地形 AABB 后销毁**（[ADR 0031](adr/0031-projectile-terrain-collision.md)），可关；点名/自身范围技能不检视线。
- **伤害免疫 / 不可选中** 为 Buff 效果（[ADR 0032](adr/0032-damage-immunity-untargetable-buffs.md)），与模板开关独立。
- **不做 GCD**（2026-08-20）：技能之间不另设公共冷却。
- **充能层数、CDR 后做**：V1 保持每技能一条 CD（即最多 1 层）。
- **受伤不打断施法**（[ADR 0034](adr/0034-damage-does-not-interrupt-cast.md)）：只有眩晕取消当前时间轴。
- **仇恨表后做**；**出生点 leash 要做**（[ADR 0035](adr/0035-spawn-leash-defer-threat.md)）：超出 `LeashRadius` 回原点、清目标，**不回血、不群体驱散**。
- **Dot 快照施法者侧**（[ADR 0036](adr/0036-dot-snapshots-source.md)）：上毒/叠层时冻结基数与 DamageDealt；目标防御与易伤每跳仍现算。Buff instance 自带 RNG。
- **技能击退 / 拉人进 V1**（[ADR 0037](adr/0037-skill-knockback-pull.md)）：核内缩短位移，Root/被携带不移动。
- **`FactionWiped` 表示当前无活动单位**（[ADR 0039](adr/0039-faction-wiped-is-currently-inactive.md)）：只检查 Alive / Reviving；可技能复活的 Dead 不阻止，宿主不得直接当作最终失败。
- **护盾为独立 ShieldLayer**（[ADR 0025](adr/0025-layered-shields.md)）：驱散来源 Buff 不自动拆盾。
- **后摇不可取消**（[ADR 0033](adr/0033-defer-charges-and-cdr.md)）：整个时间轴结束前不能起手下一技能；这不是公共 GCD。
- **Carry 仅眩晕/死亡掉落**（[ADR 0004](adr/0004-carry-relation.md)）：定身只停移动，沉默/缴械不拆关系。
- **技能整数资源池**（[ADR 0041](adr/0041-bounded-multi-resource-pools.md)）：声明上限；起手原子扣；死亡保留；复活不回满。
- **AreaEffect 快照与独立 RNG**（[ADR 0042](adr/0042-area-effect-snapshot-and-rng.md)）：生成时冻来源贡献；跟随目标 Dead/消失则销毁。
- **Carry 随载体技能位移同帧跟随**（[ADR 0004](adr/0004-carry-relation.md)、[ADR 0037](adr/0037-skill-knockback-pull.md)）：保持偏移，结束 Carry 时校正落点。
- **死亡状态清理**（[ADR 0012](adr/0012-lives-and-skill-revive.md)）：清普通 Buff、护盾、技能/移动/软锁/Carry；保留 PreventRevive、CD、ModifierSource。
- **指向技能起手转向**（[ADR 0040](adr/0040-cast-facing-updates-on-accept.md)）：Cast 校验成功即更新 Facing，之后取消不回滚。
- **共用推演核、分叉空间后端**（[ADR 0043](adr/0043-shared-kernel-split-space.md)）：俯视与横版共用伤害/Buff/时间轴；移动与碰撞按模式各写一套。V1 只实现 `TopDown`。工作说明 [space-modes.md](space-modes.md)。

---

## 待讨论

无。新缺口另开讨论并改本页。

---

## 建议阅读顺序

1. 外围接入：[host-handoff.md](host-handoff.md)
2. 本页
3. [user-guide.md](user-guide.md)
4. 相关 ADR；做横版或改移动时先读 [space-modes.md](space-modes.md) 与 [ADR 0043](adr/0043-shared-kernel-split-space.md)
5. `tests/BattleCore.Tests/`

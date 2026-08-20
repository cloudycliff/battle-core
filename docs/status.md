# Battle Core 实现状态

给换会话 / 换 agent 用的进度入口。规则以 [v1-requirements.md](v1-requirements.md)、[CONTEXT.md](../CONTEXT.md)、[adr/](adr/) 为准。**代码是否已做以本页 + `tests/` 为准**，不要只读需求文头的冻结日期。

宿主接入说明：[user-guide.md](user-guide.md)。行为冲突时以测试为准。

最后更新：2026-08-20。

---

## 已实现（可接入宿主）

- 定点空间、地形 AABB、敌对硬挡；非法位移缩短/取消，不滑墙、不互推
- 帧戳命令、事件、只读查询；迟到命令丢弃
- 移动（方向 / MoveTo / 停止）、软锁、独立朝向 `SetFacing`
- 数据驱动属性与固定修饰顺序；具名 `ModifierSource` 成组挂卸（Spawn 可注入）
- 伤害管道、护盾、暴击、防御减免、伤害增减、治疗、Lives、尸体窗口、`CanBeRevived` 永久尸体、`PreventReviveBuffEffect`
- 技能时间轴：选敌（含尸体复活选敌）、伤/治/盾/Buff/弹/区域/召唤/Carry/复活/冲锋
- 直线穿透弹、地面或跟随区域
- Buff：属性、周期伤/治、控制、Tag 免疫/替换/驱散；时长 0 = 永久
- Brain：Idle / ChaseAttack / HoldPosition / FollowOwner
- 即时 / Stash 拾取、按帧刷怪表、`maxFrames` 超时

---

## 已设计、尚未编码

| 项 | 文档 | 要点 |
|----|------|------|
| 技能整数资源池与起手消耗 | 需求 §6；对话约定 | 字符串资源 ID；开施法时扣；不够则拒绝；打断不退 |
| Buff 战斗触发 | [ADR 0029](adr/0029-buff-combat-triggers.md) | 造成伤害 / 受到伤害 / 击杀 / 起手施法；不嵌套；V1 触发动作不含再造成伤害 |
| 获得/失去 Buff 触发 | 同上 ADR | **V1 不做**；若以后加须匹配 Id/Tag、获得仅新 instance、失去不含死亡清理 |

被动技能：核内就是永久 Buff + 触发或 ModifierSource；玩家侧由宿主包装。

---

## V1 写了、未设计完或未编码

| 项 | 状态 |
|----|------|
| JSON schema / 样例加载器 | 未做；核只认内存表 |
| 指向技能自动改朝向 | 未做；仅有 `SetFacing` |
| 技能施法射程校验 | [ADR 0030](adr/0030-skill-cast-range.md)；未编码 |
| 弹体撞地形 | [ADR 0031](adr/0031-projectile-terrain-collision.md)；未编码 |
| 伤害免疫 / 不可选中 Buff | [ADR 0032](adr/0032-damage-immunity-untargetable-buffs.md)；未编码 |
| 出生点 Leash | [ADR 0035](adr/0035-spawn-leash-defer-threat.md)；未编码 |
| Dot 施法者快照 | [ADR 0036](adr/0036-dot-snapshots-source.md)；未编码 |
| 技能击退 / 拉人 | [ADR 0037](adr/0037-skill-knockback-pull.md)；未编码 |
| 后摇可否取消 | 需求有；施法中一律不能再 Cast |
| 刷怪表战斗 RNG 随机组成 | 未做；表现全确定 |

---

## V1 明确不做

引导施法、A*、滑墙互推、跟踪弹/弹射、清波/进区刷怪、外交矩阵、可破坏地形、背包真源、FrameHash/回放、仓内 Godot、触发里造成伤害（反伤）、任意 Buff 获得/失去触发。

---

## 未决（讨论中，尚未写入 ADR）

已拍板：

- 玩家 `CastSkill` **由核校验射程**（[ADR 0030](adr/0030-skill-cast-range.md)）：技能正 `Range` 才限制；未配或 ≤0 = 不限制。尚未编码。
- 弹体 **默认撞地形 AABB 后销毁**（[ADR 0031](adr/0031-projectile-terrain-collision.md)），可关；点名/自身范围技能不检视线。尚未编码。
- **伤害免疫 / 不可选中** 为 Buff 效果（[ADR 0032](adr/0032-damage-immunity-untargetable-buffs.md)），与模板开关独立。尚未编码。
- **不做 GCD**（2026-08-20）：技能之间不另设公共冷却。
- **充能层数、CDR 后做**：V1 保持每技能一条 CD（即最多 1 层）。
- **受伤不打断施法**（[ADR 0034](adr/0034-damage-does-not-interrupt-cast.md)）：只有眩晕取消当前时间轴。
- **仇恨表后做**；**出生点 leash 要做**（[ADR 0035](adr/0035-spawn-leash-defer-threat.md)）：超出 `LeashRadius` 回原点、清目标，**不回血、不群体驱散**。尚未编码。
- **Dot 快照施法者侧**（[ADR 0036](adr/0036-dot-snapshots-source.md)）：上毒/叠层时冻结基数与 DamageDealt；目标防御与易伤每跳仍现算。尚未编码。
- **技能击退 / 拉人进 V1**（[ADR 0037](adr/0037-skill-knockback-pull.md)）：核内缩短位移，Root/被携带不移动。尚未编码。

本轮开放题已全部拍板。新缺口另开讨论并改本页。

---

## 建议阅读顺序

1. 本页
2. [user-guide.md](user-guide.md)
3. 相关 ADR
4. `tests/BattleCore.Tests/`

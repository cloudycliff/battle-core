# Battle Core 实现状态

给换会话 / 换 agent 用的进度入口。规则以 [v1-requirements.md](v1-requirements.md)、[CONTEXT.md](../CONTEXT.md)、[adr/](adr/) 为准。**代码是否已做以本页 + `tests/` 为准**，不要只读需求文头的冻结日期。

宿主 / 外围 agent 入口：[host-handoff.md](host-handoff.md)。怎么 Tick：[user-guide.md](user-guide.md)。测试表示当前代码实际行为；若测试与冻结规则冲突，视为实现或文档漂移，不自动改变规则。

最后更新：2026-08-21。

---

## 已实现（可接入宿主）

- 定点空间、地形 AABB、敌对硬挡；非法位移缩短/取消，不滑墙、不互推
- 帧戳命令、事件、只读查询；迟到命令丢弃
- 移动（方向 / MoveTo / 停止）、软锁、独立朝向 `SetFacing`
- 数据驱动属性与固定修饰顺序；具名 `ModifierSource` 成组挂卸（Spawn 可注入）
- 伤害管道、护盾、暴击、防御减免、伤害增减、治疗、Lives、尸体窗口、`CanBeRevived` 永久尸体、`PreventReviveBuffEffect`
- 技能时间轴：选敌（含尸体复活选敌）、伤/治/盾/Buff/弹/区域/召唤/Carry/复活/冲锋
- 直线穿透弹、地面或跟随区域
- Buff：属性、周期伤/治/资源、控制、Tag 免疫/替换/驱散；时长 0 = 永久；周期伤害快照来源
- Brain：Idle / ChaseAttack / HoldPosition / FollowOwner
- 即时 / Stash 拾取（含拾取资格过滤）、按帧刷怪表、`maxFrames` 超时
- 技能整数资源池：起手原子消耗、死亡保留、拾取/命令/周期 Buff 回复
- AreaEffect 来源快照与独立 RNG；创建者死亡后区域仍结算

---

## 已设计、尚未编码

| 项 | 文档 | 要点 |
|----|------|------|
| Buff 战斗触发 | [ADR 0029](adr/0029-buff-combat-triggers.md) | 造成伤害 / 受到伤害 / 击杀 / 起手施法；不嵌套；V1 触发动作不含再造成伤害 |
| 获得/失去 Buff 触发 | 同上 ADR | **V1 不做**；若以后加须匹配 Id/Tag、获得仅新 instance、失去不含死亡清理 |

被动技能：核内就是永久 Buff + 触发或 ModifierSource；玩家侧由宿主包装。

---

## V1 写了、未设计完或未编码

| 项 | 状态 |
|----|------|
| JSON schema / 样例加载器 | 未做；核只认内存表 |
| 指向技能起手自动改朝向 | [ADR 0040](adr/0040-cast-facing-updates-on-accept.md)；未做，当前仅有 `SetFacing` |
| 技能施法射程校验 | [ADR 0030](adr/0030-skill-cast-range.md)；未编码 |
| 弹体撞地形 | [ADR 0031](adr/0031-projectile-terrain-collision.md)；未编码 |
| 伤害免疫 / 不可选中 Buff | [ADR 0032](adr/0032-damage-immunity-untargetable-buffs.md)；未编码 |
| 出生点 Leash | [ADR 0035](adr/0035-spawn-leash-defer-threat.md)；未编码 |
| 技能击退 / 拉人 | [ADR 0037](adr/0037-skill-knockback-pull.md)；未编码 |
| 刷怪表战斗 RNG 随机组成 | 未做；表现全确定 |

---

## V1 明确不做

引导施法、A*、滑墙互推、跟踪弹/弹射、清波/进区刷怪、外交矩阵、可破坏地形、背包真源、FrameHash/回放、仓内 Godot、触发里造成伤害（反伤）、任意 Buff 获得/失去触发。

---

## 已拍板规则索引

- 玩家 `CastSkill` **由核校验射程**（[ADR 0030](adr/0030-skill-cast-range.md)）：技能正 `Range` 才限制；未配或 ≤0 = 不限制。尚未编码。
- 弹体 **默认撞地形 AABB 后销毁**（[ADR 0031](adr/0031-projectile-terrain-collision.md)），可关；点名/自身范围技能不检视线。尚未编码。
- **伤害免疫 / 不可选中** 为 Buff 效果（[ADR 0032](adr/0032-damage-immunity-untargetable-buffs.md)），与模板开关独立。尚未编码。
- **不做 GCD**（2026-08-20）：技能之间不另设公共冷却。
- **充能层数、CDR 后做**：V1 保持每技能一条 CD（即最多 1 层）。
- **受伤不打断施法**（[ADR 0034](adr/0034-damage-does-not-interrupt-cast.md)）：只有眩晕取消当前时间轴。
- **仇恨表后做**；**出生点 leash 要做**（[ADR 0035](adr/0035-spawn-leash-defer-threat.md)）：超出 `LeashRadius` 回原点、清目标，**不回血、不群体驱散**。尚未编码。
- **Dot 快照施法者侧**（[ADR 0036](adr/0036-dot-snapshots-source.md)）：上毒/叠层时冻结基数与 DamageDealt；目标防御与易伤每跳仍现算。Buff instance 自带 RNG。
- **技能击退 / 拉人进 V1**（[ADR 0037](adr/0037-skill-knockback-pull.md)）：核内缩短位移，Root/被携带不移动。尚未编码。
- **`FactionWiped` 表示当前无活动单位**（[ADR 0039](adr/0039-faction-wiped-is-currently-inactive.md)）：只检查 Alive / Reviving；可技能复活的 Dead 不阻止，宿主不得直接当作最终失败。
- **护盾为独立 ShieldLayer**（[ADR 0025](adr/0025-layered-shields.md)）：驱散来源 Buff 不自动拆盾。
- **后摇不可取消**（[ADR 0033](adr/0033-defer-charges-and-cdr.md)）：整个时间轴结束前不能起手下一技能；这不是公共 GCD。
- **Carry 仅眩晕/死亡掉落**（[ADR 0004](adr/0004-carry-relation.md)）：定身只停移动，沉默/缴械不拆关系。
- **技能整数资源池**（[ADR 0041](adr/0041-bounded-multi-resource-pools.md)）：声明上限；起手原子扣；死亡保留；复活不回满。
- **AreaEffect 快照与独立 RNG**（[ADR 0042](adr/0042-area-effect-snapshot-and-rng.md)）：生成时冻来源贡献；跟随目标 Dead/消失则销毁。
- **Carry 随载体技能位移同帧跟随**（[ADR 0004](adr/0004-carry-relation.md)、[ADR 0037](adr/0037-skill-knockback-pull.md)）：保持偏移，结束 Carry 时校正落点。
- **死亡状态清理**（[ADR 0012](adr/0012-lives-and-skill-revive.md)）：清普通 Buff、护盾、技能/移动/软锁/Carry；保留 PreventRevive、CD、ModifierSource。
- **指向技能起手转向**（[ADR 0040](adr/0040-cast-facing-updates-on-accept.md)）：Cast 校验成功即更新 Facing，之后取消不回滚；尚未编码。

---

## 待讨论

无。新缺口另开讨论并改本页。

---

## 建议阅读顺序

1. 外围接入：[host-handoff.md](host-handoff.md)
2. 本页
3. [user-guide.md](user-guide.md)
4. 相关 ADR
5. `tests/BattleCore.Tests/`

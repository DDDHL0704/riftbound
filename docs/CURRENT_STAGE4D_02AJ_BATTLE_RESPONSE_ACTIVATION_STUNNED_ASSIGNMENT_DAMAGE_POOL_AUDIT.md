# 4D-02AJ Battle Response Activation Stunned Assignment Damage Pool Audit

日期：2026-05-15
结论：**ACCEPTED / focused slice only / project NOT READY**

## Scope

本切片收窄 P0-004 battle response activation-returned assignment damage semantics：actual Shadow activation / stack resolution / returned response 后，stunned attacker 在 assignment prompt、runtime validation 与 committed damage pool 中都必须视为 0 power / 0 damage pool。

验收目标：

- Shadow stun 解析后，returned assignment prompt 的 `damagePool[attacker] == 0`、`lethalDamageThreshold[attacker] == 0`、`battleParticipants[*].power == 0`。
- Prompt required assignments / choices 不再要求或提供 stunned attacker 作为 damage source。
- 旧 attacker-nonzero assignment 被 `InvalidPayload` 拒绝且不推进 BF-B。
- 合法 zero-attacker assignment 只允许 defenders 分配伤害，当前 BF-A close 后才推进 BF-B spell duel。

## Implementation Accepted

- `src/Riftbound.Engine/CoreRuleEngine.cs`
  - `BuildCombatDamagePool` 对 stunned battle participant 返回 0。
  - `BuildCombatLethalDamageThreshold` 对 stunned battle participant 返回 0。
- `src/Riftbound.Engine/MatchSession.cs`
  - `ResolutionResult.BattleEffectivePowerFor` 对 stunned battle participant 返回 0，使 prompt metadata 与 runtime validation 对齐。
- `tests/Riftbound.ConformanceTests/BattleDamageAssignmentLifecycleTests.cs`
  - 新增 `NaturalBattleResponseActivationStunnedAttackerUsesZeroAssignmentDamagePool`。
  - 同步修正受 stun 语义影响的 existing activation assignment fixtures，避免旧测试继续隐含依赖 stunned attacker 仍可分配攻击伤害。

## Validation

- Targeted new guard：1/1 pass。
- Focused suite：293/293 pass。
- Adjacent suite：823/823 pass。
- Backend full：4235/4235 pass。
- `git diff --check`：pass。

## Residual Risk

该切片只关闭 Shadow stun 在 activation-returned assignment prompt / runtime validation 的 focused representative。它不关闭完整 P0-004 battle lifecycle、P0-005 PaymentEngine、LayerEngine、damage prevention / replacement / modification breadth、keyword/static power 全组合、前端最终验收或 1009/811 full-official card matrix。项目仍 **NOT READY**，不得触发 `update_goal complete`。

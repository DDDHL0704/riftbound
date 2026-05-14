# Stage 4D-02E Battle Task Advancement Baseline Evidence

日期：2026-05-14
结论：**BASELINE GREEN / IMPLEMENTATION REQUIRED / PROJECT NOT READY**

本文记录 4D-02E 实现前基线。当前 HEAD 已通过现有 battle lifecycle focused / adjacent / backend full tests，但这些测试尚未覆盖 `ASSIGN_COMBAT_DAMAGE` 关闭当前 battle 后自动推进下一个 contested battlefield task。

## Baseline Commands

Focused battle lifecycle:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~BattleDamageAssignmentLifecycleTests|FullyQualifiedName~SpellDuelBattleStateMachineTests|FullyQualifiedName~BattlefieldContestBattleTaskGuardTests|FullyQualifiedName~Shadow|FullyQualifiedName~DeclareBattle|FullyQualifiedName~StartBattle|FullyQualifiedName~Priority|FullyQualifiedName~Reaction|FullyQualifiedName~Swift|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub"
```

Result: passed 426/426.

Adjacent battlefield / payment classification regression:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Battlefield|FullyQualifiedName~SpellDuel|FullyQualifiedName~DeclareBattle|FullyQualifiedName~MoveUnit|FullyQualifiedName~BoardTaskQueue|FullyQualifiedName~ActivateAbility|FullyQualifiedName~PaymentEngineCoverageAuditTests"
```

Result: passed 607/607.

Backend full:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

Result: passed 4194/4194.

## Evidence Inspected

- `docs/CURRENT_STAGE4D_02D_BATTLE_RESPONSE_ASSIGNMENT_INTEGRATION_AUDIT.md`
- `docs/CURRENT_STAGE4D_02D_BATTLE_RESPONSE_ASSIGNMENT_INTEGRATION_EVIDENCE.md`
- `tests/Riftbound.ConformanceTests/BattleDamageAssignmentLifecycleTests.cs`
- `tests/Riftbound.ConformanceTests/SpellDuelBattleStateMachineTests.cs`
- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `src/Riftbound.Engine/MatchSession.cs`

## Gap Confirmed

- Existing 4D-02C/D tests verify one active natural battle can open assignment, commit legal assignment, close battle, clear matching `START_BATTLE`, and preserve battle-response ordering.
- Existing spell-duel tests verify spell-duel cleanup can advance to another battlefield task when a matching battle disappears.
- There is no focused test for post-`ASSIGN_COMBAT_DAMAGE` task advancement when another contested battlefield remains unresolved.
- The next implementation should add that coverage and, if needed, invoke the existing battlefield task advancement path after assignment battle close.

## Verdict

Baseline is green, but 4D-02E remains unimplemented. P0-004 remains open; P0-005, P1, frontend final gates, card matrix, READY, and active goal remain open.

# Stage 4D-02D Battle Response Assignment Integration Evidence

日期：2026-05-14
结论：**FOCUSED VERIFIED / PROJECT NOT READY**

本文件记录 4D-02D test-only integration guard 的验证结果。该切片只补 4D-02B -> 4D-02C 组合链证据，不新增 runtime 行为、不改前端、不改 coverage matrix、不关闭 P0-004 / P0-005 / P1 / READY。

## Validation Commands

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

Diff hygiene:

```sh
git diff --check
```

Result: passed.

## Evidence Summary

- `BattleDamageAssignmentLifecycleTests.NaturalBattleResponsePassThenOpensAssignCombatDamageForAssignmentOrderingBattle` proves battle-response priority opens before assignment in the combined Shadow + assignment-ordering natural battle.
- The same test proves response pass-pass opens `ASSIGN_COMBAT_DAMAGE`, then legal assignment closes the battle and clears the matching `START_BATTLE` task.
- Existing one-on-one immediate battle, no-response natural assignment, 4D-02B Shadow lifecycle, and adjacent battlefield filters remain green.

## Residual Risk

This is still focused representative coverage. Full official combat timing, all swift/reaction families, replacement / prevention, all multi-combat permutations, LayerEngine, frontend E2E refresh, and full-card evidence closure remain open. Project remains **NOT READY**.

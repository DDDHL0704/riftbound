# Stage 4D-02C Natural Battle Damage Assignment Evidence

日期：2026-05-14
结论：**FOCUSED VERIFIED / PROJECT NOT READY**

本文件记录 4D-02C implementation 后的验证结果。该切片只绑定一条 natural battle damage assignment lifecycle，不新增卡、不改前端、不改 coverage matrix、不关闭 P0-004 / P0-005 / P1 / READY。

## Validation Commands

Focused battle lifecycle:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~BattleDamageAssignmentLifecycleTests|FullyQualifiedName~SpellDuelBattleStateMachineTests|FullyQualifiedName~BattlefieldContestBattleTaskGuardTests|FullyQualifiedName~Shadow|FullyQualifiedName~DeclareBattle|FullyQualifiedName~StartBattle|FullyQualifiedName~Priority|FullyQualifiedName~Reaction|FullyQualifiedName~Swift|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub"
```

Result: passed 425/425.

Adjacent battlefield / payment classification regression:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Battlefield|FullyQualifiedName~SpellDuel|FullyQualifiedName~DeclareBattle|FullyQualifiedName~MoveUnit|FullyQualifiedName~BoardTaskQueue|FullyQualifiedName~ActivateAbility|FullyQualifiedName~PaymentEngineCoverageAuditTests"
```

Result: passed 607/607.

Backend full:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

Result: passed 4193/4193.

Diff hygiene:

```sh
git diff --check
```

Result: passed.

## Evidence Summary

- `BattleDamageAssignmentLifecycleTests.NaturalStartBattleWithAssignmentOrderingDefenderOpensAssignCombatDamagePrompt` proves natural active `START_BATTLE` can open the assignment prompt and expose authoritative metadata.
- `BattleDamageAssignmentLifecycleTests.ReconnectDuringNaturalAssignCombatDamagePreservesBattleTaskMetadataAndRedaction` proves reconnect battle/task context and hidden standby redaction.
- `BattleDamageAssignmentLifecycleTests.NaturalAssignCombatDamageRejectsWrongOrStaleCommandsWithoutMutation` locks wrong player, wrong battle id, wrong battlefield, invalid target, and stale prompt no-mutation behavior.
- `BattleDamageAssignmentLifecycleTests.NaturalAssignCombatDamageCommitsSimultaneousDamageAndClosesBattle` proves legal assignment commits simultaneous damage, cleanup, battle close, battlefield control, and no matching `START_BATTLE` task residue.
- `BattleDamageAssignmentLifecycleTests.NaturalStartBattleOneOnOneImmediateBattleRemainsStable` preserves the adjacent one-on-one immediate battle representative.
- Existing 4D-02B Shadow battle-response lifecycle and adjacent battlefield / payment classification filters remain green.

## Residual Risk

This is a focused lifecycle binding for one natural assignment-ordering branch. It does not implement full official combat timing, all replacement / prevention rules, all multi-combat permutations, LayerEngine, frontend E2E refresh, or full-card evidence closure. Project remains **NOT READY**.

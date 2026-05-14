# Stage 4D-02B Battle-Response Priority Lifecycle Evidence

日期：2026-05-14
结论：**FOCUSED VERIFIED / PROJECT NOT READY**

本文件记录 4D-02B implementation 后的验证结果。该切片只绑定 battle-response priority lifecycle 与既有 Shadow swift representative，不新增卡、不改前端、不改 coverage matrix、不关闭 P0-004 / P0-005 / P1 / READY。

## Validation Commands

Focused battle-response lifecycle:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~SpellDuelBattleStateMachineTests|FullyQualifiedName~BattlefieldContestBattleTaskGuardTests|FullyQualifiedName~Shadow|FullyQualifiedName~DeclareBattle|FullyQualifiedName~StartBattle|FullyQualifiedName~Priority|FullyQualifiedName~Reaction|FullyQualifiedName~Swift|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub"
```

Result: passed 420/420.

Adjacent battlefield / task queue / payment classification regression:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Battlefield|FullyQualifiedName~SpellDuel|FullyQualifiedName~DeclareBattle|FullyQualifiedName~MoveUnit|FullyQualifiedName~BoardTaskQueue|FullyQualifiedName~ActivateAbility|FullyQualifiedName~PaymentEngineCoverageAuditTests"
```

Result: passed 607/607.

Backend full:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

Result: passed 4188/4188.

Diff hygiene:

```sh
git diff --check
```

Result: passed.

## Evidence Summary

- `ShadowActivatedAbilityTests.NaturalStartBattleOpensBattleResponsePriorityAndExposesShadowPrompt` proves natural `START_BATTLE` / `DECLARE_BATTLE` opens battle-response priority and exposes Shadow `ACTIVATE_ABILITY` to the correct player.
- `ShadowActivatedAbilityTests.ShadowActivatesAndResolvesFromNaturalBattleResponseWindow` proves activation / stack / pass-pass / stun / return-to-battle-response / final battle close.
- `ShadowActivatedAbilityTests.ShadowNaturalBattleResponseRejectsWrongPlayerBattlefieldOrStaleTargetWithoutMutation` locks wrong player, wrong battlefield target, and stale target no-mutation behavior.
- `ShadowActivatedAbilityTests.NaturalBattleResponseReconnectSnapshotExposesBattleContextWithoutHiddenLeakage` locks reconnect prompt / battle task metadata without adding hidden object ids to public participants.
- Existing battlefield, spell-duel, move-unit, board-task, activation, and PaymentEngine coverage filters remain green.
- The deferred battle-response path is intentionally limited to the minimal `COMBAT_ASSIGNMENT` declare-battle shape with no extra battlefield target / payment-resource / brush replacement choices, so richer battle options keep existing immediate resolution behavior.

## Residual Risk

This is a focused lifecycle binding for the existing Shadow swift representative. It does not implement full official combat timing, all swift / reaction families, all combat replacement layers, extra declare-battle option preservation, full LayerEngine, frontend E2E, or full-card evidence closure. Project remains **NOT READY**.

# Residual Rule Path Audit - 2026-06-15

Status: NOT READY. Independent residual rule-audit record only.

Scope:

- Worktree: `/Users/dinghaolin/MyProjects/riftbound-rule-audit-remaining-20260615`
- Branch: `codex/rule-audit-remaining-20260615`
- Synced base: local `main` at `11d37e8e` (`test: cover end turn replay protocol envelope`)
- Prior first-pass audit: `docs/CURRENT_RULE_AUDIT_FULL_PASS_2026-06-15.md`
- Non-scope: Stage 4D triggerQueue/runtime closure slices, shared coordination board, completion audit, and closure plan edits.
- Runtime changed: no.

## Residual Summary

| Residual | Current evidence | Current disposition |
|---|---|---|
| L2P-RG-005 combat damage assignment ownership | `BattleState` stores active participants and controller ids only; `BattleDamageAssigningPlayerId` still resolves to the single attacker controller; prompt metadata exposes all source assignments to that one assigning player; `ValidateCombatDamageAssignments` still requires all source damage pools in one command | Still open P1. Needs a battle assignment ledger/state-machine change, not a prompt-only patch |
| FULL-RG-001 battlefield / cleanup / control lifecycle | `PendingTaskQueue`, `BattlefieldResolutions`, `BattleResolutions`, cleanup-first blocking, illegal standby cleanup, unattached equipment cleanup, battlefield control resolution, and task advancement representatives exist | No new small local 2P P0/P1 found. Full official lifecycle remains broad state-machine work |
| FULL-RG-002 keyword families | `KeywordCoverageReporter` and keyword modules expose mixed implemented/deferred status; `CardCatalogBaselineTests` asserts all 1009 behavior specs are only representative-rule pass and no full-official pass exists | No new single feature-family blocker found in this pass. Future bugs must be fixed by keyword family |
| FULL-RG-003 layer / attachment / equipment / `700` breadth | `ContinuousEffectState`, power modifier ledger metadata, static-aura foundation metadata, dependency lists, sequence/source-order views, and replay validation exist | Still foundation-only. Multiple equipment/static/layer/control interactions remain official-rule residuals |
| FULL-RG-004 trigger ordering | Latest `main` adds GameHub ORDER_TRIGGERS replay protocol coverage, but this is protocol-envelope test coverage, not triggerQueue runtime closure | No triggerQueue closure work done in this pass |
| FULL-RG-005 payment / optional / additional cost breadth | `PaymentEngineCoverageAuditTests` records residual manifests and current representative families; latest `main` adds PAY_COST replay protocol coverage | No new ordinary local 2P P0/P1 found. Full payment matrix remains residual |

## L2P-RG-005 Detail

Rules basis:

- Core 460.2.c: each player assigns that player's side of combat damage, starting with the attacker.
- JFAQ q6.1-q6.4: assignment is not damage; damage is simultaneous after assignment; same-priority and conflicting-requirement choices belong to the assigning player.

Current implementation evidence:

- `src/Riftbound.Engine/MatchSession.cs` `BattleState` contains `AttackerObjectIds`, `DefenderObjectIds`, and `ParticipantControllerIds`; it has no partial-assignment ledger.
- `src/Riftbound.Engine/CoreRuleEngine.cs` `BattleDamageAssigningPlayerId` returns the attacker controller for the open natural damage-assignment window.
- `MatchSession.AssignCombatDamageMetadataFor` reports one `assigningPlayerId` and builds `assignmentChoices` from the full damage pool for both attacker and defender sources.
- `CoreRuleEngine.ValidateCombatDamageAssignments` iterates every `damagePool` source and rejects unless every source's entire damage pool is present in a single command.
- Existing `BattleDamageAssignmentLifecycleTests.NaturalStartBattleWithAssignmentOrderingDefenderOpensAssignCombatDamagePrompt` still asserts P1 actionable and P2 `WAIT`.

Expected future fix:

1. Add explicit damage-assignment phase state: current assigning side/player, accepted attacker-side assignments, accepted defender-side assignments.
2. Filter prompt metadata to current player's source objects only.
3. Validate that P1 cannot assign defender sources and P2 cannot assign attacker sources.
4. Commit simultaneous damage only after both sides have submitted.
5. Extend recovery/snapshot/replay validation to include partial-assignment ledger and current assigning side.

This is the highest-priority concrete residual. It is common combat-rule work, not a single-card exception.

## Battlefield / Cleanup / Control Residual

Current foundation:

- `PendingTaskQueue` is derived from pending cleanup tasks and selects state-based cleanup before battle/spell-duel tasks.
- `AdvancePendingBattlefieldTasksAfterStateChange` opens the next contested battlefield spell duel only when the stack, battle, spell duel, payment, and state-based cleanup windows are clear.
- `RunStateBasedCleanupLoop` repeats lethal cleanup, illegal standby cleanup, and unattached equipment cleanup for up to 32 passes.
- `ApplyBattleCleanup` removes combat damage, recalls surviving attackers when defenders survive, clears battle flags, and records battle closure.
- `ResolveBattlefieldControlAfterBattle` derives controller from surviving public units at the battlefield, emits resolution events, and removes illegal standby after control changes.

Residual risk:

- The code has representative integration, but not a single complete official battlefield/control/standby/conquer/hold state machine.
- Cleanup is still split across command-specific call sites and selected state-based cleanup helpers.
- Complex control changes, standby during contest, repeated cleanup after replacement/prevention, and cross-battlefield task ordering remain broad-rule audit areas.

Current disposition: no new small ordinary local 2P P0/P1 found in this pass.

## Payment / Keyword / Layer Residual

Payment:

- Shared `PaymentPlan` and many representative payment windows exist.
- `PaymentEngineCoverageAuditTests` keeps residual constants for all-window matrices, keyword payment branches, target tax matrices, resource skills, and card-matrix readiness.
- Latest `main` adds PAY_COST replay protocol envelope coverage; that does not close official payment breadth.

Keyword:

- `KeywordCoverageReporter` exposes deferred keyword families through API/reporting.
- Combat, interaction, resource, equipment, and lifecycle keyword modules still contain explicit deferred branches.
- `CardCatalogBaselineTests` confirms behavior specs are representative-rule pass and not full-official-rule pass.

Layer/equipment:

- The server now projects `POWER_MODIFIER`, `STATIC_AURA`, and rule-text views with sequence/dependency metadata.
- `LayerEngineTimestampDependencyTests` covers representative ordering, public dependency redaction, and source/participant dependency recompute.
- The server audit still labels the system foundation-only because current runtime mutates current power in selected paths and does not globally recompute every layer from base values and timestamps.

Current disposition: no new ordinary local 2P P0/P1 found in this pass. These remain broad feature-family work.

## Next Recommended Residual Order

1. Implement L2P-RG-005 with a battle damage partial-assignment ledger and source-side prompt filtering.
2. After that, rerun Chrome local 2P for an assignment battle where defender choices matter.
3. Then continue broad official residuals by feature family: battlefield/control lifecycle, cleanup queue unification, payment matrices, keyword family matrices, and full LayerEngine.

## Validation

- Mechanical: `git diff --check` passed.
- Focused residual representative suite: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~BattleDamageAssignmentLifecycleTests|FullyQualifiedName~BattlefieldContestBattleTaskGuardTests|FullyQualifiedName~PaymentEngineCoverageAuditTests|FullyQualifiedName~LayerEngineTimestampDependencyTests|FullyQualifiedName~CardCatalogBaselineTests"` passed `867/867`.
- Chrome/local 2P smoke: not run in this pass because runtime/frontend behavior was not changed.

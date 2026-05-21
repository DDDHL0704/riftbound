# 4D-05H B_SERVER Worker Prompt: ASSIGN_COMBAT_DAMAGE / ORDER_TRIGGERS Replay Guards

Date: 2026-05-21
Owner: `B_SERVER`
Dispatcher: `A_MAIN`
Project status: **NOT READY**

## Objective

Add a narrow P0/P1 guard slice for the two complex server-authoritative prompts that were not covered by the recent 05A-05G PaymentEngine replay series:

- `ASSIGN_COMBAT_DAMAGE`
- `ORDER_TRIGGERS`

The required proof is: once a valid command for the current prompt is accepted and advances the state, exact stale replay of that same command against the post-acceptance state must reject without mutation, emit no events, preserve the post-state hash, and must not reopen or fork the original prompt/task/window.

This slice is test-first. Runtime changes are allowed only if the focused tests expose a real bug.

## Allowed Write Scope

- `tests/Riftbound.ConformanceTests/BattleDamageAssignmentLifecycleTests.cs`
- `tests/Riftbound.ConformanceTests/ConformanceFixtureShapeTests.cs`
- `tests/Riftbound.ConformanceTests/RealTriggerQueueTests.cs`
- Optional: `src/Riftbound.Engine/CoreRuleEngine.cs` only if a real runtime bug is exposed by the focused tests.
- Optional: a small 05H evidence/audit doc if useful.

## Locked Scope

Do not modify:

- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- `docs/CURRENT_CARD_EFFECT_COVERAGE_BASELINE.md`
- `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`
- frontend files
- official card catalog / imported snapshot data
- protocol core fields
- Chrome/browser/formal E2E scripts
- `fullOfficial`, READY / READY-CANDIDATE flags
- `riftbound-dotnet.sln`

You are not alone in the codebase. Do not revert edits made by others, and keep this slice compatible with DOC_MATRIX's independent matrix lane.

## Required Acceptance Cases

### ASSIGN_COMBAT_DAMAGE

Add or extend focused coverage so that:

1. A valid `AssignCombatDamageCommand` is accepted from an open assign-combat-damage prompt.
2. The same command object or equivalent exact payload is submitted again against the accepted post-state.
3. Replay rejects.
4. Replay emits no events.
5. `MatchStateHasher.Hash(postAcceptedState)` equals `MatchStateHasher.Hash(replay.State)`.
6. The replay does not reopen `PromptTypes.AssignCombatDamage`, does not re-add battle tasks, does not duplicate `COMBAT_DAMAGE_ASSIGNED`, `DAMAGE_APPLIED`, `BATTLE_DAMAGE_STEP_STARTED`, `BATTLEFIELD_CONTROL_RESOLVED`, scoring, cleanup, conquer, spell-duel focus, next-battlefield advancement or `BATTLE_CLOSED` side effects.
7. Hidden standby / face-down real identities remain redacted in any prompt/snapshot used by the test.

Prefer reusing existing builders and helpers in `BattleDamageAssignmentLifecycleTests` or `ConformanceFixtureShapeTests`.

### ORDER_TRIGGERS

Add or extend focused coverage so that:

1. A valid `OrderTriggersCommand` is accepted from an open order-triggers prompt.
2. The same ordered trigger ids are submitted again against the accepted post-state.
3. Replay rejects.
4. Replay emits no events.
5. `MatchStateHasher.Hash(postAcceptedState)` equals `MatchStateHasher.Hash(replay.State)`.
6. Replay does not recreate the trigger queue, duplicate stack items, reorder stack items, reopen `PromptTypes.OrderTriggers`, fork priority, or leak hidden trigger metadata.

Prefer reusing existing builders and helpers in `ConformanceFixtureShapeTests` or `RealTriggerQueueTests`.

## Validation Required Before Returning

Run:

```bash
source scripts/dev-env.sh && dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~BattleDamageAssignmentLifecycleTests|FullyQualifiedName~ConformanceFixtureShapeTests|FullyQualifiedName~RealTriggerQueueTests"
```

Run adjacent regression:

```bash
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ASSIGN_COMBAT_DAMAGE|FullyQualifiedName~AssignCombatDamage|FullyQualifiedName~ORDER_TRIGGERS|FullyQualifiedName~OrderTriggers|FullyQualifiedName~BattleDamageAssignmentLifecycle|FullyQualifiedName~RealTriggerQueue|FullyQualifiedName~ConformanceFixtureShape"
```

Run:

```bash
git diff --check
```

If runtime changed, also run backend full:

```bash
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

## Return Format

Report:

- changed files
- whether runtime changed
- exact test commands and pass/fail counts
- hidden-info leakage finding
- whether any P0/P1 remains open
- whether project remains NOT READY

# Stage 4D-04R B Worker Prompt: LayerEngine Timestamp Dependency

Date: 2026-05-21

Status: **DISPATCH READY / PROJECT NOT READY**

Use this prompt only for the B-side server/test implementation slice. You are not alone in the codebase: another worktree may continue matrix-doc reduction in parallel. Do not revert other people's edits, and keep this slice inside the write scope below.

## Role

You are B: server rules, effect implementation, protocol runtime and tests.

A owns acceptance, write-lock coordination and final readiness. E owns pure matrix reduction in the separate matrix-docs worktree. C owns frontend only if server prompts or snapshots require display support.

## Objective

Implement a narrow 4D-04R LayerEngine representative slice that proves server-authored continuous-effect timestamp / sequence and dependency metadata.

This is not a full official LayerEngine rewrite. The target is a verifiable foundation that extends the accepted 4D-04L through 4D-04Q LayerEngine work without changing card-matrix readiness flags or frontend rule authority.

## Current Anchors

Runtime anchors:

- `src/Riftbound.Engine/MatchSession.cs`
  - `ContinuousEffectState` currently exposes layer, duration, source, target, power deltas, base/effective power, effect kind, source card, source path, applied order, condition, lifecycle and participants.
  - `BuildContinuousEffectStates` currently builds deterministic entries and sorts by scope, target, layer, applied order and effect id. It does not yet expose a dedicated timestamp / dependency graph contract.
  - `TryBuildFriendlyEquipmentStaticAuraEffect` and `BuildBattlefieldAllUnitsStaticAuraEffects` already produce static-aura lifecycle and participant metadata.
  - `BuildContinuousEffectSnapshotView` serializes the server-authored continuous-effect view used by clients.

Test anchors:

- `tests/Riftbound.ConformanceTests/ConformanceFixtureShapeTests.cs`
  - `MatchStateExposesContinuousEffectPowerLayerViews`
  - `ContinuousEffectPowerModifierAppliedOrderSurvivesEffectIdNormalization`
- `tests/Riftbound.ConformanceTests/OrnnFriendlyEquipmentStaticPowerTests.cs`
  - `OrnnStaticAuraMetadataDisappearsWhenSourceLeavesField`
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
  - `PowerModifierAppliedOrderFollowsPowerBindEchoAppendSequence`
  - existing minimum-power / static-aura LayerEngine regression coverage.

## Allowed Write Scope

Default runtime/test write lock:

- `src/Riftbound.Engine/MatchSession.cs`
- `tests/Riftbound.ConformanceTests/ConformanceFixtureShapeTests.cs`
- `tests/Riftbound.ConformanceTests/OrnnFriendlyEquipmentStaticPowerTests.cs`

Optional focused test file:

- `tests/Riftbound.ConformanceTests/LayerEngineTimestampDependencyTests.cs`

Optional docs after validation:

- `docs/CURRENT_STAGE4D_04R_LAYERENGINE_TIMESTAMP_DEPENDENCY_AUDIT.md`
- `docs/CURRENT_STAGE4D_04R_LAYERENGINE_TIMESTAMP_DEPENDENCY_EVIDENCE.md`
- `docs/CURRENT_A_MASTER_CHECKPOINT.md` only for final checkpoint wording after tests pass.

## Forbidden Scope

Do not touch without fresh A authorization:

- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- `docs/CURRENT_CARD_EFFECT_COVERAGE_BASELINE.md`
- `data/official/card-catalog.zh-CN.json`
- frontend runtime / DevUi rule paths
- Chrome / browser scripts
- formal 18-step E2E scripts
- broad PaymentEngine behavior
- battle lifecycle / task queue semantics
- broad equipment runtime outside the focused static-aura dependency tests
- hidden-info filtering internals unless a focused leakage test proves a concrete server bug
- protocol core fields unrelated to continuous-effect snapshot metadata
- `fullOfficial`
- READY / READY-CANDIDATE
- `riftbound-dotnet.sln`

## Implementation Contract

Keep the server as the only rules authority.

Add server-authored metadata for continuous-effect ordering and dependencies. Prefer a small explicit contract over broad rewrites. The exact field names can follow existing local style, but the implementation must prove these properties:

1. Every continuous-effect snapshot entry has a stable server-authored ordering signal, such as `sequence` or `timestampOrder`.
2. Static aura entries expose dependency object ids for their source and public participants without exposing hidden card identities or hidden random results.
3. Until-end power modifiers continue to preserve existing `appliedOrder`.
4. A representative mixed state with direct power modifier, minimum-power floor and static aura produces deterministic continuous-effect ordering.
5. When a static-aura source leaves the public field, stale dependency metadata disappears from `MatchState.ContinuousEffects` and from the authoritative snapshot.
6. When a participant or target leaves the relevant zone, dependency metadata is removed or recomputed deterministically.
7. Existing 4D-04L through 4D-04Q behavior remains green.

Recommended shape:

- Extend `ContinuousEffectState` with nullable ordering/dependency metadata.
- Derive metadata in `BuildContinuousEffectStates` from current authoritative public state.
- Serialize only the safe metadata in `BuildContinuousEffectSnapshotView`.
- Keep hidden or face-down card identity out of dependency arrays.
- Preserve the existing power/base/effective fields and existing tests unless the new metadata requires assertions to be added.

## Required Tests

Add or update focused tests that prove:

1. Snapshot continuous-effect entries include stable ordering metadata across repeated `ResolutionResult.BuildSnapshots` calls.
2. Dependency metadata includes public source/participant/target object ids for static auras.
3. Dependency metadata does not include hidden face-down object identities when a hidden object is present elsewhere in the state.
4. Direct power modifier plus minimum-power floor plus static aura remains deterministically ordered.
5. Static aura metadata disappears when the source leaves the public field.
6. Static aura participant dependencies are recomputed when a participant leaves the battlefield or public field.

Use focused state-construction tests where possible. Do not make the frontend compute or validate LayerEngine ordering.

## Required Validation

Run the focused LayerEngine baseline/adjacent filter:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~OrnnFriendlyEquipmentStaticPowerTests|FullyQualifiedName~P79BattlefieldStaticPowerAddsOneToBattleParticipants|FullyQualifiedName~ContinuousEffect|FullyQualifiedName~PowerModifier|FullyQualifiedName~MinimumPower|FullyQualifiedName~LayerEngine|FullyQualifiedName~EquipmentKeyword"
```

Run the new 04R focused tests if they are not already covered by that filter.

Run final hygiene:

```sh
git diff --check
```

If runtime code changes outside the allowed write scope become necessary, stop and report the blocker to A before editing.

## Expected Output

When done, report:

- changed files;
- new files;
- exact tests run and pass/fail counts;
- whether any hidden-info leakage was found;
- whether protocol shape changed and why it remains frontend-display-only;
- remaining open P0/P1/P2 items;
- final conclusion: **NOT READY** unless A later proves all Stage 4 gates.

Do not output READY-CANDIDATE. Do not mark the goal complete.

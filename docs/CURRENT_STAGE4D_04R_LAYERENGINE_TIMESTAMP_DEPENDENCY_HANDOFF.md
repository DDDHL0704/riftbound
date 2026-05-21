# Stage 4D-04R LayerEngine Timestamp Dependency Handoff

Date: 2026-05-21

Conclusion: **HANDOFF READY / PROJECT NOT READY**

## Purpose

4D-04R is the next proposed main-worktree development slice after the accepted 4D-04Q LayerEngine static aura source lifecycle foundation.

This handoff does not implement runtime behavior. It opens a future B-side acceptance contract for a narrow LayerEngine timestamp / dependency graph representative slice while the separate matrix-docs worktree continues pure card-matrix number reduction.

## Input Evidence

Accepted predecessor slices:

- 4D-04L LayerEngine foundation.
- 4D-04M minimum-power ledger exactness.
- 4D-04N direct-power ledger exactness.
- 4D-04O power modifier ordering metadata.
- 4D-04P minimum-power ordering.
- 4D-04Q static aura source lifecycle foundation.

Current server surface:

- `ContinuousEffectState` exposes layer, duration, source, target, power delta, base power, effective power, effect kind, source card, source path, applied order, condition, lifecycle and participants.
- Snapshot `timing.continuousEffects[]` is server-authored.
- Frontend must continue to render this metadata only; it must not calculate LayerEngine ordering or recompute power.

## Target

Future B slice: **4D-04R-B LayerEngine timestamp / dependency graph representative verifier**.

The goal is to prove a narrow, server-authored dependency graph for continuous effects:

- stable timestamp / sequence metadata for continuous-effect entries;
- dependency object ids for source, target and participant relationships;
- deterministic ordering when until-end modifiers, static auras and minimum-power floors are all present;
- source lifecycle invalidation when the source leaves the public field;
- dependency invalidation when a target or participant leaves the relevant zone;
- no hidden-info leakage through dependency metadata.

This is not a full official LayerEngine rewrite.

## Suggested B Write Scope

Default runtime/test write lock:

- `src/Riftbound.Engine/MatchSession.cs`
- `tests/Riftbound.ConformanceTests/ConformanceFixtureShapeTests.cs`
- `tests/Riftbound.ConformanceTests/OrnnFriendlyEquipmentStaticPowerTests.cs`

Optional new test file:

- `tests/Riftbound.ConformanceTests/LayerEngineTimestampDependencyTests.cs`

Docs write lock:

- `docs/CURRENT_STAGE4D_04R_LAYERENGINE_TIMESTAMP_DEPENDENCY_AUDIT.md`
- `docs/CURRENT_STAGE4D_04R_LAYERENGINE_TIMESTAMP_DEPENDENCY_EVIDENCE.md`
- A-side checkpoint / completion / P0-P1 docs only after validation.

## Forbidden Scope

Do not touch without fresh A authorization:

- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- `data/official/card-catalog.zh-CN.json`
- frontend runtime / DevUi local rules
- Chrome / browser scripts
- formal 18-step E2E scripts
- broad PaymentEngine
- battle lifecycle / task queue semantics
- broad equipment runtime
- hidden-info filter internals unless a focused leakage test proves a bug
- full-card matrix status
- `fullOfficial`
- READY / READY-CANDIDATE
- `riftbound-dotnet.sln`

## Acceptance

Future B evidence must prove:

1. Continuous-effect snapshot entries include server-authored timestamp or sequence metadata that is stable across repeated snapshot generation.
2. Continuous-effect entries include dependency metadata without exposing hidden card identities or internal task ids to unauthorized players.
3. A representative state with direct until-end power modifier, minimum-power floor and static aura produces deterministic server ordering.
4. A source leaving the public field removes its static aura dependency metadata without stale snapshot residue.
5. A participant or target leaving the relevant zone removes or updates dependency metadata deterministically.
6. Existing 4D-04L through 4D-04Q focused and adjacent tests remain green.
7. The slice keeps P1-001, P1-002, full LayerEngine, card matrix fullOfficial and READY open.

## Suggested Verification

Baseline command:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~OrnnFriendlyEquipmentStaticPowerTests|FullyQualifiedName~P79BattlefieldStaticPowerAddsOneToBattleParticipants|FullyQualifiedName~ContinuousEffect|FullyQualifiedName~PowerModifier|FullyQualifiedName~MinimumPower|FullyQualifiedName~LayerEngine|FullyQualifiedName~EquipmentKeyword"
```

Future B focused command should add the new 4D-04R timestamp / dependency tests to the same filter.

Final hygiene:

```sh
git diff --check
```

## Non-Closure

4D-04R handoff does not close P1-001, P1-002, P1-003, P0-005, E_CARD_MATRIX_READINESS, card matrix coverage, frontend final validation, Chrome smoke, formal 18-step E2E, completion audit or READY.

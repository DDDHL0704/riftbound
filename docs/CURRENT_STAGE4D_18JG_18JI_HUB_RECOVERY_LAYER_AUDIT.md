# Stage 4D-18JG/18JH/18JI Hub/Recovery/Layer Audit

Date: 2026-06-05 22:22 CST

Status: accepted into A_MAIN working tree for `checkpoint: stage 4D mulligan recovery layer breadth`. Project remains **NOT READY**.

## Scope

- 18JG: `tests/Riftbound.ConformanceTests/GameHubJoinTests.cs`
  - Added `MulliganDuplicateClientIntentRawPayloadReplaysButChangedRawConflictsWithoutMutation`.
  - Covers official opening `MULLIGAN` exact raw-payload duplicate replay, stable `CLIENT_INTENT_CONFLICT` for changed raw payloads with the same `clientIntentId`, no group/caller mutation and no journal growth.
- 18JH: `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`
  - Added `RecoveryValidatorRejectsSnapshotTimingBattleDamageAssignmentNullMaps`.
  - Covers snapshot battle damage assignment payloads whose `damagePool`, `legalTargets`, `existingDamage` and `lethalDamageThreshold` maps are present as `null`, locking stable required-map diagnostics.
- 18JI: `tests/Riftbound.ConformanceTests/LayerEngineTimestampDependencyTests.cs`
  - Added `LayerEngineBattlefieldStaticAuraMetadataDisappearsWhenSourceLeavesBattlefieldAcrossPlayerViews`.
  - Covers a battlefield static-aura source battlefield object leaving the public battlefield, proving the authoritative aura disappears and P1/P2 snapshot views stop exposing source/target/participant dependency metadata.

Runtime changed: no. Docs and tests only.

## Worker Commits

- `81b8a436` - `test: cover hub mulligan raw intent idempotency`
- `f41858a4` - `test: cover recovery battle damage null maps`
- `f70faceb` - `test: cover battlefield aura source leaves field`

## Validation

- Focused new tests:
  - `dotnet test Riftbound.slnx --no-restore --filter "MulliganDuplicateClientIntentRawPayloadReplaysButChangedRawConflictsWithoutMutation|RecoveryValidatorRejectsSnapshotTimingBattleDamageAssignmentNullMaps|LayerEngineBattlefieldStaticAuraMetadataDisappearsWhenSourceLeavesBattlefieldAcrossPlayerViews" --logger "console;verbosity=minimal"`
  - Result: `3/3` passed.
- Touched class filter:
  - `dotnet test Riftbound.slnx --no-restore --filter "GameHubJoinTests|MatchRecoveryTests|LayerEngineTimestampDependencyTests" --logger "console;verbosity=minimal"`
  - Result: `1433/1433` passed.
- Broader adjacent server filter:
  - `dotnet test Riftbound.slnx --no-restore --filter "GameHubJoinTests|MatchRecoveryTests|LayerEngineTimestampDependencyTests|OfficialOpeningTests|PostgresMatchRecoveryStoreSmokeTests|ConformanceFixtureRunnerTests|OrnnFriendlyEquipmentStaticPowerTests" --logger "console;verbosity=minimal"`
  - Result: `5066/5066` passed.
- Backend full:
  - `dotnet test Riftbound.slnx --no-restore --logger "console;verbosity=minimal"`
  - Result: `7246/7246` passed under the current no-DB environment.
- Mechanical checks:
  - `git diff --check` passed.
  - `git diff --cached --check` passed.
  - Anchored conflict-marker scan over `docs`, `tests` and `src` found no matches.
  - `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed.

## Remaining Risk

- Real DB-backed Postgres smoke remains open because no `ConnectionStrings__Riftbound` was configured.
- This bundle narrows protocol/recovery/LayerEngine test breadth only. Broader P0/P1, command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.

# Stage 4D-18JD/18JE/18JF Hub/Recovery/Ornn Audit

Date: 2026-06-05 22:01 CST

Status: accepted into A_MAIN working tree for `checkpoint: stage 4D pass recovery ornn breadth`. Project remains **NOT READY**.

## Scope

- 18JD: `tests/Riftbound.ConformanceTests/GameHubJoinTests.cs`
  - Added `PassWrapperDuplicateClientIntentRawPayloadReplaysButSubmitIntentChangedRawConflictsWithoutMutation`.
  - Covers `Pass` wrapper exact raw-payload duplicate replay, stable `CLIENT_INTENT_CONFLICT` for changed raw payloads sent later through `SubmitIntent` with the same `clientIntentId`, no group/caller mutation and no journal growth.
- 18JE: `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`
  - Added `RecoveryValidatorRejectsSnapshotTimingBattleDamageAssignmentMissingRequiredMaps`.
  - Covers snapshot battle damage assignment payloads missing `damagePool`, `legalTargets`, `existingDamage` and `lethalDamageThreshold`, locking stable required-map diagnostics.
- 18JF: `tests/Riftbound.ConformanceTests/OrnnFriendlyEquipmentStaticPowerTests.cs`
  - Added `OrnnStaticAuraOmitsParticipantMetadataWhenFriendlyEquipmentLeavesPublicFieldAcrossPlayerViews`.
  - Covers Ornn remaining on the public field after friendly equipment leaves it, proving the static aura persists with zero participant/dependency metadata and P1/P2 snapshot views omit participant metadata consistently.

Runtime changed: no. Docs and tests only.

## Worker Commits

- `8c9ec999` - `test: cover Pass wrapper duplicate raw payload`
- `86c1ba99` - `test: cover snapshot damage assignment missing maps`
- `631b675b` - `Add Ornn aura participant metadata regression`

## Validation

- Focused new tests:
  - `dotnet test Riftbound.slnx --no-restore --filter "PassWrapperDuplicateClientIntentRawPayloadReplaysButSubmitIntentChangedRawConflictsWithoutMutation|RecoveryValidatorRejectsSnapshotTimingBattleDamageAssignmentMissingRequiredMaps|OrnnStaticAuraOmitsParticipantMetadataWhenFriendlyEquipmentLeavesPublicFieldAcrossPlayerViews" --logger "console;verbosity=minimal"`
  - Result: `3/3` passed.
- Touched class filter:
  - `dotnet test Riftbound.slnx --no-restore --filter "GameHubJoinTests|MatchRecoveryTests|OrnnFriendlyEquipmentStaticPowerTests" --logger "console;verbosity=minimal"`
  - Result: `1428/1428` passed.
- Broader adjacent server filter:
  - `dotnet test Riftbound.slnx --no-restore --filter "GameHubJoinTests|MatchRecoveryTests|OrnnFriendlyEquipmentStaticPowerTests|OfficialOpeningTests|PostgresMatchRecoveryStoreSmokeTests|ConformanceFixtureRunnerTests|LayerEngineTimestampDependencyTests" --logger "console;verbosity=minimal"`
  - Result: `5063/5063` passed.
- Backend full:
  - `dotnet test Riftbound.slnx --no-restore --logger "console;verbosity=minimal"`
  - Result: `7243/7243` passed under the current no-DB environment.
- Mechanical checks:
  - `git diff --check` passed.
  - `git diff --cached --check` passed.
  - Anchored conflict-marker scan over `docs`, `tests` and `src` found no matches.
  - `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed.

## Remaining Risk

- Real DB-backed Postgres smoke remains open because no `ConnectionStrings__Riftbound` was configured.
- This bundle narrows protocol/recovery/LayerEngine-Ornn test breadth only. Broader P0/P1, command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.

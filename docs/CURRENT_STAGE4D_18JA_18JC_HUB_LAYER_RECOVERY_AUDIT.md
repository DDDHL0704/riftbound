# Stage 4D-18JA/18JB/18JC Hub/Layer/Recovery Audit

Date: 2026-06-05 21:45 CST

Status: accepted into A_MAIN working tree for `checkpoint: stage 4D wrapper layer recovery breadth`. Project remains **NOT READY**.

## Scope

- 18JA: `tests/Riftbound.ConformanceTests/GameHubJoinTests.cs`
  - Added `ReadyWrapperDuplicateClientIntentRawPayloadReplaysButSubmitIntentChangedRawConflictsWithoutMutation`.
  - Covers `Ready` wrapper exact raw-payload duplicate replay, stable `CLIENT_INTENT_CONFLICT` for changed raw payloads sent later through `SubmitIntent` with the same `clientIntentId`, no group/caller mutation and no journal growth.
- 18JB: `tests/Riftbound.ConformanceTests/LayerEngineTimestampDependencyTests.cs`
  - Added `LayerEngineBattlefieldStaticAuraSourceOrderDependencyMetadataMatchesAuthoritativeStateAcrossPlayerViews`.
  - Covers multiple battlefield static-aura sources targeting the same shared unit and proves authoritative dependency metadata, participant lists, `FOUNDATION_ONLY` status and P1/P2 snapshot signatures stay aligned.
- 18JC: `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`
  - Added `RecoveryValidatorRejectsSpectatorReplayTimingBattleDamageAssignmentMissingRequiredMaps`.
  - Covers spectator replay battle damage assignment payloads missing `damagePool`, `legalTargets`, `existingDamage` and `lethalDamageThreshold`, locking stable required-map diagnostics.

Runtime changed: no. Docs and tests only.

## Worker Commits

- `fc4bc6b0` - `test: cover ready wrapper idempotency conflict`
- `41fc3cb4` - `test: cover battlefield aura source order metadata`
- `55a22d42` - `Add recovery damage assignment missing map test`

## Validation

- Focused new tests:
  - `dotnet test Riftbound.slnx --no-restore --filter "ReadyWrapperDuplicateClientIntentRawPayloadReplaysButSubmitIntentChangedRawConflictsWithoutMutation|LayerEngineBattlefieldStaticAuraSourceOrderDependencyMetadataMatchesAuthoritativeStateAcrossPlayerViews|RecoveryValidatorRejectsSpectatorReplayTimingBattleDamageAssignmentMissingRequiredMaps" --logger "console;verbosity=minimal"`
  - Result: `3/3` passed.
- Touched class filter:
  - `dotnet test Riftbound.slnx --no-restore --filter "GameHubJoinTests|LayerEngineTimestampDependencyTests|MatchRecoveryTests" --logger "console;verbosity=minimal"`
  - Result: `1428/1428` passed.
- Broader adjacent server filter:
  - `dotnet test Riftbound.slnx --no-restore --filter "GameHubJoinTests|LayerEngineTimestampDependencyTests|MatchRecoveryTests|OfficialOpeningTests|PostgresMatchRecoveryStoreSmokeTests|ConformanceFixtureRunnerTests" --logger "console;verbosity=minimal"`
  - Result: `5052/5052` passed.
- Backend full:
  - `dotnet test Riftbound.slnx --no-restore --logger "console;verbosity=minimal"`
  - Result: `7240/7240` passed under the current no-DB environment.
- Mechanical checks:
  - `git diff --check` passed.
  - `git diff --cached --check` passed.
  - Anchored conflict-marker scan over `docs`, `tests` and `src` found no matches.
  - `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed.

## Remaining Risk

- Real DB-backed Postgres smoke remains open because no `ConnectionStrings__Riftbound` was configured.
- This bundle narrows protocol/recovery/LayerEngine test breadth only. Broader P0/P1, command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.

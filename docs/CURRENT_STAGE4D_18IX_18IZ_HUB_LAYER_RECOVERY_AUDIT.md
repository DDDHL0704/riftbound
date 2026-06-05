# Stage 4D-18IX/18IY/18IZ Hub/Layer/Recovery Audit

Date: 2026-06-05 21:24 CST

Status: accepted into A_MAIN working tree for `checkpoint: stage 4D hub layer recovery breadth`. Project remains **NOT READY**.

## Scope

- 18IX: `tests/Riftbound.ConformanceTests/GameHubJoinTests.cs`
  - Added `SeedScenarioDuplicateClientIntentRawPayloadReplaysButChangedScenarioConflictsWithoutMutation`.
  - Covers dev `SeedScenario` exact raw-payload duplicate replay, stable `CLIENT_INTENT_CONFLICT` for changed scenario payloads with the same `clientIntentId`, no group/caller mutation and no journal growth.
- 18IY: `tests/Riftbound.ConformanceTests/LayerEngineTimestampDependencyTests.cs`
  - Added `LayerEngineBattlefieldStaticAuraSourceOrderMetadataMatchesAuthoritativeStateAcrossPlayerViews`.
  - Covers multiple battlefield static-aura sources targeting the same unit and proves authoritative `sequence`, `sourceOrder`, `sourceObjectId` and `targetObjectId` order matches P1/P2 snapshot `continuousEffects`.
- 18IZ: `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`
  - Added `RecoveryValidatorRejectsSpectatorReplayTimingBattleDamageAssignmentMissingRequiredAssignments`.
  - Covers spectator replay battle damage assignment payloads missing `requiredAssignments` and locks the stable required-assignment-list diagnostic.

Runtime changed: no. Docs and tests only.

## Worker Commits

- `eec8aecd` - `test: cover seed scenario duplicate client intent`
- `f19af812` - `Add battlefield static aura order parity test`
- `3501d35f` - `test recovery battle damage required assignments missing`

## Validation

- Focused new tests:
  - `dotnet test Riftbound.slnx --no-restore --filter "SeedScenarioDuplicateClientIntentRawPayloadReplaysButChangedScenarioConflictsWithoutMutation|LayerEngineBattlefieldStaticAuraSourceOrderMetadataMatchesAuthoritativeStateAcrossPlayerViews|RecoveryValidatorRejectsSpectatorReplayTimingBattleDamageAssignmentMissingRequiredAssignments" --logger "console;verbosity=minimal"`
  - Result: `3/3` passed.
- Touched class filter:
  - `dotnet test Riftbound.slnx --no-restore --filter "GameHubJoinTests|LayerEngineTimestampDependencyTests|MatchRecoveryTests" --logger "console;verbosity=minimal"`
  - Result: `1425/1425` passed.
- Broader adjacent server filter:
  - `dotnet test Riftbound.slnx --no-restore --filter "GameHubJoinTests|LayerEngineTimestampDependencyTests|MatchRecoveryTests|OfficialOpeningTests|PostgresMatchRecoveryStoreSmokeTests|ConformanceFixtureRunnerTests" --logger "console;verbosity=minimal"`
  - Result: `5049/5049` passed.
- Backend full:
  - `dotnet test Riftbound.slnx --no-restore --logger "console;verbosity=minimal"`
  - Result: `7237/7237` passed under the current no-DB environment.
- Mechanical checks:
  - `git diff --check` passed.
  - `git diff --cached --check` passed.
  - Anchored conflict-marker scan over `docs`, `tests` and `src` found no matches.
  - `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed.

## Remaining Risk

- Real DB-backed Postgres smoke remains open because no `ConnectionStrings__Riftbound` was configured.
- This bundle narrows protocol/recovery/LayerEngine test breadth only. Broader P0/P1, command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.

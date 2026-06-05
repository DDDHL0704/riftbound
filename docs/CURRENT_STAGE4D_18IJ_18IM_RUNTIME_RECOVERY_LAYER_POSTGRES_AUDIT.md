# Stage 4D 18IJ-18IM Runtime/Recovery/Layer/Postgres Audit

Date: 2026-06-05

Status: accepted on A_MAIN as a bundled server/runtime closure checkpoint. Project remains **NOT READY**.

## Scope

- 18IJ: `MatchSession` duplicate `clientIntentId` handling now fingerprints raw command payloads and rejects same-player/same-intent/same-command-type retries when the raw payload differs. Exact same raw payload remains idempotent. A_MAIN preserved legacy recovered-command compatibility by treating missing raw payload fingerprints as a same-command wildcard.
- 18IK: `MatchActionLogReplayer` now reports a direct raw-command-type mismatch before replaying a persisted command whose `RecoveredCommand.CommandType` disagrees with raw `cmdType`.
- 18IL: Postgres recovery-store smoke coverage now asserts accepted and rejected recovered commands preserve raw command payload fields when a real `ConnectionStrings__Riftbound` is available. The current local validation environment had no connection string, so this smoke used its existing early return.
- 18IM: `LayerEngineTimestampDependencyTests` now locks object static-aura power scalars and player-view `continuousEffects` parity for Ornn-style friendly equipment static aura state.

## Files

- `src/Riftbound.Engine/MatchSession.cs`
- `src/Riftbound.Engine/MatchRecovery.cs`
- `tests/Riftbound.ConformanceTests/GameHubJoinTests.cs`
- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`
- `tests/Riftbound.ConformanceTests/PostgresMatchRecoveryStoreSmokeTests.cs`
- `tests/Riftbound.ConformanceTests/LayerEngineTimestampDependencyTests.cs`

## Worker Commits

- 18IJ `29176a63` (`codex/stage4d-18ij-session-idempotency`)
- 18IK `4ed74fba` (`codex/stage4d-18ik-actionlog-rawtype`)
- 18IL `12826899` (`codex/stage4d-18il-postgres-raw-roundtrip`)
- 18IM `8b3f4421` (`codex/stage4d-18im-layer-static-aura`)

## Validation

- Focused new tests: `4/4`
  - `SubmitIntentDuplicateSameCommandDifferentRawPayloadReturnsStableConflict`
  - `ActionLogReplayerRejectsRawCommandTypeMismatchBeforeReplay`
  - `PostgresRecoveryStoreLoadsRawCommandPayloadsForAcceptedAndRejectedCommands`
  - `LayerEngineObjectStaticAuraPowerScalarsMatchAuthoritativeStateAcrossPlayerViews`
- Adjacent combined server filter: `2003/2003`
  - `GameHubJoinTests`
  - `MatchRecoveryTests`
  - `LayerEngineTimestampDependencyTests`
  - `ContinuousEffect`
  - `PostgresMatchRecoveryStoreSmokeTests`
  - `OfficialOpening`
- Backend full: `Riftbound.slnx` `7226/7226`
- Mechanical checks before docs sync:
  - `git diff --cached --check`
  - `git diff --check`
  - anchored conflict-marker scan over `src`, `tests`, `docs`
  - `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Residual Risk

- Real DB-backed Postgres smoke remains open until `ConnectionStrings__Riftbound` points at a live migrated database.
- Registry revalidation of a store-returned frame that skipped `MatchRecoveryValidator` is still an audited candidate and was intentionally not merged into this bundle because it would overlap future `MatchSession.cs` runtime work.
- Broader command/recovery/random determinism, remaining recovery payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, `fullOfficial`, P0/P1 final closure and final READY status remain open.

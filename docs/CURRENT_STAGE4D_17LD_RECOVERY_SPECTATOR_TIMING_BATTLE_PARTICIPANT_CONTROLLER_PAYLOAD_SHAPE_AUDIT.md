# Stage 4D-17LD Recovery Spectator Timing Battle Participant-Controller Payload Shape Audit

Date: 2026-05-27 20:53 CST

Scope: A_MAIN server P1-004 replay/recovery determinism slice for spectator replay-frame timing battle participant-controller map payload shape validation.

Runtime change:
- `MatchRecoveryValidator.ValidateSpectatorReplayFrame` now distinguishes missing/null battle participant-controller map semantics from present malformed non-object map payloads.
- Missing/null `Timing["battle"]["participantControllerIds"]` keeps `spectator replay frame timing battle participant controller map is required`.
- Present non-object `Timing["battle"]["participantControllerIds"]` now produces `spectator replay frame timing battle participant controller map payload is required`.
- Spectator battle scalar/list validation remains unchanged.

Test coverage:
- Added `RecoveryValidatorRejectsSpectatorReplayTimingBattleParticipantControllerPayloadShapeDrift` in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- The regression mutates spectator replay-frame battle participant-controller map payload to a non-object value and asserts the explicit payload-shape diagnostic.

Validation:
- Focused single test: `1/1`.
- Focused recovery suite: `335/335`.
- Adjacent recovery/opening/store-smoke filter: `916/916`.
- Backend full suite: `6281/6281`.
- Mechanical checks passed: `git diff --check`, anchored conflict-marker scan over `docs`/`tests`/`src`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`.

Locks:
- Touched only recovery runtime validation, recovery tests, current checkpoint/completion/P0-P1/next-dispatch docs, this dedicated audit doc and the shared coordination board.
- Matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final status and `riftbound-dotnet.sln` remain locked.

Status: This narrows replay/recovery determinism only. P0/P1, broader command/recovery/random determinism, remaining recovered/spectator nested payload shape breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final status remain open. Project remains **NOT READY**.

# Stage 4D-17GX Recovery Spectator Battle Participant Controller Property Shape Audit

Date: 2026-05-25

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidateSpectatorReplayFrame` now validates object property names for nested `Timing["battle"]["participantControllerIds"]` map payloads. Map property names must be non-blank, must not carry surrounding whitespace, and duplicate normalized property names are rejected before participant-controller map extraction consumes those payloads. This prevents duplicate JSON properties or spectator timing battle participant-controller key drift from being silently resolved by dictionary / `JsonElement` key lookup during recovery-frame spectator validation.

Test change: `RecoveryValidatorRejectsSpectatorReplayTimingBattleParticipantControllerPropertyNameDrift` proves duplicate, whitespace-mutated and blank property names in the battle participant-controller map produce explicit recovery diagnostics while preserving existing battle value parity checks.

Validation:

- Focused single test: new battle participant-controller property-name drift test passed `1/1`.
- Focused recovery: `MatchRecoveryTests` passed `225/225`.
- Adjacent recovery/opening/store-smoke: passed `806/806`.
- Backend full: passed `6171/6171`.
- Mechanical checks: `git diff --check`, anchored conflict-marker scan over `src`/`tests`/`docs`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness gate, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes only the spectator timing battle participant-controller map property-name shape slice. Remaining spectator snapshot nested payload property-name breadth, broader command/recovery/random determinism, full LayerEngine breadth, battlefield/battle lifecycle breadth, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and final readiness remain open.

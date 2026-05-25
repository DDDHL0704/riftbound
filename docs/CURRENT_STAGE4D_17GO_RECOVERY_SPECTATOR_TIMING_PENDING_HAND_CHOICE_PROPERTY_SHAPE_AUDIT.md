# Stage 4D-17GO Recovery Spectator Timing Pending Hand Choice Property Shape Audit

Date: 2026-05-25

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidateSpectatorReplayFrame` now validates spectator replay timing object property names for `Timing["pendingHandChoice"]`. Properties must have non-blank names without surrounding whitespace, and duplicate normalized property names are rejected before pending-hand-choice field extraction consumes the payload. This prevents duplicate JSON properties or spectator pending-hand-choice property-name drift from being silently resolved by dictionary / `JsonElement` key lookup during recovery-frame spectator validation. The existing `legalObjectIds` spectator redaction check is unchanged.

Test change: `RecoveryValidatorRejectsSpectatorReplayTimingPendingHandChoicePropertyNameDrift` proves duplicate, whitespace-mutated and blank property names in the pending-hand-choice object produce explicit recovery diagnostics.

Validation:

- Focused single test: new pending-hand-choice property-name drift test passed `1/1`.
- Focused recovery: `MatchRecoveryTests` passed `216/216`.
- Adjacent recovery/opening/store-smoke: passed `797/797`.
- Backend full: passed `6162/6162`.
- Mechanical checks: `git diff --check`, anchored conflict-marker scan over `src`/`tests`/`docs`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness gate, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes only the spectator timing pending-hand-choice property-name shape slice. Broader command/recovery/random determinism, full LayerEngine breadth, battlefield/battle lifecycle breadth, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and final readiness remain open.

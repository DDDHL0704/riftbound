# Stage 4D-17HZ Recovery Snapshot Timing Pending Hand Choice Property Shape Audit

Date: 2026-05-25

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidateSnapshotShape` now validates property names for recovered player-view snapshot `Timing["pendingHandChoice"]` payload objects. Pending-hand-choice property names must be non-blank, must not carry surrounding whitespace, and duplicate normalized property names are rejected before pending-hand-choice field consumers can normalize those keys.

Test change: `RecoveryValidatorRejectsSnapshotTimingPendingHandChoicePropertyNameDrift` proves duplicate normalized pending-hand-choice keys, whitespace-mutated pending-hand-choice keys and blank pending-hand-choice keys produce explicit recovery diagnostics for recovered player-view snapshot timing pending-hand-choice payloads while preserving existing optional timing pending-hand-choice object behavior.

Validation:

- Focused single test: new snapshot timing pending-hand-choice property-name drift test passed `1/1`.
- Focused recovery: `MatchRecoveryTests` passed `253/253`.
- Adjacent recovery/opening/store-smoke: passed `834/834`.
- Backend full: passed `6199/6199`.
- Mechanical checks: `git diff --check`, anchored conflict-marker scan over `src`/`tests`/`docs`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness gate, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes only the recovered player-view snapshot timing pending-hand-choice property-name shape slice. Remaining recovered/spectator nested payload property-name breadth, broader command/recovery/random determinism, full LayerEngine breadth, battlefield/battle lifecycle breadth, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and final readiness remain open.

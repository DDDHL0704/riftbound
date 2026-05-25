# Stage 4D-17GT Recovery Spectator Snapshot Lane Property Shape Audit

Date: 2026-05-25

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidateSpectatorReplayFrame` now validates object property names for spectator snapshot `Lanes`, `battlefieldObjectIds[]`, `battlefields[]` and nested `standbySlots[]` payloads. Properties must have non-blank names without surrounding whitespace, and duplicate normalized property names are rejected before lane, battlefield or standby-slot field extraction consumes those payloads. This prevents duplicate JSON properties or spectator snapshot lane property-name drift from being silently resolved by dictionary / `JsonElement` key lookup during recovery-frame spectator validation.

Test change: `RecoveryValidatorRejectsSpectatorReplaySnapshotLanePropertyNameDrift` proves duplicate, whitespace-mutated and blank property names in lane, battlefield-object-id, battlefield and standby-slot snapshot objects produce explicit recovery diagnostics.

Validation:

- Focused single test: new snapshot lane property-name drift test passed `1/1`.
- Focused recovery: `MatchRecoveryTests` passed `221/221`.
- Adjacent recovery/opening/store-smoke: passed `802/802`.
- Backend full: passed `6167/6167`.
- Mechanical checks: `git diff --check`, anchored conflict-marker scan over `src`/`tests`/`docs`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness gate, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes only the spectator snapshot lane/battlefield/standby-slot property-name shape slice. Remaining spectator snapshot stack/nested payload property-name breadth, broader command/recovery/random determinism, full LayerEngine breadth, battlefield/battle lifecycle breadth, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and final readiness remain open.

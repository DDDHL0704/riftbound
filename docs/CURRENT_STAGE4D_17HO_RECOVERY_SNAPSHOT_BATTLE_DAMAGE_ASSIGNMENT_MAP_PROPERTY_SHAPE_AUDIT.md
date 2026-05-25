# Stage 4D-17HO Recovery Snapshot Battle Damage-Assignment Map Property Shape Audit

Date: 2026-05-25

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidateSnapshotShape` now validates property names for recovered player-view snapshot nested `Timing["battle"]["damageAssignment"]["damagePool"]`, `legalTargets`, `existingDamage` and `lethalDamageThreshold` map payloads. Map property names must be non-blank, must not carry surrounding whitespace, and duplicate normalized property names are rejected before battle damage-assignment map consumers can normalize those keys.

Test change: `RecoveryValidatorRejectsSnapshotTimingBattleDamageAssignmentMapPropertyNameDrift` proves duplicate normalized damage-assignment map keys, whitespace-mutated map keys and blank map keys produce explicit recovery diagnostics while preserving existing optional battle payload behavior.

Validation:

- Focused single test: new snapshot battle damage-assignment map property-name drift test passed `1/1`.
- Focused recovery: `MatchRecoveryTests` passed `242/242`.
- Adjacent recovery/opening/store-smoke: passed `823/823`.
- Backend full: passed `6188/6188`.
- Mechanical checks: `git diff --check`, anchored conflict-marker scan over `src`/`tests`/`docs`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness gate, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes only the recovered player-view snapshot battle damage-assignment nested map property-name shape slice. Remaining recovered/spectator nested payload property-name breadth, broader command/recovery/random determinism, full LayerEngine breadth, battlefield/battle lifecycle breadth, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and final readiness remain open.

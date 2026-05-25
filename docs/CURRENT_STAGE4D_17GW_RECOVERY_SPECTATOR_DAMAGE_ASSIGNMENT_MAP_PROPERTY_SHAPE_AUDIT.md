# Stage 4D-17GW Recovery Spectator Damage Assignment Map Property Shape Audit

Date: 2026-05-25

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidateSpectatorReplayFrame` now validates object property names for nested `Timing["battle"]["damageAssignment"]["damagePool"]`, `legalTargets`, `existingDamage` and `lethalDamageThreshold` map payloads. Map property names must be non-blank, must not carry surrounding whitespace, and duplicate normalized property names are rejected before map extraction consumes those payloads. This prevents duplicate JSON properties or spectator timing damage-assignment map key drift from being silently resolved by dictionary / `JsonElement` key lookup during recovery-frame spectator validation.

Test change: `RecoveryValidatorRejectsSpectatorReplayTimingBattleDamageAssignmentMapPropertyNameDrift` proves duplicate, whitespace-mutated and blank property names across all four nested map payloads produce explicit recovery diagnostics while preserving existing battle damage-assignment value parity checks.

Validation:

- Focused single test: new damage-assignment map property-name drift test passed `1/1`.
- Focused recovery: `MatchRecoveryTests` passed `224/224`.
- Adjacent recovery/opening/store-smoke: passed `805/805`.
- Backend full: passed `6170/6170`.
- Mechanical checks: `git diff --check`, anchored conflict-marker scan over `src`/`tests`/`docs`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness gate, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes only the spectator timing battle damage-assignment map property-name shape slice. Remaining spectator snapshot nested payload property-name breadth, broader command/recovery/random determinism, full LayerEngine breadth, battlefield/battle lifecycle breadth, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and final readiness remain open.

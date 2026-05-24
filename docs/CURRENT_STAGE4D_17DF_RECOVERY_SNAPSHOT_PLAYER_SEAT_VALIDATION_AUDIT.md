# Stage 4D-17DF Recovery Snapshot Player Seat Validation Audit

Date: 2026-05-24

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidatePlayerViews` now rejects recovered snapshot player payload seats with surrounding whitespace, values outside `P1` / `P2`, or duplicate normalized seats in the same snapshot player map. This prevents recovery baseline restoration from accepting seat maps that live `MatchSession` seat assignment and snapshot emission would not produce.

Test change: `RecoveryValidatorRejectsSnapshotPlayerSeatValueDrift` covers whitespace-wrapped `P1`, invalid `P3`, and duplicate normalized `P1` seats. The existing malformed-player-payload test still proves valid persisted JSON object payloads are accepted.

Validation:

- Focused recovery: `MatchRecoveryTests` passed `136/136`.
- Adjacent recovery/opening: passed `716/716`.
- Backend full: passed `6082/6082`.
- Mechanical gates: `git diff --check`, anchored conflict-marker scan, and matrix JSON parse passed before checkpoint commit.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, READY / READY-CANDIDATE, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes only recovered snapshot player seat value / duplicate validation. Broader command/recovery/random determinism, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and READY remain open.

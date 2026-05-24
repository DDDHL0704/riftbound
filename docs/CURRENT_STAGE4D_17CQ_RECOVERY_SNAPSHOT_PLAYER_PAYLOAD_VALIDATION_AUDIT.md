# Stage 4D-17CQ Recovery Snapshot Player Payload Validation Audit

Date: 2026-05-24

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidatePlayerViews` now validates each recovered snapshot `Players` entry before restoration. Player payloads must be object-shaped, carry a nonblank `id` equal to the player-map key, and carry a nonblank `seat`; both in-memory dictionary payloads and persisted `JsonElement` object payloads remain valid.

Test change: `RecoveryValidatorRejectsMalformedSnapshotPlayerPayloads` covers a mismatched payload id, blank seat, non-object payload and missing id, while also proving a valid JSON object payload does not produce an error.

Validation:

- Focused recovery: `MatchRecoveryTests` passed `121/121`.
- Adjacent recovery/opening: passed `701/701`.
- Backend full: passed `6067/6067`.
- Mechanical gates: `git diff --check`, anchored conflict-marker scan, and matrix JSON parse passed before checkpoint commit.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, READY / READY-CANDIDATE, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes only recovered snapshot player payload shape validation. Broader command/recovery/random determinism, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and READY remain open.

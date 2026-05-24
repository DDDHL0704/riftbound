# Stage 4D-17DP Recovery Authoritative Player-List Validation Audit

Date: 2026-05-24

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidateAuthoritativeState` now validates authoritative state player lists after seat-map validation. `ReadyPlayerIds`, `PassedPriorityPlayerIds`, `PassedFocusPlayerIds`, `MulliganCompletedPlayerIds`, and `DestroyedUnitOwnerIdsThisTurn` reject null lists, blank entries, entries with surrounding whitespace, duplicate normalized entries, and entries that do not resolve to a player in authoritative `Seats`.

Test change: `RecoveryValidatorRejectsAuthoritativeStatePlayerListsOutsideSeats` covers ready/pass/mulligan/destroyed-owner list members outside authoritative alice/bob seats.

Validation:

- Focused recovery: `MatchRecoveryTests` passed `146/146`.
- Adjacent recovery/opening: passed `726/726`.
- Backend full: passed `6092/6092`.
- Mechanical gates: `git diff --check`, anchored conflict-marker scan, and matrix JSON parse passed before checkpoint commit.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, READY / READY-CANDIDATE, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes only authoritative state player-list validation. Broader command/recovery/random determinism, deeper authoritative collection/object ownership validation, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and READY remain open.

# Stage 4D-17DD Recovery Snapshot Timing Player Id Normalization Validation Audit

Date: 2026-05-24

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidatePlayerViews` now rejects recovered player snapshot timing player ids with surrounding whitespace for `turnPlayerId`, optional player pointers and timing player-list members. Follow-on membership / duplicate checks still use the normalized id so diagnostics remain precise.

Test change: `RecoveryValidatorRejectsSnapshotTimingPlayerIdsWithSurroundingWhitespace` covers whitespace-wrapped `turnPlayerId`, `priorityPlayerId`, `readyPlayerIds` and `passedPriorityPlayerIds` values.

Validation:

- Focused recovery: `MatchRecoveryTests` passed `134/134`.
- Adjacent recovery/opening: passed `714/714`.
- Backend full: passed `6080/6080`.
- Mechanical gates: `git diff --check`, anchored conflict-marker scan, and matrix JSON parse passed before checkpoint commit.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, READY / READY-CANDIDATE, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes only recovered player snapshot timing player-id surrounding-whitespace validation. Broader command/recovery/random determinism, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and READY remain open.

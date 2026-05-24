# Stage 4D-17BW Recovery Action-Log Replay Initial-State Seat Map Parity Validation Audit

Date: 2026-05-24

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchActionLogReplayer.ValidateRecoveryFrameAsync` now rejects missing replay initial-state seats and verifies the replay initial-state seat map against the authoritative final state seats before action-log replay or recovery restore can continue.

Test change: `RegistryRejectsRecoveryFrameWhenReplayInitialStateSeatsMismatchFinalState` builds a real recovered ready command frame, swaps the replay initial-state seats while keeping active/turn player internally consistent with the corrupted seats, and proves `InMemoryMatchSessionRegistry` rejects the frame with `ErrorCodes.RecoveryInconsistent` plus the explicit seat-map parity diagnostic.

Validation:

- Focused recovery: `MatchRecoveryTests` passed `101/101`.
- Adjacent recovery/opening: passed `681/681`.
- Backend full: passed `6047/6047`.
- Mechanical gates: `git diff --check`, anchored conflict-marker scan, and matrix JSON parse passed before checkpoint commit.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, READY / READY-CANDIDATE, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes only replay initial-state seat map parity validation. Broader command/recovery/random determinism, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and READY remain open.

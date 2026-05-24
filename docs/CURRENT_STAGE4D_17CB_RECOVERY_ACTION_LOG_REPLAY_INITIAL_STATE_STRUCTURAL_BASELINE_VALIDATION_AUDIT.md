# Stage 4D-17CB Recovery Action-Log Replay Initial-State Structural Baseline Validation Audit

Date: 2026-05-24

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchActionLogReplayer.ValidateRecoveryFrameAsync` now validates replay initial-state structural collections and pending states before action-log replay or recovery restore can continue. Card objects, object locations, player decklists, stack items, trigger queue, battlefield/battle resolutions, until-end-of-turn effects, temporary payment resources, pending payment and pending hand choice must all be empty at the seats-derived opening baseline.

Test change: `RegistryRejectsRecoveryFrameWhenReplayInitialStateStructuralBaselineMismatches` builds a real recovered ready command frame, corrupts only replay initial-state structural/pending metadata, and proves `InMemoryMatchSessionRegistry` rejects the frame with `ErrorCodes.RecoveryInconsistent` plus explicit diagnostics for each field group.

Validation:

- Focused recovery: `MatchRecoveryTests` passed `106/106`.
- Adjacent recovery/opening: passed `686/686`.
- Backend full: passed `6052/6052`.
- Mechanical gates: `git diff --check`, anchored conflict-marker scan, and matrix JSON parse passed before checkpoint commit.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, READY / READY-CANDIDATE, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes only replay initial-state structural/pending baseline validation. Broader command/recovery/random determinism, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and READY remain open.

# Stage 4D-17FH Recovery Spectator Winning Score Validation Audit

Date: 2026-05-24

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidateSpectatorReplayFrame` now validates spectator snapshot timing `winningScore` against authoritative state. The guard mirrors the snapshot emitter's effective winning-score calculation: base score `8` plus controlled battlefield objects with card number `OGN·276/298` or `OGN·276a/298`.

Test change: new `RecoveryValidatorRejectsSpectatorReplayTimingWinningScoreMismatch` builds an authoritative state with a score-increasing battlefield object, mutates spectator timing `winningScore`, and proves the explicit diagnostic.

Validation:

- Focused recovery: `MatchRecoveryTests` passed `183/183`.
- Adjacent recovery/opening/store-smoke: passed `764/764`.
- Backend full: passed `6129/6129`.
- Mechanical checks: `git diff --check`, anchored conflict-marker scan, and matrix JSON parse passed.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, READY / READY-CANDIDATE, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes spectator timing winning-score parity only. Broader command/recovery/random determinism, full LayerEngine breadth, battlefield/battle lifecycle breadth, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and READY remain open.

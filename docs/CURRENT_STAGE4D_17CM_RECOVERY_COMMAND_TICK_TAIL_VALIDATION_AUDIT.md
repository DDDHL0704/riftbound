# Stage 4D-17CM Recovery Command Tick Tail Validation Audit

Date: 2026-05-24

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidateCommands` now rejects recovery frames with a non-empty command stream whose final recovered command `CompletedTick` does not match the recovery frame `currentTick`. Commandless spectator replay / authoritative-state frames remain allowed, but command-backed recovery must have match metadata aligned with the command-log tick tail.

Test change: `RecoveryValidatorRejectsRecoveredCommandTickTailBeforeRecoveryTick` builds a recovered command stream ending at tick `1` while the recovery frame current tick is `3`, then proves the validator reports the explicit tick-tail diagnostic.

Validation:

- Focused recovery: `MatchRecoveryTests` passed `117/117`.
- Adjacent recovery/opening: passed `697/697`.
- Backend full: passed `6063/6063`.
- Mechanical gates: `git diff --check`, anchored conflict-marker scan, and matrix JSON parse passed before checkpoint commit.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, READY / READY-CANDIDATE, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes only recovered command tick-tail validation. Broader command/recovery/random determinism, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and READY remain open.

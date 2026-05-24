# Stage 4D-17DI Recovery Rejected Command Diagnostic Normalization Validation Audit

Date: 2026-05-24

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidateCommands` now rejects surrounding whitespace in rejected recovered command `ErrorMessage` diagnostics. The guard keeps base recovery-frame validation aligned with the exact rejected-command diagnostic comparison performed during action-log replay audit.

Test change: `RecoveryValidatorRejectsRejectedCommandDiagnosticWithSurroundingWhitespace` covers a rejected command whose diagnostic is `" rejected diagnostic "`.

Validation:

- Focused recovery: `MatchRecoveryTests` passed `139/139`.
- Adjacent recovery/opening: passed `719/719`.
- Backend full: passed `6085/6085`.
- Mechanical gates: `git diff --check`, anchored conflict-marker scan, and matrix JSON parse passed before checkpoint commit.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, READY / READY-CANDIDATE, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes only rejected recovered command diagnostic surrounding-whitespace validation. Broader command/recovery/random determinism, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and READY remain open.

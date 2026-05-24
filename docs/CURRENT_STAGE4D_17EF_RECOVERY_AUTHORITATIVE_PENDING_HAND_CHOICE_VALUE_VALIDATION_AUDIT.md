# Stage 4D-17EF Recovery Authoritative Pending Hand-Choice Value Validation Audit

Date: 2026-05-24

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidateAuthoritativeState` now validates authoritative `PendingHandChoice` scalar/list/count metadata independently of player and object-reference checks. The guard rejects blank or whitespace-mutated choice ids and choice windows, invalid required counts, max counts below required counts, malformed / duplicate legal object ids, insufficient distinct legal objects for the required choice count, and null legal-object lists.

Test change: `RecoveryValidatorRejectsAuthoritativeStatePendingHandChoiceValueDrift` covers pending hand-choice id/window whitespace, max-count below required-count drift, legal-object whitespace / duplicate / blank entries, insufficient distinct legal objects, invalid required counts, max-count drift against invalid required counts, and null legal-object lists.

Validation:

- Focused recovery: `MatchRecoveryTests` passed `162/162`.
- Adjacent recovery/opening: passed `742/742`.
- Backend full: passed `6108/6108`.
- Mechanical gates: `git diff --check`, anchored conflict-marker scan, and matrix JSON parse passed before checkpoint commit.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, READY / READY-CANDIDATE, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes only authoritative pending hand-choice value validation. Broader command/recovery/random determinism, battlefield/battle lifecycle breadth, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and READY remain open.

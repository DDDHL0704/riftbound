# Stage 4D-17DT Recovery Authoritative Pending-Player Validation Audit

Date: 2026-05-24

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidateAuthoritativeState` now validates authoritative pending payment, pending hand-choice, and temporary payment resource player references after stack/trigger controller validation. Pending payment and pending hand-choice are optional states, but when present their player ids are required, reject surrounding whitespace, and must resolve to a player in authoritative `Seats`. `TemporaryPaymentResources` is a required list; null entries are rejected; each temporary resource owner player id is required, whitespace-clean, and must resolve to a seated player.

Test change: `RecoveryValidatorRejectsAuthoritativeStatePendingPlayersOutsideSeats` covers pending payment, pending hand-choice, and temporary payment resource owner player ids outside authoritative alice/bob seats.

Validation:

- Focused recovery: `MatchRecoveryTests` passed `150/150`.
- Adjacent recovery/opening: passed `730/730`.
- Backend full: passed `6096/6096`.
- Mechanical gates: `git diff --check`, anchored conflict-marker scan, and matrix JSON parse passed before checkpoint commit.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, READY / READY-CANDIDATE, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes only authoritative pending-player validation. Broader command/recovery/random determinism, object-id/location coherence, object-reference validation, battlefield/battle ownership validation, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and READY remain open.

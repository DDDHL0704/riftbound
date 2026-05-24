# Stage 4D-17EB Recovery Authoritative Resource Value Validation Audit

Date: 2026-05-24

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidateAuthoritativeState` now validates authoritative final state resource and counter metadata before deeper player/object/history checks consume it. Rune pools reject negative mana/power and malformed power-by-trait maps. Player score and experience counters reject negative values. Cards-played entries reject nonpositive values. Temporary payment resources reject malformed/duplicate ids, negative generated or remaining power, negative or future creation ticks, malformed generated/remaining trait maps, and blank / whitespace-mutated / duplicate allowed-payment-kind values.

Test change: `RecoveryValidatorRejectsAuthoritativeStateResourceValueDrift` covers rune pool numeric drift, rune trait-map drift, score/experience negative values, cards-played zero values, temporary payment resource negative/future values, temporary trait-map drift, and duplicate allowed-payment-kind drift.

Validation:

- Focused recovery: `MatchRecoveryTests` passed `158/158`.
- Adjacent recovery/opening: passed `738/738`.
- Backend full: passed `6104/6104`.
- Mechanical gates: `git diff --check`, anchored conflict-marker scan, and matrix JSON parse passed before checkpoint commit.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, READY / READY-CANDIDATE, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes only authoritative resource/counter value validation. Broader command/recovery/random determinism, battlefield/battle lifecycle breadth, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and READY remain open.

# Stage 4D-17DZ Recovery Authoritative Resolution History Value Validation Audit

Date: 2026-05-24

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidateAuthoritativeState` now validates authoritative `BattlefieldResolutions` and `BattleResolutions` resolution-history metadata before player/object reference checks. Resolution ids must be present, whitespace-clean, and duplicate-free after normalization. Resolution ticks must be nonnegative and not ahead of the authoritative state tick. Kind and reason values must be present and whitespace-clean. Related event-kind lists must be present, whitespace-clean, and duplicate-free after normalization.

Test change: `RecoveryValidatorRejectsAuthoritativeStateResolutionHistoryValueDrift` covers `battlefield-resolution-1` and `battle-resolution-1` id whitespace/duplicates, negative and future ticks, kind/reason drift, and duplicated related event kinds.

Validation:

- Focused recovery: `MatchRecoveryTests` passed `156/156`.
- Adjacent recovery/opening: passed `736/736`.
- Backend full: passed `6102/6102`.
- Mechanical gates: `git diff --check`, anchored conflict-marker scan, and matrix JSON parse passed before checkpoint commit.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, READY / READY-CANDIDATE, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes only authoritative resolution-history value validation. Broader command/recovery/random determinism, battlefield/battle lifecycle breadth, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and READY remain open.

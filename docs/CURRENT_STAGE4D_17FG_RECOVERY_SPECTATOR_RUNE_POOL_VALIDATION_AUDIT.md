# Stage 4D-17FG Recovery Spectator Rune Pool Validation Audit

Date: 2026-05-24

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidateSpectatorReplayFrame` now validates spectator snapshot player `runePool` payloads against authoritative `RunePool`. The guard covers `mana`, total `power`, `untypedPower`, and the typed `powerByTrait` dictionary, defaulting missing authoritative pools to `RunePool.Empty` to mirror snapshot emission.

Test change: new `RecoveryValidatorRejectsSpectatorReplaySnapshotPlayerRunePoolMismatch` builds a spectator snapshot with mixed untyped and typed rune power, mutates every covered rune-pool field, and proves explicit diagnostics for mana, total power, untyped power, and typed power drift.

Validation:

- Focused recovery: `MatchRecoveryTests` passed `182/182`.
- Adjacent recovery/opening/store-smoke: passed `763/763`.
- Backend full: passed `6128/6128`.
- Mechanical checks: `git diff --check`, anchored conflict-marker scan, and matrix JSON parse passed.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, READY / READY-CANDIDATE, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes spectator player rune-pool payload parity only. Broader command/recovery/random determinism, full LayerEngine breadth, battlefield/battle lifecycle breadth, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and READY remain open.

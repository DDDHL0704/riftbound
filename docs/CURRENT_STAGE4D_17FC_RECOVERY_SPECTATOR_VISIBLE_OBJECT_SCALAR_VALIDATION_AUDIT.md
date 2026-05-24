# Stage 4D-17FC Recovery Spectator Visible Object Scalar Validation Audit

Date: 2026-05-24

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidateSpectatorReplayFrame` now validates spectator snapshot visible player object scalar payloads against authoritative `CardObjectState`. The guard covers `cardNo`, `ownerId`, `controllerId`, `attachedToObjectId`, `damage`, `power`, `basePower`, `effectivePower`, `untilEndOfTurnPowerModifier`, `manaCost`, `isExhausted`, `isAttacking`, and `isDefending` after player object coverage and face-down redaction checks pass.

Test change: new `RecoveryValidatorRejectsSpectatorReplaySnapshotVisiblePlayerObjectScalarMismatch` builds a spectator-visible battlefield object, mutates all covered scalar fields, and proves explicit diagnostics for identity metadata, attachment metadata, combat/resource numbers, and object state booleans.

Validation:

- Focused recovery: `MatchRecoveryTests` passed `178/178`.
- Adjacent recovery/opening/store-smoke: passed `759/759`.
- Backend full: passed `6124/6124`.
- Mechanical checks: `git diff --check`, anchored conflict-marker scan, and matrix JSON parse passed.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, READY / READY-CANDIDATE, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes spectator visible object scalar metadata parity only. Object tag/effect list parity, object location parity, broader command/recovery/random determinism, full LayerEngine breadth, battlefield/battle lifecycle breadth, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and READY remain open.

# Stage 4D-17FP Recovery Spectator Temporary Payment Resource Validation Audit

Date: 2026-05-24

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidateSpectatorReplayFrame` now validates spectator snapshot timing `temporaryPaymentResources` against authoritative `TemporaryPaymentResourceState` entries. The validator checks list presence/count and per-resource `resourceId`, owner, source object, ability id, payment window, generated/remaining power, generated/remaining trait-power dictionaries, allowed payment kinds, payment-only flag, resource restriction, and created tick.

Test change: new `RecoveryValidatorRejectsSpectatorReplayTimingTemporaryPaymentResourcesMismatch` builds an authoritative temporary payment resource with generic and typed temporary power, proves the spectator restriction shape, mutates every emitted field, and proves the recovery validator reports temporary-resource payload drift.

Validation:

- Focused recovery: `MatchRecoveryTests` passed `191/191`.
- Adjacent recovery/opening/store-smoke: passed `772/772`.
- Backend full: passed `6137/6137`.
- Mechanical checks: `git diff --check`, anchored conflict-marker scan, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, READY / READY-CANDIDATE, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes spectator timing temporary payment resource item payload parity only. Continuous effects, trigger queue, broader command/recovery/random determinism, full LayerEngine breadth, battlefield/battle lifecycle breadth, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and READY remain open. Per user instruction, A_MAIN pauses after this batch instead of opening the next runtime slice.

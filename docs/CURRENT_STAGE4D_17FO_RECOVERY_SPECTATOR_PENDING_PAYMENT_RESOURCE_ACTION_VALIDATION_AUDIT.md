# Stage 4D-17FO Recovery Spectator Pending Payment Resource Action Validation Audit

Date: 2026-05-24

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidateSpectatorReplayFrame` now validates spectator snapshot timing `pendingPayment.paymentResourceActions` against the authoritative derived resource-action list. The expected list mirrors the snapshot generator: explicit pending-payment resource actions, legal `RECYCLE_RUNE:*` choices, temporary payment resources that can help the current rune/power cost, and Blue Sentinel delayed resource actions.

Test change: new `RecoveryValidatorRejectsSpectatorReplayTimingPendingPaymentResourceActionsMismatch` builds a pending payment with explicit, recycle-rune, and temporary-resource action sources, proves the emitted spectator list, mutates it, and proves the recovery validator reports resource-action drift.

Validation:

- Focused recovery: `MatchRecoveryTests` passed `190/190`.
- Adjacent recovery/opening/store-smoke: passed `771/771`.
- Backend full: passed `6136/6136`.
- Mechanical checks: `git diff --check`, anchored conflict-marker scan, and matrix JSON parse passed.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, READY / READY-CANDIDATE, or `riftbound-dotnet.sln` changes. Full temporary payment resource item payload parity remains outside this slice.

Remaining risk: this closes spectator timing pending-payment resource-action derivation parity only. Temporary payment resource item payloads, continuous effects, trigger queue, broader command/recovery/random determinism, full LayerEngine breadth, battlefield/battle lifecycle breadth, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and READY remain open.

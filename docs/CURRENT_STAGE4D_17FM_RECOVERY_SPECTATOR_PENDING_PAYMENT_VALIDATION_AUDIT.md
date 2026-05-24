# Stage 4D-17FM Recovery Spectator Pending Payment Validation Audit

Date: 2026-05-24

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidateSpectatorReplayFrame` now validates spectator snapshot timing `pendingPayment` base payloads against authoritative `PendingPayment`. The guard covers required/null shape, payment id, payment window, player id, cost mana, cost power, cost `powerByTrait`, and payment choices.

Test change: new `RecoveryValidatorRejectsSpectatorReplayTimingPendingPaymentMismatch` builds a pending `PAY_COST` recovery frame, mutates the spectator `pendingPayment` base and cost payloads, and proves explicit diagnostics for payment identity, player/window, cost, trait cost, and payment-choice drift.

Validation:

- Focused recovery: `MatchRecoveryTests` passed `188/188`.
- Adjacent recovery/opening/store-smoke: passed `769/769`.
- Backend full: passed `6134/6134`.
- Mechanical checks: `git diff --check`, anchored conflict-marker scan, and matrix JSON parse passed.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, READY / READY-CANDIDATE, or `riftbound-dotnet.sln` changes. Derived `pendingPayment.paymentResourceActions` parity remains outside this slice.

Remaining risk: this closes spectator timing pending-payment base payload parity only. Pending payment resource action derivation, pending hand choice, temporary payment resources, continuous effects, trigger queue, broader command/recovery/random determinism, full LayerEngine breadth, battlefield/battle lifecycle breadth, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and READY remain open.

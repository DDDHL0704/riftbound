# Stage 4D-04X PaymentEngine Trigger Payment Replay Audit

Date: 2026-05-21

Status: NOT READY

## Scope

This B_SERVER slice covers focused stale replay rejection after a `TRIGGER_PAYMENT` window has already closed.

Allowed files touched:

- `tests/Riftbound.ConformanceTests/TriggerPaymentTests.cs`

Runtime files were not changed. Matrix JSON, `PaymentEngineCoverageAuditTests.cs`, frontend, official catalog/data, browser/Chrome/formal E2E scripts, protocol core fields, `fullOfficial`, READY flags, and `riftbound-dotnet.sln` were not touched.

## Findings

- Replaying the original successful Gold trigger-payment `PAY_COST` command after the window closes is rejected without mutation.
- Replaying the original decline command after the window closes is rejected without mutation.
- Replays emit no duplicate `COST_PAID`, `TRIGGER_PAYMENT_DECLINED`, `PAYMENT_WINDOW_CLOSED`, `BATTLEFIELD_TRIGGER_RESOLVED`, or token-creation events.
- Successful-payment replay does not create a second Gold token.
- For both payment and decline branches, the queued next contested battlefield remains in the already advanced spell-duel state and does not fork or advance again.

## Runtime Assessment

No runtime bug was exposed by this focused slice. No server runtime code changed.

## Hidden Info And Protocol

No hidden-information leakage was found. No protocol shape changed.

## A_MAIN Acceptance

A_MAIN accepted this focused slice after fresh validation on main:

- `source scripts/dev-env.sh && dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~TriggerPaymentTests"`: passed, 67/67.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PaymentEngine|FullyQualifiedName~PaymentPlan|FullyQualifiedName~PAY_COST|FullyQualifiedName~ActionPrompt|FullyQualifiedName~Prompt|FullyQualifiedName~TriggerPayment"`: passed, 958/958.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`: passed, 5254/5254.
- `git diff --check`: passed.

## Remaining Open Items

- P0-005 remains open beyond this narrow stale-replay proof.
- P0-004 adjacency-sensitive battle lifecycle remains open.
- Full PaymentEngine breadth, card-matrix readiness, frontend final gates, Chrome smoke, formal 18-step E2E, `fullOfficial`, and READY remain open.

Final conclusion: NOT READY.

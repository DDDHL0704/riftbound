# Stage 4D-04W PaymentEngine Trigger Payment Window Identity Audit

Date: 2026-05-21

Status: NOT READY

## Scope

This B_SERVER slice covers focused server-authoritative `TRIGGER_PAYMENT` pending-payment identity checks only.

Allowed files touched:

- `tests/Riftbound.ConformanceTests/TriggerPaymentTests.cs`

Runtime files were inspected but not changed. Matrix JSON, `PaymentEngineCoverageAuditTests.cs`, frontend, official catalog/data, browser/Chrome/formal E2E scripts, protocol core fields, `fullOfficial`, READY flags, and `riftbound-dotnet.sln` were not touched.

A_MAIN acceptance reran focused trigger-payment coverage (65/65), adjacent payment/prompt coverage (955/955), backend full test (5251/5251), and `git diff --check`; all passed.

## Findings

- Battlefield-conquer Gold trigger payment rejects a `PAY_COST` command with the wrong `paymentId` without mutation.
- Battlefield-conquer Gold trigger payment rejects a `PAY_COST` command with the wrong `paymentWindow` without mutation.
- The active `PendingPayment` remains open after both rejections, preserving the original payment id, payment window, and legal choices.
- The rejected commands emit no `COST_PAID`, `TRIGGER_PAYMENT_DECLINED`, `PAYMENT_WINDOW_CLOSED`, `BATTLEFIELD_TRIGGER_RESOLVED`, or token-creation events.
- The queued next contested battlefield remains blocked after both rejected commands.

## Runtime Assessment

No runtime bug was exposed by this focused slice. No server runtime code changed.

## Hidden Info And Protocol

No hidden-information leakage was found. No protocol shape changed.

## Remaining Open Items

- P0-005 remains open beyond this narrow identity-check proof.
- P0-004 adjacency-sensitive battle lifecycle remains open.
- Full PaymentEngine breadth, card-matrix readiness, frontend final gates, Chrome smoke, formal 18-step E2E, `fullOfficial`, and READY remain open.

Final conclusion: NOT READY.

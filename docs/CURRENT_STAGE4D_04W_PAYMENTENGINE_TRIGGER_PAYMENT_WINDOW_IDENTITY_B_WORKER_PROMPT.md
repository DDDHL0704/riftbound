# Stage 4D-04W B Worker Prompt: PaymentEngine Trigger Payment Window Identity

Date: 2026-05-21

Owner: `B_SERVER`

Status: DISPATCHED / NOT READY

## Objective

Close one concrete P0-005 PaymentEngine residual slice by proving server-authoritative pending-payment identity checks for `TRIGGER_PAYMENT` windows.

The current trigger payment tests cover prompt creation, successful payment, decline, invalid choices, insufficient mana and next-contest blocking. This slice must add focused coverage for stale or mismatched `PAY_COST` commands that carry a wrong `paymentId` or wrong `paymentWindow` against an active trigger payment window.

## Required Behavior

For at least the battlefield-conquer Gold trigger payment representative:

- A `PAY_COST` command with the wrong `paymentId` must be rejected without mutation.
- A `PAY_COST` command with the wrong `paymentWindow` must be rejected without mutation.
- The active `PendingPayment` must remain open after those rejects.
- No `COST_PAID`, `TRIGGER_PAYMENT_DECLINED`, `PAYMENT_WINDOW_CLOSED`, `BATTLEFIELD_TRIGGER_RESOLVED` or token-creation event may be emitted by those rejected commands.
- The next contested battlefield must remain blocked if the invalid command is submitted while a next contest is queued.

If the runtime already behaves correctly, add tests only. If a bug is exposed, patch the smallest server-side path needed.

## Allowed Files

B may modify:

- `src/Riftbound.Engine/CoreRuleEngine.cs` only if a runtime bug is exposed.
- `tests/Riftbound.ConformanceTests/TriggerPaymentTests.cs`
- optional focused helper edits in existing conformance tests if needed for this exact slice.
- `docs/CURRENT_STAGE4D_04W_PAYMENTENGINE_TRIGGER_PAYMENT_WINDOW_IDENTITY_AUDIT.md`
- `docs/CURRENT_STAGE4D_04W_PAYMENTENGINE_TRIGGER_PAYMENT_WINDOW_IDENTITY_EVIDENCE.md`

## Locked Files And Scope

B must not touch:

- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`
- frontend files
- official catalog/data files
- Chrome/browser/formal E2E scripts
- API/protocol core field names
- `fullOfficial` / READY flags
- `riftbound-dotnet.sln`

## Validation Required

Run and report:

- focused trigger payment tests
- adjacent payment/prompt tests if runtime changes
- backend full `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` if runtime changes, or if the focused/adjacent surface indicates broader risk
- `git diff --check`

## Non-Closure

This slice must keep P0-005, P0-004 adjacency-sensitive battle lifecycle, full PaymentEngine breadth, card matrix readiness, frontend final gates, Chrome smoke, formal 18-step E2E, `fullOfficial`, READY and goal completion open.

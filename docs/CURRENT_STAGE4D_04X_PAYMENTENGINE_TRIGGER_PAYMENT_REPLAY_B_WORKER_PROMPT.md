# Stage 4D-04X B Worker Prompt: PaymentEngine Trigger Payment Replay

Date: 2026-05-21

Owner: `B_SERVER`

Status: DISPATCHED / NOT READY

## Objective

Close one concrete P0-005 PaymentEngine residual slice by proving server-authoritative stale replay rejection after a `TRIGGER_PAYMENT` window has already closed.

The 04W slice proved wrong `paymentId` / wrong `paymentWindow` commands are rejected while a trigger payment window is active. This 04X slice must cover replay of the original valid `PAY_COST` command after the window has closed by successful payment or by decline.

## Required Behavior

For at least the battlefield-conquer Gold trigger payment representative:

- Submit the valid `PAY_COST` command and close the trigger payment window through successful payment.
- Replay the same valid `PAY_COST` command against the post-payment state.
- The replay must be rejected without mutation.
- The replay must not create another Gold token.
- The replay must not emit duplicate `COST_PAID`, `PAYMENT_WINDOW_CLOSED`, `BATTLEFIELD_TRIGGER_RESOLVED`, `TRIGGER_PAYMENT_DECLINED`, or token-creation events.
- If a next contested battlefield is queued, the already advanced post-payment state must not fork or advance again.

Also cover the decline branch:

- Submit the valid decline command and close the trigger payment window.
- Replay the same decline command against the post-decline state.
- The replay must be rejected without mutation.
- The replay must not emit duplicate decline/close/resolution/cost/token events.
- If a next contested battlefield is queued, the post-decline state must not fork or advance again.

If the runtime already behaves correctly, add tests only. If a bug is exposed, patch the smallest server-side path needed.

## Allowed Files

B may modify:

- `src/Riftbound.Engine/CoreRuleEngine.cs` only if a runtime bug is exposed.
- `tests/Riftbound.ConformanceTests/TriggerPaymentTests.cs`
- optional focused helper edits in existing conformance tests if needed for this exact slice.
- `docs/CURRENT_STAGE4D_04X_PAYMENTENGINE_TRIGGER_PAYMENT_REPLAY_AUDIT.md`
- `docs/CURRENT_STAGE4D_04X_PAYMENTENGINE_TRIGGER_PAYMENT_REPLAY_EVIDENCE.md`

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
- adjacent payment/prompt tests if runtime changes or if focused results suggest broader risk
- backend full `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` if runtime changes, or if A_MAIN requests acceptance-level validation
- `git diff --check`

## Non-Closure

This slice must keep P0-005, P0-004 adjacency-sensitive battle lifecycle, full PaymentEngine breadth, card matrix readiness, frontend final gates, Chrome smoke, formal 18-step E2E, `fullOfficial`, READY and goal completion open.

# Stage 4D-06Y Pending Pay Cost Duplicate Client Intent Audit

Date: 2026-05-22

Owner: `A_MAIN`

Status: accepted server test / pending `PAY_COST` duplicate client-intent closure slice. Project remains **NOT READY**.

## Scope

This slice covers same-client-intent retries for ordinary pending `PAY_COST` windows and temporary payment-resource pending `PAY_COST` windows at the `MatchSession` idempotency boundary. It follows the 4D-06X stale prompt replay slice: 06X proved a new client intent with an old prompt stamp rejects after the payment window closes; 06Y proves the original client intent can be retried safely and returns the cached accepted result without re-running payment resolution.

The accepted coverage proves that pending `PAY_COST` accepts once, closes the payment window, clears the pay-cost prompt and consumes ordinary or temporary resources exactly once. A duplicate submit with the same client intent and the same prompt-scoped raw command returns the cached accepted result after the window has closed, while journal recording still contains only one `PAY_COST` entry and the authoritative state hash remains unchanged after the duplicate.

No runtime behavior change was required because `MatchSession.SubmitAsync(...)` already checks the per-player client-intent cache before stale-prompt validation or core command resolution.

Locked surfaces remained unchanged: runtime behavior, matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Added `PendingPayCostDuplicateClientIntentAfterWindowClosesReturnsCachedOrdinaryResultWithoutMutation` in `tests/Riftbound.ConformanceTests/PaymentEngineUnificationTests.cs`.
- Added `PendingPayCostDuplicateClientIntentAfterWindowClosesReturnsCachedTemporaryResourceResultWithoutMutation` in `tests/Riftbound.ConformanceTests/PaymentEngineUnificationTests.cs`.
- Ordinary coverage runs mana, generic-power and typed-power pending-payment shapes through `MatchSession`, captures the current pay-cost prompt stamp, accepts the first submit, then retries the same client intent after the payment window has closed.
- Temporary-resource coverage does the same for a generated payment-only resource, proving the resource is spent and cleared once before the duplicate returns the cached result.
- Each duplicate retry asserts accepted cached events, exact `MatchStateHasher.Hash(...)` preservation, no reopened pending payment, no pay-cost prompt fork, no stack drift and exactly one journaled `PAY_COST` command.

## Non-Closure

This narrows PaymentEngine pending `PAY_COST` same-intent idempotency, duplicate window-close and temporary-resource duplicate-spend risk only. It does not close full PaymentEngine / PAY_COST breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, full official matrix, card matrix readiness, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` or READY.

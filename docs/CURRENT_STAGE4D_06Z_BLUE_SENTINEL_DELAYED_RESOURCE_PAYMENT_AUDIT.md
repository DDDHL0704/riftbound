# Stage 4D-06Z Blue Sentinel Delayed Resource Payment Audit

Date: 2026-05-22

Owner: `A_MAIN`

Status: accepted server test / Blue Sentinel delayed resource `PAY_COST` quote-command-audit parity slice. Project remains **NOT READY**.

## Scope

This slice covers the Blue Sentinel held-battlefield delayed resource path when it is quoted into the next main-phase pending `PAY_COST` rune-payment window.

The accepted coverage proves that the authoritative prompt quotes the delayed resource action with `paymentResourceActionIds` and `paymentResourcePowerByChoice`, including generated power, payment-only lifecycle, source object, battlefield, delayed trigger, ability id, resource restriction and allowed payment kind metadata. It also proves that submitting the quoted delayed-resource action materializes exactly one temporary payment resource, spends and clears it through the PaymentEngine, closes the payment window and records matching `TRIGGER_RESOLVED`, `ABILITY_ACTIVATED`, `POWER_GAINED`, `TEMPORARY_PAYMENT_RESOURCE_SPENT`, `TEMPORARY_PAYMENT_RESOURCE_CLEARED`, `COST_PAID` and `PAYMENT_WINDOW_CLOSED` audit payloads.

No runtime behavior change was required because the existing Blue Sentinel delayed-resource implementation already emitted the prompt metadata, temporary-resource ledger events and `COST_PAID` payment-resource action mapping needed for this contract.

Locked surfaces remained unchanged: runtime behavior, matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Strengthened `BlueSentinelDelayedResourceIsPromptedAndConsumedOnlyForNextMainRunePayment` in `tests/Riftbound.ConformanceTests/BlueSentinelResourceSkillTests.cs`.
- Added prompt assertions for delayed-resource quote metadata: action id, power, payment-only lifecycle, delayed trigger id, source object id, battlefield object id, ability id, resource restriction and allowed payment kind.
- Added accepted-payment assertions for event order and audit payload parity across trigger resolution, resource activation, power gain, temporary-resource spend/cleanup, cost paid and payment-window close.
- Added `COST_PAID` assertions that the submitted delayed action maps to the materialized temporary-resource action, preserves the original `paymentChoiceIds`, records legal spend choices and carries temporary-resource ids / power into the cost payload.

## Non-Closure

This narrows the Blue Sentinel delayed-resource pending `PAY_COST` quote-command-audit contract only. It does not close full PaymentEngine / PAY_COST breadth, all resource-skill official breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, full official matrix, card matrix readiness, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` or READY.

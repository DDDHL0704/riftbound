# Stage 4D-04W PaymentEngine Trigger Payment Window Identity Evidence

Date: 2026-05-21

Status: NOT READY

## Test Evidence

Focused trigger payment coverage:

```sh
source scripts/dev-env.sh && dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~TriggerPaymentTests"
```

Result: passed, 65/65.

A_MAIN adjacent acceptance:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PaymentEngine|FullyQualifiedName~PaymentPlan|FullyQualifiedName~PAY_COST|FullyQualifiedName~ActionPrompt|FullyQualifiedName~Prompt|FullyQualifiedName~TriggerPayment"
```

Result: passed, 955/955.

A_MAIN backend full acceptance:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

Result: passed, 5251/5251.

`git diff --check`: passed.

## Added Coverage

- `BattlefieldConquerGoldTriggerPaymentRejectsWrongPaymentIdentityWithoutMutation`

The theory covers:

- wrong `paymentId`
- wrong `paymentWindow`

Both cases assert no mutation, preserved `PendingPayment`, no trigger-payment side-effect events, no token creation, and no advancement of the queued next contested battlefield.

## Runtime And Protocol Evidence

Runtime changed: no.

Protocol shape changed: no.

Hidden-info leakage found: no.

Final conclusion: NOT READY.

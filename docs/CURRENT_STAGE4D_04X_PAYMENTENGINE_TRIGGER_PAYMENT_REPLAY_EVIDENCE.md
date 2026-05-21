# Stage 4D-04X PaymentEngine Trigger Payment Replay Evidence

Date: 2026-05-21

Status: NOT READY

## Test Evidence

Focused trigger payment coverage:

```sh
source scripts/dev-env.sh && dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~TriggerPaymentTests"
```

Result: passed, 67/67.

## Added Coverage

- `BattlefieldConquerGoldTriggerPaymentRejectsPostPaymentReplayWithoutMutation`
- `BattlefieldConquerGoldTriggerPaymentRejectsPostDeclineReplayWithoutMutation`

Both cases use the battlefield-conquer Gold trigger payment representative with a queued next contested battlefield. They assert replay rejection, no events, exact state hash preservation, no duplicate token creation, and no second next-contest advancement.

## Runtime And Protocol Evidence

Runtime changed: no.

Protocol shape changed: no.

Hidden-info leakage found: no.

## A_MAIN Validation

Fresh A_MAIN validation on main:

```sh
source scripts/dev-env.sh && dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~TriggerPaymentTests"
```

Result: passed, 67/67.

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PaymentEngine|FullyQualifiedName~PaymentPlan|FullyQualifiedName~PAY_COST|FullyQualifiedName~ActionPrompt|FullyQualifiedName~Prompt|FullyQualifiedName~TriggerPayment"
```

Result: passed, 958/958.

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

Result: passed, 5254/5254.

```sh
git diff --check
```

Result: passed.

Final conclusion: NOT READY.

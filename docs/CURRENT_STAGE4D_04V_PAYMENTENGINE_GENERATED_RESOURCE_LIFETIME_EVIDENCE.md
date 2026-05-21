# Stage 4D-04V PaymentEngine Generated Resource Lifetime Evidence

Date: 2026-05-21

Status: NOT READY

## Test Evidence

Focused generated-resource coverage:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GeneratedResource|FullyQualifiedName~TemporaryPaymentResource|FullyQualifiedName~GoldToken|FullyQualifiedName~ResourceConversion|FullyQualifiedName~LegendResourceBridge|FullyQualifiedName~LuxResource|FullyQualifiedName~Honeyfruit|FullyQualifiedName~BlueSentinel|FullyQualifiedName~PaymentEngineUnification"
```

Result: passed, 258/258.

Adjacent payment / prompt coverage:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PaymentEngine|FullyQualifiedName~PaymentPlan|FullyQualifiedName~PAY_COST|FullyQualifiedName~ActionPrompt|FullyQualifiedName~Prompt|FullyQualifiedName~ResourceSkill"
```

Result: passed, 1089/1089.

Backend full validation for A acceptance:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

Result: passed, 5247/5247.

Final hygiene:

```sh
git diff --check
```

Result: passed.

## Added Coverage

- `PendingPayCostRejectsStaleTemporaryPaymentResourceReplayWithoutMutation`
- `PendingPayCostRejectsWrongPlayerOrWindowTemporaryPaymentResourceWithoutMutation`
- Existing `PendingPayCostCommitsGenericTemporaryPaymentResourceThroughPaymentPlan` now verifies that the prompt no longer exposes PayCost after successful clear.
- `EnergyChannelGeneratedResourceManaCannotBeSpentTwiceWithoutMutation`

## Runtime And Protocol Evidence

Runtime changed: no.

Protocol shape changed: no.

Hidden-info leakage found: no.

Final conclusion: NOT READY.

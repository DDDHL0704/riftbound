# Stage 4D-04S PaymentEngine Lux High-Cost Paid-Cost Evidence

Date: 2026-05-21

Conclusion: **VALIDATED / PROJECT NOT READY**

## Scope

This evidence records A-side validation for the 4D-04S Lux high-cost paid-cost representative slice.

Changed runtime / test files:

- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `tests/Riftbound.ConformanceTests/LuxHighCostPaidCostTriggerTests.cs`

No frontend, matrix JSON, official catalog, Chrome/browser script, formal E2E script, protocol core field, fullOfficial, READY or `riftbound-dotnet.sln` changes were made.

## Validation Commands

Focused 04S tests:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~LuxHighCostPaidCostTriggerTests"
```

Result: **passed 3/3**.

Lux / HighCost / RealTriggerQueue focused-adjacent filter:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Lux|FullyQualifiedName~HighCost|FullyQualifiedName~RealTriggerQueue"
```

Result: **passed 81/81**.

PaymentEngine / trigger / Lux adjacent filter:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PaymentEngineUnificationTests|FullyQualifiedName~TriggerPayment|FullyQualifiedName~RealTriggerQueue|FullyQualifiedName~LuxResourceSkillTests"
```

Result: **passed 174/174**.

Backend full:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

Result: **passed 5234/5234**.

Patch hygiene:

```sh
git diff --check
```

Result: **passed**.

## Evidence Summary

The new focused tests verify:

- a printed high-cost spell reduced below the Lux threshold does not trigger unit Lux or legend Lux;
- a lower printed-cost spell raised to the threshold by Spellshield tax triggers both unit Lux and legend Lux;
- paid cost payloads expose the authoritative server quote and committed payment values;
- insufficient Spellshield-tax payment rejects the play and leaves state unmutated;
- hidden draw details are not exposed to the opponent snapshot or public draw event payload.

## Non-Closure

This slice does not close full PaymentEngine official breadth, card matrix fullOfficial, 03MR DOC_MATRIX, FAQ evidence breadth, frontend final validation, Chrome smoke, formal 18-step E2E, completion audit or READY.

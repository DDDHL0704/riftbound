# Stage 4D-03CN PaymentEngine Legend Resource Bridge Rune-Pool Lifecycle Evidence

Evidence date: 2026-05-16
Conclusion: **ACCEPTED FOCUSED LIFECYCLE VERIFIER / NOT READY**

## 1. Changed Files

- `src/Riftbound.Engine/CoreRuleEngine.cs`
  - `MANA_GAINED` / `POWER_GAINED` legend bridge payloads now expose source-card, bridge-group, resource-kind, lifecycle and generated-resource audit metadata while preserving existing resource arithmetic.
- `tests/Riftbound.ConformanceTests/LegendResourceBridgeVerifierTests.cs`
  - Extends the exact 9-card Diana / Ornn / KaiSa / Darius verifier with generated-resource consumption and end-turn cleanup assertions.

No frontend, browser script, formal 18-step script, card matrix JSON, fullOfficial / READY or `riftbound-dotnet.sln` changes were made.

## 2. Focused Verifier

Command:

```sh
source scripts/dev-env.sh && dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter FullyQualifiedName~LegendResourceBridgeVerifierTests
```

Result:

```txt
passed 36/36
```

Coverage:

- 9 success rows across Diana / Ornn / KaiSa / Darius exact source cards.
- 9 no-mutation rejection rows for wrong timing / wrong pending item / missing previous-card gates.
- 4 generated mana consumption rows for Diana / Darius later legal `PAY_COST`.
- 5 generated power consumption rows for Ornn / KaiSa later legal `PAY_COST`.
- 9 end-turn cleanup rows proving `RUNE_POOL_CLEARED`.
- Gain event metadata now asserts `cardNo`, `sourceCardNos`, `bridgeGroup`, `resourceKind`, `resourceLifecycle` and `generatedResource`.

## 3. Adjacent Regression

Command:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~LegendResourceBridgeVerifierTests|FullyQualifiedName~PaymentEngineCoverageAuditTests|FullyQualifiedName~PaymentEngineUnificationTests|FullyQualifiedName~ResourceSkill|FullyQualifiedName~LegendAct|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub"
```

Result:

```txt
passed 596/596
```

## 4. Backend Full

Command:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

Result:

```txt
passed 4606/4606
```

## 5. Diff Hygiene

Command:

```sh
git diff --check
```

Result:

```txt
passed after final doc sync
```

## 6. Remaining Risks

- The accepted lifecycle is the current authoritative `RunePool` model: generated bridge mana / power persist in the pool until spent or cleared at turn end.
- This batch does not add a separate payment-only temporary resource ledger.
- It does not close stricter future official semantics if equipment-only / spell-only generated resources need narrower consumption windows than the current runtime provides.
- Card matrix JSON remains unchanged, affected rows remain non-READY, and `fullOfficial` remains false.

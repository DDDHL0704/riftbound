# Stage 4D-03CM PaymentEngine Legend Resource Bridge Focused Verifier Evidence

Evidence date: 2026-05-16
Conclusion: **ACCEPTED FOCUSED VERIFIER / NOT READY**

## 1. Changed Files

- `src/Riftbound.Engine/CoreRuleEngine.cs`
  - `MANA_GAINED` and `POWER_GAINED` payloads now expose normalized `amount` while preserving existing `mana` / `power` fields.
- `tests/Riftbound.ConformanceTests/LegendResourceBridgeVerifierTests.cs`
  - Adds exact 9-card focused verifier coverage for Diana, Ornn, KaiSa and Darius legend resource bridge paths.

No frontend, browser script, formal 18-step script, card matrix JSON, fullOfficial / READY or `riftbound-dotnet.sln` changes were made.

## 2. Focused Verifier

Command:

```sh
source scripts/dev-env.sh && dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter FullyQualifiedName~LegendResourceBridgeVerifierTests
```

Result:

```txt
passed 18/18
```

Coverage:

- 9 success rows across Diana / Ornn / KaiSa / Darius exact source cards.
- 9 no-mutation rejection rows for wrong timing / wrong pending item / missing previous-card gates.
- Prompt metadata, command acceptance, source exhaustion and normalized resource audit amount are asserted.

## 3. Adjacent Regression

Command:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~LegendResourceBridgeVerifierTests|FullyQualifiedName~PaymentEngineCoverageAuditTests|FullyQualifiedName~PaymentEngineUnificationTests|FullyQualifiedName~ResourceSkill|FullyQualifiedName~LegendAct|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub"
```

Result:

```txt
passed 578/578
```

## 4. Backend Full

Command:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

Result:

```txt
passed 4588/4588
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

- The current legend bridge verifier proves prompt / command / rollback / source-card parity and normalized gain audit amount.
- It does not yet prove payment-only temporary ledger lifetime, generated-resource consumption, duplicate-spend prevention or cleanup.
- Card matrix JSON remains unchanged, all affected rows remain non-READY, and `fullOfficial` remains false.

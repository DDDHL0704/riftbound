# Stage 4D-03CN PaymentEngine Legend Resource Bridge Rune-Pool Lifecycle Audit

Audit date: 2026-05-16
Conclusion: **FOCUSED LIFECYCLE VERIFIER ACCEPTED / PROJECT NOT READY**

## 1. Scope

4D-03CN follows 4D-03CM and opens a narrow B-side verifier for the already accepted Diana / Ornn / KaiSa / Darius legend resource bridge family. This batch proves the current authoritative runtime lifecycle for generated legend bridge resources: they are written to `RunePool`, can be consumed by a later legal `PAY_COST`, and are cleared by `END_TURN`.

This batch does not implement a separate payment-only temporary resource ledger, frontend behavior, browser scripts, formal 18-step scripts, card matrix JSON, `fullOfficial`, READY status or `riftbound-dotnet.sln`.

## 2. Runtime Change

`CoreRuleEngine.GainLegendMana` and `CoreRuleEngine.GainLegendPower` now expose richer audit metadata on the existing `MANA_GAINED` / `POWER_GAINED` events:

- `cardNo`
- `sourceCardNos`
- `bridgeGroup`
- `resourceKind`
- `resourceLifecycle = rune-pool-cleared-at-turn-end`
- `generatedResource = true`

Existing `amount`, `mana`, `manaAfter`, `power` and `powerAfter` payload fields remain unchanged. The runtime resource model also remains unchanged: Diana / Darius generated mana enters `RunePool.Mana`; Ornn / KaiSa generated power enters `RunePool.Power`.

## 3. Verifier Coverage

`tests/Riftbound.ConformanceTests/LegendResourceBridgeVerifierTests.cs` now covers:

| Coverage slice | Rows | Assertion |
|---|---:|---|
| Success bridge rows | 9 | Prompt, command, source-card parity, source exhaustion and generated-resource audit metadata. |
| Wrong-gate rejection rows | 9 | No-mutation rollback for Diana / Ornn / KaiSa / Darius gate failures. |
| Generated mana consumption rows | 4 | Diana and Darius generated mana can pay a later legal `PAY_COST`. |
| Generated power consumption rows | 5 | Ornn and KaiSa generated power can pay a later legal `PAY_COST`. |
| End-turn cleanup rows | 9 | All generated bridge resources use `RUNE_POOL_CLEARED` cleanup at turn end. |

Focused total: 36 rows.

## 4. Validation

Commands run:

```sh
source scripts/dev-env.sh && dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter FullyQualifiedName~LegendResourceBridgeVerifierTests
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~LegendResourceBridgeVerifierTests|FullyQualifiedName~PaymentEngineCoverageAuditTests|FullyQualifiedName~PaymentEngineUnificationTests|FullyQualifiedName~ResourceSkill|FullyQualifiedName~LegendAct|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub"
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
git diff --check
```

Results:

- Focused legend bridge lifecycle verifier: passed 36/36.
- Adjacent PaymentEngine / resource skill / legend / prompt / hub regression: passed 596/596.
- Backend full: passed 4606/4606.
- `git diff --check`: passed after final doc sync.

## 5. Non-Closure

4D-03CN proves the current `RunePool` lifecycle for the legend bridge generated resources. It does not close P0-005, P1, full-card matrix, frontend final validation or READY.

If later official review requires stricter equipment-only, spell-only or payment-only temporary ledger semantics beyond the current `RunePool` model, that remains future P0-005 work. This batch must not be used to upgrade the full `RESOURCE_SKILLS` family, any card matrix row, `fullOfficial` or project READY status by proxy.

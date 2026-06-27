# Plan B Assemble Equipment Profile Catalog Audit

日期：2026-06-27
结论：**FOCUSED SLICE ACCEPTED / PROJECT NOT READY**

本文件记录 Plan B 小切片：把现有代表性 `ASSEMBLE_EQUIPMENT` profile 从 `CoreRuleEngine` 与 `ActionPromptBuilder` 两份本地表迁移到共享 `AssembleEquipmentProfileCatalog`。该切片只消除 profile 来源漂移，不改变装配支付、目标合法性、附加费用、装备入场/贴附、事件 payload、prompt 或 snapshot 语义。

## 1. Scope

Changed:

- `src/Riftbound.Engine/AssembleEquipmentProfileCatalog.cs`
- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `src/Riftbound.Engine/MatchSession.cs`
- `src/Riftbound.Engine/CardEquipmentKeywordRules.cs`
- `tests/Riftbound.ConformanceTests/CardCatalogBaselineTests.cs`
- `docs/CURRENT_PLAN_B_ASSEMBLE_EQUIPMENT_PROFILE_CATALOG_AUDIT.md`
- `docs/CURRENT_PLAN_B_ASSEMBLE_EQUIPMENT_PROFILE_CATALOG_EVIDENCE.md`
- `docs/rules-evidence-index.md`
- `docs/CURRENT_SERVER_RULE_AUDIT.md`

Not changed:

- official card catalog JSON
- assemble command payment arithmetic, typed power spending, graveyard recycle requirements, destroy-friendly-unit costs, experience costs, or mana costs
- source/target legality and owner/controller checks
- equipment attach/detach lifecycle
- frontend runtime

## 2. Acceptance Review

| Requirement | Evidence | Verdict |
|---|---|---|
| Core and prompt no longer maintain duplicated assemble profile records | `private sealed record AssembleEquipmentProfile` was removed from `CoreRuleEngine.cs` and `MatchSession.cs`; both now consume the shared record from `AssembleEquipmentProfileCatalog.cs` | Accepted |
| Core and prompt no longer maintain duplicated `ImplementedAssembleEquipmentProfiles` dictionaries | The local dictionaries were removed and both consumers call `AssembleEquipmentProfileCatalog.TryGet(...)` | Accepted |
| Equipment keyword profile source uses the same representative profile catalog | `CardEquipmentKeywordRules.BuildProfile(...)` now calls `AssembleEquipmentProfileCatalog.HasImplementedRepresentative(spec.CardNo)` instead of reaching into `ActionPromptBuilder` | Accepted |
| Existing representative fallback remains explicit | `AssembleEquipmentProfileCatalog.FallbackRepresentative` preserves the current Long Sword fallback used by legacy unsupported-object handling | Accepted |
| No semantic expansion is claimed | The profile rows are a consolidation of existing implemented representative values; no new official assemble card is enabled by this slice | Accepted |
| Current `Is*CardNo` method-declaration cleanup | `rg -n "bool\\s+Is[A-Za-z0-9_]+CardNo\\s*\\(" src/Riftbound.Engine src/Riftbound.CardCatalog src/Riftbound.Contracts tests/Riftbound.ConformanceTests` remains at 0 matches | Accepted for helper-removal scope, no full-official claim |

## 3. Rule Authority

- Official card text from `data/official/card-catalog.zh-CN.json`.
- Existing evidence index rows for the implemented assemble representatives remain the rule authority anchors, including `p2-preflight-play-long-sword-agile-equipment`, target-rejected equipment rows, and Shurelya / special assemble representatives.
- `docs/rules-authority-and-audit.md` remains the rule-authority protocol.
- This slice does not reinterpret card text; it keeps the previously verified representative profile values and moves their source into one shared engine catalog.

## 4. Verification

Build:

```sh
/Users/dinghaolin/.dotnet/dotnet build src/Riftbound.Engine/Riftbound.Engine.csproj -c Debug --nologo
```

Result: passed.

Focused guard:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~AssembleEquipmentRepresentativeProfilesUseSharedCatalog" --nologo
```

Result: failed before implementation on the local `private sealed record AssembleEquipmentProfile`, then 1/1 passed after implementation.

Focused equipment representatives:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~Assemble|FullyQualifiedName~EquipmentKeyword|FullyQualifiedName~EquipmentState|FullyQualifiedName~LongSword" --nologo
```

Result: 165/165 passed.

Adjacent backend:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~Assemble|FullyQualifiedName~EquipmentKeyword|FullyQualifiedName~EquipmentState|FullyQualifiedName~LongSword|FullyQualifiedName~PaymentEngine|FullyQualifiedName~MatchRecovery|FullyQualifiedName~CardCatalogBaseline" --nologo
```

Result: 3211/3211 passed.

Full backend conformance:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result: 8812/8812 passed.

## 5. Residual Risks

- `AssembleEquipmentProfileCatalog` is still an engine-side representative profile table, not a complete BehaviorSpec extraction of all assemble clauses.
- This does not close full Agile, Tempered, assemble, weapon, equipment static modifier, copy-text, attach lifecycle, owner/controller, payment-window, or full card matrix coverage.
- This does not change functional-unit coverage status, frontend validation status, P0 objective status, or READY status.
- Project remains **NOT READY**.

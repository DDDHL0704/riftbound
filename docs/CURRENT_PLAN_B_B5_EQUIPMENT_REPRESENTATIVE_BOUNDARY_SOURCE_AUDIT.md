# Plan B B5 Equipment Representative Boundary Source Audit

日期：2026-06-25
结论：**FOCUSED SLICE ACCEPTED / PROJECT NOT READY**

本文件记录 Plan B 小切片：把装备关键词代表性边界从 `CardEquipmentKeywordRules` 中独立 `Is*CardNo` helper / set 查询迁移到 `EquipmentRepresentativeBoundaries` source rows，并让 `CoreRuleEngine` 与 `MatchSession` 通过领域查询消费这些 source rows。该切片只迁移代表性边界来源，不改变装备打出、装配、百炼可选贴附、奥恩静态力量重算、事件 payload、prompt 或 snapshot 语义。

## 1. Scope

Changed:

- `src/Riftbound.Engine/CardEquipmentKeywordRules.cs`
- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `src/Riftbound.Engine/MatchSession.cs`
- `tests/Riftbound.ConformanceTests/CardCatalogBaselineTests.cs`
- `tests/Riftbound.ConformanceTests/EquipmentKeywordRepresentativeBoundaryGuardTests.cs`
- `docs/CURRENT_PLAN_B_B5_EQUIPMENT_REPRESENTATIVE_BOUNDARY_SOURCE_AUDIT.md`
- `docs/CURRENT_PLAN_B_B5_EQUIPMENT_REPRESENTATIVE_BOUNDARY_SOURCE_EVIDENCE.md`
- `docs/rules-evidence-index.md`

Not changed:

- official card catalog JSON
- equipment cost, attach/detach legality, controller/owner checks, or zone movement semantics
- Ornn friendly-equipment static power arithmetic
- Agile / Tempered official breadth beyond existing representative paths
- frontend runtime

## 2. Acceptance Review

| Requirement | Evidence | Verdict |
|---|---|---|
| Agile direct-play attach representative boundary no longer uses a duplicated card-number helper | `IsAgileDirectPlayAttachRepresentativeCardNo` was removed; Core and MatchSession now call `HasAgileDirectPlayAttachRepresentativeBoundary` backed by `EquipmentRepresentativeBoundaries` | Accepted |
| Tempered optional attach representative boundary no longer uses a duplicated card-number helper | `IsTemperedOptionalAttachRepresentativeCardNo` was removed; Core and MatchSession now call `HasTemperedOptionalAttachRepresentativeBoundary` backed by `EquipmentRepresentativeBoundaries` | Accepted |
| Friendly-equipment static power representative boundary no longer uses a duplicated card-number helper | `IsFriendlyEquipmentStaticPowerRepresentativeCardNo` was removed; profile classification now calls `HasFriendlyEquipmentStaticPowerRepresentativeBoundary` backed by `EquipmentRepresentativeBoundaries` | Accepted |
| Equipment-state representative boundary no longer uses a duplicated card-number helper | `IsEquipmentStateRepresentativeCardNo` was removed; profile and tests now call `TryGetEquipmentStateRepresentative`, gated by the source-row boundary kind | Accepted |
| Source identity consumes a shared data definition | `CardEquipmentRepresentativeBoundary` rows declare the card number and `EquipmentRepresentativeBoundaryKinds` value; all public representative checks route through `HasRepresentativeBoundary` | Accepted |
| Hidden-info / recovery boundary | `MatchRecovery` remains green | Accepted |
| Current `Is*CardNo` method-declaration cleanup | `rg -n "\\b(?:private|public|internal|protected)?\\s*static\\s+bool\\s+Is[A-Za-z0-9]+CardNo\\s*\\(" src/Riftbound.Engine src/Riftbound.CardCatalog src/Riftbound.Contracts` returns no matches | Accepted for helper-removal scope, no full-official claim |

## 3. Rule Authority

- Official card text from `data/official/card-catalog.zh-CN.json`.
- `SFD·022/221` 长剑, `SFD·056/221` 斯特拉克的挑战护手, `SFD·064/221` 布甲, and `SFD·186/221` 旋转飞斧 carry `{{灵便}}`, whose text says the card gains reaction and attaches to a controlled unit when played.
- `SFD·002/221` 武装强袭者, `SFD·008/221` 哨兵好手, `SFD·119/221` / `SFD·119a/221` 贾克斯, and `SFD·085/221` / `SFD·085a/221` 奥恩 carry `{{百炼}}`, whose text allows an optional weapon assemble on play with reduced assemble cost.
- `SFD·085/221` / `SFD·085a/221` 奥恩 also says it gains `{{S}}+1` for each friendly equipment.
- Rule authority protocol: `docs/rules-authority-and-audit.md`.

## 4. Verification

Focused:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~EquipmentKeywordRepresentativeBoundaryGuardTests|FullyQualifiedName~P4EquipmentKeywordProfilesMapOfficialTextToRegistryTags|FullyQualifiedName~P5EquipmentStateAssembleLongSwordOwnerControllerFixtureProfileBindsExistingVerifierAnchors"
```

Result: guard failed before implementation on missing `Has*RepresentativeBoundary` / `TryGetEquipmentStateRepresentative` APIs, then 3/3 passed after implementation.

Adjacent:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~EquipmentKeyword|FullyQualifiedName~AgileEquipment|FullyQualifiedName~TemperedEquipment|FullyQualifiedName~OrnnFriendlyEquipment|FullyQualifiedName~EquipmentState|FullyQualifiedName~Assemble|FullyQualifiedName~FullGameEndToEnd"
```

Result: 223/223 passed.

Hidden-info / recovery boundary:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecovery"
```

Result: 1989/1989 passed.

Full backend conformance:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj
```

Result: 8585/8585 passed.

## 5. Residual Risks

- This does not close full Agile reaction timing, full Tempered official breadth, all weapon/static modifiers, copy-text effects, or full attach lifecycle coverage.
- `EquipmentRepresentativeBoundaries` still records implemented representative source rows inside the engine helper file; a later catalog extraction can move these rows farther out without changing the consumer API.
- This does not change current official-card parser breadth or functional-unit matrix status.
- Project remains **NOT READY**.

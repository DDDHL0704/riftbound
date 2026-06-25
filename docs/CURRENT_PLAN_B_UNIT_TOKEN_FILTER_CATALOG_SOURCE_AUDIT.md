# Plan B Unit Token Filter Catalog Source Audit

日期：2026-06-25
结论：**FOCUSED SLICE ACCEPTED / PROJECT NOT READY**

本文件记录 Plan B 小切片：把 static aura `UNIT_TOKEN` 目标过滤中的“单位指示物”身份判断从 `StaticAuraSpecRules` 本地 `IsUnitTokenCardNo` helper 改为 `P6TokenFactoryCatalog` 的 token factory 领域分类查询。该切片只收窄 static aura target-filter 的 token category 来源，不关闭完整 token taxonomy、完整 Soul Shepherd / token-aura official breadth、完整 P6 token factory domain 或 READY。

## 1. Scope

Changed:

- `src/Riftbound.Engine/P6TokenFactoryCatalog.cs`
- `src/Riftbound.Engine/StaticAuraSpecRules.cs`
- `tests/Riftbound.ConformanceTests/CardCatalogBaselineTests.cs`
- `docs/CURRENT_PLAN_B_UNIT_TOKEN_FILTER_CATALOG_SOURCE_AUDIT.md`
- `docs/CURRENT_PLAN_B_UNIT_TOKEN_FILTER_CATALOG_SOURCE_EVIDENCE.md`
- `docs/rules-evidence-index.md`

Not changed:

- official card catalog JSON
- token factory definitions
- `StaticAuraSpec` shape
- continuous-effect projection shape
- frontend runtime

## 2. Acceptance Review

| Requirement | Evidence | Verdict |
|---|---|---|
| `StaticAuraSpecRules` no longer owns a local `IsUnitTokenCardNo` helper | helper was deleted and guard test blocks reintroduction | Accepted |
| `UNIT_TOKEN` filter consumes token factory domain data | `StaticAuraSpecRules.TargetMatchesFilter` now calls `P6TokenFactoryCatalog.IsUnitTokenFactory(target.CardNo)` | Accepted |
| Classification covers all official unit token factory rows | tests cover `UNL·T02`, `UNL·T06`, `UNL·T07`, `SFD·T01`, `SFD·T02`, `OGN·271/298`, `OGN·272/298`, `OGN·273/298`, `OGN·274/298` | Accepted |
| Classification rejects non-unit token factories and normal cards | tests reject null/empty, battlefield tokens, equipment tokens, and `SFD·082/221` | Accepted |
| Static aura behavior remains stable | focused and adjacent StaticAura / TokenFactory / ContinuousEffect representatives remain green | Accepted |
| Full token taxonomy | complete token subtype/family taxonomy and all token-aura official breadth remain residual | Residual, no full-official claim |

## 3. Verification

Focused:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~P6TokenFactoryClassifiesUnitTokenFactoriesByCategory|FullyQualifiedName~P6TokenFactoryRejectsNonUnitTokenFactoriesByCategory|FullyQualifiedName~StaticAuraUnitTokenFilterDoesNotUseLocalCardNumberHelper|FullyQualifiedName~StaticAuraCatalogParsesCurrentPowerAuras"
```

Result: 17/17 passed.

Adjacent:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~StaticAura|FullyQualifiedName~TokenFactory|FullyQualifiedName~UnitToken|FullyQualifiedName~ContinuousEffect|FullyQualifiedName~P6TokenFactory|FullyQualifiedName~SoulShepherd|FullyQualifiedName~CardCatalogBaselineTests"
```

Result: 632/632 passed.

Hidden-info / recovery boundary:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecovery"
```

Result: 1989/1989 passed.

Full backend conformance:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj
```

Result: 8571/8571 passed.

## 4. Residual Risks

- This does not broaden complete token subtype / family taxonomy beyond the current P6 token factory domain.
- This does not close all static aura target-filter families.
- `P6TokenFactoryCatalog` remains the source for currently implemented official token factory rows; missing token rows still require catalog data, not static-aura rule branching.
- Project remains **NOT READY**.

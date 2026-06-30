# Plan B B5 Equipment Representative Boundary Source Audit

日期：2026-06-25
结论：**FOCUSED SLICE ACCEPTED / PROJECT NOT READY**

本文件记录 Plan B 小切片：把装备关键词代表性边界从 `CardEquipmentKeywordRules` 中独立 `Is*CardNo` helper / set 查询迁移到 `EquipmentRepresentativeBoundaries` source rows，并让 `CoreRuleEngine` 与 `MatchSession` 通过领域查询消费这些 source rows。该切片只迁移代表性边界来源，不改变装备打出、装配、百炼可选贴附、奥恩静态力量重算、事件 payload、prompt 或 snapshot 语义。

## 2026-06-30 Supplement: BehaviorSpec-Derived Agile and Friendly-Equipment Boundaries

本补充把 `AgileDirectPlayAttach` 与 `FriendlyEquipmentStaticPower` representative boundary 从显式卡号 source rows 继续上移到官方目录驱动的 `BehaviorSpec` 派生。`CardEquipmentKeywordRules.EquipmentRepresentativeBoundaries` 现在由三类来源合并：`BehaviorSpecCatalogBuilder.Build(...)` 派生的灵便直接贴附 / 友方装备数量静态力量边界、仍显式保留的百炼 optional attach 代表边界、以及装备状态 verifier metadata 派生的 `EquipmentState` 边界。

派生条件保持窄口径：灵便直接贴附要求官方目录中的装备 / 专属装备 `BehaviorSpec`、自身官方 `{{灵便}}` 行、已实现 `CardBehaviorRegistry.PlaysSourceToBaseAsEquipment` 路径、registry `SourceEquipmentTags` 含 `灵便`，并且该卡已有 `AssembleEquipmentProfileCatalog` 代表装配 profile；友方装备静态力量要求已实现 unit play 行为并且 `BehaviorSpec.StaticAuras` 含 `FRIENDLY_FIELD_EQUIPMENT_COUNT_TO_SOURCE_UNIT_POWER`。因此新增同形状官方卡只需进入 catalog / behavior spec 即可被 representative boundary 识别，但本批不扩大尚未实现的灵便 reaction timing、百炼全量范围或装备生命周期。

验证：focused guard red/green 1/1；EquipmentKeyword / AgileEquipment / OrnnFriendlyEquipment / AssembleEquipment / EquipmentState / CardCatalogBaseline adjacent 477/477；同组加 MatchRecovery / PaymentEngine hidden-info/payment adjacent 3254/3254；backend full conformance 9049/9049。

## 2026-06-28 Supplement: Tempered Attach Equipment Source Boundary

本补充把百炼 optional attach 中“可被选择的装备来源”也迁移到同一 representative-boundary source rows：`EquipmentRepresentativeBoundaryKinds.TemperedOptionalAttachEquipment` 现在记录 `SFD·186/221`，`CardEquipmentKeywordRules.CanBeTemperedOptionalAttachEquipment(cardNo)` 是 prompt 与 Core 结算重验共享的唯一查询。`MatchSession.IsPromptTemperedOptionalAttachChoice` 与 `CoreRuleEngine.IsLegalTemperedOptionalAttachChoice` 不再持有 `SpinningAxeCardNo` runtime 常量或直接比较 `SFD·186/221`。语义不扩大：当前仍只关闭既有《旋转飞斧》代表路径，不关闭 full Tempered official breadth。

验证：focused guard / Tempered / Jax / Armed Assaulter representative 62/62；EquipmentKeyword / TemperedEquipment / JaxTempered / ArmedAssaulterHasteTempered / AgileEquipment / AssembleEquipment / Akshan / MatchRecovery / CardCatalogBaseline adjacent 2506/2506；backend full 8867/8867。

## 2026-06-28 Follow-up: Sentinel Adept Runtime Constant Removed

`CoreRuleEngine` and `MatchSession` no longer retain the migrated `SentinelAdeptCardNo` constant. The Sentinel Adept Tempered source boundary remains expressed only by `CardEquipmentKeywordRules.EquipmentRepresentativeBoundaries` row `SFD·008/221` with `EquipmentRepresentativeBoundaryKinds.TemperedOptionalAttach`, consumed through `HasTemperedOptionalAttachRepresentativeBoundary(...)` in Core and ActionPromptBuilder.

Validation passed for this follow-up: focused equipment boundary guard 3/3; EquipmentKeyword / TemperedEquipment / JaxTempered / ArmedAssaulterHasteTempered / AgileEquipment / AssembleEquipment / Akshan / MatchRecovery / CardCatalogBaseline adjacent 2509/2509. This follow-up removes dead runtime source data only; it does not expand legal Tempered cards, full attach lifecycle breadth, copy-text effects, LayerEngine, frontend final validation, full official, or READY.

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
- `EquipmentRepresentativeBoundaries` still retains explicit source rows for Tempered optional attach representative coverage; a later catalog extraction can move those rows farther out without changing the consumer API.
- This does not change current official-card parser breadth or functional-unit matrix status.
- Project remains **NOT READY**.

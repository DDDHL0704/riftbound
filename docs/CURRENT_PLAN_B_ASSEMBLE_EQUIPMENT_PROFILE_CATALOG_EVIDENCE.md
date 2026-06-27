# Plan B Assemble Equipment Profile Catalog Evidence

日期：2026-06-27
结论：**EVIDENCE RECORDED / PROJECT NOT READY**

This file records concrete evidence for consolidating representative `ASSEMBLE_EQUIPMENT` profile metadata into one shared engine catalog.

## 1. Runtime Evidence

- `AssembleEquipmentProfileCatalog` now defines the shared `AssembleEquipmentProfile` record and representative profile rows.
- `CoreRuleEngine` now calls `AssembleEquipmentProfileCatalog.TryGet(...)` when validating and resolving representative assemble commands.
- `ActionPromptBuilder` now calls `AssembleEquipmentProfileCatalog.TryGet(...)` when exposing `ASSEMBLE_EQUIPMENT.sourceRequirements`.
- `CardEquipmentKeywordRules.BuildProfile(...)` now calls `AssembleEquipmentProfileCatalog.HasImplementedRepresentative(...)` when setting the implemented assemble representative boundary.
- `AssembleEquipmentProfileCatalog.FallbackRepresentative` preserves the current Long Sword fallback for unsupported-object handling.
- The previous local `AssembleEquipmentProfile` records and `ImplementedAssembleEquipmentProfiles` dictionaries were deleted from `CoreRuleEngine.cs` and `MatchSession.cs`.
- `ActionPromptBuilder.HasImplementedRepresentativeAssembleEquipmentProfile(...)` was deleted, so equipment keyword profile construction no longer depends on prompt internals.
- Existing payment, target, attach/detach, event, prompt, and snapshot shapes are unchanged.

## 2. Test Evidence

Focused test file:

- `tests/Riftbound.ConformanceTests/CardCatalogBaselineTests.cs`

Coverage:

- `AssembleEquipmentRepresentativeProfilesUseSharedCatalog` blocks reintroducing local `AssembleEquipmentProfile` records in `CoreRuleEngine.cs` or `MatchSession.cs`.
- The same guard blocks reintroducing local `ImplementedAssembleEquipmentProfiles` dictionaries in those files.
- The same guard requires both Core and prompt source to call `AssembleEquipmentProfileCatalog.TryGet`.
- The same guard blocks `CardEquipmentKeywordRules` from depending on `ActionPromptBuilder.HasImplementedRepresentativeAssembleEquipmentProfile` and requires it to use `AssembleEquipmentProfileCatalog.HasImplementedRepresentative`.
- Existing assemble/equipment fixtures continue to cover representative payment, typed power, target, and equipment state behavior.

## 3. Rule Evidence

- `data/official/card-catalog.zh-CN.json` remains the card-text source for the representative equipment cards.
- Existing evidence rows such as `p2-preflight-play-long-sword-agile-equipment`, `p4-play-long-sword-target-rejected`, `p4-play-shurelyas-requiem-target-rejected`, and the other equipment target-rejected rows continue to anchor official card text and command legality.
- The shared catalog keeps the already implemented representative profile values for Long Sword, Soul Sword, Jagged Dirk, Recurve Bow, Arion's Fall, Withered Battleaxe, Brutalizer, Guardian Angel, Cloth Armor, Hextech Infused Bulwark, Wanderer's Guidebook, Z Drive, Sterak's Gage, Svarshang Song, Doran's Shield, Doran's Ring, Doran's Blade, Hexdrinker, Warmog's Armor, Trinity Force, Hunter's Machete, Bone Club, Boots of Swiftness, Cull, Edge of Night, Last Rites, Vanguard's Eye, BF Sword, Sacred Shears, Blade of the Ruined King, Spinning Axe, Hearthfire Cloak, Rabadon's Deathcap, Shurelya's Requiem, Hextech Gauntlet, and Shepherd's Heirloom.
- This slice does not reinterpret official text or add new assemble coverage; it removes duplicated runtime profile sources.

## 4. Verification

```sh
/Users/dinghaolin/.dotnet/dotnet build src/Riftbound.Engine/Riftbound.Engine.csproj -c Debug --nologo
```

Result: passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~AssembleEquipmentRepresentativeProfilesUseSharedCatalog" --nologo
```

Result: failed before implementation on the local `private sealed record AssembleEquipmentProfile`, then 1/1 passed after implementation.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~Assemble|FullyQualifiedName~EquipmentKeyword|FullyQualifiedName~EquipmentState|FullyQualifiedName~LongSword" --nologo
```

Result: 165/165 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~Assemble|FullyQualifiedName~EquipmentKeyword|FullyQualifiedName~EquipmentState|FullyQualifiedName~LongSword|FullyQualifiedName~PaymentEngine|FullyQualifiedName~MatchRecovery|FullyQualifiedName~CardCatalogBaseline" --nologo
```

Result: 3211/3211 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result: 8812/8812 passed.

## 5. Helper Count

After this slice, this source-control check remains clean:

```sh
rg -n "bool\s+Is[A-Za-z0-9_]+CardNo\s*\(" src/Riftbound.Engine src/Riftbound.CardCatalog src/Riftbound.Contracts tests/Riftbound.ConformanceTests
```

Result: 0 matches.

## 6. Non-Closure Statement

This evidence does not close full assemble BehaviorSpec extraction, full Agile, Tempered, weapon, equipment static modifier, copy-text, attach lifecycle, owner/controller, full payment-window matrix, full card matrix, frontend final validation, P0 objective, or READY status.

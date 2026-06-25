# Plan B LeBlanc Ephemeral Static Source Identity Catalog Source Evidence

日期：2026-06-25
结论：**EVIDENCE RECORDED / PROJECT NOT READY**

This file records the concrete evidence for removing the duplicated LeBlanc ephemeral-static card-number allow-list from `CoreRuleEngine`.

## 1. Runtime Evidence

- `CoreRuleEngine.IsEphemeralTurnStartSuppressedByLeblancStatic` still requires the source to be a visible, non-standby unit on the same battlefield and controlled by the turn player.
- Source identity now flows through `CardBehaviorRegistry.IsImplementedUnitWithEffectKind` using:
  - `LEBLANC_PLAY_KEYWORD_UNIT`
  - `LEBLANC_ALT_A_BACK_ROW_STATIC_PLAY_UNIT`
- The previous `LeblancEphemeralStaticUnitCardNo` constant was deleted.
- The previous `IsLeblancEphemeralStaticUnitCardNo` helper was deleted.
- Existing cleanup behavior and event/snapshot shape are unchanged.

## 2. Test Evidence

Focused test files:

- `tests/Riftbound.ConformanceTests/CardCatalogBaselineTests.cs`
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`

Coverage:

- `CardBehaviorRegistryIdentifiesLeblancEphemeralStaticSuppressionSourcesByEffectKind` covers the two implemented LeBlanc source rows.
- `CardBehaviorRegistryRejectsNonMatchingLeblancEphemeralStaticSuppressionSources` rejects an unrelated LeBlanc row, cross-effect matches, and unrelated Ezreal.
- `LeblancEphemeralStaticSuppressionDoesNotUseDuplicatedCardNumberAllowList` blocks reintroducing the deleted Core cardNo helper / constant and direct `UNL-090a/219` branch.
- Existing `CoreRuleEngineSuppressesEphemeralTurnStartAtLeblancBattlefield` verifies runtime suppression remains intact.

## 3. Verification

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~CardBehaviorRegistryIdentifiesLeblancEphemeralStaticSuppressionSourcesByEffectKind|FullyQualifiedName~CardBehaviorRegistryRejectsNonMatchingLeblancEphemeralStaticSuppressionSources|FullyQualifiedName~LeblancEphemeralStaticSuppressionDoesNotUseDuplicatedCardNumberAllowList|FullyQualifiedName~CoreRuleEngineSuppressesEphemeralTurnStartAtLeblancBattlefield"
```

Result: 8/8 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~Leblanc|FullyQualifiedName~Ephemeral|FullyQualifiedName~Lifecycle|FullyQualifiedName~CardCatalogBaselineTests|FullyQualifiedName~GameHub|FullyQualifiedName~FullGameEndToEnd"
```

Result: 595/595 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecovery"
```

Result: 1989/1989 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj
```

Result: 8578/8578 passed.

## 4. Helper Count

After this slice, `rg -n "private static bool Is.*CardNo\\(" src/Riftbound.Engine src/Riftbound.CardCatalog src/Riftbound.Contracts` reports 26 total helpers, with 24 in `CoreRuleEngine.cs`.

## 5. Non-Closure Statement

This evidence does not close complete lifecycle cleanup/replacement breadth, complete LeBlanc official behavior, card matrix full-official, frontend final validation or READY.

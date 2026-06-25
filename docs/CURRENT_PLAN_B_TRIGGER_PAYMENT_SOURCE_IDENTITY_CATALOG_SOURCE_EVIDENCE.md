# Plan B Trigger Payment Source Identity Catalog Source Evidence

日期：2026-06-25
结论：**EVIDENCE RECORDED / PROJECT NOT READY**

This file records the concrete evidence for removing duplicated Fiora / Jax trigger-payment source card-number allow-lists from `CoreRuleEngine`.

## 1. Runtime Evidence

- `CardBehaviorRegistry.IsImplementedUnitWithEffectKind(cardNo, effectKind)` was added as a generic implemented-unit source identity helper.
- `CoreRuleEngine.TryGetJaxWeaponAttachSource` now validates source identity through `CardBehaviorRegistry.IsImplementedUnitWithEffectKind` using `SFD_119_JAX_NO_OPTIONAL_ASSEMBLE_PLAY_UNIT` and `SFD_119_JAX_ALT_A_NO_OPTIONAL_ASSEMBLE_PLAY_UNIT`.
- `CoreRuleEngine.TryGetSfdFioraPowerfulReadySource` now validates source identity through `CardBehaviorRegistry.IsImplementedUnitWithEffectKind` using `SFD_180_FIORA_POWERFUL_READY_PLAY_UNIT` and `SFD_180A_FIORA_POWERFUL_READY_PLAY_UNIT`.
- The previous `SfdJaxWeaponAttachCardNo`, `SfdJaxWeaponAttachAltCardNo`, and `IsJaxWeaponAttachCardNo` Core allow-list were deleted.
- The previous `SfdFioraPowerfulReadyCardNo`, `SfdFioraPowerfulReadyAltCardNo`, and `IsSfdFioraPowerfulReadyCardNo` Core allow-list were deleted.
- Existing runtime guards remain: source must be a visible unit, not standby, controlled by the acting player or legacy-owned path, and on the field.

## 2. Test Evidence

Focused test file:

- `tests/Riftbound.ConformanceTests/TriggerPaymentTests.cs`

Coverage:

- `CardBehaviorRegistryIdentifiesTriggerPaymentSourceUnitsByEffectKind` covers the four implemented source rows used by the Jax and Fiora trigger-payment representatives.
- `CardBehaviorRegistryRejectsNonMatchingTriggerPaymentSourceUnits` rejects unrelated Jax (`SFD·054/221`), wrong Jax effect kind, Fiora alt/source cross-match, and unrelated Ezreal.
- `TriggerPaymentSourceIdentityDoesNotUseDuplicatedCardNumberAllowLists` blocks reintroducing the deleted Core cardNo source allow-lists and verifies `CoreRuleEngine` consumes `CardBehaviorRegistry.IsImplementedUnitWithEffectKind`.
- Existing Jax / Fiora trigger-payment representative tests verify runtime behavior remains intact.

## 3. Verification

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~CardBehaviorRegistryIdentifiesTriggerPaymentSourceUnitsByEffectKind|FullyQualifiedName~CardBehaviorRegistryRejectsNonMatchingTriggerPaymentSourceUnits|FullyQualifiedName~TriggerPaymentSourceIdentityDoesNotUseDuplicatedCardNumberAllowLists|FullyQualifiedName~JaxWeaponAttachOpensTriggerPaymentPrompt|FullyQualifiedName~SfdFioraBoonPowerTransitionOpensYellowTriggerPayment"
```

Result: 13/13 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~TriggerPayment|FullyQualifiedName~Jax|FullyQualifiedName~Fiora|FullyQualifiedName~PaymentEngineCoverageAuditTests|FullyQualifiedName~ActionPrompt"
```

Result: 867/867 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecovery"
```

Result: 1989/1989 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj
```

Result: 8554/8554 passed.

## 4. Helper Count

After this slice, `rg -n "private static bool Is.*CardNo\\(" src/Riftbound.Engine src/Riftbound.CardCatalog src/Riftbound.Contracts` reports 28 total helpers, with 25 in `CoreRuleEngine.cs`.

## 5. Non-Closure Statement

This evidence does not close complete trigger-payment official breadth, Jax full official behavior, Fiora full official behavior, complete equipment lifecycle, card matrix full-official, frontend final validation or READY.

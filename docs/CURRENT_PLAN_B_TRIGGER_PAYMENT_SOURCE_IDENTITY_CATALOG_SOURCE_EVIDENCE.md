# Plan B Trigger Payment Source Identity Catalog Source Evidence

日期：2026-06-25
结论：**EVIDENCE RECORDED / PROJECT NOT READY**

This file records the concrete evidence for removing duplicated Fiora / Jax trigger-payment source card-number allow-lists from `CoreRuleEngine`.

2026-06-26 supplement: this evidence also covered OGN Vayne conquer-recall and Icevale Archer attack-payment representative source identity at the catalog source-effect layer. 2026-06-27 update: Vayne conquer-recall moved again to the B3 unit-conquest `UNIT_CONQUEST_PAY_1_RETURN_SELF_TO_HAND` BehaviorSpec route; Jax weapon-attach and Fiora powerful-ready moved again to the unit trigger-payment `BehaviorSpec.Triggers` route recorded in `docs/CURRENT_PLAN_B_UNIT_TRIGGER_PAYMENT_SPEC_AUDIT.md` and `docs/CURRENT_PLAN_B_UNIT_TRIGGER_PAYMENT_SPEC_EVIDENCE.md`. 2026-06-30 update: Icevale Archer attack-payment moved again to the `UNIT_ATTACK_PAY_POWER_MODIFIER` TriggerSpec route recorded in `docs/CURRENT_PLAN_B_ICEVALE_ATTACK_PAYMENT_TRIGGER_SPEC_AUDIT.md` and `docs/CURRENT_PLAN_B_ICEVALE_ATTACK_PAYMENT_TRIGGER_SPEC_EVIDENCE.md`.

## 1. Runtime Evidence

- `CardBehaviorRegistry.IsImplementedUnitWithEffectKind(cardNo, effectKind)` was added as a generic implemented-unit source identity helper.
- `CoreRuleEngine.TryGetJaxWeaponAttachSource` now validates source identity through `CardBehaviorRegistry.IsImplementedUnitWithEffectKind` using `SFD_119_JAX_NO_OPTIONAL_ASSEMBLE_PLAY_UNIT` and `SFD_119_JAX_ALT_A_NO_OPTIONAL_ASSEMBLE_PLAY_UNIT`.
- `CoreRuleEngine.TryGetSfdFioraPowerfulReadySource` now validates source identity through `CardBehaviorRegistry.IsImplementedUnitWithEffectKind` using `SFD_180_FIORA_POWERFUL_READY_PLAY_UNIT` and `SFD_180A_FIORA_POWERFUL_READY_PLAY_UNIT`.
- The previous `SfdJaxWeaponAttachCardNo`, `SfdJaxWeaponAttachAltCardNo`, and `IsJaxWeaponAttachCardNo` Core allow-list were deleted.
- The previous `SfdFioraPowerfulReadyCardNo`, `SfdFioraPowerfulReadyAltCardNo`, and `IsSfdFioraPowerfulReadyCardNo` Core allow-list were deleted.
- Existing runtime guards remain: source must be a visible unit, not standby, controlled by the acting player or legacy-owned path, and on the field.
- 2026-06-26: `CoreRuleEngine.TryGetOgnVayneConquerRecallSource` validated source identity through `CardBehaviorRegistry.IsImplementedUnitWithEffectKind` using `OGN_VAYNE_ASSAULT3_CONQUER_RECALL_PLAY_UNIT`; this was superseded on 2026-06-27 by B3 `UNIT_CONQUEST_PAY_1_RETURN_SELF_TO_HAND` BehaviorSpec lookup through `UnitConquestTriggerSpecRules.TryGetUnitConquestPayReturnSelfToHandTrigger(...)`.
- 2026-06-26: `CoreRuleEngine.TryGetIcevaleArcherAttackSource` validated source identity through `CardBehaviorRegistry.IsImplementedUnitWithEffectKind` using `ICEVALE_ARCHER_ATTACK_PAYMENT_PLAY_UNIT`.
- 2026-06-30: `CoreRuleEngine.TryGetIcevaleArcherAttackSource` now validates source identity through `UnitTriggerPaymentSpecRules.TryGetUnitAttackPayPowerModifierTrigger(...)` and no longer consumes `ICEVALE_ARCHER_ATTACK_PAYMENT_PLAY_UNIT`.
- 2026-06-26: the previous direct `sourceState.CardNo` comparisons against `OgnVayneCardNo` and `IcevaleArcherCardNo` were deleted from `CoreRuleEngine`.

## 2. Test Evidence

Focused test file:

- `tests/Riftbound.ConformanceTests/TriggerPaymentTests.cs`

Coverage:

- `CardBehaviorRegistryIdentifiesTriggerPaymentSourceUnitsByEffectKind` covers the four implemented source rows used by the Jax and Fiora trigger-payment representatives.
- `CardBehaviorRegistryRejectsNonMatchingTriggerPaymentSourceUnits` rejects unrelated Jax (`SFD·054/221`), wrong Jax effect kind, Fiora alt/source cross-match, and unrelated Ezreal.
- `TriggerPaymentSourceIdentityDoesNotUseDuplicatedCardNumberAllowLists` blocks reintroducing the deleted Core cardNo source allow-lists and verifies `CoreRuleEngine` consumes `CardBehaviorRegistry.IsImplementedUnitWithEffectKind`.
- Existing Jax / Fiora trigger-payment representative tests verify runtime behavior remains intact.
- 2026-06-26: `CardBehaviorRegistryIdentifiesTriggerPaymentSourceUnitsByEffectKind` accepted `OGN·035/298` / `OGN_VAYNE_ASSAULT3_CONQUER_RECALL_PLAY_UNIT` and `UNL-065/219` / `ICEVALE_ARCHER_ATTACK_PAYMENT_PLAY_UNIT`; the Vayne row is superseded by the 2026-06-27 B3 BehaviorSpec source test, and the Icevale runtime source selector is superseded by the 2026-06-30 attack-payment TriggerSpec source test.
- 2026-06-26: `CardBehaviorRegistryRejectsNonMatchingTriggerPaymentSourceUnits` rejects Vayne/Icevale cross-effect source identity matches.
- 2026-06-27: `TriggerPaymentSourceIdentityDoesNotUseDuplicatedCardNumberAllowLists` also blocks reintroducing `OgnVayneConquerRecallSourceEffectKind` / `OGN_VAYNE_ASSAULT3_CONQUER_RECALL_PLAY_UNIT` into `CoreRuleEngine` and verifies Vayne now uses `UnitConquestTriggerSpecRules.TryGetUnitConquestPayReturnSelfToHandTrigger`; the same guard continues to block direct `sourceState.CardNo` comparisons for `OgnVayneCardNo` and `IcevaleArcherCardNo`.

## 3. Verification

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~CardBehaviorRegistryIdentifiesTriggerPaymentSourceUnitsByEffectKind|FullyQualifiedName~CardBehaviorRegistryRejectsNonMatchingTriggerPaymentSourceUnits|FullyQualifiedName~TriggerPaymentSourceIdentityDoesNotUseDuplicatedCardNumberAllowLists" --nologo
```

Result: 13/13 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~TriggerPayment|FullyQualifiedName~Vayne|FullyQualifiedName~Icevale|FullyQualifiedName~PaymentEngineCoverageAuditTests|FullyQualifiedName~ActionPrompt" --nologo
```

Result: 845/845 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecovery" --nologo
```

Result: 1989/1989 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result: 8664/8664 passed.

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

After the original 2026-06-25 slice, `rg -n "private static bool Is.*CardNo\\(" src/Riftbound.Engine src/Riftbound.CardCatalog src/Riftbound.Contracts` reported 28 total helpers, with 25 in `CoreRuleEngine.cs`.

2026-06-26 current-loop check: `rg "(?:private|public|internal)\\s+static\\s+bool\\s+Is[A-Za-z0-9_]*CardNo\\s*\\(" src/Riftbound.Engine src/Riftbound.CardCatalog src/Riftbound.Contracts --count-matches` reports no helper definitions.

## 5. Non-Closure Statement

This evidence does not close complete trigger-payment official breadth, Jax full official behavior, Fiora full official behavior, complete equipment lifecycle, card matrix full-official, frontend final validation or READY.

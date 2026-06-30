# Plan B Unit Trigger Payment TriggerSpec Evidence

日期：2026-06-27
结论：**EVIDENCE RECORDED / PROJECT NOT READY**

This file records evidence for routing the existing Jax and Fiora unit trigger-payment representatives through `BehaviorSpec.Triggers` instead of `CoreRuleEngine` source-effect selector branches.

2026-06-30 supplement: Icevale Archer / 冰谷弓箭手 attack-payment now has its own `UNIT_ATTACK_PAY_POWER_MODIFIER` TriggerSpec slice. See `docs/CURRENT_PLAN_B_ICEVALE_ATTACK_PAYMENT_TRIGGER_SPEC_AUDIT.md` and `docs/CURRENT_PLAN_B_ICEVALE_ATTACK_PAYMENT_TRIGGER_SPEC_EVIDENCE.md`.

2026-06-30 follow-up: Jax and Fiora pending-payment reason parsing and runtime trigger payloads now validate through `UnitTriggerPaymentSpecRules.TryGetUnitArmamentAttachedPayDrawTriggerByEffectKind(...)` and `TryGetUnitControlledUnitPowerfulPayPowerReadyTriggerByEffectKind(...)`. `CoreRuleEngine` no longer owns `JaxWeaponAttachPayOneDrawEffectKind`, `SfdFioraPowerfulReadyEffectKind`, or the literal Jax / Fiora wire strings.

## 1. Official Rule Evidence

- Official catalog entry `SFD·119/221` and `SFD·119a/221`: “当你为我贴附武装时，可以选择支付{{1}}，以此抽一张牌。”
- Official catalog entry `SFD·180/221` and `SFD·180a/221`: “当你控制的一名单位变为{{强力}}时，你可以选择支付{{黄色}}，以此让其变为活跃状态。（战力达到5或以上时，即为强力单位。）”

These entries are sourced from `data/official/card-catalog.zh-CN.json`; no official data file was edited.

## 2. Runtime Evidence

- `RuleTextParser` parses Jax weapon-attachment payment text into `TriggerSpec` with:
  - `Kind=JAX_WEAPON_ATTACH_PAY_1_DRAW_1` via `TriggerKinds.UnitArmamentAttachedPayDraw`
  - `Timing=UNIT_ARMAMENT_ATTACHED`
  - `TargetScope=FRIENDLY_EQUIPMENT`
  - `ManaCost=1`
  - `DrawCount=1`
  - `Optional=true`
- `RuleTextParser` parses Fiora powerful-ready payment text into `TriggerSpec` with:
  - `Kind=SFD_FIORA_POWERFUL_READY_PAY_YELLOW_READY` via `TriggerKinds.UnitControlledUnitPowerfulPayPowerReady`
  - `Timing=CONTROLLED_UNIT_BECAME_POWERFUL`
  - `TargetScope=CONTROLLED_UNIT_ON_FIELD`
  - `PowerCost=1`
  - `PowerCostTrait=yellow`
  - `RequiredPowerThreshold=5`
  - `UnitReadyCount=1`
  - `Optional=true`
- `TriggerSpec` now carries `PowerCostTrait`, and DevUi catalog typing mirrors that field for shared catalog payload compatibility.
- `UnitTriggerPaymentSpecRules` builds a catalog-backed map and exposes:
  - `TryGetUnitArmamentAttachedPayDrawTrigger(...)`
  - `TryGetUnitArmamentAttachedPayDrawTriggerByEffectKind(...)`
  - `TryGetUnitControlledUnitPowerfulPayPowerReadyTrigger(...)`
  - `TryGetUnitControlledUnitPowerfulPayPowerReadyTriggerByEffectKind(...)`
- `CoreRuleEngine.TryGetJaxWeaponAttachSource(...)` and `TryGetSfdFioraPowerfulReadySource(...)` now require the relevant `TriggerSpec` and keep the previous public-field, visible, non-standby, current-controller / legacy-owned source guards.
- `CoreRuleEngine` builds pending-payment reasons, validates submitted payment reasons, and emits trigger/reason payload fields from the runtime trigger effect kind derived from `TriggerSpec`.
- Jax payment-window cost and draw resolution now read `ManaCost` / `DrawCount` from `TriggerSpec`.
- Fiora payment-window cost, typed trait and threshold now read `PowerCost` / `PowerCostTrait` / `RequiredPowerThreshold` from `TriggerSpec`.
- The wire-compatible trigger/effect strings are intentionally preserved: `JAX_WEAPON_ATTACH_PAY_1_DRAW_1` and `SFD_FIORA_POWERFUL_READY_PAY_YELLOW_READY`.
- `CoreRuleEngine` no longer defines the Jax / Fiora source-effect constants or local source-behavior helper methods removed by this slice.

## 3. Test Evidence

Focused test file:

- `tests/Riftbound.ConformanceTests/CardCatalogBaselineTests.cs`
- `tests/Riftbound.ConformanceTests/TriggerPaymentTests.cs`

Coverage:

- `BehaviorSpecCatalogParsesUnitTriggerPaymentTriggers` proves the official Jax and Fiora card entries produce the expected `TriggerSpec` rows.
- `TriggerPaymentSourceIdentityDoesNotUseDuplicatedCardNumberAllowLists` now blocks reintroducing the old Jax / Fiora source-effect constants, runtime wire constants, and helper methods in `CoreRuleEngine`, while requiring the new `UnitTriggerPaymentSpecRules` calls.
- Existing Jax and Fiora trigger-payment runtime tests verify prompt opening, pay, decline, insufficient payment and hidden/source guard behavior remains intact.

## 4. Verification

```sh
/Users/dinghaolin/.dotnet/dotnet build src/Riftbound.Engine/Riftbound.Engine.csproj -c Debug --nologo
```

Result: build passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~BehaviorSpecCatalogParsesUnitTriggerPaymentTriggers|FullyQualifiedName~TriggerPaymentSourceIdentityDoesNotUseDuplicatedCardNumberAllowLists" --nologo
```

Result: 2/2 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~JaxWeaponAttach|FullyQualifiedName~SfdFiora|FullyQualifiedName~TriggerPaymentSourceIdentityDoesNotUseDuplicatedCardNumberAllowLists|FullyQualifiedName~BehaviorSpecCatalogParsesUnitTriggerPaymentTriggers" --nologo
```

Result: 40/40 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~TriggerPayment|FullyQualifiedName~Jax|FullyQualifiedName~Fiora|FullyQualifiedName~PaymentEngine|FullyQualifiedName~MatchRecovery|FullyQualifiedName~CardCatalogBaseline" --nologo
```

Result: 3175/3175 passed.

2026-06-30 follow-up adjacent:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --nologo --filter "FullyQualifiedName~JaxWeaponAttach|FullyQualifiedName~SfdFiora|FullyQualifiedName~TriggerPayment|FullyQualifiedName~PaymentEngine|FullyQualifiedName~MatchRecovery|FullyQualifiedName~CardCatalogBaseline"
```

Result: 3187/3187 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result: 8813/8813 passed.

2026-06-30 follow-up backend full:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --nologo
```

Result: 9038/9038 passed.

```sh
/opt/homebrew/bin/npm --prefix src/Riftbound.DevUi run build
```

Result: passed. npm emitted existing config warnings and Vite emitted the existing chunk-size warning.

## 5. Non-Closure Statement

This evidence does not close complete trigger-payment official breadth, Jax full official behavior, Fiora full official behavior, complete Icevale attack-trigger family breadth, complete equipment lifecycle, full PaymentEngine / PAY_COST breadth, card matrix full-official, frontend final validation, formal E2E or READY.

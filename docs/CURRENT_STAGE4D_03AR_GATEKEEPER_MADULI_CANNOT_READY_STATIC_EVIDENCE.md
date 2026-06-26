# Stage 4D-03AR Gatekeeper Maduli Cannot-Ready Static Evidence

日期：2026-05-15
结论：**EVIDENCE RECORDED / PROJECT NOT READY**

## 1. Official Text

`data/official/card-catalog.zh-CN.json` contains:

- `UNL-144/219`, card id 34688, 守门者马杜里, purple, unit, energy 7, power 6.

Official text:

```txt
我无法变为活跃状态。
支付{{紫色}}：如果我的战力大于一处敌方控制的战场上所有敌方单位的战力总和，则将我移动到该处。
```

## 2. Runtime Evidence

Implementation facts:

- `BehaviorSpecCatalogBuilder` parses official `UNL-144/219` text into `StaticAbilityKinds.UnitCannotBecomeActive`.
- `CardStaticAbilitySpecRules.CardCannotBecomeActive()` binds the cannot-ready policy through `BehaviorSpec.StaticAbilities`, not through a P4 activated-ability card-number predicate.
- `BattlefieldStaticAbilitySpecRules` reuses the shared static-ability map for existing battlefield static lookups.
- `MatchSession` filters Crimson Rose ready-unit target choices with the cannot-active policy.
- Maduli `ACTIVATE_ABILITY` prompt metadata now reports `staticCannotBecomeActivePolicy=implemented`.
- `CoreRuleEngine` rejects direct Crimson Rose ready-unit commands targeting Maduli before payment / stack creation.
- Crimson Rose stale stack resolution checks the cannot-active policy and therefore leaves Maduli exhausted and emits no Maduli `UNIT_READIED`.
- Shared `ApplyReadyState` skips cannot-active cards, so mass ready behavior can ready other legal units while leaving Maduli exhausted.

## 3. Test Evidence

New or updated focused tests cover:

- Crimson Rose prompt hides friendly exhausted Maduli as a ready target.
- Crimson Rose hand-written command targeting Maduli rejects no-mutation.
- Crimson Rose stale stack item targeting Maduli resolves without readying Maduli or emitting Maduli `UNIT_READIED`.
- Hunt mass ready keeps Maduli exhausted while readying the ordinary friendly base and battlefield units.
- Maduli purple move prompt metadata now expects `implemented` cannot-ready static policy.
- BehaviorSpec catalog parses `UNL-144/219` as `UNIT_CANNOT_BECOME_ACTIVE`.
- Source guards reject the old `P4ActivatedAbilityCatalog.CardCannotBecomeActive` predicate.

## 4. Commands Run

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Maduli|FullyQualifiedName~Gatekeeper|FullyQualifiedName~CrimsonRose|FullyQualifiedName~HuntReadyGuardTests"
```

Result: 65/65 passed.

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Maduli|FullyQualifiedName~Gatekeeper|FullyQualifiedName~CrimsonRose|FullyQualifiedName~HuntReadyGuardTests|FullyQualifiedName~ActivateAbility|FullyQualifiedName~PaymentEngine|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub|FullyQualifiedName~Priority"
```

Result: 375/375 passed.

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

Result: 4345/4345 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo --filter "FullyQualifiedName~BehaviorSpecCatalogParsesUnitCannotBecomeActiveStaticAbility|FullyQualifiedName~UnitCannotBecomeActiveStaticDoesNotUseP4CardNumberPredicate"
```

Result: 2/2 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo --filter "FullyQualifiedName~CardCatalogBaseline|FullyQualifiedName~GatekeeperMaduli|FullyQualifiedName~CrimsonRose|FullyQualifiedName~HuntReadyGuard"
```

Result: 328/328 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result: 8770/8770 passed.

```sh
git diff --check
```

Result: passed.

## 5. Non-Completion Notes

- This evidence does not update `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`.
- This evidence does not claim full-official Maduli or full LayerEngine coverage.
- This evidence does not replace final frontend build / Chrome smoke / formal 18-step fresh-run.
- This evidence does not close P0-005, P1, full-card matrix or READY.

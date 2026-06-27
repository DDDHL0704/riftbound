# Plan B Unit Last-Breath Source-Battlefield AoE Damage TriggerSpec Evidence

日期：2026-06-27
结论：**EVIDENCE RECORDED / PROJECT NOT READY**

This file records evidence for routing the existing Kogmaw last-breath source-battlefield AoE damage representative through `BehaviorSpec.Triggers` instead of `CoreRuleEngine` local Kogmaw constants.

## 1. Official Rule Evidence

- Official catalog entry `OGN·190/298`: `{{绝念}}-对我所处战场上的所有单位各造成4点伤害。（当我被摧毁后，发动此效果。）`
- Existing evidence for `p2-preflight-play-ogn-kogmaw-last-breath-static` covers the current representative damage path and hidden-source guard.

No official data file was edited.

## 2. Runtime Evidence

- `RuleTextParser` parses Kogmaw last-breath AoE damage text into `TriggerSpec` with:
  - `Kind=OGN_KOGMAW_LAST_BREATH_AOE_PLAY_UNIT` via `TriggerKinds.UnitLastBreathDamageSourceBattlefieldUnits`
  - `Timing=UNIT_DESTROYED`
  - `TargetScope=SOURCE_BATTLEFIELD_UNITS`
  - `DamageAmount=4`
- `UnitDestroyedTriggerSpecRules.TryGetLastBreathSourceBattlefieldAoeDamageTrigger(...)` exposes the parsed trigger shape to the shared engine.
- `ResolveUnitLastBreathSourceBattlefieldAoeDamagePlayerId(...)` accepts a destroyed source through the parsed TriggerSpec plus visible unit boundary checks.
- Stack resolution reads damage amount from TriggerSpec while preserving the existing trigger queue, stack and recovery shape.
- `CoreRuleEngine` no longer defines `KogmawCardNo`, `KogmawLastBreathAoeEffectKind`, or `KogmawLastBreathDamage`, and no longer has Kogmaw-named helper/local paths.
- The public effect string remains `OGN_KOGMAW_LAST_BREATH_AOE_PLAY_UNIT`, so existing recovery and replay validators remain compatible.

## 3. Test Evidence

Focused test file:

- `tests/Riftbound.ConformanceTests/CardCatalogBaselineTests.cs`

Coverage:

- `BehaviorSpecCatalogParsesUnitLastBreathSourceBattlefieldAoeDamageTrigger` proves the official Kogmaw entry produces the expected `TriggerSpec` row.
- `UnitLastBreathSourceBattlefieldAoeDamageTriggerDoesNotUseCoreCardNumberBehavior` blocks reintroducing the old Core local card-number / effect / damage constant branch.
- Existing Kogmaw real trigger queue tests prove stack destruction, state-based cleanup, hidden-source guard and missing-battlefield guard remain green.
- Existing MatchRecovery representatives prove trigger queue / stack / spectator validation compatibility remains green.

## 4. Verification

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~BehaviorSpecCatalogParsesUnitLastBreathSourceBattlefieldAoeDamageTrigger|FullyQualifiedName~UnitLastBreathSourceBattlefieldAoeDamageTriggerDoesNotUseCoreCardNumberBehavior" --nologo
```

Result: 2/2 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~Kogmaw|FullyQualifiedName~LastBreath|FullyQualifiedName~RealTriggerQueue|FullyQualifiedName~TriggerSourceIdentityGuard|FullyQualifiedName~CardCatalogBaseline|FullyQualifiedName~MatchRecovery|FullyQualifiedName~ConformanceFixtureRunner" --nologo
```

Result: 5477/5477 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result: 8823/8823 passed.

DevUi build was not rerun because this slice did not change DevUi source or shared TypeScript catalog payload shape.

## 5. Non-Closure Statement

This evidence does not close complete AoE damage / replacement / prevention matrix, complete last-breath trigger timing, complete trigger queue ordering, B0 full-game end-to-end good-terminal-state evidence, card matrix full-official state, frontend final validation, or READY.

# Plan B B1 Static Aura Spec Audit

更新时间：2026-06-24

## Scope

These B1 slices advance Plan B by moving implemented `STATIC_AURA` projection and combat-power recompute surfaces from card-number allow-lists toward `BehaviorSpec` data.

Implemented in this slice:

- `BehaviorSpec.StaticAuras` protocol/catalog surface.
- Parser coverage for the currently implemented static-power representatives:
  - `SFD·085/221` / `SFD·085a/221` 奥恩：每有一件友方装备，自身 `{{S}}+1`。
  - `OGN·294/298` 崔法利兵营：此处所有单位 `{{S}}+1`。
  - `OGS·013/024` 盖伦、`SFD·236/221` / `SFD·236*/221` 德莱厄斯、`OGN·243/298` / `OGN·243a/298` 德莱厄斯：此处其他友方单位 `{{S}}+1`。
  - `UNL-147/219` / `UNL-147a/219` / `UNL-238/219` 纳什男爵：其他友方单位 `{{S}}+2`。
  - `UNL-077/219` 牧魂人：你的指示物单位获得 `{{S}}+1`。
  - `SFD·089/221` / `SFD·089a/221` 兰博：你的“机械”属性单位获得 `{{S}}+1`。
  - `OGN·151/298` / `OGN·151a/298` 李青：我所在战场上其他拥有增益的友方单位获得 `{{S}}+2`。
  - `UNL·T03` 草丛：此处的“鸟类”、“猫科”、“犬形”、“魄罗”属性单位和艾翁单位获得 `{{S}}+1`。
  - `UNL-076/219` 花瓣仙子：我所处的战场你每有一名拥有 `{{瞬息}}` 的单位，我便获得 `{{S}}+1`。
  - `OGN·240/298` / `OGN·240a/298` 瑟提：我所处的战场每有一名拥有增益的友方单位，我便获得 `{{S}}+1`。
- Parser false-positive guards:
  - `UNL-043/219` 热情的播报员：其 card text grants `{{增益}}` tokens and must not be treated as a fixed `STATIC_AURA` power modifier.
  - `UNL-195/219` 翠神：parenthetical reminder text describes the Brush battlefield token and must not be treated as a legend-source `STATIC_AURA`.
- `MatchSession` continuous-effect projection now resolves these `STATIC_AURA` kinds via `StaticAuraSpecRules` instead of `ContinuousEffectStaticAuraCards`.
- `CoreRuleEngine.ResolveBattlefieldAllUnitsPowerBonus`, `CoreRuleEngine.ResolveBattlefieldFilteredUnitsPowerBonus`, `CoreRuleEngine.ResolveSameBattlefieldFriendlyFilteredUnitCountToSourcePowerBonus`, `CoreRuleEngine.ResolveSameBattlefieldOtherFriendlyUnitsPowerBonus`, `CoreRuleEngine.ResolveSameBattlefieldOtherFriendlyFilteredUnitsPowerBonus`, `CoreRuleEngine.ResolveOtherFriendlyUnitsPowerBonus`, and `CoreRuleEngine.ResolveFriendlyFilteredUnitsPowerBonus` now apply these static power auras from `BehaviorSpec.StaticAuras`.
- Recovery static-aura source-card validation now checks the source card's `BehaviorSpec` aura surface for battlefield all-units, battlefield-filtered, same-battlefield friendly-filtered count-to-source, same-battlefield other-friendly, same-battlefield friendly-filtered, non-local other-friendly, and friendly-filtered unit auras instead of a projection allow-list.

## Not Closed

This slice does not claim full B1 completion:

- `CoreRuleEngine.ApplyFriendlyEquipmentStaticPowerRecompute` still uses the existing runtime bridge for Ornn dynamic power recompute.
- Battlefield card recognition still has an implemented-battlefield-card registry; this slice only removes card-number gating from the static-power bonus arithmetic.
- Current non-local other-friendly aura coverage is the fixed static-power family only; Nash battlefield-token creation, replacement entry destination, and enemy spell/skill target protection remain open.
- Multiple aura stacking beyond additive representative coverage, full LayerEngine timestamp ordering, additional conditional subscopes, RULE_TEXT keyword grants, and full official static-aura breadth remain open.
- Current `private static bool Is*CardNo` count is 102 total / 98 in `CoreRuleEngine`; `IsPetalPixieCardNo` was deleted in this slice.
- Project remains NOT READY.

## Rule Authority

- Official card text from `data/official/card-catalog.zh-CN.json`.
- `SFD·085/221` / `SFD·085a/221`: `每有一件友方装备，我便获得{{S}}+1。`
- `OGN·294/298`: `此处的所有单位获得{{S}}+1。（包括进攻方单位。）`
- `OGS·013/024`: `此处的其他友方单位获得{{S}}+1。`
- `SFD·236/221` / `SFD·236*/221` / `OGN·243/298` / `OGN·243a/298`: `此处的其他友方单位获得{{S}}+1。`
- `UNL-147/219` / `UNL-147a/219` / `UNL-238/219`: `其他友方单位获得{{S}}+2。`
- `UNL-077/219`: `你的指示物单位获得{{S}}+1。`
- `SFD·089/221` / `SFD·089a/221`: `你的“机械”属性单位获得{{S}}+1。（包括我。）`
- `OGN·151/298` / `OGN·151a/298`: `我所在战场上其他拥有增益的友方单位获得{{S}}+2。`
- `UNL·T03`: `此处的“鸟类”、“猫科”、“犬形”、“魄罗”属性单位和艾翁单位获得{{S}}+1。`
- `UNL-076/219`: `我所处的战场你每有一名拥有{{瞬息}}的单位，我便获得{{S}}+1。`
- `OGN·240/298` / `OGN·240a/298`: `我所处的战场每有一名拥有增益的友方单位，我便获得{{S}}+1。`
- `UNL-043/219`: `给予此处的所有单位{{增益}}。（未拥有增益的单位获得一个{{S}}+1增益。）`
- `UNL-195/219`: `位于草丛的“鸟类”、“猫科”、“犬形”、“魄罗”和“艾翁”属性单位获得{{S}}+1。` appears only as parenthetical token reminder text.
- Rule authority protocol: `docs/rules-authority-and-audit.md`.

## Validation

Focused:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~BehaviorSpecCatalogParsesStaticAuraSpecsForExistingRepresentatives|FullyQualifiedName~P79OtherFriendlyStaticPowerAddsTwoAcrossPublicField"
```

Result: 2/2 passed for the original B1 representative slice.

Latest same-battlefield friendly-filtered focused check:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~BehaviorSpecCatalogParsesStaticAuraSpecsForExistingRepresentatives|FullyQualifiedName~SameBattlefieldOtherFriendlyFilteredStaticPower"
```

Result: 2/2 passed.

Latest same-battlefield friendly-filtered count-to-source focused check:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~BehaviorSpecCatalogParsesStaticAuraSpecsForExistingRepresentatives|FullyQualifiedName~PetalPixieCountsFriendlyEphemeral|FullyQualifiedName~StaticAuraProjectionDoesNotUseMatchSessionCardNumberAllowList"
```

Result: 3/3 passed.

Latest Sett same-battlefield boon count-to-source focused check:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~CardCatalogBaselineTests|FullyQualifiedName~P79SettCountsFriendlyBoonUnitsAtSameBattlefieldForBattlePower"
```

Result: 79/79 passed.

Latest battlefield-filtered focused check:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~BehaviorSpecCatalogParsesStaticAuraSpecsForExistingRepresentatives|FullyQualifiedName~BattlefieldFilteredStaticPower"
```

Result: 2/2 passed.

Adjacent:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~StaticAura|FullyQualifiedName~OrnnFriendlyEquipmentStaticPower|FullyQualifiedName~LayerEngineBattlefieldStaticAura|FullyQualifiedName~BattlefieldStaticPower|FullyQualifiedName~P79OtherFriendlyStaticPower|FullyQualifiedName~MatchRecoveryTests.RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffect|FullyQualifiedName~FullGameEndToEndTests"
```

Result: 356/356 passed.

Latest adjacent:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~StaticAura|FullyQualifiedName~StaticPower|FullyQualifiedName~ContinuousEffect"
```

Result: 399/399 passed.

Latest adjacent after battlefield-filtered slice:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~StaticAura|FullyQualifiedName~StaticPower|FullyQualifiedName~ContinuousEffect"
```

Result: 400/400 passed.

Latest adjacent after same-battlefield count-to-source slice:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~StaticAura|FullyQualifiedName~StaticPower|FullyQualifiedName~ContinuousEffect|FullyQualifiedName~PetalPixie"
```

Result: 401/401 passed.

Latest adjacent after Sett same-battlefield boon count-to-source slice:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~StaticAura|FullyQualifiedName~StaticPower|FullyQualifiedName~ContinuousEffect|FullyQualifiedName~PetalPixie|FullyQualifiedName~Sett"
```

Result: 427/427 passed.

Full backend:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj
```

Result: 8365/8365 passed.

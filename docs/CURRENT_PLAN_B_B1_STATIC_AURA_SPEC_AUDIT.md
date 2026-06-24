# Plan B B1 Static Aura Spec Audit

更新时间：2026-06-24

## Scope

This slice advances Plan B / B1 by moving the first `STATIC_AURA` projection surface from a `MatchSession` card-number allow-list toward `BehaviorSpec` data.

Implemented in this slice:

- `BehaviorSpec.StaticAuras` protocol/catalog surface.
- Parser coverage for the currently implemented static-power representatives:
  - `SFD·085/221` / `SFD·085a/221` 奥恩：每有一件友方装备，自身 `{{S}}+1`。
  - `OGN·294/298` 崔法利兵营：此处所有单位 `{{S}}+1`。
  - `OGS·013/024` 盖伦、`SFD·236/221` / `SFD·236*/221` 德莱厄斯、`OGN·243/298` / `OGN·243a/298` 德莱厄斯：此处其他友方单位 `{{S}}+1`。
  - `UNL-147/219` / `UNL-147a/219` / `UNL-238/219` 纳什男爵：其他友方单位 `{{S}}+2`。
- Parser false-positive guard for `UNL-043/219` 热情的播报员：其 card text grants `{{增益}}` tokens and must not be treated as a fixed `STATIC_AURA` power modifier.
- `MatchSession` continuous-effect projection now resolves these `STATIC_AURA` kinds via `StaticAuraSpecRules` instead of `ContinuousEffectStaticAuraCards`.
- `CoreRuleEngine.ResolveBattlefieldAllUnitsPowerBonus`, `CoreRuleEngine.ResolveSameBattlefieldOtherFriendlyUnitsPowerBonus`, and `CoreRuleEngine.ResolveOtherFriendlyUnitsPowerBonus` now apply these static power auras from `BehaviorSpec.StaticAuras`.
- Recovery static-aura source-card validation now checks the source card's `BehaviorSpec` aura surface for battlefield all-units, same-battlefield other-friendly, and non-local other-friendly unit auras instead of a projection allow-list.

## Not Closed

This slice does not claim full B1 completion:

- `CoreRuleEngine.ApplyFriendlyEquipmentStaticPowerRecompute` still uses the existing runtime bridge for Ornn dynamic power recompute.
- Battlefield card recognition still has an implemented-battlefield-card registry; this slice only removes card-number gating from the static-power bonus arithmetic.
- Current non-local other-friendly aura coverage is the fixed static-power family only; Nash battlefield-token creation, replacement entry destination, and enemy spell/skill target protection remain open.
- Multiple aura stacking beyond additive representative coverage, full LayerEngine timestamp ordering, generic conditional subscopes, RULE_TEXT keyword grants, and full official static-aura breadth remain open.
- Current `Is*CardNo` count remains 108 total / 101 in `CoreRuleEngine`; no white-list function was deleted in this slice.
- Project remains NOT READY.

## Rule Authority

- Official card text from `data/official/card-catalog.zh-CN.json`.
- `SFD·085/221` / `SFD·085a/221`: `每有一件友方装备，我便获得{{S}}+1。`
- `OGN·294/298`: `此处的所有单位获得{{S}}+1。（包括进攻方单位。）`
- `OGS·013/024`: `此处的其他友方单位获得{{S}}+1。`
- `SFD·236/221` / `SFD·236*/221` / `OGN·243/298` / `OGN·243a/298`: `此处的其他友方单位获得{{S}}+1。`
- `UNL-147/219` / `UNL-147a/219` / `UNL-238/219`: `其他友方单位获得{{S}}+2。`
- `UNL-043/219`: `给予此处的所有单位{{增益}}。（未拥有增益的单位获得一个{{S}}+1增益。）`
- Rule authority protocol: `docs/rules-authority-and-audit.md`.

## Validation

Focused:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~BehaviorSpecCatalogParsesStaticAuraSpecsForExistingRepresentatives|FullyQualifiedName~P79OtherFriendlyStaticPowerAddsTwoAcrossPublicField"
```

Result: 2/2 passed.

Adjacent:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~StaticAura|FullyQualifiedName~OrnnFriendlyEquipmentStaticPower|FullyQualifiedName~LayerEngineBattlefieldStaticAura|FullyQualifiedName~BattlefieldStaticPower|FullyQualifiedName~P79OtherFriendlyStaticPower|FullyQualifiedName~MatchRecoveryTests.RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffect|FullyQualifiedName~FullGameEndToEndTests"
```

Result: 356/356 passed.

Full backend:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj
```

Result: 8359/8359 passed.

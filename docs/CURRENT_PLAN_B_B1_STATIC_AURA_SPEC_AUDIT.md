# Plan B B1 Static Aura Spec Audit

更新时间：2026-06-24

## Scope

This slice advances Plan B / B1 by moving the first `STATIC_AURA` projection surface from a `MatchSession` card-number allow-list toward `BehaviorSpec` data.

Implemented in this slice:

- `BehaviorSpec.StaticAuras` protocol/catalog surface.
- Parser coverage for the two already implemented representatives:
  - `SFD·085/221` / `SFD·085a/221` 奥恩：每有一件友方装备，自身 `{{S}}+1`。
  - `OGN·294/298` 崔法利兵营：此处所有单位 `{{S}}+1`。
- `MatchSession` continuous-effect projection now resolves these two `STATIC_AURA` kinds via `StaticAuraSpecRules` instead of `ContinuousEffectStaticAuraCards`.
- Recovery static-aura source-card validation now checks the source card's `BehaviorSpec` aura surface instead of the removed `MatchSession` allow-list.

## Not Closed

This slice does not claim full B1 completion:

- `CoreRuleEngine.ApplyFriendlyEquipmentStaticPowerRecompute` still uses the existing runtime bridge for Ornn dynamic power recompute.
- Combat damage `ResolveBattlefieldAllUnitsPowerBonus` still uses the existing battlefield representative runtime path.
- Multiple aura stacking, full LayerEngine timestamp ordering, generic conditional subscopes, RULE_TEXT keyword grants, and full official static-aura breadth remain open.
- Project remains NOT READY.

## Rule Authority

- Official card text from `data/official/card-catalog.zh-CN.json`.
- `SFD·085/221` / `SFD·085a/221`: `每有一件友方装备，我便获得{{S}}+1。`
- `OGN·294/298`: `此处的所有单位获得{{S}}+1。（包括进攻方单位。）`
- Rule authority protocol: `docs/rules-authority-and-audit.md`.

## Validation

Focused:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~BehaviorSpecCatalogParsesStaticAuraSpecsForExistingRepresentatives|FullyQualifiedName~StaticAuraProjectionDoesNotUseMatchSessionCardNumberAllowList"
```

Result: 2/2 passed.

Adjacent:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~StaticAura|FullyQualifiedName~OrnnFriendlyEquipmentStaticPower|FullyQualifiedName~LayerEngineBattlefieldStaticAura|FullyQualifiedName~BattlefieldStaticPower"
```

Result: 217/217 passed.

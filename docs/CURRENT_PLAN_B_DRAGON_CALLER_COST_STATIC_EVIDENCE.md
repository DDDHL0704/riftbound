# Plan B Dragon Caller Cost Static Evidence

## Evidence

- `data/official/card-catalog.zh-CN.json` records `OGN·140/298` card text: Dragon unit mana costs are reduced by 2 and cannot go below 1.
- `src/Riftbound.Engine/CardBehaviorRegistry.cs` binds `OGN·140/298` to `DRAGON_CALLER_COST_STATIC_PLAY_UNIT`.
- `tests/Riftbound.ConformanceTests/DragonCallerCostStaticTests.cs` covers:
  - prompt metadata reducing a Dragon unit and not a non-Dragon unit;
  - paying the reduced cost for a Dragon unit;
  - stacked Dragon Caller sources respecting the one-mana floor;
  - non-Dragon unit cost rejection at insufficient mana;
  - public controlled source requirements;
  - dynamic prompt recomputation after the source leaves play.
- `src/Riftbound.Engine/CoreRuleEngine.cs` and `src/Riftbound.Engine/MatchSession.cs` share the same cost-reduction ordering and expose `dragonUnitCostReductionMana`.

## Validation

Focused Dragon Caller cost-static regression passed: 11/11.
Adjacent payment / prompt / dragon / stack / recovery regression passed: 3931/3931.
Backend conformance full passed: 8925/8925.

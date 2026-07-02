# Plan B / Unit Moved Trigger Spec Audit

Date: 2026-06-25

Status: focused unit-moved trigger-spec slice accepted; project remains **NOT READY**.

## 2026-07-02 Follow-up: Generic Predicate Surface

`UnitMovedTriggerSpecRules.TryGetUnitMovedCreateDormantGoldTrigger(...)` has been removed. `CoreRuleEngine.ResolveUnitMovedCreateDormantGoldTrigger(...)` now routes through `UnitMovedTriggerSpecRules.TryGetTrigger(cardNo, UnitMovedTriggerSpecRules.IsUnitMovedCreateDormantGoldTrigger, out trigger)` and keeps the same Treasure Hunter movement guards, token creation payloads, parsed token shape, and hidden-info boundaries. Validation: focused guard / representative runtime set `38/38`, adjacent move / discard / boon / battlefield-held / recovery / full-game representative set `2790/2790`, backend full conformance `9141/9141`. This follow-up only removes the per-effect rules API; it does not add new official-text interpretation, complete the move-trigger family, or mark the project READY.

## Scope

This slice moves the implemented Treasure Hunter move-Gold representative from a Core card-number branch to `BehaviorSpec.Triggers`:

- Official catalog source: `data/official/card-catalog.zh-CN.json` has `SFD·130/221` 寻宝猎人 text `每当我移动时，打出一个休眠的“金币”装备指示物。`
- `TriggerKinds.UnitMovedCreateDormantGold` preserves the existing emitted trigger value `TREASURE_HUNTER_MOVE_CREATE_GOLD` for event/replay compatibility.
- `TriggerTimings.UnitMoved` models the movement trigger timing.
- `RuleTextParsers.TriggerParser` now parses the official text into `TriggerSpec.Kind = TREASURE_HUNTER_MOVE_CREATE_GOLD`, `Timing = UNIT_MOVED`, `TargetScope = SOURCE_UNIT`, `CreatedTokenCount = 1`, `CreatedTokenName = 金币`, `CreatedTokenDestination = OWNER_BASE`, `CreatedTokenExhausted = true`, and `CreatedTokenKeywords = [反应]`.
- `CoreRuleEngine.ResolveUnitMovedCreateDormantGoldTrigger(...)` now checks `UnitMovedTriggerSpecRules.TryGetTrigger(..., UnitMovedTriggerSpecRules.IsUnitMovedCreateDormantGoldTrigger, ...)` and reads the token shape from `TriggerSpec`.
- The old Core `TreasureHunterCardNo`, `TreasureHunterMoveCreateGoldEffectKind`, and `IsTreasureHunterCardNo(...)` branch is removed.

## Runtime Effect

- Successful visible, face-up source-unit movement still emits `TRIGGER_RESOLVED` with `trigger = TREASURE_HUNTER_MOVE_CREATE_GOLD` and creates dormant Gold equipment token(s) in the controller base.
- Token name, token count, exhausted state, and token tags now come from the parsed `TriggerSpec`.
- Existing guards remain: no trigger for no-op moves, hidden / face-down / standby source, opponent-controlled source, non-unit source, failed moves, or sources that are no longer controlled by the moving player.
- The frontend and protocol shape are unchanged beyond adding typed trigger constants; DevUi was not changed.

## Non-Goals

- This does not implement the complete move-trigger family or simultaneous movement trigger batching.
- This does not convert movement triggers to full `ORDER_TRIGGERS` / stack timing.
- This does not complete movement replacement/prevention, equipment movement, Gold token full official rules, hidden face-down original trigger policy, or full official Treasure Hunter breadth.
- This does not close B0 full-game readiness or project READY.

## Validation

- Focused behavior-spec / source guard / Treasure Hunter representatives: `11/11` passing.
- Adjacent movement / roam / battlefield moved / Treasure Hunter / full-game representatives: `133/133` passing.
- `MatchRecovery`: `1989/1989` passing.
- Backend full conformance: `8527/8527` passing.
- Source-helper count for `private static bool Is*CardNo(...)`: `34` total / `31` in `CoreRuleEngine`.

# Plan B / Unit Moved Trigger Spec Evidence

Date: 2026-06-25

Project status: **NOT READY**.

## Rule Sources

- `data/official/card-catalog.zh-CN.json`: `SFD·130/221` 寻宝猎人 official text is `每当我移动时，打出一个休眠的“金币”装备指示物。`
- `docs/CURRENT_STAGE4C_BATCH27_TREASURE_HUNTER_MOVE_GOLD_AUDIT.md` and `docs/CURRENT_STAGE4C_BATCH27_TREASURE_HUNTER_MOVE_GOLD_EVIDENCE.md`: existing representative move-Gold behavior evidence.
- `docs/rules-authority-and-audit.md` and `docs/rules-evidence-index.md`: official catalog and local evidence-index entries remain the authority inputs for this representative slice.

## BehaviorSpec Evidence

- `BehaviorSpecCatalogParsesUnitMovedCreateDormantGoldTrigger` verifies that `SFD·130/221` parses to `TriggerSpec.Kind = TREASURE_HUNTER_MOVE_CREATE_GOLD`, `Timing = UNIT_MOVED`, `TargetScope = SOURCE_UNIT`, `CreatedTokenCount = 1`, `CreatedTokenName = 金币`, `CreatedTokenDestination = OWNER_BASE`, `CreatedTokenExhausted = true`, and `CreatedTokenKeywords = [反应]`.
- `UnitMovedCreateDormantGoldTriggerDoesNotUseCardNumberAllowList` verifies `CoreRuleEngine` no longer contains `TreasureHunterCardNo`, `IsTreasureHunterCardNo`, `TreasureHunterMoveCreateGoldEffectKind`, `SFD·130/221`, or the old literal trigger value as a Core branch.
- `TreasureHunterMoveCreatesDormantGoldToken` and `TreasureHunterPreciseRoamMoveCreatesDormantGoldToken` verify the existing base-to-battlefield and precise-roam representative movement routes still create dormant Gold equipment tokens.
- `TreasureHunterHiddenStandbyOrOpponentControlledDoesNotTrigger`, `NonTreasureHunterMoveDoesNotTrigger`, `FailedTreasureHunterMoveDoesNotCreateGold`, and `TreasureHunterPreciseRoamNoOpDoesNotCreateGold` keep the existing guard matrix green.

## Runtime Evidence

- `UnitMovedTriggerSpecRules` builds its trigger map from `BehaviorSpecCatalogBuilder`, matching the existing unit-conquest and unit-destroyed trigger-spec rule pattern.
- `CoreRuleEngine.ResolveUnitMovedCreateDormantGoldTrigger(...)` now reads token count/name/destination/exhausted state/keywords from `TriggerSpec`.
- The emitted trigger payload remains `TREASURE_HUNTER_MOVE_CREATE_GOLD` for compatibility, but Core no longer selects the behavior by a Treasure Hunter card-number constant.

## Validation

- Focused behavior-spec / source guard / Treasure Hunter representatives: `11/11` passing.
- Adjacent movement / roam / battlefield moved / Treasure Hunter / full-game representatives: `133/133` passing.
- `MatchRecovery`: `1989/1989` passing.
- Backend full conformance: `8527/8527` passing.

## Residual Risk

- This slice proves one unit-moved create-dormant-Gold representative has moved to BehaviorSpec-driven routing. It does not prove the complete move-trigger family, complete simultaneous-trigger timing, optional trigger prompt breadth, full movement/control-zone matrix, all Gold token rules, frontend smoke, or READY.

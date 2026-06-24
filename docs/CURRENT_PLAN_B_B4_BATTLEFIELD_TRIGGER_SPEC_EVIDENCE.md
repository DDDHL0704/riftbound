# Plan B / B4 Battlefield Trigger Spec Evidence

Date: 2026-06-25

Project status: **NOT READY**.

## Rule Sources

- `data/official/card-catalog.zh-CN.json`: `OGN·277/298` 后巷酒吧 has official text `每当一名单位从此处向别处移动时，让其本回合内{{S}}+1。`
- `docs/rules-authority-and-audit.md` and `docs/rules-evidence-index.md`: official card text and local evidence remain the rule authority inputs for this battlefield-domain slice.
- Existing representative tests `P79BattlefieldMovedUnitGainsTemporaryPower`, `P79BattlefieldMovedUnitPowerSkipsOpponentControlledSource`, and `P79BattlefieldMovePowerSeedMovesUnitAndAppliesBonus` remain the runtime evidence for this narrow behavior.

## Runtime Evidence

The new parser path turns the official text into a structured `TriggerSpec`, including the moved-unit target scope, until-end-of-turn duration and numeric power delta. The runtime no longer checks `OGN·277/298` through `BattlefieldMovedUnitPowerPlusOneCardNo` / `IsBattlefieldMovedUnitPowerPlusOneCardNo`; it queries `BehaviorSpec.Triggers` via `BattlefieldTriggerSpecRules`.

The accepted `MOVE_UNIT` path still applies the same server-authoritative mutation after a successful battlefield-origin move. It now emits `BATTLEFIELD_TRIGGER_RESOLVED` and `POWER_MODIFIED_UNTIL_END_OF_TURN` with `BATTLEFIELD_UNIT_MOVED_AWAY_POWER_MODIFIER` as the trigger / reason, and reads the applied `+1` from the parsed spec.

## Hidden Information Evidence

No snapshot hidden-zone logic was changed. The representative GameHub and MatchRecovery validation still cover prompt/snapshot boundaries; MatchRecovery passed `1989/1989`.

## Validation

- focused behavior-spec/source guard/runtime/GameHub representative: `5/5`;
- adjacent BattlefieldMoved / BattlefieldMovePower / MoveUnit / BoardTaskQueue / FullGame / GameHub: `326/326`;
- MatchRecovery: `1989/1989`;
- backend full conformance: `8369/8369`;
- DevUi build: passed;
- `git diff --check`: passed.

## Non-Closure

This evidence proves one battlefield movement-trigger power modifier has moved to BehaviorSpec-driven routing. It does not prove the complete B4 battlefield-effect family, all trigger timing windows, all movement / control-zone edge cases, all card-effect families, frontend smoke or READY.


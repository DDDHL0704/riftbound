# Plan B / B4 Battlefield Trigger Spec Audit

Date: 2026-06-25

Status: focused B4 battlefield moved-unit trigger spec slice accepted; project remains **NOT READY**.

## Scope

This slice moves one implemented battlefield trigger away from engine card-number branching:

- `OGN·277/298` / 后巷酒吧 official text: `每当一名单位从此处向别处移动时，让其本回合内{{S}}+1。`
- `RuleTextParser` now parses that text as `TriggerSpec` with:
  - `Kind = BATTLEFIELD_UNIT_MOVED_AWAY_POWER_MODIFIER`
  - `Timing = BATTLEFIELD_UNIT_MOVED_AWAY`
  - `TargetScope = MOVED_UNIT`
  - `PowerDelta = 1`
  - `Duration = UNTIL_END_OF_TURN`
- `CoreRuleEngine.ApplyBattlefieldMovedUnitPowerPlusOne` now finds eligible battlefield sources through `BattlefieldTriggerSpecRules.TryGetBattlefieldMovedUnitPowerModifierTrigger(...)` and reads the power delta from `BehaviorSpec.Triggers`.
- `MatchSession` battlefield-object recognition now uses the same trigger-spec query instead of the old `BattlefieldMovedUnitPowerPlusOneCardNo` constant.

## Non-Closure

This is a narrow B4 cleanup slice. It does not close all battlefield trigger families, same-turn movement policy, complete battlefield lifecycle, conquest triggers, frontend/browser smoke, full official coverage or READY.

## Validation

- focused behavior-spec/source guard/runtime/GameHub representative: passed `5/5`;
- adjacent BattlefieldMoved / BattlefieldMovePower / MoveUnit / BoardTaskQueue / FullGame / GameHub: passed `326/326`;
- MatchRecovery: passed `1989/1989`;
- backend full conformance: passed `8369/8369`;
- DevUi build: passed after adding `/opt/homebrew/bin` to PATH for local `npm`;
- `git diff --check`: passed.


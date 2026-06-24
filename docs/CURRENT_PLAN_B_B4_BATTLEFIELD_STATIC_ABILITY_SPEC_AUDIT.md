# Plan B / B4 Battlefield Static Ability Spec Audit

Date: 2026-06-25

Status: focused B4 battlefield static ability spec slice accepted; project remains **NOT READY**.

## Scope

This slice moves two implemented battlefield static restrictions away from engine card-number branching:

- `OGN·295/298` official text: `单位无法从此处移动到基地。`
- `SFD·216/221` official text: `单位无法被打出到此处。`
- `RuleTextParser` now parses those texts as `StaticAbilitySpec` with:
  - `Kind = BATTLEFIELD_PREVENT_MOVE_TO_BASE`
  - `Kind = BATTLEFIELD_PREVENT_UNIT_PLAY`
- `CoreRuleEngine` move and play rejection paths now find eligible battlefield sources through `BattlefieldStaticAbilitySpecRules`.
- `MatchSession` prompt filtering and battlefield-object recognition use the same static ability spec queries instead of the old `BattlefieldPreventMoveToBaseCardNo` / `BattlefieldPreventUnitPlayCardNo` constants.

## Non-Closure

This is a narrow B4 cleanup slice. It does not close all battlefield static abilities, complete battlefield lifecycle, complete movement / control-zone edge cases, frontend/browser smoke, full official coverage or READY.

## Validation

- focused behavior-spec/source guard/runtime/GameHub representative: passed `9/9`;
- catalog surface follow-up: passed `3/3`;
- adjacent BattlefieldStatic / MoveUnit / PlayCard / GameHub / BoardTaskQueue / FullGame: passed `610/610`;
- MatchRecovery: passed `1989/1989`;
- backend full conformance: passed `8371/8371`.

# Plan B / B4 Battlefield Static Ability Spec Audit

Date: 2026-06-25

Status: focused B4 battlefield static ability spec slices accepted; project remains **NOT READY**.

## Scope

These slices move implemented battlefield static abilities away from engine card-number branching:

- `OGN·295/298` official text: `单位无法从此处移动到基地。`
- `SFD·216/221` official text: `单位无法被打出到此处。`
- `SFD·211/221` official text: `如果此战场受你控制，则友方{{回响}}的费用减少{{1}}。`
- `SFD·213/221` official text: `如果此战场受你控制，则每回合打出的第一件友方装备的费用减少{{1}}，不包括指示物。`
- `RuleTextParser` now parses those texts as `StaticAbilitySpec` with:
  - `Kind = BATTLEFIELD_PREVENT_MOVE_TO_BASE`
  - `Kind = BATTLEFIELD_PREVENT_UNIT_PLAY`
  - `Kind = BATTLEFIELD_ECHO_COST_REDUCTION`, `Amount = 1`
  - `Kind = BATTLEFIELD_EQUIPMENT_COST_REDUCTION`, `Amount = 1`
- `CoreRuleEngine` move and play rejection paths now find eligible battlefield sources through `BattlefieldStaticAbilitySpecRules`.
- `CoreRuleEngine` Echo optional-cost planning now reads `BATTLEFIELD_ECHO_COST_REDUCTION` from `BehaviorSpec.StaticAbilities` instead of the old `BattlefieldEchoCostReductionCardNo` branch.
- `CoreRuleEngine` equipment play cost planning now reads `BATTLEFIELD_EQUIPMENT_COST_REDUCTION` from `BehaviorSpec.StaticAbilities` instead of the old `BattlefieldEquipmentCostReductionCardNo` branch.
- `MatchSession` prompt filtering, Echo/equipment cost metadata, and battlefield-object recognition use the same static ability spec queries instead of the old `BattlefieldPreventMoveToBaseCardNo` / `BattlefieldPreventUnitPlayCardNo` / `BattlefieldEchoCostReductionCardNo` / `BattlefieldEquipmentCostReductionCardNo` constants.

## Non-Closure

This is a narrow B4 cleanup slice. It does not close all battlefield static abilities, complete battlefield lifecycle, complete movement / control-zone edge cases, frontend/browser smoke, full official coverage or READY.

## Validation

- latest focused behavior-spec/source guard/static equipment runtime representative: passed `6/6`;
- latest adjacent BattlefieldStatic / EquipmentCostReduction / PlayCard / PaymentEngine / GameHub / BoardTaskQueue / FullGame: passed `1317/1317`;
- MatchRecovery: passed `1989/1989`;
- backend full conformance: passed `8371/8371`.

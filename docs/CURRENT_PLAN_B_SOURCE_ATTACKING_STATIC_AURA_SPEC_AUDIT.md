# Plan B / Source Attacking Static Aura Spec Audit

Date: 2026-06-25

Status: focused source-attacking static-aura slice accepted; project remains **NOT READY**.

## Scope

This slice moves the implemented Scarlet Pigeon multi-attacker combat-power representative from a Core card-number branch to `BehaviorSpec.StaticAuras`:

- Official catalog source: `data/official/card-catalog.zh-CN.json` has `UNL-154/219` 猩红飞鸽 text `如果我和另一名单位一起进攻一处战场，则我获得{{S}}+2。`
- `StaticAuraKinds.SourceAttackingWithAnotherUnitPower` models source-only combat power while the source is attacking with the required attacker count.
- `StaticAuraSpec.RequiredAttackingUnitCount` models the "with another unit" threshold without hard-coding this card number in Core.
- `RuleTextParsers.StaticAuraParser` now parses the official text into `Kind = SOURCE_ATTACKING_WITH_ANOTHER_UNIT_POWER`, `Layer = STATIC_AURA`, `TargetScope = SOURCE_OBJECT`, `ParticipantScope = ATTACKING_BATTLEFIELD_PUBLIC_UNITS`, `PowerDeltaPerParticipant = 2`, and `RequiredAttackingUnitCount = 2`.
- Current runtime routing is superseded by the B1 source battle-state scope router: `CoreRuleEngine.ResolveSourceBattleStatePowerStaticAuraBonus(...)` enumerates `StaticAuraSpecRules.GetStaticAuras(...)`, identifies this shape with `StaticAuraSpecRules.IsSourceAttackingWithAnotherUnitPowerStaticAura(...)`, and reads the combat threshold plus power delta from `StaticAuraSpec`.
- The old Core `ScarletPigeonCardNo` and `IsScarletPigeonCardNo(...)` branch is removed.

## Runtime Effect

- Scarlet Pigeon still gains `staticPowerBonus = 2` only while it is an attacking unit in a battle with at least two attackers.
- The combat damage payload and lethal-damage threshold behavior remain unchanged for the representative test path.
- The source must still be a public unit object; hidden / non-unit objects do not receive the bonus.
- DevUi runtime behavior is unchanged; catalog typing now includes `staticAuras[].requiredAttackingUnitCount`.

## Non-Goals

- This does not implement complete combat continuous-effect projection for every attack-condition text.
- This does not close complete battle / spell-duel timing, assignment prompts, or simultaneous trigger ordering.
- This does not migrate Dune Drake, Waterbender, or remaining combat-power helpers.
- This does not close B0 full-game readiness or project READY.

## Validation

- Focused static-aura parse / source guard / Scarlet Pigeon representative: `3/3` passing.
- Adjacent static-aura / Scarlet Pigeon / Wise Elder / Ornn / Dune Drake / Waterbender / combat damage / declare battle / full-game representatives: `411/411` passing.
- `MatchRecovery`: `1989/1989` passing.
- Backend full conformance: `8531/8531` passing.
- DevUi build: passing.
- Source-helper count for `private static bool Is*CardNo(...)`: `32` total / `29` in `CoreRuleEngine`.

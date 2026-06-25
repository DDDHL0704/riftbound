# Plan B / Source Attacking Ready Enemy Static Aura Spec Audit

Date: 2026-06-25

Status: focused source-attacking-ready-enemy static-aura slice accepted; project remains **NOT READY**.

## Scope

This slice moves the implemented Dune Drake ready-enemy combat-power representative from a Core card-number branch to `BehaviorSpec.StaticAuras`:

- Official catalog source: `data/official/card-catalog.zh-CN.json` has `OGN·131/298` 沙丘亚龙 text `当我进攻时，如果此处有处于活跃状态的敌方单位，则让我{{S}}+2。`
- `StaticAuraKinds.SourceAttackingReadyEnemyUnitPower` models source-only combat power while the source is attacking a battlefield that has a ready enemy unit.
- `StaticAuraSpec.RequiredReadyEnemyUnitCount` models the ready-enemy threshold without hard-coding this card number in Core.
- `RuleTextParsers.StaticAuraParser` now parses the official text into `Kind = SOURCE_ATTACKING_READY_ENEMY_UNIT_POWER`, `Layer = STATIC_AURA`, `TargetScope = SOURCE_OBJECT`, `ParticipantScope = READY_ENEMY_BATTLEFIELD_PUBLIC_UNITS`, `PowerDeltaPerParticipant = 2`, and `RequiredReadyEnemyUnitCount = 1`.
- `CoreRuleEngine.ResolveSourceAttackingReadyEnemyUnitPowerBonus(...)` now checks `StaticAuraSpecRules.TryGetSourceAttackingReadyEnemyUnitPowerAura(...)` and reads the ready-enemy threshold plus power delta from `StaticAuraSpec`.
- The old Core `DuneDrakeCardNo` branch is removed.

## Runtime Effect

- Dune Drake still gains `staticPowerBonus = 2` only while it is an attacking unit and the battle has at least one ready enemy defender.
- Dune Drake does not gain this bonus while defending.
- The combat damage payload and lethal-damage threshold behavior remain unchanged for the representative test path.
- The source must still be a public, face-up unit object; hidden / non-unit objects do not receive the bonus.
- DevUi runtime behavior is unchanged; catalog typing now includes `staticAuras[].requiredReadyEnemyUnitCount`.

## Non-Goals

- This does not implement complete combat continuous-effect projection for every ready-enemy condition text.
- This does not close complete battle / spell-duel timing, assignment prompts, or simultaneous trigger ordering.
- This does not migrate remaining combat-power helpers outside this representative family.
- This does not close B0 full-game readiness or project READY.

## Validation

- Focused static-aura parse / source guard / Dune Drake representatives: `4/4` passing.
- Adjacent static-aura / Dune Drake / Waterbender / Scarlet Pigeon / Wise Elder / Ornn / combat damage / declare battle / full-game representatives: `411/411` passing.
- `MatchRecovery`: `1989/1989` passing.
- Backend full conformance: `8531/8531` passing.
- DevUi build: passing.
- Source-helper count for `private static bool Is*CardNo(...)`: `32` total / `29` in `CoreRuleEngine`.

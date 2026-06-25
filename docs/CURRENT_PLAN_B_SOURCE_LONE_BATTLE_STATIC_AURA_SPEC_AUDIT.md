# Plan B / Source Lone Battle Static Aura Spec Audit

Date: 2026-06-25

Status: focused source-lone-battle static-aura slice accepted; project remains **NOT READY**.

## Scope

This slice moves the implemented Waterbender lone-attacker / lone-defender combat-power representative from a Core card-number branch to `BehaviorSpec.StaticAuras`:

- Official catalog source: `data/official/card-catalog.zh-CN.json` has `OGN·055/298` 驭水者 text `如果我独自进攻或防守一处战场，则我获得 {{S}}+2。`
- `StaticAuraKinds.SourceLoneBattlePower` models source-only combat power while the source is attacking or defending alone.
- `StaticAuraSpec.RequiredAttackingUnitCount` and `RequiredDefendingUnitCount` model the lone-battle thresholds without hard-coding this card number in Core.
- `RuleTextParsers.StaticAuraParser` now parses the official text into `Kind = SOURCE_LONE_BATTLE_POWER`, `Layer = STATIC_AURA`, `TargetScope = SOURCE_OBJECT`, `ParticipantScope = BATTLEFIELD_PUBLIC_UNITS`, `PowerDeltaPerParticipant = 2`, `RequiredAttackingUnitCount = 1`, and `RequiredDefendingUnitCount = 1`.
- `CoreRuleEngine.ResolveWaterbenderLoneBattlePowerBonus(...)` now checks `StaticAuraSpecRules.TryGetSourceLoneBattlePowerAura(...)` and reads the combat thresholds plus power delta from `StaticAuraSpec`.
- The old Core `WaterbenderCardNo` direct card-number branch is removed.

## Runtime Effect

- Waterbender still gains `staticPowerBonus = 2` when it attacks alone.
- Waterbender still gains `staticPowerBonus = 2` when it defends alone.
- Waterbender still skips the bonus when it attacks with another unit.
- The combat damage payload and lethal-damage threshold behavior remain unchanged for the representative test paths.
- DevUi runtime behavior is unchanged; catalog typing now includes `staticAuras[].requiredDefendingUnitCount`.

## Non-Goals

- This does not implement complete combat conditional power for every attack / defense condition.
- This does not close complete battle / spell-duel timing, assignment prompts, or simultaneous trigger ordering.
- This does not migrate Dune Drake or remaining combat-power helpers.
- This does not close B0 full-game readiness or project READY.

## Validation

- Focused static-aura parse / source guard / Waterbender representatives: `5/5` passing.
- Adjacent static-aura / Waterbender / Scarlet Pigeon / Dune Drake / Wise Elder / Ornn / combat damage / declare battle / full-game representatives: `411/411` passing.
- `MatchRecovery`: `1989/1989` passing.
- Backend full conformance: `8531/8531` passing.
- DevUi build: passing.
- Source-helper count for `private static bool Is*CardNo(...)`: `32` total / `29` in `CoreRuleEngine`.

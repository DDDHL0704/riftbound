# Plan B / Unit Destroyed Trigger Spec Audit

Date: 2026-06-25

Status: focused friendly-destroyed gain-experience, power-until-end, and first-friendly-destroyed draw TriggerSpec slices accepted; project remains **NOT READY**.

## Scope

This slice moves the implemented friendly-destroyed experience trigger away from engine card-number branching:

- `UNL-129/219` 凶残颚鱼 official text from `data/official/card-catalog.zh-CN.json`: `当另一名友方单位被摧毁时，获得1经验。`
- `RuleTextParser` now parses that text as `TriggerSpec` with:
  - `Kind = SAVAGE_JAWFISH_FRIENDLY_DESTROYED_EXPERIENCE_1`
  - `Timing = UNIT_DESTROYED`
  - `TargetScope = OTHER_FRIENDLY_DESTROYED_UNIT`
  - `ExperienceCount = 1`
- `TriggerKinds.UnitFriendlyDestroyedGainExperience` keeps the existing effect-kind value for stack / replay compatibility while exposing a generic engine name.
- `CoreRuleEngine.BuildSavageJawfishFriendlyDestroyedTriggerQueueItems` now identifies eligible source units through `UnitDestroyedTriggerSpecRules.TryGetFriendlyDestroyedGainExperienceTrigger(...)` and emits the effect id from `BehaviorSpec.Triggers`.
- `CoreRuleEngine.ResolveSavageJawfishFriendlyDestroyedExperienceStackItem` now validates the source through the same TriggerSpec path and reads the experience amount from `TriggerSpec.ExperienceCount`.
- The old `SavageJawfishCardNo` / `IsSavageJawfishCardNo` branch is removed from `CoreRuleEngine`.

This slice also moves the implemented friendly-destroyed power trigger away from engine card-number branching:

- `UNL-068/219` 幽魂半人马 official text from `data/official/card-catalog.zh-CN.json`: `当另一名友方单位被摧毁时，让我本回合内{{S}}+2。`
- `RuleTextParser` now parses that text as `TriggerSpec` with:
  - `Kind = GHOSTLY_CENTAUR_FRIENDLY_DESTROYED_POWER_2`
  - `Timing = UNIT_DESTROYED`
  - `TargetScope = OTHER_FRIENDLY_DESTROYED_UNIT`
  - `PowerDelta = 2`
  - `Duration = UNTIL_END_OF_TURN`
- `TriggerKinds.UnitFriendlyDestroyedPowerUntilEndOfTurn` keeps the existing effect-kind value for stack / replay compatibility while exposing a generic engine name.
- `CoreRuleEngine.BuildGhostlyCentaurFriendlyDestroyedTriggerQueueItems` now identifies eligible source units through the shared `UnitDestroyedTriggerSpecRules` path and emits the effect id from `BehaviorSpec.Triggers`.
- `CoreRuleEngine.ResolveGhostlyCentaurFriendlyDestroyedPowerStackItem` now validates the source through the same TriggerSpec path and reads the power amount from `TriggerSpec.PowerDelta`.
- The old `GhostlyCentaurCardNo` direct card-number branch is removed from `CoreRuleEngine`.

This slice also moves the implemented first-friendly-destroyed draw trigger away from engine card-number branching:

- `OGN·118/298` 残响之魂 official text from `data/official/card-catalog.zh-CN.json`: `每回合首次：当你的友方单位被摧毁时，抽一张牌。`
- `RuleTextParser` now parses that text as `TriggerSpec` with:
  - `Kind = RESONANT_SOUL_FIRST_FRIENDLY_DESTROYED_DRAW_1`
  - `Timing = UNIT_DESTROYED`
  - `TargetScope = OTHER_FRIENDLY_DESTROYED_UNIT`
  - `DrawCount = 1`
  - `OncePerTurn = true`
- `TriggerKinds.UnitFirstFriendlyDestroyedDrawOne` keeps the existing effect-kind value for stack / replay compatibility while exposing a generic engine name.
- `CoreRuleEngine.BuildResonantSoulFirstFriendlyDestroyedTriggerQueueItems` now identifies eligible source units through the shared `UnitDestroyedTriggerSpecRules` path and emits the effect id from `BehaviorSpec.Triggers`, while preserving the existing destroyed-owner once-per-turn guard.
- `CoreRuleEngine` immediate single-trigger resolution and stack resolution now read the draw amount from `TriggerSpec.DrawCount`.
- The old `ResonantSoulCardNo` direct card-number branch and `ResonantSoulFirstFriendlyDestroyedDrawEffectKind` Core constant are removed from `CoreRuleEngine`.
- Current source-helper count for `private static bool Is*CardNo(...)` is `38` total / `35` in `CoreRuleEngine`; this count is unchanged by the Ghostly Centaur and Resonant Soul slices because the old Core paths used direct card-number comparisons rather than `Is*CardNo(...)` helpers.

## Non-Goals

- This keeps the existing `SAVAGE_JAWFISH_FRIENDLY_DESTROYED_EXPERIENCE_1`, `GHOSTLY_CENTAUR_FRIENDLY_DESTROYED_POWER_2`, and `RESONANT_SOUL_FIRST_FRIENDLY_DESTROYED_DRAW_1` stack effect strings for compatibility with recovery and replay validators.
- This does not migrate Viktor or other destroyed-trigger families to TriggerSpec.
- This does not rename existing recovery validator constants or old audit file names that describe the legacy effect id.
- This does not close complete natural destroyed-trigger prompt breadth, B0 full-game readiness, or project READY.

## Follow-Up

- Migrate the remaining friendly-destroyed / destroyed-unit trigger families one effect kind at a time into `UnitDestroyedTriggerSpecRules`.
- Keep source-guard tests for each migrated family so new cards can be enabled by BehaviorSpec data rather than `Is*CardNo(...)` allow-lists.

# Plan B / Unit Destroyed Trigger Spec Audit

Date: 2026-06-25

Status: focused friendly-destroyed gain-experience TriggerSpec slice accepted; project remains **NOT READY**.

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
- Current source-helper count for `private static bool Is*CardNo(...)` is `38` total / `35` in `CoreRuleEngine`.

## Non-Goals

- This keeps the existing `SAVAGE_JAWFISH_FRIENDLY_DESTROYED_EXPERIENCE_1` stack effect string for compatibility with recovery and replay validators.
- This does not migrate Ghostly Centaur, Resonant Soul, Viktor, or other destroyed-trigger families to TriggerSpec.
- This does not rename existing recovery validator constants or old audit file names that describe the legacy effect id.
- This does not close complete natural destroyed-trigger prompt breadth, B0 full-game readiness, or project READY.

## Follow-Up

- Migrate the remaining friendly-destroyed / destroyed-unit trigger families one effect kind at a time into `UnitDestroyedTriggerSpecRules`.
- Keep source-guard tests for each migrated family so new cards can be enabled by BehaviorSpec data rather than `Is*CardNo(...)` allow-lists.

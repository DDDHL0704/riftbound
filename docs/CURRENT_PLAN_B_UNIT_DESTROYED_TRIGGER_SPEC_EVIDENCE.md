# Plan B / Unit Destroyed Trigger Spec Evidence

Date: 2026-06-25

Project status: **NOT READY**.

## Rule Sources

- `data/official/card-catalog.zh-CN.json`: `UNL-129/219` 凶残颚鱼 has official text `当另一名友方单位被摧毁时，获得1经验。`
- `docs/rules-authority-and-audit.md` and `docs/rules-evidence-index.md`: official catalog text remains the local rule authority input for this representative slice.

## BehaviorSpec Evidence

- `BehaviorSpecCatalogParsesUnitFriendlyDestroyedGainExperienceTrigger` verifies that 凶残颚鱼's official text parses to `TriggerSpec.Kind = SAVAGE_JAWFISH_FRIENDLY_DESTROYED_EXPERIENCE_1`, `Timing = UNIT_DESTROYED`, `TargetScope = OTHER_FRIENDLY_DESTROYED_UNIT`, and `ExperienceCount = 1`.
- `UnitFriendlyDestroyedGainExperienceTriggerDoesNotUseCardNumberAllowList` verifies that `CoreRuleEngine` no longer contains `SavageJawfishCardNo` / `IsSavageJawfishCardNo`.

## Runtime Evidence

- `RealSavageJawfishFriendlyDestroyedTriggersEnterApnapOrderWindowAndGainExperienceThroughStack` verifies the real stack-destruction trigger route still queues APNAP ordered triggers and resolves experience through the stack.
- `P79SavageJawfishGainsExperienceWhenAnotherFriendlyUnitDestroyed` keeps the representative fixture route green.
- `StateBasedCleanupSavageJawfishTriggersOrderAndGainExperienceThroughStack`, `StateBasedCleanupHiddenSavageJawfishDoNotEnqueueTriggers`, and `StateBasedCleanupSavageJawfishSkipsWhenSourceAlsoDies` are covered by the adjacent `SavageJawfish|FriendlyDestroyed|StateBasedCleanup` filter and keep cleanup-trigger behavior, hidden-source filtering, and same-removal skip behavior green.
- Existing recovery validators for `SAVAGE_JAWFISH_FRIENDLY_DESTROYED_EXPERIENCE_1` still pass because the stack effect value is unchanged.

## Validation

- Focused 凶残颚鱼 behavior-spec/source-guard/runtime representatives: `4/4` passing.
- Adjacent `SavageJawfish|FriendlyDestroyed|StateBasedCleanup` representatives: `111/111` passing.
- `MatchRecovery`: `1989/1989` passing.
- Backend full conformance: `8500/8500` passing.
- No DevUi source or catalog TypeScript shape changed in this slice, so DevUi build was not rerun.

## Residual Risk

- The effect-kind value remains the legacy `SAVAGE_JAWFISH_FRIENDLY_DESTROYED_EXPERIENCE_1`; only source recognition, TriggerSpec parsing, and experience amount routing moved to data-driven engine paths.
- Other destroyed-trigger families still have card-number based source recognition and remain follow-up work.
- Complete destroyed-trigger prompt breadth and hidden-information edge matrices remain open.

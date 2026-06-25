# Plan B / Unit Destroyed Trigger Spec Evidence

Date: 2026-06-25

Project status: **NOT READY**.

## Rule Sources

- `data/official/card-catalog.zh-CN.json`: `UNL-129/219` 凶残颚鱼 has official text `当另一名友方单位被摧毁时，获得1经验。`
- `data/official/card-catalog.zh-CN.json`: `UNL-068/219` 幽魂半人马 has official text `当另一名友方单位被摧毁时，让我本回合内{{S}}+2。`
- `docs/rules-authority-and-audit.md` and `docs/rules-evidence-index.md`: official catalog text remains the local rule authority input for this representative slice.

## BehaviorSpec Evidence

- `BehaviorSpecCatalogParsesUnitFriendlyDestroyedGainExperienceTrigger` verifies that 凶残颚鱼's official text parses to `TriggerSpec.Kind = SAVAGE_JAWFISH_FRIENDLY_DESTROYED_EXPERIENCE_1`, `Timing = UNIT_DESTROYED`, `TargetScope = OTHER_FRIENDLY_DESTROYED_UNIT`, and `ExperienceCount = 1`.
- `UnitFriendlyDestroyedGainExperienceTriggerDoesNotUseCardNumberAllowList` verifies that `CoreRuleEngine` no longer contains `SavageJawfishCardNo` / `IsSavageJawfishCardNo`.
- `BehaviorSpecCatalogParsesUnitFriendlyDestroyedPowerUntilEndTrigger` verifies that 幽魂半人马's official text parses to `TriggerSpec.Kind = GHOSTLY_CENTAUR_FRIENDLY_DESTROYED_POWER_2`, `Timing = UNIT_DESTROYED`, `TargetScope = OTHER_FRIENDLY_DESTROYED_UNIT`, `Duration = UNTIL_END_OF_TURN`, and `PowerDelta = 2`.
- `UnitFriendlyDestroyedPowerUntilEndTriggerDoesNotUseCardNumberAllowList` verifies that `CoreRuleEngine` no longer contains `GhostlyCentaurCardNo`.

## Runtime Evidence

- `RealSavageJawfishFriendlyDestroyedTriggersEnterApnapOrderWindowAndGainExperienceThroughStack` verifies the real stack-destruction trigger route still queues APNAP ordered triggers and resolves experience through the stack.
- `P79SavageJawfishGainsExperienceWhenAnotherFriendlyUnitDestroyed` keeps the representative fixture route green.
- `StateBasedCleanupSavageJawfishTriggersOrderAndGainExperienceThroughStack`, `StateBasedCleanupHiddenSavageJawfishDoNotEnqueueTriggers`, and `StateBasedCleanupSavageJawfishSkipsWhenSourceAlsoDies` are covered by the adjacent `SavageJawfish|FriendlyDestroyed|StateBasedCleanup` filter and keep cleanup-trigger behavior, hidden-source filtering, and same-removal skip behavior green.
- `RealGhostlyCentaurFriendlyDestroyedTriggersEnterApnapOrderWindowAndGainPowerThroughStack` verifies the real stack-destruction trigger route still queues APNAP ordered triggers and resolves until-end-of-turn power through the stack.
- `P79GhostlyCentaurGainsTemporaryPowerWhenAnotherFriendlyUnitDestroyed` keeps the representative fixture route green.
- `StateBasedCleanupGhostlyCentaursTriggerOrderAndGainPowerThroughStack`, `StateBasedCleanupHiddenGhostlyCentaursDoNotEnqueueTriggers`, and `StateBasedCleanupGhostlyCentaurSkipsWhenSourceAlsoDies` are covered by the adjacent `GhostlyCentaur|FriendlyDestroyed|StateBasedCleanup` filter and keep cleanup-trigger behavior, hidden-source filtering, and same-removal skip behavior green.
- Existing recovery validators for `SAVAGE_JAWFISH_FRIENDLY_DESTROYED_EXPERIENCE_1` and `GHOSTLY_CENTAUR_FRIENDLY_DESTROYED_POWER_2` still pass because the stack effect values are unchanged.

## Validation

- Focused 凶残颚鱼 behavior-spec/source-guard/runtime representatives: `4/4` passing.
- Adjacent `SavageJawfish|FriendlyDestroyed|StateBasedCleanup` representatives: `111/111` passing.
- Focused 幽魂半人马 behavior-spec/source-guard/runtime representatives: `4/4` passing.
- Adjacent `GhostlyCentaur|FriendlyDestroyed|StateBasedCleanup` representatives: `114/114` passing.
- `MatchRecovery`: `1989/1989` passing.
- Backend full conformance: `8502/8502` passing.
- No DevUi source or catalog TypeScript shape changed in this slice, so DevUi build was not rerun.

## Residual Risk

- The effect-kind values remain the legacy `SAVAGE_JAWFISH_FRIENDLY_DESTROYED_EXPERIENCE_1` and `GHOSTLY_CENTAUR_FRIENDLY_DESTROYED_POWER_2`; only source recognition, TriggerSpec parsing, and effect amount routing moved to data-driven engine paths.
- Other destroyed-trigger families still have card-number based source recognition and remain follow-up work.
- Complete destroyed-trigger prompt breadth and hidden-information edge matrices remain open.

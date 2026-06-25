# Plan B / Unit Destroyed Trigger Spec Evidence

Date: 2026-06-25

Project status: **NOT READY**.

## Rule Sources

- `data/official/card-catalog.zh-CN.json`: `UNL-129/219` 凶残颚鱼 has official text `当另一名友方单位被摧毁时，获得1经验。`
- `data/official/card-catalog.zh-CN.json`: `UNL-068/219` 幽魂半人马 has official text `当另一名友方单位被摧毁时，让我本回合内{{S}}+2。`
- `data/official/card-catalog.zh-CN.json`: `OGN·118/298` 残响之魂 has official text `每回合首次：当你的友方单位被摧毁时，抽一张牌。`
- `data/official/card-catalog.zh-CN.json`: `ARC-006/006`, `OGN·246/298`, and `OGN·246a/298` 维克托 have official text `如果我在场上，则每当你的另一名非“随从”单位被摧毁时，打出一名1{{S}}的“随从”到你的基地。`
- `data/official/card-catalog.zh-CN.json`: `SFD·036/221` and `UNL-221/219` 哀哀魄罗 have official text `{{绝念}} — 当我被摧毁时，如果此处没有其他友方单位，则抽一张牌。`
- `data/official/card-catalog.zh-CN.json`: `UNL-156/219` 忠忠魄罗 has official text `{{绝念>}} 如果我被摧毁时未处于落单状态，则抽一张牌。`
- `docs/rules-authority-and-audit.md` and `docs/rules-evidence-index.md`: official catalog text remains the local rule authority input for this representative slice.

## BehaviorSpec Evidence

- `BehaviorSpecCatalogParsesUnitFriendlyDestroyedGainExperienceTrigger` verifies that 凶残颚鱼's official text parses to `TriggerSpec.Kind = SAVAGE_JAWFISH_FRIENDLY_DESTROYED_EXPERIENCE_1`, `Timing = UNIT_DESTROYED`, `TargetScope = OTHER_FRIENDLY_DESTROYED_UNIT`, and `ExperienceCount = 1`.
- `UnitFriendlyDestroyedGainExperienceTriggerDoesNotUseCardNumberAllowList` verifies that `CoreRuleEngine` no longer contains `SavageJawfishCardNo` / `IsSavageJawfishCardNo`.
- `BehaviorSpecCatalogParsesUnitFriendlyDestroyedPowerUntilEndTrigger` verifies that 幽魂半人马's official text parses to `TriggerSpec.Kind = GHOSTLY_CENTAUR_FRIENDLY_DESTROYED_POWER_2`, `Timing = UNIT_DESTROYED`, `TargetScope = OTHER_FRIENDLY_DESTROYED_UNIT`, `Duration = UNTIL_END_OF_TURN`, and `PowerDelta = 2`.
- `UnitFriendlyDestroyedPowerUntilEndTriggerDoesNotUseCardNumberAllowList` verifies that `CoreRuleEngine` no longer contains `GhostlyCentaurCardNo`.
- `BehaviorSpecCatalogParsesUnitFirstFriendlyDestroyedDrawTrigger` verifies that 残响之魂's official text parses to `TriggerSpec.Kind = RESONANT_SOUL_FIRST_FRIENDLY_DESTROYED_DRAW_1`, `Timing = UNIT_DESTROYED`, `TargetScope = OTHER_FRIENDLY_DESTROYED_UNIT`, `DrawCount = 1`, and `OncePerTurn = true`.
- `UnitFirstFriendlyDestroyedDrawTriggerDoesNotUseCardNumberAllowList` verifies that `CoreRuleEngine` no longer contains `ResonantSoulCardNo`.
- `BehaviorSpecCatalogParsesUnitDestroyedNonMinionCreateMinionTrigger` verifies that all three 维克托 destroyed-trigger prints parse to `TriggerSpec.Kind = VIKTOR_DESTROYED_NON_MINION_CREATE_MINION`, `Timing = UNIT_DESTROYED`, `TargetScope = OTHER_FRIENDLY_DESTROYED_UNIT`, `ExcludesTokens = true`, `CreatedTokenCount = 1`, `CreatedTokenName = 随从`, `CreatedTokenPower = 1`, and `CreatedTokenDestination = OWNER_BASE`.
- `UnitDestroyedNonMinionCreateMinionTriggerDoesNotUseCardNumberAllowList` verifies that `CoreRuleEngine` no longer contains `ViktorDestroyedNonMinionArcCardNo`, `ViktorDestroyedNonMinionOgnCardNo`, `ViktorDestroyedNonMinionOgnAltACardNo`, or `IsViktorDestroyedNonMinionCardNo`.
- `BehaviorSpecCatalogParsesUnitLastBreathDrawIfAloneTrigger` verifies that both 哀哀魄罗 prints parse to `TriggerSpec.Kind = SAD_PORO_LAST_BREATH_DRAW_1`, `Timing = UNIT_DESTROYED`, `TargetScope = SOURCE_UNIT`, `DrawCount = 1`, and `RequiresNoOtherFriendlyUnitAtSamePosition = true`.
- `UnitLastBreathDrawIfAloneTriggerDoesNotUseCardNumberAllowList` verifies that `CoreRuleEngine` no longer contains `SadPoroOriginalCardNo`, `SadPoroUnleashedCardNo`, or `IsSadPoroCardNo`.
- `BehaviorSpecCatalogParsesUnitLastBreathDrawIfNotAloneTrigger` verifies that 忠忠魄罗 parses to `TriggerSpec.Kind = LOYAL_PORO_LAST_BREATH_DRAW_1`, `Timing = UNIT_DESTROYED`, `TargetScope = SOURCE_UNIT`, `DrawCount = 1`, and `RequiresOtherFriendlyUnitAtSamePosition = true`.
- `UnitLastBreathDrawIfNotAloneTriggerDoesNotUseCardNumberAllowList` verifies that `CoreRuleEngine` no longer contains `LoyalPoroCardNo` or `LoyalPoroLastBreathDrawEffectKind`.

## Runtime Evidence

- `RealSavageJawfishFriendlyDestroyedTriggersEnterApnapOrderWindowAndGainExperienceThroughStack` verifies the real stack-destruction trigger route still queues APNAP ordered triggers and resolves experience through the stack.
- `P79SavageJawfishGainsExperienceWhenAnotherFriendlyUnitDestroyed` keeps the representative fixture route green.
- `StateBasedCleanupSavageJawfishTriggersOrderAndGainExperienceThroughStack`, `StateBasedCleanupHiddenSavageJawfishDoNotEnqueueTriggers`, and `StateBasedCleanupSavageJawfishSkipsWhenSourceAlsoDies` are covered by the adjacent `SavageJawfish|FriendlyDestroyed|StateBasedCleanup` filter and keep cleanup-trigger behavior, hidden-source filtering, and same-removal skip behavior green.
- `RealGhostlyCentaurFriendlyDestroyedTriggersEnterApnapOrderWindowAndGainPowerThroughStack` verifies the real stack-destruction trigger route still queues APNAP ordered triggers and resolves until-end-of-turn power through the stack.
- `P79GhostlyCentaurGainsTemporaryPowerWhenAnotherFriendlyUnitDestroyed` keeps the representative fixture route green.
- `StateBasedCleanupGhostlyCentaursTriggerOrderAndGainPowerThroughStack`, `StateBasedCleanupHiddenGhostlyCentaursDoNotEnqueueTriggers`, and `StateBasedCleanupGhostlyCentaurSkipsWhenSourceAlsoDies` are covered by the adjacent `GhostlyCentaur|FriendlyDestroyed|StateBasedCleanup` filter and keep cleanup-trigger behavior, hidden-source filtering, and same-removal skip behavior green.
- `RealResonantSoulFirstFriendlyDestroyedTriggersEnterApnapOrderWindowAndDrawThroughStack` verifies the real stack-destruction trigger route still queues APNAP ordered triggers and resolves draw through the stack while respecting the first-destroyed owner guard.
- `P79ResonantSoulDrawsOnlyForFirstFriendlyUnitDestroyedEachTurn` keeps the representative fixture route green.
- `StateBasedCleanupResonantSoulsTriggerOrderAndDrawThroughStack`, `StateBasedCleanupHiddenResonantSoulsDoNotEnqueueTriggers`, `StateBasedCleanupResonantSoulsSkipWhenOwnersAlreadyDestroyedThisTurn`, and `StateBasedCleanupResonantSoulSkipsWhenSourceAlsoDies` are covered by the adjacent `ResonantSoul|FriendlyDestroyed|StateBasedCleanup` filter and keep cleanup-trigger behavior, hidden-source filtering, first-owner guard, and same-removal skip behavior green.
- `RealViktorDestroyedNonMinionTriggersAutoStackAndCreatesMinionToken` verifies the real stack-destruction trigger route still queues APNAP ordered triggers and resolves the Zaun minion token through the stack.
- `StateBasedCleanupViktorDestroyedNonMinionTriggersAutoStackAndCreatesMinionToken` verifies the same TriggerSpec source route from state-based cleanup.
- The broader `ViktorDestroyedNonMinion|FriendlyDestroyed|StateBasedCleanup` adjacent filter keeps the existing invalid-source, hidden-source, token-exclusion, and shared destroyed-trigger cleanup paths green.
- `StateBasedCleanupSadPorosTriggerOrderAndDrawThroughStack` verifies both Sad Poro prints still enqueue APNAP ordered last-breath draw triggers and resolve through the stack.
- `StateBasedCleanupSadPoroSkipsWhenNotIsolated` verifies the TriggerSpec source route preserves the existing "no other friendly unit at this position" condition.
- `P79SadPoroDrawsWhenDestroyedWhileIsolated` and `P79SadPoroSkipsDrawWhenDestroyedWithAnotherFriendlyUnitAtSameBase` keep the representative fixture route green.
- `StateBasedCleanupLoyalPoroTriggersWhenNotIsolatedAndDrawsThroughStack` verifies 忠忠魄罗 still enqueues APNAP ordered last-breath draw triggers and resolves through the stack when another friendly unit shares the position.
- `StateBasedCleanupLoyalPoroSkipsWhenIsolated` and `StateBasedCleanupLoyalPoroSkipsWhenOnlyOtherFriendlyAlsoDies` verify the TriggerSpec source route preserves the existing non-isolation condition and cleanup-removal exclusion.
- `P79LoyalPoroDrawsWhenDestroyedWithAnotherFriendlyUnitAtSameBase` and `P79LoyalPoroSkipsDrawWhenDestroyedWhileIsolated` keep the representative fixture route green.
- Existing recovery validators for `SAVAGE_JAWFISH_FRIENDLY_DESTROYED_EXPERIENCE_1`, `GHOSTLY_CENTAUR_FRIENDLY_DESTROYED_POWER_2`, `RESONANT_SOUL_FIRST_FRIENDLY_DESTROYED_DRAW_1`, `VIKTOR_DESTROYED_NON_MINION_CREATE_MINION`, `SAD_PORO_LAST_BREATH_DRAW_1`, and `LOYAL_PORO_LAST_BREATH_DRAW_1` still pass because the stack effect values are unchanged.

## Validation

- Focused 凶残颚鱼 behavior-spec/source-guard/runtime representatives: `4/4` passing.
- Adjacent `SavageJawfish|FriendlyDestroyed|StateBasedCleanup` representatives: `111/111` passing.
- Focused 幽魂半人马 behavior-spec/source-guard/runtime representatives: `4/4` passing.
- Adjacent `GhostlyCentaur|FriendlyDestroyed|StateBasedCleanup` representatives: `114/114` passing.
- Focused 残响之魂 behavior-spec/source-guard/runtime representatives: `4/4` passing.
- Adjacent `ResonantSoul|FriendlyDestroyed|StateBasedCleanup` representatives: `116/116` passing.
- Focused 维克托 behavior-spec/source-guard representatives: `4/4` passing.
- Focused 维克托 runtime representatives: `2/2` passing.
- Adjacent `ViktorDestroyedNonMinion|FriendlyDestroyed|StateBasedCleanup` representatives: `131/131` passing.
- Focused 哀哀魄罗 behavior-spec/source-guard representatives: `3/3` passing.
- Adjacent `SadPoro` representatives: `13/13` passing.
- Focused 忠忠魄罗 behavior-spec/source-guard representatives: `2/2` passing.
- Adjacent `LoyalPoro` representatives: `13/13` passing.
- Adjacent `LastBreath|StateBasedCleanup` representatives: `211/211` passing.
- `MatchRecovery`: `1989/1989` passing.
- Backend full conformance: `8513/8513` passing.
- DevUi catalog TypeScript shape was updated for the new TriggerSpec condition fields; `/opt/homebrew/bin/npm --prefix src/Riftbound.DevUi run build` passed.

## Residual Risk

- The effect-kind values remain the legacy `SAVAGE_JAWFISH_FRIENDLY_DESTROYED_EXPERIENCE_1`, `GHOSTLY_CENTAUR_FRIENDLY_DESTROYED_POWER_2`, `RESONANT_SOUL_FIRST_FRIENDLY_DESTROYED_DRAW_1`, `VIKTOR_DESTROYED_NON_MINION_CREATE_MINION`, `SAD_PORO_LAST_BREATH_DRAW_1`, and `LOYAL_PORO_LAST_BREATH_DRAW_1`; only source recognition, TriggerSpec parsing, condition checks, and effect amount / token-count routing moved to data-driven engine paths.
- Other destroyed-trigger families still have card-number based source recognition and remain follow-up work.
- Complete destroyed-trigger prompt breadth and hidden-information edge matrices remain open.

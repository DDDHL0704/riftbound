# Plan B / Unit Destroyed Trigger Spec Evidence

Date: 2026-06-25
Updated: 2026-06-30

Project status: **NOT READY**.

## Rule Sources

- `data/official/card-catalog.zh-CN.json`: `UNL-129/219` 凶残颚鱼 has official text `当另一名友方单位被摧毁时，获得1经验。`
- `data/official/card-catalog.zh-CN.json`: `UNL-068/219` 幽魂半人马 has official text `当另一名友方单位被摧毁时，让我本回合内{{S}}+2。`
- `data/official/card-catalog.zh-CN.json`: `OGN·118/298` 残响之魂 has official text `每回合首次：当你的友方单位被摧毁时，抽一张牌。`
- `data/official/card-catalog.zh-CN.json`: `ARC-006/006`, `OGN·246/298`, and `OGN·246a/298` 维克托 have official text `如果我在场上，则每当你的另一名非“随从”单位被摧毁时，打出一名1{{S}}的“随从”到你的基地。`
- `data/official/card-catalog.zh-CN.json`: `SFD·036/221` and `UNL-221/219` 哀哀魄罗 have official text `{{绝念}} — 当我被摧毁时，如果此处没有其他友方单位，则抽一张牌。`
- `data/official/card-catalog.zh-CN.json`: `UNL-156/219` 忠忠魄罗 has official text `{{绝念>}} 如果我被摧毁时未处于落单状态，则抽一张牌。`
- `data/official/card-catalog.zh-CN.json`: `OGN·096/298` 警觉的哨兵 has official text `{{绝念}}—抽一张牌。`
- `data/official/card-catalog.zh-CN.json`: `OGN·216/298` 侦察飞鹰 has official text `{{绝念}}—召出一枚休眠的符文。`
- `data/official/card-catalog.zh-CN.json`: `UNL-152/219` 黑色玫瑰要员 has official text `{{强攻}}（如果我是进攻方，则{{S}}+1。）\n{{绝念>}} 召出一枚休眠的符文。`
- `data/official/card-catalog.zh-CN.json`: `OGN·239/298` Mechanical Trickster has official text `{{绝念}}—打出三名1{{S}}的“随从”到你的基地。`
- `data/official/card-catalog.zh-CN.json`: `SFD·021/221` Ironclad Vanguard has official text `{{绝念}}—打出两名3{{S}}的“机器人”到你的基地。`
- `data/official/card-catalog.zh-CN.json`: `UNL-153/219` Muddy Dredger has official text `{{绝念>}} 打出一名1{{S}}的“战鹰”到你的基地，它拥有{{法盾}}。`
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
- `BehaviorSpecCatalogParsesUnitLastBreathDrawOneTrigger` verifies that 警觉的哨兵 parses to `TriggerSpec.Kind = WATCHFUL_SENTINEL_LAST_BREATH_DRAW_1`, `Timing = UNIT_DESTROYED`, `TargetScope = SOURCE_UNIT`, and `DrawCount = 1`.
- `UnitLastBreathDrawOneTriggerDoesNotUseCardNumberAllowList` verifies that `CoreRuleEngine` no longer contains `WatchfulSentinelCardNo` or `WatchfulSentinelLastBreathDrawEffectKind`, and that `MatchRecovery` no longer contains `WatchfulSentinelCardNoForRecovery`.
- `BehaviorSpecCatalogParsesUnitLastBreathCallRuneTrigger` verifies that both 侦察飞鹰 and 黑色玫瑰要员 parse to `TriggerSpec.Kind = SCOUTING_WARHAWK_LAST_BREATH_CALL_RUNE_1`, `Timing = UNIT_DESTROYED`, `TargetScope = SOURCE_UNIT`, and `RuneCallCount = 1`.
- `UnitLastBreathCallRuneTriggerDoesNotUseCardNumberAllowList` verifies that `CoreRuleEngine` no longer contains `ScoutingWarhawkCardNo` or `ScoutingWarhawkLastBreathCallRuneEffectKind`.
- `BehaviorSpecCatalogParsesUnitLastBreathCreateBaseUnitTrigger` verifies that Mechanical Trickster, Ironclad Vanguard, and Muddy Dredger parse to `Timing = UNIT_DESTROYED`, `TargetScope = SOURCE_UNIT`, `CreatedTokenCount`, `CreatedTokenName`, `CreatedTokenPower`, and `CreatedTokenDestination = OWNER_BASE`, with Muddy Dredger also carrying `CreatedTokenKeywords = [法盾]`.
- `UnitLastBreathCreateBaseUnitTriggerDoesNotUseCardNumberAllowList` verifies that `CoreRuleEngine` no longer contains the old Mechanical Trickster, Ironclad Vanguard, or Muddy Dredger Core card-number/effect-kind constants or card-specific resolver names.

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
- `RealWatchfulSentinelLastBreathTriggersEnterApnapOrderWindowAndResolveThroughStack` verifies the real stack-destruction trigger route still queues APNAP ordered triggers and resolves draw through the stack.
- `StateBasedCleanupWatchfulSentinelTriggersOrderAndResolveThroughStack` verifies both visible Watchful Sentinel cleanup triggers still enqueue and resolve through the stack.
- `StateBasedCleanupHiddenWatchfulSentinelsDoNotEnqueueTriggers` verifies hidden / standby source filtering remains intact.
- `CoreRuleEngineQueuesWatchfulSentinelLastBreathDrawWhenDestroyed` keeps the representative fixture route green.
- `MatchRecovery` source-card validation for `WATCHFUL_SENTINEL_LAST_BREATH_DRAW_1` now calls `UnitDestroyedTriggerSpecRules.TryGetLastBreathDrawOneTrigger(...)` instead of comparing against `WatchfulSentinelCardNoForRecovery`.
- `RecoveryValidatorRejectsSnapshotTimingTriggerQueueStandardLastBreathSourceCardContextDrift`, `RecoveryValidatorRejectsAuthoritativeStateTriggerQueueStandardLastBreathSourceCardContextDrift`, `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueStandardLastBreathSourceCardContextDriftWithoutCountMismatch`, and `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueStandardLastBreathSourceCardContextDriftWithCountMismatch` now expect a BehaviorSpec trigger-shape mismatch for Watchful Sentinel instead of a fixed `OGN·096/298` card-number mismatch.
- `P79ScoutingWarhawkCallsSleepingRuneWhenDestroyed` now covers both 侦察飞鹰 and 黑色玫瑰要员 through the same last-breath call-rune TriggerSpec route; each visible source queues `SCOUTING_WARHAWK_LAST_BREATH_CALL_RUNE_1`, resolves it, calls one dormant rune, exhausts that rune, and moves the source unit to graveyard.
- Existing `ScoutingWarhawk` real trigger queue and state-based cleanup tests verify the migrated route still supports APNAP trigger ordering, stack resolution, hidden / standby source filtering, and unchanged recovery-visible effect-kind payloads.
- `P79MechanicalTricksterCreatesThreeMinionsWhenDestroyed`, `P79IroncladVanguardCreatesTwoRobotsWhenDestroyed`, and existing Muddy Dredger trigger-queue representatives now cover the same last-breath create-base-unit TriggerSpec route; visible sources queue their existing effect kinds and stack resolution creates base units from `TriggerSpec` token count / name / power / keyword / destination data.
- Existing `MechanicalTrickster`, `IroncladVanguard`, and `MuddyDredger` real trigger queue and state-based cleanup tests verify the migrated route still supports APNAP trigger ordering, stack resolution, hidden / standby source filtering, invalid-source filtering, and unchanged recovery-visible effect-kind payloads.
- Existing recovery validators for `SAVAGE_JAWFISH_FRIENDLY_DESTROYED_EXPERIENCE_1`, `GHOSTLY_CENTAUR_FRIENDLY_DESTROYED_POWER_2`, `RESONANT_SOUL_FIRST_FRIENDLY_DESTROYED_DRAW_1`, `VIKTOR_DESTROYED_NON_MINION_CREATE_MINION`, `SAD_PORO_LAST_BREATH_DRAW_1`, `LOYAL_PORO_LAST_BREATH_DRAW_1`, `WATCHFUL_SENTINEL_LAST_BREATH_DRAW_1`, `SCOUTING_WARHAWK_LAST_BREATH_CALL_RUNE_1`, `MECHANICAL_TRICKSTER_LAST_BREATH_CREATE_MINIONS`, `IRONCLAD_VANGUARD_LAST_BREATH_CREATE_ROBOTS`, and `MUDDY_DREDGER_LAST_BREATH_CREATE_WARHAWK` still pass because the stack effect values are unchanged.

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
- Focused 警觉的哨兵 behavior-spec/source-guard representatives: `2/2` passing.
- Adjacent `WatchfulSentinel` representatives: `12/12` passing.
- Focused 侦察飞鹰 / 黑色玫瑰要员 call-rune behavior-spec/source-guard/runtime representatives: `5/5` passing.
- Adjacent `ScoutingWarhawk` representatives: `13/13` passing.
- Focused Mechanical Trickster / Ironclad Vanguard / Muddy Dredger create-base-unit behavior-spec/source-guard representatives: `4/4` passing.
- Focused Mechanical Trickster / Ironclad Vanguard / Muddy Dredger runtime representatives: `25/25` passing.
- Adjacent `LastBreath|StateBasedCleanup` representatives: `220/220` passing.
- `FullGameEndToEnd`: `15/15` passing.
- `MatchRecovery`: `1989/1989` passing.
- Backend full conformance: `8523/8523` passing.
- 2026-06-30 recovery follow-up focused guard and drift filter `UnitLastBreathDrawOneTriggerDoesNotUseCardNumberAllowList|StandardLastBreathSourceCardContextDrift`: `5/5` passing.
- 2026-06-30 recovery follow-up adjacent / hidden-info gate `WatchfulSentinel|LastBreath|StateBasedCleanup|CardCatalogBaselineTests|MatchRecovery|TriggerSourceIdentityGuard`: `2392/2392` passing.
- 2026-06-30 recovery follow-up backend full conformance: `9049/9049` passing.
- DevUi catalog TypeScript shape did not change in the last-breath create-base-unit slice; the latest shape sync remains covered by the prior `/opt/homebrew/bin/npm --prefix src/Riftbound.DevUi run build` pass.

## Residual Risk

- The effect-kind values remain the legacy `SAVAGE_JAWFISH_FRIENDLY_DESTROYED_EXPERIENCE_1`, `GHOSTLY_CENTAUR_FRIENDLY_DESTROYED_POWER_2`, `RESONANT_SOUL_FIRST_FRIENDLY_DESTROYED_DRAW_1`, `VIKTOR_DESTROYED_NON_MINION_CREATE_MINION`, `SAD_PORO_LAST_BREATH_DRAW_1`, `LOYAL_PORO_LAST_BREATH_DRAW_1`, `WATCHFUL_SENTINEL_LAST_BREATH_DRAW_1`, `SCOUTING_WARHAWK_LAST_BREATH_CALL_RUNE_1`, `MECHANICAL_TRICKSTER_LAST_BREATH_CREATE_MINIONS`, `IRONCLAD_VANGUARD_LAST_BREATH_CREATE_ROBOTS`, and `MUDDY_DREDGER_LAST_BREATH_CREATE_WARHAWK`; only source recognition, TriggerSpec parsing, condition checks, and effect amount / token-count / token-name / token-power / token-keyword / rune-count routing moved to data-driven engine paths.
- Other destroyed-trigger families still have card-number based source recognition and remain follow-up work.
- Complete destroyed-trigger prompt breadth and hidden-information edge matrices remain open.

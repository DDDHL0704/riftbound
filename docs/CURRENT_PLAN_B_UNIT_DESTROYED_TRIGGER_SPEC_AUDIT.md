# Plan B / Unit Destroyed Trigger Spec Audit

Date: 2026-06-25
Updated: 2026-06-30

Status: focused friendly-destroyed gain-experience, power-until-end, first-friendly-destroyed draw, destroyed non-minion create-minion, last-breath draw-if-alone, last-breath draw-if-not-alone, last-breath draw-one, last-breath call-rune, and last-breath create-base-unit TriggerSpec slices accepted; project remains **NOT READY**.

## Scope

2026-06-30 standard last-breath recovery follow-up:

- `MatchRecovery` no longer owns standard last-breath source-card recovery constants for Sad Poro, Loyal Poro, Unsung Hero, Scouting Warhawk, Honest Broker, Undercover Agent, Mechanical Trickster, Ironclad Vanguard, or Muddy Dredger.
- Recovered snapshot, authoritative-state, and spectator replay source-card validation now reads `BehaviorSpec.Triggers` through `UnitDestroyedTriggerSpecRules`:
  - `TryGetLastBreathDrawIfAloneTrigger(sourceCardNo, out _)`
  - `TryGetLastBreathDrawIfNotAloneTrigger(sourceCardNo, out _)`
  - `TryGetLastBreathPowerfulDrawTrigger(sourceCardNo, out _)`
  - `TryGetLastBreathCallRuneOneTrigger(sourceCardNo, out _)`
  - `TryGetLastBreathCreateDormantGoldTrigger(sourceCardNo, out _)`
  - `TryGetLastBreathDiscardDrawTrigger(sourceCardNo, out _)`
  - exact `TryGetTrigger(sourceCardNo, expectedEffectKind, out _)` for `MECHANICAL_TRICKSTER_LAST_BREATH_CREATE_MINIONS`, `IRONCLAD_VANGUARD_LAST_BREATH_CREATE_ROBOTS`, and `MUDDY_DREDGER_LAST_BREATH_CREATE_WARHAWK`.
- The old `GetStandardLastBreathSourceCardNosForRecovery` helper is removed. Public effect strings and trigger id suffixes remain unchanged.

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
- 2026-06-30 recovery follow-up: `MatchRecovery` no longer owns `SavageJawfishCardNoForRecovery`; recovered snapshot, authoritative-state, and spectator replay source-card validation now use `UnitDestroyedTriggerSpecRules.TryGetFriendlyDestroyedGainExperienceTrigger(sourceCardNo, out _)`.

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
- 2026-06-30 recovery follow-up: `MatchRecovery` no longer owns `GhostlyCentaurCardNoForRecovery`; recovered snapshot, authoritative-state, and spectator replay source-card validation now use `UnitDestroyedTriggerSpecRules.TryGetFriendlyDestroyedPowerUntilEndTrigger(sourceCardNo, out _)`.

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
- 2026-06-30 recovery follow-up: `MatchRecovery` no longer owns `ResonantSoulCardNoForRecovery`; recovered snapshot, authoritative-state, and spectator replay source-card validation now use `UnitDestroyedTriggerSpecRules.TryGetFirstFriendlyDestroyedDrawTrigger(sourceCardNo, out _)`.
- Current source-helper count for `private static bool Is*CardNo(...)` is `38` total / `35` in `CoreRuleEngine`; this count is unchanged by the Ghostly Centaur and Resonant Soul slices because the old Core paths used direct card-number comparisons rather than `Is*CardNo(...)` helpers.

This slice also moves the implemented destroyed non-minion create-minion trigger away from engine card-number branching:

- `ARC-006/006`, `OGN·246/298`, and `OGN·246a/298` 维克托 official text from `data/official/card-catalog.zh-CN.json`: `如果我在场上，则每当你的另一名非“随从”单位被摧毁时，打出一名1{{S}}的“随从”到你的基地。`
- `RuleTextParser` now parses that text as `TriggerSpec` with:
  - `Kind = VIKTOR_DESTROYED_NON_MINION_CREATE_MINION`
  - `Timing = UNIT_DESTROYED`
  - `TargetScope = OTHER_FRIENDLY_DESTROYED_UNIT`
  - `ExcludesTokens = true`
  - `CreatedTokenCount = 1`
  - `CreatedTokenName = 随从`
  - `CreatedTokenPower = 1`
  - `CreatedTokenDestination = OWNER_BASE`
- `TriggerKinds.UnitDestroyedNonMinionCreateMinion` keeps the existing effect-kind value for stack / replay compatibility while exposing a generic engine name.
- `CoreRuleEngine.BuildViktorDestroyedNonMinionTriggerQueueItems` now identifies eligible source units through the shared `UnitDestroyedTriggerSpecRules` path and emits the effect id from `BehaviorSpec.Triggers`.
- `CoreRuleEngine.ResolveViktorDestroyedNonMinionStackItem` now validates the source through the same TriggerSpec path and reads token count / event reason from the `TriggerSpec`.
- The old `ViktorDestroyedNonMinionArcCardNo` / `ViktorDestroyedNonMinionOgnCardNo` / `ViktorDestroyedNonMinionOgnAltACardNo` / `IsViktorDestroyedNonMinionCardNo` Core branch is removed from `CoreRuleEngine`.
- 2026-06-30 recovery follow-up: `MatchRecovery` no longer owns `ViktorDestroyedNonMinionArcCardNoForRecovery`, `ViktorDestroyedNonMinionOgnCardNoForRecovery`, or `ViktorDestroyedNonMinionOgnAltACardNoForRecovery`; recovered snapshot, authoritative-state, and spectator replay source-card validation now use `UnitDestroyedTriggerSpecRules.TryGetDestroyedNonMinionCreateMinionTrigger(sourceCardNo, out _)`.
- The old friendly-destroyed recovery source-card allow-list helper is removed after Ghostly Centaur, Savage Jawfish, Resonant Soul, and Viktor all moved to TriggerSpec source validation.
- Current source-helper count for `private static bool Is*CardNo(...)` is `37` total / `34` in `CoreRuleEngine`.

This slice also moves the implemented Sad Poro last-breath draw trigger away from engine card-number branching:

- `SFD·036/221` and `UNL-221/219` 哀哀魄罗 official text from `data/official/card-catalog.zh-CN.json`: `{{绝念}} — 当我被摧毁时，如果此处没有其他友方单位，则抽一张牌。`
- `RuleTextParser` now parses that text as `TriggerSpec` with:
  - `Kind = SAD_PORO_LAST_BREATH_DRAW_1`
  - `Timing = UNIT_DESTROYED`
  - `TargetScope = SOURCE_UNIT`
  - `DrawCount = 1`
  - `RequiresNoOtherFriendlyUnitAtSamePosition = true`
- `TriggerKinds.UnitLastBreathDrawIfAlone` keeps the existing effect-kind value for stack / replay compatibility while exposing a generic engine name.
- `CoreRuleEngine.ResolveSadPoroLastBreathDrawPlayerId` now identifies eligible source units through the shared `UnitDestroyedTriggerSpecRules` path while preserving the existing field-position isolation check.
- `CoreRuleEngine` explicit-destroy and state-based-cleanup trigger construction now emits the effect id from `BehaviorSpec.Triggers`, and stack resolution reads the draw count from `TriggerSpec.DrawCount`.
- The old `SadPoroOriginalCardNo` / `SadPoroUnleashedCardNo` / `IsSadPoroCardNo` Core branch is removed from `CoreRuleEngine`.
- Current source-helper count for `private static bool Is*CardNo(...)` is `36` total / `33` in `CoreRuleEngine`.

This slice also moves the implemented Loyal Poro last-breath draw trigger away from engine card-number branching:

- `UNL-156/219` 忠忠魄罗 official text from `data/official/card-catalog.zh-CN.json`: `{{绝念>}} 如果我被摧毁时未处于落单状态，则抽一张牌。`
- `RuleTextParser` now parses that text as `TriggerSpec` with:
  - `Kind = LOYAL_PORO_LAST_BREATH_DRAW_1`
  - `Timing = UNIT_DESTROYED`
  - `TargetScope = SOURCE_UNIT`
  - `DrawCount = 1`
  - `RequiresOtherFriendlyUnitAtSamePosition = true`
- `TriggerKinds.UnitLastBreathDrawIfNotAlone` keeps the existing effect-kind value for stack / replay compatibility while exposing a generic engine name.
- `CoreRuleEngine.ResolveLoyalPoroLastBreathDrawPlayerId` now identifies eligible source units through the shared `UnitDestroyedTriggerSpecRules` path while preserving the existing field-position non-isolation check.
- `CoreRuleEngine` explicit-destroy and state-based-cleanup trigger construction now emits the effect id from `BehaviorSpec.Triggers`, and stack resolution reads the draw count from `TriggerSpec.DrawCount`.
- The old `LoyalPoroCardNo` and `LoyalPoroLastBreathDrawEffectKind` Core constants are removed from `CoreRuleEngine`.
- Current source-helper count for `private static bool Is*CardNo(...)` remains `36` total / `33` in `CoreRuleEngine`; this slice removes direct Core card-number/effect-kind constants rather than an `Is*CardNo(...)` helper.

This slice also moves the implemented Watchful Sentinel last-breath draw trigger away from engine card-number branching:

- `OGN·096/298` 警觉的哨兵 official text from `data/official/card-catalog.zh-CN.json`: `{{绝念}}—抽一张牌。`
- `RuleTextParser` now parses that text as `TriggerSpec` with:
  - `Kind = WATCHFUL_SENTINEL_LAST_BREATH_DRAW_1`
  - `Timing = UNIT_DESTROYED`
  - `TargetScope = SOURCE_UNIT`
  - `DrawCount = 1`
- `TriggerKinds.UnitLastBreathDrawOne` keeps the existing effect-kind value for stack / replay compatibility while exposing a generic engine name.
- `CoreRuleEngine.ResolveWatchfulSentinelLastBreathDrawPlayerId` now identifies eligible source units through the shared `UnitDestroyedTriggerSpecRules` path while preserving the existing destroyed-unit / graveyard / visible-cleanup checks.
- `CoreRuleEngine` explicit-destroy and state-based-cleanup trigger construction now emits the effect id from `BehaviorSpec.Triggers`, and stack resolution reads the draw count from `TriggerSpec.DrawCount`.
- The old `WatchfulSentinelCardNo` and `WatchfulSentinelLastBreathDrawEffectKind` Core constants are removed from `CoreRuleEngine`.
- 2026-06-30 recovery follow-up: `MatchRecovery` no longer owns `WatchfulSentinelCardNoForRecovery`; recovered snapshot, authoritative-state, and spectator replay source-card validation now use `UnitDestroyedTriggerSpecRules.TryGetLastBreathDrawOneTrigger(sourceCardNo, out _)`.
- Current source-helper count for `private static bool Is*CardNo(...)` remains `36` total / `33` in `CoreRuleEngine`; this slice removes direct Core card-number/effect-kind constants rather than an `Is*CardNo(...)` helper.

This slice also moves the implemented unit last-breath call-rune trigger away from Scouting Warhawk card-number branching:

- `OGN·216/298` 侦察飞鹰 official text from `data/official/card-catalog.zh-CN.json`: `{{绝念}}—召出一枚休眠的符文。`
- `UNL-152/219` 黑色玫瑰要员 carries the same trigger text after its assault keyword: `{{绝念>}} 召出一枚休眠的符文。`
- `RuleTextParser` now parses that text as `TriggerSpec` with:
  - `Kind = SCOUTING_WARHAWK_LAST_BREATH_CALL_RUNE_1`
  - `Timing = UNIT_DESTROYED`
  - `TargetScope = SOURCE_UNIT`
  - `RuneCallCount = 1`
- `TriggerKinds.UnitLastBreathCallRuneOne` keeps the existing effect-kind value for stack / replay compatibility while exposing a generic engine name.
- `CoreRuleEngine.ResolveUnitLastBreathCallRunePlayerId` now identifies eligible source units through the shared `UnitDestroyedTriggerSpecRules` path while preserving the existing destroyed-unit / graveyard / visible-source checks.
- `CoreRuleEngine` explicit-destroy and state-based-cleanup trigger construction now emits the effect id from `BehaviorSpec.Triggers`, and immediate / stack resolution reads the rune-call count from `TriggerSpec.RuneCallCount`.
- The old `ScoutingWarhawkCardNo` and `ScoutingWarhawkLastBreathCallRuneEffectKind` Core constants are removed from `CoreRuleEngine`.
- Current source-helper count for `private static bool Is*CardNo(...)` remains `36` total / `33` in `CoreRuleEngine`; this slice removes direct Core card-number/effect-kind constants rather than an `Is*CardNo(...)` helper.

This slice also moves the implemented unit last-breath create-base-unit trigger family away from card-number branching:

- `OGN·239/298` Mechanical Trickster official text from `data/official/card-catalog.zh-CN.json`: `{{绝念}}—打出三名1{{S}}的“随从”到你的基地。`
- `SFD·021/221` Ironclad Vanguard official text from `data/official/card-catalog.zh-CN.json`: `{{绝念}}—打出两名3{{S}}的“机器人”到你的基地。`
- `UNL-153/219` Muddy Dredger official text from `data/official/card-catalog.zh-CN.json`: `{{绝念>}} 打出一名1{{S}}的“战鹰”到你的基地，它拥有{{法盾}}。`
- `RuleTextParser` now parses those texts as `TriggerSpec` with:
  - `Timing = UNIT_DESTROYED`
  - `TargetScope = SOURCE_UNIT`
  - `CreatedTokenCount`, `CreatedTokenName`, `CreatedTokenPower`, and `CreatedTokenDestination = OWNER_BASE`
  - `CreatedTokenKeywords = [法盾]` for Muddy Dredger's Warhawk token.
- `TriggerKinds.UnitLastBreathCreateMinions`, `TriggerKinds.UnitLastBreathCreateRobots`, and `TriggerKinds.UnitLastBreathCreateWarhawk` keep the existing effect-kind values for stack / replay compatibility while exposing data-driven engine names.
- `CoreRuleEngine.ResolveUnitLastBreathCreateBaseUnitPlayerId` now identifies eligible source units through the shared `UnitDestroyedTriggerSpecRules` path while preserving the existing destroyed-unit / graveyard / visible-source / non-standby-source checks.
- `CoreRuleEngine` explicit-destroy and state-based-cleanup trigger construction now emits the effect id from `BehaviorSpec.Triggers`, and stack resolution creates base units from `TriggerSpec` token count / name / power / keyword / destination data.
- Token creation now uses the shared P6 token factory when a matching official token definition exists, so Muddy Dredger's Warhawk keeps its `UNL·T02` token card number and `法盾` tag.
- The old `MechanicalTricksterCardNo`, `IroncladVanguardCardNo`, `MuddyDredgerCardNo`, and their Core last-breath create-token effect-kind constants are removed from `CoreRuleEngine`.
- Current source-helper count for `private static bool Is*CardNo(...)` remains `36` total / `33` in `CoreRuleEngine`; this slice removes direct Core card-number/effect-kind constants rather than an `Is*CardNo(...)` helper.

## Non-Goals

- This keeps the existing `SAVAGE_JAWFISH_FRIENDLY_DESTROYED_EXPERIENCE_1`, `GHOSTLY_CENTAUR_FRIENDLY_DESTROYED_POWER_2`, `RESONANT_SOUL_FIRST_FRIENDLY_DESTROYED_DRAW_1`, `VIKTOR_DESTROYED_NON_MINION_CREATE_MINION`, `SAD_PORO_LAST_BREATH_DRAW_1`, `LOYAL_PORO_LAST_BREATH_DRAW_1`, `WATCHFUL_SENTINEL_LAST_BREATH_DRAW_1`, `SCOUTING_WARHAWK_LAST_BREATH_CALL_RUNE_1`, `MECHANICAL_TRICKSTER_LAST_BREATH_CREATE_MINIONS`, `IRONCLAD_VANGUARD_LAST_BREATH_CREATE_ROBOTS`, and `MUDDY_DREDGER_LAST_BREATH_CREATE_WARHAWK` stack effect strings for compatibility with recovery and replay validators.
- This does not migrate other destroyed-trigger families to TriggerSpec.
- This does not rename existing recovery validator constants or old audit file names that describe the legacy effect id.
- This does not close complete natural destroyed-trigger prompt breadth, B0 full-game readiness, or project READY.

## Follow-Up

- Migrate the remaining friendly-destroyed / destroyed-unit trigger families one effect kind at a time into `UnitDestroyedTriggerSpecRules`.
- Keep source-guard tests for each migrated family so new cards can be enabled by BehaviorSpec data rather than `Is*CardNo(...)` allow-lists.

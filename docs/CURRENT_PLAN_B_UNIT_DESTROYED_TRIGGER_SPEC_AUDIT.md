# Plan B / Unit Destroyed Trigger Spec Audit

Date: 2026-06-25

Status: focused friendly-destroyed gain-experience, power-until-end, first-friendly-destroyed draw, destroyed non-minion create-minion, last-breath draw-if-alone, last-breath draw-if-not-alone, last-breath draw-one, and last-breath call-rune TriggerSpec slices accepted; project remains **NOT READY**.

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

## Non-Goals

- This keeps the existing `SAVAGE_JAWFISH_FRIENDLY_DESTROYED_EXPERIENCE_1`, `GHOSTLY_CENTAUR_FRIENDLY_DESTROYED_POWER_2`, `RESONANT_SOUL_FIRST_FRIENDLY_DESTROYED_DRAW_1`, `VIKTOR_DESTROYED_NON_MINION_CREATE_MINION`, `SAD_PORO_LAST_BREATH_DRAW_1`, `LOYAL_PORO_LAST_BREATH_DRAW_1`, `WATCHFUL_SENTINEL_LAST_BREATH_DRAW_1`, and `SCOUTING_WARHAWK_LAST_BREATH_CALL_RUNE_1` stack effect strings for compatibility with recovery and replay validators.
- This does not migrate other destroyed-trigger families to TriggerSpec.
- This does not rename existing recovery validator constants or old audit file names that describe the legacy effect id.
- This does not close complete natural destroyed-trigger prompt breadth, B0 full-game readiness, or project READY.

## Follow-Up

- Migrate the remaining friendly-destroyed / destroyed-unit trigger families one effect kind at a time into `UnitDestroyedTriggerSpecRules`.
- Keep source-guard tests for each migrated family so new cards can be enabled by BehaviorSpec data rather than `Is*CardNo(...)` allow-lists.

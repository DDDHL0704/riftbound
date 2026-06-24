# Plan B / B4 Battlefield Trigger Spec Evidence

Date: 2026-06-25

Project status: **NOT READY**.

## Rule Sources

- `data/official/card-catalog.zh-CN.json`: `OGN·277/298` 后巷酒吧 has official text `每当一名单位从此处向别处移动时，让其本回合内{{S}}+1。`
- `data/official/card-catalog.zh-CN.json`: `UNL-216/219` 皮城学院 has official text `当你据守此处时，在本回合内，你的下一个法术获得等同于其基础费用的{{回响}}。（你可以选择支付此额外费用，以重复此法术效果。）`
- `docs/rules-authority-and-audit.md` and `docs/rules-evidence-index.md`: official card text and local evidence remain the rule authority inputs for this battlefield-domain slice.
- Existing representative tests `P79BattlefieldMovedUnitGainsTemporaryPower`, `P79BattlefieldMovedUnitPowerSkipsOpponentControlledSource`, and `P79BattlefieldMovePowerSeedMovesUnitAndAppliesBonus` remain the runtime evidence for this narrow behavior.
- Existing representative tests `P79BattlefieldHeldNextSpellEcho...` and GameHub `P79BattlefieldHeldNextSpellEcho...` remain the runtime evidence for the held-next-spell Echo behavior.

## Runtime Evidence

The new parser path turns the official text into a structured `TriggerSpec`, including the moved-unit target scope, until-end-of-turn duration and numeric power delta. The runtime no longer checks `OGN·277/298` through `BattlefieldMovedUnitPowerPlusOneCardNo` / `IsBattlefieldMovedUnitPowerPlusOneCardNo`; it queries `BehaviorSpec.Triggers` via `BattlefieldTriggerSpecRules`.

The accepted `MOVE_UNIT` path still applies the same server-authoritative mutation after a successful battlefield-origin move. It now emits `BATTLEFIELD_TRIGGER_RESOLVED` and `POWER_MODIFIED_UNTIL_END_OF_TURN` with `BATTLEFIELD_UNIT_MOVED_AWAY_POWER_MODIFIER` as the trigger / reason, and reads the applied `+1` from the parsed spec.

The 2026-06-25 follow-up parser path turns the Piltover Academy official text into a structured `TriggerSpec` with `Kind=BATTLEFIELD_HELD_NEXT_SPELL_GAINS_ECHO`, `Timing=BATTLEFIELD_HELD`, and `Duration=UNTIL_END_OF_TURN`. The runtime no longer checks `UNL-216/219` through `BattlefieldHeldNextSpellEchoCardNo` / `IsBattlefieldHeldNextSpellEchoCardNo`; it queries `BehaviorSpec.Triggers` via `BattlefieldTriggerSpecRules`.

The accepted `DECLARE_BATTLE` held-battlefield path still stores the same until-end-of-turn marker `BATTLEFIELD_HELD_NEXT_SPELL_GAINS_ECHO:{playerId}`. The next spell prompt still offers the existing Echo optional cost, charges extra mana equal to the spell base cost, repeats the stack item, and consumes the marker. Only the source recognition moved from a card-number branch to BehaviorSpec.

## Hidden Information Evidence

No snapshot hidden-zone logic was changed. The representative GameHub and MatchRecovery validation still cover prompt/snapshot boundaries; MatchRecovery passed `1989/1989`.

## Validation

- moved-unit focused behavior-spec/source guard/runtime/GameHub representative: `5/5`;
- moved-unit adjacent BattlefieldMoved / BattlefieldMovePower / MoveUnit / BoardTaskQueue / FullGame / GameHub: `326/326`;
- held-next-spell Echo focused behavior-spec/source guard/runtime/GameHub representative: `5/5`;
- held-next-spell Echo adjacent BattlefieldHeld / BattlefieldTriggerSpec / BattlefieldMovedUnitPower / BattlefieldMovePower / GameHub battlefield representatives: `102/102`;
- MatchRecovery: `1989/1989`;
- backend full conformance after the held-next-spell Echo follow-up: `8373/8373`;
- DevUi build/browser smoke: not repeated for the held-next-spell Echo follow-up because no DevUi files changed.

## Non-Closure

This evidence proves two battlefield trigger representatives have moved to BehaviorSpec-driven routing. It does not prove the complete B4 battlefield-effect family, all trigger timing windows, all movement / control-zone edge cases, all card-effect families, frontend smoke or READY.

# Plan B / B4 Battlefield Static Ability Spec Evidence

Date: 2026-06-25

Project status: **NOT READY**.

## Rule Sources

- `data/official/card-catalog.zh-CN.json`: `OGN·295/298` has official text `单位无法从此处移动到基地。`
- `data/official/card-catalog.zh-CN.json`: `SFD·216/221` has official text `单位无法被打出到此处。`
- `docs/rules-authority-and-audit.md` and `docs/rules-evidence-index.md`: official card text and local evidence remain the rule authority inputs for this battlefield-domain slice.
- Existing representative tests `P79BattlefieldStaticPreventMoveToBaseRejectsMoveUnit`, `P79BattlefieldStaticPreventMoveToBasePromptSkipsOpponentControlledSource`, `P79BattlefieldStaticPreventsUnitPlayToBattlefield`, `P79BattlefieldStaticPreventUnitPlaySkipsOpponentControlledSource`, `P79BattlefieldStaticPreventMoveBaseSeedRejectsMoveToBase`, and `P79BattlefieldStaticPreventPlayUnitsSeedRejectsAmbushToBattlefield` remain the runtime evidence for this narrow behavior.

## Runtime Evidence

The new parser path turns the official static restriction texts into structured `StaticAbilitySpec` entries. Runtime no longer checks these effects through `BattlefieldPreventMoveToBaseCardNo`, `IsBattlefieldPreventMoveToBaseCardNo`, `BattlefieldPreventUnitPlayCardNo`, or `IsBattlefieldPreventUnitPlayCardNo`; it queries `BehaviorSpec.StaticAbilities` via `BattlefieldStaticAbilitySpecRules`.

The accepted `MOVE_UNIT` and `PLAY_CARD` paths preserve the same server-authoritative rejection behavior:

- battlefield-to-base movement blocked by `BATTLEFIELD_PREVENT_MOVE_TO_BASE` still returns `ErrorCodes.InvalidTarget` and leaves zones unchanged;
- unit play to the battlefield blocked by `BATTLEFIELD_PREVENT_UNIT_PLAY` still returns `ErrorCodes.InvalidTarget`, preserves hand/rune/stack state, and keeps prompt filtering authoritative.

## Hidden Information Evidence

No hidden-zone or opponent-hand projection logic was changed. The representative GameHub tests still cover prompt/snapshot boundaries; MatchRecovery passed `1989/1989`.

## Validation

- focused behavior-spec/source guard/runtime/GameHub representative: `9/9`;
- catalog surface follow-up: `3/3`;
- adjacent BattlefieldStatic / MoveUnit / PlayCard / GameHub / BoardTaskQueue / FullGame: `610/610`;
- MatchRecovery: `1989/1989`;
- backend full conformance: `8371/8371`.

## Non-Closure

This evidence proves two battlefield static restrictions have moved to BehaviorSpec-driven routing. It does not prove the complete B4 battlefield-effect family, all movement / play timing windows, all battlefield lifecycle rules, all card-effect families, frontend smoke or READY.

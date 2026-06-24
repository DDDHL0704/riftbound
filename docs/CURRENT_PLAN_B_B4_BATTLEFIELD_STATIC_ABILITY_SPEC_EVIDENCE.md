# Plan B / B4 Battlefield Static Ability Spec Evidence

Date: 2026-06-25

Project status: **NOT READY**.

## Rule Sources

- `data/official/card-catalog.zh-CN.json`: `OGN·295/298` has official text `单位无法从此处移动到基地。`
- `data/official/card-catalog.zh-CN.json`: `SFD·216/221` has official text `单位无法被打出到此处。`
- `data/official/card-catalog.zh-CN.json`: `SFD·211/221` has official text `如果此战场受你控制，则友方{{回响}}的费用减少{{1}}。`
- `data/official/card-catalog.zh-CN.json`: `SFD·213/221` has official text `如果此战场受你控制，则每回合打出的第一件友方装备的费用减少{{1}}，不包括指示物。`
- `docs/rules-authority-and-audit.md` and `docs/rules-evidence-index.md`: official card text and local evidence remain the rule authority inputs for this battlefield-domain slice.
- Existing representative tests `P79BattlefieldStaticPreventMoveToBaseRejectsMoveUnit`, `P79BattlefieldStaticPreventMoveToBasePromptSkipsOpponentControlledSource`, `P79BattlefieldStaticPreventsUnitPlayToBattlefield`, `P79BattlefieldStaticPreventUnitPlaySkipsOpponentControlledSource`, `P79BattlefieldStaticPreventMoveBaseSeedRejectsMoveToBase`, and `P79BattlefieldStaticPreventPlayUnitsSeedRejectsAmbushToBattlefield` remain the runtime evidence for this narrow behavior.
- Existing representative tests `P79BattlefieldStaticReducesEchoCost`, `P79BattlefieldStaticEchoCostReductionSkipsOpponentControlledSource`, `P79BattlefieldStaticEchoCostReductionPromptSkipsOpponentControlledSource`, and `P79BattlefieldStaticEchoCostReductionSeedPaysReducedEchoCost` remain the runtime evidence for the Echo cost-reduction behavior.
- Existing representative tests `P79BattlefieldStaticReducesFirstEquipmentCost`, `P79BattlefieldStaticEquipmentCostReductionSkipsOpponentControlledSource`, `P79BattlefieldStaticEquipmentCostReductionPromptSkipsOpponentControlledSource`, and `P79BattlefieldStaticEquipmentCostReductionSeedPaysReducedEquipmentCost` remain the runtime evidence for the equipment cost-reduction behavior.

## Runtime Evidence

The new parser path turns the official static ability texts into structured `StaticAbilitySpec` entries. Runtime no longer checks these effects through `BattlefieldPreventMoveToBaseCardNo`, `IsBattlefieldPreventMoveToBaseCardNo`, `BattlefieldPreventUnitPlayCardNo`, `IsBattlefieldPreventUnitPlayCardNo`, `BattlefieldEchoCostReductionCardNo`, `IsBattlefieldEchoCostReductionCardNo`, `BattlefieldEquipmentCostReductionCardNo`, or `IsBattlefieldEquipmentCostReductionCardNo`; it queries `BehaviorSpec.StaticAbilities` via `BattlefieldStaticAbilitySpecRules`.

The accepted `MOVE_UNIT` and `PLAY_CARD` paths preserve the same server-authoritative rejection behavior:

- battlefield-to-base movement blocked by `BATTLEFIELD_PREVENT_MOVE_TO_BASE` still returns `ErrorCodes.InvalidTarget` and leaves zones unchanged;
- unit play to the battlefield blocked by `BATTLEFIELD_PREVENT_UNIT_PLAY` still returns `ErrorCodes.InvalidTarget`, preserves hand/rune/stack state, and keeps prompt filtering authoritative.
- Echo optional-cost reduction from `BATTLEFIELD_ECHO_COST_REDUCTION` still reduces the extra Echo mana by `Amount = 1`, exposes the reduced optional-cost candidate in server prompt metadata, records `battlefieldEchoCostReductionMana = 1` in `COST_PAID`, and skips sources not controlled by the battlefield owner.
- Equipment cost reduction from `BATTLEFIELD_EQUIPMENT_COST_REDUCTION` still reduces the first friendly equipment `PLAY_CARD` mana cost each turn by `Amount = 1`, exposes `minimumManaCost` / `battlefieldEquipmentCostReductionMana` in server prompt metadata, records `PLAYED_EQUIPMENT_THIS_TURN:<playerId>`, and skips sources not controlled by the battlefield owner.

## Hidden Information Evidence

No hidden-zone or opponent-hand projection logic was changed. The representative GameHub tests still cover prompt/snapshot boundaries; MatchRecovery passed `1989/1989`.

## Validation

- latest focused behavior-spec/source guard/static equipment runtime representative: `6/6`;
- latest adjacent BattlefieldStatic / EquipmentCostReduction / PlayCard / PaymentEngine / GameHub / BoardTaskQueue / FullGame: `1317/1317`;
- MatchRecovery: `1989/1989`;
- backend full conformance: `8371/8371`.

## Non-Closure

This evidence proves two battlefield static restrictions, one battlefield static Echo cost reduction, and one battlefield static equipment cost reduction have moved to BehaviorSpec-driven routing. It does not prove the complete B4 battlefield-effect family, complex Echo/equipment costs, all movement / play timing windows, all battlefield lifecycle rules, all card-effect families, frontend smoke or READY.

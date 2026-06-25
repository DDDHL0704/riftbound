# Plan B / B4 Battlefield Static Ability Spec Evidence

Date: 2026-06-25

Project status: **NOT READY**.

## Rule Sources

- `data/official/card-catalog.zh-CN.json`: `OGN·295/298` has official text `单位无法从此处移动到基地。`
- `data/official/card-catalog.zh-CN.json`: `SFD·216/221` has official text `单位无法被打出到此处。`
- `data/official/card-catalog.zh-CN.json`: `SFD·211/221` has official text `如果此战场受你控制，则友方{{回响}}的费用减少{{1}}。`
- `data/official/card-catalog.zh-CN.json`: `SFD·213/221` has official text `如果此战场受你控制，则每回合打出的第一件友方装备的费用减少{{1}}，不包括指示物。`
- `data/official/card-catalog.zh-CN.json`: `UNL-213/219` has official text `此处的单位获得“{{横置}}：获得1经验。”`
- `data/official/card-catalog.zh-CN.json`: `OGN·296/298` has official text `以此处的单位作为目标的法术或技能，造成的伤害+1（每段伤害都+1）。`
- `data/official/card-catalog.zh-CN.json`: `OGN·278/298` and `OGN·278a/298` have official text `你可以选择在此处额外布置一张{{待命}}卡牌。`
- `data/official/card-catalog.zh-CN.json`: `UNL-206/219` has official text `如果此处的一名单位在战斗中被摧毁，其控制者可以选择支付{{A}}{{A}}{{A}}，以此改为移除其所受伤害、将其变为休眠状态、并将其召回。`
- `docs/rules-authority-and-audit.md` and `docs/rules-evidence-index.md`: official card text and local evidence remain the rule authority inputs for this battlefield-domain slice.
- Existing representative tests `P79BattlefieldStaticPreventMoveToBaseRejectsMoveUnit`, `P79BattlefieldStaticPreventMoveToBasePromptSkipsOpponentControlledSource`, `P79BattlefieldStaticPreventsUnitPlayToBattlefield`, `P79BattlefieldStaticPreventUnitPlaySkipsOpponentControlledSource`, `P79BattlefieldStaticPreventMoveBaseSeedRejectsMoveToBase`, and `P79BattlefieldStaticPreventPlayUnitsSeedRejectsAmbushToBattlefield` remain the runtime evidence for this narrow behavior.
- Existing representative tests `P79BattlefieldStaticReducesEchoCost`, `P79BattlefieldStaticEchoCostReductionSkipsOpponentControlledSource`, `P79BattlefieldStaticEchoCostReductionPromptSkipsOpponentControlledSource`, and `P79BattlefieldStaticEchoCostReductionSeedPaysReducedEchoCost` remain the runtime evidence for the Echo cost-reduction behavior.
- Existing representative tests `P79BattlefieldStaticReducesFirstEquipmentCost`, `P79BattlefieldStaticEquipmentCostReductionSkipsOpponentControlledSource`, `P79BattlefieldStaticEquipmentCostReductionPromptSkipsOpponentControlledSource`, and `P79BattlefieldStaticEquipmentCostReductionSeedPaysReducedEquipmentCost` remain the runtime evidence for the equipment cost-reduction behavior.
- Existing representative tests `P79BattlefieldUnitExperienceAbilityExhaustsSourceAndGainsExperience`, `P79BattlefieldUnitExperienceAbilityRequiresReadySource`, `P79BattlefieldUnitExperienceAbilitySkipsOpponentControlledSource`, and the matching GameHub seed tests remain the runtime evidence for the granted unit-experience behavior.
- Existing representative tests `P79BattlefieldTargetDamageBonusAddsOneToSpellDamage`, `P79BattlefieldTargetDamageBonusSkipsOpponentControlledSource`, `P79BattlefieldTargetDamageBonusSkipsTargetsWithoutVoidGate`, the matching GameHub seed test, and adjacent Xerath skill-damage tests remain the runtime evidence for the target spell/skill damage-bonus behavior.
- Existing representative tests `P79BattlefieldBandleTreeArrangesExtraStandbyCard`, `P79BattlefieldBandleTreeRejectsExtraStandbyWithoutControlledTree`, and `P79BattlefieldExtraStandbySeedOffersBandleDestinationAndHides` remain the runtime evidence for the extra-standby destination behavior.
- Existing representative tests `P79BattlefieldBattleDestroyedUnitPaysThreeAndRecalls`, `P79BattlefieldBattleDestroyedUnitSkipsOpponentOwnedAltar`, `P79BattlefieldBattleDestroyedUnitFallsBackToDestroyWhenNoMana`, and `P79BattlefieldBattleDestroyedRecallSeedOffersBattlefieldDestinationAndRecalls` remain the runtime evidence for the battle-destroyed recall replacement behavior.

## Runtime Evidence

The new parser path turns the official static ability texts into structured `StaticAbilitySpec` entries. Runtime no longer checks these effects through `BattlefieldPreventMoveToBaseCardNo`, `IsBattlefieldPreventMoveToBaseCardNo`, `BattlefieldPreventUnitPlayCardNo`, `IsBattlefieldPreventUnitPlayCardNo`, `BattlefieldEchoCostReductionCardNo`, `IsBattlefieldEchoCostReductionCardNo`, `BattlefieldEquipmentCostReductionCardNo`, `IsBattlefieldEquipmentCostReductionCardNo`, `BattlefieldGrantUnitExperienceCardNo`, `IsBattlefieldGrantUnitExperienceCardNo`, `BattlefieldTargetSpellSkillDamageBonusCardNo`, `IsBattlefieldTargetSpellSkillDamageBonusCardNo`, `BattlefieldExtraStandbyCardNo`, `BattlefieldExtraStandbyAltCardNo`, `IsBattlefieldExtraStandbyCardNo`, `BattlefieldDestroyedInBattleRecallCardNo`, `IsBattlefieldDestroyedInBattleRecallCardNo`, or `BattlefieldDestroyedInBattleRecallManaCost`; it queries `BehaviorSpec.StaticAbilities` via `BattlefieldStaticAbilitySpecRules`.

The accepted `MOVE_UNIT` and `PLAY_CARD` paths preserve the same server-authoritative rejection behavior:

- battlefield-to-base movement blocked by `BATTLEFIELD_PREVENT_MOVE_TO_BASE` still returns `ErrorCodes.InvalidTarget` and leaves zones unchanged;
- unit play to the battlefield blocked by `BATTLEFIELD_PREVENT_UNIT_PLAY` still returns `ErrorCodes.InvalidTarget`, preserves hand/rune/stack state, and keeps prompt filtering authoritative.
- Echo optional-cost reduction from `BATTLEFIELD_ECHO_COST_REDUCTION` still reduces the extra Echo mana by `Amount = 1`, exposes the reduced optional-cost candidate in server prompt metadata, records `battlefieldEchoCostReductionMana = 1` in `COST_PAID`, and skips sources not controlled by the battlefield owner.
- Equipment cost reduction from `BATTLEFIELD_EQUIPMENT_COST_REDUCTION` still reduces the first friendly equipment `PLAY_CARD` mana cost each turn by `Amount = 1`, exposes `minimumManaCost` / `battlefieldEquipmentCostReductionMana` in server prompt metadata, records `PLAYED_EQUIPMENT_THIS_TURN:<playerId>`, and skips sources not controlled by the battlefield owner.
- Granted unit-experience activation from `BATTLEFIELD_GRANT_UNIT_EXHAUST_GAIN_EXPERIENCE` still requires a ready source unit at the battlefield, exhausts that source, emits `BATTLEFIELD_TRIGGER_RESOLVED` with `amount = 1`, emits `EXPERIENCE_GAINED.amount = 1`, and skips sources not controlled by the battlefield owner.
- Target spell/skill damage bonus from `BATTLEFIELD_TARGET_SPELL_SKILL_DAMAGE_BONUS` still requires the target unit to be at the same controlled battlefield, skips dirty opponent-controlled battlefield sources, and adds `Amount = 1` to the resolved damage.
- Extra standby destination permission from `BATTLEFIELD_EXTRA_STANDBY_DESTINATION` still exposes `BATTLEFIELD:<objectId>` as a `HIDE_CARD` destination only for a controlled/legacy-owned battlefield source, accepts both official Bandle Tree variants, records `CARD_HIDDEN.destinationZone = BATTLEFIELD`, and rejects dirty opponent-controlled sources without state mutation.
- Battle-destroyed recall replacement from `BATTLEFIELD_DESTROYED_IN_BATTLE_PAY_3_RECALL` still applies only during `DECLARE_BATTLE_COMBAT_DAMAGE`, requires the controlled/legacy-owned battlefield source, reads `Amount = 3` for the mana payment, removes damage, exhausts the unit, recalls it to its controller's base, and falls back to normal destruction when the controller cannot pay.

## Hidden Information Evidence

No hidden-zone or opponent-hand projection logic was changed. The extra-standby path still emits `CARD_HIDDEN` without revealing opponent hidden zones, the battle-destroyed replacement path moves only public battlefield objects, and the representative GameHub tests still cover prompt/snapshot boundaries; MatchRecovery passed `1989/1989`.

## Validation

- latest focused behavior-spec/source guard/Blood Altar battle-destroyed recall runtime representatives: `6/6`;
- latest adjacent battle-destroyed recall / DeclareBattle / BattleDamageAssignment / FullGameEndToEnd representatives: `230/230`;
- CardCatalog baseline: `168/168`;
- MatchRecovery: `1989/1989`;
- backend full conformance: `8459/8459`.
- DevUi build/browser smoke: not repeated; this slice did not touch DevUi files or frontend behavior.

## Non-Closure

This evidence proves two battlefield static restrictions, one battlefield static Echo cost reduction, one battlefield static equipment cost reduction, one battlefield granted unit-experience ability, one battlefield target spell/skill damage bonus, one battlefield extra-standby destination permission, and one battlefield battle-destroyed recall replacement have moved to BehaviorSpec-driven routing. It does not prove the complete B4 battlefield-effect family, complex Echo/equipment costs, all movement / play timing windows, all battlefield lifecycle rules, full activated ability modeling for granted abilities, all spell/skill damage modifier timing edges, complete extra-standby destination / hidden-info breadth, complete battle-destroyed replacement / payment prompt breadth, all card-effect families, frontend smoke or READY.

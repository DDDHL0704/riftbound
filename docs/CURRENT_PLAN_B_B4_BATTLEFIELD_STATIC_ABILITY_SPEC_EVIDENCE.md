# Plan B / B4 Battlefield Static Ability Spec Evidence

Date: 2026-06-28

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
- `data/official/card-catalog.zh-CN.json`: `SFD·208/221` has official text `如果此战场受你控制，则所有友方传奇获得“{{横置}}：将你控制的一件武装贴附到你控制的一名单位上。”`
- `docs/rules-authority-and-audit.md` and `docs/rules-evidence-index.md`: official card text and local evidence remain the rule authority inputs for this battlefield-domain slice.
- Existing representative tests `P79BattlefieldStaticPreventMoveToBaseRejectsMoveUnit`, `P79BattlefieldStaticPreventMoveToBasePromptSkipsOpponentControlledSource`, `P79BattlefieldStaticPreventsUnitPlayToBattlefield`, `P79BattlefieldStaticPreventUnitPlaySkipsOpponentControlledSource`, `P79BattlefieldStaticPreventMoveBaseSeedRejectsMoveToBase`, and `P79BattlefieldStaticPreventPlayUnitsSeedRejectsAmbushToBattlefield` remain the runtime evidence for this narrow behavior.
- `OfficialDeckMidgameRejectsBattlefieldPreventMoveToBaseAndScoreVictoryActionLogReplaysToFinalStateHash` now proves the same move restriction through legal official Vex deck submission/opening, P1 `OGN·295/298` selection, an official `UNL-057/219` Wildclaw Beastmaster at that battlefield, server-authored `MOVE_UNIT` prompt metadata with no `BATTLEFIELD_TO_BASE` source requirement for that unit, rejected `MOVE_UNIT` with `ErrorCodes.InvalidTarget`, unchanged state hash, score victory, and final-state action-log replay that includes the rejected command.
- `OfficialDeckMidgameRejectsBattlefieldPreventUnitPlayAndScoreVictoryActionLogReplaysToFinalStateHash` now proves the same unit-play restriction through legal official Vex deck submission/opening, P1 `SFD·216/221` selection, official `UNL-057/219` Wildclaw Beastmaster in hand with sufficient mana, server-authored `PLAY_CARD` prompt metadata whose source-specific destination choices omit `BATTLEFIELD:<SFD·216 object>`, rejected `PLAY_CARD` with `ErrorCodes.InvalidTarget`, unchanged state hash, score victory, and final-state action-log replay that includes the rejected command.
- Existing representative tests `P79BattlefieldStaticReducesEchoCost`, `P79BattlefieldStaticEchoCostReductionSkipsOpponentControlledSource`, `P79BattlefieldStaticEchoCostReductionPromptSkipsOpponentControlledSource`, and `P79BattlefieldStaticEchoCostReductionSeedPaysReducedEchoCost` remain the runtime evidence for the Echo cost-reduction behavior.
- `OfficialDeckMidgameResolvesMaraiSpireEchoCostReductionAndScoreVictoryActionLogReplaysToFinalStateHash` now proves the same Echo cost-reduction behavior through legal official Jhin deck submission/opening, P1 `SFD·211/221` Marai Spire selection, official `UNL-061/219` Center Stage with `ECHO`, `COST_PAID.battlefieldEchoCostReductionMana=1`, stack `effectRepeatCount=2`, score victory, and final-state action-log replay.
- Existing representative tests `P79BattlefieldStaticReducesFirstEquipmentCost`, `P79BattlefieldStaticEquipmentCostReductionSkipsOpponentControlledSource`, `P79BattlefieldStaticEquipmentCostReductionPromptSkipsOpponentControlledSource`, and `P79BattlefieldStaticEquipmentCostReductionSeedPaysReducedEquipmentCost` remain the runtime evidence for the equipment cost-reduction behavior.
- `OfficialDeckMidgameResolvesOrnnForgeEquipmentCostReductionAndScoreVictoryActionLogReplaysToFinalStateHash` now proves the same equipment cost-reduction behavior through legal official Rumble deck submission/opening, P1 `SFD·213/221` Ornn's Forge selection, official `SFD·022/221` Long Sword with only 1 mana, `PLAY_CARD.sourceRequirements.minimumManaCost=1`, `COST_PAID.battlefieldEquipmentCostReductionMana=1`, Long Sword stack resolution, score victory, and final-state action-log replay.
- Existing representative tests `P79BattlefieldUnitExperienceAbilityExhaustsSourceAndGainsExperience`, `P79BattlefieldUnitExperienceAbilityRequiresReadySource`, `P79BattlefieldUnitExperienceAbilitySkipsOpponentControlledSource`, and the matching GameHub seed tests remain the runtime evidence for the granted unit-experience behavior.
- `OfficialDeckMidgameResolvesMutationGardenGrantedUnitExperienceAndScoreVictoryActionLogReplaysToFinalStateHash` now proves the same granted unit-experience behavior through legal official Vex deck submission/opening, P1 `UNL-213/219` Mutation Garden selection, server-authored `BATTLEFIELD_UNIT_EXHAUST_GAIN_EXPERIENCE`, source exhaustion, `EXPERIENCE_GAINED.totalExperience=1`, score victory, and final-state action-log replay.
- Existing representative tests `P79BattlefieldTargetDamageBonusAddsOneToSpellDamage`, `P79BattlefieldTargetDamageBonusSkipsOpponentControlledSource`, `P79BattlefieldTargetDamageBonusSkipsTargetsWithoutVoidGate`, the matching GameHub seed test, and adjacent Xerath skill-damage tests remain the runtime evidence for the target spell/skill damage-bonus behavior.
- `OfficialDeckMidgameResolvesVoidGateTargetSpellSkillDamageBonusAndScoreVictoryActionLogReplaysToFinalStateHash` now proves the same target spell/skill damage-bonus behavior through legal official Jhin vs Vex deck submission/opening, P2 `OGN·296/298` Void Gate selection, official `UNL-007/219` Punishment stack resolution against a public same-battlefield `UNL-057/219` Wildclaw Beastmaster, score victory, and final-state action-log replay.
- Existing representative tests `P79BattlefieldBandleTreeArrangesExtraStandbyCard`, `P79BattlefieldBandleTreeRejectsExtraStandbyWithoutControlledTree`, and `P79BattlefieldExtraStandbySeedOffersBandleDestinationAndHides` remain the runtime evidence for the extra-standby destination behavior.
- Existing representative tests `P79BattlefieldBattleDestroyedUnitPaysThreeAndRecalls`, `P79BattlefieldBattleDestroyedUnitSkipsOpponentOwnedAltar`, `P79BattlefieldBattleDestroyedUnitFallsBackToDestroyWhenNoMana`, and `P79BattlefieldBattleDestroyedRecallSeedOffersBattlefieldDestinationAndRecalls` remain the runtime evidence for the battle-destroyed recall replacement behavior.
- `OfficialDeckMidgameResolvesBloodAltarBattleDestroyedRecallAndScoreVictoryActionLogReplaysToFinalStateHash` now proves the same battle-destroyed recall replacement behavior through legal official Vex vs Rumble deck submission/opening, P2 `UNL-206/219` Blood Altar selection, official `UNL-057/219` Wildclaw Beastmaster attacking official `OGN·096/298` Watchful Sentinel at that battlefield, real `DECLARE_BATTLE` / `ASSIGN_COMBAT_DAMAGE`, 3-mana payment, replacement recall to P2 base, score victory, and final-state action-log replay.
- Existing representative tests `P79BattlefieldForgeGrantsLegendArmamentAttach`, `P79BattlefieldForgeReattachesControlledArmament`, `P79BattlefieldForgeLegendAttachRejectedWithoutControlledForge`, `P79BattlefieldForgeLegendAttachRejectsOpponentOwnedForge`, `P79BattlefieldForgeLegendAttachRejectsLegendSourceWithoutCardNo`, and `P79BattlefieldLegendAttachArmamentSeedOffersLegendActionAndAttaches` remain the runtime evidence for the battlefield-granted legend attach-armament behavior.
- `OfficialDeckMidgameResolvesPoroForgeLegendAttachArmamentAndScoreVictoryActionLogReplaysToFinalStateHash` now proves the same battlefield-granted legend attach-armament behavior through legal official Rumble deck submission/opening, P1 `SFD·208/221` Poro Forge selection, official `SFD·181/221` Rumble legend, official `SFD·006/221` Aggressive Dragonhound as the controlled unit target, official `SFD·022/221` Long Sword as a controlled `武装`, server-authored `LEGEND_ACT`, legend exhaustion, `BATTLEFIELD_TRIGGER_RESOLVED.trigger = BATTLEFIELD_CONTROLLED_LEGEND_ATTACH_ARMAMENT`, `EQUIPMENT_ATTACHED`, score victory, and final-state action-log replay.

## Runtime Evidence

The new parser path turns the official static ability texts into structured `StaticAbilitySpec` entries. Runtime no longer checks these effects through `BattlefieldPreventMoveToBaseCardNo`, `IsBattlefieldPreventMoveToBaseCardNo`, `BattlefieldPreventUnitPlayCardNo`, `IsBattlefieldPreventUnitPlayCardNo`, `BattlefieldEchoCostReductionCardNo`, `IsBattlefieldEchoCostReductionCardNo`, `BattlefieldEquipmentCostReductionCardNo`, `IsBattlefieldEquipmentCostReductionCardNo`, `BattlefieldGrantUnitExperienceCardNo`, `IsBattlefieldGrantUnitExperienceCardNo`, `BattlefieldTargetSpellSkillDamageBonusCardNo`, `IsBattlefieldTargetSpellSkillDamageBonusCardNo`, `BattlefieldExtraStandbyCardNo`, `BattlefieldExtraStandbyAltCardNo`, `IsBattlefieldExtraStandbyCardNo`, `BattlefieldDestroyedInBattleRecallCardNo`, `IsBattlefieldDestroyedInBattleRecallCardNo`, `BattlefieldDestroyedInBattleRecallManaCost`, `BattlefieldGrantLegendAttachArmamentCardNo`, `IsBattlefieldGrantLegendAttachArmamentCardNo`, or `RequiredControlledBattlefieldCardNo`; it queries `BehaviorSpec.StaticAbilities` via `BattlefieldStaticAbilitySpecRules`.

The accepted `MOVE_UNIT` and `PLAY_CARD` paths preserve the same server-authoritative rejection behavior:

- battlefield-to-base movement blocked by `BATTLEFIELD_PREVENT_MOVE_TO_BASE` still returns `ErrorCodes.InvalidTarget` and leaves zones unchanged;
- The official-deck replay path now carries that same `BATTLEFIELD_PREVENT_MOVE_TO_BASE` route from server-authored prompt filtering through a replayable rejected `MOVE_UNIT`, no-mutated state hash, subsequent score victory, and final-state action-log replay.
- unit play to the battlefield blocked by `BATTLEFIELD_PREVENT_UNIT_PLAY` still returns `ErrorCodes.InvalidTarget`, preserves hand/rune/stack state, and keeps prompt filtering authoritative.
- The official-deck replay path now carries that same `BATTLEFIELD_PREVENT_UNIT_PLAY` route from server-authored prompt destination filtering through a replayable rejected `PLAY_CARD`, no-mutated state hash, subsequent score victory, and final-state action-log replay.
- Echo optional-cost reduction from `BATTLEFIELD_ECHO_COST_REDUCTION` still reduces the extra Echo mana by `Amount = 1`, exposes the reduced optional-cost candidate in server prompt metadata, records `battlefieldEchoCostReductionMana = 1` in `COST_PAID`, and skips sources not controlled by the battlefield owner.
- The official-deck replay path now carries that same `BATTLEFIELD_ECHO_COST_REDUCTION` route from server-authored `PLAY_CARD` prompt through reduced Echo payment, `STACK_ITEM_ADDED.effectRepeatCount = 2`, repeated draw resolution, score victory, and final-state action-log replay.
- Equipment cost reduction from `BATTLEFIELD_EQUIPMENT_COST_REDUCTION` still reduces the first friendly equipment `PLAY_CARD` mana cost each turn by `Amount = 1`, exposes `minimumManaCost` / `battlefieldEquipmentCostReductionMana` in server prompt metadata, records `PLAYED_EQUIPMENT_THIS_TURN:<playerId>`, and skips sources not controlled by the battlefield owner.
- The official-deck replay path now carries that same `BATTLEFIELD_EQUIPMENT_COST_REDUCTION` route from server-authored `PLAY_CARD` prompt through reduced Long Sword payment, `PLAYED_EQUIPMENT_THIS_TURN:P1`, stack resolution attaching the equipment to a controlled unit, score victory, and final-state action-log replay.
- Granted unit-experience activation from `BATTLEFIELD_GRANT_UNIT_EXHAUST_GAIN_EXPERIENCE` still requires a ready source unit at the battlefield, exhausts that source, emits `BATTLEFIELD_TRIGGER_RESOLVED` with `amount = 1`, emits `EXPERIENCE_GAINED.amount = 1`, and skips sources not controlled by the battlefield owner.
- The official-deck replay path now carries that same `BATTLEFIELD_GRANT_UNIT_EXHAUST_GAIN_EXPERIENCE` route from server-authored `ACTIVATE_ABILITY` prompt through source exhaustion, `BATTLEFIELD_TRIGGER_RESOLVED.amount = 1`, `EXPERIENCE_GAINED.totalExperience = 1`, score victory, and final-state action-log replay.
- Target spell/skill damage bonus from `BATTLEFIELD_TARGET_SPELL_SKILL_DAMAGE_BONUS` still requires the target unit to be at the same controlled battlefield, skips dirty opponent-controlled battlefield sources, and adds `Amount = 1` to the resolved damage.
- The official-deck replay path now carries that same `BATTLEFIELD_TARGET_SPELL_SKILL_DAMAGE_BONUS` route from server-authored `PLAY_CARD` prompt through stack resolution and verifies `DAMAGE_APPLIED.damage = 4` for official Punishment against a public Wildclaw target at Void Gate.
- Extra standby destination permission from `BATTLEFIELD_EXTRA_STANDBY_DESTINATION` still exposes `BATTLEFIELD:<objectId>` as a `HIDE_CARD` destination only for a controlled/legacy-owned battlefield source, accepts both official Bandle Tree variants, records `CARD_HIDDEN.destinationZone = BATTLEFIELD`, and rejects dirty opponent-controlled sources without state mutation.
- Battle-destroyed recall replacement from `BATTLEFIELD_DESTROYED_IN_BATTLE_PAY_3_RECALL` still applies only during `DECLARE_BATTLE_COMBAT_DAMAGE`, requires the controlled/legacy-owned battlefield source, reads `Amount = 3` for the mana payment, removes damage, exhausts the unit, recalls it to its controller's base, and falls back to normal destruction when the controller cannot pay.
- The official-deck replay path now carries that same `BATTLEFIELD_DESTROYED_IN_BATTLE_PAY_3_RECALL` route from server-authored `DECLARE_BATTLE` / `ASSIGN_COMBAT_DAMAGE` through `BATTLEFIELD_TRIGGER_RESOLVED`, `COST_PAID.mana = 3`, `UNIT_RECALLED_TO_BASE.replacementEffectId`, score victory, and final-state action-log replay.
- Battlefield-granted legend attach-armament from `BATTLEFIELD_GRANT_LEGEND_EXHAUST_ATTACH_ARMAMENT` still exposes `LEGEND_EXHAUST_ATTACH_CONTROLLED_ARMAMENT_FROM_BATTLEFIELD` only when the player controls a battlefield with that static ability, rejects missing/opponent-controlled sources without mutation, exhausts the legend, attaches or reattaches the controlled armament, and emits `BATTLEFIELD_TRIGGER_RESOLVED` with the concrete `battlefieldObjectId` and `battlefieldCardNo`.
- The official-deck replay path now carries that same `BATTLEFIELD_GRANT_LEGEND_EXHAUST_ATTACH_ARMAMENT` route from server-authored `LEGEND_ACT` prompt through prompt-indexed controlled-unit / armament choices, `LEGEND_ABILITY_ACTIVATED`, `LEGEND_EXHAUSTED`, `BATTLEFIELD_TRIGGER_RESOLVED.trigger = BATTLEFIELD_CONTROLLED_LEGEND_ATTACH_ARMAMENT`, `EQUIPMENT_ATTACHED`, score victory, and final-state action-log replay.

## Hidden Information Evidence

No hidden-zone or opponent-hand projection logic was changed. The prevent move-to-base replay uses only public battlefield/base objects and asserts snapshot boundaries after the rejected command; the extra-standby path still emits `CARD_HIDDEN` without revealing opponent hidden zones, the battle-destroyed replacement and legend attach-armament paths move only public battlefield/base objects, and the representative GameHub tests still cover prompt/snapshot boundaries; MatchRecovery remained covered in adjacent validation.

## Validation

- latest prevent unit-play official-deck rejected-command replay focused validation: `1/1`;
- latest prevent unit-play / PlayCard / FullGameEndToEnd / MatchRecovery / CardCatalogBaseline adjacent validation: `2689/2689`;
- backend full conformance after the prevent unit-play replay increment: `8863/8863`;
- latest prevent move-to-base official-deck rejected-command replay focused validation: `1/1`;
- latest prevent move-to-base / MoveUnit / FullGameEndToEnd / MatchRecovery / CardCatalogBaseline adjacent validation: `2517/2517`;
- backend full conformance after the prevent move-to-base replay increment: `8862/8862`;
- latest Blood Altar official-deck replay focused validation: `1/1`;
- latest Blood Altar battle-destroyed recall / DeclareBattle / BattleDamageAssignment / FullGameEndToEnd / MatchRecovery / CardCatalogBaseline adjacent validation: `2597/2597`;
- backend full conformance after the Blood Altar replay increment: `8861/8861`;
- latest Poro Forge official-deck replay focused validation: `1/1`;
- latest Poro Forge legend attach-armament / LegendAct / FullGameEndToEnd / MatchRecovery adjacent validation: `2249/2249`;
- backend full conformance after the Poro Forge replay increment: `8860/8860`;
- latest Ornn's Forge official-deck replay focused validation: `1/1`;
- latest Ornn's Forge equipment cost-reduction / LongSword / FullGameEndToEnd / MatchRecovery adjacent validation: `2179/2179`;
- backend full conformance after the Ornn's Forge replay increment: `8859/8859`;
- latest Marai Spire official-deck replay focused validation: `1/1`;
- latest Marai Spire Echo / FullGameEndToEnd / MatchRecovery adjacent validation: `2208/2208`;
- backend full conformance after the Marai Spire replay increment: `8858/8858`;
- latest Mutation Garden official-deck replay focused validation: `1/1`;
- latest Mutation Garden granted unit-experience / FullGameEndToEnd / MatchRecovery adjacent validation: `2183/2183`;
- backend full conformance after the Mutation Garden replay increment: `8857/8857`;
- latest Void Gate official-deck replay focused validation: `1/1`;
- latest Void Gate target-damage / FullGameEndToEnd / MatchRecovery adjacent validation: `2086/2086`;
- backend full conformance after the Void Gate replay increment: `8856/8856`;
- latest focused behavior-spec/source guard/Poro Forge legend attach runtime representatives: `8/8`;
- CardCatalog baseline: `172/172`;
- MatchRecovery: `1989/1989`;
- backend full conformance: `8471/8471`.
- DevUi build/browser smoke: not repeated; this slice did not touch DevUi files or frontend behavior.

## Non-Closure

This evidence proves two battlefield static restrictions, one battlefield static Echo cost reduction, one battlefield static equipment cost reduction, one battlefield granted unit-experience ability, one battlefield target spell/skill damage bonus, one battlefield extra-standby destination permission, one battlefield battle-destroyed recall replacement, and one battlefield-granted legend attach-armament ability have moved to BehaviorSpec-driven routing. It does not prove the complete B4 battlefield-effect family, complex Echo/equipment costs, all movement / play timing windows, all battlefield lifecycle rules, full activated ability modeling for granted abilities, all spell/skill damage modifier timing edges, complete extra-standby destination / hidden-info breadth, complete battle-destroyed replacement / payment prompt breadth, all card-effect families, frontend smoke or READY.

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
- `docs/rules-authority-and-audit.md` and `docs/rules-evidence-index.md`: official card text and local evidence remain the rule authority inputs for this battlefield-domain slice.
- Existing representative tests `P79BattlefieldStaticPreventMoveToBaseRejectsMoveUnit`, `P79BattlefieldStaticPreventMoveToBasePromptSkipsOpponentControlledSource`, `P79BattlefieldStaticPreventsUnitPlayToBattlefield`, `P79BattlefieldStaticPreventUnitPlaySkipsOpponentControlledSource`, `P79BattlefieldStaticPreventMoveBaseSeedRejectsMoveToBase`, and `P79BattlefieldStaticPreventPlayUnitsSeedRejectsAmbushToBattlefield` remain the runtime evidence for this narrow behavior.
- Existing representative tests `P79BattlefieldStaticReducesEchoCost`, `P79BattlefieldStaticEchoCostReductionSkipsOpponentControlledSource`, `P79BattlefieldStaticEchoCostReductionPromptSkipsOpponentControlledSource`, and `P79BattlefieldStaticEchoCostReductionSeedPaysReducedEchoCost` remain the runtime evidence for the Echo cost-reduction behavior.
- Existing representative tests `P79BattlefieldStaticReducesFirstEquipmentCost`, `P79BattlefieldStaticEquipmentCostReductionSkipsOpponentControlledSource`, `P79BattlefieldStaticEquipmentCostReductionPromptSkipsOpponentControlledSource`, and `P79BattlefieldStaticEquipmentCostReductionSeedPaysReducedEquipmentCost` remain the runtime evidence for the equipment cost-reduction behavior.
- Existing representative tests `P79BattlefieldUnitExperienceAbilityExhaustsSourceAndGainsExperience`, `P79BattlefieldUnitExperienceAbilityRequiresReadySource`, `P79BattlefieldUnitExperienceAbilitySkipsOpponentControlledSource`, and the matching GameHub seed tests remain the runtime evidence for the granted unit-experience behavior.
- Existing representative tests `P79BattlefieldTargetDamageBonusAddsOneToSpellDamage`, `P79BattlefieldTargetDamageBonusSkipsOpponentControlledSource`, `P79BattlefieldTargetDamageBonusSkipsTargetsWithoutVoidGate`, the matching GameHub seed test, and adjacent Xerath skill-damage tests remain the runtime evidence for the target spell/skill damage-bonus behavior.

## Runtime Evidence

The new parser path turns the official static ability texts into structured `StaticAbilitySpec` entries. Runtime no longer checks these effects through `BattlefieldPreventMoveToBaseCardNo`, `IsBattlefieldPreventMoveToBaseCardNo`, `BattlefieldPreventUnitPlayCardNo`, `IsBattlefieldPreventUnitPlayCardNo`, `BattlefieldEchoCostReductionCardNo`, `IsBattlefieldEchoCostReductionCardNo`, `BattlefieldEquipmentCostReductionCardNo`, `IsBattlefieldEquipmentCostReductionCardNo`, `BattlefieldGrantUnitExperienceCardNo`, `IsBattlefieldGrantUnitExperienceCardNo`, `BattlefieldTargetSpellSkillDamageBonusCardNo`, or `IsBattlefieldTargetSpellSkillDamageBonusCardNo`; it queries `BehaviorSpec.StaticAbilities` via `BattlefieldStaticAbilitySpecRules`.

The accepted `MOVE_UNIT` and `PLAY_CARD` paths preserve the same server-authoritative rejection behavior:

- battlefield-to-base movement blocked by `BATTLEFIELD_PREVENT_MOVE_TO_BASE` still returns `ErrorCodes.InvalidTarget` and leaves zones unchanged;
- unit play to the battlefield blocked by `BATTLEFIELD_PREVENT_UNIT_PLAY` still returns `ErrorCodes.InvalidTarget`, preserves hand/rune/stack state, and keeps prompt filtering authoritative.
- Echo optional-cost reduction from `BATTLEFIELD_ECHO_COST_REDUCTION` still reduces the extra Echo mana by `Amount = 1`, exposes the reduced optional-cost candidate in server prompt metadata, records `battlefieldEchoCostReductionMana = 1` in `COST_PAID`, and skips sources not controlled by the battlefield owner.
- Equipment cost reduction from `BATTLEFIELD_EQUIPMENT_COST_REDUCTION` still reduces the first friendly equipment `PLAY_CARD` mana cost each turn by `Amount = 1`, exposes `minimumManaCost` / `battlefieldEquipmentCostReductionMana` in server prompt metadata, records `PLAYED_EQUIPMENT_THIS_TURN:<playerId>`, and skips sources not controlled by the battlefield owner.
- Granted unit-experience activation from `BATTLEFIELD_GRANT_UNIT_EXHAUST_GAIN_EXPERIENCE` still requires a ready source unit at the battlefield, exhausts that source, emits `BATTLEFIELD_TRIGGER_RESOLVED` with `amount = 1`, emits `EXPERIENCE_GAINED.amount = 1`, and skips sources not controlled by the battlefield owner.
- Target spell/skill damage bonus from `BATTLEFIELD_TARGET_SPELL_SKILL_DAMAGE_BONUS` still requires the target unit to be at the same controlled battlefield, skips dirty opponent-controlled battlefield sources, and adds `Amount = 1` to the resolved damage.

## Hidden Information Evidence

No hidden-zone or opponent-hand projection logic was changed. The representative GameHub tests still cover prompt/snapshot boundaries; MatchRecovery passed `1989/1989`.

## Validation

- latest focused behavior-spec/source guard/target damage runtime representative: `6/6`;
- latest adjacent BattlefieldTargetDamageBonus / BattlefieldStatic / Xerath / GameHub / P6 surface representatives: `86/86`;
- MatchRecovery: `1989/1989`;
- backend full conformance: `8383/8383`.
- DevUi build/browser smoke: not repeated; this slice did not touch DevUi files or frontend behavior.

## Non-Closure

This evidence proves two battlefield static restrictions, one battlefield static Echo cost reduction, one battlefield static equipment cost reduction, one battlefield granted unit-experience ability, and one battlefield target spell/skill damage bonus have moved to BehaviorSpec-driven routing. It does not prove the complete B4 battlefield-effect family, complex Echo/equipment costs, all movement / play timing windows, all battlefield lifecycle rules, full activated ability modeling for granted abilities, all spell/skill damage modifier timing edges, all card-effect families, frontend smoke or READY.

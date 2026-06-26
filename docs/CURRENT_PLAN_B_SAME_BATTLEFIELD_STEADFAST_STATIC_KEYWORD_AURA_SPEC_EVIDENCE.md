# Plan B Same-Battlefield Steadfast Static Keyword Aura Spec Evidence

更新时间：2026-06-26

## Evidence Summary

This evidence records the Taric and Farron representatives for data-driven same-battlefield RULE_TEXT keyword grants.

Catalog / BehaviorSpec:

- `src/Riftbound.CardCatalog/RuleTextParsers.cs` parses `此处的其他友方单位获得{{...}}` into `StaticAuraKinds.SameBattlefieldOtherFriendlyUnitsKeyword`.
- `tests/Riftbound.ConformanceTests/CardCatalogBaselineTests.cs` now verifies `OGN·074/298` exposes one `StaticAuraSpec` with `GrantedKeyword=坚守`.
- `data/official/card-catalog.zh-CN.json` also supplies `OGN·015/298` Farron Captain text `此处的其他友方单位获得{{强攻}}。（如果他们是进攻方，则{{S}}+1。）`, which resolves through the same `StaticAuraSpec.Kind=SAME_BATTLEFIELD_OTHER_FRIENDLY_UNITS_KEYWORD` path with `GrantedKeyword=强攻`.

Engine projection:

- `src/Riftbound.Engine/MatchSession.cs` builds RULE_TEXT continuous effects through `BuildSameBattlefieldOtherFriendlyUnitsKeywordAuraEffects`.
- `src/Riftbound.Engine/StaticAuraSpecRules.cs` supplies `TryGetSameBattlefieldOtherFriendlyUnitsKeywordAura` from official BehaviorSpec data.
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs` verifies the projection targets only the other friendly unit at Taric's battlefield.

Combat resolution:

- `src/Riftbound.Engine/CoreRuleEngine.cs` resolves same-battlefield other-friendly keyword bonuses through `ResolveSameBattlefieldOtherFriendlyUnitsKeywordBonus`.
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs` verifies a defending friendly unit granted `坚守` receives `keywordBonus=1`, `keyword=坚守`, and combat power 3 from base power 2.
- The same test verifies the enemy attacker remains at `keywordBonus=0`, no `staticPowerBonus`, combat power 5.

Lifecycle removal:

- `P79SameBattlefieldStaticKeywordGrantExpiresWhenSourceLeavesBattlefield` verifies that the RULE_TEXT continuous effect exists while Taric remains at the same battlefield, but disappears when the source object is no longer on the field.
- The same lifecycle guard verifies combat resolution recomputes from current locations: the formerly granted defender drops from `keywordBonus=1` / combat power 3 to `keywordBonus=0` / combat power 2 after the source leaves.
- `P79SameBattlefieldStaticKeywordGrantExpiresWhenTargetMovesToAnotherBattlefield` verifies the same duration from the target side: if the friendly unit moves to another battlefield while Taric remains at the original battlefield, the continuous effect is absent and battle power recomputes without the granted `坚守`.

Controller scope:

- `P79SameBattlefieldStaticKeywordGrantUsesCurrentControllerForFriendlyScope` verifies `other friendly` is evaluated from the source object's current controller. A Farron Captain owned by P1 but controlled by P2 grants `强攻` only to the same-battlefield P2 attacker and not to P1's same-battlefield defender.

Full-game replay:

- `FullGameEndToEndTests.OfficialDeckMidgameAppliesSameBattlefieldStaticKeywordAndScoreVictoryActionLogReplaysToFinalStateHash` carries official `OGN·015/298` Farron Captain and `UNL-004/219` Ascended Believer through a legal official Jhin deck path. Server prompts stage both units to the same battlefield, `MatchSession` projects the `RULE_TEXT:SAME_BATTLEFIELD_OTHER_FRIENDLY_UNITS_KEYWORD:*:强攻` object effect from Farron to the Believer, real `DECLARE_BATTLE` damage records `basePower=1`, `keyword=强攻`, `keywordBonus=1`, no `staticPowerBonus`, `combatPower=2`, and `damage=2`, and the score-victory action log replays to the same final state hash.
- `FullGameEndToEndTests.OfficialDeckMidgameAppliesSameBattlefieldSteadfastStaticKeywordAndScoreVictoryActionLogReplaysToFinalStateHash` carries official `OGN·074/298` Taric and `UNL-090/219` LeBlanc through a legal official Lillia deck path. Server prompts stage both units to an opposing battlefield, `MatchSession` projects the `RULE_TEXT:SAME_BATTLEFIELD_OTHER_FRIENDLY_UNITS_KEYWORD:*:坚守` object effect from Taric to LeBlanc, real `DECLARE_BATTLE` damage records defender `basePower=4`, `keyword=坚守`, `keywordBonus=1`, no `staticPowerBonus`, `combatPower=5`, and `damage=5`, and the score-victory action log replays after a follow-up battle declaration and no-legal `BATTLE_SKIPPED`.
- `FullGameEndToEndTests.OfficialDeckMidgameOrdersTaricBulwarkBeforeBackRowInDamageAssignmentAndScoreVictoryActionLogReplaysToFinalStateHash` carries official `OGN·074/298` Taric, `UNL-090/219` LeBlanc and `UNL-057/219` Wildclaw Beastmaster through a legal official Lillia deck path. The driver intentionally declares defenders as LeBlanc then Taric, while the server-authored `ASSIGN_COMBAT_DAMAGE` prompt orders legal targets as Taric first (`BULWARK_FIRST`) and LeBlanc last (`BACK_ROW_LAST`). The same battle effective-power path gives both defenders lethal threshold 5 from printed/granted `坚守`, and Wildclaw assigns 5 damage to Taric then 2 to LeBlanc before score-victory action-log replay reaches the same final state hash.

Hidden source boundary:

- `P79SameBattlefieldStaticKeywordGrantDoesNotProjectFromFaceDownSource` verifies a face-down same-battlefield keyword source produces no RULE_TEXT continuous effect even when authoritative test state still carries the card number, and combat resolution does not grant `坚守` to the same-battlefield friendly defender.
- `P79SameBattlefieldStaticKeywordGrantDoesNotProjectToFaceDownTarget` verifies a face-down same-battlefield friendly unit is not emitted as a RULE_TEXT continuous-effect target.
- `P79SameBattlefieldStaticKeywordGrantDoesNotProjectFromStandbySource` verifies a standby same-battlefield keyword source produces no RULE_TEXT continuous effect and combat resolution does not grant `坚守` to the same-battlefield friendly defender.

Existing fixture alignment:

- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-taric-keyword-unit.fixture.json` still covers the ordinary hand-play path into the controller base, and now points the same-battlefield static keyword grant to the B2 RULE_TEXT aura representative.
- `docs/rules-evidence-index.md`, `docs/p2-rules-preflight.md`, and `docs/CURRENT_P2_STATUS.md` now distinguish the covered Taric static keyword grant from the still-open defensive power and Bulwark ordering surfaces.

## Validation Evidence

- Focused static-aura parse / Taric representative: 2/2 passed.
- Adjacent StaticAura / StaticPower / ContinuousEffect / Steadfast / Taric / DeclareBattle / FullGameEndToEnd representatives: 543/543 passed.
- MatchRecovery hidden-information boundary: 1989/1989 passed.
- Backend full: 8532/8532 passed.
- Focused source-leaves lifecycle guard `P79SameBattlefieldStaticKeywordGrantExpiresWhenSourceLeavesBattlefield`: 1/1 passed.
- Adjacent SameBattlefield / StaticKeyword / StaticAura / Steadfast / Taric / MatchRecovery representatives: 2063/2063 passed.
- Backend full after lifecycle guard: 8611/8611 passed.
- Focused source-leaves + target-moves lifecycle guards: 2/2 passed.
- Adjacent SameBattlefield / StaticKeyword / StaticAura / Steadfast / Taric / MatchRecovery representatives after target-moves guard: 2064/2064 passed.
- Backend full after target-moves guard: 8612/8612 passed.
- Focused controller-scope guard `P79SameBattlefieldStaticKeywordGrantUsesCurrentControllerForFriendlyScope`: 1/1 passed.
- Adjacent SameBattlefield / StaticKeyword / StaticAura / Steadfast / Farron / Control / MatchRecovery representatives: 2271/2271 passed.
- Backend full after controller-scope guard: 8613/8613 passed.
- Focused face-down source guard `P79SameBattlefieldStaticKeywordGrantDoesNotProjectFromFaceDownSource`: 1/1 passed.
- Adjacent SameBattlefield / StaticKeyword / StaticAura / Steadfast / Taric / Hidden / FaceDown / MatchRecovery representatives: 2163/2163 passed.
- Backend full after face-down source guard: 8614/8614 passed.
- Focused face-down target guard `P79SameBattlefieldStaticKeywordGrantDoesNotProjectToFaceDownTarget`: 1/1 passed.
- Adjacent SameBattlefield / StaticKeyword / StaticAura / Steadfast / Taric / Hidden / FaceDown / MatchRecovery representatives after face-down target guard: 2164/2164 passed.
- Backend full after face-down target guard: 8615/8615 passed.
- Focused standby source guard `P79SameBattlefieldStaticKeywordGrantDoesNotProjectFromStandbySource`: 1/1 passed.
- Same-battlefield static keyword adjacent representatives after standby source guard: 15/15 passed.
- StaticAura / StaticKeyword / MatchRecovery adjacent representatives after standby source guard: 2050/2050 passed.
- Backend full after standby source guard: 8629/8629 passed.
- Focused official-deck Farron replay `OfficialDeckMidgameAppliesSameBattlefieldStaticKeywordAndScoreVictoryActionLogReplaysToFinalStateHash`: 1/1 passed.
- FullGameEndToEnd B0/B2 cross-slice representatives after official-deck Farron replay: 22/22 passed.
- SameBattlefield / StaticKeyword / StaticAura / Farron / FullGameEndToEnd / MatchRecovery adjacent representatives after official-deck Farron replay: 2090/2090 passed.
- MatchRecovery hidden-information boundary after official-deck Farron replay: 1989/1989 passed.
- Backend full after official-deck Farron replay: 8714/8714 passed.
- Focused official-deck Taric replay `OfficialDeckMidgameAppliesSameBattlefieldSteadfastStaticKeywordAndScoreVictoryActionLogReplaysToFinalStateHash`: 1/1 passed.
- FullGameEndToEnd B0/B2 cross-slice representatives after official-deck Taric replay: 23/23 passed.
- SameBattlefield / StaticKeyword / StaticAura / Steadfast / Taric / Farron / FullGameEndToEnd / MatchRecovery adjacent representatives after official-deck Taric replay: 2105/2105 passed.
- MatchRecovery hidden-information boundary after official-deck Taric replay: 1989/1989 passed.
- Backend full after official-deck Taric replay: 8715/8715 passed.
- Focused official-deck Taric Bulwark assignment replay `OfficialDeckMidgameOrdersTaricBulwarkBeforeBackRowInDamageAssignmentAndScoreVictoryActionLogReplaysToFinalStateHash`: 1/1 passed.
- BattleDamageAssignment lifecycle representatives after Taric Bulwark assignment replay: 48/48 passed.
- FullGameEndToEnd B0/B2 cross-slice representatives after Taric Bulwark assignment replay: 24/24 passed.
- BattleDamageAssignment / AssignCombatDamage / Steadfast / Taric / SameBattlefield / StaticKeyword / FullGameEndToEnd / MatchRecovery adjacent representatives after Taric Bulwark assignment replay: 2126/2126 passed.
- Backend full after Taric Bulwark assignment replay: 8716/8716 passed.

## Remaining Evidence Needed

- Full Taric official coverage still needs broader defensive keyword interactions and complete battle assignment breadth beyond the current `壁垒` before `后排` official-deck representative.
- Full RULE_TEXT keyword grant scope coverage remains open beyond same-battlefield other-friendly units and beyond the current Farron same-battlefield Assault official-deck replay representative.
- Full standby / face-down identity coverage remains open beyond the covered same-battlefield source and target representatives.
- The card-effect matrix FU row for `OGN·074/298` still requires a separate, matrix-aware blocker-reduction slice before its FU-level status is changed.

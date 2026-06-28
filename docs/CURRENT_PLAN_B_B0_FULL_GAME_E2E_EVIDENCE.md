# Plan B / B0 Full-Game E2E Evidence

Date: 2026-06-28

Project status: **NOT READY**.

## Rule Sources

- `docs/rules-authority-and-audit.md`: official PDFs, `data/official`, Riot official pages and `playloltcg` card text remain the only rule authorities.
- `docs/rules-evidence-index.md`: prior Stage 4D spell-duel / battle task evidence covers the server task model for `START_SPELL_DUEL`, `START_BATTLE`, `DECLARE_BATTLE`, battle close and prompt / queue redaction.
- `data/official/card-catalog.zh-CN.json`: B0 deck construction uses official card records and `OfficialDeckValidator`.

## Runtime Evidence

The new B0 probe exercises a real `MatchSession` with legal official decks. It does not seed a handcrafted battle state. It uses only server commands and server prompts for:

- `SUBMIT_DECK`
- `READY`
- `MULLIGAN`
- `TAP_RUNE`
- `PLAY_CARD`
- `PASS_PRIORITY`
- `END_TURN`
- `MOVE_UNIT`
- `PASS_FOCUS`
- `DECLARE_BATTLE`
- `ACTIVATE_ABILITY`
- `ASSIGN_COMBAT_DAMAGE`
- `HIDE_CARD`
- `REVEAL_CARD`
- `REVEAL_CARD` with a battlefield standby source, same-battlefield target, top-five reveal/count damage and recycle
- `HIDE_CARD` with `BATTLEFIELD:<Bandle Tree>` extra-standby destination
- rejected `HIDE_CARD` with `BATTLEFIELD:<non-Bandle battlefield>` extra-standby destination
- score-based `END_TURN` advancement to `MATCH_WON`
- `SURRENDER`

The Hub official-opening play-card surrender-win action-log replay smoke (`OfficialDeckCanPlayPromptLegalCardReachSurrenderWinAndReplayThroughHub`) proves the public Hub path can take legal official decks through submission, ready, mulligan, prompt-selected rune tap / recycle resources, a prompt-authored `PLAY_CARD`, priority handoff, stack resolution, next-turn handoff, surrender `MATCH_WON`, both-player opponent-hand redaction, and `MatchActionLogReplayer` final-state hash recovery without using a development seed. It selects source, target, destination and required payment choices from server prompt metadata instead of hard-coding a card number.

The new engine regression proves the spell-duel close handoff chooses the battlefield task player when the turn player is the mover. This is the natural path that previous fixture-style tests did not cover.

The no-legal battle regression proves the next natural blocker is consumed by shared engine state rather than single-card logic. After spell duel closes, `CoreRuleEngine` checks the existing server-authored `DECLARE_BATTLE` requirements for the `START_BATTLE` task player. When no ready face-up attacker / defender declaration exists, the engine emits `BATTLE_SKIPPED`, records `BATTLEFIELD_BATTLE_SKIPPED:*` until end of turn, clears the blocking battlefield task family from state / snapshot projection, and returns to neutral open main timing.

The turn-start battle reopen regression proves still-contested battlefields do not remain idle after a no-legal battle skip expires. `ResolveTurnStart` now advances pending battlefield tasks after turn-start ready / draw / score state is built. The B0 probe drives multiple natural turns, observes repeated no-legal skips until the moved combatant naturally readies, then submits the first server-authored `DECLARE_BATTLE` candidate and observes `BATTLE_DECLARED` plus `BATTLE_CLOSED` from a legal official deck path.

The same-turn skipped-battle reopen regression proves the engine also responds to official-rule cleanup windows after a real state change during the same turn. Core rules 319.6 / 319.7 make cleanup pending after objects enter / leave the field or game-object state changes, and rules 323.9 / 323.13 require pending battles to be offered when enemy-controlled units occupy a contested battlefield. `SameTurnBattleSkippedMarkerIsClearedAfterHasteUnitEntersBattlefieldReady` starts from a spell-duel-completed contested battlefield whose battle task was skipped for no legal combatants, then pays official `HASTE_READY` for `OGN·010/298` Legion Rearguard and plays it directly to that battlefield. The shared `PlaySourceUnitToBattlefield` Haste path now mirrors base unit entry, recording `hasteReadyOptionalCostPaid` and leaving the unit ready; `AdvancePendingBattlefieldTasksAfterStateChange` compares the pre-change and post-change states, clears `BATTLEFIELD_BATTLE_SKIPPED:*` only on a false-to-true legal-combatants transition, and exposes `START_BATTLE` / `DECLARE_BATTLE` before turn end without reopening scaffold states that were already legal before the command.

The score-victory regression proves the same legal official-deck path can continue after real `BATTLE_CLOSED` through server-authored `END_TURN` prompts until battlefield scoring emits `SCORE_GAINED` and a single score-based `MATCH_WON`. The runtime fix restores ordinary open-main action to `TurnPlayerId` after a non-turn-player battle task closes with no further battlefield task, and prevents duplicate `MATCH_WON` during turn start when pre-rune-call scoring already won before the synthetic draw result is built.

The action-log replay regressions prove the score-victory command stream can be journaled and replayed to the same final state hash. `OfficialLowCurvePostBattleScoreVictoryActionLogReplaysToFinalStateHash` starts from the existing mirrored Jhin official low-curve battle-closed state and covers post-battle `END_TURN` scoring. `OfficialLowCurveFullGameScoreVictoryActionLogReplaysToFinalStateHash`, `DistinctOfficialLowCurveFullGameScoreVictoryActionLogReplaysToFinalStateHash`, and `StandbyHeavyOfficialLowCurveFullGameScoreVictoryActionLogReplaysToFinalStateHash` start from a seated official low-curve initial state, record `SUBMIT_DECK`, `READY`, `MULLIGAN`, tap/play/move/focus, reopened battle declaration, and score-victory `END_TURN` commands through `MatchJournal`, convert entries to recovered commands / events, and verify `MatchActionLogReplayer.VerifyFinalStateAsync` reaches the expected final state hash with no replay errors. The B0 test driver writes replayable raw command payloads for prompt-derived object ids and destinations instead of only storing `cmdType`.

The battle-prompt action-log replay regressions prove the two complex B0 prompt representatives are also recoverable from seated-room command logs. `OfficialDecksResolveMultiDefenderBattleDamageAssignmentActionLogReplaysToFinalStateHash` records a legal official Lillia multi-defender path through prompt-derived `DECLARE_BATTLE` and both players' `ASSIGN_COMBAT_DAMAGE` submissions, then replays to the same battle-closed final state hash. `OfficialDecksResolveShadowBattleResponseActivationActionLogReplaysToFinalStateHash` records a legal official Vex / Shadow path through prompt-derived `DECLARE_BATTLE`, `ACTIVATE_ABILITY`, stack resolution and response priority close, then replays to the same battle-closed final state hash. The damage raw payload now uses the protocol lower-camel `assignments[].sourceObjectId`, `assignments[].targetObjectId` and `assignments[].damage` fields used by recovery.

The standby hide/reveal action-log replay regression proves explicit standby setup is also recoverable from seated-room command logs. `StandbyOfficialDecksHideRevealAndScoreVictoryActionLogReplaysToFinalStateHash` records a legal official Poppy deck path that hides official `OGN·135/298` Pakaa Cub through prompt-derived `HIDE_CARD` with `STANDBY_A`, confirms `CARD_HIDDEN` does not expose `cardNo`, reveals the same base object through prompt-derived `REVEAL_CARD` with `STANDBY_REVEAL_0`, then continues through the existing non-standby battle / score-victory route. The hide/reveal raw payloads now preserve source object id, card number, destination, mode and optional cost fields used by recovery.

The battlefield extra-standby action-log replay regression proves a legal official deck can also use Bandle Tree's battlefield standby destination in the B0 full-game route. `StandbyOfficialDecksBattlefieldExtraStandbyHideAndScoreVictoryActionLogReplaysToFinalStateHash` records legal official Poppy decks whose battlefield set includes `OGN·278/298` Bandle Tree, follows the normal official opening seed path until the active standby player controls Bandle Tree and has `OGN·135/298` Pakaa Cub in hand, submits prompt-derived `HIDE_CARD` to `BATTLEFIELD:<Bandle Tree>`, confirms `CARD_HIDDEN.destinationZone = BATTLEFIELD` without `cardNo`, keeps the hidden card face-down at that battlefield, and continues through non-standby battle / score-victory replay with no `REVEAL_CARD` command in that stream. This slice changes only test / evidence coverage; it does not close battlefield standby reaction or complete extra-standby cleanup breadth.

The battlefield extra-standby rejected-command action-log replay regression proves the command-side guard is also recoverable from a legal official deck opening. `StandbyOfficialDecksRejectBattlefieldExtraStandbyWithoutBandleTreeAndScoreVictoryActionLogReplaysToFinalStateHash` records legal official Poppy decks whose battlefield set intentionally lacks `OGN·278/298` Bandle Tree, follows the normal official opening seed path until the active standby player has `OGN·135/298` Pakaa Cub in hand, confirms the server-authored `HIDE_CARD` prompt omits `BATTLEFIELD:<non-Bandle battlefield>`, then submits that direct command anyway. The engine rejects it with `ErrorCodes.InvalidTarget`, emits no events, preserves state hash, rune pool, hand and object location, then the same journal continues through non-standby battle / score-victory replay to the same final state hash including the rejected command. This slice changes only test / evidence coverage; it does not close battlefield standby reaction, all standby replacement-cost branches, complete hidden-info breadth, or complete extra-standby cleanup breadth.

The battlefield standby reveal prompt/resolver regressions prove the shared standby reveal path is no longer base-only. `ActionPromptFiltersRevealCardSourcesByWindowAndFaceDownStandby` now verifies that an open-main `REVEAL_CARD` prompt can expose both a base face-down standby source and a battlefield face-down standby source, with source-specific destination choices `BASE` and `BATTLEFIELD:<battlefieldObjectId>`. `P4RevealCardCommandRevealsStandbyCardAtBattlefield` verifies a direct `REVEAL_CARD` command flips an official standby unit face-up in place at its controlled public battlefield, preserves the precise `ObjectLocations` battlefield id, keeps the stack empty, and emits a `CARD_REVEALED` payload with the battlefield destination. This runtime slice covers open-main reveal only.

The battlefield standby reaction prompt/resolver regression proves closed-window standby reaction is no longer base-only for the representative stack path. `ActionPromptFiltersRevealCardSourcesByWindowAndFaceDownStandby` now also verifies that a closed-main priority `REVEAL_CARD` prompt exposes both base and battlefield face-down standby sources with immediate destination `STACK`. `P4RevealCardCommandPlaysBattlefieldStandbyReactionToStackAndReturnsToBattlefield` verifies an official battlefield standby unit can be revealed as `STANDBY_REACTION`, leaves its battlefield zone while on the stack, records `BATTLEFIELD:<battlefieldObjectId>` on the stack item as the post-resolution play destination, and returns to that precise battlefield after both players pass priority. Focused regression `P4RevealCardCommandPlaysBattlefieldStandbyReactionToStackAndReturnsToBattlefield|ActionPromptFiltersRevealCardSourcesByWindowAndFaceDownStandby` passed 2/2, adjacent `RevealCard|StandbyOfficialDecks|FullGameEndToEnd|MatchRecovery` passed 2163/2163, and backend full passed 8890/8890. This runtime slice does not close illegal/lost-control standby cleanup breadth, all standby replacement-cost branches, complete hidden-info breadth, all standby card effects, complete B0, or READY.

The battlefield-source standby reaction target-damage regressions prove one official `OGN·121/298` / `OGN·121a/298` Teemo target route now resolves through shared behavior metadata instead of a card-number branch. `ActionPromptRevealCardMetadataExposesBattlefieldStandbyReactionTargets` verifies prompt metadata exposes `ENEMY_UNIT_AT_SOURCE_BATTLEFIELD`, max target count 1, and only same-battlefield enemy unit choices for a face-down battlefield standby source. `P4RevealCardCommandBattlefieldStandbyReactionTargetDamageCountsTopFiveStandbyCards` reveals battlefield standby Teemo as `STANDBY_REACTION`, stores the target on the stack, resolves back to the precise battlefield, reveals the controller's top five main-deck cards, counts cards tagged `待命`, applies that count as damage to the still-legal target, and recycles the looked cards to the bottom of the main deck. Focused target/context guard regression passed 6/6, `RevealCard|StandbyReaction|ActionPrompt` adjacent passed 131/131, hidden-info / recovery adjacent including `MatchRecovery` passed 2120/2120, and backend full passed 8893/8893. This runtime slice does not close base spell-duel context covered by the follow-up slice, base battle-response context covered by the later follow-up slice, complete standby replacement-cost branches, full standby card-family breadth, complete B0, or READY.

The base standby reaction spell-duel context regressions extend that target-damage path to a base face-down standby source when the response stack belongs to an active spell duel. `ActionPromptRevealCardMetadataExposesBaseStandbyReactionTargetsFromSpellDuelContext` verifies `SpellDuelClosed` stack-priority prompt metadata resolves `ENEMY_UNIT_AT_SOURCE_BATTLEFIELD` from `SpellDuelState.BattlefieldObjectId` and exposes only same-battlefield enemy unit targets. `P4RevealCardCommandBaseStandbyReactionTargetDamageUsesSpellDuelBattlefieldContext` verifies `REVEAL_CARD mode=STANDBY_REACTION` stores the target and inherited `SpellDuelOpen` timing context while leaving destination empty, then stack resolution returns Teemo to base, reveals top five, counts `待命`, damages the same-battlefield enemy target, recycles looked cards, and leaves the lower spell-duel stack item intact. Focused regression passed 2/2, `RevealCard|StandbyReaction|ActionPrompt|SpellDuel` adjacent passed 199/199, hidden-info / recovery adjacent passed 2179/2179, and backend full passed 8895/8895. This still does not close base battle-response context covered by the later follow-up slice, non-active-battle/spell-duel base contexts without a battlefield, complete standby replacement-cost branches, full standby card-family breadth, complete B0, or READY.

The base standby reaction battle-response context regressions extend the same target-damage route to a real `DECLARE_BATTLE` response window. `DeclareBattleOpensStandbyReactionBattleResponseForDefender` verifies a completed-spell-duel `START_BATTLE` task opens `BATTLE_RESPONSE_PRIORITY_OPENED` for the defending player when `StackPriorityActions` exposes `REVEAL_CARD`, and prompt metadata resolves `ENEMY_UNIT_AT_SOURCE_BATTLEFIELD` from `BattleState.BattlefieldObjectId`. `BaseStandbyReactionTargetDamageUsesBattleResponseBattlefieldContext` verifies base face-down `OGN·121/298` Teemo can enter the empty-stack battle response window as `STANDBY_REACTION`, stores the same-battlefield attacker target with `TimingContext=NeutralClosed` and empty destination, resolves top-five counted standby damage, recycles looked cards, returns Teemo to base, and returns to the active battle response window. Focused regression passed 2/2, adjacent / hidden-info `StandbyReactionBattleResponse|RevealCard|StandbyReaction|ActionPrompt|BattleResponse|DeclareBattle|MatchRecovery` passed 2284/2284, and backend full passed 8897/8897. This still does not close non-active-battle/spell-duel base contexts without a battlefield, complete standby replacement-cost branches, full standby card-family breadth, complete B0, or READY.

The Tide Caller standby reaction swap regressions extend `REVEAL_CARD mode=STANDBY_REACTION` from damage-only targets to the shared optional friendly-unit target / location-swap behavior. `PromptMetadataExposesFriendlyUnitTargetsForStandbyReactionSwap` verifies face-down base `OGN·199/298` Tide Caller exposes `FRIENDLY_UNIT`, 0-1 targets, and only controlled visible unit choices while excluding non-unit public objects. `BaseStandbyReactionTideCallerSwapsWithFriendlyUnitOnResolution` verifies the reveal command stores the friendly target on the stack, resolves the source to base, swaps the source with the target through the existing `SwapsSourceWithFirstTargetLocation` path, and preserves precise `ObjectLocations.BattlefieldObjectId` for the source's new battlefield location. Focused regression passed 2/2, adjacent / hidden-info `StandbyReactionTideCaller|RevealCard|StandbyReaction|ActionPrompt|TideCaller|MatchRecovery` passed 2130/2130, and backend full passed 8899/8899. This closes only this representative Tide Caller standby reaction swap path; complete standby card-family breadth, all replacement-cost branches, non-representative reaction windows, complete B0, and READY remain open.

The SFD Teemo shared standby-reaction metadata regressions prove official `SFD·230/221` and `SFD·230*/221` use the same target-damage resolver as the already covered Teemo route without adding an engine card-number branch. `PromptMetadataExposesSfdTeemoStandbyReactionTargets` verifies both SFD card numbers expose `ENEMY_UNIT_AT_SOURCE_BATTLEFIELD`, max target count 1, and only same-battlefield enemy unit choices for a face-down battlefield standby source. `BattlefieldStandbyReactionSfdTeemoDamagesAndRecyclesWithSharedResolver` verifies both source card numbers enter the stack with their own effect kinds, return to the precise battlefield, reveal the controller's top five main-deck cards, count cards tagged `待命`, apply that count as damage to the same-battlefield target, and recycle the looked cards. Focused regression passed 4/4, adjacent / hidden-info `StandbyReactionSfdTeemo|StandbyReaction|RevealCard|ActionPrompt|Teemo|MatchRecovery` passed 2158/2158, and backend full passed 8903/8903. This closes only the SFD Teemo shared metadata breadth; defend-trigger timing, non-active battle/spell-duel base contexts, complete standby replacement-cost branches, full standby card-family breadth, complete B0, and READY remain open.

The battlefield extra-standby lost-control cleanup action-log replay regression proves the existing cleanup path is reachable from a legal official deck opening and can continue through the B0 score-victory route. `StandbyOfficialDecksBattlefieldExtraStandbyCleanupAfterControlLossScoreVictoryActionLogReplaysToFinalStateHash` records legal official Poppy / Bandle Tree and Lillia openings, derives a focused midgame with official `OGN·135/298` Pakaa Cub face-down at P1's Bandle Tree, official `UNL-092/219` Demacia Envoy defending, and official `UNL-057/219` Wildclaw Beastmaster attacking for P2. The server-authored `DECLARE_BATTLE` route emits `BATTLEFIELD_CONQUERED`, resolves Bandle Tree control from P1 to P2, emits `BATTLEFIELD_STANDBY_REMOVED`, moves Pakaa Cub face-up to P1 graveyard, and then continues through score victory and action-log replay to the same final state hash. Focused cleanup replay passed 1/1, adjacent `StandbyOfficialDecks|Standby|RevealCard|FullGameEndToEndTests|MatchRecovery` passed 2241/2241, and backend full passed 8891/8891. This slice changes only test / evidence coverage; it does not close complete illegal/lost-control standby cleanup breadth, all standby replacement-cost branches, complete hidden-info breadth, all standby card effects, complete B0, or READY.

The same-battlefield static-aura action-log replay regression proves a legal official deck can carry a data-driven `STATIC_AURA` through the B0 full-game route. `OfficialDeckMidgameAppliesSameBattlefieldStaticAuraAndScoreVictoryActionLogReplaysToFinalStateHash` records legal official Poppy decks containing `OGS·013/024` Garen and `UNL-092/219` Demacia Envoy, follows the normal official opening seed path, stages both friendly units and an opposing defender through server-authored `PLAY_CARD` / `MOVE_UNIT` prompts, verifies Garen's `BehaviorSpec.StaticAuras` projection targets the Envoy, declares battle with the boosted Envoy, observes `DAMAGE_APPLIED` with `basePower=2`, `staticPowerBonus=1`, `combatPower=3`, and `damage=3`, then continues through score-victory replay to the same final state hash. `OfficialDeckMidgameAppliesDariusSameBattlefieldStaticAuraAndScoreVictoryActionLogReplaysToFinalStateHash` runs the same mechanism through a legal official Darius deck containing `OGN·253/298` legend, `OGN·243/298` champion, `SFD·236/221` Darius static-aura source, and `SFD·006/221` Aggressive Dragonhound; the server prompt route projects `SAME_BATTLEFIELD_OTHER_FRIENDLY_UNITS_POWER_PLUS_ONE` from Darius to Dragonhound, records Dragonhound `basePower=3`, `staticPowerBonus=1`, `combatPower=4`, and `damage=4`, then replays through score victory to the same final state hash. This slice changes only test / evidence coverage; it does not close complete static-aura breadth, complete official deck archetype breadth, or READY.

The Ornn friendly-equipment count-to-source static-aura action-log replay regression proves a legal official deck can carry an object-source recomputed `STATIC_AURA` through the B0 full-game route. `OfficialDeckMidgameAppliesOrnnFriendlyEquipmentStaticAuraAndScoreVictoryActionLogReplaysToFinalStateHash` records legal official Rumble decks containing `SFD·085/221` Ornn and `SFD·022/221` Long Sword, verifies official deck submission/opening first, stages Long Sword as a public friendly equipment in P1 base, then plays and moves Ornn through server-authored prompts. The projected `FRIENDLY_FIELD_EQUIPMENT_COUNT_TO_SOURCE_UNIT_POWER` effect targets Ornn itself with Long Sword as participant and `PowerDelta=1`; real battle damage records recomputed `basePower=5`, no extra `staticPowerBonus`, `combatPower=5`, and `damage=5`, then replays through score victory to the same final state hash. This slice changes only test / evidence coverage; it does not close complete equipment attachment lifecycle, complete static-aura breadth, complete official deck archetype breadth, or READY.

The other-friendly static-aura action-log replay regression proves a legal official deck can carry a non-local data-driven `STATIC_AURA` through the B0 full-game route. `OfficialDeckMidgameAppliesOtherFriendlyStaticAuraAndScoreVictoryActionLogReplaysToFinalStateHash` records legal official Vex decks containing `UNL-147/219` Baron Nashor and `UNL-057/219` Wildclaw Beastmaster, follows a seeded official opening path, stages Baron Nashor to base and Wildclaw to a battlefield through server-authored `PLAY_CARD` / `MOVE_UNIT` prompts, verifies Baron Nashor's `BehaviorSpec.StaticAuras` projection targets the other friendly Wildclaw with `OTHER_FRIENDLY_UNITS_POWER`, declares battle with Wildclaw, observes `DAMAGE_APPLIED` with `basePower=7`, `staticPowerBonus=2`, `combatPower=9`, and `damage=9`, then continues through score-victory replay to the same final state hash. This slice changes only test / evidence coverage; it does not close Baron Nest creation, enemy spell/skill targeting protection, complete static-aura breadth, complete official deck archetype breadth, or READY.

The source-combat static-aura action-log replay regression proves a legal official deck can carry a battle-conditional source-object `STATIC_AURA` through the B0 full-game route. `OfficialDeckMidgameAppliesSourceCombatStaticAuraAndScoreVictoryActionLogReplaysToFinalStateHash` records legal official Poppy decks containing `UNL-154/219` Scarlet Pigeon and `UNL-092/219` Demacia Envoy, follows the normal official opening seed path, stages both friendly units and an opposing defender through server-authored `PLAY_CARD` / `MOVE_UNIT` prompts, submits a server-authorized `DECLARE_BATTLE` with both Scarlet Pigeon and Demacia Envoy as attackers, observes Scarlet Pigeon's real `DAMAGE_APPLIED` with `basePower=3`, `staticPowerBonus=2`, `combatPower=5`, and `damage=5`, then continues through score-victory replay to the same final state hash. This slice changes only test / evidence coverage; it does not close complete source-combat static-aura breadth, complete official deck archetype breadth, or READY.

The Dune Drake source-attacking-ready-enemy static-aura action-log replay regression proves a legal official deck can carry the ready-enemy source-object `STATIC_AURA` through the B0 full-game route. `OfficialDeckMidgameAppliesDuneDrakeSourceAttackingReadyEnemyStaticAuraAndScoreVictoryActionLogReplaysToFinalStateHash` records legal official Poppy decks containing `OGN·131/298` Dune Drake, follows the normal official opening seed path, stages Dune Drake and an opposing ready defender to the same battlefield through server-authored `PLAY_CARD` / `MOVE_UNIT` prompts, submits a server-authorized `DECLARE_BATTLE` with Dune Drake as the only attacker, verifies the projected `SOURCE_ATTACKING_READY_ENEMY_UNIT_POWER` effect has the defender as participant, observes Dune Drake's real `DAMAGE_APPLIED` with `basePower=5`, `staticPowerBonus=2`, `combatPower=7`, and `damage=7`, then continues through score-victory replay to the same final state hash. This slice changes only test / evidence coverage; it does not close complete source-combat static-aura breadth, complete official deck archetype breadth, or READY.

The Petal Pixie same-battlefield ephemeral count-to-source static-aura action-log replay regression proves a legal official deck opening can feed a focused count-to-source `STATIC_AURA` replay with an official token participant. `OfficialDeckMidgameAppliesPetalPixieSameBattlefieldEphemeralCountStaticAuraAndScoreVictoryActionLogReplaysToFinalStateHash` records legal official Lillia decks containing `UNL-076/219` Petal Pixie, verifies official deck submission/opening first, then starts the replay from a focused midgame `START_BATTLE` state with Petal Pixie, official `UNL·T07` Faerie token, and opposing `UNL-057/219` Wildclaw Beastmaster at the same P1 battlefield. The projected `SAME_BATTLEFIELD_FRIENDLY_FILTERED_UNIT_COUNT_TO_SOURCE_POWER` effect targets Petal Pixie itself with the Faerie token as the same-battlefield `{{瞬息}}` participant, real battle damage records `basePower=2`, `staticPowerBonus=1`, `combatPower=3`, and `damage=3`, and the score-victory action log replays to the same final state hash. This slice changes only test / evidence coverage; it does not close Lillia token creation, complete count-to-source static-aura breadth, complete official deck archetype breadth, or READY.

The Soul Shepherd friendly-token static-aura action-log replay regression proves a legal official deck opening can feed a focused `FRIENDLY_FILTERED_UNITS_POWER` replay with an official token-unit target. `OfficialDeckMidgameAppliesSoulShepherdFriendlyTokenStaticAuraAndScoreVictoryActionLogReplaysToFinalStateHash` records legal official Lillia decks containing `UNL-077/219` Soul Shepherd, verifies official deck submission/opening first, then starts the replay from a focused midgame `START_BATTLE` state with Soul Shepherd in base, official `UNL·T02` Warhawk token, and opposing `UNL-057/219` Wildclaw Beastmaster at the same P1 battlefield. The projected `FRIENDLY_FILTERED_UNITS_POWER` effect targets the Warhawk token with that official token as participant, real battle damage records `basePower=1`, `staticPowerBonus=1`, `combatPower=2`, and `damage=2`, and the score-victory action log replays to the same final state hash. This slice changes only test / evidence coverage; it does not close token creation, complete friendly-filtered static-aura breadth, complete official deck archetype breadth, or READY.

The Rumble friendly-mechanical static-aura action-log replay regression proves a legal official deck opening can feed the tag-filtered self-including branch of `FRIENDLY_FILTERED_UNITS_POWER`. `OfficialDeckMidgameAppliesRumbleFriendlyMechanicalStaticAuraAndScoreVictoryActionLogReplaysToFinalStateHash` records legal official Rumble decks containing `SFD·089/221` Rumble, verifies official deck submission/opening first, then starts the replay from a focused midgame `START_BATTLE` state with Rumble and opposing `OGN·096/298` Watchful Sentinel at the same P1 battlefield. The projected `FRIENDLY_FILTERED_UNITS_POWER` effect targets Rumble itself from the same public source object with Rumble's official `机械` tag as the filter match, real battle damage records `basePower=4`, `staticPowerBonus=1`, `combatPower=5`, and `damage=5`, and the score-victory action log replays to the same final state hash. This slice changes only test / evidence coverage; it does not close complete friendly-filtered static-aura breadth, complete official deck archetype breadth, or READY.

The Rumble legend friendly-mechanical Steadfast action-log replay regression proves a legal official deck opening can feed the tag-filtered legend-source branch of `FRIENDLY_FILTERED_UNITS_KEYWORD`. `OfficialDeckMidgameAppliesRumbleLegendFriendlyMechanicalSteadfastAndScoreVictoryActionLogReplaysToFinalStateHash` records a legal Lillia attacker deck and a legal Rumble defender deck with an extra main-deck `SFD·026/221` Rumble unit, verifies official deck submission/opening first, then starts the replay from a focused midgame `START_BATTLE` state with Watchful Sentinel and the friendly mechanical Rumble defender at the same P1 battlefield. P2's `SFD·181/221` Rumble legend projects `RULE_TEXT:FRIENDLY_FILTERED_UNITS_KEYWORD` to that defender, real defender damage records `basePower=4`, `keyword=坚守`, `keywordBonus=1`, `combatPower=5`, and `damage=5`, and the score-victory action log replays to the same final state hash. This slice changes only test / evidence coverage; it does not close complete friendly-filtered RULE_TEXT keyword aura breadth, complete official deck archetype breadth, or READY.

The Speeding Mech friendly-mechanical Spellshield/Roam action-log replay regression proves a legal official deck opening can feed the multi-keyword public-field-source branch of `FRIENDLY_FILTERED_UNITS_KEYWORD`. `OfficialDeckMidgameProjectsSpeedingMechFriendlyMechanicalSpellshieldRoamAndScoreVictoryActionLogReplaysToFinalStateHash` records a legal Rumble deck containing `SFD·071/221` Speeding Mech and a second official `SFD·026/221` Rumble unit, verifies official deck submission/opening first, then starts the replay from a focused midgame `START_BATTLE` state with Speeding Mech in P1 base, the friendly mechanical Rumble unit and opposing `OGN·096/298` Watchful Sentinel at the same P1 battlefield. Speeding Mech projects two RULE_TEXT effects, `法盾` and `游走`, to the friendly mechanical Rumble target without adding printed tags to the target object, then the server-authored `DECLARE_BATTLE` path continues through score-victory action-log replay to the same final state hash. This slice changes only test / evidence coverage; dynamic Roam movement and Spellshield tax remain covered by the focused P79 representative, and this slice does not close complete friendly-filtered RULE_TEXT keyword aura breadth, complete official deck archetype breadth, or READY.

The Treasure Pile trigger-payment action-log replay regressions prove a legal official deck opening can feed both focused battlefield-conquer payment choices through the B0 score-victory replay path. `OfficialDeckMidgamePaysTreasurePileConquerGoldAndScoreVictoryActionLogReplaysToFinalStateHash` records a legal Vex deck opening that selected official `SFD·220/221` Treasure Pile, then starts from a focused midgame `START_BATTLE` state with `UNL-057/219` Wildclaw Beastmaster, opposing `OGN·096/298` Watchful Sentinel, and sufficient available mana at that P1 battlefield. The server-authored `DECLARE_BATTLE` route opens `TRIGGER_PAYMENT`; the pay branch accepts replayable `PAY_COST(SPEND_MANA:1)`, emits `BATTLEFIELD_TRIGGER_RESOLVED`, `COST_PAID`, and `EQUIPMENT_TOKEN_CREATED`, creates an exhausted Gold equipment token, and continues through score-victory action-log replay to the same final state hash. `OfficialDeckMidgameDeclinesTreasurePileConquerGoldAndScoreVictoryActionLogReplaysToFinalStateHash` records replayable `PAY_COST(DECLINE)` from the same official opening family, emits `TRIGGER_PAYMENT_DECLINED` plus declined `PAYMENT_WINDOW_CLOSED`, creates no token, emits no `COST_PAID`, and still continues through score-victory replay. This slice changes only test / evidence coverage; it does not close all triggered-cost battlefield FUs, complete PaymentEngine breadth, complete official deck archetype breadth, or READY.

The Sunken Temple powerful-unit trigger-payment action-log replay regressions prove another legal official deck opening can feed both focused battlefield-conquer payment choices with a condition and hidden draw. `OfficialDeckMidgamePaysSunkenTemplePowerfulDrawAndScoreVictoryActionLogReplaysToFinalStateHash` records a legal Vex deck opening that selected official `SFD·218/221` Sunken Temple, then starts from a focused midgame `START_BATTLE` state with `UNL-057/219` Wildclaw Beastmaster as the surviving powerful conquest attacker and opposing `OGN·096/298` Watchful Sentinel at that P1 battlefield. The server-authored `DECLARE_BATTLE` route opens `TRIGGER_PAYMENT`; the pay branch accepts replayable `PAY_COST(SPEND_MANA:1)`, emits `BATTLEFIELD_TRIGGER_RESOLVED`, `COST_PAID`, and `CARD_DRAWN`, moves one controlled main-deck card into P1 hand, and continues through score-victory action-log replay to the same final state hash. `OfficialDeckMidgameDeclinesSunkenTemplePowerfulDrawAndScoreVictoryActionLogReplaysToFinalStateHash` records replayable `PAY_COST(DECLINE)` from the same official opening family, emits `TRIGGER_PAYMENT_DECLINED` plus declined `PAYMENT_WINDOW_CLOSED`, draws no card, emits no `COST_PAID`, and still continues through score-victory replay. This slice changes only test / evidence coverage; it does not close all triggered-cost battlefield FUs, complete powerful-unit condition breadth, complete PaymentEngine breadth, complete official deck archetype breadth, or READY.

The Imperial Shrine pay-return-unit create-Sand-Soldier action-log replay regressions prove a legal official deck opening can feed both focused conquered-battlefield payment choices with cost payment, zone movement, token creation, decline semantics, and hidden metadata redaction. `OfficialDeckMidgameResolvesImperialShrineSandSoldierAndScoreVictoryActionLogReplaysToFinalStateHash` records a legal Vex deck opening that selected official `SFD·207/221` Imperial Shrine, then starts from a focused midgame `START_BATTLE` state with `UNL-057/219` Wildclaw Beastmaster and opposing `OGN·096/298` Watchful Sentinel at that P1 battlefield. The server-authored `DECLARE_BATTLE` route opens `TRIGGER_PAYMENT`; the pay branch accepts replayable `PAY_COST(SPEND_MANA:1)`, emits `BATTLEFIELD_TRIGGER_RESOLVED`, `COST_PAID`, `UNIT_RETURNED_TO_HAND`, and `UNIT_TOKEN_CREATED`, spends one mana, returns the controlled Wildclaw object to P1 hand, creates a ready 2-power `SFD·T02` Sand Soldier token at the battlefield, and continues through score-victory action-log replay to the same final state hash. `OfficialDeckMidgameDeclinesImperialShrineSandSoldierAndScoreVictoryActionLogReplaysToFinalStateHash` records replayable `PAY_COST(DECLINE)` from the same official opening family, emits `TRIGGER_PAYMENT_DECLINED` plus declined `PAYMENT_WINDOW_CLOSED`, returns no unit, creates no token, emits no `COST_PAID`, and still continues through score-victory replay. The runtime snapshot follow-up redacts object ids that have moved into a non-viewer hand, main deck, rune deck, or hidden battlefield standby from battle / battlefield task / resolution metadata ids and object-id collections, closing the opponent-view leak exposed by this replay. This slice does not close complete return-unit target choice breadth, complete token lifecycle breadth, complete battlefield FUs, complete official deck archetype breadth, or READY.

The Hall of Legends pay-ready-legend action-log replay regressions prove a legal official deck opening can feed both focused conquered-battlefield payment choices with cost payment, decline semantics, and legend readiness. `OfficialDeckMidgameResolvesHallOfLegendsReadyLegendAndScoreVictoryActionLogReplaysToFinalStateHash` records a legal Vex deck opening that selected official `SFD·210/221` Hall of Legends, then starts from a focused midgame `START_BATTLE` state with `UNL-057/219` Wildclaw Beastmaster, opposing `OGN·096/298` Watchful Sentinel, sufficient available mana, and an exhausted P1 legend. The server-authored `DECLARE_BATTLE` route opens `TRIGGER_PAYMENT`; the pay branch accepts replayable `PAY_COST(SPEND_MANA:1)`, emits `BATTLEFIELD_TRIGGER_RESOLVED`, `COST_PAID`, and `LEGEND_READIED`, spends one mana, readies the controlled legend, and continues through score-victory action-log replay to the same final state hash. `OfficialDeckMidgameDeclinesHallOfLegendsReadyLegendAndScoreVictoryActionLogReplaysToFinalStateHash` records replayable `PAY_COST(DECLINE)` from the same official opening family, emits `TRIGGER_PAYMENT_DECLINED` plus declined `PAYMENT_WINDOW_CLOSED`, keeps the legend exhausted, emits no `COST_PAID`, and still continues through score-victory replay. This slice now changes runtime trigger-payment routing; it does not close complete legend target-choice breadth, complete battlefield FUs, complete official deck archetype breadth, or READY.

The Hunting Grounds overkill create-Warhawk action-log replay regression proves a legal official deck opening can feed a focused conquered-battlefield overkill-token route. `OfficialDeckMidgameResolvesHuntingGroundsOverkillWarhawkAndScoreVictoryActionLogReplaysToFinalStateHash` records a legal Vex deck opening that selected official `UNL-217/219` Hunting Grounds, then starts from a focused midgame `START_BATTLE` state with `UNL-057/219` Wildclaw Beastmaster and opposing `OGN·096/298` Watchful Sentinel at that P1 battlefield. The server-authored `DECLARE_BATTLE` route emits `BATTLEFIELD_TRIGGER_RESOLVED` and `UNIT_TOKEN_CREATED`, assigns at least three overkill damage to the enemy unit, creates a 1-power `UNL·T02` Warhawk token with `法盾` at that battlefield, and continues through score-victory action-log replay to the same final state hash. This slice changes only test / evidence coverage; it does not close complete token lifecycle breadth, complete overkill / damage-assignment breadth, complete battlefield FUs, complete official deck archetype breadth, or READY.

The Candlelit Sanctum conquered reveal/recycle action-log replay regression proves a legal official deck opening can feed the parsed battlefield-conquer reveal/recycle route into the B0 score-victory path. `OfficialDeckMidgameResolvesCandlelitSanctumConquerRevealRecycleAndScoreVictoryActionLogReplaysToFinalStateHash` records a legal Vex deck opening that selected official `OGN·291/298` Candlelit Sanctum, then starts from a focused midgame `START_BATTLE` state with `UNL-057/219` Wildclaw Beastmaster and opposing `OGN·096/298` Watchful Sentinel at that P1 battlefield. The server-authored `DECLARE_BATTLE` route emits `BATTLEFIELD_TRIGGER_RESOLVED` with `trigger=BATTLEFIELD_CONQUERED_REVEAL_TOP_TWO_RECYCLE`, `CARDS_REVEALED`, and `CARDS_RECYCLED`, reveals the top two controlled P1 main-deck cards, recycles the parsed count to the bottom of that deck, and continues through score-victory action-log replay to the same final state hash. This slice changes only test / evidence coverage; it does not close the official optional any-number recycle choice, arbitrary return ordering prompt, complete reveal / recycle hidden-info breadth, complete battlefield FUs, complete official deck archetype breadth, or READY.

The Thunder Sigil conquered recycle-rune action-log replay regression proves a legal official deck opening can feed the parsed battlefield-conquer recycle-rune route into the B0 score-victory path. `OfficialDeckMidgameResolvesThunderSigilConquerRecycleRuneAndScoreVictoryActionLogReplaysToFinalStateHash` records a legal Vex deck opening that selected official `OGN·287/298` 雷霆之纹, then starts from a focused midgame `START_BATTLE` state with `UNL-057/219` Wildclaw Beastmaster and opposing `OGN·096/298` Watchful Sentinel at that P1 battlefield. The server-authored `DECLARE_BATTLE` route emits `BATTLEFIELD_TRIGGER_RESOLVED` with `trigger=BATTLEFIELD_CONQUERED_RECYCLE_RUNE` and `CARDS_RECYCLED`, moves one controlled P1 base rune to the bottom of the P1 main deck, and continues through score-victory action-log replay to the same final state hash. This slice changes only test / evidence coverage; it does not close optional rune choice prompts, complete base/main-deck replacement breadth, complete battlefield FUs, complete official deck archetype breadth, or READY.

The Zaun Sump conquered discard-draw action-log replay regression proves a legal official deck opening can feed the parsed battlefield-conquer discard-draw route into the B0 score-victory path. `OfficialDeckMidgameResolvesZaunSumpConquerDiscardDrawAndScoreVictoryActionLogReplaysToFinalStateHash` records a legal Vex deck opening that selected official `OGN·298/298` 祖安地沟, then starts from a focused midgame `START_BATTLE` state with `UNL-057/219` Wildclaw Beastmaster and opposing `OGN·096/298` Watchful Sentinel at that P1 battlefield. The server-authored `DECLARE_BATTLE` route emits `BATTLEFIELD_TRIGGER_RESOLVED` with `trigger=BATTLEFIELD_CONQUERED_DISCARD_DRAW`, `CARD_DISCARDED`, and `CARD_DRAWN`, moves one controlled P1 hand card to P1 graveyard, draws one controlled P1 main-deck card, and continues through score-victory action-log replay to the same final state hash. This slice changes only test / evidence coverage; it does not close complete discard choice prompts, complete discard replacement / trigger breadth, complete battlefield FUs, complete official deck archetype breadth, or READY.

The Seat of Power conquered draw-for-other-battlefields action-log replay regression proves a legal official deck opening can feed the parsed battlefield-conquer draw-for-other-battlefields route into the B0 score-victory path. `OfficialDeckMidgameResolvesSeatOfPowerConquerDrawForOtherBattlefieldsAndScoreVictoryActionLogReplaysToFinalStateHash` records a legal Vex deck opening that selected official `SFD·217/221` 权能之座, then starts from a focused midgame `START_BATTLE` state with `UNL-057/219` Wildclaw Beastmaster, opposing `OGN·096/298` Watchful Sentinel, and two other controlled P1 battlefield card objects from the official selected battlefield pool. The server-authored `DECLARE_BATTLE` route emits `BATTLEFIELD_TRIGGER_RESOLVED` with `trigger=BATTLEFIELD_CONQUERED_DRAW_FOR_OTHER_BATTLEFIELDS`, includes the two `otherBattlefieldObjectIds`, draws two controlled P1 main-deck cards, and continues through score-victory action-log replay to the same final state hash. This slice changes only test / evidence coverage; it does not close ally / two-headed-giant semantics, complete other-battlefield control breadth, complete battlefield FUs, complete official deck archetype breadth, or READY.

The Mount Targon conquered ready-runes-at-end action-log replay regression proves a legal official deck opening can feed the parsed delayed end-turn rune-ready route into the B0 score-victory path. `OfficialDeckMidgameResolvesMountTargonConquerReadyRunesAtEndAndScoreVictoryActionLogReplaysToFinalStateHash` records a legal Vex deck opening that selected official `OGN·289/298` 巨神峰之巅, then starts from a focused midgame `START_BATTLE` state with `UNL-057/219` Wildclaw Beastmaster, opposing `OGN·096/298` Watchful Sentinel, and two exhausted controlled P1 base runes. The server-authored `DECLARE_BATTLE` route emits `BATTLEFIELD_TRIGGER_RESOLVED` with `trigger=BATTLEFIELD_CONQUERED_READY_RUNES_AT_END`, schedules two end-turn ready-rune effect ids, leaves those runes exhausted until the next server-authored `END_TURN`, then clears the delayed markers, readies the same runes, and continues through score-victory action-log replay to the same final state hash. This slice changes only test / evidence coverage; it does not close optional rune choice prompts, complete delayed end-turn trigger breadth, complete battlefield FUs, complete official deck archetype breadth, or READY.

The Minefield conquered mill action-log replay regression proves a legal official deck opening can feed the parsed battlefield-conquer mill route into the B0 score-victory path. `OfficialDeckMidgameResolvesMinefieldConquerMillAndScoreVictoryActionLogReplaysToFinalStateHash` records a legal Vex deck opening that selected official `SFD·212/221` Minefield, then starts from a focused midgame `START_BATTLE` state with `UNL-057/219` Wildclaw Beastmaster and opposing `OGN·096/298` Watchful Sentinel at that P1 battlefield. The server-authored `DECLARE_BATTLE` route emits `BATTLEFIELD_TRIGGER_RESOLVED` with `trigger=BATTLEFIELD_CONQUERED_MILL_TOP_TWO` and `CARDS_MILLED`, moves the top two controlled P1 main-deck cards to P1 graveyard, and continues through score-victory action-log replay to the same final state hash. This slice changes only test / evidence coverage; it does not close complete main-deck / graveyard replacement breadth, complete battlefield FUs, complete official deck archetype breadth, or READY.

The Dream Tree friendly-spell draw action-log replay regression proves a legal official deck opening can feed a focused `FRIENDLY_UNIT_AT_THIS_BATTLEFIELD` spell-target trigger route before the game continues to a normal score victory. `OfficialDeckMidgameResolvesDreamTreeFriendlySpellDrawAndScoreVictoryActionLogReplaysToFinalStateHash` records a legal Vex deck opening that selected official `OGN·292/298` Dream Tree, then starts from a focused midgame main-phase state with `UNL-057/219` Wildclaw Beastmaster and opposing `OGN·096/298` Watchful Sentinel located at that P1 battlefield, plus official `SFD·034/221` Savage Strength in P1 hand. The server-authored `PLAY_CARD` route targets the same-battlefield friendly unit, emits `BATTLEFIELD_TRIGGER_RESOLVED`, `CARD_DRAWN`, and `STACK_ITEM_ADDED`, records `BATTLEFIELD_FRIENDLY_SPELL_DRAW_USED:P1:{battlefieldObjectId}` until end of turn, resolves the spell stack, then continues through score-victory action-log replay to the same final state hash. This slice does not close complete friendly-spell target breadth, optional spell-duel target timing breadth, complete battlefield FUs, complete official deck archetype breadth, or READY.

The Ravenbloom Conservatory defend reveal-spell action-log replay regression proves a legal official deck opening can feed a focused defended-battlefield reveal route with a controlled main-deck top card. `OfficialDeckMidgameResolvesRavenbloomDefendRevealSpellAndScoreVictoryActionLogReplaysToFinalStateHash` records legal Vex and Lillia deck openings that selected official `SFD·215/221` Ravenbloom Conservatory for P2, then starts from a focused midgame `START_BATTLE` state with P1 `UNL-057/219` Wildclaw Beastmaster, P2 `UNL-036/219` Mutant Kitten, and official `SFD·087/221` Prophet's Omen on top of P2's controlled main deck. The server-authored `DECLARE_BATTLE` route emits `BATTLEFIELD_TRIGGER_RESOLVED`, `CARDS_REVEALED`, and `CARD_DRAWN`, recognizes the revealed card as a spell, moves it to P2 hand, and continues through score-victory action-log replay to the same final state hash. This slice changes only test / evidence coverage; it does not close the non-spell recycle B0 branch, complete reveal / recycle hidden-info breadth, complete battlefield FUs, complete official deck archetype breadth, or READY.

The Ravenbloom Conservatory defend reveal non-spell action-log replay regression covers the miss branch of the same BehaviorSpec route. `OfficialDeckMidgameResolvesRavenbloomDefendRevealNonSpellRecycleAndScoreVictoryActionLogReplaysToFinalStateHash` records legal Vex and Lillia deck openings that selected official `SFD·215/221` Ravenbloom Conservatory for P2, then starts from a focused midgame `START_BATTLE` state with P1 `UNL-057/219` Wildclaw Beastmaster, P2 `UNL-036/219` Mutant Kitten defender, and a second official `UNL-036/219` Mutant Kitten on top of P2's controlled main deck. The server-authored `DECLARE_BATTLE` route emits `BATTLEFIELD_TRIGGER_RESOLVED`, `CARDS_REVEALED`, and `CARDS_RECYCLED`, recognizes the revealed card is not a spell, recycles it to the bottom of P2's main deck, and continues through score-victory action-log replay to the same final state hash. This slice changes only test / evidence coverage; it does not close complete reveal / recycle hidden-info breadth, complete battlefield FUs, complete official deck archetype breadth, or READY.

The Plunder Alley defend move-friendly-unit-to-base action-log replay regression proves a legal official deck opening can feed a focused defended-battlefield movement route. `OfficialDeckMidgameResolvesPlunderAlleyDefendMoveToBaseAndScoreVictoryActionLogReplaysToFinalStateHash` records legal Jhin and Vex deck openings that selected official `OGN·285/298` Plunder Alley for P2, then starts from a focused midgame `START_BATTLE` state with P1 `OGN·096/298` Watchful Sentinel and P2 `UNL-057/219` Wildclaw Beastmaster at that battlefield. The server-authored `DECLARE_BATTLE` route submits the defender through `battlefieldTargetObjectIds`, emits `BATTLEFIELD_TRIGGER_RESOLVED` and `UNIT_MOVED_TO_BASE`, moves the selected surviving friendly defender from battlefield to P2 base through the parsed `BATTLEFIELD_DEFENSE_MOVE_FRIENDLY_UNIT_TO_BASE` route, and continues through score-victory action-log replay to the same final state hash. This slice changes only test / evidence coverage; it does not close complete optional yes/no trigger prompts, complete movement / control-zone edge cases, complete battlefield FUs, complete official deck archetype breadth, or READY.

The Rehearsal Hall held move-unit-to-base action-log replay regression proves a legal official deck opening can feed a focused held-battlefield movement route and still reach score victory. `OfficialDeckMidgameResolvesRehearsalHallHeldMoveUnitToBaseAndScoreVictoryActionLogReplaysToFinalStateHash` records legal Jhin and Vex deck openings that selected official `UNL-207/219` Rehearsal Hall for P2, then starts from a focused midgame `START_BATTLE` state with P1 `OGN·096/298` Watchful Sentinel and P2 `UNL-057/219` Wildclaw Beastmaster at that battlefield. The server-authored `DECLARE_BATTLE` route emits `BATTLEFIELD_TRIGGER_RESOLVED` and `UNIT_MOVED_TO_BASE`, moves the surviving defender from battlefield to P2 base through the parsed `BATTLEFIELD_HELD_MOVE_UNIT_TO_BASE` route, then continues through effective-controller turn-start battlefield scoring and score-victory action-log replay to the same final state hash. Runtime changed in `CoreRuleEngine.ApplyBattlefieldHeldScoresAtTurnStart` to score official battlefield objects by shared effective field controller rather than only explicit `ControllerId`.

The prevent move-to-base rejected-command action-log replay regression proves a legal official deck opening can feed a focused battlefield movement restriction into the B0 score-victory path. `OfficialDeckMidgameRejectsBattlefieldPreventMoveToBaseAndScoreVictoryActionLogReplaysToFinalStateHash` records a legal Vex deck opening that selected official `OGN·295/298`, then starts from a focused midgame movement state with P1 `UNL-057/219` Wildclaw Beastmaster at that battlefield. The server-authored `MOVE_UNIT` prompt metadata contains no `BATTLEFIELD_TO_BASE` source requirement for that unit; a direct battlefield-to-base `MOVE_UNIT` is rejected with `ErrorCodes.InvalidTarget`, emits no events, keeps the same state hash and battlefield location, then the same journal continues through score-victory action-log replay to the same final state hash including that rejected command. This slice changes only test / evidence coverage; it does not close complete same-turn movement policy, complete movement / control-zone edge cases, complete battlefield lifecycle breadth, complete battlefield FUs, complete official deck archetype breadth, or READY.

The prevent unit-play rejected-command action-log replay regression proves a legal official deck opening can feed a focused battlefield play restriction into the B0 score-victory path. `OfficialDeckMidgameRejectsBattlefieldPreventUnitPlayAndScoreVictoryActionLogReplaysToFinalStateHash` records a legal Vex deck opening that selected official `SFD·216/221`, then starts from a focused midgame play state with P1 `UNL-057/219` Wildclaw Beastmaster in hand and enough mana to play it normally. The server-authored `PLAY_CARD` prompt metadata omits `BATTLEFIELD:<SFD·216 object>` from that source's destination choices; a direct `PLAY_CARD` to the prohibited battlefield is rejected with `ErrorCodes.InvalidTarget`, emits no events, preserves hand, rune pool and stack state, then the same journal continues through score-victory action-log replay to the same final state hash including that rejected command. This slice changes only test / evidence coverage; it does not close complete play destination policy, complete timing-window breadth, complete battlefield lifecycle breadth, complete battlefield FUs, complete official deck archetype breadth, or READY.

The Back Alley Bar moved-unit power action-log replay regression now proves a legal official deck opening can feed the parsed movement trigger into the B0 score-victory path. `OfficialDeckMidgameResolvesBackAlleyBarMovedUnitPowerAndScoreVictoryActionLogReplaysToFinalStateHash` records a legal Vex deck opening that selected official `OGN·277/298` Back Alley Bar, then starts from a focused midgame movement state with P1 `UNL-057/219` Wildclaw Beastmaster at that battlefield. The server-authored `MOVE_UNIT` route moves Wildclaw Beastmaster from battlefield to base, emits `BATTLEFIELD_TRIGGER_RESOLVED` with `trigger=BATTLEFIELD_UNIT_MOVED_AWAY_POWER_MODIFIER`, applies the parsed until-end-of-turn power modifier ledger entry to the moved unit, then continues through score-victory action-log replay to the same final state hash. This slice changes only test / evidence coverage; it does not close complete same-turn movement policy, complete movement / control-zone edge cases, complete battlefield lifecycle breadth, complete battlefield FUs, complete official deck archetype breadth, or READY.

The Piltover Academy held-next-spell Echo action-log replay regression now proves a legal official deck opening can feed the parsed held marker into a later spell stack that reaches score victory. `OfficialDeckMidgameResolvesPiltoverAcademyHeldNextSpellEchoAndScoreVictoryActionLogReplaysToFinalStateHash` records legal Poppy and Vex deck openings that selected official `UNL-216/219` Piltover Academy for P2, then starts from a focused midgame `START_BATTLE` state with P1 `OGN·096/298` Watchful Sentinel and P2 `UNL-034/219` Crimson Signet Treant at that battlefield. The server-authored `DECLARE_BATTLE` route emits `BATTLEFIELD_TRIGGER_RESOLVED`, stores `BATTLEFIELD_HELD_NEXT_SPELL_GAINS_ECHO:P2`, and replays the marker path. The derived P2 neutral-open next-spell window then submits official `UNL-007/219` Punishment with the granted Echo optional cost, pays 4 mana from base cost 2 plus Echo 2, consumes the marker, records repeat count 2, resolves the repeated spell stack, and continues through score-victory action-log replay to the same final state hash. This slice changes only test / evidence coverage; it does not close natural same-turn non-active spell access, Swift stack-response `PLAY_CARD` prompts, complete Echo optional prompt breadth, complete battlefield FUs, complete official deck archetype breadth, or READY.

The Frost Hold turn-start damage action-log replay regression now proves a legal official deck opening can feed the parsed turn-start battlefield trigger into score victory. `OfficialDeckMidgameResolvesFrostHoldTurnStartDamageAndScoreVictoryActionLogReplaysToFinalStateHash` records legal Vex official deck openings that select official `UNL-212/219` Frost Hold for P1, then starts from a focused midgame main phase with both players' official `UNL-057/219` Wildclaw Beastmasters at that battlefield. The server-authored `END_TURN` route advances to P2's turn start, emits `BATTLEFIELD_TRIGGER_RESOLVED` with `BATTLEFIELD_TURN_START_DAMAGE_ALL_UNITS`, applies one damage to both same-battlefield units before scoring, and continues through score-victory action-log replay to the same final state hash. This slice changes only test / evidence coverage; it does not add a runtime card-number branch or close optional trigger prompts, complete turn-start battlefield breadth, complete battlefield FUs, complete official deck archetype breadth, or READY.

The Duskpetal Lab turn-start destroy/draw action-log replay regression now proves a legal official deck opening can feed the parsed optional turn-start battlefield trigger into score victory. `OfficialDeckMidgameResolvesDuskpetalLabTurnStartDestroyDrawAndScoreVictoryActionLogReplaysToFinalStateHash` records legal official Vex openings that select official `UNL-209/219` Duskpetal Lab for P2, then starts from a focused midgame main phase with one P2 official `UNL-057/219` Wildclaw Beastmaster at Duskpetal Lab and another P2 Wildclaw at a different battlefield. The server-authored `END_TURN` route advances to P2's turn start, emits `BATTLEFIELD_TRIGGER_RESOLVED` with `BATTLEFIELD_TURN_START_DESTROY_UNIT_DRAW`, destroys only the same-battlefield controlled unit, moves it to P2 graveyard, draws one P2 card before scoring, leaves the offsite controlled unit on the battlefield, and continues through score-victory action-log replay to the same final state hash. This slice changes only test / evidence coverage; it does not add a runtime card-number branch or close optional yes/no trigger prompts, complete turn-start battlefield breadth, complete battlefield FUs, complete official deck archetype breadth, or READY.

The Power Obelisk first-turn extra-rune action-log replay regression now proves a legal official deck opening can select the parsed first-turn extra-rune battlefield source and feed a focused first-turn replay into score victory. `OfficialDecksResolvePowerObeliskFirstTurnExtraRuneAndScoreVictoryActionLogReplaysToFinalStateHash` records legal Vex official openings that select official `OGN·284/298` Power Obelisk for P1, then derives a focused first-turn replay state from that official opening. The server-authored `END_TURN` route advances to P2's first turn start, emits `RUNES_CALLED` with `count=4`, decreases P2's rune deck by four, preserves hidden-zone snapshot boundaries, and continues through score-victory action-log replay to the same final state hash. This slice changes only test / evidence coverage; it does not add a runtime card-number branch or close natural earliest opening rune-call timing, complete turn-start battlefield breadth, complete battlefield FUs, complete official deck archetype breadth, or READY.

The Glory Arena first-turn score action-log replay regression now proves a legal official deck opening can select the parsed first-turn score battlefield source and feed a focused first-turn replay into score victory. `OfficialDecksResolveGloryArenaFirstTurnScoreAndScoreVictoryActionLogReplaysToFinalStateHash` records legal Vex official openings that select official `OGN·290/298` Glory Arena for P1, then derives a focused first-turn replay state from that official opening. The server-authored `END_TURN` route advances to P2's first turn start, emits `BATTLEFIELD_TRIGGER_RESOLVED` and `SCORE_GAINED` with `BATTLEFIELD_FIRST_TURN_GAIN_SCORE`, grants one score to P2 before continuing the game, preserves hidden-zone snapshot boundaries, and continues through score-victory action-log replay to the same final state hash. This slice changes only test / evidence coverage; it does not add a runtime card-number branch or close natural earliest opening score timing, complete turn-start battlefield breadth, complete battlefield FUs, complete official deck archetype breadth, or READY.

The Forgotten Monument score-delay action-log replay regression now proves a legal official deck opening can select the parsed score-delay battlefield source and feed a focused first-turn replay into score victory. `OfficialDecksResolveForgottenMonumentScoreDelayAndScoreVictoryActionLogReplaysToFinalStateHash` records legal Vex official openings that select official `SFD·209/221` Forgotten Monument for P1, then derives a focused first-turn replay state from that official opening with official `OGN·290/298` Glory Arena as the first-turn score source. The server-authored `END_TURN` route advances to P2's first turn start, emits `BATTLEFIELD_SCORE_PREVENTED` with `BATTLEFIELD_SCORE_DELAY_UNTIL_THIRD_TURN`, records `preventedReason=BATTLEFIELD_FIRST_TURN_GAIN_SCORE`, keeps P2 at zero score for that step, preserves hidden-zone snapshot boundaries, and continues through score-victory action-log replay to the same final state hash. This slice changes only test / evidence coverage; it does not add a runtime card-number branch or close complete physical `此处` score scoping, natural earliest opening score timing, complete turn-start battlefield breadth, complete battlefield FUs, complete official deck archetype breadth, or READY.

The winning-score increase action-log replay regression now proves a legal official deck opening can select the parsed winning-score static battlefield source and feed a focused first-turn replay into score victory. `OfficialDecksResolveWinningScoreIncreaseAndScoreVictoryActionLogReplaysToFinalStateHash` records legal Vex official openings that select official `OGN·276/298` for P1, then derives a focused first-turn replay state from that official opening with official `OGN·290/298` Glory Arena as the first-turn score source and P2 starting on seven score. The server-authored `END_TURN` route advances to P2's first turn start, projects `winningScore=9`, emits the first-turn `SCORE_GAINED` that takes P2 to eight without `MATCH_WON`, preserves hidden-zone snapshot boundaries, then continues through score-victory action-log replay to a final `MATCH_WON` with `winningScore=9` and the same final state hash. This slice changes only test / evidence coverage; it does not add a runtime card-number branch or close multiple-source stacking breadth, natural earliest opening score timing, complete battlefield FUs, complete official deck archetype breadth, or READY.

The Vaults of Helia held unit-cost action-log replay regression proves a legal official deck opening can feed the BehaviorSpec-backed held-battlefield cost marker into a later server-authored play prompt. `OfficialDeckMidgameAppliesVaultsOfHeliaHeldUnitCostIncreaseAndScoreVictoryActionLogReplaysToFinalStateHash` records a legal Poppy deck opening that selected official `UNL-219/219` Vaults of Helia for P1, then starts from a focused midgame main phase with `BATTLEFIELD_HELD_NON_TOKEN_UNIT_COST_INCREASE:P1` active and official `OGN·211/298` Loyal Craftsman in P1 hand. The server-authored `PLAY_CARD` prompt exposes Loyal Craftsman with `manaCost=3`, `minimumManaCost=4`, and `battlefieldHeldUnitCostIncreaseMana=1`; submitting the play pays 4 mana, emits `COST_PAID` with the parsed held-unit-cost surcharge, adds the unit stack item, resolves it, and continues through score-victory action-log replay to the same final state hash. This slice changes only test / evidence coverage plus a catalog reason string; it does not close complex multi-modifier payment stacking, token/non-token breadth, complete battlefield FUs, complete official deck archetype breadth, or READY.

The Dunehorn Beast unit battlefield-held draw action-log replay regression proves a legal official deck opening can feed a unit-source held trigger into the B0 score-victory route. `OfficialDeckMidgameResolvesDunehornBeastUnitHeldDrawAndScoreVictoryActionLogReplaysToFinalStateHash` records legal Jhin deck openings, then starts from a focused midgame `START_BATTLE` state with P2 official `SFD·027/221` Dunehorn Beast defending a slow battlefield against P1 `OGN·096/298` Watchful Sentinel. The server-authored `DECLARE_BATTLE` route leaves Dunehorn Beast as the surviving holder, emits `TRIGGER_RESOLVED` with `trigger=UNIT_BATTLEFIELD_HELD_DRAW`, `sourceCardNo=SFD·027/221`, and `drawCount=2`, draws two controlled main-deck cards for P2, then continues through score-victory action-log replay to the same final state hash. The low-hand active-entry sentence has separate StaticAbilitySpec coverage and now also has a B0 official-deck replay below; this B0 slice still does not close complete unit-held trigger breadth, complete battlefield FUs, complete official deck archetype breadth, or READY.

The Dunehorn Beast low-hand active-entry action-log replay regression proves a legal official deck opening can feed `SOURCE_UNIT_ENTER_READY` into the B0 score-victory route. `OfficialDeckMidgameResolvesDunehornBeastLowHandActiveEntryAndScoreVictoryActionLogReplaysToFinalStateHash` records a legal Jhin deck opening, then starts from a focused midgame play state with P1 official `SFD·027/221` Dunehorn Beast in hand plus two other cards. The server-authored `PLAY_CARD` to a P1 battlefield leaves the controller with two hand cards, resolves Dunehorn Beast ready, emits `UNIT_PLAYED_TO_BATTLEFIELD` with `entryStaticAbilityKind=SOURCE_UNIT_ENTER_READY`, self source object/card metadata, `isExhausted=false`, and continues through score-victory action-log replay to the same final state hash with hidden-info guards. This slice changes only test / evidence coverage; it does not add a runtime card-number branch or close complete active-entry family breadth, complete official deck archetype breadth, P0 full objective, or READY.

The Molten Drake other-friendly active-entry action-log replay regression proves a legal official deck opening can feed `OTHER_FRIENDLY_UNITS_ENTER_READY` into the B0 score-victory route. `OfficialDeckMidgameResolvesMoltenDrakeOtherFriendlyActiveEntryAndScoreVictoryActionLogReplaysToFinalStateHash` records a legal Jhin deck opening, then starts from a focused midgame play state with public face-up P1 official `OGN·011/298` Molten Drake in base and official `OGN·010/298` Legion Rearguard in hand. The server-authored `PLAY_CARD` to a P1 battlefield pays only Legion Rearguard's base cost, does not pay `HASTE_READY`, resolves Legion Rearguard ready from Molten Drake, emits `UNIT_PLAYED_TO_BATTLEFIELD` with `entryStaticAbilityKind=OTHER_FRIENDLY_UNITS_ENTER_READY`, Molten Drake source object/card metadata, `isExhausted=false`, and continues through score-victory action-log replay to the same final state hash with hidden-info guards. This slice changes only test / evidence coverage; it does not add a runtime card-number branch or close complete active-entry family breadth, complete official deck archetype breadth, P0 full objective, or READY.

The Master Yi level active-entry action-log replay regression proves a legal official Master Yi level deck opening can feed `FRIENDLY_UNITS_ENTER_READY` into the B0 score-victory route. `OfficialDeckMidgameResolvesMasterYiLevelActiveEntryAndScoreVictoryActionLogReplaysToFinalStateHash` records a legal Master Yi level deck opening, then starts from a focused midgame play state with P1 `UNL-191/219` in the legend zone, P1 at 11 experience, and official `UNL-092/219` Demacia Envoy in hand. The server-authored `PLAY_CARD` to a P1 battlefield pays only Demacia Envoy's base cost, does not pay `HASTE_READY`, resolves Demacia Envoy ready from the legend-source static ability, emits `UNIT_PLAYED_TO_BATTLEFIELD` with `entryStaticAbilityKind=FRIENDLY_UNITS_ENTER_READY`, Master Yi source object/card metadata, `isExhausted=false`, and continues through score-victory action-log replay to the same final state hash with hidden-info guards. This slice changes only test / evidence coverage; it does not add a runtime card-number branch or close complete active-entry family breadth, complete official deck archetype breadth, P0 full objective, or READY.

The Card Trick draw/recycle action-log replay regression proves a legal official Vex deck opening can feed official `OGN·183/298` Card Trick into the B0 score-victory route and expose a real private-zone stack-target redaction issue. `OfficialDeckMidgameResolvesCardTrickDrawRecycleAndScoreVictoryActionLogReplaysToFinalStateHash` records a legal Vex deck opening, then starts from a focused midgame play state with Card Trick in P1 hand and controlled top-three main-deck objects. The server-authored `PLAY_CARD` prompt exposes exactly those top-three target choices to P1, the stack resolves by moving the selected card to P1 hand and recycling the unselected top cards to the bottom of P1's main deck, and the command stream continues through score-victory action-log replay to the same final state hash. The shared runtime change is hidden-information scoped: `MatchSession` now projects stack `targetObjectIds` through viewer visibility redaction for hand / main-deck / rune-deck / hidden-standby targets, and `MatchRecovery` validates spectator stack target ids against the same redacted projection. This slice does not add a runtime card-number branch or close complete main-deck look/recycle breadth, complete hidden-information matrix, complete spell target breadth, P0 full objective, or READY.

The Flowing Time Mirror ephemeral-cleanup action-log replay regressions prove the existing legal Lost Library B0 route also carries official `OGN·180/298` through the shared `瞬息` start-of-turn lifecycle for both printed target shapes and the LeBlanc suppression representative. `OfficialDeckMidgameResolvesLostLibraryHighCostSpellInsightAndScoreVictoryActionLogReplaysToFinalStateHash` records the unit target before play, verifies the spell gives P2's same-battlefield `OGN·096/298` Watchful Sentinel the `瞬息` tag, then continues through `END_TURN` until P2's turn start. The match journal must contain exactly one `UNIT_DESTROYED` event for that unit target from an `END_TURN` command with `reason=EPHEMERAL_TURN_START`, `ownerPlayerId=P2`, `destroyedByPlayerId=P2`, and `destinationZone=GRAVEYARD`. `OfficialDeckMidgameResolvesFlowingTimeMirrorEquipmentEphemeralCleanupAndScoreVictoryActionLogReplaysToFinalStateHash` records the equipment target route from a legal official Vex vs Rumble opening, verifies server prompt legality for official `SFD·022/221` Long Sword as public equipment, resolves `OBJECT_TAG_ADDED(tag=瞬息)`, then proves P2's next turn-start `END_TURN` emits exactly one `EQUIPMENT_DESTROYED` with `reason=EPHEMERAL_TURN_START` and moves the equipment to P2 graveyard. `OfficialDeckMidgameResolvesFlowingTimeMirrorLeblancSuppressedEphemeralCleanupAndScoreVictoryActionLogReplaysToFinalStateHash` records the same official spell from a legal Vex vs Lillia opening with P2 `UNL-090/219` LeBlanc and `OGN·096/298` Watchful Sentinel at the same battlefield; it proves P2's next turn-start emits no `UNIT_DESTROYED.reason=EPHEMERAL_TURN_START` for the protected target and then replays through score victory. This slice changes only test / evidence coverage; it does not add runtime behavior or close all ephemeral token/equipment lifecycle edges, P0 full objective, or READY.

The Forbidden Wasteland battlefield isolated-defender RULE_TEXT keyword-modifier action-log replay regression proves a legal official deck opening can feed a battlefield-source keyword modifier from `BehaviorSpec.StaticAuras`. `OfficialDeckMidgameAppliesBattlefieldIsolatedDefenderKeywordModifierAndScoreVictoryActionLogReplaysToFinalStateHash` records legal official Vex and Rumble deck openings, probes until P2 selects official `UNL-210/219` Forbidden Wasteland, then starts the replay from a focused midgame `START_BATTLE` state with `UNL-057/219` Wildclaw Beastmaster and `UNL-090/219` LeBlanc at that P2 battlefield. The projected `BATTLEFIELD_ISOLATED_DEFENDER_KEYWORD_MODIFIER` RULE_TEXT route grants the only defender `坚守` with `keywordBonus=-2`, real defender damage records `basePower=4`, `combatPower=2`, and `damage=2`, and the score-victory action log replays to the same final state hash. `BattlefieldIsolatedDefenderKeywordModifierProjectionTests` separately proves the continuous effect projects for exactly one public defender and does not project when two defenders share the battlefield.

The source-lone-battle static-aura action-log replay regression proves a legal official deck can carry Waterbender's battle-conditional source-object `STATIC_AURA` through the B0 full-game route. `OfficialDeckMidgameAppliesSourceLoneBattleStaticAuraAndScoreVictoryActionLogReplaysToFinalStateHash` records legal official Lillia decks containing `OGN·055/298` Waterbender and `OGN·096/298` Watchful Sentinel, follows the normal official opening seed path, stages both units through server-authored `PLAY_CARD` / `MOVE_UNIT` prompts, submits a server-authorized `DECLARE_BATTLE` with Waterbender as the only attacker, verifies the projected `SOURCE_LONE_BATTLE_POWER` effect targets Waterbender itself, observes Waterbender's real `DAMAGE_APPLIED` with `basePower=2`, `staticPowerBonus=2`, `combatPower=4`, and `damage=4`, then continues through score-victory replay to the same final state hash. This slice changes only test / evidence coverage; it does not close complete source-lone-battle static-aura breadth, complete official deck archetype breadth, or READY.

The friendly single-defender static-aura action-log replay regression proves a legal official deck can carry Master Yi intro's legend-source `STATIC_AURA` through the B0 full-game route. `OfficialDeckMidgameAppliesFriendlySingleDefenderStaticAuraAndScoreVictoryActionLogReplaysToFinalStateHash` records a legal official Master Yi intro deck containing `OGS·019/024` Master Yi intro as the legend and `UNL-092/219` Demacia Envoy as the single friendly defender, follows the normal official opening seed path, stages Demacia Envoy through server-authored `PLAY_CARD` / `MOVE_UNIT` prompts to the opposing battlefield, submits a server-authorized `DECLARE_BATTLE` from the battlefield owner, observes the Envoy's real defender `DAMAGE_APPLIED` with `basePower=2`, `staticPowerBonus=2`, `combatPower=4`, and `damage=4`, then continues through score-victory replay to the same final state hash. This slice changes only test / evidence coverage; it does not close complete legend-source static-aura breadth, complete official deck archetype breadth, or READY.

The Master Yi level friendly-units static-aura action-log replay regression proves a legal official deck can carry an experience-gated legend-source `STATIC_AURA` through the B0 full-game route. `OfficialDeckMidgameAppliesMasterYiLevelFriendlyUnitsStaticAuraAndScoreVictoryActionLogReplaysToFinalStateHash` records a legal official Master Yi level deck containing `UNL-191/219` as the legend and `UNL-092/219` Demacia Envoy, follows the normal official opening seed path, starts the midgame route at 5 experience, uses Demacia Envoy's server-resolved on-play text to gain the sixth experience, verifies the legend-source `FRIENDLY_UNITS_POWER` continuous effect targets the Envoy with `SourcePath=CoreRuleEngine.ResolveFriendlyUnitsPowerBonus`, submits a server-authorized `DECLARE_BATTLE`, observes the Envoy's real attacker `DAMAGE_APPLIED` with `basePower=2`, `staticPowerBonus=1`, `combatPower=3`, and `damage=3`, then continues through score-victory replay to the same final state hash. This slice changes only test / evidence coverage; it does not close Master Yi `{{等级11>}}`, complete experience-gated legend-source static-aura breadth, complete official deck archetype breadth, or READY.

The Wise Elder source-object filtered static-aura action-log replay regression proves a legal official deck can carry a self-conditioned source-object `STATIC_AURA` through the B0 full-game route. `OfficialDeckMidgameAppliesWiseElderSourceObjectFilteredStaticAuraAndScoreVictoryActionLogReplaysToFinalStateHash` records a legal official Master Yi level deck containing `OGN·065/298` Wise Elder and `OGN·136/298` Arena Rookie, follows the normal official opening seed path, stages Wise Elder to a battlefield, uses Arena Rookie's server-authored targeted `PLAY_CARD` prompt to grant Wise Elder `{{增益}}`, verifies Wise Elder's `SOURCE_OBJECT_FILTERED_POWER` continuous effect targets itself with `SourcePath=CoreRuleEngine.ResolveSourceObjectFilteredPowerBonus`, submits a server-authorized `DECLARE_BATTLE`, observes Wise Elder's real attacker `DAMAGE_APPLIED` with boon-adjusted `basePower=5`, `staticPowerBonus=1`, `combatPower=6`, and `damage=6`, then continues through score-victory replay to the same final state hash. This slice changes only test / evidence coverage; it does not close complete source-object filtered static-aura breadth, complete official deck archetype breadth, or READY.

The battlefield all-units static-aura action-log replay regression proves a legal official deck can carry a public battlefield-source `STATIC_AURA` through the B0 full-game route. `OfficialDeckMidgameAppliesBattlefieldAllUnitsStaticAuraAndScoreVictoryActionLogReplaysToFinalStateHash` records legal official Vex decks whose battlefield set includes `OGN·294/298` Trifarian Training Grounds, probes official-opening seeds until P1 randomly selects that battlefield, stages `UNL-057/219` Wildclaw Beastmaster and an opposing defender to that battlefield through server-authored `PLAY_CARD` / `MOVE_UNIT` prompts, verifies the battlefield's `BATTLEFIELD_ALL_UNITS_POWER_PLUS_ONE` projection targets both attacker and defender, observes Wildclaw's real `DAMAGE_APPLIED` with `basePower=7`, `staticPowerBonus=1`, `combatPower=8`, and `damage=8`, then continues through score-victory replay to the same final state hash. This slice changes only test / evidence coverage; it does not close complete battlefield static-aura breadth, complete official deck archetype breadth, or READY.

The battlefield all-units static-keyword action-log replay regression proves a legal official deck can carry a public battlefield-source `RULE_TEXT` keyword aura into a real movement command. `OfficialDeckMidgameProjectsBattlefieldAllUnitsStaticKeywordRoamAndScoreVictoryActionLogReplaysToFinalStateHash` records legal official Vex decks whose battlefield set includes `OGN·297/298` Wind Hill / 疾风山丘, probes official-opening seeds until P1 randomly selects that battlefield, stages `UNL-057/219` Wildclaw Beastmaster to Wind Hill through server-authored `PLAY_CARD` / `MOVE_UNIT` prompts, verifies the battlefield's `BATTLEFIELD_ALL_UNITS_KEYWORD` projection grants `游走` without adding a printed tag, submits the server-authored precise battlefield `MOVE_UNIT` from Wind Hill to a second official P1 battlefield with optional cost `ROAM`, then continues through score-victory replay to the same final state hash. This slice changes only test / evidence coverage; it does not close complete battlefield RULE_TEXT keyword breadth, complete official deck archetype breadth, or READY.

The Void Gate target spell/skill damage bonus action-log replay regression proves a legal official deck can carry a B4 battlefield static ability through a real spell stack. `OfficialDeckMidgameResolvesVoidGateTargetSpellSkillDamageBonusAndScoreVictoryActionLogReplaysToFinalStateHash` records legal official Jhin and Vex decks whose P2 battlefield set includes `OGN·296/298` Void Gate / 虚空之门, probes official-opening seeds until P2 randomly selects that battlefield, stages `UNL-057/219` Wildclaw Beastmaster to Void Gate, submits server-authored official `UNL-007/219` Punishment from P1 against that public battlefield target, resolves the stack, verifies the parsed `BATTLEFIELD_TARGET_SPELL_SKILL_DAMAGE_BONUS` path increases damage from 3 to 4, then continues through score-victory replay to the same final state hash. This slice changes only test / evidence coverage; it does not close complete spell/skill damage modifier timing edges, multi-target damage breadth, replacement ordering, full B4, complete official deck archetype breadth, or READY.

The Mutation Garden granted unit-experience action-log replay regression proves a legal official deck can carry a B4 battlefield static granted ability through the real `ACTIVATE_ABILITY` prompt path. `OfficialDeckMidgameResolvesMutationGardenGrantedUnitExperienceAndScoreVictoryActionLogReplaysToFinalStateHash` records legal official Vex decks whose battlefield set includes `UNL-213/219` Mutation Garden / 蜕变花园, probes official-opening seeds until P1 randomly selects that battlefield, stages `UNL-057/219` Wildclaw Beastmaster to Mutation Garden, submits the server-authored `BATTLEFIELD_UNIT_EXHAUST_GAIN_EXPERIENCE` activation, verifies the source unit exhausts, `BATTLEFIELD_TRIGGER_RESOLVED.amount = 1`, `EXPERIENCE_GAINED.totalExperience = 1`, then continues through score-victory replay to the same final state hash. This slice changes only test / evidence coverage; it does not close full activated ability modeling for granted abilities, complete experience spending / threshold breadth, complete B4, complete official deck archetype breadth, or READY.

The Marai Spire Echo cost-reduction action-log replay regression proves a legal official deck can carry a B4 battlefield static cost modifier through the real `PLAY_CARD` optional-cost prompt path. `OfficialDeckMidgameResolvesMaraiSpireEchoCostReductionAndScoreVictoryActionLogReplaysToFinalStateHash` records legal official Jhin decks whose battlefield set includes `SFD·211/221` Marai Spire / 玛莱尖塔, probes official-opening seeds until P1 randomly selects that battlefield, stages official `UNL-061/219` Center Stage in hand with only 3 mana, submits the server-authored `ECHO` optional cost after prompt metadata exposes the battlefield reduction reason, verifies `COST_PAID.mana = 3`, `baseMana = 2`, `battlefieldEchoCostReductionMana = 1`, stack `effectRepeatCount = 2`, final Center Stage in graveyard, two-card draw state movement, then continues through score-victory replay to the same final state hash. This slice changes only test / evidence coverage; it does not close complex Echo costs, all optional-cost/payment-resource combinations, complete B4, complete official deck archetype breadth, or READY.

The Ornn's Forge equipment cost-reduction action-log replay regression proves a legal official deck can carry a B4 battlefield static cost modifier through the real `PLAY_CARD` equipment prompt path. `OfficialDeckMidgameResolvesOrnnForgeEquipmentCostReductionAndScoreVictoryActionLogReplaysToFinalStateHash` records legal official Rumble decks whose battlefield set includes `SFD·213/221` Ornn's Forge / 奥恩的锻炉, probes official-opening seeds until P1 randomly selects that battlefield, stages official `SFD·022/221` Long Sword in hand with only 1 mana and official `SFD·006/221` Aggressive Dragonhound in base as the controlled attachment target, verifies `PLAY_CARD.sourceRequirements` exposes `manaCost = 2`, `minimumManaCost = 1`, and `battlefieldEquipmentCostReductionMana = 1`, verifies `COST_PAID.mana = 1`, `baseMana = 2`, `battlefieldEquipmentCostReductionMana = 1`, records `PLAYED_EQUIPMENT_THIS_TURN:P1`, resolves Long Sword onto the controlled unit, then continues through score-victory replay to the same final state hash. This slice changes only test / evidence coverage; it does not close complex equipment costs, all payment-resource combinations, complete equipment attachment lifecycle breadth, complete B4, complete official deck archetype breadth, or READY.

The Poro Forge legend attach-armament action-log replay regression proves a legal official deck can carry a B4 battlefield-granted legend action through the real `LEGEND_ACT` prompt path. `OfficialDeckMidgameResolvesPoroForgeLegendAttachArmamentAndScoreVictoryActionLogReplaysToFinalStateHash` records legal official Rumble decks whose battlefield set includes `SFD·208/221` Poro Forge / 魄罗熔炉, probes official-opening seeds until P1 randomly selects that battlefield, stages official `SFD·181/221` Rumble legend as a ready known source, official `SFD·006/221` Aggressive Dragonhound in base as the controlled unit target, and official `SFD·022/221` Long Sword in base as a controlled `武装`. The server-authored `LEGEND_ACT` prompt exposes `LEGEND_EXHAUST_ATTACH_CONTROLLED_ARMAMENT_FROM_BATTLEFIELD` plus indexed target choices for the controlled unit and armament; submitting it exhausts the legend, emits `LEGEND_ABILITY_ACTIVATED`, `LEGEND_EXHAUSTED`, `BATTLEFIELD_TRIGGER_RESOLVED.trigger = BATTLEFIELD_CONTROLLED_LEGEND_ATTACH_ARMAMENT`, and `EQUIPMENT_ATTACHED`, attaches Long Sword to the controlled unit, then continues through score-victory replay to the same final state hash. This slice changes only test / evidence coverage plus `FullGameEndToEndTests.RawCommand` replay serialization for `LegendActCommand`; it does not close full activated ability modeling for granted abilities, complete armament attachment lifecycle breadth, complete B4, complete official deck archetype breadth, or READY.

The Poro Forge rejected-command action-log replay regression proves the command-side guard is also recoverable from a legal official deck opening. `OfficialDeckMidgameRejectsPoroForgeLegendAttachArmamentWithoutControlledForgeAndScoreVictoryActionLogReplaysToFinalStateHash` records legal official Rumble decks whose battlefield set intentionally lacks `SFD·208/221` Poro Forge, verifies a legal official opening, then stages the same ready `SFD·181/221` Rumble legend, official `SFD·006/221` Aggressive Dragonhound in base, and official `SFD·022/221` Long Sword in base as a controlled `武装`. The server-authored `LEGEND_ACT` prompt does not expose `LEGEND_EXHAUST_ATTACH_CONTROLLED_ARMAMENT_FROM_BATTLEFIELD`; direct submission is rejected with `ErrorCodes.UnsupportedCardBehavior`, emits no events, preserves state hash, keeps the legend ready, keeps Long Sword unattached, then continues through score-victory replay to the same final state hash including that rejected command. This slice changes only test / evidence coverage; it does not close full activated ability modeling for granted abilities, complete armament attachment lifecycle breadth, complete B4, complete official deck archetype breadth, or READY.

The Blood Altar battle-destroyed recall action-log replay regression proves a legal official deck opening can feed the `BATTLEFIELD_DESTROYED_IN_BATTLE_PAY_3_RECALL` replacement path through real battle prompts. `OfficialDeckMidgameResolvesBloodAltarBattleDestroyedRecallAndScoreVictoryActionLogReplaysToFinalStateHash` records a legal Vex attacker deck and legal Rumble defender deck whose battlefield set includes `UNL-206/219` Blood Altar / 鲜血祭坛, probes official-opening seeds until P2 selects Blood Altar, then starts from a focused midgame `START_BATTLE` state with P1 official `UNL-057/219` Wildclaw Beastmaster and P2 official `OGN·096/298` Watchful Sentinel at that P2 battlefield. The server-authored `DECLARE_BATTLE` route resolves combat damage through `ASSIGN_COMBAT_DAMAGE`, reads the battlefield static ability replacement, spends exactly 3 P2 mana, suppresses the defender's `UNIT_DESTROYED`, emits `BATTLEFIELD_TRIGGER_RESOLVED`, `COST_PAID`, and `UNIT_RECALLED_TO_BASE`, returns the defender to P2 base exhausted with damage removed, then continues through score-victory replay to the same final state hash. This slice changes only test / evidence coverage; it does not close optional replacement prompt choice breadth, complete battle-destroyed replacement ordering, complete battlefield lifecycle breadth, complete B4, complete official deck archetype breadth, or READY.

The source same-location static-aura action-log replay regression proves a legal official deck can carry a threshold source-object `STATIC_AURA` through the B0 full-game route. `OfficialDeckMidgameAppliesSourceSameLocationStaticAuraAndScoreVictoryActionLogReplaysToFinalStateHash` records legal official Poppy decks containing `SFD·159/221` Reliable Siege Dog and `UNL-092/219` Demacia Envoy, follows the normal official opening seed path, stages both friendly units and an opposing defender through server-authored `PLAY_CARD` / `MOVE_UNIT` prompts, verifies Reliable Siege Dog's `SOURCE_SAME_LOCATION_OTHER_FRIENDLY_UNIT_POWER` projection targets itself with the Envoy as a same-location participant, observes Reliable Siege Dog's real `DAMAGE_APPLIED` with `basePower=2`, `staticPowerBonus=1`, `combatPower=3`, and `damage=3`, then continues through score-victory replay to the same final state hash. This slice changes only test / evidence coverage; it does not close complete same-location / count-to-source static-aura breadth, complete official deck archetype breadth, or READY.

The same-battlefield boon count-to-source static-aura action-log replay regression proves a legal official deck can carry a filtered count-to-source `STATIC_AURA` through the B0 full-game route. `OfficialDeckMidgameAppliesSameBattlefieldBoonCountStaticAuraAndScoreVictoryActionLogReplaysToFinalStateHash` records legal official Poppy decks containing `OGN·240/298` Sett, `OGN·136/298` Arena Rookie, and `UNL-092/219` Demacia Envoy, follows the normal official opening seed path, stages Demacia Envoy to a battlefield, uses Arena Rookie's server-authored targeted `PLAY_CARD` prompt to grant `{{增益}}` to Demacia Envoy, stages Sett to the same battlefield, verifies Sett's `SAME_BATTLEFIELD_FRIENDLY_FILTERED_UNIT_COUNT_TO_SOURCE_POWER` projection targets itself with the boon-bearing Envoy as participant, observes Sett's real `DAMAGE_APPLIED` with `basePower=5`, `staticPowerBonus=1`, `combatPower=6`, and `damage=6`, then continues through score-victory replay to the same final state hash. This slice changes only test / evidence coverage; it does not close complete count-to-source static-aura breadth, complete official deck archetype breadth, or READY.

The same-battlefield other-friendly filtered static-aura action-log replay regression proves a legal official deck can carry a filtered target `STATIC_AURA` through the B0 full-game route. `OfficialDeckMidgameAppliesSameBattlefieldOtherFriendlyFilteredStaticAuraAndScoreVictoryActionLogReplaysToFinalStateHash` records legal official Poppy decks containing `OGN·151/298` Lee Sin, `OGN·136/298` Arena Rookie, and `UNL-092/219` Demacia Envoy, follows the normal official opening seed path, stages Demacia Envoy to a battlefield, uses Arena Rookie's server-authored targeted `PLAY_CARD` prompt to grant `{{增益}}` to Demacia Envoy, stages Lee Sin to the same battlefield, verifies Lee Sin's `SAME_BATTLEFIELD_OTHER_FRIENDLY_FILTERED_UNITS_POWER` projection targets the boon-bearing Envoy and not Lee Sin itself, observes the Envoy's real `DAMAGE_APPLIED` with boon-adjusted `basePower=3`, `staticPowerBonus=2`, `combatPower=5`, and `damage=5`, then continues through score-victory replay to the same final state hash. This slice changes only test / evidence coverage; it does not close complete same-battlefield filtered static-aura breadth, complete official deck archetype breadth, or READY.

The same-battlefield static-keyword action-log replay regression proves a legal official deck can carry a data-driven `RULE_TEXT` keyword aura through the B0 full-game route. `OfficialDeckMidgameAppliesSameBattlefieldStaticKeywordAndScoreVictoryActionLogReplaysToFinalStateHash` records a legal official Jhin deck containing `OGN·015/298` Farron Captain and `UNL-004/219` Ascended Believer, follows the normal official opening seed path, stages both friendly units and an opposing defender through server-authored `PLAY_CARD` / `MOVE_UNIT` prompts, verifies Farron's `BehaviorSpec.StaticAuras` / `SAME_BATTLEFIELD_OTHER_FRIENDLY_UNITS_KEYWORD` projection targets the Believer with `强攻`, declares battle with the granted attacker, observes `DAMAGE_APPLIED` with `basePower=1`, `keyword=强攻`, `keywordBonus=1`, no `staticPowerBonus`, `combatPower=2`, and `damage=2`, then continues through score-victory replay to the same final state hash. This slice changes only test / evidence coverage; it does not close complete RULE_TEXT keyword aura breadth, complete official deck archetype breadth, or READY.

The same-battlefield Steadfast static-keyword action-log replay regression proves the defensive side of the same data-driven `RULE_TEXT` aura path in the B0 full-game route. `OfficialDeckMidgameAppliesSameBattlefieldSteadfastStaticKeywordAndScoreVictoryActionLogReplaysToFinalStateHash` records a legal official Lillia deck containing `OGN·074/298` Taric and `UNL-090/219` LeBlanc, stages both units to an opposing battlefield through server-authored `PLAY_CARD` / `MOVE_UNIT` prompts, verifies Taric projects `SAME_BATTLEFIELD_OTHER_FRIENDLY_UNITS_KEYWORD` to LeBlanc with `坚守`, lets the battlefield owner declare battle, observes defender `DAMAGE_APPLIED` with `basePower=4`, `keyword=坚守`, `keywordBonus=1`, no `staticPowerBonus`, `combatPower=5`, and `damage=5`, then continues through a follow-up server-authored battle declaration, a post-battle no-legal `BATTLE_SKIPPED`, and score-victory replay to the same final state hash. This slice also fixes the shared post-battle task advancement path so active `START_BATTLE` tasks after battle cleanup either expose the task player as active or use the existing no-legal battle skip path instead of leaving a WAIT-only blocker.

The Taric Bulwark damage-assignment replay proves the same official Lillia route also covers Taric's printed `壁垒` ordering in the server-authored `ASSIGN_COMBAT_DAMAGE` window. `OfficialDeckMidgameOrdersTaricBulwarkBeforeBackRowInDamageAssignmentAndScoreVictoryActionLogReplaysToFinalStateHash` records legal official Lillia decks containing `OGN·074/298` Taric, `UNL-090/219` LeBlanc, and `UNL-057/219` Wildclaw Beastmaster. The driver intentionally submits defenders as LeBlanc then Taric, while the prompt and runtime reorder legal targets to Taric first (`BULWARK_FIRST`) and LeBlanc last (`BACK_ROW_LAST`). The runtime fix makes assignment damage pools and lethal thresholds use battle effective power, so Taric and LeBlanc both expose lethal threshold 5 while defending with printed/granted `坚守`; Wildclaw's 7 damage assigns 5 to Taric then 2 to LeBlanc, and the score-victory action log replays to the same final state hash.

The standby reaction action-log replay regression proves a real priority-window standby reaction is recoverable from seated-room command logs. `OfficialDecksResolveStandbyReactionDuringShadowResponseActionLogReplaysToFinalStateHash` records legal official Vex decks containing `UNL-194/219` Shadow and `OGN·197/298` Teemo. The driver hides Teemo through prompt-derived `HIDE_CARD`, opens Shadow's battle-response stack, passes priority to the hidden-card controller, reveals Teemo through prompt-derived `REVEAL_CARD` with `Mode=STANDBY_REACTION` and `Destination=STACK`, resolves Teemo's on-play self-power modifier, then resolves Shadow and closes battle. This slice changes only test / evidence coverage; it does not add runtime rule behavior.

The distinct-deck regression proves the full-game score-victory path is not limited to two copies of the same deck. `DistinctOfficialLowCurveDecksReachScoreVictoryAfterRealBattleThroughServerPrompts` submits legal official Jhin and Rumble low-curve decks with different legend and champion cards, then drives the same server prompt path to real battle close and score-based `MATCH_WON`. This slice changes only the test driver / evidence: it parameterizes the deck pair and skips `待命` units when selecting the representative unit to play or move, so the B0 probe remains focused on battle / score instead of standby cleanup.

The standby-heavy regression proves that the same full-game score-victory route remains stable when one official low-curve deck includes standby-capable cards. `StandbyHeavyOfficialLowCurveDecksReachScoreVictoryAfterRealBattleThroughServerPrompts` submits distinct legal Jhin and Poppy low-curve decks, keeps representative unit selection on non-standby battle-path units, then drives setup, real battle close and score-based `MATCH_WON` through server-authored prompts. This slice changes only test / evidence coverage; it does not add runtime rule behavior or close full standby reveal / reaction mechanics.

The damage-assignment regression proves that a legal official deck pair can reach the server-authored multi-defender battle damage assignment window without a handcrafted battle fixture. `OfficialDecksResolveMultiDefenderBattleDamageAssignmentThroughServerPrompts` submits legal Lillia green/blue decks containing official `UNL-036/219` Mutant Kitten (`壁垒`) and `UNL-090/219` LeBlanc (`后排`), stages two invading units on one battlefield through server prompts, opens `BATTLE_DAMAGE_ASSIGNMENT_OPENED`, submits `ASSIGN_COMBAT_DAMAGE` for both players, and observes `DAMAGE_APPLIED` plus `BATTLE_CLOSED`. This slice changes only test / evidence coverage; it does not close complete combat damage assignment breadth.

The response-activation regression proves that a legal official deck pair can reach and use a server-authored battle response activation window without a handcrafted battle fixture. `OfficialDecksResolveShadowBattleResponseActivationThroughServerPrompts` submits legal Vex green/purple decks with `UNL-232/219` Vex legend, `UNL-055/219` Vex champion, and official `UNL-194/219` Shadow. The driver plays Shadow directly to a contested battlefield, opens `BATTLE_RESPONSE_PRIORITY_OPENED`, submits prompt-authored `ACTIVATE_ABILITY` with a quoted payment resource action when needed, resolves Shadow's stack item to apply `STUNNED`, returns to battle response priority, and closes battle with `BATTLE_RESPONSE_PRIORITY_CLOSED` plus `BATTLE_CLOSED`. This slice changes only test / evidence coverage; it does not close all response windows or the broader swift / reaction family.

The Swift spell-duel stack-priority regression proves that `STACK_PRIORITY` prompts can expose a server-authored `PLAY_CARD` candidate for an eligible official Swift spell while preserving Swift / Reaction window separation. `SwiftSpellPromptAndPlayCardAreLegalInSpellDuelStackPriorityWindow` starts from a minimal legal state where a pending stack item carries `TimingContext=SPELL_DUEL_OPEN`, priority is on P2, P2 has official `UNL-007/219` Punishment in hand, and a public battlefield unit is a legal target. The prompt exposes `PLAY_CARD` with only the current player's source and public target choices; submitting the command spends 2 mana, removes Punishment from hand, and adds a new `PUNISHMENT_DAMAGE_3` stack item that inherits `SPELL_DUEL_OPEN`. `P4PermissionKeywordTimingSeparatesSwiftReactionAndOrdinaryWindows` remains green, so ordinary `NEUTRAL_CLOSED` priority windows still reject Swift unless the pending stack context is spell-duel. This slice changes shared timing behavior and one official Behavior definition; it does not close ordinary priority Swift, complete Swift / Reaction timing, complete spell-duel lifecycle, all target-bearing Swift spells, or READY.

## Hidden Information Evidence

`FullGameEndToEndTests.AssertNoHiddenZoneLeak` checks exact JSON string values in each viewer snapshot after accepted full-game steps and rejects exposure of opponent hand, main-deck and rune-deck object ids without prefix false positives between similar object ids. The current guard is centralized through `FullGameEndToEndTests.AssertAccepted`, so every accepted result routed through the shared B0 helper performs this hidden-zone snapshot check immediately. The battlefield extra-standby regression also asserts the `CARD_HIDDEN` payload for the Bandle Tree destination omits `cardNo` while preserving the public battlefield destination. The Card Trick regression adds stack-target coverage: private-zone target ids in stack snapshots are redacted through the same viewer visibility path and spectator recovery now expects `HIDDEN` for those targets. This is a focused hidden-zone guard for the full-game probe and does not replace the broader `MatchRecovery` spectator validation suite.

## Validation

Latest Flowing Time Mirror / LeBlanc suppressed-cleanup focused validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~OfficialDeckMidgameResolvesFlowingTimeMirrorLeblancSuppressedEphemeralCleanup"
```

Result:

```text
Passed: 1, Failed: 0, Skipped: 0, Total: 1
```

Latest Flowing Time Mirror / LeBlanc / Ephemeral adjacent validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~Leblanc|FullyQualifiedName~LeBlanc|FullyQualifiedName~Ephemeral|FullyQualifiedName~FlowingTimeMirror|FullyQualifiedName~FullGameEndToEnd|FullyQualifiedName~MatchRecovery"
```

Result:

```text
Passed: 2132, Failed: 0, Skipped: 0, Total: 2132
```

Latest Flowing Time Mirror / LeBlanc suppressed-cleanup backend full validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore
```

Result:

```text
Passed: 8887, Failed: 0, Skipped: 0, Total: 8887
```

Latest Card Trick draw/recycle official-deck replay focused validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~OfficialDeckMidgameResolvesCardTrickDrawRecycle"
```

Result:

```text
Passed: 1, Failed: 0, Skipped: 0, Total: 1
```

Latest Card Trick / full-game / recovery adjacent validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~CardTrick|FullyQualifiedName~FullGameEndToEnd|FullyQualifiedName~MatchRecovery"
```

Result:

```text
Passed: 2096, Failed: 0, Skipped: 0, Total: 2096
```

Latest Card Trick draw/recycle backend full validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore
```

Result:

```text
Passed: 8885, Failed: 0, Skipped: 0, Total: 8885
```

Latest Poro Forge official-deck rejected-command replay focused validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~OfficialDeckMidgameRejectsPoroForgeLegendAttachArmamentWithoutControlledForge"
```

Result:

```text
Passed: 1, Failed: 0, Skipped: 0, Total: 1
```

Latest Poro Forge / LegendAct / hidden-info adjacent validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~PoroForge|FullyQualifiedName~LegendAttachArmament|FullyQualifiedName~LegendAct|FullyQualifiedName~FullGameEndToEnd|FullyQualifiedName~MatchRecovery|FullyQualifiedName~CardCatalogBaseline"
```

Result:

```text
Passed: 2486, Failed: 0, Skipped: 0, Total: 2486
```

Latest Poro Forge rejected-command backend full validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore
```

Result:

```text
Passed: 8865, Failed: 0, Skipped: 0, Total: 8865
```

Latest prevent unit-play official-deck rejected-command replay focused validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~OfficialDeckMidgameRejectsBattlefieldPreventUnitPlay"
```

Result:

```text
Passed: 1, Failed: 0, Skipped: 0, Total: 1
```

Latest prevent unit-play adjacent / hidden-info validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~BattlefieldStatic|FullyQualifiedName~PreventUnitPlay|FullyQualifiedName~PlayCard|FullyQualifiedName~FullGameEndToEnd|FullyQualifiedName~MatchRecovery|FullyQualifiedName~CardCatalogBaseline"
```

Result:

```text
Passed: 2689, Failed: 0, Skipped: 0, Total: 2689
```

Latest prevent unit-play backend full validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore
```

Result:

```text
Passed: 8863, Failed: 0, Skipped: 0, Total: 8863
```

Latest prevent move-to-base official-deck rejected-command replay focused validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~OfficialDeckMidgameRejectsBattlefieldPreventMoveToBase"
```

Result:

```text
Passed: 1, Failed: 0, Skipped: 0, Total: 1
```

Latest prevent move-to-base adjacent / hidden-info validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~BattlefieldStatic|FullyQualifiedName~PreventMoveToBase|FullyQualifiedName~MoveUnit|FullyQualifiedName~FullGameEndToEnd|FullyQualifiedName~MatchRecovery|FullyQualifiedName~CardCatalogBaseline"
```

Result:

```text
Passed: 2517, Failed: 0, Skipped: 0, Total: 2517
```

Latest prevent move-to-base backend full validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore
```

Result:

```text
Passed: 8862, Failed: 0, Skipped: 0, Total: 8862
```

Latest Blood Altar battle-destroyed recall official-deck replay focused validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~OfficialDeckMidgameResolvesBloodAltarBattleDestroyedRecall"
```

Result:

```text
Passed: 1, Failed: 0, Skipped: 0, Total: 1
```

Latest Blood Altar battle-destroyed recall adjacent / hidden-info validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~BattlefieldStatic|FullyQualifiedName~BattlefieldBattleDestroyed|FullyQualifiedName~BloodAltar|FullyQualifiedName~DestroyedInBattle|FullyQualifiedName~DeclareBattle|FullyQualifiedName~BattleDamageAssignment|FullyQualifiedName~FullGameEndToEnd|FullyQualifiedName~MatchRecovery|FullyQualifiedName~CardCatalogBaseline"
```

Result:

```text
Passed: 2597, Failed: 0, Skipped: 0, Total: 2597
```

Latest Blood Altar battle-destroyed recall backend full validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore
```

Result:

```text
Passed: 8861, Failed: 0, Skipped: 0, Total: 8861
```

Latest Poro Forge legend attach-armament official-deck replay focused validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~OfficialDeckMidgameResolvesPoroForgeLegendAttachArmament"
```

Result:

```text
Passed: 1, Failed: 0, Skipped: 0, Total: 1
```

Latest Poro Forge legend attach-armament adjacent / hidden-info validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~BattlefieldStatic|FullyQualifiedName~LegendAttachArmament|FullyQualifiedName~PoroForge|FullyQualifiedName~LegendAct|FullyQualifiedName~FullGameEndToEnd|FullyQualifiedName~MatchRecovery"
```

Result:

```text
Passed: 2249, Failed: 0, Skipped: 0, Total: 2249
```

Latest Poro Forge legend attach-armament backend full validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore
```

Result:

```text
Passed: 8860, Failed: 0, Skipped: 0, Total: 8860
```

Latest Ornn's Forge equipment cost-reduction official-deck replay focused validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~OfficialDeckMidgameResolvesOrnnForgeEquipmentCostReduction"
```

Result:

```text
Passed: 1, Failed: 0, Skipped: 0, Total: 1
```

Latest Ornn's Forge equipment cost-reduction adjacent / hidden-info validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~BattlefieldStatic|FullyQualifiedName~EquipmentCostReduction|FullyQualifiedName~LongSword|FullyQualifiedName~EquipmentKeyword|FullyQualifiedName~FullGameEndToEnd|FullyQualifiedName~MatchRecovery"
```

Result:

```text
Passed: 2179, Failed: 0, Skipped: 0, Total: 2179
```

Latest Ornn's Forge equipment cost-reduction backend full validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore
```

Result:

```text
Passed: 8859, Failed: 0, Skipped: 0, Total: 8859
```

Latest Marai Spire Echo cost-reduction official-deck replay focused validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~OfficialDeckMidgameResolvesMaraiSpireEchoCostReductionAndScoreVictoryActionLogReplaysToFinalStateHash"
```

Result:

```text
Passed: 1, Failed: 0, Skipped: 0, Total: 1
```

Latest Marai Spire Echo cost-reduction adjacent / hidden-info validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~BattlefieldStatic|FullyQualifiedName~EchoCostReduction|FullyQualifiedName~CenterStage|FullyQualifiedName~Echo|FullyQualifiedName~FullGameEndToEnd|FullyQualifiedName~MatchRecovery"
```

Result:

```text
Passed: 2208, Failed: 0, Skipped: 0, Total: 2208
```

Latest Marai Spire Echo cost-reduction backend full validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore
```

Result:

```text
Passed: 8858, Failed: 0, Skipped: 0, Total: 8858
```

Latest Mutation Garden granted unit-experience official-deck replay focused validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~OfficialDeckMidgameResolvesMutationGardenGrantedUnitExperienceAndScoreVictoryActionLogReplaysToFinalStateHash"
```

Result:

```text
Passed: 1, Failed: 0, Skipped: 0, Total: 1
```

Latest Mutation Garden granted unit-experience adjacent / hidden-info validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~BattlefieldUnitExperienceAbility|FullyQualifiedName~BattlefieldGrantUnitExperience|FullyQualifiedName~MutationGarden|FullyQualifiedName~ActivateAbility|FullyQualifiedName~FullGameEndToEnd|FullyQualifiedName~MatchRecovery"
```

Result:

```text
Passed: 2183, Failed: 0, Skipped: 0, Total: 2183
```

Latest Mutation Garden granted unit-experience backend full validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore
```

Result:

```text
Passed: 8857, Failed: 0, Skipped: 0, Total: 8857
```

Latest Void Gate target spell/skill damage bonus official-deck replay focused validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~OfficialDeckMidgameResolvesVoidGateTargetSpellSkillDamageBonusAndScoreVictoryActionLogReplaysToFinalStateHash"
```

Result:

```text
Passed: 1, Failed: 0, Skipped: 0, Total: 1
```

Latest Void Gate target spell/skill damage bonus adjacent / hidden-info validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~BattlefieldTargetDamageBonus|FullyQualifiedName~VoidGate|FullyQualifiedName~TargetSpellSkillDamage|FullyQualifiedName~FullGameEndToEnd|FullyQualifiedName~MatchRecovery"
```

Result:

```text
Passed: 2086, Failed: 0, Skipped: 0, Total: 2086
```

Latest Void Gate target spell/skill damage bonus backend full validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore
```

Result:

```text
Passed: 8856, Failed: 0, Skipped: 0, Total: 8856
```

Latest Wind Hill battlefield all-units RULE_TEXT Roam official-deck replay focused validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~OfficialDeckMidgameProjectsBattlefieldAllUnitsStaticKeywordRoamAndScoreVictoryActionLogReplaysToFinalStateHash"
```

Result:

```text
Passed: 1, Failed: 0, Skipped: 0, Total: 1
```

Latest Wind Hill battlefield all-units RULE_TEXT Roam adjacent / hidden-info validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~BattlefieldAllUnits|FullyQualifiedName~BattlefieldStatic|FullyQualifiedName~StaticKeyword|FullyQualifiedName~StaticAura|FullyQualifiedName~Roam|FullyQualifiedName~FullGameEndToEnd|FullyQualifiedName~MatchRecovery"
```

Result:

```text
Passed: 2212, Failed: 0, Skipped: 0, Total: 2212
```

Latest Wind Hill battlefield all-units RULE_TEXT Roam backend full validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore
```

Result:

```text
Passed: 8855, Failed: 0, Skipped: 0, Total: 8855
```

Latest Mount Targon conquered ready-runes-at-end official-deck replay focused validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~OfficialDeckMidgameResolvesMountTargonConquerReadyRunesAtEnd" --nologo
```

Result:

```text
Passed: 1, Failed: 0, Skipped: 0, Total: 1
```

Latest Mount Targon FullGameEndToEnd validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~FullGameEndToEndTests" --nologo
```

Result:

```text
Passed: 86, Failed: 0, Skipped: 0, Total: 86
```

Latest Mount Targon adjacent / hidden-info validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~BattlefieldConquerReadyRunesAtEnd|FullyQualifiedName~FullGameEndToEndTests|FullyQualifiedName~MatchRecovery" --nologo
```

Result:

```text
Passed: 2079, Failed: 0, Skipped: 0, Total: 2079
```

Latest Mount Targon backend full validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result:

```text
Passed: 8850, Failed: 0, Skipped: 0, Total: 8850
```

Latest Seat of Power conquered draw-for-other-battlefields official-deck replay focused validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~OfficialDeckMidgameResolvesSeatOfPowerConquerDrawForOtherBattlefields" --nologo
```

Result:

```text
Passed: 1, Failed: 0, Skipped: 0, Total: 1
```

Latest Seat of Power FullGameEndToEnd validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~FullGameEndToEndTests" --nologo
```

Result:

```text
Passed: 85, Failed: 0, Skipped: 0, Total: 85
```

Latest Seat of Power adjacent / hidden-info validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~BattlefieldConquerDrawForOtherBattlefields|FullyQualifiedName~FullGameEndToEndTests|FullyQualifiedName~MatchRecovery" --nologo
```

Result:

```text
Passed: 2076, Failed: 0, Skipped: 0, Total: 2076
```

Latest Seat of Power backend full validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result:

```text
Passed: 8849, Failed: 0, Skipped: 0, Total: 8849
```

Latest Zaun Sump conquered discard-draw official-deck replay focused validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~OfficialDeckMidgameResolvesZaunSumpConquerDiscardDraw" --nologo
```

Result:

```text
Passed: 1, Failed: 0, Skipped: 0, Total: 1
```

Latest Zaun Sump FullGameEndToEnd validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~FullGameEndToEndTests" --nologo
```

Result:

```text
Passed: 84, Failed: 0, Skipped: 0, Total: 84
```

Latest Zaun Sump adjacent / hidden-info validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~BattlefieldConquerDiscardDraw|FullyQualifiedName~FullGameEndToEndTests|FullyQualifiedName~MatchRecovery" --nologo
```

Result:

```text
Passed: 2077, Failed: 0, Skipped: 0, Total: 2077
```

Latest Zaun Sump backend full validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result:

```text
Passed: 8848, Failed: 0, Skipped: 0, Total: 8848
```

Latest Thunder Sigil conquered recycle-rune official-deck replay focused validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~OfficialDeckMidgameResolvesThunderSigilConquerRecycleRune" --nologo
```

Result:

```text
Passed: 1, Failed: 0, Skipped: 0, Total: 1
```

Latest Thunder Sigil FullGameEndToEnd validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~FullGameEndToEndTests" --nologo
```

Result:

```text
Passed: 83, Failed: 0, Skipped: 0, Total: 83
```

Latest Thunder Sigil adjacent / hidden-info validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~BattlefieldConquerRecycleRune|FullyQualifiedName~FullGameEndToEndTests|FullyQualifiedName~MatchRecovery" --nologo
```

Result:

```text
Passed: 2076, Failed: 0, Skipped: 0, Total: 2076
```

Latest Thunder Sigil backend full validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result:

```text
Passed: 8847, Failed: 0, Skipped: 0, Total: 8847
```

Latest winning-score increase score-victory focused validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo --filter "FullyQualifiedName~OfficialDecksResolveWinningScoreIncrease"
```

Result:

```text
Passed: 1, Failed: 0, Skipped: 0, Total: 1
```

Latest winning-score increase FullGameEndToEnd validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo --filter "FullyQualifiedName~FullGameEndToEndTests"
```

Result:

```text
Passed: 82, Failed: 0, Skipped: 0, Total: 82
```

Latest winning-score increase adjacent / hidden-info validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo --filter "FullyQualifiedName~WinningScore|FullyQualifiedName~BattlefieldStaticWinningScore|FullyQualifiedName~MindAndBalance|FullyQualifiedName~FullGameEndToEndTests|FullyQualifiedName~MatchRecovery"
```

Result:

```text
Passed: 2082, Failed: 0, Skipped: 0, Total: 2082
```

Latest winning-score increase backend full validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result:

```text
Passed: 8838, Failed: 0, Skipped: 0, Total: 8838
```

Latest Forgotten Monument score-delay score-victory focused validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo --filter "FullyQualifiedName~OfficialDecksResolveForgottenMonumentScoreDelay"
```

Result:

```text
Passed: 1, Failed: 0, Skipped: 0, Total: 1
```

Latest Forgotten Monument FullGameEndToEnd validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo --filter "FullyQualifiedName~FullGameEndToEndTests"
```

Result:

```text
Passed: 81, Failed: 0, Skipped: 0, Total: 81
```

Latest Forgotten Monument adjacent / hidden-info validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo --filter "FullyQualifiedName~ForgottenMonument|FullyQualifiedName~ScoreDelay|FullyQualifiedName~ScorePrevented|FullyQualifiedName~FirstTurnScore|FullyQualifiedName~BattlefieldHeldScore|FullyQualifiedName~BattleResponse|FullyQualifiedName~BattlefieldTriggerSpec|FullyQualifiedName~FullGameEndToEndTests|FullyQualifiedName~MatchRecovery"
```

Result:

```text
Passed: 2140, Failed: 0, Skipped: 0, Total: 2140
```

Latest Forgotten Monument backend full validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result:

```text
Passed: 8837, Failed: 0, Skipped: 0, Total: 8837
```

Latest Glory Arena first-turn score-to-victory focused validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo --filter "FullyQualifiedName~OfficialDecksResolveGloryArenaFirstTurnScore"
```

Result:

```text
Passed: 1, Failed: 0, Skipped: 0, Total: 1
```

Latest Glory Arena FullGameEndToEnd validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo --filter "FullyQualifiedName~FullGameEndToEndTests"
```

Result:

```text
Passed: 80, Failed: 0, Skipped: 0, Total: 80
```

Latest Glory Arena adjacent / hidden-info validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo --filter "FullyQualifiedName~GloryArena|FullyQualifiedName~FirstTurnScore|FullyQualifiedName~ScoreDelay|FullyQualifiedName~BattlefieldFirstTurn|FullyQualifiedName~BattlefieldTriggerSpec|FullyQualifiedName~FullGameEndToEndTests|FullyQualifiedName~MatchRecovery"
```

Result:

```text
Passed: 2083, Failed: 0, Skipped: 0, Total: 2083
```

Latest Glory Arena backend full validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result:

```text
Passed: 8836, Failed: 0, Skipped: 0, Total: 8836
```

Latest Power Obelisk first-turn extra-rune score-victory focused validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo --filter "FullyQualifiedName~OfficialDecksResolvePowerObeliskFirstTurnExtraRune"
```

Result:

```text
Passed: 1, Failed: 0, Skipped: 0, Total: 1
```

Latest Power Obelisk FullGameEndToEnd validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo --filter "FullyQualifiedName~FullGameEndToEndTests"
```

Result:

```text
Passed: 79, Failed: 0, Skipped: 0, Total: 79
```

Latest Power Obelisk adjacent / hidden-info validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo --filter "FullyQualifiedName~PowerObelisk|FullyQualifiedName~FirstTurnRune|FullyQualifiedName~BattlefieldFirstTurn|FullyQualifiedName~BattlefieldTriggerSpec|FullyQualifiedName~FullGameEndToEndTests|FullyQualifiedName~MatchRecovery"
```

Result:

```text
Passed: 2076, Failed: 0, Skipped: 0, Total: 2076
```

Latest Power Obelisk backend full validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result:

```text
Passed: 8835, Failed: 0, Skipped: 0, Total: 8835
```

Latest Duskpetal Lab turn-start destroy-draw score-victory focused validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo --filter "FullyQualifiedName~OfficialDeckMidgameResolvesDuskpetalLabTurnStartDestroyDraw"
```

Result:

```text
Passed: 1, Failed: 0, Skipped: 0, Total: 1
```

Latest Duskpetal Lab FullGameEndToEnd validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo --filter "FullyQualifiedName~FullGameEndToEndTests"
```

Result:

```text
Passed: 78, Failed: 0, Skipped: 0, Total: 78
```

Latest Duskpetal Lab adjacent / hidden-info validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo --filter "FullyQualifiedName~Duskpetal|FullyQualifiedName~BattlefieldTurnStartDestroy|FullyQualifiedName~BattlefieldTurnStart|FullyQualifiedName~BattlefieldTriggerSpec|FullyQualifiedName~FullGameEndToEndTests|FullyQualifiedName~MatchRecovery"
```

Result:

```text
Passed: 2077, Failed: 0, Skipped: 0, Total: 2077
```

Latest Duskpetal Lab backend full validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result:

```text
Passed: 8834, Failed: 0, Skipped: 0, Total: 8834
```

Latest Frost Hold turn-start damage score-victory focused validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo --filter "FullyQualifiedName~OfficialDeckMidgameResolvesFrostHoldTurnStartDamage"
```

Result:

```text
Passed: 1, Failed: 0, Skipped: 0, Total: 1
```

Latest Frost Hold FullGameEndToEnd validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo --filter "FullyQualifiedName~FullGameEndToEndTests"
```

Result:

```text
Passed: 77, Failed: 0, Skipped: 0, Total: 77
```

Latest Frost Hold adjacent / hidden-info validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo --filter "FullyQualifiedName~FrostHold|FullyQualifiedName~BattlefieldTurnStartDamage|FullyQualifiedName~BattlefieldTriggerSpec|FullyQualifiedName~FullGameEndToEndTests|FullyQualifiedName~MatchRecovery"
```

Result:

```text
Passed: 2071, Failed: 0, Skipped: 0, Total: 2071
```

Latest Frost Hold backend full validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result:

```text
Passed: 8833, Failed: 0, Skipped: 0, Total: 8833
```

Latest Piltover Academy held-next-spell Echo score-victory focused validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo --filter "FullyQualifiedName~OfficialDeckMidgameResolvesPiltoverAcademyHeldNextSpellEcho"
```

Result:

```text
Passed: 1, Failed: 0, Skipped: 0, Total: 1
```

Latest Piltover Academy FullGameEndToEnd validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo --filter "FullyQualifiedName~FullGameEndToEndTests"
```

Result:

```text
Passed: 76, Failed: 0, Skipped: 0, Total: 76
```

Latest Piltover Academy adjacent / hidden-info validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo --filter "FullyQualifiedName~PiltoverAcademy|FullyQualifiedName~HeldNextSpellEcho|FullyQualifiedName~BattlefieldHeld|FullyQualifiedName~BattlefieldTriggerSpec|FullyQualifiedName~Stack|FullyQualifiedName~FullGameEndToEndTests|FullyQualifiedName~MatchRecovery"
```

Result:

```text
Passed: 2655, Failed: 0, Skipped: 0, Total: 2655
```

Latest Piltover Academy backend full validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result:

```text
Passed: 8832, Failed: 0, Skipped: 0, Total: 8832
```

Latest Back Alley Bar moved-unit power score-victory focused validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo --filter "FullyQualifiedName~OfficialDeckMidgameResolvesBackAlleyBarMovedUnitPower"
```

Result:

```text
Passed: 1, Failed: 0, Skipped: 0, Total: 1
```

Latest Back Alley Bar FullGameEndToEnd validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo --filter "FullyQualifiedName~FullGameEndToEndTests"
```

Result:

```text
Passed: 76, Failed: 0, Skipped: 0, Total: 76
```

Latest Back Alley Bar adjacent / hidden-info validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo --filter "FullyQualifiedName~BackAlleyBar|FullyQualifiedName~BattlefieldMovedUnitPower|FullyQualifiedName~BattlefieldMovePower|FullyQualifiedName~MoveUnit|FullyQualifiedName~FullGameEndToEndTests|FullyQualifiedName~MatchRecovery"
```

Result:

```text
Passed: 2152, Failed: 0, Skipped: 0, Total: 2152
```

Latest Back Alley Bar backend full validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result:

```text
Passed: 8832, Failed: 0, Skipped: 0, Total: 8832
```

Latest Rehearsal Hall held move-unit-to-base score-victory owner-fallback focused validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo --filter "FullyQualifiedName~OfficialDeckMidgameResolvesRehearsalHallHeldMoveUnitToBase"
```

Result:

```text
Passed: 1, Failed: 0, Skipped: 0, Total: 1
```

Latest Rehearsal Hall FullGameEndToEnd validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo --filter "FullyQualifiedName~FullGameEndToEndTests"
```

Result:

```text
Passed: 76, Failed: 0, Skipped: 0, Total: 76
```

Latest Rehearsal Hall adjacent / hidden-info validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo --filter "FullyQualifiedName~RehearsalHall|FullyQualifiedName~BattlefieldHeldMoveUnitToBase|FullyQualifiedName~BattlefieldHeldScore|FullyQualifiedName~BattlefieldHeld|FullyQualifiedName~MoveUnit|FullyQualifiedName~FullGameEndToEndTests|FullyQualifiedName~MatchRecovery"
```

Result:

```text
Passed: 2233, Failed: 0, Skipped: 0, Total: 2233
```

Latest Rehearsal Hall backend full validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result:

```text
Passed: 8832, Failed: 0, Skipped: 0, Total: 8832
```

Latest Minefield conquered mill official-deck replay focused validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo --filter "FullyQualifiedName~OfficialDeckMidgameResolvesMinefieldConquerMill"
```

Result:

```text
Passed: 1, Failed: 0, Skipped: 0, Total: 1
```

Latest Minefield FullGameEndToEnd validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo --filter "FullyQualifiedName~FullGameEndToEndTests"
```

Result:

```text
Passed: 76, Failed: 0, Skipped: 0, Total: 76
```

Latest Minefield adjacent / hidden-info validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo --filter "FullyQualifiedName~Minefield|FullyQualifiedName~BattlefieldConquerMill|FullyQualifiedName~BattlefieldConquer|FullyQualifiedName~BattlefieldTriggerSpec|FullyQualifiedName~FullGameEndToEndTests|FullyQualifiedName~GameHubJoinTests|FullyQualifiedName~MatchRecovery"
```

Result:

```text
Passed: 2356, Failed: 0, Skipped: 0, Total: 2356
```

Latest Minefield backend full validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result:

```text
Passed: 8832, Failed: 0, Skipped: 0, Total: 8832
```

Latest Candlelit Sanctum conquered reveal/recycle official-deck replay focused validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo --filter "FullyQualifiedName~OfficialDeckMidgameResolvesCandlelitSanctumConquerRevealRecycle"
```

Result:

```text
Passed: 1, Failed: 0, Skipped: 0, Total: 1
```

Latest Candlelit Sanctum FullGameEndToEnd validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo --filter "FullyQualifiedName~FullGameEndToEndTests"
```

Result:

```text
Passed: 75, Failed: 0, Skipped: 0, Total: 75
```

Latest Candlelit Sanctum adjacent / hidden-info validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo --filter "FullyQualifiedName~Candlelit|FullyQualifiedName~BattlefieldConquerRevealRecycle|FullyQualifiedName~BattlefieldConquer|FullyQualifiedName~BattlefieldTriggerSpec|FullyQualifiedName~FullGameEndToEndTests|FullyQualifiedName~GameHubJoinTests|FullyQualifiedName~MatchRecovery"
```

Result:

```text
Passed: 2355, Failed: 0, Skipped: 0, Total: 2355
```

Latest Candlelit Sanctum backend full validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result:

```text
Passed: 8831, Failed: 0, Skipped: 0, Total: 8831
```

Latest Dunehorn Beast unit battlefield-held draw official-deck replay focused validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~OfficialDeckMidgameResolvesDunehornBeastUnitHeldDraw" --nologo
```

Result:

```text
Passed: 1, Failed: 0, Skipped: 0, Total: 1
```

Latest Dunehorn Beast FullGameEndToEnd validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~FullGameEndToEndTests" --nologo
```

Result:

```text
Passed: 74, Failed: 0, Skipped: 0, Total: 74
```

Latest Dunehorn Beast adjacent / hidden-info validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~Dunehorn|FullyQualifiedName~UnitBattlefieldHeldDraw|FullyQualifiedName~BattlefieldHeldDraw|FullyQualifiedName~BattlefieldHeld|FullyQualifiedName~BattlefieldTriggerSpec|FullyQualifiedName~FullGameEndToEndTests|FullyQualifiedName~MatchRecovery" --nologo
```

Result:

```text
Passed: 2152, Failed: 0, Skipped: 0, Total: 2152
```

Latest Dunehorn Beast backend full validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result:

```text
Passed: 8830, Failed: 0, Skipped: 0, Total: 8830
```

Latest Vaults of Helia held unit-cost official-deck replay focused validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~BehaviorSpecCatalogParsesBattlefieldHeldUnitCostIncreaseTrigger|FullyQualifiedName~OfficialDeckMidgameAppliesVaultsOfHeliaHeldUnitCostIncrease" --nologo
```

Result:

```text
Passed: 2, Failed: 0, Skipped: 0, Total: 2
```

Latest Vaults of Helia FullGameEndToEnd validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~FullGameEndToEndTests" --nologo
```

Result:

```text
Passed: 73, Failed: 0, Skipped: 0, Total: 73
```

Latest Vaults of Helia adjacent / hidden-info validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~BattlefieldHeldUnitCostIncrease|FullyQualifiedName~VaultsOfHelia|FullyQualifiedName~PaymentEngine|FullyQualifiedName~PlayCard|FullyQualifiedName~BattlefieldHeld|FullyQualifiedName~BattlefieldTriggerSpec|FullyQualifiedName~CardCatalogBaselineTests|FullyQualifiedName~FullGameEndToEndTests|FullyQualifiedName~MatchRecovery" --nologo
```

Result:

```text
Passed: 3445, Failed: 0, Skipped: 0, Total: 3445
```

Latest Vaults of Helia backend full validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result:

```text
Passed: 8829, Failed: 0, Skipped: 0, Total: 8829
```

Latest Swift spell-duel stack-priority focused validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~SwiftStackPriorityPlayCard|FullyQualifiedName~P4PermissionKeywordTimingSeparatesSwiftReactionAndOrdinaryWindows" --nologo
```

Result:

```text
Passed: 2, Failed: 0, Skipped: 0, Total: 2
```

Latest Swift spell-duel stack-priority adjacent validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~SwiftStackPriorityPlayCard|FullyQualifiedName~SpellDuel|FullyQualifiedName~StackPriority|FullyQualifiedName~PassPriority|FullyQualifiedName~PlayCard|FullyQualifiedName~Prompt|FullyQualifiedName~CardCatalogBaselineTests" --nologo
```

Result:

```text
Passed: 1373, Failed: 0, Skipped: 0, Total: 1373
```

Latest Swift spell-duel stack-priority hidden-info adjacent validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~SwiftStackPriorityPlayCard|FullyQualifiedName~FullGameEndToEndTests|FullyQualifiedName~MatchRecovery" --nologo
```

Result:

```text
Passed: 2057, Failed: 0, Skipped: 0, Total: 2057
```

Latest Swift spell-duel stack-priority backend full validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result:

```text
Passed: 8764, Failed: 0, Skipped: 0, Total: 8764
```

Latest Plunder Alley defend move-friendly-unit-to-base focused validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~OfficialDeckMidgameResolvesPlunderAlleyDefendMoveToBase" --nologo
```

Result:

```text
Passed: 1, Failed: 0, Skipped: 0, Total: 1
```

Latest Plunder Alley FullGameEndToEnd validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~FullGameEndToEndTests" --nologo
```

Result:

```text
Passed: 68, Failed: 0, Skipped: 0, Total: 68
```

Latest Plunder Alley adjacent validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~Plunder|FullyQualifiedName~BattlefieldDefend|FullyQualifiedName~BattlefieldTriggerSpec|FullyQualifiedName~DeclareBattle|FullyQualifiedName~GameHub|FullyQualifiedName~FullGameEndToEndTests|FullyQualifiedName~MatchRecovery" --nologo
```

Result:

```text
Passed: 2396, Failed: 0, Skipped: 0, Total: 2396
```

Latest Plunder Alley backend full validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result:

```text
Passed: 8765, Failed: 0, Skipped: 0, Total: 8765
```

Latest Hub official-opening play-card surrender-win action-log replay focused validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~OfficialDeckCanPlayPromptLegalCardReachSurrenderWinAndReplayThroughHub" --nologo
```

Result:

```text
Passed: 1, Failed: 0, Skipped: 0, Total: 1
```

Latest `OfficialDeck` adjacent validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~OfficialDeck" --nologo
```

Result:

```text
Passed: 66, Failed: 0, Skipped: 0, Total: 66
```

Latest Hub / recovery adjacent validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~GameHubJoinTests|FullyQualifiedName~MatchRecovery" --nologo
```

Result:

```text
Passed: 2212, Failed: 0, Skipped: 0, Total: 2212
```

Latest backend full validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result:

```text
Passed: 8824, Failed: 0, Skipped: 0, Total: 8824
```

Focused validation:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~BattlefieldIsolatedDefenderKeywordModifier" --nologo
```

Result:

```text
Passed: 3, Failed: 0, Skipped: 0, Total: 3
```

Full-game validation:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo --filter "FullyQualifiedName~FullGameEndToEndTests"
```

Result:

```text
Passed: 39, Failed: 0, Skipped: 0, Total: 39
```

Adjacent validation:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~BattlefieldIsolatedDefenderKeywordModifier|FullyQualifiedName~BattlefieldIsolated|FullyQualifiedName~ForbiddenWasteland|FullyQualifiedName~StaticAura|FullyQualifiedName~StaticKeyword|FullyQualifiedName~Steadfast|FullyQualifiedName~ContinuousEffect|FullyQualifiedName~FullGameEndToEndTests|FullyQualifiedName~MatchRecovery" --nologo
```

Result:

```text
Passed: 2111, Failed: 0, Skipped: 0, Total: 2111
```

Backend full validation:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj
```

Result:

```text
Passed: 8733, Failed: 0, Skipped: 0, Total: 8733
```

Latest Rumble legend friendly-mechanical Steadfast focused validation:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~RumbleLegendFriendlyMechanicalSteadfast" --nologo
```

Result:

```text
Passed: 1, Failed: 0, Skipped: 0, Total: 1
```

Latest full-game validation:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~FullGameEndToEndTests" --nologo
```

Result:

```text
Passed: 40, Failed: 0, Skipped: 0, Total: 40
```

Latest adjacent validation:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~RumbleLegendFriendlyMechanicalSteadfast|FullyQualifiedName~FriendlyFiltered|FullyQualifiedName~StaticKeyword|FullyQualifiedName~StaticAura|FullyQualifiedName~Steadfast|FullyQualifiedName~Rumble|FullyQualifiedName~FullGameEndToEndTests|FullyQualifiedName~MatchRecovery" --nologo
```

Result:

```text
Passed: 2112, Failed: 0, Skipped: 0, Total: 2112
```

Latest backend full validation:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result:

```text
Passed: 8734, Failed: 0, Skipped: 0, Total: 8734
```

Latest Treasure Pile trigger-payment focused validation:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo --filter "FullyQualifiedName~OfficialDeckMidgamePaysTreasurePileConquerGold"
```

Result:

```text
Passed: 1, Failed: 0, Skipped: 0, Total: 1
```

Latest Treasure Pile full-game validation:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~FullGameEndToEndTests" --nologo
```

Result:

```text
Passed: 42, Failed: 0, Skipped: 0, Total: 42
```

Latest Treasure Pile adjacent validation:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~TreasurePile|FullyQualifiedName~BattlefieldConquerGold|FullyQualifiedName~TriggerPayment|FullyQualifiedName~FullGameEndToEndTests|FullyQualifiedName~MatchRecovery" --nologo
```

Result:

```text
Passed: 2124, Failed: 0, Skipped: 0, Total: 2124
```

Latest Treasure Pile backend full validation:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result:

```text
Passed: 8736, Failed: 0, Skipped: 0, Total: 8736
```

Latest Sunken Temple powerful-unit trigger-payment focused validation:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo --filter "FullyQualifiedName~OfficialDeckMidgamePaysSunkenTemplePowerfulDraw"
```

Result:

```text
Passed: 1, Failed: 0, Skipped: 0, Total: 1
```

Latest Sunken Temple full-game validation:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~FullGameEndToEndTests" --nologo
```

Result:

```text
Passed: 43, Failed: 0, Skipped: 0, Total: 43
```

Latest Sunken Temple adjacent validation:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~SunkenTemple|FullyQualifiedName~PowerfulDraw|FullyQualifiedName~BattlefieldConquerPowerful|FullyQualifiedName~TriggerPayment|FullyQualifiedName~FullGameEndToEndTests|FullyQualifiedName~MatchRecovery" --nologo
```

Result:

```text
Passed: 2129, Failed: 0, Skipped: 0, Total: 2129
```

Latest Sunken Temple backend full validation:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result:

```text
Passed: 8737, Failed: 0, Skipped: 0, Total: 8737
```

Latest Treasure Pile / Sunken Temple trigger-payment decline focused validation:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~OfficialDeckMidgameDeclinesTreasurePileConquerGoldAndScoreVictoryActionLogReplaysToFinalStateHash|FullyQualifiedName~OfficialDeckMidgameDeclinesSunkenTemplePowerfulDrawAndScoreVictoryActionLogReplaysToFinalStateHash" --nologo
```

Result:

```text
Passed: 2, Failed: 0, Skipped: 0, Total: 2
```

Latest Treasure Pile / Sunken Temple decline full-game validation:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~FullGameEndToEndTests" --nologo
```

Result:

```text
Passed: 70, Failed: 0, Skipped: 0, Total: 70
```

Latest Treasure Pile / Sunken Temple decline adjacent validation:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~TreasurePile|FullyQualifiedName~SunkenTemple|FullyQualifiedName~BattlefieldConquerGold|FullyQualifiedName~BattlefieldConquerPowerful|FullyQualifiedName~TriggerPayment|FullyQualifiedName~FullGameEndToEndTests|FullyQualifiedName~MatchRecovery" --nologo
```

Result:

```text
Passed: 2156, Failed: 0, Skipped: 0, Total: 2156
```

Latest Treasure Pile / Sunken Temple decline backend full validation:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result:

```text
Passed: 8826, Failed: 0, Skipped: 0, Total: 8826
```

Latest Imperial Shrine / Hall of Legends trigger-payment focused validation:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo --filter "FullyQualifiedName~ImperialShrineSandSoldier|FullyQualifiedName~HallOfLegendsReadyLegend"
```

Result:

```text
Passed: 4, Failed: 0, Skipped: 0, Total: 4
```

Latest Imperial Shrine / Hall of Legends FullGameEndToEnd validation:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo --filter "FullyQualifiedName~FullGameEndToEndTests"
```

Result:

```text
Passed: 72, Failed: 0, Skipped: 0, Total: 72
```

Latest Imperial Shrine / Hall of Legends adjacent / hidden-info validation:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo --filter "FullyQualifiedName~ImperialShrine|FullyQualifiedName~SandSoldier|FullyQualifiedName~PayReturnUnit|FullyQualifiedName~ReturnUnitCreate|FullyQualifiedName~HallOfLegends|FullyQualifiedName~ReadyLegend|FullyQualifiedName~LegendReadied|FullyQualifiedName~TriggerPayment|FullyQualifiedName~BattlefieldConquer|FullyQualifiedName~FullGameEndToEndTests|FullyQualifiedName~MatchRecovery"
```

Result:

```text
Passed: 2222, Failed: 0, Skipped: 0, Total: 2222
```

Latest Imperial Shrine / Hall of Legends backend full validation:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result:

```text
Passed: 8828, Failed: 0, Skipped: 0, Total: 8828
```

Latest Ravenbloom defend reveal-spell focused validation:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo --filter "FullyQualifiedName~OfficialDeckMidgameResolvesRavenbloomDefendRevealSpell"
```

Result:

```text
Passed: 1, Failed: 0, Skipped: 0, Total: 1
```

Latest Ravenbloom full-game validation:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~FullGameEndToEndTests" --nologo
```

Result:

```text
Passed: 46, Failed: 0, Skipped: 0, Total: 46
```

Latest Ravenbloom adjacent validation:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~Ravenbloom|FullyQualifiedName~DefendReveal|FullyQualifiedName~RevealSpell|FullyQualifiedName~BattlefieldDefend|FullyQualifiedName~BattlefieldTriggerSpec|FullyQualifiedName~FullGameEndToEndTests|FullyQualifiedName~MatchRecovery" --nologo
```

Result:

```text
Passed: 2056, Failed: 0, Skipped: 0, Total: 2056
```

Latest Ravenbloom backend full validation:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result:

```text
Passed: 8740, Failed: 0, Skipped: 0, Total: 8740
```

Latest Ravenbloom defend reveal non-spell recycle focused validation:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo --filter "FullyQualifiedName~OfficialDeckMidgameResolvesRavenbloomDefendRevealNonSpellRecycle"
```

Result:

```text
Passed: 1, Failed: 0, Skipped: 0, Total: 1
```

Latest Ravenbloom non-spell full-game validation:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~FullGameEndToEndTests" --nologo
```

Result:

```text
Passed: 47, Failed: 0, Skipped: 0, Total: 47
```

Latest Ravenbloom non-spell adjacent validation:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~Ravenbloom|FullyQualifiedName~DefendReveal|FullyQualifiedName~RevealSpell|FullyQualifiedName~Recycle|FullyQualifiedName~BattlefieldDefend|FullyQualifiedName~BattlefieldTriggerSpec|FullyQualifiedName~FullGameEndToEndTests|FullyQualifiedName~MatchRecovery" --nologo
```

Result:

```text
Passed: 2181, Failed: 0, Skipped: 0, Total: 2181
```

Latest Ravenbloom non-spell backend full validation:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result:

```text
Passed: 8741, Failed: 0, Skipped: 0, Total: 8741
```

Latest Hunting Grounds overkill create-Warhawk focused validation:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo --filter "FullyQualifiedName~OfficialDeckMidgameResolvesHuntingGroundsOverkillWarhawk"
```

Result:

```text
Passed: 1, Failed: 0, Skipped: 0, Total: 1
```

Latest Hunting Grounds full-game validation:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~FullGameEndToEndTests" --nologo
```

Result:

```text
Passed: 48, Failed: 0, Skipped: 0, Total: 48
```

Latest Hunting Grounds adjacent validation:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~Hunting|FullyQualifiedName~Overkill|FullyQualifiedName~Warhawk|FullyQualifiedName~BattlefieldConquer|FullyQualifiedName~BattlefieldTriggerSpec|FullyQualifiedName~FullGameEndToEndTests|FullyQualifiedName~MatchRecovery" --nologo
```

Result:

```text
Passed: 2142, Failed: 0, Skipped: 0, Total: 2142
```

Latest Hunting Grounds backend full validation:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result:

```text
Passed: 8742, Failed: 0, Skipped: 0, Total: 8742
```

Latest Dream Tree friendly-spell draw focused validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo --filter "FullyQualifiedName~OfficialDeckMidgameResolvesDreamTreeFriendlySpellDraw"
```

Result:

```text
Passed: 1, Failed: 0, Skipped: 0, Total: 1
```

Latest Dream Tree FullGameEndToEnd validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo --filter "FullyQualifiedName~FullGameEndToEndTests"
```

Result:

```text
Passed: 49, Failed: 0, Skipped: 0, Total: 49
```

Latest Dream Tree adjacent / hidden-info validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo --filter "FullyQualifiedName~BattlefieldFriendlySpellDraw|FullyQualifiedName~BattlefieldFriendlySpellTarget|FullyQualifiedName~BattlefieldSpellPowerBonus|FullyQualifiedName~BattlefieldTriggerSpec|FullyQualifiedName~OfficialDeckMidgameResolvesDreamTreeFriendlySpellDraw|FullyQualifiedName~FullGameEndToEndTests|FullyQualifiedName~MatchRecovery"
```

Result:

```text
Passed: 2051, Failed: 0, Skipped: 0, Total: 2051
```

Latest Dream Tree backend full validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result:

```text
Passed: 8745, Failed: 0, Skipped: 0, Total: 8745
```

Latest Waste Hall spell-power bonus focused validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo --filter "FullyQualifiedName~OfficialDeckMidgameResolvesWasteHallSpellPowerBonus"
```

Result:

```text
Passed: 1, Failed: 0, Skipped: 0, Total: 1
```

Latest Waste Hall FullGameEndToEnd validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo --filter "FullyQualifiedName~FullGameEndToEndTests"
```

Result:

```text
Passed: 50, Failed: 0, Skipped: 0, Total: 50
```

Latest Waste Hall adjacent / hidden-info validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo --filter "FullyQualifiedName~BattlefieldSpellPowerBonus|FullyQualifiedName~BattlefieldTriggerSpec|FullyQualifiedName~OfficialDeckMidgameResolvesWasteHallSpellPowerBonus|FullyQualifiedName~FullGameEndToEndTests|FullyQualifiedName~MatchRecovery"
```

Result:

```text
Passed: 2045, Failed: 0, Skipped: 0, Total: 2045
```

Latest Waste Hall backend full validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result:

```text
Passed: 8746, Failed: 0, Skipped: 0, Total: 8746
```

Latest Lost Library high-cost spell insight focused validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo --filter "FullyQualifiedName~OfficialDeckMidgameResolvesLostLibraryHighCostSpellInsight"
```

Result:

```text
Passed: 1, Failed: 0, Skipped: 0, Total: 1
```

Latest Lost Library FullGameEndToEnd validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo --filter "FullyQualifiedName~FullGameEndToEndTests"
```

Result:

```text
Passed: 51, Failed: 0, Skipped: 0, Total: 51
```

Latest Lost Library adjacent / hidden-info validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo --filter "FullyQualifiedName~BattlefieldHighCostSpellInsight|FullyQualifiedName~BattlefieldTriggerSpec|FullyQualifiedName~OfficialDeckMidgameResolvesLostLibraryHighCostSpellInsight|FullyQualifiedName~FullGameEndToEndTests|FullyQualifiedName~MatchRecovery"
```

Result:

```text
Passed: 2046, Failed: 0, Skipped: 0, Total: 2046
```

Latest Lost Library backend full validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result:

```text
Passed: 8747, Failed: 0, Skipped: 0, Total: 8747
```

Latest Darius same-battlefield static-aura B0 score-victory replay focused validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo --filter "FullyQualifiedName~OfficialDeckMidgameAppliesDariusSameBattlefieldStaticAura"
```

Result:

```text
Passed: 1, Failed: 0, Skipped: 0, Total: 1
```

Latest FullGameEndToEnd validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo --filter "FullyQualifiedName~FullGameEndToEndTests"
```

Result:

```text
Passed: 89, Failed: 0, Skipped: 0, Total: 89
```

Latest Darius same-battlefield static-aura adjacent / hidden-info validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo --filter "FullyQualifiedName~Darius|FullyQualifiedName~SameBattlefieldOtherFriendly|FullyQualifiedName~SameBattlefieldStaticAura|FullyQualifiedName~StaticAura|FullyQualifiedName~StaticPower|FullyQualifiedName~ContinuousEffect|FullyQualifiedName~FullGameEndToEndTests|FullyQualifiedName~MatchRecovery"
```

Result:

```text
Passed: 2166, Failed: 0, Skipped: 0, Total: 2166
```

Latest Ornn friendly-equipment static-aura B0 score-victory replay focused validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~OfficialDeckMidgameAppliesOrnnFriendlyEquipmentStaticAura" --nologo
```

Result:

```text
Passed: 1, Failed: 0, Skipped: 0, Total: 1
```

Latest Ornn friendly-equipment static-aura focused representative validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~OfficialDeckMidgameAppliesOrnnFriendlyEquipmentStaticAura|FullyQualifiedName~OrnnFriendlyEquipmentStaticPowerTests|FullyQualifiedName~LayerEngine" --nologo
```

Result:

```text
Passed: 64, Failed: 0, Skipped: 0, Total: 64
```

Latest Ornn friendly-equipment static-aura FullGameEndToEnd validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~FullGameEndToEndTests" --nologo
```

Result:

```text
Passed: 90, Failed: 0, Skipped: 0, Total: 90
```

Latest Ornn friendly-equipment static-aura adjacent / hidden-info validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~Ornn|FullyQualifiedName~FriendlyEquipment|FullyQualifiedName~EquipmentKeyword|FullyQualifiedName~StaticAura|FullyQualifiedName~StaticPower|FullyQualifiedName~ContinuousEffect|FullyQualifiedName~FullGameEndToEndTests|FullyQualifiedName~MatchRecovery" --nologo
```

Result:

```text
Passed: 2200, Failed: 0, Skipped: 0, Total: 2200
```

Latest backend full validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result:

```text
Passed: 8854, Failed: 0, Skipped: 0, Total: 8854
```

## Non-Closure

This evidence proves the engine can drive mirrored Jhin low-curve decks, a distinct Jhin-vs-Rumble official low-curve deck pair, and a standby-heavy Jhin-vs-Poppy official deck pair through setup, opening, live prompt-driven gameplay, contested battlefield task creation, no-legal battle skip, later turn-start battlefield reopen, same-turn false-to-true legal-combatants skipped-battle reopen, real battle declaration, battle close and score-based match result without leaking hidden zones. It also proves official Lillia multi-defender damage-assignment paths can open and resolve `ASSIGN_COMBAT_DAMAGE` through server prompts, including Taric `壁垒` before LeBlanc `后排` ordering with printed/granted `坚守` battle effective power; official Vex / Shadow battle response paths can open and resolve `ACTIVATE_ABILITY` through server prompts; an official Poppy / Pakaa Cub standby path can hide and reveal a standby card through server prompts without exposing the hidden card number in the hide event; an official Poppy / Bandle Tree path can hide a standby card to a battlefield extra-standby destination and still finish through score-victory replay; official Poppy / Garen / Demacia Envoy, Darius legend / Darius static source / Aggressive Dragonhound, Rumble / Ornn / Long Sword, Vex / Baron Nashor / Wildclaw Beastmaster, Poppy / Scarlet Pigeon / Demacia Envoy, Lillia / Petal Pixie / Faerie token / Wildclaw Beastmaster, Lillia / Soul Shepherd / Warhawk token / Wildclaw Beastmaster, Lillia / Waterbender / Watchful Sentinel, Master Yi intro / Demacia Envoy, Master Yi level / Demacia Envoy, Master Yi level / Wise Elder / Arena Rookie / Watchful Sentinel, Vex / Trifarian Training Grounds / Wildclaw Beastmaster, Poppy / Reliable Siege Dog / Demacia Envoy, Poppy / Sett / Arena Rookie / Demacia Envoy, and Poppy / Lee Sin / Arena Rookie / Demacia Envoy paths can apply data-driven same-battlefield, friendly-equipment count-to-source, non-local other-friendly, source-combat, same-battlefield ephemeral count-to-source, friendly-token filtered, source-lone-battle, friendly single-defender, experience-gated friendly-units, source-object filtered, battlefield-source all-units, source same-location threshold, same-battlefield boon count-to-source, and same-battlefield other-friendly filtered static auras to real battle damage and still finish through score-victory replay; official Jhin / Farron Captain / Ascended Believer and Lillia / Taric / LeBlanc paths can apply data-driven same-battlefield RULE_TEXT keyword auras to attacker and defender real battle damage and still finish through score-victory replay; and an official Vex / Shadow / Teemo path can reveal a hidden standby card as a stack reaction during response priority. The mirrored Jhin, distinct Jhin-vs-Rumble, standby-heavy Jhin-vs-Poppy, Lillia damage-assignment, Taric Bulwark assignment, Vex / Shadow response-activation, Pakaa Cub standby hide/reveal, Bandle Tree battlefield extra-standby hide, Garen same-battlefield static-aura, Darius same-battlefield static-aura, Ornn friendly-equipment static-aura, Baron Nashor other-friendly static-aura, Scarlet Pigeon source-combat static-aura, Petal Pixie same-battlefield ephemeral count-to-source static-aura, Soul Shepherd friendly-token static-aura, Waterbender source-lone-battle static-aura, Master Yi intro friendly single-defender static-aura, Master Yi level friendly-units static-aura, Wise Elder source-object filtered static-aura, Trifarian Training Grounds battlefield all-units static-aura, Reliable Siege Dog source same-location static-aura, Sett same-battlefield boon count-to-source static-aura, Lee Sin same-battlefield other-friendly filtered static-aura, Farron same-battlefield static-keyword aura, Taric same-battlefield Steadfast static-keyword aura, and Teemo standby reaction command streams can now be recovered from their representative initial states through their final representative battle / score state to the same final state hash. It does not close all official deck archetypes, token-creation command breadth, all standby reaction card effects / targeted standby reactions, battlefield standby reaction / cleanup breadth, non-ready-base standby cleanup breadth, complete combat damage assignment breadth, complete static-aura official breadth, complete RULE_TEXT keyword aura breadth, complete spell-duel / battle lifecycle breadth, all response windows, full card matrix readiness, frontend gates or final READY.

The current Forbidden Wasteland increment additionally proves one battlefield-source isolated-defender RULE_TEXT keyword-modifier route through direct continuous-effect projection, real defender damage, score-victory replay, and hidden-info guarded full-game helpers. Complete battlefield RULE_TEXT keyword-modifier breadth remains open.

The current Rumble legend increment additionally proves one legend-source friendly-filtered RULE_TEXT keyword aura route through real defender damage, score-victory replay, and hidden-info guarded full-game helpers. Complete friendly-filtered RULE_TEXT keyword aura breadth remains open.

The current Treasure Pile increment additionally proves one BehaviorSpec-driven battlefield-conquer trigger-payment route through both replayable `PAY_COST(SPEND_MANA:1)` and `PAY_COST(DECLINE)` branches, exhausted Gold token creation only on payment, declined window closure without `COST_PAID`, score-victory replay, and hidden-info guarded full-game helpers. Complete triggered-cost battlefield FUs and complete PaymentEngine breadth remain open.

The current Sunken Temple increment additionally proves one BehaviorSpec-driven battlefield-conquer powerful-unit trigger-payment route through both replayable `PAY_COST(SPEND_MANA:1)` and `PAY_COST(DECLINE)` branches, controlled main-deck draw only on payment, declined window closure without `COST_PAID`, score-victory replay, and hidden-info guarded full-game helpers. Complete triggered-cost battlefield FUs, complete powerful-unit condition breadth, and complete PaymentEngine breadth remain open.

The current Imperial Shrine increment additionally proves one BehaviorSpec-driven battlefield-conquer pay-return-unit create-Sand-Soldier route through `TRIGGER_PAYMENT`, both replayable `PAY_COST(SPEND_MANA:1)` and `PAY_COST(DECLINE)` branches, controlled unit return-to-hand and ready 2-power Sand Soldier token creation only on payment, declined window closure without `COST_PAID`, score-victory replay, hidden-info guarded full-game helpers, and opponent-view redaction of returned hidden hand object ids from battle metadata. Complete return-unit target-choice breadth, complete token lifecycle breadth, complete battlefield FUs, and complete official deck archetype breadth remain open.

The current Hall of Legends increment additionally proves one BehaviorSpec-driven battlefield-conquer pay-ready-legend route through `TRIGGER_PAYMENT`, both replayable `PAY_COST(SPEND_MANA:1)` and `PAY_COST(DECLINE)` branches, the exhausted controlled legend becoming ready only on payment, declined window closure without `COST_PAID`, score-victory replay, and hidden-info guarded full-game helpers. Complete legend target-choice breadth, complete battlefield FUs, and complete official deck archetype breadth remain open.

The current Hunting Grounds increment additionally proves one BehaviorSpec-driven battlefield-conquer overkill-token route through assigned overkill threshold checking, 1-power `UNL·T02` Warhawk token creation with `法盾` at that battlefield, score-victory replay, and hidden-info guarded full-game helpers. Complete token lifecycle breadth, complete overkill / damage-assignment breadth, complete battlefield FUs, and complete official deck archetype breadth remain open.

The current Candlelit Sanctum increment additionally proves one BehaviorSpec-driven battlefield-conquer reveal/recycle route through legal official Vex deck submission/opening, parsed `BATTLEFIELD_CONQUERED_REVEAL_TOP_TWO_RECYCLE`, controlled main-deck top-two reveal, parsed-count recycle to the bottom of the main deck, score-victory replay, and hidden-info guarded full-game helpers. The official optional any-number recycle choice, arbitrary return ordering prompt, complete reveal / recycle hidden-info breadth, complete battlefield FUs, and complete official deck archetype breadth remain open.

The current Thunder Sigil increment additionally proves one BehaviorSpec-driven battlefield-conquer recycle-rune route through legal official Vex deck submission/opening, parsed `BATTLEFIELD_CONQUERED_RECYCLE_RUNE`, controlled base-rune movement to the bottom of the main deck, score-victory replay, and hidden-info guarded full-game helpers. Optional rune choice prompts, complete base/main-deck replacement breadth, complete battlefield FUs, and complete official deck archetype breadth remain open.

The current Zaun Sump increment additionally proves one BehaviorSpec-driven battlefield-conquer discard-draw route through legal official Vex deck submission/opening, parsed `BATTLEFIELD_CONQUERED_DISCARD_DRAW`, controlled hand discard to graveyard, controlled main-deck draw, score-victory replay, and hidden-info guarded full-game helpers. Complete discard choice prompts, complete discard replacement / trigger breadth, complete battlefield FUs, and complete official deck archetype breadth remain open.

The current Seat of Power increment additionally proves one BehaviorSpec-driven battlefield-conquer draw-for-other-battlefields route through legal official Vex deck submission/opening, parsed `BATTLEFIELD_CONQUERED_DRAW_FOR_OTHER_BATTLEFIELDS`, two other controlled battlefield source objects, two-card controlled main-deck draw, score-victory replay, and hidden-info guarded full-game helpers. Ally / two-headed-giant semantics, complete other-battlefield control breadth, complete battlefield FUs, and complete official deck archetype breadth remain open.

The current Mount Targon increment additionally proves one BehaviorSpec-driven battlefield-conquer ready-runes-at-end route through legal official Vex deck submission/opening, parsed `BATTLEFIELD_CONQUERED_READY_RUNES_AT_END`, two exhausted controlled base runes, delayed end-turn ready markers, subsequent server-authored `END_TURN` marker cleanup and rune readying, score-victory replay, and hidden-info guarded full-game helpers. Optional rune choice prompts, complete delayed end-turn trigger breadth, complete battlefield FUs, and complete official deck archetype breadth remain open.

The current Minefield increment additionally proves one BehaviorSpec-driven battlefield-conquer mill route through legal official Vex deck submission/opening, parsed `BATTLEFIELD_CONQUERED_MILL_TOP_TWO`, controlled main-deck top-two movement into graveyard, score-victory replay, and hidden-info guarded full-game helpers. Complete main-deck / graveyard replacement breadth, complete battlefield FUs, and complete official deck archetype breadth remain open.

The current Rehearsal Hall increment additionally proves one BehaviorSpec-driven held move-unit-to-base route through legal official Jhin vs Vex deck submission/opening, parsed `BATTLEFIELD_HELD_MOVE_UNIT_TO_BASE`, server-authored movement of the surviving defender to owner base, score-victory replay through effective-controller battlefield scoring, and hidden-info guarded full-game helpers. Optional yes/no trigger prompts, complete same-battlefield target-choice breadth, complete movement / control-zone edge cases, complete battlefield FUs, and complete official deck archetype breadth remain open.

The current Back Alley Bar increment additionally proves one BehaviorSpec-driven moved-unit power route through legal official Vex deck submission/opening, server-authored `MOVE_UNIT`, parsed `BATTLEFIELD_UNIT_MOVED_AWAY_POWER_MODIFIER`, until-end-of-turn power-modifier ledger, score-victory replay, and hidden-info guarded full-game helpers. Complete same-turn movement policy, complete movement / control-zone edge cases, complete battlefield lifecycle breadth, complete battlefield FUs, and complete official deck archetype breadth remain open.

The current Piltover Academy increment additionally proves one BehaviorSpec-driven held-next-spell Echo marker route through legal official Poppy vs Vex deck submission/opening, parsed `BATTLEFIELD_HELD_NEXT_SPELL_GAINS_ECHO`, a derived next-spell window, Echo optional cost payment, repeated spell stack resolution, score-victory replay, and hidden-info guarded full-game helpers. Natural same-turn non-active spell access, Swift stack-response `PLAY_CARD` prompts, complete Echo optional prompt breadth, complete battlefield FUs, and complete official deck archetype breadth remain open.

The current Frost Hold increment additionally proves one BehaviorSpec-driven turn-start damage route through legal official Vex deck submission/opening, parsed `BATTLEFIELD_TURN_START_DAMAGE_ALL_UNITS`, same-battlefield target scoping for both players' official Wildclaw Beastmasters, pre-scoring damage application, score-victory replay, and hidden-info guarded full-game helpers. Complete turn-start battlefield trigger breadth, optional trigger choice prompts, complete battlefield FUs, and complete official deck archetype breadth remain open.

The current Duskpetal Lab increment additionally proves one BehaviorSpec-driven turn-start destroy-draw route through legal official Vex deck submission/opening, parsed `BATTLEFIELD_TURN_START_DESTROY_UNIT_DRAW`, same-battlefield controlled-unit destruction, one-card draw before scoring, offsite controlled-unit preservation, score-victory replay, and hidden-info guarded full-game helpers. Complete optional yes/no trigger prompts, complete turn-start battlefield trigger breadth, complete battlefield FUs, and complete official deck archetype breadth remain open.

The current Power Obelisk increment additionally proves one BehaviorSpec-driven first-turn extra-rune route through legal official Vex deck submission/opening, parsed `BATTLEFIELD_FIRST_TURN_EXTRA_RUNE`, a focused first-turn replay state derived from that opening, replayable P2 first-turn `RUNES_CALLED` count four, score-victory replay, and hidden-info guarded full-game helpers. Natural earliest opening rune-call timing, complete turn-start battlefield trigger breadth, complete battlefield FUs, and complete official deck archetype breadth remain open.

The current Glory Arena increment additionally proves one BehaviorSpec-driven first-turn score route through legal official Vex deck submission/opening, parsed `BATTLEFIELD_FIRST_TURN_GAIN_SCORE`, a focused first-turn replay state derived from that opening, replayable P2 first-turn one-score gain, score-victory replay, and hidden-info guarded full-game helpers. Natural earliest opening score timing, complete turn-start battlefield trigger breadth, complete battlefield FUs, and complete official deck archetype breadth remain open.

The current Forgotten Monument increment additionally proves one BehaviorSpec-driven score-delay route through legal official Vex deck submission/opening, parsed `BATTLEFIELD_SCORE_DELAY_UNTIL_THIRD_TURN`, a focused first-turn replay state derived from that opening, replayable P2 first-turn score prevention against `BATTLEFIELD_FIRST_TURN_GAIN_SCORE`, score-victory replay, and hidden-info guarded full-game helpers. Complete physical `此处` score scoping, natural earliest opening score timing, complete turn-start battlefield trigger breadth, complete battlefield FUs, and complete official deck archetype breadth remain open.

The current winning-score increase increment additionally proves one BehaviorSpec-driven static winning-score route through legal official Vex deck submission/opening, parsed `BATTLEFIELD_WINNING_SCORE_INCREASE`, a focused first-turn replay state derived from that opening, replayable P2 eighth score without a win, later score-victory at `winningScore=9`, and hidden-info guarded full-game helpers. Multiple-source stacking breadth, natural earliest opening score timing, complete battlefield FUs, and complete official deck archetype breadth remain open.

The current Dream Tree increment additionally proves one BehaviorSpec-driven friendly-spell draw route through an official `PLAY_CARD` target at the same battlefield, parsed `BATTLEFIELD_FRIENDLY_SPELL_DRAW_ONE`, one controlled main-deck draw, until-end trigger memory, spell stack resolution, score-victory replay, and hidden-info guarded full-game helpers. Complete friendly-spell target breadth, optional spell-duel target timing breadth, complete battlefield FUs, and complete official deck archetype breadth remain open.

The current Waste Hall increment additionally proves one BehaviorSpec-driven spell-power bonus route through legal official Vex deck submission/opening, an official `PLAY_CARD` spell stack entry, parsed `BATTLEFIELD_SPELL_POWER_PLUS_1`, same-battlefield friendly-unit target selection, until-end-of-turn power modification, spell stack resolution, score-victory replay, and hidden-info guarded full-game helpers. Optional trigger choice prompts, complete spell-duel target timing breadth, complete battlefield FUs, and complete official deck archetype breadth remain open.

The current Lost Library increment additionally proves one BehaviorSpec-driven high-cost spell insight route through legal official Vex deck submission/opening, official `OGN·180/298` `PLAY_CARD` spell stack entry, parsed `BATTLEFIELD_HIGH_COST_SPELL_INSIGHT_RECYCLE`, paid-mana threshold checking at 4, controlled main-deck top-card recycle to the bottom of the deck, spell stack resolution, score-victory replay, and hidden-info guarded full-game helpers. Optional trigger choice prompts, complete insight / recycle hidden-info breadth, complete spell-duel target timing breadth, complete battlefield FUs, and complete official deck archetype breadth remain open.

The current Ravenbloom increment additionally proves one BehaviorSpec-driven battlefield-defended reveal-top spell route through controlled main-deck top-card reveal, spell detection, moving the revealed spell to hand, score-victory replay, and hidden-info guarded full-game helpers. The non-spell recycle branch, complete reveal / recycle hidden-info breadth, complete battlefield FUs, and complete official deck archetype breadth remain open.

The current Ravenbloom non-spell increment additionally proves the BehaviorSpec-driven battlefield-defended miss branch through controlled main-deck top-card reveal, non-spell detection, recycling the revealed card to the bottom of the defending player's main deck, score-victory replay, and hidden-info guarded full-game helpers. Complete reveal / recycle hidden-info breadth, complete battlefield FUs, and complete official deck archetype breadth remain open.

The current Plunder Alley increment additionally proves one BehaviorSpec-driven battlefield-defended move-friendly-unit-to-base route through legal official Jhin vs Vex deck submission/opening, parsed `BATTLEFIELD_DEFENSE_MOVE_FRIENDLY_UNIT_TO_BASE`, server-authored defender target submission via `battlefieldTargetObjectIds`, movement of the selected surviving friendly defender to its owner's base, score-victory replay, and hidden-info guarded full-game helpers. Complete optional yes/no trigger prompts, complete movement / control-zone edge cases, complete battlefield FUs, and complete official deck archetype breadth remain open.

The current Vaults of Helia increment additionally proves one BehaviorSpec-driven held-battlefield non-token unit cost increase route through legal official Poppy deck submission/opening, parsed `BATTLEFIELD_HELD_NON_TOKEN_UNIT_COST_INCREASE`, prompt source requirement surcharge metadata, `COST_PAID` surcharge payload, stack resolution, score-victory replay, and hidden-info guarded full-game helpers. Complex multi-modifier payment stacking, token/non-token breadth, complete battlefield FUs, and complete official deck archetype breadth remain open.

The current Dunehorn Beast held-draw increment additionally proves one BehaviorSpec-driven unit battlefield-held draw route through legal official Jhin deck submission/opening, parsed `UNIT_BATTLEFIELD_HELD_DRAW`, surviving held unit source identity, two-card controlled main-deck draw, score-victory replay, and hidden-info guarded full-game helpers. The low-hand active-entry sentence has separate StaticAbilitySpec coverage and now also has a B0 official-deck replay; complete unit-held trigger breadth, complete battlefield FUs, and complete official deck archetype breadth are still open.

The current Dunehorn Beast low-hand active-entry increment additionally proves one BehaviorSpec-driven source-unit active-entry route through legal official Jhin deck submission/opening, parsed `SOURCE_UNIT_ENTER_READY`, post-play two-card hand-count satisfaction, self source entry metadata, score-victory replay, and hidden-info guarded full-game helpers. Complete active-entry family breadth, complete official deck archetype breadth, P0 full objective, and READY are still open.

The current Molten Drake other-friendly active-entry increment additionally proves one BehaviorSpec-driven other-friendly active-entry route through legal official Jhin deck submission/opening, parsed `OTHER_FRIENDLY_UNITS_ENTER_READY`, public face-up Molten Drake source identity, unpaid `HASTE_READY` Legion Rearguard entering active on a P1 battlefield, source entry metadata, score-victory replay, and hidden-info guarded full-game helpers. Complete active-entry family breadth, complete official deck archetype breadth, P0 full objective, and READY are still open.

The current Master Yi level active-entry increment additionally proves one BehaviorSpec-driven level-gated friendly-units active-entry route through legal official Master Yi deck submission/opening, parsed `FRIENDLY_UNITS_ENTER_READY`, P1 11-experience requirement satisfaction, public legend-zone source identity, unpaid `HASTE_READY` Demacia Envoy entering active on a P1 battlefield, source entry metadata, score-victory replay, and hidden-info guarded full-game helpers. Complete active-entry family breadth, complete official deck archetype breadth, P0 full objective, and READY are still open.

The current Card Trick draw/recycle increment additionally proves one official spell route through legal official Vex deck submission/opening, top-three controlled main-deck target choices, selected-card draw, unselected-card recycle-to-bottom, score-victory replay, exact-value hidden-info guarded full-game helpers, and spectator/private-zone stack-target redaction. Complete main-deck look/recycle breadth, complete hidden-information matrix, complete spell target breadth, P0 full objective, and READY are still open.

The current LeBlanc suppressed-ephemeral cleanup increment additionally proves the official spell-granted `瞬息` lifecycle suppression route through legal official Vex vs Lillia deck submission/opening, official `OGN·180/298` target selection against same-battlefield `OGN·096/298` Watchful Sentinel, same-battlefield official `UNL-090/219` LeBlanc static text, shared high-cost spell insight recycle, stack resolution, P2 turn-start cleanup with no protected-target `UNIT_DESTROYED.reason=EPHEMERAL_TURN_START`, score-victory replay, and hidden-info guarded full-game helpers. Together with the existing Flowing Time Mirror unit/equipment cleanup replays, the Flowing Time Mirror B0 cleanup and LeBlanc suppression representatives are covered. All ephemeral token/equipment lifecycle edges, P0 full objective, and READY are still open.

The current evidence-alignment increment records already-green B0 routes that were present in `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs` but missing from this evidence file. Those routes cover `UNL-029/219` Crimson Signet Treant conquest repeat; `UNL-218/219` Idol Valley unit-play boon; `UNL-215/219` Meteor Spring first-unit move-other; `UNL-214/219` Ghost Bay returned-unit call-rune; `OGN·280/298` Hidden Valley held draw; `OGN·288/298` Star Peak held call-rune; `SFD·219/221` Confetti Tree held each-player call-rune; `OGN·283/298` Navori Arena held grant-boon; `OGN·275/298` Unity Sanctum held create-Minion; `OGN·281/298` Hallowed Tomb held return-hero; `OGN·293/298` Grand Plaza held seven-units win; `SFD·214/221` Energy Hub held pay-power score; `OGN·286/298` Reckoner Arena held activate-unit-conquest; and `OGN·279/298` Fortified Position defend-grant-Steadfast. Each route starts from legal official deck submission/opening or a verified opening-derived midgame state, uses server-authored prompts / commands, runs through score victory or direct match win as appropriate, and replays through `MatchActionLogReplayer` to the same final state hash with hidden-info guarded B0 helpers. This is a documentation-only evidence repair and does not close complete battlefield FU breadth, complete optional prompt breadth, complete official deck archetype breadth, or READY.

The current Hub official-opening play-card surrender-win action-log replay smoke additionally proves the public Hub path can reach and resolve a real prompt-authored `PLAY_CARD` after official deck opening resources, advance the turn, emit a terminal surrender `MATCH_WON`, and replay the recorded command stream to the same final state hash. It does not by itself prove battle, score victory, all deck archetypes, all response windows, or complete card-effect breadth; those remain covered only by focused `FullGameEndToEndTests` representatives and remain open overall.

The current Moonveil Altar increment additionally proves one BehaviorSpec-driven battlefield-conquer ready-equipment route through legal official Rumble vs Rumble deck submission/opening, parsed `BATTLEFIELD_CONQUERED_READY_EQUIPMENT`, a red legal `SFD·022/221` Long Sword equipment object sourced from the official P1 deck, exhausted and attached to `SFD·006/221` Aggressive Dragonhound, real battle declaration against `OGN·096/298` Watchful Sentinel, `EQUIPMENT_READIED`, optional armament detach resolution, score-victory replay, and hidden-info guarded full-game helpers. Complete optional equipment-selection prompts, complete armament attachment edge cases, complete battlefield FUs, and complete official deck archetype breadth remain open.

The current Shirana Monastery increment additionally proves one BehaviorSpec-driven battlefield-conquer consume-boon draw route through legal official Vex vs Rumble deck submission/opening, parsed `BATTLEFIELD_CONQUERED_CONSUME_BOON_DRAW`, an official `UNL-057/219` Wildclaw Beastmaster field unit carrying a boon, real battle declaration against `OGN·096/298` Watchful Sentinel, `BOON_CONSUMED`, one controlled main-deck draw, score-victory replay, and hidden-info guarded full-game helpers. Optional yes/no prompts, complete boon targeting breadth, complete battlefield FUs, and complete official deck archetype breadth remain open.

The current Darius same-battlefield static-aura increment additionally proves the same `SAME_BATTLEFIELD_OTHER_FRIENDLY_UNITS_POWER_PLUS_ONE` BehaviorSpec route already covered by Garen also works through a legal official Darius deck route using `SFD·236/221` as a main-deck source and `SFD·006/221` Aggressive Dragonhound as the boosted ally. Complete static-aura official breadth, complete official deck archetype breadth, and READY remain open.

The current Ornn friendly-equipment static-aura increment additionally proves the `FRIENDLY_FIELD_EQUIPMENT_COUNT_TO_SOURCE_UNIT_POWER` BehaviorSpec route works through a legal official Rumble deck route using `SFD·085/221` Ornn as the source and `SFD·022/221` Long Sword as the public friendly equipment participant. Complete equipment attachment lifecycle breadth, complete static-aura official breadth, complete official deck archetype breadth, and READY remain open.

# Plan B / B0 Full-Game E2E Evidence

Date: 2026-06-27

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
- `HIDE_CARD` with `BATTLEFIELD:<Bandle Tree>` extra-standby destination
- score-based `END_TURN` advancement to `MATCH_WON`
- `SURRENDER`

The Hub official-opening play-card surrender-win action-log replay smoke (`OfficialDeckCanPlayPromptLegalCardReachSurrenderWinAndReplayThroughHub`) proves the public Hub path can take legal official decks through submission, ready, mulligan, prompt-selected rune tap / recycle resources, a prompt-authored `PLAY_CARD`, priority handoff, stack resolution, next-turn handoff, surrender `MATCH_WON`, both-player opponent-hand redaction, and `MatchActionLogReplayer` final-state hash recovery without using a development seed. It selects source, target, destination and required payment choices from server prompt metadata instead of hard-coding a card number.

The new engine regression proves the spell-duel close handoff chooses the battlefield task player when the turn player is the mover. This is the natural path that previous fixture-style tests did not cover.

The no-legal battle regression proves the next natural blocker is consumed by shared engine state rather than single-card logic. After spell duel closes, `CoreRuleEngine` checks the existing server-authored `DECLARE_BATTLE` requirements for the `START_BATTLE` task player. When no ready face-up attacker / defender declaration exists, the engine emits `BATTLE_SKIPPED`, records `BATTLEFIELD_BATTLE_SKIPPED:*` until end of turn, clears the blocking battlefield task family from state / snapshot projection, and returns to neutral open main timing.

The turn-start battle reopen regression proves still-contested battlefields do not remain idle after a no-legal battle skip expires. `ResolveTurnStart` now advances pending battlefield tasks after turn-start ready / draw / score state is built. The B0 probe drives multiple natural turns, observes repeated no-legal skips until the moved combatant naturally readies, then submits the first server-authored `DECLARE_BATTLE` candidate and observes `BATTLE_DECLARED` plus `BATTLE_CLOSED` from a legal official deck path.

The score-victory regression proves the same legal official-deck path can continue after real `BATTLE_CLOSED` through server-authored `END_TURN` prompts until battlefield scoring emits `SCORE_GAINED` and a single score-based `MATCH_WON`. The runtime fix restores ordinary open-main action to `TurnPlayerId` after a non-turn-player battle task closes with no further battlefield task, and prevents duplicate `MATCH_WON` during turn start when pre-rune-call scoring already won before the synthetic draw result is built.

The action-log replay regressions prove the score-victory command stream can be journaled and replayed to the same final state hash. `OfficialLowCurvePostBattleScoreVictoryActionLogReplaysToFinalStateHash` starts from the existing mirrored Jhin official low-curve battle-closed state and covers post-battle `END_TURN` scoring. `OfficialLowCurveFullGameScoreVictoryActionLogReplaysToFinalStateHash`, `DistinctOfficialLowCurveFullGameScoreVictoryActionLogReplaysToFinalStateHash`, and `StandbyHeavyOfficialLowCurveFullGameScoreVictoryActionLogReplaysToFinalStateHash` start from a seated official low-curve initial state, record `SUBMIT_DECK`, `READY`, `MULLIGAN`, tap/play/move/focus, reopened battle declaration, and score-victory `END_TURN` commands through `MatchJournal`, convert entries to recovered commands / events, and verify `MatchActionLogReplayer.VerifyFinalStateAsync` reaches the expected final state hash with no replay errors. The B0 test driver writes replayable raw command payloads for prompt-derived object ids and destinations instead of only storing `cmdType`.

The battle-prompt action-log replay regressions prove the two complex B0 prompt representatives are also recoverable from seated-room command logs. `OfficialDecksResolveMultiDefenderBattleDamageAssignmentActionLogReplaysToFinalStateHash` records a legal official Lillia multi-defender path through prompt-derived `DECLARE_BATTLE` and both players' `ASSIGN_COMBAT_DAMAGE` submissions, then replays to the same battle-closed final state hash. `OfficialDecksResolveShadowBattleResponseActivationActionLogReplaysToFinalStateHash` records a legal official Vex / Shadow path through prompt-derived `DECLARE_BATTLE`, `ACTIVATE_ABILITY`, stack resolution and response priority close, then replays to the same battle-closed final state hash. The damage raw payload now uses the protocol lower-camel `assignments[].sourceObjectId`, `assignments[].targetObjectId` and `assignments[].damage` fields used by recovery.

The standby hide/reveal action-log replay regression proves explicit standby setup is also recoverable from seated-room command logs. `StandbyOfficialDecksHideRevealAndScoreVictoryActionLogReplaysToFinalStateHash` records a legal official Poppy deck path that hides official `OGN·135/298` Pakaa Cub through prompt-derived `HIDE_CARD` with `STANDBY_A`, confirms `CARD_HIDDEN` does not expose `cardNo`, reveals the same base object through prompt-derived `REVEAL_CARD` with `STANDBY_REVEAL_0`, then continues through the existing non-standby battle / score-victory route. The hide/reveal raw payloads now preserve source object id, card number, destination, mode and optional cost fields used by recovery.

The battlefield extra-standby action-log replay regression proves a legal official deck can also use Bandle Tree's battlefield standby destination in the B0 full-game route. `StandbyOfficialDecksBattlefieldExtraStandbyHideAndScoreVictoryActionLogReplaysToFinalStateHash` records legal official Poppy decks whose battlefield set includes `OGN·278/298` Bandle Tree, follows the normal official opening seed path until the active standby player controls Bandle Tree and has `OGN·135/298` Pakaa Cub in hand, submits prompt-derived `HIDE_CARD` to `BATTLEFIELD:<Bandle Tree>`, confirms `CARD_HIDDEN.destinationZone = BATTLEFIELD` without `cardNo`, keeps the hidden card face-down at that battlefield, and continues through non-standby battle / score-victory replay with no `REVEAL_CARD` command in that stream. This slice changes only test / evidence coverage; it does not close battlefield standby reveal or complete extra-standby cleanup breadth.

The same-battlefield static-aura action-log replay regression proves a legal official deck can carry a data-driven `STATIC_AURA` through the B0 full-game route. `OfficialDeckMidgameAppliesSameBattlefieldStaticAuraAndScoreVictoryActionLogReplaysToFinalStateHash` records legal official Poppy decks containing `OGS·013/024` Garen and `UNL-092/219` Demacia Envoy, follows the normal official opening seed path, stages both friendly units and an opposing defender through server-authored `PLAY_CARD` / `MOVE_UNIT` prompts, verifies Garen's `BehaviorSpec.StaticAuras` projection targets the Envoy, declares battle with the boosted Envoy, observes `DAMAGE_APPLIED` with `basePower=2`, `staticPowerBonus=1`, `combatPower=3`, and `damage=3`, then continues through score-victory replay to the same final state hash. This slice changes only test / evidence coverage; it does not close complete static-aura breadth, complete official deck archetype breadth, or READY.

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

The Dream Tree friendly-spell draw action-log replay regression proves a legal official deck opening can feed a focused `FRIENDLY_UNIT_AT_THIS_BATTLEFIELD` spell-target trigger route before the game continues to a normal score victory. `OfficialDeckMidgameResolvesDreamTreeFriendlySpellDrawAndScoreVictoryActionLogReplaysToFinalStateHash` records a legal Vex deck opening that selected official `OGN·292/298` Dream Tree, then starts from a focused midgame main-phase state with `UNL-057/219` Wildclaw Beastmaster and opposing `OGN·096/298` Watchful Sentinel located at that P1 battlefield, plus official `SFD·034/221` Savage Strength in P1 hand. The server-authored `PLAY_CARD` route targets the same-battlefield friendly unit, emits `BATTLEFIELD_TRIGGER_RESOLVED`, `CARD_DRAWN`, and `STACK_ITEM_ADDED`, records `BATTLEFIELD_FRIENDLY_SPELL_DRAW_USED:P1:{battlefieldObjectId}` until end of turn, resolves the spell stack, then continues through score-victory action-log replay to the same final state hash. This slice does not close complete friendly-spell target breadth, optional spell-duel target timing breadth, complete battlefield FUs, complete official deck archetype breadth, or READY.

The Ravenbloom Conservatory defend reveal-spell action-log replay regression proves a legal official deck opening can feed a focused defended-battlefield reveal route with a controlled main-deck top card. `OfficialDeckMidgameResolvesRavenbloomDefendRevealSpellAndScoreVictoryActionLogReplaysToFinalStateHash` records legal Vex and Lillia deck openings that selected official `SFD·215/221` Ravenbloom Conservatory for P2, then starts from a focused midgame `START_BATTLE` state with P1 `UNL-057/219` Wildclaw Beastmaster, P2 `UNL-036/219` Mutant Kitten, and official `SFD·087/221` Prophet's Omen on top of P2's controlled main deck. The server-authored `DECLARE_BATTLE` route emits `BATTLEFIELD_TRIGGER_RESOLVED`, `CARDS_REVEALED`, and `CARD_DRAWN`, recognizes the revealed card as a spell, moves it to P2 hand, and continues through score-victory action-log replay to the same final state hash. This slice changes only test / evidence coverage; it does not close the non-spell recycle B0 branch, complete reveal / recycle hidden-info breadth, complete battlefield FUs, complete official deck archetype breadth, or READY.

The Ravenbloom Conservatory defend reveal non-spell action-log replay regression covers the miss branch of the same BehaviorSpec route. `OfficialDeckMidgameResolvesRavenbloomDefendRevealNonSpellRecycleAndScoreVictoryActionLogReplaysToFinalStateHash` records legal Vex and Lillia deck openings that selected official `SFD·215/221` Ravenbloom Conservatory for P2, then starts from a focused midgame `START_BATTLE` state with P1 `UNL-057/219` Wildclaw Beastmaster, P2 `UNL-036/219` Mutant Kitten defender, and a second official `UNL-036/219` Mutant Kitten on top of P2's controlled main deck. The server-authored `DECLARE_BATTLE` route emits `BATTLEFIELD_TRIGGER_RESOLVED`, `CARDS_REVEALED`, and `CARDS_RECYCLED`, recognizes the revealed card is not a spell, recycles it to the bottom of P2's main deck, and continues through score-victory action-log replay to the same final state hash. This slice changes only test / evidence coverage; it does not close complete reveal / recycle hidden-info breadth, complete battlefield FUs, complete official deck archetype breadth, or READY.

The Plunder Alley defend move-friendly-unit-to-base action-log replay regression proves a legal official deck opening can feed a focused defended-battlefield movement route. `OfficialDeckMidgameResolvesPlunderAlleyDefendMoveToBaseAndScoreVictoryActionLogReplaysToFinalStateHash` records legal Jhin and Vex deck openings that selected official `OGN·285/298` Plunder Alley for P2, then starts from a focused midgame `START_BATTLE` state with P1 `OGN·096/298` Watchful Sentinel and P2 `UNL-057/219` Wildclaw Beastmaster at that battlefield. The server-authored `DECLARE_BATTLE` route submits the defender through `battlefieldTargetObjectIds`, emits `BATTLEFIELD_TRIGGER_RESOLVED` and `UNIT_MOVED_TO_BASE`, moves the selected surviving friendly defender from battlefield to P2 base through the parsed `BATTLEFIELD_DEFENSE_MOVE_FRIENDLY_UNIT_TO_BASE` route, and continues through score-victory action-log replay to the same final state hash. This slice changes only test / evidence coverage; it does not close complete optional yes/no trigger prompts, complete movement / control-zone edge cases, complete battlefield FUs, complete official deck archetype breadth, or READY.

The Forbidden Wasteland battlefield isolated-defender RULE_TEXT keyword-modifier action-log replay regression proves a legal official deck opening can feed a battlefield-source keyword modifier from `BehaviorSpec.StaticAuras`. `OfficialDeckMidgameAppliesBattlefieldIsolatedDefenderKeywordModifierAndScoreVictoryActionLogReplaysToFinalStateHash` records legal official Vex and Rumble deck openings, probes until P2 selects official `UNL-210/219` Forbidden Wasteland, then starts the replay from a focused midgame `START_BATTLE` state with `UNL-057/219` Wildclaw Beastmaster and `UNL-090/219` LeBlanc at that P2 battlefield. The projected `BATTLEFIELD_ISOLATED_DEFENDER_KEYWORD_MODIFIER` RULE_TEXT route grants the only defender `坚守` with `keywordBonus=-2`, real defender damage records `basePower=4`, `combatPower=2`, and `damage=2`, and the score-victory action log replays to the same final state hash. `BattlefieldIsolatedDefenderKeywordModifierProjectionTests` separately proves the continuous effect projects for exactly one public defender and does not project when two defenders share the battlefield.

The source-lone-battle static-aura action-log replay regression proves a legal official deck can carry Waterbender's battle-conditional source-object `STATIC_AURA` through the B0 full-game route. `OfficialDeckMidgameAppliesSourceLoneBattleStaticAuraAndScoreVictoryActionLogReplaysToFinalStateHash` records legal official Lillia decks containing `OGN·055/298` Waterbender and `OGN·096/298` Watchful Sentinel, follows the normal official opening seed path, stages both units through server-authored `PLAY_CARD` / `MOVE_UNIT` prompts, submits a server-authorized `DECLARE_BATTLE` with Waterbender as the only attacker, verifies the projected `SOURCE_LONE_BATTLE_POWER` effect targets Waterbender itself, observes Waterbender's real `DAMAGE_APPLIED` with `basePower=2`, `staticPowerBonus=2`, `combatPower=4`, and `damage=4`, then continues through score-victory replay to the same final state hash. This slice changes only test / evidence coverage; it does not close complete source-lone-battle static-aura breadth, complete official deck archetype breadth, or READY.

The friendly single-defender static-aura action-log replay regression proves a legal official deck can carry Master Yi intro's legend-source `STATIC_AURA` through the B0 full-game route. `OfficialDeckMidgameAppliesFriendlySingleDefenderStaticAuraAndScoreVictoryActionLogReplaysToFinalStateHash` records a legal official Master Yi intro deck containing `OGS·019/024` Master Yi intro as the legend and `UNL-092/219` Demacia Envoy as the single friendly defender, follows the normal official opening seed path, stages Demacia Envoy through server-authored `PLAY_CARD` / `MOVE_UNIT` prompts to the opposing battlefield, submits a server-authorized `DECLARE_BATTLE` from the battlefield owner, observes the Envoy's real defender `DAMAGE_APPLIED` with `basePower=2`, `staticPowerBonus=2`, `combatPower=4`, and `damage=4`, then continues through score-victory replay to the same final state hash. This slice changes only test / evidence coverage; it does not close complete legend-source static-aura breadth, complete official deck archetype breadth, or READY.

The Master Yi level friendly-units static-aura action-log replay regression proves a legal official deck can carry an experience-gated legend-source `STATIC_AURA` through the B0 full-game route. `OfficialDeckMidgameAppliesMasterYiLevelFriendlyUnitsStaticAuraAndScoreVictoryActionLogReplaysToFinalStateHash` records a legal official Master Yi level deck containing `UNL-191/219` as the legend and `UNL-092/219` Demacia Envoy, follows the normal official opening seed path, starts the midgame route at 5 experience, uses Demacia Envoy's server-resolved on-play text to gain the sixth experience, verifies the legend-source `FRIENDLY_UNITS_POWER` continuous effect targets the Envoy with `SourcePath=CoreRuleEngine.ResolveFriendlyUnitsPowerBonus`, submits a server-authorized `DECLARE_BATTLE`, observes the Envoy's real attacker `DAMAGE_APPLIED` with `basePower=2`, `staticPowerBonus=1`, `combatPower=3`, and `damage=3`, then continues through score-victory replay to the same final state hash. This slice changes only test / evidence coverage; it does not close Master Yi `{{等级11>}}`, complete experience-gated legend-source static-aura breadth, complete official deck archetype breadth, or READY.

The Wise Elder source-object filtered static-aura action-log replay regression proves a legal official deck can carry a self-conditioned source-object `STATIC_AURA` through the B0 full-game route. `OfficialDeckMidgameAppliesWiseElderSourceObjectFilteredStaticAuraAndScoreVictoryActionLogReplaysToFinalStateHash` records a legal official Master Yi level deck containing `OGN·065/298` Wise Elder and `OGN·136/298` Arena Rookie, follows the normal official opening seed path, stages Wise Elder to a battlefield, uses Arena Rookie's server-authored targeted `PLAY_CARD` prompt to grant Wise Elder `{{增益}}`, verifies Wise Elder's `SOURCE_OBJECT_FILTERED_POWER` continuous effect targets itself with `SourcePath=CoreRuleEngine.ResolveSourceObjectFilteredPowerBonus`, submits a server-authorized `DECLARE_BATTLE`, observes Wise Elder's real attacker `DAMAGE_APPLIED` with boon-adjusted `basePower=5`, `staticPowerBonus=1`, `combatPower=6`, and `damage=6`, then continues through score-victory replay to the same final state hash. This slice changes only test / evidence coverage; it does not close complete source-object filtered static-aura breadth, complete official deck archetype breadth, or READY.

The battlefield all-units static-aura action-log replay regression proves a legal official deck can carry a public battlefield-source `STATIC_AURA` through the B0 full-game route. `OfficialDeckMidgameAppliesBattlefieldAllUnitsStaticAuraAndScoreVictoryActionLogReplaysToFinalStateHash` records legal official Vex decks whose battlefield set includes `OGN·294/298` Trifarian Training Grounds, probes official-opening seeds until P1 randomly selects that battlefield, stages `UNL-057/219` Wildclaw Beastmaster and an opposing defender to that battlefield through server-authored `PLAY_CARD` / `MOVE_UNIT` prompts, verifies the battlefield's `BATTLEFIELD_ALL_UNITS_POWER_PLUS_ONE` projection targets both attacker and defender, observes Wildclaw's real `DAMAGE_APPLIED` with `basePower=7`, `staticPowerBonus=1`, `combatPower=8`, and `damage=8`, then continues through score-victory replay to the same final state hash. This slice changes only test / evidence coverage; it does not close complete battlefield static-aura breadth, complete official deck archetype breadth, or READY.

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

`FullGameEndToEndTests.AssertNoHiddenZoneLeak` serializes each viewer snapshot after accepted full-game steps and rejects exposure of opponent hand, main-deck and rune-deck object ids. The current guard is centralized through `FullGameEndToEndTests.AssertAccepted`, so every accepted result routed through the shared B0 helper performs this hidden-zone snapshot check immediately. The battlefield extra-standby regression also asserts the `CARD_HIDDEN` payload for the Bandle Tree destination omits `cardNo` while preserving the public battlefield destination. This is a focused hidden-zone guard for the full-game probe and does not replace the broader `MatchRecovery` spectator validation suite.

## Validation

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

## Non-Closure

This evidence proves the engine can drive mirrored Jhin low-curve decks, a distinct Jhin-vs-Rumble official low-curve deck pair, and a standby-heavy Jhin-vs-Poppy official deck pair through setup, opening, live prompt-driven gameplay, contested battlefield task creation, no-legal battle skip, later turn-start battlefield reopen, real battle declaration, battle close and score-based match result without leaking hidden zones. It also proves official Lillia multi-defender damage-assignment paths can open and resolve `ASSIGN_COMBAT_DAMAGE` through server prompts, including Taric `壁垒` before LeBlanc `后排` ordering with printed/granted `坚守` battle effective power; official Vex / Shadow battle response paths can open and resolve `ACTIVATE_ABILITY` through server prompts; an official Poppy / Pakaa Cub standby path can hide and reveal a standby card through server prompts without exposing the hidden card number in the hide event; an official Poppy / Bandle Tree path can hide a standby card to a battlefield extra-standby destination and still finish through score-victory replay; official Poppy / Garen / Demacia Envoy, Vex / Baron Nashor / Wildclaw Beastmaster, Poppy / Scarlet Pigeon / Demacia Envoy, Lillia / Petal Pixie / Faerie token / Wildclaw Beastmaster, Lillia / Soul Shepherd / Warhawk token / Wildclaw Beastmaster, Lillia / Waterbender / Watchful Sentinel, Master Yi intro / Demacia Envoy, Master Yi level / Demacia Envoy, Master Yi level / Wise Elder / Arena Rookie / Watchful Sentinel, Vex / Trifarian Training Grounds / Wildclaw Beastmaster, Poppy / Reliable Siege Dog / Demacia Envoy, Poppy / Sett / Arena Rookie / Demacia Envoy, and Poppy / Lee Sin / Arena Rookie / Demacia Envoy paths can apply data-driven same-battlefield, non-local other-friendly, source-combat, same-battlefield ephemeral count-to-source, friendly-token filtered, source-lone-battle, friendly single-defender, experience-gated friendly-units, source-object filtered, battlefield-source all-units, source same-location threshold, same-battlefield boon count-to-source, and same-battlefield other-friendly filtered static auras to real battle damage and still finish through score-victory replay; official Jhin / Farron Captain / Ascended Believer and Lillia / Taric / LeBlanc paths can apply data-driven same-battlefield RULE_TEXT keyword auras to attacker and defender real battle damage and still finish through score-victory replay; and an official Vex / Shadow / Teemo path can reveal a hidden standby card as a stack reaction during response priority. The mirrored Jhin, distinct Jhin-vs-Rumble, standby-heavy Jhin-vs-Poppy, Lillia damage-assignment, Taric Bulwark assignment, Vex / Shadow response-activation, Pakaa Cub standby hide/reveal, Bandle Tree battlefield extra-standby hide, Garen same-battlefield static-aura, Baron Nashor other-friendly static-aura, Scarlet Pigeon source-combat static-aura, Petal Pixie same-battlefield ephemeral count-to-source static-aura, Soul Shepherd friendly-token static-aura, Waterbender source-lone-battle static-aura, Master Yi intro friendly single-defender static-aura, Master Yi level friendly-units static-aura, Wise Elder source-object filtered static-aura, Trifarian Training Grounds battlefield all-units static-aura, Reliable Siege Dog source same-location static-aura, Sett same-battlefield boon count-to-source static-aura, Lee Sin same-battlefield other-friendly filtered static-aura, Farron same-battlefield static-keyword aura, Taric same-battlefield Steadfast static-keyword aura, and Teemo standby reaction command streams can now be recovered from their representative initial states through their final representative battle / score state to the same final state hash. It does not close all official deck archetypes, token-creation command breadth, all standby reaction card effects / targeted standby reactions, battlefield extra-standby reveal / cleanup breadth, non-ready-base standby cleanup breadth, complete combat damage assignment breadth, complete static-aura official breadth, complete RULE_TEXT keyword aura breadth, complete spell-duel / battle lifecycle breadth, all response windows, full card matrix readiness, frontend gates or final READY.

The current Forbidden Wasteland increment additionally proves one battlefield-source isolated-defender RULE_TEXT keyword-modifier route through direct continuous-effect projection, real defender damage, score-victory replay, and hidden-info guarded full-game helpers. Complete battlefield RULE_TEXT keyword-modifier breadth remains open.

The current Rumble legend increment additionally proves one legend-source friendly-filtered RULE_TEXT keyword aura route through real defender damage, score-victory replay, and hidden-info guarded full-game helpers. Complete friendly-filtered RULE_TEXT keyword aura breadth remains open.

The current Treasure Pile increment additionally proves one BehaviorSpec-driven battlefield-conquer trigger-payment route through both replayable `PAY_COST(SPEND_MANA:1)` and `PAY_COST(DECLINE)` branches, exhausted Gold token creation only on payment, declined window closure without `COST_PAID`, score-victory replay, and hidden-info guarded full-game helpers. Complete triggered-cost battlefield FUs and complete PaymentEngine breadth remain open.

The current Sunken Temple increment additionally proves one BehaviorSpec-driven battlefield-conquer powerful-unit trigger-payment route through both replayable `PAY_COST(SPEND_MANA:1)` and `PAY_COST(DECLINE)` branches, controlled main-deck draw only on payment, declined window closure without `COST_PAID`, score-victory replay, and hidden-info guarded full-game helpers. Complete triggered-cost battlefield FUs, complete powerful-unit condition breadth, and complete PaymentEngine breadth remain open.

The current Imperial Shrine increment additionally proves one BehaviorSpec-driven battlefield-conquer pay-return-unit create-Sand-Soldier route through `TRIGGER_PAYMENT`, both replayable `PAY_COST(SPEND_MANA:1)` and `PAY_COST(DECLINE)` branches, controlled unit return-to-hand and ready 2-power Sand Soldier token creation only on payment, declined window closure without `COST_PAID`, score-victory replay, hidden-info guarded full-game helpers, and opponent-view redaction of returned hidden hand object ids from battle metadata. Complete return-unit target-choice breadth, complete token lifecycle breadth, complete battlefield FUs, and complete official deck archetype breadth remain open.

The current Hall of Legends increment additionally proves one BehaviorSpec-driven battlefield-conquer pay-ready-legend route through `TRIGGER_PAYMENT`, both replayable `PAY_COST(SPEND_MANA:1)` and `PAY_COST(DECLINE)` branches, the exhausted controlled legend becoming ready only on payment, declined window closure without `COST_PAID`, score-victory replay, and hidden-info guarded full-game helpers. Complete legend target-choice breadth, complete battlefield FUs, and complete official deck archetype breadth remain open.

The current Hunting Grounds increment additionally proves one BehaviorSpec-driven battlefield-conquer overkill-token route through assigned overkill threshold checking, 1-power `UNL·T02` Warhawk token creation with `法盾` at that battlefield, score-victory replay, and hidden-info guarded full-game helpers. Complete token lifecycle breadth, complete overkill / damage-assignment breadth, complete battlefield FUs, and complete official deck archetype breadth remain open.

The current Dream Tree increment additionally proves one BehaviorSpec-driven friendly-spell draw route through an official `PLAY_CARD` target at the same battlefield, parsed `BATTLEFIELD_FRIENDLY_SPELL_DRAW_ONE`, one controlled main-deck draw, until-end trigger memory, spell stack resolution, score-victory replay, and hidden-info guarded full-game helpers. Complete friendly-spell target breadth, optional spell-duel target timing breadth, complete battlefield FUs, and complete official deck archetype breadth remain open.

The current Waste Hall increment additionally proves one BehaviorSpec-driven spell-power bonus route through legal official Vex deck submission/opening, an official `PLAY_CARD` spell stack entry, parsed `BATTLEFIELD_SPELL_POWER_PLUS_1`, same-battlefield friendly-unit target selection, until-end-of-turn power modification, spell stack resolution, score-victory replay, and hidden-info guarded full-game helpers. Optional trigger choice prompts, complete spell-duel target timing breadth, complete battlefield FUs, and complete official deck archetype breadth remain open.

The current Lost Library increment additionally proves one BehaviorSpec-driven high-cost spell insight route through legal official Vex deck submission/opening, official `OGN·180/298` `PLAY_CARD` spell stack entry, parsed `BATTLEFIELD_HIGH_COST_SPELL_INSIGHT_RECYCLE`, paid-mana threshold checking at 4, controlled main-deck top-card recycle to the bottom of the deck, spell stack resolution, score-victory replay, and hidden-info guarded full-game helpers. Optional trigger choice prompts, complete insight / recycle hidden-info breadth, complete spell-duel target timing breadth, complete battlefield FUs, and complete official deck archetype breadth remain open.

The current Ravenbloom increment additionally proves one BehaviorSpec-driven battlefield-defended reveal-top spell route through controlled main-deck top-card reveal, spell detection, moving the revealed spell to hand, score-victory replay, and hidden-info guarded full-game helpers. The non-spell recycle branch, complete reveal / recycle hidden-info breadth, complete battlefield FUs, and complete official deck archetype breadth remain open.

The current Ravenbloom non-spell increment additionally proves the BehaviorSpec-driven battlefield-defended miss branch through controlled main-deck top-card reveal, non-spell detection, recycling the revealed card to the bottom of the defending player's main deck, score-victory replay, and hidden-info guarded full-game helpers. Complete reveal / recycle hidden-info breadth, complete battlefield FUs, and complete official deck archetype breadth remain open.

The current Plunder Alley increment additionally proves one BehaviorSpec-driven battlefield-defended move-friendly-unit-to-base route through legal official Jhin vs Vex deck submission/opening, parsed `BATTLEFIELD_DEFENSE_MOVE_FRIENDLY_UNIT_TO_BASE`, server-authored defender target submission via `battlefieldTargetObjectIds`, movement of the selected surviving friendly defender to its owner's base, score-victory replay, and hidden-info guarded full-game helpers. Complete optional yes/no trigger prompts, complete movement / control-zone edge cases, complete battlefield FUs, and complete official deck archetype breadth remain open.

The current Hub official-opening play-card surrender-win action-log replay smoke additionally proves the public Hub path can reach and resolve a real prompt-authored `PLAY_CARD` after official deck opening resources, advance the turn, emit a terminal surrender `MATCH_WON`, and replay the recorded command stream to the same final state hash. It does not by itself prove battle, score victory, all deck archetypes, all response windows, or complete card-effect breadth; those remain covered only by focused `FullGameEndToEndTests` representatives and remain open overall.

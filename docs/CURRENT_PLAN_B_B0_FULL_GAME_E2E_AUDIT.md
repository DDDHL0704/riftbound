# Plan B / B0 Full-Game E2E Audit

Date: 2026-06-28

Status: focused B0 Molten Drake other-friendly active-entry replay accepted; project remains **NOT READY**.

## Scope

This slice adds a server-authoritative full-game probe that starts from legal official deck submission rather than a hand-built combat fixture. The probe covers:

- both players submit an official deck through `SubmitDeckAsync`;
- both players ready and consume mulligan prompts;
- the server advances into main phase;
- the Hub opening smoke now selects a server-authored `PLAY_CARD` source / target / destination / payment shape from `ActionPromptCandidateDto.Metadata.sourceRequirements`, submits it after prompt-selected rune tap / recycle resources, observes `CARD_PLAYED`, `COST_PAID`, `STACK_ITEM_ADDED`, passes priority for both players, resolves the stack, advances to the next player with `END_TURN`, accepts that player's `SURRENDER`, emits `MATCH_WON`, checks both player snapshots keep the opponent hand hidden, and replays the recorded Hub command stream to the same final state hash;
- both players use server prompts to tap runes and play units;
- a live unit moves from base to the opponent battlefield;
- the resulting contested battlefield opens and closes spell duel focus through `PASS_FOCUS`;
- if the resulting `START_BATTLE` task has no legal ready attackers / defenders, the engine records `BATTLE_SKIPPED` and clears the blocking battlefield task family for that battlefield for the rest of the turn;
- later turn starts reopen the still-contested battlefield task after the end-of-turn skip marker expires;
- once both official-deck combatants have naturally readied across turns, the server exposes `DECLARE_BATTLE` and accepts the first server-authored battle declaration candidate;
- the real official-deck path emits `BATTLE_DECLARED` and `BATTLE_CLOSED`;
- after real battle close, repeated server `END_TURN` prompts drive battlefield scoring until score-based `MATCH_WON`;
- the score-victory result has a single `MATCH_WON` event and winner score satisfies the emitted `winningScore`;
- from an already battle-closed official low-curve state, a `MatchJournal`-recorded post-battle score-victory `END_TURN` command stream replays through `MatchActionLogReplayer` to the same final state hash and recovered event payload hash;
- from a seated official low-curve initial state, the full mirrored Jhin, distinct Jhin-vs-Rumble, and standby-heavy Jhin-vs-Poppy `SUBMIT_DECK` -> `READY` -> `MULLIGAN` -> gameplay -> `DECLARE_BATTLE` -> score-victory command streams replay through `MatchActionLogReplayer` to the same final state hash and recovered event payload hash;
- from a seated official Lillia initial state, the full multi-defender `DECLARE_BATTLE` -> `ASSIGN_COMBAT_DAMAGE` command stream replays through `MatchActionLogReplayer` to the same final state hash and recovered event payload hash;
- from a seated official Lillia / Taric / LeBlanc / Wildclaw Beastmaster initial state, the full `PLAY_CARD` / `MOVE_UNIT` staging -> reversed defender declaration -> server-authored `ASSIGN_COMBAT_DAMAGE` command stream replays through `MatchActionLogReplayer` to the same final state hash while Taric's printed `壁垒` orders him before LeBlanc's `后排`;
- from a seated official Vex initial state, the full Shadow `DECLARE_BATTLE` -> `ACTIVATE_ABILITY` -> stack resolution -> battle close command stream replays through `MatchActionLogReplayer` to the same final state hash and recovered event payload hash;
- from a seated official Poppy standby initial state, the full `HIDE_CARD` -> `REVEAL_CARD` -> non-standby battle -> score-victory command stream replays through `MatchActionLogReplayer` to the same final state hash and recovered event payload hash;
- from a seated official Poppy / Bandle Tree initial state, the full `HIDE_CARD` to `BATTLEFIELD:<Bandle Tree>` -> non-standby battle -> score-victory command stream replays through `MatchActionLogReplayer` to the same final state hash and recovered event payload hash while the battlefield standby card remains face-down;
- from a seated official Poppy initial state without Bandle Tree, a direct rejected `HIDE_CARD` to `BATTLEFIELD:<non-Bandle battlefield>` records `ErrorCodes.InvalidTarget`, no events, unchanged hand/rune/location state, then the same command stream continues through non-standby battle, score victory, and action-log replay to the same final state hash;
- from a seated official Jhin opening state, a verified legal official-deck opening feeds a focused midgame play state with public face-up P1 `OGN·011/298` Molten Drake in base and official `OGN·010/298` Legion Rearguard in hand; playing Legion Rearguard to a P1 battlefield without `HASTE_READY` resolves `OTHER_FRIENDLY_UNITS_ENTER_READY`, emits Molten Drake source entry metadata, then continues through score victory and action-log replay to the same final state hash;
- from a seated official Poppy / Garen / Demacia Envoy initial state, the full `PLAY_CARD` / `MOVE_UNIT` staging -> same-battlefield static-aura `DECLARE_BATTLE` -> score-victory command stream replays through `MatchActionLogReplayer` to the same final state hash and recovered event payload hash while `OGS·013/024` Garen's `BehaviorSpec.StaticAuras` projection gives `UNL-092/219` Demacia Envoy `staticPowerBonus=1`;
- from a seated official Darius initial state, the full `PLAY_CARD` / `MOVE_UNIT` staging -> same-battlefield static-aura `DECLARE_BATTLE` -> score-victory command stream replays through `MatchActionLogReplayer` to the same final state hash and recovered event payload hash while `SFD·236/221` Darius's `BehaviorSpec.StaticAuras` projection gives `SFD·006/221` Aggressive Dragonhound `staticPowerBonus=1`;
- from a seated official Rumble initial state, an official `SFD·022/221` Long Sword is staged as friendly public field equipment, then the full `PLAY_CARD` / `MOVE_UNIT` staging -> friendly-equipment count-to-source static-aura `DECLARE_BATTLE` -> score-victory command stream replays through `MatchActionLogReplayer` to the same final state hash while `SFD·085/221` Ornn's `BehaviorSpec.StaticAuras` projection recomputes Ornn from printed 4 power to effective 5 power without adding a separate `staticPowerBonus`;
- from a seated official Vex / Baron Nashor / Wildclaw Beastmaster initial state, the full `PLAY_CARD` / `MOVE_UNIT` staging -> other-friendly static-aura `DECLARE_BATTLE` -> score-victory command stream replays through `MatchActionLogReplayer` to the same final state hash while `UNL-147/219` Baron Nashor's `BehaviorSpec.StaticAuras` projection gives `UNL-057/219` Wildclaw Beastmaster `staticPowerBonus=2`;
- from a seated official Poppy / Scarlet Pigeon / Demacia Envoy initial state, the full `PLAY_CARD` / `MOVE_UNIT` staging -> source-combat static-aura two-attacker `DECLARE_BATTLE` -> score-victory command stream replays through `MatchActionLogReplayer` to the same final state hash while `UNL-154/219` Scarlet Pigeon's `BehaviorSpec.StaticAuras` source-combat route gives itself `staticPowerBonus=2`;
- from a seated official Poppy / Dune Drake initial state, the full `PLAY_CARD` / `MOVE_UNIT` staging -> opposing ready defender on the same battlefield -> source-attacking-ready-enemy static-aura one-attacker `DECLARE_BATTLE` -> score-victory command stream replays through `MatchActionLogReplayer` to the same final state hash while `OGN·131/298` Dune Drake's `BehaviorSpec.StaticAuras` / `SOURCE_ATTACKING_READY_ENEMY_UNIT_POWER` route gives itself `staticPowerBonus=2`;
- from a seated official Lillia / Petal Pixie / Wildclaw Beastmaster opening state, a verified legal official-deck opening feeds a midgame `START_BATTLE` state with `UNL-076/219` Petal Pixie, official `UNL·T07` Faerie token and opposing `UNL-057/219` Wildclaw Beastmaster at the same P1 battlefield; the same-battlefield ephemeral count-to-source static-aura `DECLARE_BATTLE` -> score-victory command stream replays through `MatchActionLogReplayer` to the same final state hash while Petal Pixie's `BehaviorSpec.StaticAuras` / `SAME_BATTLEFIELD_FRIENDLY_FILTERED_UNIT_COUNT_TO_SOURCE_POWER` route gives itself `staticPowerBonus=1`;
- from a seated official Lillia / Soul Shepherd / Wildclaw Beastmaster opening state, a verified legal official-deck opening feeds a midgame `START_BATTLE` state with `UNL-077/219` Soul Shepherd in base, official `UNL·T02` Warhawk token and opposing `UNL-057/219` Wildclaw Beastmaster at the same P1 battlefield; the friendly-token static-aura `DECLARE_BATTLE` -> score-victory command stream replays through `MatchActionLogReplayer` to the same final state hash while Soul Shepherd's `BehaviorSpec.StaticAuras` / `FRIENDLY_FILTERED_UNITS_POWER` route gives the token attacker `staticPowerBonus=1`;
- from a seated official Rumble / SFD·089 Rumble / Watchful Sentinel opening state, a verified legal official-deck opening feeds a midgame `START_BATTLE` state with `SFD·089/221` Rumble and opposing `OGN·096/298` Watchful Sentinel at the same P1 battlefield; the friendly-mechanical static-aura `DECLARE_BATTLE` -> score-victory command stream replays through `MatchActionLogReplayer` to the same final state hash while Rumble's `BehaviorSpec.StaticAuras` / `FRIENDLY_FILTERED_UNITS_POWER` tag-filter route gives Rumble itself `staticPowerBonus=1`;
- from a seated official Lillia / Rumble opening state, a verified legal official-deck opening feeds a midgame `START_BATTLE` state with `OGN·096/298` Watchful Sentinel and a second official `SFD·026/221` Rumble unit at the same P1 battlefield; P2's `SFD·181/221` Rumble legend projects `FRIENDLY_FILTERED_UNITS_KEYWORD` / `坚守` to the friendly mechanical defender, defender damage records `basePower=4`, `keyword=坚守`, `keywordBonus=1`, `combatPower=5`, and `damage=5`, and the score-victory command stream replays through `MatchActionLogReplayer` to the same final state hash;
- from a seated official Rumble opening state, a verified legal official-deck opening feeds a midgame `START_BATTLE` state with `SFD·071/221` Speeding Mech in P1 base, a second official `SFD·026/221` Rumble unit and opposing `OGN·096/298` Watchful Sentinel at the same P1 battlefield; Speeding Mech projects `FRIENDLY_FILTERED_UNITS_KEYWORD` / `法盾` and `游走` to the friendly mechanical unit without adding printed tags, and the score-victory command stream replays through `MatchActionLogReplayer` to the same final state hash;
- from a seated official Vex opening state, a verified legal official-deck opening selects P1 `OGN·297/298` Wind Hill / 疾风山丘, then server prompts play and move `UNL-057/219` Wildclaw Beastmaster to Wind Hill, project `RULE_TEXT:BATTLEFIELD_ALL_UNITS_KEYWORD` / `游走` from the battlefield source without adding printed tags, submit a server-authored precise `MOVE_UNIT` from `BATTLEFIELD:<Wind Hill>` to a second official P1 battlefield with optional cost `ROAM`, and continue through score victory and action-log replay to the same final state hash;
- from a seated official Jhin vs Vex opening state, a verified legal official-deck opening selects P2 `OGN·296/298` Void Gate / 虚空之门, then a focused midgame state keeps P2 `UNL-057/219` Wildclaw Beastmaster at that battlefield; the server-authored `PLAY_CARD` prompt lets P1 cast official `UNL-007/219` Punishment at that public battlefield target, the stack resolves through `BATTLEFIELD_TARGET_SPELL_SKILL_DAMAGE_BONUS` for 4 total damage, and the command stream continues through score victory and action-log replay to the same final state hash;
- from a seated official Vex opening state, a verified legal official-deck opening selects P1 `UNL-213/219` Mutation Garden / 蜕变花园, then a focused midgame state keeps P1 `UNL-057/219` Wildclaw Beastmaster at that battlefield; the server-authored `ACTIVATE_ABILITY` prompt exposes `BATTLEFIELD_UNIT_EXHAUST_GAIN_EXPERIENCE`, the command exhausts that unit, emits `BATTLEFIELD_TRIGGER_RESOLVED.amount = 1` and `EXPERIENCE_GAINED.totalExperience = 1`, and the command stream continues through score victory and action-log replay to the same final state hash;
- from a seated official Jhin opening state, a verified legal official-deck opening selects P1 `SFD·211/221` Marai Spire / 玛莱尖塔, then a focused midgame state keeps official `UNL-061/219` Center Stage in P1 hand with only 3 mana; the server-authored `PLAY_CARD` prompt exposes `ECHO` with the battlefield reduction reason, the command pays base 2 plus reduced Echo 1, records `battlefieldEchoCostReductionMana = 1`, creates a stack item with `effectRepeatCount = 2`, resolves to draw two cards, and the command stream continues through score victory and action-log replay to the same final state hash;
- from a seated official Rumble opening state, a verified legal official-deck opening selects P1 `SFD·213/221` Ornn's Forge / 奥恩的锻炉, then a focused midgame state keeps official `SFD·022/221` Long Sword in P1 hand, official `SFD·006/221` Aggressive Dragonhound in P1 base, and only 1 mana; the server-authored `PLAY_CARD` prompt exposes `minimumManaCost = 1` and `battlefieldEquipmentCostReductionMana = 1`, the command pays the reduced first-equipment cost, records `PLAYED_EQUIPMENT_THIS_TURN:P1`, resolves Long Sword onto the controlled unit, and the command stream continues through score victory and action-log replay to the same final state hash;
- from a seated official Rumble opening state, a verified legal official-deck opening selects P1 `SFD·208/221` Poro Forge / 魄罗熔炉, then a focused midgame state keeps official `SFD·181/221` Rumble legend ready, official `SFD·006/221` Aggressive Dragonhound in P1 base, and official `SFD·022/221` Long Sword in P1 base as a controlled `武装`; the server-authored `LEGEND_ACT` prompt exposes `LEGEND_EXHAUST_ATTACH_CONTROLLED_ARMAMENT_FROM_BATTLEFIELD` plus indexed controlled-unit / armament target choices, the command exhausts the legend, emits `BATTLEFIELD_TRIGGER_RESOLVED.trigger = BATTLEFIELD_CONTROLLED_LEGEND_ATTACH_ARMAMENT`, attaches Long Sword to the controlled unit, and the command stream continues through score victory and action-log replay to the same final state hash;
- from a seated official Rumble opening state without Poro Forge, a focused midgame state keeps the same ready Rumble legend, controlled unit and controlled armament; the server-authored `LEGEND_ACT` prompt does not expose `LEGEND_EXHAUST_ATTACH_CONTROLLED_ARMAMENT_FROM_BATTLEFIELD`, a direct `LEGEND_ACT` for that battlefield-granted ability is rejected without exhausting the legend or attaching the armament, and the command stream including that rejected command continues through score victory and action-log replay to the same final state hash;
- from a seated official Vex vs Rumble opening state, a verified legal official-deck opening selects P2 `UNL-206/219` Blood Altar / 鲜血祭坛, then a focused midgame `START_BATTLE` state keeps P1 official `UNL-057/219` Wildclaw Beastmaster attacking P2 official `OGN·096/298` Watchful Sentinel at that battlefield with exactly 3 P2 mana; the server-authored `DECLARE_BATTLE` / `ASSIGN_COMBAT_DAMAGE` route resolves `BATTLEFIELD_DESTROYED_IN_BATTLE_PAY_3_RECALL`, spends P2's 3 mana, suppresses `UNIT_DESTROYED`, removes the defender's damage, exhausts it, recalls it to P2 base, and the command stream continues through score victory and action-log replay to the same final state hash;
- from a seated official Vex opening state, a verified legal official-deck opening feeds a midgame `START_BATTLE` state with `SFD·207/221` Imperial Shrine, `UNL-057/219` Wildclaw Beastmaster, opposing `OGN·096/298` Watchful Sentinel and sufficient available mana at the same P1 battlefield; the conquest path opens `TRIGGER_PAYMENT`, the pay branch pays the parsed cost, returns the controlled attacker to hand, creates a ready 2-power Sand Soldier token at that battlefield, the decline branch closes without cost, return, or token creation, the returned hand object's id is redacted from opponent battle metadata snapshots, and both command streams replay through `MatchActionLogReplayer` to the same final state hash;
- from a seated official Vex opening state, a verified legal official-deck opening feeds a midgame `START_BATTLE` state with `SFD·210/221` Hall of Legends, `UNL-057/219` Wildclaw Beastmaster, opposing `OGN·096/298` Watchful Sentinel, sufficient available mana, and an exhausted P1 legend; the conquest path opens `TRIGGER_PAYMENT`, the pay branch pays the parsed cost and readies the controlled legend, the decline branch closes without cost and leaves that legend exhausted, and both command streams replay through `MatchActionLogReplayer` to the same final state hash;
- from a seated official Vex opening state, a verified legal official-deck opening selects P1 `OGN·295/298`, then starts a focused midgame movement state with `UNL-057/219` Wildclaw Beastmaster at that battlefield; the server-authored `MOVE_UNIT` prompt metadata omits `BATTLEFIELD_TO_BASE` for that source, direct battlefield-to-base `MOVE_UNIT` is rejected with `ErrorCodes.InvalidTarget` and unchanged state hash, and the command stream including that rejected command continues through score victory and action-log replay to the same final state hash;
- from a seated official Vex opening state, a verified legal official-deck opening selects P1 `SFD·216/221`, then starts a focused midgame play state with `UNL-057/219` Wildclaw Beastmaster in hand and sufficient mana; the server-authored `PLAY_CARD` prompt metadata omits that battlefield destination for the source, direct battlefield-destination `PLAY_CARD` is rejected with `ErrorCodes.InvalidTarget` and unchanged state hash, and the command stream including that rejected command continues through score victory and action-log replay to the same final state hash;
- from a seated official Vex opening state, a verified legal official-deck opening selects P1 `OGN·277/298` Back Alley Bar, then starts a focused midgame movement state with `UNL-057/219` Wildclaw Beastmaster at that battlefield; the server-authored `MOVE_UNIT` path moves that unit to base, resolves the parsed `BATTLEFIELD_UNIT_MOVED_AWAY_POWER_MODIFIER` until-end-of-turn ledger entry, and the score-victory command stream replays through `MatchActionLogReplayer` to the same final state hash;
- from a seated official Poppy vs Vex opening state, a verified legal official-deck opening selects P2 `UNL-216/219` Piltover Academy, then starts a focused midgame `START_BATTLE` state with P1 `OGN·096/298` Watchful Sentinel and P2 `UNL-034/219` Crimson Signet Treant at that battlefield; the held path resolves the parsed `BATTLEFIELD_HELD_NEXT_SPELL_GAINS_ECHO` marker, and a derived P2 neutral-open next-spell window plays official `UNL-007/219` Punishment with the granted Echo optional cost, resolves the repeated spell stack, and continues through score victory and action-log replay to the same final state hash;
- from a seated official Vex opening state, a verified legal official-deck opening selects P1 `UNL-212/219` Frost Hold, then starts a focused midgame main phase with both players' official `UNL-057/219` Wildclaw Beastmasters at that battlefield; server-authored `END_TURN` advances to P2 turn start, resolves parsed `BATTLEFIELD_TURN_START_DAMAGE_ALL_UNITS`, applies one pre-scoring damage to both same-battlefield units, and continues through score victory and action-log replay to the same final state hash;
- from a seated official Vex opening state, a verified legal official-deck opening selects P2 `UNL-209/219` Duskpetal Lab, then starts a focused midgame main phase with one P2 official `UNL-057/219` Wildclaw Beastmaster at Duskpetal Lab and another P2 Wildclaw at a different battlefield; server-authored `END_TURN` advances to P2 turn start, resolves parsed `BATTLEFIELD_TURN_START_DESTROY_UNIT_DRAW`, destroys only the same-battlefield controlled unit, draws one card before scoring, leaves the offsite controlled unit on the battlefield, and continues through score victory and action-log replay to the same final state hash;
- from a seated official Vex opening state, a verified legal official-deck opening selects P1 `OGN·284/298` Power Obelisk, then starts a focused first-turn replay state derived from that official opening; server-authored `END_TURN` advances to P2 first turn start, resolves parsed `BATTLEFIELD_FIRST_TURN_EXTRA_RUNE`, calls four runes for P2, and continues through score victory and action-log replay to the same final state hash;
- from a seated official Vex opening state, a verified legal official-deck opening selects P1 `OGN·290/298` Glory Arena, then starts a focused first-turn replay state derived from that official opening; server-authored `END_TURN` advances to P2 first turn start, resolves parsed `BATTLEFIELD_FIRST_TURN_GAIN_SCORE`, grants one score to P2, and continues through score victory and action-log replay to the same final state hash;
- from a seated official Vex opening state, a verified legal official-deck opening selects P1 `SFD·209/221` Forgotten Monument, then starts a focused first-turn replay state derived from that official opening with official `OGN·290/298` Glory Arena as the first-turn score source; server-authored `END_TURN` advances to P2 first turn start, resolves parsed `BATTLEFIELD_SCORE_DELAY_UNTIL_THIRD_TURN`, prevents the first-turn score, and continues through score victory and action-log replay to the same final state hash;
- from a seated official Vex opening state, a verified legal official-deck opening selects P1 `OGN·276/298` winning-score increase battlefield, then starts a focused first-turn replay state derived from that official opening with P2 on seven score and official `OGN·290/298` Glory Arena as the first-turn score source; server-authored `END_TURN` advances to P2 first turn start, raises the effective winning score to nine, grants P2 its eighth score without a win, and continues through score victory at the raised threshold with action-log replay to the same final state hash;
- from a seated official Vex opening state, a verified legal official-deck opening feeds a midgame `START_BATTLE` state with `UNL-217/219` Hunting Grounds, `UNL-057/219` Wildclaw Beastmaster and opposing `OGN·096/298` Watchful Sentinel at the same P1 battlefield; the conquest path assigns at least 3 overkill damage to the enemy unit, creates a 1-power `UNL·T02` Warhawk token with `法盾` at that battlefield, and the score-victory command stream replays through `MatchActionLogReplayer` to the same final state hash;
- from a seated official Vex opening state, a verified legal official-deck opening feeds a midgame `START_BATTLE` state with `OGN·291/298` Candlelit Sanctum, `UNL-057/219` Wildclaw Beastmaster and opposing `OGN·096/298` Watchful Sentinel at the same P1 battlefield; the conquest path reads the parsed reveal/recycle trigger, reveals the top two controlled main-deck cards, recycles the parsed count to the bottom of that deck, and the score-victory command stream replays through `MatchActionLogReplayer` to the same final state hash;
- from a seated official Vex opening state, a verified legal official-deck opening feeds a midgame `START_BATTLE` state with `OGN·287/298` Thunder Sigil / 雷霆之纹, `UNL-057/219` Wildclaw Beastmaster and opposing `OGN·096/298` Watchful Sentinel at the same P1 battlefield; the conquest path reads the parsed recycle-rune trigger, moves one controlled P1 base rune to the bottom of the P1 main deck, and the score-victory command stream replays through `MatchActionLogReplayer` to the same final state hash;
- from a seated official Vex opening state, a verified legal official-deck opening feeds a midgame `START_BATTLE` state with `OGN·298/298` Zaun Sump / 祖安地沟, `UNL-057/219` Wildclaw Beastmaster and opposing `OGN·096/298` Watchful Sentinel at the same P1 battlefield; the conquest path reads the parsed discard-draw trigger, discards one controlled P1 hand card to graveyard, draws one controlled P1 main-deck card, and the score-victory command stream replays through `MatchActionLogReplayer` to the same final state hash;
- from a seated official Vex opening state, a verified legal official-deck opening feeds a midgame `START_BATTLE` state with `SFD·217/221` Seat of Power / 权能之座, `UNL-057/219` Wildclaw Beastmaster and opposing `OGN·096/298` Watchful Sentinel at the same P1 battlefield; the focused state carries two other controlled P1 battlefield card objects from the official selected battlefield pool, the conquest path reads the parsed draw-for-other-battlefields trigger, draws two controlled P1 main-deck cards, and the score-victory command stream replays through `MatchActionLogReplayer` to the same final state hash;
- from a seated official Vex opening state, a verified legal official-deck opening feeds a midgame `START_BATTLE` state with `OGN·289/298` Mount Targon / 巨神峰之巅, `UNL-057/219` Wildclaw Beastmaster and opposing `OGN·096/298` Watchful Sentinel at the same P1 battlefield; the focused state carries two exhausted controlled P1 base runes, the conquest path schedules parsed end-turn ready-rune effects, subsequent server-authored `END_TURN` readies those runes and clears the markers, and the score-victory command stream replays through `MatchActionLogReplayer` to the same final state hash;
- from a seated official Vex opening state, a verified legal official-deck opening feeds a midgame `START_BATTLE` state with `SFD·212/221` Minefield, `UNL-057/219` Wildclaw Beastmaster and opposing `OGN·096/298` Watchful Sentinel at the same P1 battlefield; the conquest path reads the parsed mill trigger, moves the top two controlled main-deck cards to the graveyard, and the score-victory command stream replays through `MatchActionLogReplayer` to the same final state hash;
- from a seated official Vex vs Lillia opening state, a verified legal official-deck opening feeds a midgame `START_BATTLE` state with P2 `SFD·215/221` Ravenbloom Conservatory, P1 `UNL-057/219` Wildclaw Beastmaster, P2 `UNL-036/219` Mutant Kitten, and official `SFD·087/221` Prophet's Omen on top of P2's controlled main deck; the defense path reveals the top card, recognizes it as a spell, moves it to P2 hand, and the score-victory command stream replays through `MatchActionLogReplayer` to the same final state hash;
- from a seated official Vex vs Lillia opening state, a verified legal official-deck opening feeds a midgame `START_BATTLE` state with P2 `SFD·215/221` Ravenbloom Conservatory, P1 `UNL-057/219` Wildclaw Beastmaster, P2 `UNL-036/219` Mutant Kitten defender, and a second official `UNL-036/219` Mutant Kitten on top of P2's controlled main deck; the defense path reveals the top card, recognizes it is not a spell, recycles it to the bottom of P2's main deck, and the score-victory command stream replays through `MatchActionLogReplayer` to the same final state hash;
- from a seated official Jhin vs Vex opening state, a verified legal official-deck opening feeds a midgame `START_BATTLE` state with P2 `OGN·285/298` Plunder Alley, P1 `OGN·096/298` Watchful Sentinel, and P2 `UNL-057/219` Wildclaw Beastmaster defender at that battlefield; the defense path accepts the server-authored `battlefieldTargetObjectIds` defender target, resolves `BATTLEFIELD_DEFENSE_MOVE_FRIENDLY_UNIT_TO_BASE`, moves the defender to its owner's base, and the score-victory command stream replays through `MatchActionLogReplayer` to the same final state hash;
- from a seated official Jhin vs Vex opening state, a verified legal official-deck opening feeds a midgame `START_BATTLE` state with P2 `UNL-207/219` Rehearsal Hall, P1 `OGN·096/298` Watchful Sentinel, and P2 `UNL-057/219` Wildclaw Beastmaster defender at that battlefield; the held path resolves `BATTLEFIELD_HELD_MOVE_UNIT_TO_BASE`, moves the surviving defender to its owner's base, then uses effective-controller turn-start battlefield scoring to continue through score victory and action-log replay to the same final state hash;
- from a seated official Poppy opening state, a verified legal official-deck opening selects P1 `UNL-219/219` Vaults of Helia, then starts a focused midgame main phase with the parsed `BATTLEFIELD_HELD_NON_TOKEN_UNIT_COST_INCREASE:P1` marker active; P1 plays official `OGN·211/298` Loyal Craftsman through the server-authored `PLAY_CARD` prompt, the prompt metadata and `COST_PAID` event record base mana `3`, minimum/paid mana `4`, and `battlefieldHeldUnitCostIncreaseMana=1`, and the score-victory command stream replays through `MatchActionLogReplayer` to the same final state hash;
- from a seated official Jhin opening state, a verified legal official-deck opening feeds a midgame `START_BATTLE` state with P2 official `SFD·027/221` Dunehorn Beast defending a slow battlefield against P1 `OGN·096/298` Watchful Sentinel; the defender survives, the unit-source `UNIT_BATTLEFIELD_HELD_DRAW` trigger draws two controlled main-deck cards for P2, and the score-victory command stream replays through `MatchActionLogReplayer` to the same final state hash;
- from a seated official Jhin opening state, a verified legal official-deck opening feeds a focused midgame play state with P1 official `SFD·027/221` Dunehorn Beast plus two other cards in hand; playing Dunehorn Beast to a P1 battlefield leaves two cards in hand, resolves `SOURCE_UNIT_ENTER_READY`, emits self source entry metadata, then continues through score victory and action-log replay to the same final state hash;
- from a seated official Vex opening state, a verified legal official-deck opening feeds a midgame `START_BATTLE` state with `SFD·220/221` Treasure Pile, `UNL-057/219` Wildclaw Beastmaster, opposing `OGN·096/298` Watchful Sentinel and sufficient available mana at the same P1 battlefield; the conquest path opens `TRIGGER_PAYMENT`, accepts replayable `PAY_COST(SPEND_MANA:1)`, creates an exhausted Gold equipment token, and the score-victory command stream replays through `MatchActionLogReplayer` to the same final state hash;
- from the same seated official Vex / Treasure Pile opening family, the B0 route also accepts replayable `PAY_COST(DECLINE)`, emits `TRIGGER_PAYMENT_DECLINED` / declined `PAYMENT_WINDOW_CLOSED`, creates no Gold token, applies no `COST_PAID`, then continues through score victory and action-log replay to the same final state hash;
- from a seated official Vex opening state, a verified legal official-deck opening feeds a midgame `START_BATTLE` state with `SFD·218/221` Sunken Temple, `UNL-057/219` Wildclaw Beastmaster as a surviving powerful conquest attacker, opposing `OGN·096/298` Watchful Sentinel and sufficient available mana at the same P1 battlefield; the conquest path opens `TRIGGER_PAYMENT`, accepts replayable `PAY_COST(SPEND_MANA:1)`, draws one controlled main-deck card, and the score-victory command stream replays through `MatchActionLogReplayer` to the same final state hash;
- from the same seated official Vex / Sunken Temple opening family, the B0 route also accepts replayable `PAY_COST(DECLINE)`, emits `TRIGGER_PAYMENT_DECLINED` / declined `PAYMENT_WINDOW_CLOSED`, draws no card, applies no `COST_PAID`, then continues through score victory and action-log replay to the same final state hash;
- from a seated official Vex opening state, a verified legal official-deck opening selects `UNL-218/219` Idol Valley, then plays an official `UNL-057/219` Wildclaw Beastmaster to that battlefield; the parsed `BATTLEFIELD_PLAY_UNIT_PAY_BOON` route pays 1 mana, grants the played unit `{{增益}}`, and continues through score victory and action-log replay to the same final state hash;
- from a seated official Vex opening state, a verified legal official-deck opening selects `UNL-215/219` Meteor Spring, then plays a first unit to that battlefield while another friendly unit is already there; the parsed `BATTLEFIELD_FIRST_UNIT_PLAYED_MOVE_OTHER_TO_BASE` route moves the other same-battlefield friendly unit to base, records the once-per-turn marker, and continues through score victory and action-log replay to the same final state hash;
- from a seated official Vex opening state, a verified legal official-deck opening selects `UNL-214/219` Ghost Bay, then plays official `OGN·104/298` Reconsider to return a unit from that battlefield; the parsed `BATTLEFIELD_UNIT_RETURNED_PAY_CALL_RUNE` route pays 1 mana, calls an additional rune, and continues through score victory and action-log replay to the same final state hash;
- from seated official battlefield-held opening states, focused midgame B0 routes now cover `OGN·280/298` Hidden Valley draw-one, `OGN·288/298` Star Peak call-rune, and `SFD·219/221` Confetti Tree each-player-call-rune held triggers through `DECLARE_BATTLE`, score victory, hidden-info guarded helpers, and action-log replay;
- from seated official battlefield-held opening states, focused midgame B0 routes now cover `OGN·283/298` Navori Arena grant-boon, `OGN·275/298` Unity Sanctum create-Minion, `OGN·281/298` Hallowed Tomb return-hero, `OGN·293/298` Grand Plaza seven-units win, `SFD·214/221` Energy Hub pay-power score, and `OGN·286/298` Reckoner Arena activate-unit-conquest triggers through server-authored battle prompts and action-log replay;
- from a seated official Jhin vs Vex opening state, focused midgame B0 routes now also cover `OGN·279/298` Fortified Position defend-grant-Steadfast through server-authored defender declaration, real battle close, score victory, and action-log replay;
- from a seated official Crimson Signet Treant opening state, the B0 route now carries `UNL-029/219` Crimson Signet Treant's unit-conquest repeat into real conquest, observes repeated triggered resolution metadata, and continues through score victory and action-log replay;
- from a seated official Lillia / Waterbender / Watchful Sentinel initial state, the full `PLAY_CARD` / `MOVE_UNIT` staging -> source-lone-battle static-aura one-attacker `DECLARE_BATTLE` -> score-victory command stream replays through `MatchActionLogReplayer` to the same final state hash while `OGN·055/298` Waterbender's `BehaviorSpec.StaticAuras` / `SOURCE_LONE_BATTLE_POWER` route gives itself `staticPowerBonus=2`;
- from a seated official Master Yi intro / Demacia Envoy initial state, the full `PLAY_CARD` / `MOVE_UNIT` staging to the opposing battlefield -> friendly single-defender static-aura `DECLARE_BATTLE` -> score-victory command stream replays through `MatchActionLogReplayer` to the same final state hash while `OGS·019/024` Master Yi intro's `BehaviorSpec.StaticAuras` / `FRIENDLY_SINGLE_DEFENDING_UNIT_POWER` route gives the single defending `UNL-092/219` Demacia Envoy `staticPowerBonus=2`;
- from a seated official Master Yi level / Demacia Envoy initial state, the full `PLAY_CARD` / `MOVE_UNIT` staging after a server-resolved sixth-experience gain -> friendly-units static-aura `DECLARE_BATTLE` -> score-victory command stream replays through `MatchActionLogReplayer` to the same final state hash while `UNL-191/219` Master Yi level's `BehaviorSpec.StaticAuras` / `FRIENDLY_UNITS_POWER` route gives the attacking `UNL-092/219` Demacia Envoy `staticPowerBonus=1`;
- from a seated official Master Yi level / Wise Elder / Arena Rookie initial state, the full `PLAY_CARD` / `MOVE_UNIT` staging -> Arena Rookie boon grant -> source-object filtered static-aura `DECLARE_BATTLE` -> score-victory command stream replays through `MatchActionLogReplayer` to the same final state hash while `OGN·065/298` Wise Elder's `BehaviorSpec.StaticAuras` / `SOURCE_OBJECT_FILTERED_POWER` route sees its own `{{增益}}`, projects to itself, and gives the attacking Wise Elder `staticPowerBonus=1`;
- from a seated official Vex / Trifarian Training Grounds / Wildclaw Beastmaster initial state, the full official opening seed probe -> `PLAY_CARD` / `MOVE_UNIT` staging to `OGN·294/298` -> battlefield all-units static-aura `DECLARE_BATTLE` -> score-victory command stream replays through `MatchActionLogReplayer` to the same final state hash while the battlefield's `BehaviorSpec.StaticAuras` route gives both attacker and defender `BATTLEFIELD_ALL_UNITS_POWER_PLUS_ONE`, with Wildclaw damage `staticPowerBonus=1`;
- from a seated official Poppy / Reliable Siege Dog / Demacia Envoy initial state, the full `PLAY_CARD` / `MOVE_UNIT` staging -> source same-location static-aura `DECLARE_BATTLE` -> score-victory command stream replays through `MatchActionLogReplayer` to the same final state hash while `SFD·159/221` Reliable Siege Dog's `BehaviorSpec.StaticAuras` / `SOURCE_SAME_LOCATION_OTHER_FRIENDLY_UNIT_POWER` route gives itself `staticPowerBonus=1`;
- from a seated official Poppy / Sett / Arena Rookie / Demacia Envoy initial state, the full `PLAY_CARD` / `MOVE_UNIT` staging -> Arena Rookie boon grant -> same-battlefield boon count-to-source static-aura `DECLARE_BATTLE` -> score-victory command stream replays through `MatchActionLogReplayer` to the same final state hash while `OGN·240/298` Sett's `BehaviorSpec.StaticAuras` / `SAME_BATTLEFIELD_FRIENDLY_FILTERED_UNIT_COUNT_TO_SOURCE_POWER` route gives itself `staticPowerBonus=1`;
- from a seated official Poppy / Lee Sin / Arena Rookie / Demacia Envoy initial state, the full `PLAY_CARD` / `MOVE_UNIT` staging -> Arena Rookie boon grant -> same-battlefield other-friendly filtered static-aura `DECLARE_BATTLE` -> score-victory command stream replays through `MatchActionLogReplayer` to the same final state hash while `OGN·151/298` Lee Sin's `BehaviorSpec.StaticAuras` / `SAME_BATTLEFIELD_OTHER_FRIENDLY_FILTERED_UNITS_POWER` route gives the boon-bearing Envoy `staticPowerBonus=2`;
- from a seated official Jhin / Farron Captain / Ascended Believer initial state, the full `PLAY_CARD` / `MOVE_UNIT` staging -> same-battlefield static-keyword `DECLARE_BATTLE` -> score-victory command stream replays through `MatchActionLogReplayer` to the same final state hash and recovered event payload hash while `OGN·015/298` Farron Captain's `BehaviorSpec.StaticAuras` projection gives `UNL-004/219` Ascended Believer `keyword=强攻` / `keywordBonus=1`;
- from a seated official Lillia / Taric / LeBlanc initial state, the full `PLAY_CARD` / `MOVE_UNIT` staging to an opposing battlefield -> same-battlefield Steadfast static-keyword `DECLARE_BATTLE` -> follow-up battle task -> post-battle no-legal `BATTLE_SKIPPED` -> score-victory command stream replays through `MatchActionLogReplayer` to the same final state hash while `OGN·074/298` Taric's `BehaviorSpec.StaticAuras` projection gives `UNL-090/219` LeBlanc `keyword=坚守` / `keywordBonus=1`;
- from a seated official Vex / Wildclaw Beastmaster vs Rumble / LeBlanc opening with `UNL-210/219` Forbidden Wasteland selected, a focused midgame `START_BATTLE` state stages `UNL-057/219` Wildclaw Beastmaster and `UNL-090/219` LeBlanc at P2 Forbidden Wasteland; the battlefield isolated-defender RULE_TEXT keyword-modifier route gives the single defender `keyword=坚守` / `keywordBonus=-2`, and the score-victory command stream replays through `MatchActionLogReplayer` to the same final state hash;
- from a seated official Vex / Teemo / Shadow initial state, the full `HIDE_CARD` -> Shadow `ACTIVATE_ABILITY` -> `REVEAL_CARD` as `STANDBY_REACTION` -> Teemo stack resolution -> Shadow stack resolution -> battle close command stream replays through `MatchActionLogReplayer` to the same final state hash and recovered event payload hash;
- the score-victory path now runs the original mirrored Jhin deck, a distinct Jhin-vs-Rumble official deck pair, and a standby-heavy Jhin-vs-Poppy official deck pair;
- a legal official Lillia deck pair can drive multi-defender `DECLARE_BATTLE` into `ASSIGN_COMBAT_DAMAGE`, submit both players' assignments, and close battle with `DAMAGE_APPLIED` / `BATTLE_CLOSED`;
- a legal official Vex deck pair can drive `DECLARE_BATTLE` into `BATTLE_RESPONSE_PRIORITY_OPENED`, activate `UNL-194/219` Shadow through `ACTIVATE_ABILITY`, resolve the stack, return to battle response priority, and close battle with `BATTLE_RESPONSE_PRIORITY_CLOSED` / `BATTLE_CLOSED`;
- every accepted step that uses the shared B0 `AssertAccepted` helper now checks player snapshots for hidden opponent hand, main-deck and rune-deck object id leakage;
- the earlier surrender result smoke remains covered separately.

This accepted-step hidden snapshot guard slice adds no runtime rule changes. It tightens the B0 test harness by moving `AssertNoHiddenZoneLeak` into `FullGameEndToEndTests.AssertAccepted`, so accepted `SUBMIT_DECK`, `READY`, `MULLIGAN`, `TAP_RUNE`, `PLAY_CARD`, `MOVE_UNIT`, `DECLARE_BATTLE`, `ACTIVATE_ABILITY`, `ASSIGN_COMBAT_DAMAGE`, `PASS_PRIORITY`, `PASS_FOCUS`, `END_TURN`, `HIDE_CARD`, `REVEAL_CARD` and `SURRENDER` results covered by the shared helper all reject opponent hidden-zone object id leakage immediately.

This Hub official-opening play-card surrender-win action-log replay smoke adds no runtime rule changes. It tightens `GameHubJoinTests` coverage by proving a legal official deck pair can move through Hub submission / ready / mulligan / rune resource prompts into a prompt-authored `PLAY_CARD`, stack priority handoff, stack resolution, next-turn handoff, surrender `MATCH_WON`, basic opponent-hand redaction, and `MatchActionLogReplayer` final-state hash recovery without a development seed.

The first B0 runtime fix was narrow: when spell duel closes into an existing `START_BATTLE` task, the engine promotes `ActivePlayerId` to the task player (`CleanupTaskState.PlayerId`) instead of always keeping the turn player. This preserves the existing battlefield-owner declaration model and fixes natural games where the mover is not the battle-task player.

This slice narrows the next exposed B0 blocker: after spell duel closes, the engine now asks the existing server-authored `DECLARE_BATTLE` legality model whether the `START_BATTLE` task player has any legal declaration. If not, it writes a `BATTLEFIELD_BATTLE_SKIPPED:*` end-of-turn marker, emits `BATTLE_SKIPPED` with participant metadata, suppresses repeated pending-task / battlefield-task projection for that battlefield, and returns to the turn player in open main timing.

This turn-start slice narrows the follow-up B0 blocker: after turn-start ready / draw / score effects complete, `ResolveTurnStart` now advances still-contested battlefield tasks through the same shared `AdvancePendingBattlefieldTasksAfterStateChange` path used by movement and battle cleanup. The full-game probe proves no-legal skipped battlefields reopen on later turns and can reach real `DECLARE_BATTLE` / `BATTLE_CLOSED` once both sides' units are ready.

This score-victory slice narrows the next B0 blocker. When a non-turn-player `START_BATTLE` task resolves and no further battlefield task opens, `AdvancePendingBattlefieldTasksAfterStateChange` now restores `ActivePlayerId` to `TurnPlayerId` for ordinary open main timing so the next server-authored `END_TURN` prompt is accepted by the same authority check that produced it. `BuildTurnStartEvents` also avoids adding a second `MATCH_WON` when pre-rune-call battlefield scoring already emitted the win event.

This distinct-deck slice adds no runtime rule changes. It broadens the B0 probe from mirrored Jhin decks to a second legal official deck pairing: P1 uses `UNL-181/219` Jhin with `UNL-022/219`, while P2 uses `SFD·181/221` Rumble with `SFD·026/221`. The auto-driver also avoids selecting `待命` units as its representative battle-path unit so this B0 slice stays on the battle / score path instead of detouring into the standby cleanup family.

This standby-heavy slice also adds no runtime rule changes. It broadens the same battle / score probe to a Poppy official deck pair: P1 uses `UNL-181/219` Jhin with `UNL-022/219`, while P2 uses `UNL-203/219` Poppy with `UNL-116/219`. The existing standby-aware driver can keep the B0 route on a real `DECLARE_BATTLE` / `BATTLE_CLOSED` / score-victory path even when the low-curve deck includes standby-capable cards.

This damage-assignment slice adds no runtime rule changes. It broadens B0 from real battle close to a real official-deck multi-defender battle damage assignment window. Both players use legal Lillia green/blue decks containing official `UNL-036/219` Mutant Kitten (`壁垒`) and `UNL-090/219` LeBlanc (`后排`). The driver stages two invading units on one battlefield through server prompts, opens `BATTLE_DAMAGE_ASSIGNMENT_OPENED`, submits `ASSIGN_COMBAT_DAMAGE` for both players, and observes `DAMAGE_APPLIED` + `BATTLE_CLOSED` without hidden-zone leakage.

This response-activation slice adds no runtime rule changes. It broadens B0 from response-pass / assignment fixtures to a real official-deck battle response activation path. Both players use legal Vex green/purple decks with `UNL-232/219` Vex legend, `UNL-055/219` Vex champion, and official `UNL-194/219` Shadow. The driver follows Shadow's official text by playing it directly to a contested battlefield active, opens `BATTLE_RESPONSE_PRIORITY_OPENED`, submits prompt-authored `ACTIVATE_ABILITY` with the required payment resource action when quoted, resolves Shadow's stack item, observes `STUNNED`, returns to battle response priority, and then closes the battle without hidden-zone leakage.

This action-log replay slice adds no runtime rule changes. The earlier replay test started from the already verified mirrored Jhin official low-curve `BATTLE_CLOSED` state and covered only post-battle scoring. This slice extends that evidence back to the seated room baseline and across three score-victory deck representatives: mirrored Jhin, distinct Jhin-vs-Rumble, and standby-heavy Jhin-vs-Poppy. The replay tests record `SUBMIT_DECK`, `READY`, `MULLIGAN`, tap/play/move/focus, reopened battle declaration, and score-victory `END_TURN` commands through `MatchJournal`, store replayable raw command payloads for prompt-derived object ids, and verify `MatchActionLogReplayer.VerifyFinalStateAsync` reaches the exact expected final state hash with recovered event payload hashes.

This battle-prompt replay slice also adds no runtime rule changes. It extends the same seated-room action-log recovery check to the two remaining B0 complex prompt representatives: official Lillia multi-defender battle damage assignment and official Vex / Shadow battle response activation. The damage replay records prompt-derived `ASSIGN_COMBAT_DAMAGE` payloads with protocol lower-camel assignment fields, and the Shadow replay records prompt-derived `ACTIVATE_ABILITY` payloads with the selected source, target and payment resource choices.

This standby hide/reveal replay slice adds no runtime rule changes. It extends the seated-room action-log recovery check into explicit standby prompt coverage: a legal official Poppy deck hides official `OGN·135/298` Pakaa Cub through server-authored `HIDE_CARD` with `STANDBY_A`, verifies the `CARD_HIDDEN` payload does not expose `cardNo`, reveals the same object through `REVEAL_CARD` with `STANDBY_REVEAL_0`, then continues through the existing non-standby battle / score-victory route. The test driver now writes replayable raw payloads for `HIDE_CARD` and `REVEAL_CARD`.

This battlefield extra-standby replay slice adds no runtime rule changes. It extends the seated-room action-log recovery check to a legal official Poppy deck whose battlefields include official `OGN·278/298` Bandle Tree. The driver uses the normal official opening seed path until the active standby player controls Bandle Tree and has official `OGN·135/298` Pakaa Cub in hand, submits server-authored `HIDE_CARD` with destination `BATTLEFIELD:<Bandle Tree>`, verifies `CARD_HIDDEN.destinationZone = BATTLEFIELD` without `cardNo`, keeps the hidden card face-down at that battlefield, then continues through non-standby battle and score-victory replay. This slice deliberately does not model battlefield standby reveal breadth.

This battlefield extra-standby rejection replay slice also adds no runtime rule changes. It extends the seated-room action-log recovery check to a legal official Poppy standby deck whose battlefields intentionally omit Bandle Tree. The driver uses the normal official opening seed path until the active standby player has official `OGN·135/298` Pakaa Cub in hand, confirms the server-authored `HIDE_CARD` prompt does not expose the selected `BATTLEFIELD:<non-Bandle battlefield>` destination, submits that direct command anyway, verifies `ErrorCodes.InvalidTarget`, the Bandle Tree control error message, no events, unchanged state hash, unchanged rune pool and unchanged hand location, then continues through non-standby battle and score-victory replay with the rejected command in the journal. This deliberately covers the command-side guard and replay behavior; it does not model battlefield standby reveal or complete extra-standby cleanup breadth.

This same-battlefield static-aura replay slice also adds no runtime rule changes. It extends the seated-room action-log recovery check to a legal official Poppy deck containing official `OGS·013/024` Garen and `UNL-092/219` Demacia Envoy. The driver uses the normal official opening seed path, moves both friendly units to the same battlefield through server commands, verifies Garen projects `SAME_BATTLEFIELD_OTHER_FRIENDLY_UNITS_POWER_PLUS_ONE` to the Envoy via `BehaviorSpec.StaticAuras`, declares battle with the Envoy, observes `DAMAGE_APPLIED` with `basePower=2`, `staticPowerBonus=1`, `combatPower=3`, and `damage=3`, then continues through score victory and action-log replay.

The Darius follow-up same-battlefield static-aura replay slice also adds no runtime rule changes. It extends the same seated-room action-log recovery check to a legal official Darius deck containing `OGN·253/298` legend, `OGN·243/298` champion, official `SFD·236/221` Darius as the static-aura source, and `SFD·006/221` Aggressive Dragonhound as the boosted ally. The driver uses the normal official opening seed path, moves both friendly units to the same battlefield through server commands, verifies Darius projects `SAME_BATTLEFIELD_OTHER_FRIENDLY_UNITS_POWER_PLUS_ONE` to Dragonhound via `BehaviorSpec.StaticAuras`, declares battle with Dragonhound, observes `DAMAGE_APPLIED` with `basePower=3`, `staticPowerBonus=1`, `combatPower=4`, and `damage=4`, then continues through score victory and action-log replay.

This Ornn friendly-equipment count-to-source static-aura replay slice also adds no runtime rule changes. It extends the seated-room action-log recovery check to a legal official Rumble deck containing official `SFD·085/221` Ornn and official `SFD·022/221` Long Sword. The driver verifies official deck submission/opening first, stages Long Sword as a public friendly equipment in P1 base, then plays and moves Ornn through server prompts, verifies Ornn projects `FRIENDLY_FIELD_EQUIPMENT_COUNT_TO_SOURCE_UNIT_POWER` to itself via `BehaviorSpec.StaticAuras` with Long Sword as the participant, declares battle with Ornn, observes `DAMAGE_APPLIED` with recomputed `basePower=5`, no extra `staticPowerBonus`, `combatPower=5`, and `damage=5`, then continues through score victory and action-log replay.

This other-friendly static-aura replay slice also adds no runtime rule changes. It extends the seated-room action-log recovery check to a legal official Vex deck containing official `UNL-147/219` Baron Nashor and `UNL-057/219` Wildclaw Beastmaster. The driver uses a fixed seed list to find a stable official opening, stages Baron Nashor to base and Wildclaw to a battlefield through server commands, verifies Baron Nashor projects `OTHER_FRIENDLY_UNITS_POWER` to Wildclaw via `BehaviorSpec.StaticAuras`, declares battle with Wildclaw, observes `DAMAGE_APPLIED` with `basePower=7`, `staticPowerBonus=2`, `combatPower=9`, and `damage=9`, then continues through score victory and action-log replay. This deliberately covers the static-aura power path only; Baron Nest creation and spell/skill target protection remain open.

This source-combat static-aura replay slice also adds no runtime rule changes. It extends the seated-room action-log recovery check to a legal official Poppy deck containing official `UNL-154/219` Scarlet Pigeon and `UNL-092/219` Demacia Envoy. The driver uses the normal official opening seed path, stages both friendly units and an opposing defender through server commands, submits a server-authored `DECLARE_BATTLE` with both Scarlet Pigeon and Demacia Envoy as attackers, observes Scarlet Pigeon's `DAMAGE_APPLIED` with `basePower=3`, `staticPowerBonus=2`, `combatPower=5`, and `damage=5`, then continues through score victory and action-log replay. This deliberately covers the official-deck real-damage route for one source-combat static-aura representative; broader source-combat static-aura official-deck breadth remains open.

This Dune Drake source-attacking-ready-enemy static-aura replay slice also adds no runtime rule changes. It extends the seated-room action-log recovery check to a legal official Poppy deck containing official `OGN·131/298` Dune Drake. The driver uses the normal official opening seed path, stages Dune Drake and an opposing ready defender to the same battlefield through server-authored `PLAY_CARD` / `MOVE_UNIT` prompts, submits a server-authored `DECLARE_BATTLE` with Dune Drake as the only attacker, verifies the projected `SOURCE_ATTACKING_READY_ENEMY_UNIT_POWER` effect uses the ready enemy defender as participant, observes Dune Drake's `DAMAGE_APPLIED` with `basePower=5`, `staticPowerBonus=2`, `combatPower=7`, and `damage=7`, then continues through score victory and action-log replay. This deliberately covers the official-deck real-damage route for the Dune Drake source-attacking-ready-enemy representative; broader source-combat static-aura official-deck breadth remains open.

This Petal Pixie same-battlefield ephemeral count-to-source static-aura replay slice also adds no runtime rule changes. It starts from legal official Lillia deck submission/opening, then builds a focused midgame `START_BATTLE` state with official `UNL-076/219` Petal Pixie, an official `UNL·T07` Faerie token carrying `{{瞬息}}`, and an opposing `UNL-057/219` Wildclaw Beastmaster at the same P1 battlefield. The driver verifies Petal Pixie's `SAME_BATTLEFIELD_FRIENDLY_FILTERED_UNIT_COUNT_TO_SOURCE_POWER` continuous effect targets itself, uses the Faerie token as the same-battlefield ephemeral participant, records `basePower=2`, `staticPowerBonus=1`, `combatPower=3`, and `damage=3`, then continues through score victory and action-log replay. This deliberately covers the official token participant / battle-damage route for one same-battlefield ephemeral count-to-source representative; Lillia token creation and broader count-to-source official-deck breadth remain open.

This Soul Shepherd friendly-token static-aura replay slice also adds no runtime rule changes. It starts from legal official Lillia deck submission/opening, then builds a focused midgame `START_BATTLE` state with official `UNL-077/219` Soul Shepherd in base, an official `UNL·T02` Warhawk token, and an opposing `UNL-057/219` Wildclaw Beastmaster at the same P1 battlefield. The driver verifies Soul Shepherd's `FRIENDLY_FILTERED_UNITS_POWER` continuous effect targets the Warhawk token, uses that official token as the friendly filtered participant, records `basePower=1`, `staticPowerBonus=1`, `combatPower=2`, and `damage=2`, then continues through score victory and action-log replay. This deliberately covers the official token-unit battle-damage route for one friendly filtered static-power representative; token creation and broader friendly-filtered official-deck breadth remain open.

This Rumble friendly-mechanical static-aura replay slice also adds no runtime rule changes. It starts from legal official Rumble deck submission/opening, then builds a focused midgame `START_BATTLE` state with official `SFD·089/221` Rumble and an opposing `OGN·096/298` Watchful Sentinel at the same P1 battlefield. The driver verifies Rumble's `FRIENDLY_FILTERED_UNITS_POWER` continuous effect targets Rumble itself through the official `机械` tag filter, records `basePower=4`, `staticPowerBonus=1`, `combatPower=5`, and `damage=5`, then continues through score victory and action-log replay. This deliberately covers the tag-filtered self-boost route for one friendly filtered static-power representative; broader friendly-filtered official-deck breadth remains open.

This Forbidden Wasteland battlefield isolated-defender RULE_TEXT keyword-modifier replay slice adds a shared `MatchSession` continuous-effect projection path. It starts from legal official Vex and Rumble deck submission/opening with the P2 battlefield seed selecting official `UNL-210/219` Forbidden Wasteland, then builds a focused midgame `START_BATTLE` state with `UNL-057/219` Wildclaw Beastmaster attacking and `UNL-090/219` LeBlanc as the only public defender at that battlefield. The driver verifies the BehaviorSpec-driven `BATTLEFIELD_ISOLATED_DEFENDER_KEYWORD_MODIFIER` route applies `坚守` with `keywordBonus=-2`, records defender damage with `basePower=4`, `combatPower=2`, and `damage=2`, then continues through score victory and action-log replay. Projection tests separately prove the RULE_TEXT effect is present for exactly one defender and absent when defenders are not isolated.

This source-lone-battle static-aura replay slice also adds no runtime rule changes. It extends the seated-room action-log recovery check to legal official Lillia decks containing official `OGN·055/298` Waterbender and `OGN·096/298` Watchful Sentinel. The driver uses the normal official opening seed path, stages Waterbender and the opposing Watchful Sentinel through server commands to the same battlefield, submits a server-authored `DECLARE_BATTLE` with Waterbender as the only attacker, verifies the `SOURCE_LONE_BATTLE_POWER` continuous effect targets Waterbender itself, observes `DAMAGE_APPLIED` with `basePower=2`, `staticPowerBonus=2`, `combatPower=4`, and `damage=4`, then continues through score victory and action-log replay. This deliberately covers the official-deck real-damage route for the Waterbender source-lone-battle static-aura representative; broader source-lone-battle and source-object static-aura official-deck breadth remains open.

This friendly single-defender static-aura replay slice also adds no runtime rule changes. It extends the seated-room action-log recovery check to a legal official Master Yi intro deck containing official `OGS·019/024` Master Yi intro as the legend and `UNL-092/219` Demacia Envoy as the single friendly defending unit. The driver uses the normal official opening seed path, stages Demacia Envoy through server commands to the opposing battlefield, lets the battlefield owner declare battle against that single defender, observes the defender's `DAMAGE_APPLIED` with `basePower=2`, `staticPowerBonus=2`, `combatPower=4`, and `damage=4`, then continues through score victory and action-log replay. This deliberately covers the official-deck real-damage route for the Master Yi intro friendly single-defender static-aura representative; broader legend-source static-aura official-deck breadth remains open.

This Master Yi level friendly-units static-aura replay slice also adds no runtime rule changes. It extends the seated-room action-log recovery check to a legal official Master Yi level deck containing official `UNL-191/219` Master Yi level as the legend and `UNL-092/219` Demacia Envoy. The driver uses the normal official opening seed path, starts the midgame at 5 experience, lets Demacia Envoy's server-resolved on-play text grant the sixth experience, verifies the legend-source `FRIENDLY_UNITS_POWER` continuous effect targets Demacia Envoy via `BehaviorSpec.StaticAuras`, declares battle with the Envoy as attacker, observes `DAMAGE_APPLIED` with `basePower=2`, `staticPowerBonus=1`, `combatPower=3`, and `damage=3`, then continues through score victory and action-log replay. This deliberately covers the official-deck real-damage route for the Master Yi level experience-gated friendly-units static-aura representative; Master Yi `{{等级11>}}` and broader legend-source static-aura official-deck breadth remain open.

This Wise Elder source-object filtered static-aura replay slice also adds no runtime rule changes. It extends the seated-room action-log recovery check to a legal official Master Yi level green/orange deck containing official `OGN·065/298` Wise Elder and `OGN·136/298` Arena Rookie. The driver uses the normal official opening seed path, stages Wise Elder to a battlefield, plays Arena Rookie through the server-authored targeted `PLAY_CARD` prompt to grant Wise Elder `{{增益}}`, verifies Wise Elder projects `SOURCE_OBJECT_FILTERED_POWER` to itself via `BehaviorSpec.StaticAuras`, declares battle with Wise Elder as attacker, observes `DAMAGE_APPLIED` with boon-adjusted `basePower=5`, `staticPowerBonus=1`, `combatPower=6`, and `damage=6`, then continues through score victory and action-log replay. This deliberately covers the official-deck real-damage route for one source-object filtered static-aura representative; broader source-object filtered official-deck breadth remains open.

This battlefield all-units static-aura replay slice also adds no runtime rule changes. It extends the seated-room action-log recovery check to a legal official Vex deck whose battlefield set includes official `OGN·294/298` Trifarian Training Grounds. The driver probes deterministic official-opening seeds until P1's randomly selected battlefield is Trifarian Training Grounds, stages `UNL-057/219` Wildclaw Beastmaster and an opposing defender to that public battlefield through server commands, verifies the battlefield projects `BATTLEFIELD_ALL_UNITS_POWER_PLUS_ONE` to both attacker and defender via `BehaviorSpec.StaticAuras`, declares battle with Wildclaw, observes `DAMAGE_APPLIED` with `basePower=7`, `staticPowerBonus=1`, `combatPower=8`, and `damage=8`, then continues through score victory and action-log replay. This deliberately covers the official-deck real-damage route for one battlefield-source all-units static-aura representative; broader battlefield static-aura official-deck breadth remains open.

This battlefield all-units static-keyword replay slice also adds no runtime rule changes. It extends the seated-room action-log recovery check to a legal official Vex deck whose battlefield set includes official `OGN·297/298` Wind Hill / 疾风山丘. The driver probes deterministic official-opening seeds until P1's randomly selected battlefield is Wind Hill, stages `UNL-057/219` Wildclaw Beastmaster to Wind Hill through server-authored `PLAY_CARD` / `MOVE_UNIT` prompts, verifies the battlefield projects `RULE_TEXT:BATTLEFIELD_ALL_UNITS_KEYWORD` with granted `游走` to the unit via `BehaviorSpec.StaticAuras`, submits the server-authored precise battlefield `MOVE_UNIT` with optional cost `ROAM` to a second official P1 battlefield object, then continues through score victory and action-log replay. This deliberately covers the official-deck dynamic movement route for one battlefield-source all-units RULE_TEXT keyword representative; broader battlefield RULE_TEXT keyword official-deck breadth remains open.

This Void Gate target spell/skill damage bonus replay slice also adds no runtime rule changes. It extends the seated-room action-log recovery check to legal official Jhin and Vex decks whose battlefield set includes official `OGN·296/298` Void Gate / 虚空之门 for P2. The driver probes deterministic official-opening seeds until P2's randomly selected battlefield is Void Gate, stages `UNL-057/219` Wildclaw Beastmaster at that public battlefield, submits server-authored official `UNL-007/219` Punishment from P1 against that public target, resolves the stack, observes `DAMAGE_APPLIED.damage=4` from the parsed `BATTLEFIELD_TARGET_SPELL_SKILL_DAMAGE_BONUS` static ability, then continues through score victory and action-log replay. This deliberately covers one official-deck spell-stack route for a B4 battlefield static ability; complete spell/skill damage modifier timing edges, multi-target damage breadth, replacement ordering, full B4, and READY remain open.

This source same-location static-aura replay slice also adds no runtime rule changes. It extends the seated-room action-log recovery check to a legal official Poppy deck containing official `SFD·159/221` Reliable Siege Dog and `UNL-092/219` Demacia Envoy. The driver uses the normal official opening seed path, stages both friendly units and an opposing defender through server commands, verifies Reliable Siege Dog projects `SOURCE_SAME_LOCATION_OTHER_FRIENDLY_UNIT_POWER` to itself via `BehaviorSpec.StaticAuras` only while another friendly unit is at the same public location, declares battle with Reliable Siege Dog, observes `DAMAGE_APPLIED` with `basePower=2`, `staticPowerBonus=1`, `combatPower=3`, and `damage=3`, then continues through score victory and action-log replay. This deliberately covers the official-deck real-damage route for one same-location source-threshold static-aura representative; broader same-location / count-to-source official-deck breadth remains open.

This same-battlefield boon count-to-source static-aura replay slice also adds no runtime rule changes. It extends the seated-room action-log recovery check to a legal official Poppy deck containing official `OGN·240/298` Sett, `OGN·136/298` Arena Rookie and `UNL-092/219` Demacia Envoy. The driver uses the normal official opening seed path, stages Demacia Envoy to a battlefield, plays Arena Rookie through the server-authored targeted `PLAY_CARD` prompt to grant `{{增益}}` to Demacia Envoy, moves Sett to the same battlefield, verifies Sett projects `SAME_BATTLEFIELD_FRIENDLY_FILTERED_UNIT_COUNT_TO_SOURCE_POWER` to itself via `BehaviorSpec.StaticAuras` with Demacia Envoy as the same-battlefield boon participant, declares battle with Sett, observes `DAMAGE_APPLIED` with `basePower=5`, `staticPowerBonus=1`, `combatPower=6`, and `damage=6`, then continues through score victory and action-log replay. This deliberately covers the official-deck real-damage route for one same-battlefield filtered count-to-source static-aura representative; broader count-to-source official-deck breadth remains open.

This same-battlefield other-friendly filtered static-aura replay slice also adds no runtime rule changes. It extends the seated-room action-log recovery check to a legal official Poppy deck containing official `OGN·151/298` Lee Sin, `OGN·136/298` Arena Rookie and `UNL-092/219` Demacia Envoy. The driver uses the normal official opening seed path, stages Demacia Envoy to a battlefield, plays Arena Rookie through the server-authored targeted `PLAY_CARD` prompt to grant `{{增益}}` to Demacia Envoy, moves Lee Sin to the same battlefield, verifies Lee Sin projects `SAME_BATTLEFIELD_OTHER_FRIENDLY_FILTERED_UNITS_POWER` via `BehaviorSpec.StaticAuras` to the boon-bearing Envoy but not to himself, declares battle with the Envoy, observes `DAMAGE_APPLIED` with boon-adjusted `basePower=3`, `staticPowerBonus=2`, `combatPower=5`, and `damage=5`, then continues through score victory and action-log replay. This deliberately covers the official-deck real-damage route for one same-battlefield filtered target static-aura representative; broader same-battlefield filtered official-deck breadth remains open.

This same-battlefield static-keyword replay slice also adds no runtime rule changes. It extends the seated-room action-log recovery check to a legal official Jhin deck containing official `OGN·015/298` Farron Captain and `UNL-004/219` Ascended Believer. The driver uses the normal official opening seed path, moves both friendly units to the same battlefield through server commands, verifies Farron projects `SAME_BATTLEFIELD_OTHER_FRIENDLY_UNITS_KEYWORD` to the Believer via `BehaviorSpec.StaticAuras`, declares battle with the Believer, observes `DAMAGE_APPLIED` with `basePower=1`, `keyword=强攻`, `keywordBonus=1`, no `staticPowerBonus`, `combatPower=2`, and `damage=2`, then continues through score victory and action-log replay.

This same-battlefield Steadfast static-keyword replay slice adds one shared runtime task-advancement fix. It extends the seated-room action-log recovery check to a legal official Lillia deck containing official `OGN·074/298` Taric and `UNL-090/219` LeBlanc. The driver stages Taric and LeBlanc as defenders on the opponent battlefield, verifies Taric projects `SAME_BATTLEFIELD_OTHER_FRIENDLY_UNITS_KEYWORD` to LeBlanc via `BehaviorSpec.StaticAuras`, declares battle from the battlefield owner, observes defender `DAMAGE_APPLIED` with `basePower=4`, `keyword=坚守`, `keywordBonus=1`, no `staticPowerBonus`, `combatPower=5`, and `damage=5`, then continues through a follow-up server-authored battle declaration, post-battle no-legal `BATTLE_SKIPPED`, score victory, and action-log replay. The runtime change is limited to `AdvancePendingBattlefieldTasksAfterStateChange`: active `START_BATTLE` tasks after battle cleanup now set the task player active when a declaration is legal, or reuse the existing no-legal battle skip path when no declaration is legal, instead of leaving a WAIT-only blocker.

This Taric Bulwark assignment slice adds one shared runtime battle effective-power fix. It extends the seated-room action-log recovery check to legal official Lillia decks containing official `OGN·074/298` Taric, `UNL-090/219` LeBlanc and `UNL-057/219` Wildclaw Beastmaster. The driver stages Taric and LeBlanc as defenders at the Beastmaster's battlefield, intentionally submits defenders as LeBlanc then Taric, and verifies the server-authored `ASSIGN_COMBAT_DAMAGE` prompt orders legal targets as Taric first (`BULWARK_FIRST`) and LeBlanc last (`BACK_ROW_LAST`). `CoreRuleEngine` and `MatchSession` now compute assignment damage pools and lethal thresholds from battle effective power rather than printed base power, so printed/granted `坚守` makes both Taric and LeBlanc lethal threshold 5; Wildclaw assigns 5 damage to Taric then 2 to LeBlanc, and the action log replays to the same score-victory final state hash.

This standby reaction replay slice also adds no runtime rule changes. It extends the same seated-room action-log recovery check to a real priority-window standby reaction: a legal official Vex deck hides official `OGN·197/298` Teemo through `HIDE_CARD`, opens the existing Shadow battle-response stack, passes priority back to the hidden-card controller, reveals Teemo through `REVEAL_CARD` with `Mode=STANDBY_REACTION` and `Destination=STACK`, resolves Teemo's on-play self-power modifier, then resolves Shadow and closes the battle.

## Evidence

- Added `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs`.
- Strengthened `tests/Riftbound.ConformanceTests/BoardTaskQueueFoundationTests.cs` with `PassFocusClosesSpellDuelAndPromotesBattlefieldOwnerWhenMoverIsTurnPlayer`.
- Strengthened `tests/Riftbound.ConformanceTests/BoardTaskQueueFoundationTests.cs` with `PassFocusClosesSpellDuelAndSkipsStartBattleWhenNoLegalCombatants`.
- Strengthened `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs` with `OfficialLowCurveDecksReopenContestedBattleAfterSkippedCombatantsReadyAcrossTurns`.
- Strengthened `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs` with `OfficialLowCurveDecksReachScoreVictoryAfterRealBattleThroughServerPrompts`.
- Strengthened `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs` with `OfficialLowCurvePostBattleScoreVictoryActionLogReplaysToFinalStateHash`.
- Strengthened `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs` with `OfficialLowCurveFullGameScoreVictoryActionLogReplaysToFinalStateHash`.
- Strengthened `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs` with `DistinctOfficialLowCurveFullGameScoreVictoryActionLogReplaysToFinalStateHash`.
- Strengthened `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs` with `StandbyHeavyOfficialLowCurveFullGameScoreVictoryActionLogReplaysToFinalStateHash`.
- Strengthened `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs` with `OfficialDecksResolveMultiDefenderBattleDamageAssignmentActionLogReplaysToFinalStateHash`.
- Strengthened `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs` with `OfficialDecksResolveShadowBattleResponseActivationActionLogReplaysToFinalStateHash`.
- Strengthened `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs` with `StandbyOfficialDecksHideRevealAndScoreVictoryActionLogReplaysToFinalStateHash`.
- Strengthened `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs` with `StandbyOfficialDecksBattlefieldExtraStandbyHideAndScoreVictoryActionLogReplaysToFinalStateHash`.
- Strengthened `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs` with `OfficialDeckMidgameAppliesSameBattlefieldStaticAuraAndScoreVictoryActionLogReplaysToFinalStateHash`.
- Strengthened `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs` with `OfficialDeckMidgameAppliesDariusSameBattlefieldStaticAuraAndScoreVictoryActionLogReplaysToFinalStateHash`.
- Strengthened `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs` with `OfficialDeckMidgameAppliesOrnnFriendlyEquipmentStaticAuraAndScoreVictoryActionLogReplaysToFinalStateHash`.
- Strengthened `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs` with `OfficialDeckMidgameAppliesOtherFriendlyStaticAuraAndScoreVictoryActionLogReplaysToFinalStateHash`.
- Strengthened `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs` with `OfficialDeckMidgameAppliesSourceCombatStaticAuraAndScoreVictoryActionLogReplaysToFinalStateHash`.
- Strengthened `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs` with `OfficialDeckMidgameAppliesDuneDrakeSourceAttackingReadyEnemyStaticAuraAndScoreVictoryActionLogReplaysToFinalStateHash`.
- Strengthened `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs` with `OfficialDeckMidgameAppliesPetalPixieSameBattlefieldEphemeralCountStaticAuraAndScoreVictoryActionLogReplaysToFinalStateHash`.
- Strengthened `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs` with `OfficialDeckMidgameAppliesSoulShepherdFriendlyTokenStaticAuraAndScoreVictoryActionLogReplaysToFinalStateHash`.
- Strengthened `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs` with `OfficialDeckMidgameAppliesRumbleFriendlyMechanicalStaticAuraAndScoreVictoryActionLogReplaysToFinalStateHash`.
- Strengthened `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs` with `OfficialDeckMidgameAppliesRumbleLegendFriendlyMechanicalSteadfastAndScoreVictoryActionLogReplaysToFinalStateHash`.
- Strengthened `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs` with `OfficialDeckMidgameAppliesSourceLoneBattleStaticAuraAndScoreVictoryActionLogReplaysToFinalStateHash`.
- Strengthened `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs` with `OfficialDeckMidgameAppliesFriendlySingleDefenderStaticAuraAndScoreVictoryActionLogReplaysToFinalStateHash`.
- Strengthened `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs` with `OfficialDeckMidgameAppliesMasterYiLevelFriendlyUnitsStaticAuraAndScoreVictoryActionLogReplaysToFinalStateHash`.
- Strengthened `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs` with `OfficialDeckMidgameAppliesWiseElderSourceObjectFilteredStaticAuraAndScoreVictoryActionLogReplaysToFinalStateHash`.
- Strengthened `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs` with `OfficialDeckMidgameAppliesBattlefieldAllUnitsStaticAuraAndScoreVictoryActionLogReplaysToFinalStateHash`.
- Strengthened `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs` with `OfficialDeckMidgameProjectsBattlefieldAllUnitsStaticKeywordRoamAndScoreVictoryActionLogReplaysToFinalStateHash`.
- Strengthened `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs` with `OfficialDeckMidgameResolvesVoidGateTargetSpellSkillDamageBonusAndScoreVictoryActionLogReplaysToFinalStateHash`.
- Strengthened `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs` with `OfficialDeckMidgameResolvesMutationGardenGrantedUnitExperienceAndScoreVictoryActionLogReplaysToFinalStateHash`.
- Strengthened `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs` with `OfficialDeckMidgameResolvesMaraiSpireEchoCostReductionAndScoreVictoryActionLogReplaysToFinalStateHash`.
- Strengthened `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs` with `OfficialDeckMidgameAppliesSourceSameLocationStaticAuraAndScoreVictoryActionLogReplaysToFinalStateHash`.
- Strengthened `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs` with `OfficialDeckMidgameAppliesSameBattlefieldBoonCountStaticAuraAndScoreVictoryActionLogReplaysToFinalStateHash`.
- Strengthened `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs` with `OfficialDeckMidgameAppliesSameBattlefieldOtherFriendlyFilteredStaticAuraAndScoreVictoryActionLogReplaysToFinalStateHash`.
- Strengthened `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs` with `OfficialDeckMidgameAppliesSameBattlefieldStaticKeywordAndScoreVictoryActionLogReplaysToFinalStateHash`.
- Strengthened `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs` with `OfficialDeckMidgameAppliesSameBattlefieldSteadfastStaticKeywordAndScoreVictoryActionLogReplaysToFinalStateHash`.
- Strengthened `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs` with `OfficialDeckMidgameAppliesBattlefieldIsolatedDefenderKeywordModifierAndScoreVictoryActionLogReplaysToFinalStateHash`.
- Strengthened `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs` with `OfficialDeckMidgamePaysTreasurePileConquerGoldAndScoreVictoryActionLogReplaysToFinalStateHash`.
- Strengthened `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs` with `OfficialDeckMidgamePaysSunkenTemplePowerfulDrawAndScoreVictoryActionLogReplaysToFinalStateHash`.
- Strengthened `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs` with `OfficialDeckMidgameResolvesImperialShrineSandSoldierAndScoreVictoryActionLogReplaysToFinalStateHash`.
- Strengthened `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs` with `OfficialDeckMidgameResolvesHallOfLegendsReadyLegendAndScoreVictoryActionLogReplaysToFinalStateHash`.
- Strengthened `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs` with `OfficialDeckMidgameResolvesHuntingGroundsOverkillWarhawkAndScoreVictoryActionLogReplaysToFinalStateHash`.
- Strengthened `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs` with `OfficialDeckMidgameResolvesCandlelitSanctumConquerRevealRecycleAndScoreVictoryActionLogReplaysToFinalStateHash`.
- Strengthened `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs` with `OfficialDeckMidgameResolvesThunderSigilConquerRecycleRuneAndScoreVictoryActionLogReplaysToFinalStateHash`.
- Strengthened `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs` with `OfficialDeckMidgameResolvesMinefieldConquerMillAndScoreVictoryActionLogReplaysToFinalStateHash`.
- Strengthened `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs` with `OfficialDeckMidgameResolvesRehearsalHallHeldMoveUnitToBaseAndScoreVictoryActionLogReplaysToFinalStateHash`.
- Strengthened `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs` with `OfficialDeckMidgameResolvesDreamTreeFriendlySpellDrawAndScoreVictoryActionLogReplaysToFinalStateHash`.
- Strengthened `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs` with `OfficialDeckMidgameResolvesRavenbloomDefendRevealSpellAndScoreVictoryActionLogReplaysToFinalStateHash`.
- Strengthened `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs` with `OfficialDeckMidgameResolvesRavenbloomDefendRevealNonSpellRecycleAndScoreVictoryActionLogReplaysToFinalStateHash`.
- Added `tests/Riftbound.ConformanceTests/BattlefieldIsolatedDefenderKeywordModifierProjectionTests.cs` covering single-defender projection and multi-defender non-projection for `UNL-210/219` Forbidden Wasteland.
- Strengthened `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs` with `OfficialDeckMidgameOrdersTaricBulwarkBeforeBackRowInDamageAssignmentAndScoreVictoryActionLogReplaysToFinalStateHash`.
- Strengthened `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs` with `OfficialDecksResolveStandbyReactionDuringShadowResponseActionLogReplaysToFinalStateHash`.
- Strengthened `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs` with `DistinctOfficialLowCurveDecksReachScoreVictoryAfterRealBattleThroughServerPrompts`.
- Strengthened `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs` with `StandbyHeavyOfficialLowCurveDecksReachScoreVictoryAfterRealBattleThroughServerPrompts`.
- Strengthened `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs` with `OfficialDecksResolveMultiDefenderBattleDamageAssignmentThroughServerPrompts`.
- Strengthened `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs` with `OfficialDecksResolveShadowBattleResponseActivationThroughServerPrompts`.
- Test driver changed in `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs` to parameterize P1/P2 decks, force required official cards into legal low-curve Lillia / Vex decks for the damage-assignment and response-activation routes, support required official exclusive units, and skip `待命` units when choosing the representative play / move unit.
- Runtime changed in `src/Riftbound.Engine/CoreRuleEngine.cs` only in the spell-duel close handoff path.
- Runtime changed in `src/Riftbound.Engine/CoreRuleEngine.cs` turn start to advance pending battlefield tasks after turn-start ready / draw / score state is built.
- Runtime changed in `src/Riftbound.Engine/CoreRuleEngine.cs` to restore ordinary open main `ActivePlayerId` to `TurnPlayerId` after battlefield-task advancement finds no further contested battlefield.
- Runtime changed in `src/Riftbound.Engine/CoreRuleEngine.cs` to prevent duplicate turn-start `MATCH_WON` events when battlefield scoring wins before draw.
- Runtime changed in `src/Riftbound.Engine/CoreRuleEngine.cs` to resolve active post-battle `START_BATTLE` tasks during shared battlefield task advancement: legal tasks activate the task player, while no-legal tasks emit `BATTLE_SKIPPED` and continue advancement.
- Runtime changed in `src/Riftbound.Engine/CoreRuleEngine.cs` and `src/Riftbound.Engine/MatchSession.cs` so `ASSIGN_COMBAT_DAMAGE` damage pools and lethal thresholds use battle effective power, including printed combat keyword bonuses, RULE_TEXT-granted keyword bonuses and static power modifiers; assignment `DAMAGE_APPLIED` payloads now expose `combatRole`, ordered `assignmentIndex` and `assignmentRole`.
- Runtime changed in `src/Riftbound.Engine/CardPermissionKeywordRules.cs` so Swift `PLAY_CARD` timing is allowed during stack-priority response only when the pending stack context is `SPELL_DUEL_OPEN`; ordinary `NEUTRAL_CLOSED` priority remains Reaction-only. `src/Riftbound.Engine/CoreRuleEngine.cs` also keeps stale `PLAY_CARD` source-object replay rejected before hand-zone checks when the source is already pending on the stack.
- Runtime changed in `src/Riftbound.Engine/MatchSession.cs` to project battlefield isolated-defender RULE_TEXT keyword modifiers from `BehaviorSpec.StaticAuras` when the active battle has exactly one public defender at the source battlefield.
- Runtime changed in `src/Riftbound.Engine/MatchSession.cs` snapshot projection to redact object ids that have moved into a non-viewer hand, main deck, rune deck or hidden battlefield standby from battle / battlefield task / resolution metadata ids and object-id collections.
- Runtime projection / queue filtering changed in `src/Riftbound.Engine/MatchSession.cs` to make the skip marker suppress repeated same-turn battlefield tasks and hide the internal marker from public continuous-effect projection.
- Test driver changed in `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs` to write replayable raw command payloads for B0 prompt-derived commands instead of only `cmdType`, so action-log recovery can reconstruct the same `GameCommand` object ids / destinations / damage assignments / activated ability choices / hide-reveal standby choices / battlefield extra-standby destination used by the server prompt path.
- Test guard strengthened in `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs`: `AssertAccepted` now calls `AssertNoHiddenZoneLeak`, giving the B0 full-game harness step-level hidden snapshot coverage for every accepted result routed through the shared helper.

## Residuals

This is not a READY claim and does not close complete game resolution. The current B0 full-game probe now proves mirrored Jhin low-curve decks, a distinct Jhin-vs-Rumble low-curve official deck pair, and a standby-heavy Jhin-vs-Poppy official deck pair can submit legal decks, pass opening prompts, create a contested battlefield, consume no-legal battle tasks, reopen them on later turns, declare and close a real battle, and finish by score-based `MATCH_WON` without surrender. It also proves official Lillia multi-defender damage-assignment deck pairs can open and resolve `ASSIGN_COMBAT_DAMAGE` through server prompts, including Taric `壁垒` before LeBlanc `后排` ordering with printed/granted `坚守` battle effective power; official Vex / Shadow deck pairs can open and resolve a battle response activation through server prompts; official Poppy / Garen / Demacia Envoy, Darius legend / Darius static source / Aggressive Dragonhound, Rumble / Ornn / Long Sword, Vex / Baron Nashor / Wildclaw Beastmaster, Poppy / Scarlet Pigeon / Demacia Envoy, Lillia / Petal Pixie / Faerie token / Wildclaw Beastmaster, Lillia / Soul Shepherd / Warhawk token / Wildclaw Beastmaster, Lillia / Waterbender / Watchful Sentinel, Master Yi intro / Demacia Envoy, Master Yi level / Demacia Envoy, Master Yi level / Wise Elder / Arena Rookie / Watchful Sentinel, Vex / Trifarian Training Grounds / Wildclaw Beastmaster, Poppy / Reliable Siege Dog / Demacia Envoy, Poppy / Sett / Arena Rookie / Demacia Envoy, and Poppy / Lee Sin / Arena Rookie / Demacia Envoy decks can carry spec-driven same-battlefield, friendly-equipment count-to-source, non-local other-friendly, source-combat, same-battlefield ephemeral count-to-source, friendly-token filtered, source-lone-battle, friendly single-defender, experience-gated friendly-units, source-object filtered, battlefield-source all-units, source same-location threshold, same-battlefield boon count-to-source, and same-battlefield other-friendly filtered static auras into real battle damage and score-victory replay; official Jhin / Farron Captain / Ascended Believer and Lillia / Taric / LeBlanc decks can carry spec-driven same-battlefield RULE_TEXT keyword auras into attacker and defender real battle damage and score-victory replay; and post-battle active `START_BATTLE` tasks no longer strand the game in a WAIT-only B0 state. The action-log replay slice now proves the mirrored Jhin, distinct Jhin-vs-Rumble, standby-heavy Jhin-vs-Poppy, Lillia damage-assignment, Taric Bulwark assignment, Vex / Shadow response-activation, explicit Pakaa Cub standby hide/reveal, Bandle Tree battlefield extra-standby hide, Garen same-battlefield static-aura, Darius same-battlefield static-aura, Ornn friendly-equipment static-aura, Baron Nashor other-friendly static-aura, Scarlet Pigeon source-combat static-aura, Petal Pixie same-battlefield ephemeral count-to-source static-aura, Soul Shepherd friendly-token static-aura, Waterbender source-lone-battle static-aura, Master Yi intro friendly single-defender static-aura, Master Yi level friendly-units static-aura, Wise Elder source-object filtered static-aura, Trifarian Training Grounds battlefield all-units static-aura, Reliable Siege Dog source same-location static-aura, Sett same-battlefield boon count-to-source static-aura, Lee Sin same-battlefield other-friendly filtered static-aura, Farron same-battlefield static-keyword aura, Taric same-battlefield Steadfast static-keyword aura, and Vex / Shadow / Teemo standby reaction paths can be recovered from their representative initial states through the final battle / score result to the same final state hash. It does not prove all real deck archetypes, token-creation command breadth, all standby reaction card effects / targeted standby reactions, battlefield extra-standby reveal / cleanup breadth, non-ready-base standby cleanup breadth, complete combat damage assignment breadth, full static-aura official breadth, full RULE_TEXT keyword aura breadth, all response windows, or all card-effect families can complete a game.

This Dune Drake increment additionally proves a legal official Poppy / Dune Drake deck can carry `SOURCE_ATTACKING_READY_ENEMY_UNIT_POWER` through seated-room official opening, server-authored play / move / declaration prompts, ready-enemy participant projection, real battle damage, score victory, and action-log replay. It still does not close complete source-combat static-aura breadth, complete official deck archetype breadth, or READY.

This Petal Pixie increment additionally proves a legal official Lillia / Petal Pixie deck can carry `SAME_BATTLEFIELD_FRIENDLY_FILTERED_UNIT_COUNT_TO_SOURCE_POWER` with an official `UNL·T07` ephemeral token participant through a focused midgame battle declaration, real damage, score victory, and action-log replay. It still does not close Lillia token creation, complete count-to-source static-aura breadth, complete official deck archetype breadth, or READY.

This Soul Shepherd increment additionally proves a legal official Lillia / Soul Shepherd deck can carry `FRIENDLY_FILTERED_UNITS_POWER` with an official `UNL·T02` token-unit target through a focused midgame battle declaration, real damage, score victory, and action-log replay. It still does not close token creation, complete friendly-filtered static-aura breadth, complete official deck archetype breadth, or READY.

This Rumble increment additionally proves a legal official Rumble / SFD·089 Rumble deck can carry `FRIENDLY_FILTERED_UNITS_POWER` with a `TAG:机械` target filter and the source object also acting as the boosted target through a focused midgame battle declaration, real damage, score victory, and action-log replay. It still does not close complete friendly-filtered static-aura breadth, complete official deck archetype breadth, or READY.

This Rumble legend increment additionally proves a legal official Rumble deck can carry `FRIENDLY_FILTERED_UNITS_KEYWORD` with a `TAG:机械` target filter from the legend zone through a focused midgame battle declaration, real defender damage, score victory, and action-log replay. It still does not close complete friendly-filtered RULE_TEXT keyword aura breadth, complete official deck archetype breadth, or READY.

This Forbidden Wasteland increment additionally proves a legal official Vex / Rumble deck opening can carry a battlefield isolated-defender RULE_TEXT keyword modifier through a focused midgame battle declaration, real defender damage, score victory, and action-log replay. It also adds direct projection coverage for the isolated-defender condition. It still does not close complete battlefield RULE_TEXT keyword-modifier breadth, complete official deck archetype breadth, or READY.

This Treasure Pile increment additionally proves a legal official Vex deck opening can carry a BehaviorSpec-driven conquered-battlefield trigger-payment route through both `TRIGGER_PAYMENT` branches: replayable `PAY_COST(SPEND_MANA:1)` creates an exhausted Gold token, while replayable `PAY_COST(DECLINE)` closes the trigger window without `COST_PAID`, token creation, or hidden-info leakage. Both branches continue through score victory and action-log replay. It still does not close complete triggered-cost battlefield FUs, complete PaymentEngine breadth, complete official deck archetype breadth, or READY.

This Sunken Temple increment additionally proves a legal official Vex deck opening can carry a BehaviorSpec-driven conquered-battlefield powerful-unit trigger-payment route through both `TRIGGER_PAYMENT` branches: replayable `PAY_COST(SPEND_MANA:1)` draws one controlled main-deck card, while replayable `PAY_COST(DECLINE)` closes the trigger window without `COST_PAID`, draw, or hidden-info leakage. Both branches continue through score victory and action-log replay. It still does not close complete triggered-cost battlefield FUs, complete powerful-unit condition breadth, complete PaymentEngine breadth, complete official deck archetype breadth, or READY.

This Imperial Shrine increment additionally proves a legal official Vex deck opening can carry a BehaviorSpec-driven conquered-battlefield pay-return-unit create-Sand-Soldier route through `TRIGGER_PAYMENT`, replayable `PAY_COST(SPEND_MANA:1)` and `PAY_COST(DECLINE)`, controlled unit return-to-hand and ready 2-power Sand Soldier token creation only on payment, declined window closure without `COST_PAID`, score victory, action-log replay, and opponent-view battle metadata redaction for the returned hidden hand object. It still does not close complete return-unit target choice breadth, complete token lifecycle breadth, complete battlefield FUs, complete official deck archetype breadth, or READY.

This Hall of Legends increment additionally proves a legal official Vex deck opening can carry a BehaviorSpec-driven conquered-battlefield pay-ready-legend route through `TRIGGER_PAYMENT`, replayable `PAY_COST(SPEND_MANA:1)` and `PAY_COST(DECLINE)`, the exhausted controlled legend becoming ready only on payment, declined window closure without `COST_PAID`, score victory, and action-log replay. It still does not close complete legend target-choice breadth, complete battlefield FUs, complete official deck archetype breadth, or READY.

This Back Alley Bar increment additionally proves a legal official Vex deck opening can carry the BehaviorSpec-driven moved-unit power route through server-authored `MOVE_UNIT`, the parsed until-end-of-turn power-modifier ledger, score victory, and action-log replay. It still does not close complete same-turn movement policy, complete movement / control-zone edge cases, complete battlefield lifecycle breadth, complete battlefield FUs, complete official deck archetype breadth, or READY.

This Piltover Academy increment additionally proves a legal official Poppy vs Vex deck opening can carry the BehaviorSpec-driven held-next-spell Echo marker into a derived next-spell window, pay the granted Echo optional cost on official `UNL-007/219` Punishment, resolve the repeated spell stack, and continue through score victory plus action-log replay. It still does not close natural same-turn non-active spell access, Swift stack-response `PLAY_CARD` prompts, complete Echo optional prompt breadth, complete battlefield FUs, complete official deck archetype breadth, or READY.

This Hunting Grounds increment additionally proves a legal official Vex deck opening can carry a BehaviorSpec-driven conquered-battlefield overkill token route through server-authored `DECLARE_BATTLE`, assigned overkill threshold checking, 1-power `UNL·T02` Warhawk token creation with `法盾` at that battlefield, score victory, and action-log replay. It still does not close complete token lifecycle breadth, complete overkill / damage-assignment breadth, complete battlefield FUs, complete official deck archetype breadth, or READY.

This Candlelit Sanctum increment additionally proves a legal official Vex deck opening can carry the BehaviorSpec-driven conquered-battlefield reveal/recycle route through server-authored `DECLARE_BATTLE`, top-two controlled main-deck reveal, parsed-count recycle to the bottom of the main deck, score victory, action-log replay, and hidden-info guarded full-game helpers. It still does not close the official optional any-number recycle choice, arbitrary return ordering prompt, complete reveal / recycle hidden-info breadth, complete battlefield FUs, complete official deck archetype breadth, or READY.

This Thunder Sigil increment additionally proves a legal official Vex deck opening can carry the BehaviorSpec-driven conquered-battlefield recycle-rune route through server-authored `DECLARE_BATTLE`, parsed `BATTLEFIELD_CONQUERED_RECYCLE_RUNE`, controlled base-rune movement to the bottom of the main deck, score victory, action-log replay, and hidden-info guarded full-game helpers. It still does not close optional rune choice prompts, complete base/main-deck replacement breadth, complete battlefield FUs, complete official deck archetype breadth, or READY.

This Zaun Sump increment additionally proves a legal official Vex deck opening can carry the BehaviorSpec-driven conquered-battlefield discard-draw route through server-authored `DECLARE_BATTLE`, parsed `BATTLEFIELD_CONQUERED_DISCARD_DRAW`, controlled hand discard to graveyard, controlled main-deck draw, score victory, action-log replay, and hidden-info guarded full-game helpers. It still does not close complete discard choice prompts, complete discard replacement / trigger breadth, complete battlefield FUs, complete official deck archetype breadth, or READY.

This Seat of Power increment additionally proves a legal official Vex deck opening can carry the BehaviorSpec-driven conquered-battlefield draw-for-other-battlefields route through server-authored `DECLARE_BATTLE`, parsed `BATTLEFIELD_CONQUERED_DRAW_FOR_OTHER_BATTLEFIELDS`, two other controlled battlefield source objects, controlled main-deck draw count two, score victory, action-log replay, and hidden-info guarded full-game helpers. It still does not close ally / two-headed-giant semantics, complete other-battlefield control breadth, complete battlefield FUs, complete official deck archetype breadth, or READY.

This Mount Targon increment additionally proves a legal official Vex deck opening can carry the BehaviorSpec-driven conquered-battlefield ready-runes-at-end route through server-authored `DECLARE_BATTLE`, parsed `BATTLEFIELD_CONQUERED_READY_RUNES_AT_END`, two controlled exhausted base runes, delayed end-turn rune-ready markers, subsequent server-authored `END_TURN` marker cleanup and rune readying, score victory, action-log replay, and hidden-info guarded full-game helpers. It still does not close optional rune choice prompts, complete delayed end-turn trigger breadth, complete battlefield FUs, complete official deck archetype breadth, or READY.

This Minefield increment additionally proves a legal official Vex deck opening can carry the BehaviorSpec-driven conquered-battlefield mill route through server-authored `DECLARE_BATTLE`, moving the top two controlled main-deck cards to graveyard, score victory, action-log replay, and hidden-info guarded full-game helpers. It still does not close complete main-deck / graveyard replacement breadth, complete battlefield FUs, complete official deck archetype breadth, or READY.

This Rehearsal Hall increment additionally proves a legal official Jhin vs Vex deck opening can carry the BehaviorSpec-driven held move-unit-to-base route through server-authored `DECLARE_BATTLE`, move the surviving defender to its owner's base, recover turn-start battlefield scoring through owner / effective-controller fallback when the official battlefield object has no explicit `ControllerId`, and continue through score victory plus action-log replay. It still does not close optional yes/no trigger prompts, complete same-battlefield target-choice breadth, complete movement / control-zone edge cases, complete battlefield FUs, complete official deck archetype breadth, or READY.

This Ravenbloom increment additionally proves legal official Vex and Lillia deck openings can carry a BehaviorSpec-driven defended-battlefield reveal-top spell route through controlled main-deck top-card reveal, spell detection, moving the revealed spell to hand, score victory, and action-log replay. It still does not close the non-spell recycle B0 branch, complete reveal / recycle hidden-info breadth, complete battlefield FUs, complete official deck archetype breadth, or READY.

This Ravenbloom non-spell increment additionally proves the same legal official deck opening family can carry the miss branch through controlled main-deck top-card reveal, non-spell detection, recycling the revealed card to the bottom of the defending player's main deck, score victory, and action-log replay. It still does not close complete reveal / recycle hidden-info breadth, complete battlefield FUs, complete official deck archetype breadth, or READY.

This Plunder Alley increment additionally proves a legal official Jhin vs Vex deck opening can carry a BehaviorSpec-driven defended-battlefield move-friendly-unit-to-base route through server-authored `DECLARE_BATTLE`, parsed `BATTLEFIELD_DEFENSE_MOVE_FRIENDLY_UNIT_TO_BASE`, moving the selected surviving friendly defender to its owner's base, score victory, and action-log replay. It still does not close complete optional yes/no trigger prompts, complete movement / control-zone edge cases, complete battlefield FUs, complete official deck archetype breadth, or READY.

This Vaults of Helia increment additionally proves a legal official Poppy deck opening can carry the BehaviorSpec-driven held-battlefield non-token unit cost increase into a later server-authored `PLAY_CARD` prompt, charge an official non-token unit `+1` mana through prompt source requirements and `COST_PAID`, then continue through score victory and action-log replay. It still does not close complex multi-modifier payment stacking, token/non-token breadth, complete battlefield FUs, complete official deck archetype breadth, or READY.

This Dunehorn Beast held-draw increment additionally proves a legal official Jhin deck opening can carry the BehaviorSpec-driven unit battlefield-held draw trigger through a real `DECLARE_BATTLE`, draw two controlled main-deck cards from the surviving held unit source, then continue through score victory and action-log replay. The low-hand active-entry sentence has separate StaticAbilitySpec coverage and now also has a B0 official-deck replay below. This B0 slice still does not close complete unit-held trigger breadth, complete battlefield FUs, complete official deck archetype breadth, or READY.

This Dunehorn Beast low-hand active-entry increment additionally proves a legal official Jhin deck opening can carry `SOURCE_UNIT_ENTER_READY` into the B0 score-victory route. The focused state keeps P1 `SFD·027/221` Dunehorn Beast in hand with exactly two other cards; after the server-authored `PLAY_CARD` to a P1 battlefield, the controller has two cards remaining, Dunehorn Beast enters active, `UNIT_PLAYED_TO_BATTLEFIELD` records `entryStaticAbilityKind=SOURCE_UNIT_ENTER_READY` plus self source object/card metadata, and the command stream continues through score victory and final-state replay. It still does not close complete active-entry family breadth, complete official deck archetype breadth, P0 full objective, or READY.

This Molten Drake other-friendly active-entry increment additionally proves a legal official Jhin deck opening can carry `OTHER_FRIENDLY_UNITS_ENTER_READY` into the B0 score-victory route. The focused state keeps public face-up P1 `OGN·011/298` Molten Drake in base and official `OGN·010/298` Legion Rearguard in hand; after the server-authored `PLAY_CARD` to a P1 battlefield without `HASTE_READY`, Legion Rearguard enters active, `UNIT_PLAYED_TO_BATTLEFIELD` records `entryStaticAbilityKind=OTHER_FRIENDLY_UNITS_ENTER_READY` plus Molten Drake source object/card metadata, and the command stream continues through score victory and final-state replay. It still does not close complete active-entry family breadth, complete official deck archetype breadth, P0 full objective, or READY.

This Swift stack-priority increment additionally proves official `UNL-007/219` Punishment can be offered and accepted from a `STACK_PRIORITY` prompt when the pending stack item is part of a `SPELL_DUEL_OPEN` context, while `P4PermissionKeywordTimingSeparatesSwiftReactionAndOrdinaryWindows` keeps ordinary priority Swift rejected. It still does not close ordinary priority Swift, complete Swift / Reaction timing, complete spell-duel lifecycle, all target-bearing Swift spells, or READY.

This evidence-alignment increment records already-green B0 routes that were present in `FullGameEndToEndTests` but missing from this audit: Crimson Signet Treant conquest repeat; Idol Valley unit-play boon; Meteor Spring first-unit move-other; Ghost Bay returned-unit call-rune; Hidden Valley, Star Peak, Confetti Tree held triggers; Navori Arena, Unity Sanctum, Hallowed Tomb, Grand Plaza, Energy Hub, Reckoner Arena held triggers; and Fortified Position defend-grant-Steadfast. It changes documentation only, not runtime behavior or test code, and does not close complete battlefield FU breadth, complete official deck archetype breadth, or READY.

This prevent move-to-base increment additionally proves a legal official Vex deck opening can carry a BehaviorSpec-driven battlefield static restriction into server-authored `MOVE_UNIT` prompt filtering, reject a direct battlefield-to-base `MOVE_UNIT` without mutation, journal and replay that rejected command, then continue through score victory and final-state replay. It still does not close complete same-turn movement policy, complete movement / control-zone edge cases, complete battlefield lifecycle breadth, complete battlefield FUs, complete official deck archetype breadth, or READY.

This prevent unit-play increment additionally proves a legal official Vex deck opening can carry a BehaviorSpec-driven battlefield static restriction into server-authored `PLAY_CARD` destination filtering, reject a direct unit play to that battlefield before payment / stack mutation, journal and replay that rejected command, then continue through score victory and final-state replay. It still does not close complete play destination policy, complete timing-window breadth, complete battlefield lifecycle breadth, complete battlefield FUs, complete official deck archetype breadth, or READY.

This extra-standby rejection increment additionally proves a legal official Poppy deck opening can carry the BehaviorSpec-driven `BATTLEFIELD_EXTRA_STANDBY_DESTINATION` guard into server-authored `HIDE_CARD` destination filtering, reject a direct hand-written battlefield standby destination when the player does not control Bandle Tree, journal and replay that rejected command, then continue through score victory and final-state replay. It still does not close battlefield standby reveal / cleanup breadth, all standby replacement costs, complete hidden-information matrix, complete battlefield FUs, complete official deck archetype breadth, or READY.

This Poro Forge rejection increment additionally proves a legal official Rumble deck opening can carry the BehaviorSpec-driven `BATTLEFIELD_GRANT_LEGEND_EXHAUST_ATTACH_ARMAMENT` guard into server-authored `LEGEND_ACT` prompt filtering, reject a direct battlefield-granted legend action when the player does not control Poro Forge, journal and replay that rejected command, then continue through score victory and final-state replay. It still does not close full activated ability modeling for granted abilities, complete armament attachment lifecycle breadth, complete battlefield FUs, complete official deck archetype breadth, or READY.

Current §6 helper count after this slice: `bool Is*CardNo(` helper definitions are 0 across `src/Riftbound.Engine`, `src/Riftbound.Contracts`, `src/Riftbound.CardCatalog` and `tests/Riftbound.ConformanceTests`; the broader residual `IsSourceCardNoForAbility` occurrence is the P4 activated ability catalog source mapping and call sites, not a newly introduced card-specific engine branch. Coverage-matrix unsupported functional-unit count was not changed by this B0/B4 slice.

Open follow-up:

- evidence whether same-turn effects that ready or add units after a no-legal battle skip should reopen that battlefield battle task before turn end.
- broaden standby-heavy coverage beyond the Teemo stack-reaction representative into targeted standby reactions, battlefield standby reveal / cleanup branches, and non-ready-base cleanup branches.
- broaden B0 beyond representative damage assignment / response activation into more target ordering, replacement / duration cleanup, and card-effect families.

## Validation

Latest Molten Drake other-friendly active-entry official-deck replay focused validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~OfficialDeckMidgameResolvesMoltenDrakeOtherFriendlyActiveEntry"
```

Result:

```text
Passed: 1, Failed: 0, Skipped: 0, Total: 1
```

Latest Molten Drake other-friendly active-entry replay / active-entry / hidden-info adjacent validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~OfficialDeckMidgameResolvesMoltenDrakeOtherFriendlyActiveEntry|FullyQualifiedName~MoltenDrakeOtherFriendlyActiveEntry|FullyQualifiedName~LegionRearguardHasteReadyEntry|FullyQualifiedName~OfficialDeckMidgameResolvesDunehornBeastLowHandActiveEntry|FullyQualifiedName~DunehornLowHandActiveEntryStaticAbility|FullyQualifiedName~MasterYiLevelActiveEntryStaticAbility|FullyQualifiedName~RenataTokenActiveEntryStaticAbility|FullyQualifiedName~FullGameEndToEndTests|FullyQualifiedName~MatchRecoveryTests"
```

Result:

```text
Passed: 2103, Failed: 0, Skipped: 0, Total: 2103
```

Latest Molten Drake other-friendly active-entry backend full validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore
```

Result:

```text
Passed: 8883, Failed: 0, Skipped: 0, Total: 8883
```

Latest Dunehorn Beast low-hand active-entry official-deck replay focused validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~OfficialDeckMidgameResolvesDunehornBeastLowHandActiveEntry"
```

Result:

```text
Passed: 1, Failed: 0, Skipped: 0, Total: 1
```

Latest Dunehorn Beast low-hand active-entry replay / active-entry / hidden-info adjacent validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~OfficialDeckMidgameResolvesDunehornBeastLowHandActiveEntry|FullyQualifiedName~OfficialDeckMidgameResolvesDunehornBeastUnitHeldDraw|FullyQualifiedName~DunehornLowHandActiveEntryStaticAbility|FullyQualifiedName~MasterYiLevelActiveEntryStaticAbility|FullyQualifiedName~MoltenDrakeOtherFriendlyActiveEntry|FullyQualifiedName~RenataTokenActiveEntryStaticAbility|FullyQualifiedName~FullGameEndToEndTests|FullyQualifiedName~MatchRecoveryTests"
```

Result:

```text
Passed: 2100, Failed: 0, Skipped: 0, Total: 2100
```

Latest Dunehorn Beast low-hand active-entry backend full validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore
```

Result:

```text
Passed: 8882, Failed: 0, Skipped: 0, Total: 8882
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

Latest extra-standby official-deck rejected-command replay focused validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~StandbyOfficialDecksRejectBattlefieldExtraStandbyWithoutBandleTree"
```

Result:

```text
Passed: 1, Failed: 0, Skipped: 0, Total: 1
```

Latest extra-standby / standby / hidden-info adjacent validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~BandleTree|FullyQualifiedName~BattlefieldExtraStandby|FullyQualifiedName~HideCard|FullyQualifiedName~Standby|FullyQualifiedName~FullGameEndToEnd|FullyQualifiedName~MatchRecovery|FullyQualifiedName~CardCatalogBaseline"
```

Result:

```text
Passed: 2506, Failed: 0, Skipped: 0, Total: 2506
```

Latest extra-standby backend full validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore
```

Result:

```text
Passed: 8864, Failed: 0, Skipped: 0, Total: 8864
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

Latest B0 evidence-alignment focused validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~FullGameEndToEndTests" --nologo
```

Result:

```text
Passed: 82, Failed: 0, Skipped: 0, Total: 82
```

Latest B0 evidence-alignment backend full validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result:

```text
Passed: 8846, Failed: 0, Skipped: 0, Total: 8846
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

Focused battlefield isolated-defender keyword-modifier validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~BattlefieldIsolatedDefenderKeywordModifier" --nologo
```

Result:

```text
Passed: 3, Failed: 0, Skipped: 0, Total: 3
```

FullGameEndToEnd validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo --filter "FullyQualifiedName~FullGameEndToEndTests"
```

Result:

```text
Passed: 39, Failed: 0, Skipped: 0, Total: 39
```

Adjacent / hidden-info validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~BattlefieldIsolatedDefenderKeywordModifier|FullyQualifiedName~BattlefieldIsolated|FullyQualifiedName~ForbiddenWasteland|FullyQualifiedName~StaticAura|FullyQualifiedName~StaticKeyword|FullyQualifiedName~Steadfast|FullyQualifiedName~ContinuousEffect|FullyQualifiedName~FullGameEndToEndTests|FullyQualifiedName~MatchRecovery" --nologo
```

Result:

```text
Passed: 2111, Failed: 0, Skipped: 0, Total: 2111
```

Backend full validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result:

```text
Passed: 8733, Failed: 0, Skipped: 0, Total: 8733
```

Latest Rumble legend friendly-mechanical Steadfast focused validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~RumbleLegendFriendlyMechanicalSteadfast" --nologo
```

Result:

```text
Passed: 1, Failed: 0, Skipped: 0, Total: 1
```

Latest FullGameEndToEnd validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~FullGameEndToEndTests" --nologo
```

Result:

```text
Passed: 40, Failed: 0, Skipped: 0, Total: 40
```

Latest adjacent / hidden-info validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~RumbleLegendFriendlyMechanicalSteadfast|FullyQualifiedName~FriendlyFiltered|FullyQualifiedName~StaticKeyword|FullyQualifiedName~StaticAura|FullyQualifiedName~Steadfast|FullyQualifiedName~Rumble|FullyQualifiedName~FullGameEndToEndTests|FullyQualifiedName~MatchRecovery" --nologo
```

Result:

```text
Passed: 2112, Failed: 0, Skipped: 0, Total: 2112
```

Latest backend full validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result:

```text
Passed: 8734, Failed: 0, Skipped: 0, Total: 8734
```

Latest Treasure Pile trigger-payment focused validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo --filter "FullyQualifiedName~OfficialDeckMidgamePaysTreasurePileConquerGold"
```

Result:

```text
Passed: 1, Failed: 0, Skipped: 0, Total: 1
```

Latest Treasure Pile FullGameEndToEnd validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~FullGameEndToEndTests" --nologo
```

Result:

```text
Passed: 42, Failed: 0, Skipped: 0, Total: 42
```

Latest Treasure Pile adjacent / hidden-info validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~TreasurePile|FullyQualifiedName~BattlefieldConquerGold|FullyQualifiedName~TriggerPayment|FullyQualifiedName~FullGameEndToEndTests|FullyQualifiedName~MatchRecovery" --nologo
```

Result:

```text
Passed: 2124, Failed: 0, Skipped: 0, Total: 2124
```

Latest Treasure Pile backend full validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result:

```text
Passed: 8736, Failed: 0, Skipped: 0, Total: 8736
```

Latest Sunken Temple powerful-unit trigger-payment focused validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo --filter "FullyQualifiedName~OfficialDeckMidgamePaysSunkenTemplePowerfulDraw"
```

Result:

```text
Passed: 1, Failed: 0, Skipped: 0, Total: 1
```

Latest Sunken Temple FullGameEndToEnd validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~FullGameEndToEndTests" --nologo
```

Result:

```text
Passed: 43, Failed: 0, Skipped: 0, Total: 43
```

Latest Sunken Temple adjacent / hidden-info validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~SunkenTemple|FullyQualifiedName~PowerfulDraw|FullyQualifiedName~BattlefieldConquerPowerful|FullyQualifiedName~TriggerPayment|FullyQualifiedName~FullGameEndToEndTests|FullyQualifiedName~MatchRecovery" --nologo
```

Result:

```text
Passed: 2129, Failed: 0, Skipped: 0, Total: 2129
```

Latest Sunken Temple backend full validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result:

```text
Passed: 8737, Failed: 0, Skipped: 0, Total: 8737
```

Latest Treasure Pile / Sunken Temple trigger-payment decline focused validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~OfficialDeckMidgameDeclinesTreasurePileConquerGoldAndScoreVictoryActionLogReplaysToFinalStateHash|FullyQualifiedName~OfficialDeckMidgameDeclinesSunkenTemplePowerfulDrawAndScoreVictoryActionLogReplaysToFinalStateHash" --nologo
```

Result:

```text
Passed: 2, Failed: 0, Skipped: 0, Total: 2
```

Latest Treasure Pile / Sunken Temple decline FullGameEndToEnd validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~FullGameEndToEndTests" --nologo
```

Result:

```text
Passed: 70, Failed: 0, Skipped: 0, Total: 70
```

Latest Treasure Pile / Sunken Temple decline adjacent / hidden-info validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~TreasurePile|FullyQualifiedName~SunkenTemple|FullyQualifiedName~BattlefieldConquerGold|FullyQualifiedName~BattlefieldConquerPowerful|FullyQualifiedName~TriggerPayment|FullyQualifiedName~FullGameEndToEndTests|FullyQualifiedName~MatchRecovery" --nologo
```

Result:

```text
Passed: 2156, Failed: 0, Skipped: 0, Total: 2156
```

Latest Treasure Pile / Sunken Temple decline backend full validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result:

```text
Passed: 8826, Failed: 0, Skipped: 0, Total: 8826
```

Latest Imperial Shrine / Hall of Legends trigger-payment focused validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo --filter "FullyQualifiedName~ImperialShrineSandSoldier|FullyQualifiedName~HallOfLegendsReadyLegend"
```

Result:

```text
Passed: 4, Failed: 0, Skipped: 0, Total: 4
```

Latest Imperial Shrine / Hall of Legends FullGameEndToEnd validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo --filter "FullyQualifiedName~FullGameEndToEndTests"
```

Result:

```text
Passed: 72, Failed: 0, Skipped: 0, Total: 72
```

Latest Imperial Shrine / Hall of Legends adjacent / hidden-info validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo --filter "FullyQualifiedName~ImperialShrine|FullyQualifiedName~SandSoldier|FullyQualifiedName~PayReturnUnit|FullyQualifiedName~ReturnUnitCreate|FullyQualifiedName~HallOfLegends|FullyQualifiedName~ReadyLegend|FullyQualifiedName~LegendReadied|FullyQualifiedName~TriggerPayment|FullyQualifiedName~BattlefieldConquer|FullyQualifiedName~FullGameEndToEndTests|FullyQualifiedName~MatchRecovery"
```

Result:

```text
Passed: 2222, Failed: 0, Skipped: 0, Total: 2222
```

Latest Imperial Shrine / Hall of Legends backend full validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result:

```text
Passed: 8828, Failed: 0, Skipped: 0, Total: 8828
```

Latest Ravenbloom defend reveal-spell focused validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo --filter "FullyQualifiedName~OfficialDeckMidgameResolvesRavenbloomDefendRevealSpell"
```

Result:

```text
Passed: 1, Failed: 0, Skipped: 0, Total: 1
```

Latest Ravenbloom FullGameEndToEnd validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~FullGameEndToEndTests" --nologo
```

Result:

```text
Passed: 46, Failed: 0, Skipped: 0, Total: 46
```

Latest Ravenbloom adjacent / hidden-info validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~Ravenbloom|FullyQualifiedName~DefendReveal|FullyQualifiedName~RevealSpell|FullyQualifiedName~BattlefieldDefend|FullyQualifiedName~BattlefieldTriggerSpec|FullyQualifiedName~FullGameEndToEndTests|FullyQualifiedName~MatchRecovery" --nologo
```

Result:

```text
Passed: 2056, Failed: 0, Skipped: 0, Total: 2056
```

Latest Ravenbloom backend full validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result:

```text
Passed: 8740, Failed: 0, Skipped: 0, Total: 8740
```

Latest Ravenbloom defend reveal non-spell recycle focused validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo --filter "FullyQualifiedName~OfficialDeckMidgameResolvesRavenbloomDefendRevealNonSpellRecycle"
```

Result:

```text
Passed: 1, Failed: 0, Skipped: 0, Total: 1
```

Latest Ravenbloom non-spell FullGameEndToEnd validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~FullGameEndToEndTests" --nologo
```

Result:

```text
Passed: 47, Failed: 0, Skipped: 0, Total: 47
```

Latest Ravenbloom non-spell adjacent / hidden-info validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~Ravenbloom|FullyQualifiedName~DefendReveal|FullyQualifiedName~RevealSpell|FullyQualifiedName~Recycle|FullyQualifiedName~BattlefieldDefend|FullyQualifiedName~BattlefieldTriggerSpec|FullyQualifiedName~FullGameEndToEndTests|FullyQualifiedName~MatchRecovery" --nologo
```

Result:

```text
Passed: 2181, Failed: 0, Skipped: 0, Total: 2181
```

Latest Ravenbloom non-spell backend full validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result:

```text
Passed: 8741, Failed: 0, Skipped: 0, Total: 8741
```

Latest Hunting Grounds overkill create-Warhawk focused validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo --filter "FullyQualifiedName~OfficialDeckMidgameResolvesHuntingGroundsOverkillWarhawk"
```

Result:

```text
Passed: 1, Failed: 0, Skipped: 0, Total: 1
```

Latest Hunting Grounds FullGameEndToEnd validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~FullGameEndToEndTests" --nologo
```

Result:

```text
Passed: 48, Failed: 0, Skipped: 0, Total: 48
```

Latest Hunting Grounds adjacent / hidden-info validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~Hunting|FullyQualifiedName~Overkill|FullyQualifiedName~Warhawk|FullyQualifiedName~BattlefieldConquer|FullyQualifiedName~BattlefieldTriggerSpec|FullyQualifiedName~FullGameEndToEndTests|FullyQualifiedName~MatchRecovery" --nologo
```

Result:

```text
Passed: 2142, Failed: 0, Skipped: 0, Total: 2142
```

Latest Hunting Grounds backend full validation passed:

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

Latest Plunder Alley adjacent / hidden-info validation passed:

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

Latest Moonveil Altar conquer ready-equipment B0 score-victory replay focused validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo --filter "FullyQualifiedName~OfficialDeckMidgameResolvesMoonveilAltarConquerReadyEquipment"
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
Passed: 87, Failed: 0, Skipped: 0, Total: 87
```

Latest Moonveil Altar adjacent / hidden-info validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo --filter "FullyQualifiedName~ReadyEquipment|FullyQualifiedName~BattlefieldConquer|FullyQualifiedName~BattlefieldTriggerSpec|FullyQualifiedName~FullGameEndToEndTests|FullyQualifiedName~MatchRecovery"
```

Result:

```text
Passed: 2157, Failed: 0, Skipped: 0, Total: 2157
```

Latest backend full validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result:

```text
Passed: 8851, Failed: 0, Skipped: 0, Total: 8851
```

Latest Shirana Monastery conquer consume-boon draw B0 score-victory replay focused validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo --filter "FullyQualifiedName~OfficialDeckMidgameResolvesShiranaMonasteryConquerConsumeBoonDraw"
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
Passed: 88, Failed: 0, Skipped: 0, Total: 88
```

Latest Shirana Monastery adjacent / hidden-info validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo --filter "FullyQualifiedName~ConsumeBoon|FullyQualifiedName~Boon|FullyQualifiedName~BattlefieldConquer|FullyQualifiedName~BattlefieldTriggerSpec|FullyQualifiedName~FullGameEndToEndTests|FullyQualifiedName~MatchRecovery"
```

Result:

```text
Passed: 2255, Failed: 0, Skipped: 0, Total: 2255
```

Latest backend full validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result:

```text
Passed: 8852, Failed: 0, Skipped: 0, Total: 8852
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

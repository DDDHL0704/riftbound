# Plan B / B0 Full-Game E2E Evidence

Date: 2026-06-26

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

The same-battlefield static-keyword action-log replay regression proves a legal official deck can carry a data-driven `RULE_TEXT` keyword aura through the B0 full-game route. `OfficialDeckMidgameAppliesSameBattlefieldStaticKeywordAndScoreVictoryActionLogReplaysToFinalStateHash` records a legal official Jhin deck containing `OGN·015/298` Farron Captain and `UNL-004/219` Ascended Believer, follows the normal official opening seed path, stages both friendly units and an opposing defender through server-authored `PLAY_CARD` / `MOVE_UNIT` prompts, verifies Farron's `BehaviorSpec.StaticAuras` / `SAME_BATTLEFIELD_OTHER_FRIENDLY_UNITS_KEYWORD` projection targets the Believer with `强攻`, declares battle with the granted attacker, observes `DAMAGE_APPLIED` with `basePower=1`, `keyword=强攻`, `keywordBonus=1`, no `staticPowerBonus`, `combatPower=2`, and `damage=2`, then continues through score-victory replay to the same final state hash. This slice changes only test / evidence coverage; it does not close complete RULE_TEXT keyword aura breadth, complete official deck archetype breadth, or READY.

The same-battlefield Steadfast static-keyword action-log replay regression proves the defensive side of the same data-driven `RULE_TEXT` aura path in the B0 full-game route. `OfficialDeckMidgameAppliesSameBattlefieldSteadfastStaticKeywordAndScoreVictoryActionLogReplaysToFinalStateHash` records a legal official Lillia deck containing `OGN·074/298` Taric and `UNL-090/219` LeBlanc, stages both units to an opposing battlefield through server-authored `PLAY_CARD` / `MOVE_UNIT` prompts, verifies Taric projects `SAME_BATTLEFIELD_OTHER_FRIENDLY_UNITS_KEYWORD` to LeBlanc with `坚守`, lets the battlefield owner declare battle, observes defender `DAMAGE_APPLIED` with `basePower=4`, `keyword=坚守`, `keywordBonus=1`, no `staticPowerBonus`, `combatPower=5`, and `damage=5`, then continues through a follow-up server-authored battle declaration, a post-battle no-legal `BATTLE_SKIPPED`, and score-victory replay to the same final state hash. This slice also fixes the shared post-battle task advancement path so active `START_BATTLE` tasks after battle cleanup either expose the task player as active or use the existing no-legal battle skip path instead of leaving a WAIT-only blocker.

The Taric Bulwark damage-assignment replay proves the same official Lillia route also covers Taric's printed `壁垒` ordering in the server-authored `ASSIGN_COMBAT_DAMAGE` window. `OfficialDeckMidgameOrdersTaricBulwarkBeforeBackRowInDamageAssignmentAndScoreVictoryActionLogReplaysToFinalStateHash` records legal official Lillia decks containing `OGN·074/298` Taric, `UNL-090/219` LeBlanc, and `UNL-057/219` Wildclaw Beastmaster. The driver intentionally submits defenders as LeBlanc then Taric, while the prompt and runtime reorder legal targets to Taric first (`BULWARK_FIRST`) and LeBlanc last (`BACK_ROW_LAST`). The runtime fix makes assignment damage pools and lethal thresholds use battle effective power, so Taric and LeBlanc both expose lethal threshold 5 while defending with printed/granted `坚守`; Wildclaw's 7 damage assigns 5 to Taric then 2 to LeBlanc, and the score-victory action log replays to the same final state hash.

The standby reaction action-log replay regression proves a real priority-window standby reaction is recoverable from seated-room command logs. `OfficialDecksResolveStandbyReactionDuringShadowResponseActionLogReplaysToFinalStateHash` records legal official Vex decks containing `UNL-194/219` Shadow and `OGN·197/298` Teemo. The driver hides Teemo through prompt-derived `HIDE_CARD`, opens Shadow's battle-response stack, passes priority to the hidden-card controller, reveals Teemo through prompt-derived `REVEAL_CARD` with `Mode=STANDBY_REACTION` and `Destination=STACK`, resolves Teemo's on-play self-power modifier, then resolves Shadow and closes battle. This slice changes only test / evidence coverage; it does not add runtime rule behavior.

The distinct-deck regression proves the full-game score-victory path is not limited to two copies of the same deck. `DistinctOfficialLowCurveDecksReachScoreVictoryAfterRealBattleThroughServerPrompts` submits legal official Jhin and Rumble low-curve decks with different legend and champion cards, then drives the same server prompt path to real battle close and score-based `MATCH_WON`. This slice changes only the test driver / evidence: it parameterizes the deck pair and skips `待命` units when selecting the representative unit to play or move, so the B0 probe remains focused on battle / score instead of standby cleanup.

The standby-heavy regression proves that the same full-game score-victory route remains stable when one official low-curve deck includes standby-capable cards. `StandbyHeavyOfficialLowCurveDecksReachScoreVictoryAfterRealBattleThroughServerPrompts` submits distinct legal Jhin and Poppy low-curve decks, keeps representative unit selection on non-standby battle-path units, then drives setup, real battle close and score-based `MATCH_WON` through server-authored prompts. This slice changes only test / evidence coverage; it does not add runtime rule behavior or close full standby reveal / reaction mechanics.

The damage-assignment regression proves that a legal official deck pair can reach the server-authored multi-defender battle damage assignment window without a handcrafted battle fixture. `OfficialDecksResolveMultiDefenderBattleDamageAssignmentThroughServerPrompts` submits legal Lillia green/blue decks containing official `UNL-036/219` Mutant Kitten (`壁垒`) and `UNL-090/219` LeBlanc (`后排`), stages two invading units on one battlefield through server prompts, opens `BATTLE_DAMAGE_ASSIGNMENT_OPENED`, submits `ASSIGN_COMBAT_DAMAGE` for both players, and observes `DAMAGE_APPLIED` plus `BATTLE_CLOSED`. This slice changes only test / evidence coverage; it does not close complete combat damage assignment breadth.

The response-activation regression proves that a legal official deck pair can reach and use a server-authored battle response activation window without a handcrafted battle fixture. `OfficialDecksResolveShadowBattleResponseActivationThroughServerPrompts` submits legal Vex green/purple decks with `UNL-232/219` Vex legend, `UNL-055/219` Vex champion, and official `UNL-194/219` Shadow. The driver plays Shadow directly to a contested battlefield, opens `BATTLE_RESPONSE_PRIORITY_OPENED`, submits prompt-authored `ACTIVATE_ABILITY` with a quoted payment resource action when needed, resolves Shadow's stack item to apply `STUNNED`, returns to battle response priority, and closes battle with `BATTLE_RESPONSE_PRIORITY_CLOSED` plus `BATTLE_CLOSED`. This slice changes only test / evidence coverage; it does not close all response windows or the broader swift / reaction family.

## Hidden Information Evidence

`FullGameEndToEndTests.AssertNoHiddenZoneLeak` serializes each viewer snapshot after accepted full-game steps and rejects exposure of opponent hand, main-deck and rune-deck object ids. The current guard is centralized through `FullGameEndToEndTests.AssertAccepted`, so every accepted result routed through the shared B0 helper performs this hidden-zone snapshot check immediately. The battlefield extra-standby regression also asserts the `CARD_HIDDEN` payload for the Bandle Tree destination omits `cardNo` while preserving the public battlefield destination. This is a focused hidden-zone guard for the full-game probe and does not replace the broader `MatchRecovery` spectator validation suite.

## Validation

Focused validation:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo --filter "FullyQualifiedName~OfficialDeckMidgameAppliesOtherFriendlyStaticAura"
```

Result:

```text
Passed: 1, Failed: 0, Skipped: 0, Total: 1
```

Full-game validation:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo --filter "FullyQualifiedName~FullGameEndToEndTests"
```

Result:

```text
Passed: 25, Failed: 0, Skipped: 0, Total: 25
```

Adjacent validation:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo --filter "FullyQualifiedName~OtherFriendly|FullyQualifiedName~StaticAura|FullyQualifiedName~StaticPower|FullyQualifiedName~Baron|FullyQualifiedName~FullGameEndToEndTests|FullyQualifiedName~MatchRecovery"
```

Result:

```text
Passed: 2114, Failed: 0, Skipped: 0, Total: 2114
```

Backend full validation:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj
```

Result:

```text
Passed: 8717, Failed: 0, Skipped: 0, Total: 8717
```

## Non-Closure

This evidence proves the engine can drive mirrored Jhin low-curve decks, a distinct Jhin-vs-Rumble official low-curve deck pair, and a standby-heavy Jhin-vs-Poppy official deck pair through setup, opening, live prompt-driven gameplay, contested battlefield task creation, no-legal battle skip, later turn-start battlefield reopen, real battle declaration, battle close and score-based match result without leaking hidden zones. It also proves official Lillia multi-defender damage-assignment paths can open and resolve `ASSIGN_COMBAT_DAMAGE` through server prompts, including Taric `壁垒` before LeBlanc `后排` ordering with printed/granted `坚守` battle effective power; official Vex / Shadow battle response paths can open and resolve `ACTIVATE_ABILITY` through server prompts; an official Poppy / Pakaa Cub standby path can hide and reveal a standby card through server prompts without exposing the hidden card number in the hide event; an official Poppy / Bandle Tree path can hide a standby card to a battlefield extra-standby destination and still finish through score-victory replay; official Poppy / Garen / Demacia Envoy and Vex / Baron Nashor / Wildclaw Beastmaster paths can apply data-driven same-battlefield and non-local other-friendly static auras to real battle damage and still finish through score-victory replay; official Jhin / Farron Captain / Ascended Believer and Lillia / Taric / LeBlanc paths can apply data-driven same-battlefield RULE_TEXT keyword auras to attacker and defender real battle damage and still finish through score-victory replay; and an official Vex / Shadow / Teemo path can reveal a hidden standby card as a stack reaction during response priority. The mirrored Jhin, distinct Jhin-vs-Rumble, standby-heavy Jhin-vs-Poppy, Lillia damage-assignment, Taric Bulwark assignment, Vex / Shadow response-activation, Pakaa Cub standby hide/reveal, Bandle Tree battlefield extra-standby hide, Garen same-battlefield static-aura, Baron Nashor other-friendly static-aura, Farron same-battlefield static-keyword aura, Taric same-battlefield Steadfast static-keyword aura, and Teemo standby reaction command streams can now be recovered from seated-room `SUBMIT_DECK` through their final representative battle / score state to the same final state hash. It does not close all official deck archetypes, all standby reaction card effects / targeted standby reactions, battlefield extra-standby reveal / cleanup breadth, non-ready-base standby cleanup breadth, complete combat damage assignment breadth, complete static-aura official breadth, complete RULE_TEXT keyword aura breadth, complete spell-duel / battle lifecycle breadth, all response windows, full card matrix readiness, frontend gates or final READY.

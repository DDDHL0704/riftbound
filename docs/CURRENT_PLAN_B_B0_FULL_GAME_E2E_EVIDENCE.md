# Plan B / B0 Full-Game E2E Evidence

Date: 2026-06-25

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
- score-based `END_TURN` advancement to `MATCH_WON`
- `SURRENDER`

The new engine regression proves the spell-duel close handoff chooses the battlefield task player when the turn player is the mover. This is the natural path that previous fixture-style tests did not cover.

The no-legal battle regression proves the next natural blocker is consumed by shared engine state rather than single-card logic. After spell duel closes, `CoreRuleEngine` checks the existing server-authored `DECLARE_BATTLE` requirements for the `START_BATTLE` task player. When no ready face-up attacker / defender declaration exists, the engine emits `BATTLE_SKIPPED`, records `BATTLEFIELD_BATTLE_SKIPPED:*` until end of turn, clears the blocking battlefield task family from state / snapshot projection, and returns to neutral open main timing.

The turn-start battle reopen regression proves still-contested battlefields do not remain idle after a no-legal battle skip expires. `ResolveTurnStart` now advances pending battlefield tasks after turn-start ready / draw / score state is built. The B0 probe drives multiple natural turns, observes repeated no-legal skips until the moved combatant naturally readies, then submits the first server-authored `DECLARE_BATTLE` candidate and observes `BATTLE_DECLARED` plus `BATTLE_CLOSED` from a legal official deck path.

The score-victory regression proves the same legal official-deck path can continue after real `BATTLE_CLOSED` through server-authored `END_TURN` prompts until battlefield scoring emits `SCORE_GAINED` and a single score-based `MATCH_WON`. The runtime fix restores ordinary open-main action to `TurnPlayerId` after a non-turn-player battle task closes with no further battlefield task, and prevents duplicate `MATCH_WON` during turn start when pre-rune-call scoring already won before the synthetic draw result is built.

The action-log replay regressions prove the score-victory command stream can be journaled and replayed to the same final state hash. `OfficialLowCurvePostBattleScoreVictoryActionLogReplaysToFinalStateHash` starts from the existing mirrored Jhin official low-curve battle-closed state and covers post-battle `END_TURN` scoring. `OfficialLowCurveFullGameScoreVictoryActionLogReplaysToFinalStateHash`, `DistinctOfficialLowCurveFullGameScoreVictoryActionLogReplaysToFinalStateHash`, and `StandbyHeavyOfficialLowCurveFullGameScoreVictoryActionLogReplaysToFinalStateHash` start from a seated official low-curve initial state, record `SUBMIT_DECK`, `READY`, `MULLIGAN`, tap/play/move/focus, reopened battle declaration, and score-victory `END_TURN` commands through `MatchJournal`, convert entries to recovered commands / events, and verify `MatchActionLogReplayer.VerifyFinalStateAsync` reaches the expected final state hash with no replay errors. The B0 test driver writes replayable raw command payloads for prompt-derived object ids and destinations instead of only storing `cmdType`.

The battle-prompt action-log replay regressions prove the two complex B0 prompt representatives are also recoverable from seated-room command logs. `OfficialDecksResolveMultiDefenderBattleDamageAssignmentActionLogReplaysToFinalStateHash` records a legal official Lillia multi-defender path through prompt-derived `DECLARE_BATTLE` and both players' `ASSIGN_COMBAT_DAMAGE` submissions, then replays to the same battle-closed final state hash. `OfficialDecksResolveShadowBattleResponseActivationActionLogReplaysToFinalStateHash` records a legal official Vex / Shadow path through prompt-derived `DECLARE_BATTLE`, `ACTIVATE_ABILITY`, stack resolution and response priority close, then replays to the same battle-closed final state hash. The damage raw payload now uses the protocol lower-camel `assignments[].sourceObjectId`, `assignments[].targetObjectId` and `assignments[].damage` fields used by recovery.

The standby hide/reveal action-log replay regression proves explicit standby setup is also recoverable from seated-room command logs. `StandbyOfficialDecksHideRevealAndScoreVictoryActionLogReplaysToFinalStateHash` records a legal official Poppy deck path that hides official `OGN·135/298` Pakaa Cub through prompt-derived `HIDE_CARD` with `STANDBY_A`, confirms `CARD_HIDDEN` does not expose `cardNo`, reveals the same base object through prompt-derived `REVEAL_CARD` with `STANDBY_REVEAL_0`, then continues through the existing non-standby battle / score-victory route. The hide/reveal raw payloads now preserve source object id, card number, destination, mode and optional cost fields used by recovery.

The distinct-deck regression proves the full-game score-victory path is not limited to two copies of the same deck. `DistinctOfficialLowCurveDecksReachScoreVictoryAfterRealBattleThroughServerPrompts` submits legal official Jhin and Rumble low-curve decks with different legend and champion cards, then drives the same server prompt path to real battle close and score-based `MATCH_WON`. This slice changes only the test driver / evidence: it parameterizes the deck pair and skips `待命` units when selecting the representative unit to play or move, so the B0 probe remains focused on battle / score instead of standby cleanup.

The standby-heavy regression proves that the same full-game score-victory route remains stable when one official low-curve deck includes standby-capable cards. `StandbyHeavyOfficialLowCurveDecksReachScoreVictoryAfterRealBattleThroughServerPrompts` submits distinct legal Jhin and Poppy low-curve decks, keeps representative unit selection on non-standby battle-path units, then drives setup, real battle close and score-based `MATCH_WON` through server-authored prompts. This slice changes only test / evidence coverage; it does not add runtime rule behavior or close full standby reveal / reaction mechanics.

The damage-assignment regression proves that a legal official deck pair can reach the server-authored multi-defender battle damage assignment window without a handcrafted battle fixture. `OfficialDecksResolveMultiDefenderBattleDamageAssignmentThroughServerPrompts` submits legal Lillia green/blue decks containing official `UNL-036/219` Mutant Kitten (`壁垒`) and `UNL-090/219` LeBlanc (`后排`), stages two invading units on one battlefield through server prompts, opens `BATTLE_DAMAGE_ASSIGNMENT_OPENED`, submits `ASSIGN_COMBAT_DAMAGE` for both players, and observes `DAMAGE_APPLIED` plus `BATTLE_CLOSED`. This slice changes only test / evidence coverage; it does not close complete combat damage assignment breadth.

The response-activation regression proves that a legal official deck pair can reach and use a server-authored battle response activation window without a handcrafted battle fixture. `OfficialDecksResolveShadowBattleResponseActivationThroughServerPrompts` submits legal Vex green/purple decks with `UNL-232/219` Vex legend, `UNL-055/219` Vex champion, and official `UNL-194/219` Shadow. The driver plays Shadow directly to a contested battlefield, opens `BATTLE_RESPONSE_PRIORITY_OPENED`, submits prompt-authored `ACTIVATE_ABILITY` with a quoted payment resource action when needed, resolves Shadow's stack item to apply `STUNNED`, returns to battle response priority, and closes battle with `BATTLE_RESPONSE_PRIORITY_CLOSED` plus `BATTLE_CLOSED`. This slice changes only test / evidence coverage; it does not close all response windows or the broader swift / reaction family.

## Hidden Information Evidence

`FullGameEndToEndTests.AssertNoHiddenZoneLeak` serializes each viewer snapshot after every accepted step and rejects exposure of opponent hand, main-deck and rune-deck object ids. This is a focused hidden-zone guard for the full-game probe and does not replace the broader `MatchRecovery` spectator validation suite.

## Validation

Focused validation:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~FullGameEndToEndTests"
```

Result:

```text
Passed: 14, Failed: 0, Skipped: 0, Total: 14
```

Adjacent validation:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~FullGameEndToEndTests|FullyQualifiedName~BoardTaskQueueFoundationTests|FullyQualifiedName~SpellDuelBattleStateMachineTests|FullyQualifiedName~BattlefieldContestBattleTaskGuardTests|FullyQualifiedName~DeclareBattle|FullyQualifiedName~GameHubJoinTests|FullyQualifiedName~BattleDamageAssignment|FullyQualifiedName~ShadowActivatedAbility|FullyQualifiedName~RevealCard|FullyQualifiedName~Standby|FullyQualifiedName~HideCard"
```

Result:

```text
Passed: 699, Failed: 0, Skipped: 0, Total: 699
```

Recovery / hidden-info validation:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecovery"
```

Result:

```text
Passed: 1989, Failed: 0, Skipped: 0, Total: 1989
```

Backend full validation:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj
```

Result:

```text
Passed: 8468, Failed: 0, Skipped: 0, Total: 8468
```

## Non-Closure

This evidence proves the engine can drive mirrored Jhin low-curve decks, a distinct Jhin-vs-Rumble official low-curve deck pair, and a standby-heavy Jhin-vs-Poppy official low-curve deck pair through setup, opening, live prompt-driven gameplay, contested battlefield task creation, no-legal battle skip, later turn-start battlefield reopen, real battle declaration, battle close and score-based match result without leaking hidden zones. It also proves an official Lillia multi-defender damage-assignment path can open and resolve `ASSIGN_COMBAT_DAMAGE` through server prompts, an official Vex / Shadow battle response path can open and resolve `ACTIVATE_ABILITY` through server prompts, and an official Poppy / Pakaa Cub standby path can hide and reveal a standby card through server prompts without exposing the hidden card number in the hide event. The mirrored Jhin, distinct Jhin-vs-Rumble, standby-heavy Jhin-vs-Poppy, Lillia damage-assignment, Vex / Shadow response-activation, and Pakaa Cub standby hide/reveal command streams can now be recovered from seated-room `SUBMIT_DECK` through their final representative battle / score state to the same final state hash. It does not close all official deck archetypes, standby reaction-to-stack timing, battlefield extra-standby destinations, non-ready-base standby cleanup breadth, complete combat damage assignment breadth, complete spell-duel / battle lifecycle breadth, all response windows, full card matrix readiness, frontend gates or final READY.

# Plan B / B0 Full-Game E2E Evidence

Date: 2026-06-24

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
- score-based `END_TURN` advancement to `MATCH_WON`
- `SURRENDER`

The new engine regression proves the spell-duel close handoff chooses the battlefield task player when the turn player is the mover. This is the natural path that previous fixture-style tests did not cover.

The no-legal battle regression proves the next natural blocker is consumed by shared engine state rather than single-card logic. After spell duel closes, `CoreRuleEngine` checks the existing server-authored `DECLARE_BATTLE` requirements for the `START_BATTLE` task player. When no ready face-up attacker / defender declaration exists, the engine emits `BATTLE_SKIPPED`, records `BATTLEFIELD_BATTLE_SKIPPED:*` until end of turn, clears the blocking battlefield task family from state / snapshot projection, and returns to neutral open main timing.

The turn-start battle reopen regression proves still-contested battlefields do not remain idle after a no-legal battle skip expires. `ResolveTurnStart` now advances pending battlefield tasks after turn-start ready / draw / score state is built. The B0 probe drives multiple natural turns, observes repeated no-legal skips until the moved combatant naturally readies, then submits the first server-authored `DECLARE_BATTLE` candidate and observes `BATTLE_DECLARED` plus `BATTLE_CLOSED` from a legal official deck path.

The score-victory regression proves the same legal official-deck path can continue after real `BATTLE_CLOSED` through server-authored `END_TURN` prompts until battlefield scoring emits `SCORE_GAINED` and a single score-based `MATCH_WON`. The runtime fix restores ordinary open-main action to `TurnPlayerId` after a non-turn-player battle task closes with no further battlefield task, and prevents duplicate `MATCH_WON` during turn start when pre-rune-call scoring already won before the synthetic draw result is built.

The distinct-deck regression proves the full-game score-victory path is not limited to two copies of the same deck. `DistinctOfficialLowCurveDecksReachScoreVictoryAfterRealBattleThroughServerPrompts` submits legal official Jhin and Rumble low-curve decks with different legend and champion cards, then drives the same server prompt path to real battle close and score-based `MATCH_WON`. This slice changes only the test driver / evidence: it parameterizes the deck pair and skips `待命` units when selecting the representative unit to play or move, so the B0 probe remains focused on battle / score instead of standby cleanup.

The standby-heavy regression proves that the same full-game score-victory route remains stable when one official low-curve deck includes standby-capable cards. `StandbyHeavyOfficialLowCurveDecksReachScoreVictoryAfterRealBattleThroughServerPrompts` submits distinct legal Jhin and Poppy low-curve decks, keeps representative unit selection on non-standby battle-path units, then drives setup, real battle close and score-based `MATCH_WON` through server-authored prompts. This slice changes only test / evidence coverage; it does not add runtime rule behavior or close full standby reveal / reaction mechanics.

## Hidden Information Evidence

`FullGameEndToEndTests.AssertNoHiddenZoneLeak` serializes each viewer snapshot after every accepted step and rejects exposure of opponent hand, main-deck and rune-deck object ids. This is a focused hidden-zone guard for the full-game probe and does not replace the broader `MatchRecovery` spectator validation suite.

## Validation

Focused validation:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~FullGameEndToEndTests"
```

Result:

```text
Passed: 5, Failed: 0, Skipped: 0, Total: 5
```

Adjacent validation:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~FullGameEndToEndTests|FullyQualifiedName~BoardTaskQueueFoundationTests|FullyQualifiedName~SpellDuelBattleStateMachineTests|FullyQualifiedName~BattlefieldContestBattleTaskGuardTests|FullyQualifiedName~DeclareBattle|FullyQualifiedName~GameHubJoinTests"
```

Result:

```text
Passed: 370, Failed: 0, Skipped: 0, Total: 370
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
Passed: 8355, Failed: 0, Skipped: 0, Total: 8355
```

## Non-Closure

This evidence proves the engine can drive mirrored Jhin low-curve decks, a distinct Jhin-vs-Rumble official low-curve deck pair, and a standby-heavy Jhin-vs-Poppy official low-curve deck pair through setup, opening, live prompt-driven gameplay, contested battlefield task creation, no-legal battle skip, later turn-start battlefield reopen, real battle declaration, battle close and score-based match result without leaking hidden zones. It does not close all official deck archetypes, full standby reveal / reaction mechanics, complete combat damage assignment breadth, complete spell-duel / battle lifecycle breadth, full card matrix readiness, frontend gates or final READY.

# Plan B / B0 Full-Game E2E Audit

Date: 2026-06-24

Status: focused B0 server E2E score-victory slice accepted; project remains **NOT READY**.

## Scope

This slice adds a server-authoritative full-game probe that starts from legal official deck submission rather than a hand-built combat fixture. The probe covers:

- both players submit an official deck through `SubmitDeckAsync`;
- both players ready and consume mulligan prompts;
- the server advances into main phase;
- both players use server prompts to tap runes and play units;
- a live unit moves from base to the opponent battlefield;
- the resulting contested battlefield opens and closes spell duel focus through `PASS_FOCUS`;
- if the resulting `START_BATTLE` task has no legal ready attackers / defenders, the engine records `BATTLE_SKIPPED` and clears the blocking battlefield task family for that battlefield for the rest of the turn;
- later turn starts reopen the still-contested battlefield task after the end-of-turn skip marker expires;
- once both official-deck combatants have naturally readied across turns, the server exposes `DECLARE_BATTLE` and accepts the first server-authored battle declaration candidate;
- the real official-deck path emits `BATTLE_DECLARED` and `BATTLE_CLOSED`;
- after real battle close, repeated server `END_TURN` prompts drive battlefield scoring until score-based `MATCH_WON`;
- the score-victory result has a single `MATCH_WON` event and winner score satisfies the emitted `winningScore`;
- every accepted step checks player snapshots for hidden opponent hand, main-deck and rune-deck object id leakage;
- the earlier surrender result smoke remains covered separately.

The first B0 runtime fix was narrow: when spell duel closes into an existing `START_BATTLE` task, the engine promotes `ActivePlayerId` to the task player (`CleanupTaskState.PlayerId`) instead of always keeping the turn player. This preserves the existing battlefield-owner declaration model and fixes natural games where the mover is not the battle-task player.

This slice narrows the next exposed B0 blocker: after spell duel closes, the engine now asks the existing server-authored `DECLARE_BATTLE` legality model whether the `START_BATTLE` task player has any legal declaration. If not, it writes a `BATTLEFIELD_BATTLE_SKIPPED:*` end-of-turn marker, emits `BATTLE_SKIPPED` with participant metadata, suppresses repeated pending-task / battlefield-task projection for that battlefield, and returns to the turn player in open main timing.

This turn-start slice narrows the follow-up B0 blocker: after turn-start ready / draw / score effects complete, `ResolveTurnStart` now advances still-contested battlefield tasks through the same shared `AdvancePendingBattlefieldTasksAfterStateChange` path used by movement and battle cleanup. The full-game probe proves no-legal skipped battlefields reopen on later turns and can reach real `DECLARE_BATTLE` / `BATTLE_CLOSED` once both sides' units are ready.

This score-victory slice narrows the next B0 blocker. When a non-turn-player `START_BATTLE` task resolves and no further battlefield task opens, `AdvancePendingBattlefieldTasksAfterStateChange` now restores `ActivePlayerId` to `TurnPlayerId` for ordinary open main timing so the next server-authored `END_TURN` prompt is accepted by the same authority check that produced it. `BuildTurnStartEvents` also avoids adding a second `MATCH_WON` when pre-rune-call battlefield scoring already emitted the win event.

## Evidence

- Added `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs`.
- Strengthened `tests/Riftbound.ConformanceTests/BoardTaskQueueFoundationTests.cs` with `PassFocusClosesSpellDuelAndPromotesBattlefieldOwnerWhenMoverIsTurnPlayer`.
- Strengthened `tests/Riftbound.ConformanceTests/BoardTaskQueueFoundationTests.cs` with `PassFocusClosesSpellDuelAndSkipsStartBattleWhenNoLegalCombatants`.
- Strengthened `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs` with `OfficialLowCurveDecksReopenContestedBattleAfterSkippedCombatantsReadyAcrossTurns`.
- Strengthened `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs` with `OfficialLowCurveDecksReachScoreVictoryAfterRealBattleThroughServerPrompts`.
- Runtime changed in `src/Riftbound.Engine/CoreRuleEngine.cs` only in the spell-duel close handoff path.
- Runtime changed in `src/Riftbound.Engine/CoreRuleEngine.cs` turn start to advance pending battlefield tasks after turn-start ready / draw / score state is built.
- Runtime changed in `src/Riftbound.Engine/CoreRuleEngine.cs` to restore ordinary open main `ActivePlayerId` to `TurnPlayerId` after battlefield-task advancement finds no further contested battlefield.
- Runtime changed in `src/Riftbound.Engine/CoreRuleEngine.cs` to prevent duplicate turn-start `MATCH_WON` events when battlefield scoring wins before draw.
- Runtime projection / queue filtering changed in `src/Riftbound.Engine/MatchSession.cs` to make the skip marker suppress repeated same-turn battlefield tasks and hide the internal marker from public continuous-effect projection.

## Residuals

This is not a READY claim and does not close complete game resolution. The current B0 full-game probe now proves one low-curve official-deck path can submit legal decks, pass opening prompts, create a contested battlefield, consume no-legal battle tasks, reopen them on later turns, declare and close a real battle, and finish by score-based `MATCH_WON` without surrender. It does not prove all real deck archetypes, all battle damage assignment branches, all response windows, or all card-effect families can complete a game.

Current §6 mouth count after this slice: `Is*CardNo` engine whitelist definitions remain 108. Coverage-matrix unsupported functional-unit count was not changed by this B0 state-machine slice.

Open follow-up:

- evidence whether same-turn effects that ready or add units after a no-legal battle skip should reopen that battlefield battle task before turn end.
- broaden B0 beyond the low-curve prompt driver into richer official deck paths that exercise battle damage assignment, response windows, replacement / duration cleanup, and more card-effect families.

## Validation

Focused validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~FullGameEndToEndTests"
```

Result:

```text
Passed: 3, Failed: 0, Skipped: 0, Total: 3
```

Adjacent validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~FullGameEndToEndTests|FullyQualifiedName~BoardTaskQueueFoundationTests|FullyQualifiedName~SpellDuelBattleStateMachineTests|FullyQualifiedName~BattlefieldContestBattleTaskGuardTests|FullyQualifiedName~DeclareBattle|FullyQualifiedName~GameHubJoinTests"
```

Result:

```text
Passed: 368, Failed: 0, Skipped: 0, Total: 368
```

Recovery / hidden-info validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecovery"
```

Result:

```text
Passed: 1989, Failed: 0, Skipped: 0, Total: 1989
```

Backend full validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj
```

Result:

```text
Passed: 8353, Failed: 0, Skipped: 0, Total: 8353
```

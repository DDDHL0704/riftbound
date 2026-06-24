# Plan B / B0 Full-Game E2E Audit

Date: 2026-06-24

Status: focused B0 server E2E no-legal battle skip slice accepted; project remains **NOT READY**.

## Scope

This slice adds a server-authoritative full-game probe that starts from legal official deck submission rather than a hand-built combat fixture. The probe covers:

- both players submit an official deck through `SubmitDeckAsync`;
- both players ready and consume mulligan prompts;
- the server advances into main phase;
- both players use server prompts to tap runes and play units;
- a live unit moves from base to the opponent battlefield;
- the resulting contested battlefield opens and closes spell duel focus through `PASS_FOCUS`;
- if the resulting `START_BATTLE` task has no legal ready attackers / defenders, the engine records `BATTLE_SKIPPED` and clears the blocking battlefield task family for that battlefield for the rest of the turn;
- every accepted step checks player snapshots for hidden opponent hand, main-deck and rune-deck object id leakage;
- the match reaches a server `MATCH_WON` result through `SURRENDER`.

The first B0 runtime fix was narrow: when spell duel closes into an existing `START_BATTLE` task, the engine promotes `ActivePlayerId` to the task player (`CleanupTaskState.PlayerId`) instead of always keeping the turn player. This preserves the existing battlefield-owner declaration model and fixes natural games where the mover is not the battle-task player.

This slice narrows the next exposed B0 blocker: after spell duel closes, the engine now asks the existing server-authored `DECLARE_BATTLE` legality model whether the `START_BATTLE` task player has any legal declaration. If not, it writes a `BATTLEFIELD_BATTLE_SKIPPED:*` end-of-turn marker, emits `BATTLE_SKIPPED` with participant metadata, suppresses repeated pending-task / battlefield-task projection for that battlefield, and returns to the turn player in open main timing.

## Evidence

- Added `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs`.
- Strengthened `tests/Riftbound.ConformanceTests/BoardTaskQueueFoundationTests.cs` with `PassFocusClosesSpellDuelAndPromotesBattlefieldOwnerWhenMoverIsTurnPlayer`.
- Strengthened `tests/Riftbound.ConformanceTests/BoardTaskQueueFoundationTests.cs` with `PassFocusClosesSpellDuelAndSkipsStartBattleWhenNoLegalCombatants`.
- Runtime changed in `src/Riftbound.Engine/CoreRuleEngine.cs` only in the spell-duel close handoff path.
- Runtime projection / queue filtering changed in `src/Riftbound.Engine/MatchSession.cs` to make the skip marker suppress repeated same-turn battlefield tasks and hide the internal marker from public continuous-effect projection.

## Residuals

This is not a READY claim and does not close complete battle declaration. The current natural `MOVE_UNIT` path exhausts the moved unit, while `DECLARE_BATTLE` candidates still require ready face-up attackers and defenders. The B0 full-game probe now proves this no-enabled-declare state is consumed by the server state machine instead of leaving a blocking `START_BATTLE` task.

Open follow-up:

- close real `BATTLE_DECLARED` / `BATTLE_CLOSED` through an official-deck, server-prompt E2E path;
- close scoring victory through server prompts instead of surrender.
- evidence whether same-turn effects that ready or add units after a no-legal battle skip should reopen that battlefield battle task before turn end.

## Validation

Focused validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~FullGameEndToEndTests|FullyQualifiedName~PassFocusClosesSpellDuelAndSkipsStartBattleWhenNoLegalCombatants|FullyQualifiedName~PassFocusClosesSpellDuelAndPromotesBattlefieldOwnerWhenMoverIsTurnPlayer|FullyQualifiedName~PassFocusClosesSpellDuelAndPromotesStartBattleWithParticipantData|FullyQualifiedName~ActiveStartBattleDeclareBattleClearsTaskAndPreservesRepresentativeEvents"
```

Result:

```text
Passed: 5, Failed: 0, Skipped: 0, Total: 5
```

Adjacent validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~FullGameEndToEndTests|FullyQualifiedName~BoardTaskQueueFoundationTests|FullyQualifiedName~SpellDuelBattleStateMachineTests|FullyQualifiedName~BattlefieldContestBattleTaskGuardTests|FullyQualifiedName~DeclareBattle|FullyQualifiedName~GameHubJoinTests"
```

Result:

```text
Passed: 366, Failed: 0, Skipped: 0, Total: 366
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
Passed: 8351, Failed: 0, Skipped: 0, Total: 8351
```

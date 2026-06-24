# Plan B / B0 Full-Game E2E Audit

Date: 2026-06-24

Status: focused B0 server E2E slice accepted; project remains **NOT READY**.

## Scope

This slice adds a server-authoritative full-game probe that starts from legal official deck submission rather than a hand-built combat fixture. The probe covers:

- both players submit an official deck through `SubmitDeckAsync`;
- both players ready and consume mulligan prompts;
- the server advances into main phase;
- both players use server prompts to tap runes and play units;
- a live unit moves from base to the opponent battlefield;
- the resulting contested battlefield opens and closes spell duel focus through `PASS_FOCUS`;
- the same battlefield promotes to `START_BATTLE`;
- every accepted step checks player snapshots for hidden opponent hand, main-deck and rune-deck object id leakage;
- the match reaches a server `MATCH_WON` result through `SURRENDER`.

The runtime fix in this slice is narrow: when spell duel closes into an existing `START_BATTLE` task, the engine promotes `ActivePlayerId` to the task player (`CleanupTaskState.PlayerId`) instead of always keeping the turn player. This preserves the existing battlefield-owner declaration model and fixes natural games where the mover is not the battle-task player.

## Evidence

- Added `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs`.
- Strengthened `tests/Riftbound.ConformanceTests/BoardTaskQueueFoundationTests.cs` with `PassFocusClosesSpellDuelAndPromotesBattlefieldOwnerWhenMoverIsTurnPlayer`.
- Runtime changed in `src/Riftbound.Engine/CoreRuleEngine.cs` only in the spell-duel close handoff path.

## Residuals

This is not a READY claim and does not close complete battle declaration. The current natural `MOVE_UNIT` path exhausts the moved unit, while `DECLARE_BATTLE` candidates still require ready face-up attackers and defenders. The B0 full-game probe therefore asserts the promoted `START_BATTLE` task and documents the no-enabled-declare residual instead of pretending battle resolution is complete.

Open follow-up:

- decide and evidence whether exhausted units at a contested battlefield can participate in the immediate battle, or whether the engine must auto-resolve / skip `START_BATTLE` when no legal combatants exist;
- close real `BATTLE_DECLARED` / `BATTLE_CLOSED` through an official-deck, server-prompt E2E path;
- close scoring victory through server prompts instead of surrender.

## Validation

Focused validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~FullGameEndToEndTests|FullyQualifiedName~PassFocusClosesSpellDuelAndPromotesBattlefieldOwnerWhenMoverIsTurnPlayer"
```

Result:

```text
Passed: 2, Failed: 0, Skipped: 0, Total: 2
```

Adjacent validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~FullGameEndToEndTests|FullyQualifiedName~BoardTaskQueueFoundationTests|FullyQualifiedName~SpellDuelBattleStateMachineTests|FullyQualifiedName~BattlefieldContestBattleTaskGuardTests|FullyQualifiedName~DeclareBattle|FullyQualifiedName~GameHubJoinTests"
```

Result:

```text
Passed: 365, Failed: 0, Skipped: 0, Total: 365
```

Backend full validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj
```

Result:

```text
Passed: 8350, Failed: 0, Skipped: 0, Total: 8350
```

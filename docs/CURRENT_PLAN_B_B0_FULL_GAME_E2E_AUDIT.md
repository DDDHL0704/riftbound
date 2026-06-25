# Plan B / B0 Full-Game E2E Audit

Date: 2026-06-25

Status: focused B0 full-game score-victory action-log replay slice accepted; project remains **NOT READY**.

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
- from an already battle-closed official low-curve state, a `MatchJournal`-recorded post-battle score-victory `END_TURN` command stream replays through `MatchActionLogReplayer` to the same final state hash and recovered event payload hash;
- from a seated official low-curve initial state, the full mirrored Jhin `SUBMIT_DECK` -> `READY` -> `MULLIGAN` -> gameplay -> `DECLARE_BATTLE` -> score-victory command stream replays through `MatchActionLogReplayer` to the same final state hash and recovered event payload hash;
- the score-victory path now runs the original mirrored Jhin deck, a distinct Jhin-vs-Rumble official deck pair, and a standby-heavy Jhin-vs-Poppy official deck pair;
- a legal official Lillia deck pair can drive multi-defender `DECLARE_BATTLE` into `ASSIGN_COMBAT_DAMAGE`, submit both players' assignments, and close battle with `DAMAGE_APPLIED` / `BATTLE_CLOSED`;
- a legal official Vex deck pair can drive `DECLARE_BATTLE` into `BATTLE_RESPONSE_PRIORITY_OPENED`, activate `UNL-194/219` Shadow through `ACTIVATE_ABILITY`, resolve the stack, return to battle response priority, and close battle with `BATTLE_RESPONSE_PRIORITY_CLOSED` / `BATTLE_CLOSED`;
- every accepted step checks player snapshots for hidden opponent hand, main-deck and rune-deck object id leakage;
- the earlier surrender result smoke remains covered separately.

The first B0 runtime fix was narrow: when spell duel closes into an existing `START_BATTLE` task, the engine promotes `ActivePlayerId` to the task player (`CleanupTaskState.PlayerId`) instead of always keeping the turn player. This preserves the existing battlefield-owner declaration model and fixes natural games where the mover is not the battle-task player.

This slice narrows the next exposed B0 blocker: after spell duel closes, the engine now asks the existing server-authored `DECLARE_BATTLE` legality model whether the `START_BATTLE` task player has any legal declaration. If not, it writes a `BATTLEFIELD_BATTLE_SKIPPED:*` end-of-turn marker, emits `BATTLE_SKIPPED` with participant metadata, suppresses repeated pending-task / battlefield-task projection for that battlefield, and returns to the turn player in open main timing.

This turn-start slice narrows the follow-up B0 blocker: after turn-start ready / draw / score effects complete, `ResolveTurnStart` now advances still-contested battlefield tasks through the same shared `AdvancePendingBattlefieldTasksAfterStateChange` path used by movement and battle cleanup. The full-game probe proves no-legal skipped battlefields reopen on later turns and can reach real `DECLARE_BATTLE` / `BATTLE_CLOSED` once both sides' units are ready.

This score-victory slice narrows the next B0 blocker. When a non-turn-player `START_BATTLE` task resolves and no further battlefield task opens, `AdvancePendingBattlefieldTasksAfterStateChange` now restores `ActivePlayerId` to `TurnPlayerId` for ordinary open main timing so the next server-authored `END_TURN` prompt is accepted by the same authority check that produced it. `BuildTurnStartEvents` also avoids adding a second `MATCH_WON` when pre-rune-call battlefield scoring already emitted the win event.

This distinct-deck slice adds no runtime rule changes. It broadens the B0 probe from mirrored Jhin decks to a second legal official deck pairing: P1 uses `UNL-181/219` Jhin with `UNL-022/219`, while P2 uses `SFD·181/221` Rumble with `SFD·026/221`. The auto-driver also avoids selecting `待命` units as its representative battle-path unit so this B0 slice stays on the battle / score path instead of detouring into the standby cleanup family.

This standby-heavy slice also adds no runtime rule changes. It broadens the same battle / score probe to a Poppy official deck pair: P1 uses `UNL-181/219` Jhin with `UNL-022/219`, while P2 uses `UNL-203/219` Poppy with `UNL-116/219`. The existing standby-aware driver can keep the B0 route on a real `DECLARE_BATTLE` / `BATTLE_CLOSED` / score-victory path even when the low-curve deck includes standby-capable cards.

This damage-assignment slice adds no runtime rule changes. It broadens B0 from real battle close to a real official-deck multi-defender battle damage assignment window. Both players use legal Lillia green/blue decks containing official `UNL-036/219` Mutant Kitten (`壁垒`) and `UNL-090/219` LeBlanc (`后排`). The driver stages two invading units on one battlefield through server prompts, opens `BATTLE_DAMAGE_ASSIGNMENT_OPENED`, submits `ASSIGN_COMBAT_DAMAGE` for both players, and observes `DAMAGE_APPLIED` + `BATTLE_CLOSED` without hidden-zone leakage.

This response-activation slice adds no runtime rule changes. It broadens B0 from response-pass / assignment fixtures to a real official-deck battle response activation path. Both players use legal Vex green/purple decks with `UNL-232/219` Vex legend, `UNL-055/219` Vex champion, and official `UNL-194/219` Shadow. The driver follows Shadow's official text by playing it directly to a contested battlefield active, opens `BATTLE_RESPONSE_PRIORITY_OPENED`, submits prompt-authored `ACTIVATE_ABILITY` with the required payment resource action when quoted, resolves Shadow's stack item, observes `STUNNED`, returns to battle response priority, and then closes the battle without hidden-zone leakage.

This action-log replay slice adds no runtime rule changes. The earlier replay test started from the already verified mirrored Jhin official low-curve `BATTLE_CLOSED` state and covered only post-battle scoring. This slice extends that evidence back to the seated room baseline: `OfficialLowCurveFullGameScoreVictoryActionLogReplaysToFinalStateHash` records `SUBMIT_DECK`, `READY`, `MULLIGAN`, tap/play/move/focus, reopened battle declaration, and score-victory `END_TURN` commands through `MatchJournal`, stores replayable raw command payloads for prompt-derived object ids, and verifies `MatchActionLogReplayer.VerifyFinalStateAsync` reaches the exact expected final state hash with recovered event payload hashes.

## Evidence

- Added `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs`.
- Strengthened `tests/Riftbound.ConformanceTests/BoardTaskQueueFoundationTests.cs` with `PassFocusClosesSpellDuelAndPromotesBattlefieldOwnerWhenMoverIsTurnPlayer`.
- Strengthened `tests/Riftbound.ConformanceTests/BoardTaskQueueFoundationTests.cs` with `PassFocusClosesSpellDuelAndSkipsStartBattleWhenNoLegalCombatants`.
- Strengthened `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs` with `OfficialLowCurveDecksReopenContestedBattleAfterSkippedCombatantsReadyAcrossTurns`.
- Strengthened `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs` with `OfficialLowCurveDecksReachScoreVictoryAfterRealBattleThroughServerPrompts`.
- Strengthened `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs` with `OfficialLowCurvePostBattleScoreVictoryActionLogReplaysToFinalStateHash`.
- Strengthened `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs` with `OfficialLowCurveFullGameScoreVictoryActionLogReplaysToFinalStateHash`.
- Strengthened `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs` with `DistinctOfficialLowCurveDecksReachScoreVictoryAfterRealBattleThroughServerPrompts`.
- Strengthened `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs` with `StandbyHeavyOfficialLowCurveDecksReachScoreVictoryAfterRealBattleThroughServerPrompts`.
- Strengthened `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs` with `OfficialDecksResolveMultiDefenderBattleDamageAssignmentThroughServerPrompts`.
- Strengthened `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs` with `OfficialDecksResolveShadowBattleResponseActivationThroughServerPrompts`.
- Test driver changed in `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs` to parameterize P1/P2 decks, force required official cards into legal low-curve Lillia / Vex decks for the damage-assignment and response-activation routes, support required official exclusive units, and skip `待命` units when choosing the representative play / move unit.
- Runtime changed in `src/Riftbound.Engine/CoreRuleEngine.cs` only in the spell-duel close handoff path.
- Runtime changed in `src/Riftbound.Engine/CoreRuleEngine.cs` turn start to advance pending battlefield tasks after turn-start ready / draw / score state is built.
- Runtime changed in `src/Riftbound.Engine/CoreRuleEngine.cs` to restore ordinary open main `ActivePlayerId` to `TurnPlayerId` after battlefield-task advancement finds no further contested battlefield.
- Runtime changed in `src/Riftbound.Engine/CoreRuleEngine.cs` to prevent duplicate turn-start `MATCH_WON` events when battlefield scoring wins before draw.
- Runtime projection / queue filtering changed in `src/Riftbound.Engine/MatchSession.cs` to make the skip marker suppress repeated same-turn battlefield tasks and hide the internal marker from public continuous-effect projection.
- Test driver changed in `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs` to write replayable raw command payloads for B0 prompt-derived commands instead of only `cmdType`, so action-log recovery can reconstruct the same `GameCommand` object ids / destinations used by the server prompt path.

## Residuals

This is not a READY claim and does not close complete game resolution. The current B0 full-game probe now proves mirrored Jhin low-curve decks, a distinct Jhin-vs-Rumble low-curve official deck pair, and a standby-heavy Jhin-vs-Poppy official deck pair can submit legal decks, pass opening prompts, create a contested battlefield, consume no-legal battle tasks, reopen them on later turns, declare and close a real battle, and finish by score-based `MATCH_WON` without surrender. It also proves an official Lillia multi-defender damage-assignment deck pair can open and resolve `ASSIGN_COMBAT_DAMAGE` through server prompts, and an official Vex / Shadow deck pair can open and resolve a battle response activation through server prompts. The action-log replay slice now proves the mirrored Jhin score-victory path can be recovered from seated-room `SUBMIT_DECK` through final score win to the same final state hash. It does not prove all real deck archetypes, full standby reveal / reaction mechanics, complete combat damage assignment breadth, all response windows, or all card-effect families can complete a game.

Current §6 mouth count after this slice: `private static bool Is*CardNo(...)` helper definitions remain 48 in `src/Riftbound.Engine`. Coverage-matrix unsupported functional-unit count was not changed by this B0 test-driver slice.

Open follow-up:

- evidence whether same-turn effects that ready or add units after a no-legal battle skip should reopen that battlefield battle task before turn end.
- broaden action-log replay beyond the mirrored Jhin score-victory route into distinct-deck, standby-heavy, damage-assignment, and response-activation B0 representatives.
- broaden standby-heavy coverage beyond the battle-path representative into explicit standby reveal / reaction and non-ready-base cleanup branches.
- broaden B0 beyond representative damage assignment / response activation into standby reaction windows, replacement / duration cleanup, and more card-effect families.

## Validation

Focused validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~FullGameEndToEndTests"
```

Result:

```text
Passed: 9, Failed: 0, Skipped: 0, Total: 9
```

Adjacent validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~FullGameEndToEndTests|FullyQualifiedName~BoardTaskQueueFoundationTests|FullyQualifiedName~SpellDuelBattleStateMachineTests|FullyQualifiedName~BattlefieldContestBattleTaskGuardTests|FullyQualifiedName~DeclareBattle|FullyQualifiedName~GameHubJoinTests|FullyQualifiedName~BattleDamageAssignment|FullyQualifiedName~ShadowActivatedAbility"
```

Result:

```text
Passed: 530, Failed: 0, Skipped: 0, Total: 530
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
Passed: 8463, Failed: 0, Skipped: 0, Total: 8463
```

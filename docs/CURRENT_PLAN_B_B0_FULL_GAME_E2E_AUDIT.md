# Plan B / B0 Full-Game E2E Audit

Date: 2026-06-26

Status: focused B0/B2 same-battlefield static-keyword official-deck replay slice accepted; project remains **NOT READY**.

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
- from a seated official low-curve initial state, the full mirrored Jhin, distinct Jhin-vs-Rumble, and standby-heavy Jhin-vs-Poppy `SUBMIT_DECK` -> `READY` -> `MULLIGAN` -> gameplay -> `DECLARE_BATTLE` -> score-victory command streams replay through `MatchActionLogReplayer` to the same final state hash and recovered event payload hash;
- from a seated official Lillia initial state, the full multi-defender `DECLARE_BATTLE` -> `ASSIGN_COMBAT_DAMAGE` command stream replays through `MatchActionLogReplayer` to the same final state hash and recovered event payload hash;
- from a seated official Vex initial state, the full Shadow `DECLARE_BATTLE` -> `ACTIVATE_ABILITY` -> stack resolution -> battle close command stream replays through `MatchActionLogReplayer` to the same final state hash and recovered event payload hash;
- from a seated official Poppy standby initial state, the full `HIDE_CARD` -> `REVEAL_CARD` -> non-standby battle -> score-victory command stream replays through `MatchActionLogReplayer` to the same final state hash and recovered event payload hash;
- from a seated official Poppy / Bandle Tree initial state, the full `HIDE_CARD` to `BATTLEFIELD:<Bandle Tree>` -> non-standby battle -> score-victory command stream replays through `MatchActionLogReplayer` to the same final state hash and recovered event payload hash while the battlefield standby card remains face-down;
- from a seated official Poppy / Garen / Demacia Envoy initial state, the full `PLAY_CARD` / `MOVE_UNIT` staging -> same-battlefield static-aura `DECLARE_BATTLE` -> score-victory command stream replays through `MatchActionLogReplayer` to the same final state hash and recovered event payload hash while `OGS·013/024` Garen's `BehaviorSpec.StaticAuras` projection gives `UNL-092/219` Demacia Envoy `staticPowerBonus=1`;
- from a seated official Jhin / Farron Captain / Ascended Believer initial state, the full `PLAY_CARD` / `MOVE_UNIT` staging -> same-battlefield static-keyword `DECLARE_BATTLE` -> score-victory command stream replays through `MatchActionLogReplayer` to the same final state hash and recovered event payload hash while `OGN·015/298` Farron Captain's `BehaviorSpec.StaticAuras` projection gives `UNL-004/219` Ascended Believer `keyword=强攻` / `keywordBonus=1`;
- from a seated official Vex / Teemo / Shadow initial state, the full `HIDE_CARD` -> Shadow `ACTIVATE_ABILITY` -> `REVEAL_CARD` as `STANDBY_REACTION` -> Teemo stack resolution -> Shadow stack resolution -> battle close command stream replays through `MatchActionLogReplayer` to the same final state hash and recovered event payload hash;
- the score-victory path now runs the original mirrored Jhin deck, a distinct Jhin-vs-Rumble official deck pair, and a standby-heavy Jhin-vs-Poppy official deck pair;
- a legal official Lillia deck pair can drive multi-defender `DECLARE_BATTLE` into `ASSIGN_COMBAT_DAMAGE`, submit both players' assignments, and close battle with `DAMAGE_APPLIED` / `BATTLE_CLOSED`;
- a legal official Vex deck pair can drive `DECLARE_BATTLE` into `BATTLE_RESPONSE_PRIORITY_OPENED`, activate `UNL-194/219` Shadow through `ACTIVATE_ABILITY`, resolve the stack, return to battle response priority, and close battle with `BATTLE_RESPONSE_PRIORITY_CLOSED` / `BATTLE_CLOSED`;
- every accepted step that uses the shared B0 `AssertAccepted` helper now checks player snapshots for hidden opponent hand, main-deck and rune-deck object id leakage;
- the earlier surrender result smoke remains covered separately.

This accepted-step hidden snapshot guard slice adds no runtime rule changes. It tightens the B0 test harness by moving `AssertNoHiddenZoneLeak` into `FullGameEndToEndTests.AssertAccepted`, so accepted `SUBMIT_DECK`, `READY`, `MULLIGAN`, `TAP_RUNE`, `PLAY_CARD`, `MOVE_UNIT`, `DECLARE_BATTLE`, `ACTIVATE_ABILITY`, `ASSIGN_COMBAT_DAMAGE`, `PASS_PRIORITY`, `PASS_FOCUS`, `END_TURN`, `HIDE_CARD`, `REVEAL_CARD` and `SURRENDER` results covered by the shared helper all reject opponent hidden-zone object id leakage immediately.

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

This same-battlefield static-aura replay slice also adds no runtime rule changes. It extends the seated-room action-log recovery check to a legal official Poppy deck containing official `OGS·013/024` Garen and `UNL-092/219` Demacia Envoy. The driver uses the normal official opening seed path, moves both friendly units to the same battlefield through server commands, verifies Garen projects `SAME_BATTLEFIELD_OTHER_FRIENDLY_UNITS_POWER_PLUS_ONE` to the Envoy via `BehaviorSpec.StaticAuras`, declares battle with the Envoy, observes `DAMAGE_APPLIED` with `basePower=2`, `staticPowerBonus=1`, `combatPower=3`, and `damage=3`, then continues through score victory and action-log replay.

This same-battlefield static-keyword replay slice also adds no runtime rule changes. It extends the seated-room action-log recovery check to a legal official Jhin deck containing official `OGN·015/298` Farron Captain and `UNL-004/219` Ascended Believer. The driver uses the normal official opening seed path, moves both friendly units to the same battlefield through server commands, verifies Farron projects `SAME_BATTLEFIELD_OTHER_FRIENDLY_UNITS_KEYWORD` to the Believer via `BehaviorSpec.StaticAuras`, declares battle with the Believer, observes `DAMAGE_APPLIED` with `basePower=1`, `keyword=强攻`, `keywordBonus=1`, no `staticPowerBonus`, `combatPower=2`, and `damage=2`, then continues through score victory and action-log replay.

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
- Strengthened `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs` with `OfficialDeckMidgameAppliesSameBattlefieldStaticKeywordAndScoreVictoryActionLogReplaysToFinalStateHash`.
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
- Runtime projection / queue filtering changed in `src/Riftbound.Engine/MatchSession.cs` to make the skip marker suppress repeated same-turn battlefield tasks and hide the internal marker from public continuous-effect projection.
- Test driver changed in `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs` to write replayable raw command payloads for B0 prompt-derived commands instead of only `cmdType`, so action-log recovery can reconstruct the same `GameCommand` object ids / destinations / damage assignments / activated ability choices / hide-reveal standby choices / battlefield extra-standby destination used by the server prompt path.
- Test guard strengthened in `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs`: `AssertAccepted` now calls `AssertNoHiddenZoneLeak`, giving the B0 full-game harness step-level hidden snapshot coverage for every accepted result routed through the shared helper.

## Residuals

This is not a READY claim and does not close complete game resolution. The current B0 full-game probe now proves mirrored Jhin low-curve decks, a distinct Jhin-vs-Rumble low-curve official deck pair, and a standby-heavy Jhin-vs-Poppy official deck pair can submit legal decks, pass opening prompts, create a contested battlefield, consume no-legal battle tasks, reopen them on later turns, declare and close a real battle, and finish by score-based `MATCH_WON` without surrender. It also proves an official Lillia multi-defender damage-assignment deck pair can open and resolve `ASSIGN_COMBAT_DAMAGE` through server prompts, official Vex / Shadow deck pairs can open and resolve a battle response activation through server prompts, an official Poppy / Garen / Demacia Envoy deck can carry a spec-driven same-battlefield static aura into real battle damage and score-victory replay, and an official Jhin / Farron Captain / Ascended Believer deck can carry a spec-driven same-battlefield RULE_TEXT keyword aura into real battle damage and score-victory replay. The action-log replay slice now proves the mirrored Jhin, distinct Jhin-vs-Rumble, standby-heavy Jhin-vs-Poppy, Lillia damage-assignment, Vex / Shadow response-activation, explicit Pakaa Cub standby hide/reveal, Bandle Tree battlefield extra-standby hide, Garen same-battlefield static-aura, Farron same-battlefield static-keyword aura, and Vex / Shadow / Teemo standby reaction paths can be recovered from seated-room `SUBMIT_DECK` through the final representative battle / score result to the same final state hash. It does not prove all real deck archetypes, all standby reaction card effects / targeted standby reactions, battlefield extra-standby reveal / cleanup breadth, non-ready-base standby cleanup breadth, complete combat damage assignment breadth, full static-aura official breadth, full RULE_TEXT keyword aura breadth, all response windows, or all card-effect families can complete a game.

Current §6 mouth count after this slice: `bool Is*CardNo(` helper definitions are 0 across `src/Riftbound.Engine`, `src/Riftbound.Contracts`, `src/Riftbound.CardCatalog` and `tests/Riftbound.ConformanceTests`; the broader residual `IsSourceCardNoForAbility` occurrence is the P4 activated ability catalog source mapping and call sites, not a newly introduced card-specific engine branch. Coverage-matrix unsupported functional-unit count was not changed by this B0 test-harness slice.

Open follow-up:

- evidence whether same-turn effects that ready or add units after a no-legal battle skip should reopen that battlefield battle task before turn end.
- broaden standby-heavy coverage beyond the Teemo stack-reaction representative into targeted standby reactions, battlefield standby reveal / cleanup branches, and non-ready-base cleanup branches.
- broaden B0 beyond representative damage assignment / response activation into more target ordering, replacement / duration cleanup, and card-effect families.

## Validation

Focused validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~FullGameEndToEndTests"
```

Result:

```text
Passed: 22, Failed: 0, Skipped: 0, Total: 22
```

Adjacent validation passed:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~FullGameEndToEndTests|FullyQualifiedName~BoardTaskQueueFoundationTests|FullyQualifiedName~SpellDuelBattleStateMachineTests|FullyQualifiedName~BattlefieldContestBattleTaskGuardTests|FullyQualifiedName~DeclareBattle|FullyQualifiedName~GameHubJoinTests|FullyQualifiedName~BattleDamageAssignment|FullyQualifiedName~ShadowActivatedAbility|FullyQualifiedName~RevealCard|FullyQualifiedName~Standby|FullyQualifiedName~HideCard"
```

Result:

```text
Passed: 721, Failed: 0, Skipped: 0, Total: 721
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
Passed: 8714, Failed: 0, Skipped: 0, Total: 8714
```

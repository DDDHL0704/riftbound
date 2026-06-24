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
- `SURRENDER`

The new engine regression proves the spell-duel close handoff chooses the battlefield task player when the turn player is the mover. This is the natural path that previous fixture-style tests did not cover.

The no-legal battle regression proves the next natural blocker is consumed by shared engine state rather than single-card logic. After spell duel closes, `CoreRuleEngine` checks the existing server-authored `DECLARE_BATTLE` requirements for the `START_BATTLE` task player. When no ready face-up attacker / defender declaration exists, the engine emits `BATTLE_SKIPPED`, records `BATTLEFIELD_BATTLE_SKIPPED:*` until end of turn, clears the blocking battlefield task family from state / snapshot projection, and returns to neutral open main timing.

## Hidden Information Evidence

`FullGameEndToEndTests.AssertNoHiddenZoneLeak` serializes each viewer snapshot after every accepted step and rejects exposure of opponent hand, main-deck and rune-deck object ids. This is a focused hidden-zone guard for the full-game probe and does not replace the broader `MatchRecovery` spectator validation suite.

## Validation

Focused validation:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~FullGameEndToEndTests|FullyQualifiedName~PassFocusClosesSpellDuelAndSkipsStartBattleWhenNoLegalCombatants|FullyQualifiedName~PassFocusClosesSpellDuelAndPromotesBattlefieldOwnerWhenMoverIsTurnPlayer|FullyQualifiedName~PassFocusClosesSpellDuelAndPromotesStartBattleWithParticipantData|FullyQualifiedName~ActiveStartBattleDeclareBattleClearsTaskAndPreservesRepresentativeEvents"
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
Passed: 366, Failed: 0, Skipped: 0, Total: 366
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
Passed: 8351, Failed: 0, Skipped: 0, Total: 8351
```

## Non-Closure

This evidence proves the engine can drive legal official decks through setup, opening, live prompt-driven gameplay, contested battlefield task creation, no-legal battle skip and match result without leaking hidden zones. It does not close full official game completion, real battle resolution from official decks, score-based victory, complete combat damage assignment breadth, complete spell-duel / battle lifecycle breadth, full card matrix readiness, frontend gates or final READY.

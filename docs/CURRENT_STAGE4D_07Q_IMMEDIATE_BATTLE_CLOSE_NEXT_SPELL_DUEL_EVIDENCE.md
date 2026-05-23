# Stage 4D-07Q Immediate Battle Close Next Spell Duel Evidence

Date: 2026-05-23

Project status: **NOT READY**

## Changed Files

- `tests/Riftbound.ConformanceTests/BattlefieldContestBattleTaskGuardTests.cs`
- `docs/CURRENT_A_MASTER_CHECKPOINT.md`
- `docs/CURRENT_COMPLETION_AUDIT.md`
- `docs/CURRENT_SERVER_RULE_AUDIT.md`
- `docs/CURRENT_STAGE4D_P0_P1_CLOSURE_PLAN.md`
- `docs/CURRENT_STAGE4D_NEXT_DISPATCH_AND_WRITELOCKS.md`
- `docs/CURRENT_STAGE4D_SHARED_COORDINATION_BOARD.md`
- `docs/CURRENT_STAGE4D_07Q_IMMEDIATE_BATTLE_CLOSE_NEXT_SPELL_DUEL_AUDIT.md`
- `docs/CURRENT_STAGE4D_07Q_IMMEDIATE_BATTLE_CLOSE_NEXT_SPELL_DUEL_EVIDENCE.md`

## Validation

```bash
/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ImmediateDeclareBattleAdvancesNextContestedBattlefieldTaskAfterCurrentBattleCloses"
```

Result: passed `1/1`.

```bash
/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~BattlefieldContestBattleTaskGuardTests|FullyQualifiedName~BoardTaskQueueFoundationTests|FullyQualifiedName~SpellDuelBattleStateMachineTests|FullyQualifiedName~BattleDamageAssignmentLifecycleTests|FullyQualifiedName~Prompt|FullyQualifiedName~MatchStateHasher|FullyQualifiedName~GameHubJoinTests"
```

Result: passed `446/446`.

```bash
/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --no-restore
```

Result: passed `5405/5405`.

```bash
git diff --check
rg -n '^(<<<<<<<|=======|>>>>>>>)' docs tests src
rg -n '[ \t]+$' docs/CURRENT_STAGE4D_05*.md docs/CURRENT_STAGE4D_06*.md docs/CURRENT_STAGE4D_07*.md
```

Result: passed. `git diff --check` exited `0`; conflict-marker scan and trailing-whitespace scan returned no matches.

## Runtime And Protocol

Runtime changed: no. Existing immediate battle close and next-contest advancement already emit battle close, control-resolution and next-spell-duel handoff payloads in the asserted order.

Protocol shape changed: no.

Hidden-info leakage found: no.

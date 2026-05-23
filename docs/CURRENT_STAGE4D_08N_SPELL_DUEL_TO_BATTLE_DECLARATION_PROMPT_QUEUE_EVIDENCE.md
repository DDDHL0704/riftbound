# Stage 4D-08N Spell Duel To Battle Declaration Prompt Queue Evidence

Date: 2026-05-23

Project status: **NOT READY**

## Changed Files

- `tests/Riftbound.ConformanceTests/BoardTaskQueueFoundationTests.cs`
- `docs/CURRENT_A_MASTER_CHECKPOINT.md`
- `docs/CURRENT_COMPLETION_AUDIT.md`
- `docs/CURRENT_SERVER_RULE_AUDIT.md`
- `docs/CURRENT_STAGE4D_P0_P1_CLOSURE_PLAN.md`
- `docs/CURRENT_STAGE4D_NEXT_DISPATCH_AND_WRITELOCKS.md`
- `docs/CURRENT_STAGE4D_SHARED_COORDINATION_BOARD.md`
- `docs/CURRENT_STAGE4D_08N_SPELL_DUEL_TO_BATTLE_DECLARATION_PROMPT_QUEUE_AUDIT.md`
- `docs/CURRENT_STAGE4D_08N_SPELL_DUEL_TO_BATTLE_DECLARATION_PROMPT_QUEUE_EVIDENCE.md`

## Validation

```bash
/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PassFocusClosesSpellDuelAndPromotesStartBattleWithParticipantData"
```

Result: passed `1/1`.

```bash
/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~BoardTaskQueueFoundationTests|FullyQualifiedName~BattlefieldContestBattleTaskGuardTests|FullyQualifiedName~SpellDuelBattleStateMachineTests|FullyQualifiedName~BattleDamageAssignmentLifecycleTests|FullyQualifiedName~Prompt|FullyQualifiedName~MatchStateHasher|FullyQualifiedName~GameHubJoinTests"
```

Result: passed `446/446`.

```bash
/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --no-restore
```

Result: passed `5405/5405`.

```bash
git diff --check
```

Result: passed.

```bash
rg -n '^(<<<<<<<|=======|>>>>>>>)' docs tests src
```

Result: passed; no conflict markers found.

```bash
rg -n '[ \t]+$' docs/CURRENT_STAGE4D_05*.md docs/CURRENT_STAGE4D_06*.md docs/CURRENT_STAGE4D_07*.md docs/CURRENT_STAGE4D_08*.md
```

Result: passed; no trailing whitespace found.

## Runtime And Protocol

Runtime changed: no. Existing spell-duel close / start-battle promotion behavior already advanced to the battle-declaration prompt.

Protocol shape changed: no.

Hidden-info leakage found: no.

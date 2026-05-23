# Stage 4D-06E Assign Combat Damage Stale Prompt Replay Evidence

Date: 2026-05-22

Project status: **NOT READY**

## Changed Files

- `tests/Riftbound.ConformanceTests/BattleDamageAssignmentLifecycleTests.cs`
- `docs/CURRENT_A_MASTER_CHECKPOINT.md`
- `docs/CURRENT_COMPLETION_AUDIT.md`
- `docs/CURRENT_SERVER_RULE_AUDIT.md`
- `docs/CURRENT_STAGE4D_P0_P1_CLOSURE_PLAN.md`
- `docs/CURRENT_STAGE4D_NEXT_DISPATCH_AND_WRITELOCKS.md`
- `docs/CURRENT_STAGE4D_SHARED_COORDINATION_BOARD.md`
- `docs/CURRENT_STAGE4D_06E_ASSIGN_COMBAT_DAMAGE_STALE_PROMPT_REPLAY_AUDIT.md`
- `docs/CURRENT_STAGE4D_06E_ASSIGN_COMBAT_DAMAGE_STALE_PROMPT_REPLAY_EVIDENCE.md`

## Validation

```bash
source scripts/dev-env.sh && dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~NaturalAssignCombatDamageStalePromptReplayAfterNextContestStartsRejectsWithoutMutation"
```

Result: passed `1/1`.

```bash
source scripts/dev-env.sh && dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~AssignCombatDamage|FullyQualifiedName~BattleDamageAssignment|FullyQualifiedName~DeclareBattle|FullyQualifiedName~TaskQueue|FullyQualifiedName~Prompt|FullyQualifiedName~MatchRecovery"
```

Result: passed `378/378`.

```bash
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

Result: passed `5367/5367`.

```bash
git diff --check
```

Result: passed.

```bash
rg -n '^(<<<<<<<|=======|>>>>>>>)' docs tests src
```

Result: passed with no matches.

```bash
rg -n '[ \t]+$' docs/CURRENT_STAGE4D_05{J,K,L,M,N,O,P,Q,R,S,T,U,V,W,X,Y,Z}_*_REPLAY_{AUDIT,EVIDENCE}.md docs/CURRENT_STAGE4D_06A_PASS_REPLAY_{AUDIT,EVIDENCE}.md docs/CURRENT_STAGE4D_06B_ORDER_TRIGGERS_REPLAY_{AUDIT,EVIDENCE}.md docs/CURRENT_STAGE4D_06C_SPELL_DUEL_STALE_PROMPT_REPLAY_{AUDIT,EVIDENCE}.md docs/CURRENT_STAGE4D_06D_STACK_PRIORITY_STALE_PROMPT_REPLAY_{AUDIT,EVIDENCE}.md docs/CURRENT_STAGE4D_06E_ASSIGN_COMBAT_DAMAGE_STALE_PROMPT_REPLAY_{AUDIT,EVIDENCE}.md
```

Result: passed with no matches.

## Runtime And Protocol

Runtime changed: no. The existing `MatchSession.TryRejectStalePrompt(...)` rejects raw commands with stale `promptId` / `snapshotTick`; the new test makes the assign-combat-damage next-contest stale prompt replay behavior explicit.

Protocol shape changed: no.

Hidden-info leakage found: no.

# Stage 4D-06M Declare Battle Stale Prompt Replay Evidence

Date: 2026-05-22

Project status: **NOT READY**

## Changed Files

- `tests/Riftbound.ConformanceTests/BattlefieldContestBattleTaskGuardTests.cs`
- `docs/CURRENT_A_MASTER_CHECKPOINT.md`
- `docs/CURRENT_COMPLETION_AUDIT.md`
- `docs/CURRENT_SERVER_RULE_AUDIT.md`
- `docs/CURRENT_STAGE4D_P0_P1_CLOSURE_PLAN.md`
- `docs/CURRENT_STAGE4D_NEXT_DISPATCH_AND_WRITELOCKS.md`
- `docs/CURRENT_STAGE4D_SHARED_COORDINATION_BOARD.md`
- `docs/CURRENT_STAGE4D_06M_DECLARE_BATTLE_STALE_PROMPT_REPLAY_AUDIT.md`
- `docs/CURRENT_STAGE4D_06M_DECLARE_BATTLE_STALE_PROMPT_REPLAY_EVIDENCE.md`

## Validation

```bash
DOTNET_ROOT=${HOME}/.dotnet ${HOME}/.dotnet/dotnet test Riftbound.slnx --no-restore --filter FullyQualifiedName~DeclareBattleStalePromptReplayAfterNextSpellDuelStartsRejectsWithoutMutation
```

Result: passed `1/1`.

```bash
DOTNET_ROOT=${HOME}/.dotnet ${HOME}/.dotnet/dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~BattlefieldContestBattleTaskGuardTests|FullyQualifiedName~DeclareBattle|FullyQualifiedName~SpellDuel|FullyQualifiedName~TaskQueue|FullyQualifiedName~Prompt|FullyQualifiedName~BattleDamageAssignmentLifecycleTests|FullyQualifiedName~MatchRecovery"
```

Result: passed `415/415`.

```bash
DOTNET_ROOT=${HOME}/.dotnet ${HOME}/.dotnet/dotnet test Riftbound.slnx --no-restore
```

Result: passed `5375/5375`.

```bash
git diff --check
```

Result: passed.

```bash
rg -n '^(<<<<<<<|=======|>>>>>>>)' docs tests src
```

Result: passed with no matches.

```bash
rg -n '[ \t]+$' docs/CURRENT_STAGE4D_05{J,K,L,M,N,O,P,Q,R,S,T,U,V,W,X,Y,Z}_*_REPLAY_{AUDIT,EVIDENCE}.md docs/CURRENT_STAGE4D_06A_PASS_REPLAY_{AUDIT,EVIDENCE}.md docs/CURRENT_STAGE4D_06B_ORDER_TRIGGERS_REPLAY_{AUDIT,EVIDENCE}.md docs/CURRENT_STAGE4D_06C_SPELL_DUEL_STALE_PROMPT_REPLAY_{AUDIT,EVIDENCE}.md docs/CURRENT_STAGE4D_06D_STACK_PRIORITY_STALE_PROMPT_REPLAY_{AUDIT,EVIDENCE}.md docs/CURRENT_STAGE4D_06E_ASSIGN_COMBAT_DAMAGE_STALE_PROMPT_REPLAY_{AUDIT,EVIDENCE}.md docs/CURRENT_STAGE4D_06F_PAY_COST_STALE_PROMPT_REPLAY_{AUDIT,EVIDENCE}.md docs/CURRENT_STAGE4D_06G_ORDER_TRIGGERS_STALE_PROMPT_REPLAY_{AUDIT,EVIDENCE}.md docs/CURRENT_STAGE4D_06H_HAND_CHOICE_STALE_PROMPT_REPLAY_{AUDIT,EVIDENCE}.md docs/CURRENT_STAGE4D_06I_MULLIGAN_STALE_PROMPT_REPLAY_{AUDIT,EVIDENCE}.md docs/CURRENT_STAGE4D_06J_READY_STALE_PROMPT_REPLAY_{AUDIT,EVIDENCE}.md docs/CURRENT_STAGE4D_06K_SUBMIT_DECK_STALE_PROMPT_REPLAY_{AUDIT,EVIDENCE}.md docs/CURRENT_STAGE4D_06L_MOVE_UNIT_STALE_PROMPT_REPLAY_{AUDIT,EVIDENCE}.md docs/CURRENT_STAGE4D_06M_DECLARE_BATTLE_STALE_PROMPT_REPLAY_{AUDIT,EVIDENCE}.md
```

Result: passed with no matches.

## Runtime And Protocol

Runtime changed: no. `MatchSession.SubmitAsync(...)` already applies `TryRejectStalePrompt(...)` before resolving core commands; this slice adds coverage for the `DECLARE_BATTLE` battle-declaration to next-spell-duel transition.

Protocol shape changed: no.

Hidden-info leakage found: no.

## Coordination

Before opening this A_MAIN slice, `DOC_MATRIX_CURRENT` was clean at handoff/source commit `4c999922`. A_MAIN did not integrate or reject that commit in this slice because main still has active 05J-06M dirty runtime/test/docs slices. `DOC_MATRIX_CURRENT` should remain in guard until A_MAIN records the integration / rejection result or publishes a new explicit scope.

# Stage 4D-06O Surrender Stale Prompt Replay Evidence

Date: 2026-05-22

Project status: **NOT READY**

## Changed Files

- `src/Riftbound.Engine/MatchSession.cs`
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
- `docs/CURRENT_A_MASTER_CHECKPOINT.md`
- `docs/CURRENT_COMPLETION_AUDIT.md`
- `docs/CURRENT_SERVER_RULE_AUDIT.md`
- `docs/CURRENT_STAGE4D_P0_P1_CLOSURE_PLAN.md`
- `docs/CURRENT_STAGE4D_NEXT_DISPATCH_AND_WRITELOCKS.md`
- `docs/CURRENT_STAGE4D_SHARED_COORDINATION_BOARD.md`
- `docs/CURRENT_STAGE4D_06O_SURRENDER_STALE_PROMPT_REPLAY_AUDIT.md`
- `docs/CURRENT_STAGE4D_06O_SURRENDER_STALE_PROMPT_REPLAY_EVIDENCE.md`

## Validation

```bash
DOTNET_ROOT=${HOME}/.dotnet ${HOME}/.dotnet/dotnet test Riftbound.slnx --no-restore --filter FullyQualifiedName~SurrenderStalePromptReplayAfterMatchFinishedRejectsWithoutMutation
```

Result: passed `1/1`.

```bash
DOTNET_ROOT=${HOME}/.dotnet ${HOME}/.dotnet/dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureRunnerTests|FullyQualifiedName~ConformanceFixtureShapeTests|FullyQualifiedName~GameHubJoinTests"
```

Result: passed `3283/3283`.

```bash
DOTNET_ROOT=${HOME}/.dotnet ${HOME}/.dotnet/dotnet test Riftbound.slnx --no-restore
```

Result: passed `5377/5377`.

```bash
git diff --check
```

Result: passed.

```bash
rg -n '^(<<<<<<<|=======|>>>>>>>)' docs tests src
```

Result: passed with no matches.

```bash
rg -n '[ \t]+$' docs/CURRENT_STAGE4D_05{J,K,L,M,N,O,P,Q,R,S,T,U,V,W,X,Y,Z}_*_REPLAY_{AUDIT,EVIDENCE}.md docs/CURRENT_STAGE4D_06A_PASS_REPLAY_{AUDIT,EVIDENCE}.md docs/CURRENT_STAGE4D_06B_ORDER_TRIGGERS_REPLAY_{AUDIT,EVIDENCE}.md docs/CURRENT_STAGE4D_06{C,D,E,F,G,H,I,J,K,L,M,N,O}_*_STALE_PROMPT_REPLAY_{AUDIT,EVIDENCE}.md
```

Result: passed with no matches.

## Runtime And Protocol

Runtime changed: yes. `MatchSession.SubmitAsync(...)` now lets prompt-stamped finished-state command replays hit `TryRejectStalePrompt(...)` before the existing `MATCH_FINISHED` throw path. Unstamped finished-state submits retain `MATCH_FINISHED`.

Protocol shape changed: no.

Hidden-info leakage found: no.

## Coordination

Before opening this A_MAIN slice, `DOC_MATRIX_CURRENT` was clean at handoff/source commit `4c999922`. A_MAIN did not integrate or reject that commit in this slice because main still has active 05J-06O dirty runtime/test/docs slices. `DOC_MATRIX_CURRENT` should remain in guard until A_MAIN records the integration / rejection result or publishes a new explicit scope.

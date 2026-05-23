# Stage 4D-06X Pending Pay Cost Stale Prompt Replay Evidence

Date: 2026-05-22

Project status: **NOT READY**

## Changed Files

- `tests/Riftbound.ConformanceTests/PaymentEngineUnificationTests.cs`
- `docs/CURRENT_A_MASTER_CHECKPOINT.md`
- `docs/CURRENT_COMPLETION_AUDIT.md`
- `docs/CURRENT_SERVER_RULE_AUDIT.md`
- `docs/CURRENT_STAGE4D_P0_P1_CLOSURE_PLAN.md`
- `docs/CURRENT_STAGE4D_NEXT_DISPATCH_AND_WRITELOCKS.md`
- `docs/CURRENT_STAGE4D_SHARED_COORDINATION_BOARD.md`
- `docs/CURRENT_STAGE4D_06X_PENDING_PAY_COST_STALE_PROMPT_REPLAY_AUDIT.md`
- `docs/CURRENT_STAGE4D_06X_PENDING_PAY_COST_STALE_PROMPT_REPLAY_EVIDENCE.md`

## Validation

```bash
DOTNET_ROOT=${HOME}/.dotnet ${HOME}/.dotnet/dotnet test /Users/dinghaolin/MyProjects/riftbound-dotnet/Riftbound.slnx --no-restore --filter "FullyQualifiedName~PendingPayCostPromptScopedOrdinaryReplayAfterWindowClosesRejectsWithoutMutation|FullyQualifiedName~PendingPayCostPromptScopedTemporaryResourceReplayAfterWindowClosesRejectsWithoutMutation"
```

Result: passed `4/4`.

```bash
DOTNET_ROOT=${HOME}/.dotnet ${HOME}/.dotnet/dotnet test /Users/dinghaolin/MyProjects/riftbound-dotnet/Riftbound.slnx --no-restore --filter "FullyQualifiedName~PaymentEngineUnificationTests|FullyQualifiedName~PayCost|FullyQualifiedName~PAY_COST|FullyQualifiedName~PaymentEngine|FullyQualifiedName~Prompt|FullyQualifiedName~MatchStateHasher|FullyQualifiedName~TriggerPaymentTests|FullyQualifiedName~GameHubJoinTests"
```

Result: passed `1190/1190`.

```bash
DOTNET_ROOT=${HOME}/.dotnet ${HOME}/.dotnet/dotnet test /Users/dinghaolin/MyProjects/riftbound-dotnet/Riftbound.slnx --no-restore
```

Result: passed `5400/5400`.

```bash
git diff --check
rg -n '^(<<<<<<<|=======|>>>>>>>)' docs tests src
rg -n '[ \t]+$' docs/CURRENT_STAGE4D_05*.md docs/CURRENT_STAGE4D_06*.md
```

Result: passed. No whitespace warnings, conflict markers, or trailing whitespace in the new 05J-06X evidence docs.

## Runtime And Protocol

Runtime changed: no. Existing `MatchSession.TryRejectStalePrompt(...)` rejects raw commands with stale `promptId` / `snapshotTick`; the new tests make pending `PAY_COST` ordinary and temporary-resource stale prompt replay behavior explicit.

Protocol shape changed: no.

Hidden-info leakage found: no.

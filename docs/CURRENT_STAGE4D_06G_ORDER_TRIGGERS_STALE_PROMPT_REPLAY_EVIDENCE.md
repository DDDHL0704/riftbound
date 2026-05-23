# Stage 4D-06G Order Triggers Stale Prompt Replay Evidence

Date: 2026-05-22

Project status: **NOT READY**

## Changed Files

- `tests/Riftbound.ConformanceTests/ConformanceFixtureShapeTests.cs`
- `docs/CURRENT_A_MASTER_CHECKPOINT.md`
- `docs/CURRENT_COMPLETION_AUDIT.md`
- `docs/CURRENT_SERVER_RULE_AUDIT.md`
- `docs/CURRENT_STAGE4D_P0_P1_CLOSURE_PLAN.md`
- `docs/CURRENT_STAGE4D_NEXT_DISPATCH_AND_WRITELOCKS.md`
- `docs/CURRENT_STAGE4D_SHARED_COORDINATION_BOARD.md`
- `docs/CURRENT_STAGE4D_06G_ORDER_TRIGGERS_STALE_PROMPT_REPLAY_AUDIT.md`
- `docs/CURRENT_STAGE4D_06G_ORDER_TRIGGERS_STALE_PROMPT_REPLAY_EVIDENCE.md`

## Validation

```bash
source scripts/dev-env.sh && dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~OrderTriggersStalePromptReplayAfterStackPriorityStartsRejectsWithoutMutation"
```

Result: passed `1/1`.

```bash
source scripts/dev-env.sh && dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~OrderTriggers|FullyQualifiedName~TriggerQueue|FullyQualifiedName~ConformanceFixtureShape|FullyQualifiedName~Prompt|FullyQualifiedName~MatchRecovery"
```

Result: passed `366/366`.

```bash
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

Result: passed `5369/5369`.

```bash
git diff --check
```

Result: passed.

```bash
rg -n '^(<<<<<<<|=======|>>>>>>>)' docs tests src
```

Result: passed with no matches.

```bash
rg -n '[ \t]+$' docs/CURRENT_STAGE4D_05{J,K,L,M,N,O,P,Q,R,S,T,U,V,W,X,Y,Z}_*_REPLAY_{AUDIT,EVIDENCE}.md docs/CURRENT_STAGE4D_06A_PASS_REPLAY_{AUDIT,EVIDENCE}.md docs/CURRENT_STAGE4D_06B_ORDER_TRIGGERS_REPLAY_{AUDIT,EVIDENCE}.md docs/CURRENT_STAGE4D_06C_SPELL_DUEL_STALE_PROMPT_REPLAY_{AUDIT,EVIDENCE}.md docs/CURRENT_STAGE4D_06D_STACK_PRIORITY_STALE_PROMPT_REPLAY_{AUDIT,EVIDENCE}.md docs/CURRENT_STAGE4D_06E_ASSIGN_COMBAT_DAMAGE_STALE_PROMPT_REPLAY_{AUDIT,EVIDENCE}.md docs/CURRENT_STAGE4D_06F_PAY_COST_STALE_PROMPT_REPLAY_{AUDIT,EVIDENCE}.md docs/CURRENT_STAGE4D_06G_ORDER_TRIGGERS_STALE_PROMPT_REPLAY_{AUDIT,EVIDENCE}.md
```

Result: passed with no matches.

## Runtime And Protocol

Runtime changed: no. The existing `MatchSession.TryRejectStalePrompt(...)` rejects raw commands with stale `promptId` / `snapshotTick`; the new test makes the order-triggers stack-priority stale prompt replay behavior explicit.

Protocol shape changed: no.

Hidden-info leakage found: no.

## Coordination

Before opening this A_MAIN slice, `DOC_MATRIX_CURRENT` was observed dirty on its authorized 03SP-03ST docs/matrix bundle. A_MAIN did not touch `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, or the 03SP-03ST candidate docs.

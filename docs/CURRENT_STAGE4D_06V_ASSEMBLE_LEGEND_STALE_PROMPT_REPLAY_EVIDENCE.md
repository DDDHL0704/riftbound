# Stage 4D-06V Assemble Legend Stale Prompt Replay Evidence

Date: 2026-05-22

Project status: **NOT READY**

## Changed Files

- `tests/Riftbound.ConformanceTests/EdgeOfNightAssembleGuardTests.cs`
- `tests/Riftbound.ConformanceTests/LegendResourceBridgeVerifierTests.cs`
- `docs/CURRENT_A_MASTER_CHECKPOINT.md`
- `docs/CURRENT_COMPLETION_AUDIT.md`
- `docs/CURRENT_SERVER_RULE_AUDIT.md`
- `docs/CURRENT_STAGE4D_P0_P1_CLOSURE_PLAN.md`
- `docs/CURRENT_STAGE4D_NEXT_DISPATCH_AND_WRITELOCKS.md`
- `docs/CURRENT_STAGE4D_SHARED_COORDINATION_BOARD.md`
- `docs/CURRENT_STAGE4D_06V_ASSEMBLE_LEGEND_STALE_PROMPT_REPLAY_AUDIT.md`
- `docs/CURRENT_STAGE4D_06V_ASSEMBLE_LEGEND_STALE_PROMPT_REPLAY_EVIDENCE.md`

## Validation

```bash
DOTNET_ROOT=${HOME}/.dotnet ${HOME}/.dotnet/dotnet test /Users/dinghaolin/MyProjects/riftbound-dotnet/Riftbound.slnx --no-restore --filter "FullyQualifiedName~EdgeOfNightAssembleStalePromptReplayAfterEquipmentAttachesRejectsWithoutMutation|FullyQualifiedName~LegendResourceBridgeStalePromptReplayAfterResourceGainRejectsWithoutMutation"
```

Result: passed `10/10`.

```bash
DOTNET_ROOT=${HOME}/.dotnet ${HOME}/.dotnet/dotnet test /Users/dinghaolin/MyProjects/riftbound-dotnet/Riftbound.slnx --no-restore --filter "FullyQualifiedName~EdgeOfNightAssembleGuardTests|FullyQualifiedName~LegendResourceBridgeVerifierTests|FullyQualifiedName~PaymentEngine|FullyQualifiedName~ActionPrompt|FullyQualifiedName~Prompt|FullyQualifiedName~MatchStateHasher|FullyQualifiedName~AssembleEquipment|FullyQualifiedName~LegendAct"
```

Result: passed `1207/1207`.

```bash
DOTNET_ROOT=${HOME}/.dotnet ${HOME}/.dotnet/dotnet test /Users/dinghaolin/MyProjects/riftbound-dotnet/Riftbound.slnx --no-restore
```

Result: passed `5394/5394`.

```bash
git diff --check
rg -n '^(<<<<<<<|=======|>>>>>>>)' docs tests src
rg -n '[ \t]+$' docs/CURRENT_STAGE4D_05*.md docs/CURRENT_STAGE4D_06*.md
```

Result: passed. No whitespace warnings, conflict markers or trailing whitespace in the new 05J-06V evidence docs.

## Runtime And Protocol

Runtime changed: no.

Protocol shape changed: no.

Hidden-info leakage found: no.

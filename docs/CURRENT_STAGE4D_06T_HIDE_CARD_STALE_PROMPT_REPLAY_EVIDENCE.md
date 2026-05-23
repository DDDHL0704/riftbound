# Stage 4D-06T Hide Card Stale Prompt Replay Evidence

Date: 2026-05-22

Project status: **NOT READY**

## Changed Files

- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
- `docs/CURRENT_A_MASTER_CHECKPOINT.md`
- `docs/CURRENT_COMPLETION_AUDIT.md`
- `docs/CURRENT_SERVER_RULE_AUDIT.md`
- `docs/CURRENT_STAGE4D_P0_P1_CLOSURE_PLAN.md`
- `docs/CURRENT_STAGE4D_NEXT_DISPATCH_AND_WRITELOCKS.md`
- `docs/CURRENT_STAGE4D_SHARED_COORDINATION_BOARD.md`
- `docs/CURRENT_STAGE4D_06T_HIDE_CARD_STALE_PROMPT_REPLAY_AUDIT.md`
- `docs/CURRENT_STAGE4D_06T_HIDE_CARD_STALE_PROMPT_REPLAY_EVIDENCE.md`

## Validation

```bash
DOTNET_ROOT=${HOME}/.dotnet ${HOME}/.dotnet/dotnet test /Users/dinghaolin/MyProjects/riftbound-dotnet/Riftbound.slnx --no-restore --filter "FullyQualifiedName~HideCardStalePromptReplayAfterCardMovesToBaseRejectsWithoutMutation"
```

Result: passed `1/1`.

```bash
DOTNET_ROOT=${HOME}/.dotnet ${HOME}/.dotnet/dotnet test /Users/dinghaolin/MyProjects/riftbound-dotnet/Riftbound.slnx --no-restore --filter "FullyQualifiedName~HideCard|FullyQualifiedName~Standby|FullyQualifiedName~ActionPrompt|FullyQualifiedName~Prompt|FullyQualifiedName~MatchStateHasher"
```

Result: passed `315/315`.

```bash
DOTNET_ROOT=${HOME}/.dotnet ${HOME}/.dotnet/dotnet test /Users/dinghaolin/MyProjects/riftbound-dotnet/Riftbound.slnx --no-restore
```

Result: passed `5382/5382`.

```bash
git diff --check
rg -n '^(<<<<<<<|=======|>>>>>>>)' docs tests src
rg -n '[ \t]+$' docs/CURRENT_STAGE4D_05*.md docs/CURRENT_STAGE4D_06*.md
```

Result: passed. No whitespace warnings, conflict markers or trailing whitespace in the new 05J-06T evidence docs.

## Runtime And Protocol

Runtime changed: no.

Protocol shape changed: no.

Hidden-info leakage found: no.

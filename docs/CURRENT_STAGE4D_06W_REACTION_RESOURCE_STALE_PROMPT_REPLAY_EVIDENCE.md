# Stage 4D-06W Reaction Resource Stale Prompt Replay Evidence

Date: 2026-05-22

Project status: **NOT READY**

## Changed Files

- `tests/Riftbound.ConformanceTests/ReactionResourceSkillTests.cs`
- `tests/Riftbound.ConformanceTests/HoneyfruitResourceSkillTests.cs`
- `docs/CURRENT_A_MASTER_CHECKPOINT.md`
- `docs/CURRENT_COMPLETION_AUDIT.md`
- `docs/CURRENT_SERVER_RULE_AUDIT.md`
- `docs/CURRENT_STAGE4D_P0_P1_CLOSURE_PLAN.md`
- `docs/CURRENT_STAGE4D_NEXT_DISPATCH_AND_WRITELOCKS.md`
- `docs/CURRENT_STAGE4D_SHARED_COORDINATION_BOARD.md`
- `docs/CURRENT_STAGE4D_06W_REACTION_RESOURCE_STALE_PROMPT_REPLAY_AUDIT.md`
- `docs/CURRENT_STAGE4D_06W_REACTION_RESOURCE_STALE_PROMPT_REPLAY_EVIDENCE.md`

## Validation

```bash
DOTNET_ROOT=${HOME}/.dotnet ${HOME}/.dotnet/dotnet test /Users/dinghaolin/MyProjects/riftbound-dotnet/Riftbound.slnx --no-restore --filter "FullyQualifiedName~DragonSoulSageReactionResourceStalePromptReplayAfterManaGainRejectsWithoutMutation|FullyQualifiedName~HoneyfruitLevelSixResourceStalePromptReplayAfterTemporaryLedgerRejectsWithoutMutation"
```

Result: passed `2/2`.

```bash
DOTNET_ROOT=${HOME}/.dotnet ${HOME}/.dotnet/dotnet test /Users/dinghaolin/MyProjects/riftbound-dotnet/Riftbound.slnx --no-restore --filter "FullyQualifiedName~ReactionResourceSkillTests|FullyQualifiedName~HoneyfruitResourceSkillTests|FullyQualifiedName~ResourceSkill|FullyQualifiedName~PaymentEngine|FullyQualifiedName~ActionPrompt|FullyQualifiedName~Prompt|FullyQualifiedName~MatchStateHasher|FullyQualifiedName~ActivateAbility"
```

Result: passed `1245/1245`.

```bash
DOTNET_ROOT=${HOME}/.dotnet ${HOME}/.dotnet/dotnet test /Users/dinghaolin/MyProjects/riftbound-dotnet/Riftbound.slnx --no-restore
```

Result: passed `5396/5396`.

```bash
git diff --check
rg -n '^(<<<<<<<|=======|>>>>>>>)' docs tests src
rg -n '[ \t]+$' docs/CURRENT_STAGE4D_05*.md docs/CURRENT_STAGE4D_06*.md
```

Result: passed. No whitespace warnings, conflict markers, or trailing whitespace in the new 05J-06W evidence docs.

## Runtime And Protocol

Runtime changed: no.

Protocol shape changed: no.

Hidden-info leakage found: no.

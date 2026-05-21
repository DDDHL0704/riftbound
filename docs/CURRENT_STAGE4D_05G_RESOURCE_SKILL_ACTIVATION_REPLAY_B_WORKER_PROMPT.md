# 4D-05G B_SERVER Prompt: Resource Skill Activation Replay Guards

You are `B_SERVER` for the current Stage 4D A_MAIN flow.

## Objective

Add focused server-side conformance coverage for P0-005 PaymentEngine / resource-skill activation replay breadth.

Successful server-authored `ACTIVATE_ABILITY` resource-skill commands that resolve immediately and create payment-only temporary resources must accept once. Replaying the exact same `ActivateAbilityCommand` against the post-activation state must reject without mutation.

This is a narrow test slice. Do not broaden into matrix, frontend, protocol, official catalog, browser, Chrome, formal E2E or READY work.

## Required Coverage

Use existing helpers and state builders.

Cover at least these two representative paths:

1. `MalzaharResourceSkillTests`
   - successful Malzahar resource skill activation with a legal friendly unit or equipment cost target;
   - replay the exact same `ActivateAbilityCommand` against the post-activation state;
   - assert rejected, no events, exact state hash unchanged, source remains exhausted, cost object remains in graveyard, no duplicate `ABILITY_ACTIVATED` / `UNIT_EXHAUSTED` / removal / `POWER_GAINED`, no stack item appears, and exactly one temporary payment resource remains.

2. `GoldTokenResourceSkillTests`
   - successful UNL or SFD Gold token resource skill activation;
   - replay the exact same `ActivateAbilityCommand` against the post-activation state;
   - assert rejected, no events, exact state hash unchanged, Gold source remains destroyed / in graveyard, existing stack priority context is unchanged, no duplicate `ABILITY_ACTIVATED` / `UNIT_EXHAUSTED` / `EQUIPMENT_DESTROYED` / `MANA_GAINED` / `POWER_GAINED`, and exactly one temporary payment resource remains.

Prefer one representative method per file. Keep helpers local and small.

## Allowed Write Scope

You may modify only:

- `tests/Riftbound.ConformanceTests/MalzaharResourceSkillTests.cs`
- `tests/Riftbound.ConformanceTests/GoldTokenResourceSkillTests.cs`

Optional if and only if the focused tests expose a real runtime bug:

- `src/Riftbound.Engine/CoreRuleEngine.cs`

Optional focused docs only if you need them for a concise evidence note:

- `docs/CURRENT_STAGE4D_05G_RESOURCE_SKILL_ACTIVATION_REPLAY_AUDIT.md`
- `docs/CURRENT_STAGE4D_05G_RESOURCE_SKILL_ACTIVATION_REPLAY_EVIDENCE.md`

## Locked Scope

Do not modify:

- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- `docs/CURRENT_CARD_EFFECT_COVERAGE_BASELINE.md`
- `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`
- frontend files
- official catalog / source snapshot data
- protocol core fields
- Chrome / browser / formal E2E scripts
- `fullOfficial` / READY / READY-CANDIDATE state
- `riftbound-dotnet.sln`

You are not alone in the codebase. `DOC_MATRIX_CURRENT` is active in a separate worktree on matrix/current-doc/audit-test files. Do not touch its lane and do not revert edits made by others.

## Service Authority / Hidden Info Invariants

- Server remains the only rules authority.
- Tests must exercise server commands / authoritative state only.
- Do not add frontend-side legality logic.
- Do not expose hidden card identity, deck order, hidden random output, or internal hidden metadata in assertions / logs.
- Do not change protocol shape.

## Required Validation

Run focused validation:

```bash
source scripts/dev-env.sh && dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~MalzaharResourceSkillTests|FullyQualifiedName~GoldTokenResourceSkillTests"
```

Run adjacent regression:

```bash
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ResourceSkill|FullyQualifiedName~TemporaryPaymentResource|FullyQualifiedName~PaymentEngine|FullyQualifiedName~ActionPrompt|FullyQualifiedName~Prompt|FullyQualifiedName~GoldToken|FullyQualifiedName~Malzahar"
```

Always run:

```bash
git diff --check
```

If you modify runtime, also run:

```bash
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

## Final Report

Report:

- changed files;
- whether runtime changed;
- focused / adjacent / diff-check results;
- whether hidden-info leakage was found;
- whether protocol shape changed;
- any blocker.

Stage 4 remains **NOT READY**.

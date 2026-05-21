# 4D-05F B_SERVER Prompt: Trigger Payment Representative Replay Guards

You are `B_SERVER` for the current Stage 4D A_MAIN flow.

## Objective

Add focused server-side conformance coverage for P0-005 PaymentEngine / `TRIGGER_PAYMENT` representative replay breadth beyond the already-covered battlefield Gold and SFD Fiora windows.

Successful server-authored `TRIGGER_PAYMENT` commands must accept once, close or advance exactly once, and replaying the same `PayCostCommand` against the post-acceptance state must reject without mutation.

This is a narrow test slice. Do not broaden into matrix, frontend, protocol, official catalog, browser, Chrome, formal E2E or READY work.

## Required Coverage

Use existing helpers and state builders in `tests/Riftbound.ConformanceTests/TriggerPaymentTests.cs`.

Add focused tests for at least two of these representative paths, preferring three if the file already has convenient helpers:

- Sunken Temple Powerful `TRIGGER_PAYMENT` accepted payment replay.
- OGN Vayne conquer return `TRIGGER_PAYMENT` accepted payment replay.
- Jax weapon attach `TRIGGER_PAYMENT` accepted payment replay.
- Icevale Archer attack `TRIGGER_PAYMENT` accepted payment replay.

For each covered path:

- Open the existing server-authored `TRIGGER_PAYMENT` window.
- Capture the exact `PayCostCommand` that is legal in that window.
- Submit it once and assert the path's existing expected result.
- Capture `MatchStateHasher.Hash(...)` after acceptance.
- Replay the exact same command against the post-acceptance state.
- Assert replay is rejected, emits no events, preserves exact state hash, does not restore `PendingPayment`, does not duplicate `COST_PAID` / trigger-resolution / movement / draw / attach / damage / score / stack effects, and does not fork prompts or task state.

If one representative has a low-friction decline path already in the same section, you may also add a decline replay guard, but do not let that expand the slice beyond `TriggerPaymentTests.cs`.

## Allowed Write Scope

You may modify only:

- `tests/Riftbound.ConformanceTests/TriggerPaymentTests.cs`

Optional if and only if the focused tests expose a real runtime bug:

- `src/Riftbound.Engine/CoreRuleEngine.cs`

Optional focused docs only if you need them for a concise evidence note:

- `docs/CURRENT_STAGE4D_05F_TRIGGER_PAYMENT_REPRESENTATIVE_REPLAY_AUDIT.md`
- `docs/CURRENT_STAGE4D_05F_TRIGGER_PAYMENT_REPRESENTATIVE_REPLAY_EVIDENCE.md`

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
source scripts/dev-env.sh && dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~TriggerPaymentTests"
```

Run adjacent regression:

```bash
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~TriggerPayment|FullyQualifiedName~PaymentEngine|FullyQualifiedName~ActionPrompt|FullyQualifiedName~Prompt|FullyQualifiedName~BattleDamageAssignmentLifecycle"
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

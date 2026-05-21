# 4D-05E B_SERVER Prompt: Keyword Optional `PLAY_CARD` Replay Guards

You are `B_SERVER` for the current Stage 4D A_MAIN flow.

## Objective

Add focused server-side conformance coverage for P0-005 PaymentEngine / keyword optional payment branches:

- successful keyword optional `PLAY_CARD` commands with Haste / Tempered / typed recycle-resource costs accept once;
- replaying the same `PlayCardCommand` against the post-acceptance state rejects;
- replay rejection produces no events and does not mutate state.

This is a narrow test slice. Do not broaden into matrix, frontend, protocol, official catalog or READY work.

## Allowed Write Scope

You may modify only:

- `tests/Riftbound.ConformanceTests/ReksaiHasteReadyRedPaymentTests.cs`
- `tests/Riftbound.ConformanceTests/ArmedAssaulterHasteTemperedTests.cs`

Optional if and only if the focused tests expose a real runtime bug:

- `src/Riftbound.Engine/CoreRuleEngine.cs`
- the smallest local helper in the same test files

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

## Required Coverage

Add focused tests that cover at least these two representative paths:

1. `ReksaiHasteReadyRedPaymentTests`
   - successful `PLAY_CARD` with `HASTE_READY` and typed red power, preferably including the existing red-rune recycle payment-resource path;
   - replay the exact same `PlayCardCommand` against the post-acceptance state;
   - assert rejected, `Events` empty, `MatchStateHasher.Hash` unchanged, no second `CARD_PLAYED` / `RUNE_RECYCLED` / `POWER_GAINED` / `COST_PAID` / `STACK_ITEM_ADDED`, rune pool / rune zones / hand / stack unchanged, and no prompt/state fork.

2. `ArmedAssaulterHasteTemperedTests`
   - successful `PLAY_CARD` with both `HASTE_READY` and `TEMPERED_ATTACH:<equipmentObjectId>`;
   - replay the exact same `PlayCardCommand` against the post-acceptance state;
   - assert rejected, `Events` empty, `MatchStateHasher.Hash` unchanged, no second `CARD_PLAYED` / `COST_PAID` / `STACK_ITEM_ADDED`, no second mana/power spend, hand/base/stack/equipment attachment state unchanged. The equipment should still only be represented as the original pending stack optional cost before stack resolution.

Use existing helper style in each file. If you add helpers, keep them local and small.

## Service Authority / Hidden Info Invariants

- Server remains the only rules authority.
- Tests must exercise server commands / authoritative state only.
- Do not add frontend-side legality logic.
- Do not expose hidden card identity, deck order, hidden random output, or internal hidden metadata in assertions / logs.
- Do not change protocol shape.

## Required Validation

Run:

```bash
source scripts/dev-env.sh && dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~ReksaiHasteReadyRedPaymentTests|FullyQualifiedName~ArmedAssaulterHasteTemperedTests"
```

Run adjacent regression:

```bash
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Reksai|FullyQualifiedName~ArmedAssaulter|FullyQualifiedName~Haste|FullyQualifiedName~Tempered|FullyQualifiedName~PaymentEngine|FullyQualifiedName~ActionPrompt|FullyQualifiedName~Prompt"
```

Run:

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

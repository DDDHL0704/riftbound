# Stage 4D-13H Prompt Id Only Mulligan Command After First Turn Evidence

Status: accepted.

## Code

- `tests/Riftbound.ConformanceTests/OfficialOpeningTests.cs`
  - Added `PromptIdOnlyFinalMulliganCommandAfterFirstTurnRejectsWithoutMutation`.
  - Added `PromptIdOnlyFirstMulliganCommandAfterFirstTurnRejectsWithoutMutation`.
  - Added `PromptIdOnlyMulliganRawCommand`.

## Focused Validation

```text
/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PromptIdOnlyFinalMulliganCommandAfterFirstTurnRejectsWithoutMutation|FullyQualifiedName~PromptIdOnlyFirstMulliganCommandAfterFirstTurnRejectsWithoutMutation"
```

Result: passed `2/2`.

## Adjacent Validation

```text
/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~OfficialOpeningTests|FullyQualifiedName~ConformanceFixtureShapeTests|FullyQualifiedName~Prompt|FullyQualifiedName~MatchStateHasher|FullyQualifiedName~GameHubJoinTests"
```

Result: passed `761/761`.

## Backend Full Validation

```text
/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --no-restore
```

Result: passed `5717/5717`.

## Mechanical Validation

```text
git diff --check
rg -n '^(<<<<<<<|=======|>>>>>>>)' docs tests src
rg -n '[ \t]+$' docs/CURRENT_STAGE4D_{05,06,07,08,09,10,11,12,13}*.md
```

Result: passed. `git diff --check` produced no output; conflict-marker scan over `docs`, `tests` and `src` found no matches; trailing-whitespace scan for the 05J through 13H evidence/docs set found no matches.

## DOC Matrix Guard

`DOC_MATRIX_CURRENT` remains clean at `4c999922`; A_MAIN did not integrate or reject that source commit because main still has active 05J-13H dirty runtime/test/docs slices.

Project remains **NOT READY**.

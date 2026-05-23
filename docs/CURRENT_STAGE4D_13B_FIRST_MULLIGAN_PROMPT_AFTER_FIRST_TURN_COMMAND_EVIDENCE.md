# Stage 4D-13B First Mulligan Prompt After First Turn Command Evidence

Status: accepted.

## Code

- `tests/Riftbound.ConformanceTests/OfficialOpeningTests.cs`
  - Added `RoomCommandsWithFirstMulliganPromptAfterFirstTurnRejectWithoutMutation`.
  - Added theory `SubmitCommandWithFirstMulliganPromptAfterFirstTurnRejectsWithoutMutation`.
  - Added shared helper `AssertSubmitCommandWithFirstMulliganPromptAfterFirstTurnRejectsWithoutMutation`.
  - Added shared setup helper `BuildFirstMulliganFirstTurnAuditContext`.
  - Added shared assertion helper `AssertFirstMulliganPromptAfterFirstTurnRejection`.
  - Renamed the shared command factory to `CreateMulliganPromptAfterFirstTurnCommand` and reused it for first and final consumed mulligan prompts.

## Focused Validation

```text
/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~RoomCommandsWithFirstMulliganPromptAfterFirstTurnRejectWithoutMutation|FullyQualifiedName~SubmitCommandWithFirstMulliganPromptAfterFirstTurnRejectsWithoutMutation|FullyQualifiedName~OfficialFirstMulliganStalePromptReplayAfterFirstTurnStartsRejectsWithoutMutation|FullyQualifiedName~RoomCommandsWithFinalMulliganPromptAfterFirstTurnRejectWithoutMutation|FullyQualifiedName~SubmitCommandWithFinalMulliganPromptAfterFirstTurnRejectsWithoutMutation"
```

Result: passed `41/41`.

## Adjacent Validation

```text
/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~OfficialOpeningTests|FullyQualifiedName~ConformanceFixtureShapeTests|FullyQualifiedName~Prompt|FullyQualifiedName~MatchStateHasher|FullyQualifiedName~GameHubJoinTests"
```

Result: passed `559/559`.

## Backend Full Validation

```text
/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --no-restore
```

Result: passed `5515/5515`.

## Mechanical Validation

```text
git diff --check
rg -n '^(<<<<<<<|=======|>>>>>>>)' docs tests src
rg -n '[ \t]+$' docs/CURRENT_STAGE4D_{05,06,07,08,09,10,11,12,13}*.md
```

Result: passed. `git diff --check` produced no output; conflict-marker scan over `docs`, `tests` and `src` found no matches; trailing-whitespace scan for the 05J through 13B evidence/docs set found no matches.

## DOC Matrix Guard

`DOC_MATRIX_CURRENT` remains clean at `4c999922`; A_MAIN did not integrate or reject that source commit because main still has active 05J-13B dirty runtime/test/docs slices.

Project remains **NOT READY**.

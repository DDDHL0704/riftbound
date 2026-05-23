# Stage 4D-13F Prompt Id Only Mulligan Prompt After First Turn Command Evidence

Status: accepted.

## Code

- `tests/Riftbound.ConformanceTests/OfficialOpeningTests.cs`
  - Added `PromptIdOnlyRoomCommandsWithFinalMulliganPromptAfterFirstTurnRejectWithoutMutation`.
  - Added theory `SubmitCommandWithFinalMulliganPromptIdAfterFirstTurnRejectsWithoutMutation`.
  - Added helper `AssertSubmitCommandWithFinalMulliganPromptIdAfterFirstTurnRejectsWithoutMutation`.
  - Added `PromptIdOnlyRoomCommandsWithFirstMulliganPromptAfterFirstTurnRejectWithoutMutation`.
  - Added theory `SubmitCommandWithFirstMulliganPromptIdAfterFirstTurnRejectsWithoutMutation`.
  - Added helper `AssertSubmitCommandWithFirstMulliganPromptIdAfterFirstTurnRejectsWithoutMutation`.
  - Added prompt-id-only raw command helpers for `READY`, `SUBMIT_DECK` and generic submit-command payloads.

## Focused Validation

```text
/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PromptIdOnlyRoomCommandsWithFinalMulliganPromptAfterFirstTurnRejectWithoutMutation|FullyQualifiedName~SubmitCommandWithFinalMulliganPromptIdAfterFirstTurnRejectsWithoutMutation|FullyQualifiedName~PromptIdOnlyRoomCommandsWithFirstMulliganPromptAfterFirstTurnRejectWithoutMutation|FullyQualifiedName~SubmitCommandWithFirstMulliganPromptIdAfterFirstTurnRejectsWithoutMutation"
```

Result: passed `40/40`.

## Adjacent Validation

```text
/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~OfficialOpeningTests|FullyQualifiedName~ConformanceFixtureShapeTests|FullyQualifiedName~Prompt|FullyQualifiedName~MatchStateHasher|FullyQualifiedName~GameHubJoinTests"
```

Result: passed `719/719`.

## Backend Full Validation

```text
/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --no-restore
```

Result: passed `5675/5675`.

## Mechanical Validation

```text
git diff --check
rg -n '^(<<<<<<<|=======|>>>>>>>)' docs tests src
rg -n '[ \t]+$' docs/CURRENT_STAGE4D_{05,06,07,08,09,10,11,12,13}*.md
```

Result: passed. `git diff --check` produced no output; conflict-marker scan over `docs`, `tests` and `src` found no matches; trailing-whitespace scan for the 05J through 13F evidence/docs set found no matches.

## DOC Matrix Guard

`DOC_MATRIX_CURRENT` remains clean at `4c999922`; A_MAIN did not integrate or reject that source commit because main still has active 05J-13F dirty runtime/test/docs slices.

Project remains **NOT READY**.

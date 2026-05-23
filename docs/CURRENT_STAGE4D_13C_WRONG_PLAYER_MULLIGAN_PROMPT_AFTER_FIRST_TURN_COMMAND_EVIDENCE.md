# Stage 4D-13C Wrong Player Mulligan Prompt After First Turn Command Evidence

Status: accepted.

## Code

- `tests/Riftbound.ConformanceTests/OfficialOpeningTests.cs`
  - Added `WrongPlayerRoomCommandsWithFinalMulliganPromptAfterFirstTurnRejectWithoutMutation`.
  - Added theory `SubmitCommandWithFinalMulliganPromptAfterFirstTurnWrongPlayerRejectsWithoutMutation`.
  - Added helper `AssertSubmitCommandWithFinalMulliganPromptAfterFirstTurnWrongPlayerRejectsWithoutMutation`.
  - Added `WrongPlayerRoomCommandsWithFirstMulliganPromptAfterFirstTurnRejectWithoutMutation`.
  - Added theory `SubmitCommandWithFirstMulliganPromptAfterFirstTurnWrongPlayerRejectsWithoutMutation`.
  - Added helper `AssertSubmitCommandWithFirstMulliganPromptAfterFirstTurnWrongPlayerRejectsWithoutMutation`.
  - Extended first/final first-turn audit contexts with player-specific decklist selection so room `SUBMIT_DECK` wrong-player checks do not rely on fixed seating assumptions.

## Focused Validation

```text
/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~WrongPlayerRoomCommandsWithFinalMulliganPromptAfterFirstTurnRejectWithoutMutation|FullyQualifiedName~SubmitCommandWithFinalMulliganPromptAfterFirstTurnWrongPlayerRejectsWithoutMutation|FullyQualifiedName~WrongPlayerRoomCommandsWithFirstMulliganPromptAfterFirstTurnRejectWithoutMutation|FullyQualifiedName~SubmitCommandWithFirstMulliganPromptAfterFirstTurnWrongPlayerRejectsWithoutMutation|FullyQualifiedName~RoomCommandsWithFirstMulliganPromptAfterFirstTurnRejectWithoutMutation|FullyQualifiedName~SubmitCommandWithFirstMulliganPromptAfterFirstTurnRejectsWithoutMutation|FullyQualifiedName~RoomCommandsWithFinalMulliganPromptAfterFirstTurnRejectWithoutMutation|FullyQualifiedName~SubmitCommandWithFinalMulliganPromptAfterFirstTurnRejectsWithoutMutation"
```

Result: passed `80/80`.

## Adjacent Validation

```text
/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~OfficialOpeningTests|FullyQualifiedName~ConformanceFixtureShapeTests|FullyQualifiedName~Prompt|FullyQualifiedName~MatchStateHasher|FullyQualifiedName~GameHubJoinTests"
```

Result: passed `599/599`.

## Backend Full Validation

```text
/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --no-restore
```

Result: passed `5555/5555`.

## Mechanical Validation

```text
git diff --check
rg -n '^(<<<<<<<|=======|>>>>>>>)' docs tests src
rg -n '[ \t]+$' docs/CURRENT_STAGE4D_{05,06,07,08,09,10,11,12,13}*.md
```

Result: passed. `git diff --check` produced no output; conflict-marker scan over `docs`, `tests` and `src` found no matches; trailing-whitespace scan for the 05J through 13C evidence/docs set found no matches.

## DOC Matrix Guard

`DOC_MATRIX_CURRENT` remains clean at `4c999922`; A_MAIN did not integrate or reject that source commit because main still has active 05J-13C dirty runtime/test/docs slices.

Project remains **NOT READY**.

# Stage 4D-15P Wrong Player First Turn Tap Rune Prompt Evidence

Status: accepted.

## Code

- `tests/Riftbound.ConformanceTests/OfficialOpeningTests.cs`
  - Added wrong-player `TAP_RUNE` coverage using the active player's current first-turn `MAIN_ACTION` prompt id and snapshot envelope forms.

## Focused Validation

```text
/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~WrongPlayerFirstTurnTapRunePrompt"
```

Result: passed `3/3`.

## Adjacent Validation

```text
/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~OfficialOpeningTests|FullyQualifiedName~ConformanceFixtureShapeTests|FullyQualifiedName~Prompt|FullyQualifiedName~MatchStateHasher|FullyQualifiedName~GameHubJoinTests"
```

Result: passed `937/937`.

## Backend Full Validation

```text
/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --no-restore
```

Result: passed `5893/5893`.

## Mechanical Validation

```text
git diff --check
rg -n '^(<<<<<<<|=======|>>>>>>>)' docs tests src
rg -n '[ \t]+$' docs/CURRENT_STAGE4D_{05,06,07,08,09,10,11,12,13,14,15}*.md
```

Result: passed. `git diff --check` produced no output; conflict-marker scan over `docs`, `tests` and `src` found no matches; trailing-whitespace scan for the 05J through 15P evidence/docs set found no matches.

## DOC Matrix Guard

`DOC_MATRIX_CURRENT` remains clean at `4c999922`; A_MAIN did not integrate or reject that source commit because main still has active 05J-15P dirty runtime/test/docs slices.

Project remains **NOT READY**.

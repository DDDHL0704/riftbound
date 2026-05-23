# Stage 4D-15Z Wrong Player First Turn Ready Prompt Evidence

Status: accepted.

## Code

- `tests/Riftbound.ConformanceTests/OfficialOpeningTests.cs`
  - Added wrong-player `READY` coverage using the active player's current first-turn `MAIN_ACTION` prompt id and snapshot envelope forms.

## Focused Validation

```text
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~WrongPlayerFirstTurnReadyPrompt"
```

Result: passed `3/3`.

## Adjacent Validation

```text
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~OfficialOpeningTests|FullyQualifiedName~ConformanceFixtureShapeTests|FullyQualifiedName~Prompt|FullyQualifiedName~Hash|FullyQualifiedName~GameHub"
```

Result: passed `971/971`.

## Backend Full Validation

```text
/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --no-restore
```

Result: passed `5923/5923`.

## Mechanical Validation

```text
git diff --check
rg -n '^(<<<<<<<|=======|>>>>>>>)' docs tests src
rg -n '[ \t]+$' docs/CURRENT_STAGE4D_{05,06,07,08,09,10,11,12,13,14,15}*.md
```

Result: passed. `git diff --check` produced no output; conflict-marker scan over `docs`, `tests` and `src` found no matches; trailing-whitespace scan for the 05J through 15Z evidence/docs set found no matches.

## DOC Matrix Guard

`DOC_MATRIX_CURRENT` remains clean at `4c999922`; A_MAIN did not integrate or reject that source commit because main still has active 05J-15Z dirty runtime/test/docs slices.

Project remains **NOT READY**.

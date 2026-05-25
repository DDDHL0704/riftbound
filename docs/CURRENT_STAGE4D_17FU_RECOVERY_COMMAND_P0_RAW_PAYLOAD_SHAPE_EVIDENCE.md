# Stage 4D-17FU Recovery Command P0 Raw Payload Shape Evidence

Date: 2026-05-25

Status: accepted. Project remains **NOT READY**.

Changed files:

- `src/Riftbound.Engine/MatchRecovery.cs`
- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`
- current Stage 4D checkpoint / completion / P0-P1 / next-dispatch docs
- `docs/CURRENT_STAGE4D_SHARED_COORDINATION_BOARD.md`
- this evidence doc and the paired audit doc

Focused validation:

```text
export DOTNET_ROOT="$HOME/.dotnet"; export PATH="$DOTNET_ROOT:$PATH"
dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~RecoveryValidatorRejectsP0PayloadCommandRawPayloadShapeDrift"
```

Result: passed `1/1`.

Focused recovery validation:

```text
export DOTNET_ROOT="$HOME/.dotnet"; export PATH="$DOTNET_ROOT:$PATH"
dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~MatchRecoveryTests"
```

Result: passed `195/195`.

Adjacent validation:

```text
export DOTNET_ROOT="$HOME/.dotnet"; export PATH="$DOTNET_ROOT:$PATH"
dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~MatchRecoveryTests|FullyQualifiedName~OfficialOpeningTests|FullyQualifiedName~PostgresMatchRecoveryStoreSmokeTests"
```

Result: passed `776/776`.

Backend full validation:

```text
export DOTNET_ROOT="$HOME/.dotnet"; export PATH="$DOTNET_ROOT:$PATH"
dotnet test Riftbound.slnx --no-restore
```

Result: passed `6141/6141`.

Mechanical validation:

```text
git diff --check
rg -n '^(<<<<<<<|=======|>>>>>>>)' docs src tests
jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json
```

Result: all passed before checkpoint commit.

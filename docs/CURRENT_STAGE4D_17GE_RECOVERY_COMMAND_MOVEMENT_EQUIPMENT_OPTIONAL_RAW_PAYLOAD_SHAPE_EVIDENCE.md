# Stage 4D-17GE Recovery Command Movement Equipment Optional Raw Payload Shape Evidence

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
DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:/opt/homebrew/opt/node@24/bin:/opt/homebrew/opt/postgresql@16/bin:$PATH dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~RecoveryValidatorRejectsMovementAndEquipmentOptionalCommandRawPayloadShapeDrift"
```

Result: passed `1/1`.

Focused recovery validation:

```text
DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:/opt/homebrew/opt/node@24/bin:/opt/homebrew/opt/postgresql@16/bin:$PATH dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~MatchRecoveryTests"
```

Result: passed `205/205`.

Adjacent validation:

```text
DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:/opt/homebrew/opt/node@24/bin:/opt/homebrew/opt/postgresql@16/bin:$PATH dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~MatchRecoveryTests|FullyQualifiedName~OfficialOpeningTests|FullyQualifiedName~PostgresMatchRecoveryStoreSmokeTests"
```

Result: passed `786/786`.

Backend full validation:

```text
DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:/opt/homebrew/opt/node@24/bin:/opt/homebrew/opt/postgresql@16/bin:$PATH dotnet test Riftbound.slnx --no-restore
```

Result: passed `6151/6151`.

Environment note:

- `~/.dotnet/dotnet` SDK `10.0.100` was already installed. `scripts/dev-env.sh` exits early on this machine because local `psql` / `redis-cli` are not in `PATH`, so tests used explicit `DOTNET_ROOT` / `PATH` instead.

Mechanical validation:

```text
git diff --check
rg -n '^(<<<<<<<|=======|>>>>>>>)' docs src tests
jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json
```

Result: all passed before checkpoint commit.

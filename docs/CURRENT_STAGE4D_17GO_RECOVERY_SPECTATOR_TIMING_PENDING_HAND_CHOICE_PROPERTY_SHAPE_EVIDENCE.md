# Stage 4D-17GO Recovery Spectator Timing Pending Hand Choice Property Shape Evidence

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
DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:/opt/homebrew/opt/node@24/bin:/opt/homebrew/opt/postgresql@16/bin:$PATH dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingPendingHandChoicePropertyNameDrift"
```

Result: passed `1/1`.

Focused recovery validation:

```text
DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:/opt/homebrew/opt/node@24/bin:/opt/homebrew/opt/postgresql@16/bin:$PATH dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~MatchRecoveryTests"
```

Result: passed `216/216`.

Adjacent validation:

```text
DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:/opt/homebrew/opt/node@24/bin:/opt/homebrew/opt/postgresql@16/bin:$PATH dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~MatchRecoveryTests|FullyQualifiedName~OfficialOpeningTests|FullyQualifiedName~PostgresMatchRecoveryStoreSmokeTests"
```

Result: passed `797/797`.

Backend full validation:

```text
DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:/opt/homebrew/opt/node@24/bin:/opt/homebrew/opt/postgresql@16/bin:$PATH dotnet test Riftbound.slnx --no-restore
```

Result: passed `6162/6162`.

Environment note:

- `~/.dotnet/dotnet` SDK `10.0.100` was already installed. Tests used explicit `DOTNET_ROOT` / `PATH`.

Mechanical validation:

```text
git diff --check
rg -n '^(<<<<<<<|=======|>>>>>>>)' docs src tests
jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json
```

Result: all passed before checkpoint commit. The conflict-marker scan returned no matches.

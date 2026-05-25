# Stage 4D-17FQ Recovery Spectator Continuous Effect Validation Evidence

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
dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectsMismatch"
```

Result: passed `1/1`.

Focused recovery validation:

```text
export DOTNET_ROOT="$HOME/.dotnet"; export PATH="$DOTNET_ROOT:$PATH"
dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~MatchRecoveryTests"
```

Result: passed `192/192`.

Adjacent validation:

```text
export DOTNET_ROOT="$HOME/.dotnet"; export PATH="$DOTNET_ROOT:$PATH"
dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~MatchRecoveryTests|FullyQualifiedName~OfficialOpeningTests|FullyQualifiedName~PostgresMatchRecoveryStoreSmokeTests"
```

Result: passed `773/773`.

Backend full validation:

```text
export DOTNET_ROOT="$HOME/.dotnet"; export PATH="$DOTNET_ROOT:$PATH"
dotnet test Riftbound.slnx --no-restore
```

Result: passed `6138/6138`.

Mechanical validation:

```text
git diff --check
rg -n '^(<<<<<<<|=======|>>>>>>>)' src tests docs --glob '!src/Riftbound.DevUi/node_modules/**'
jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json
```

Result: all passed before checkpoint commit.

Environment note: `~/.dotnet` was absent before this slice, so SDK `10.0.100` was installed with `dotnet-install.sh`. `scripts/dev-env.sh` could not be used as-is because local `psql` / `redis-cli` were not installed; this did not block backend validation.

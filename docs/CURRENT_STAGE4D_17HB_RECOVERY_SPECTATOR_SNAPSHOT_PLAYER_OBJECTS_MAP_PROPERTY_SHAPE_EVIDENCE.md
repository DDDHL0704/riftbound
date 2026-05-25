# Stage 4D-17HB Recovery Spectator Snapshot Player Objects Map Property Shape Evidence

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
DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplaySnapshotPlayerObjectsMapPropertyNameDrift
```

Result: passed `1/1`.

Focused recovery validation:

```text
DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~MatchRecoveryTests"
```

Result: passed `229/229`.

Adjacent validation:

```text
DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~MatchRecoveryTests|FullyQualifiedName~PostgresMatchRecoveryStoreSmoke|FullyQualifiedName~OfficialOpening"
```

Result: passed `810/810`.

Backend full validation:

```text
DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH dotnet test Riftbound.slnx --no-restore
```

Result: passed `6175/6175`.

Environment note:

- `~/.dotnet/dotnet` SDK `10.0.100` was already installed. Tests used explicit `DOTNET_ROOT` / `PATH` because `scripts/dev-env.sh` requires local `psql` / `redis-cli` commands that were not present in this shell.

Mechanical validation:

```text
git diff --check
rg -n '^(<<<<<<<|=======|>>>>>>>)' docs src tests --glob '!src/Riftbound.DevUi/node_modules/**'
jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json
```

Result: all passed before checkpoint commit. The anchored conflict-marker scan returned no matches.

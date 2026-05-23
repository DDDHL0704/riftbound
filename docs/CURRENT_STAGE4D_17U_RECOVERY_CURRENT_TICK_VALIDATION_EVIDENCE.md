2026-05-24 Stage 4D-17U recovery current tick validation evidence.

Changed files:
- `src/Riftbound.Engine/MatchRecovery.cs`
- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`

Focused command:

```bash
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~MatchRecoveryTests"
```

Focused result: passed `47/47`.

Adjacent command:

```bash
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~MatchRecovery|FullyQualifiedName~PostgresMatchRecoveryStoreSmoke|FullyQualifiedName~OfficialOpening"
```

Adjacent result: passed `628/628`.

Backend full command:

```bash
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

Backend full result: passed `5993/5993`.

Mechanical checks:
- `git diff --check`: passed.
- `rg -n "^(<<<<<<<|=======|>>>>>>>)" docs tests src`: no matches.
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`: passed.

Continuity:
- No frontend, protocol, official catalog, matrix JSON, browser/Chrome/formal E2E script, `fullOfficial`, READY / READY-CANDIDATE or `riftbound-dotnet.sln` change.
- Project remains **NOT READY**.

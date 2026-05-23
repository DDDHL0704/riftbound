2026-05-24 Stage 4D-17P recovery raw command shape validation evidence.

Changed files:
- `src/Riftbound.Engine/MatchRecovery.cs`
- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`

Focused command:

```bash
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~MatchRecoveryTests"
```

Focused result: passed `42/42`.

Adjacent command:

```bash
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~MatchRecovery|FullyQualifiedName~PostgresMatchRecoveryStoreSmoke|FullyQualifiedName~OfficialOpening"
```

Adjacent result: passed `623/623`.

Backend full command:

```bash
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

Backend full result: passed `5988/5988`.

Mechanical checks:
- `git diff --check`: passed.
- `rg -n "^(<<<<<<<|=======|>>>>>>>)" docs tests src`: no matches.
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`: passed.

Continuity:
- No frontend, protocol, official catalog, matrix JSON, browser/Chrome/formal E2E script, `fullOfficial`, READY / READY-CANDIDATE or `riftbound-dotnet.sln` change.
- Project remains **NOT READY**.

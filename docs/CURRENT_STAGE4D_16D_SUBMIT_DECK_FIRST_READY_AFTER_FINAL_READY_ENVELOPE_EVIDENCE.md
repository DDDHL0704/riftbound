2026-05-24 Stage 4D-16D evidence.

Changed file:

- `tests/Riftbound.ConformanceTests/OfficialOpeningTests.cs`

Focused command:

```bash
source scripts/dev-env.sh && dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~PromptIdOnlySubmitDeckWithFirstReadyPromptAfterFinalReadyRejectsWithoutMutation|FullyQualifiedName~SnapshotOnlySubmitDeckWithFirstReadyPromptAfterFinalReadyRejectsWithoutMutation"
```

Focused result: passed `2/2`.

Adjacent command:

```bash
source scripts/dev-env.sh && dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~OfficialOpeningTests|FullyQualifiedName~ConformanceFixtureShapeTests|FullyQualifiedName~Prompt|FullyQualifiedName~Hash|FullyQualifiedName~GameHub"
```

Adjacent result: passed `981/981`.

Backend full command:

```bash
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

Backend full result: passed `5933/5933`.

Continuity:

- No runtime, frontend, protocol, official catalog, matrix JSON, Chrome/browser/formal E2E script, `fullOfficial`, READY / READY-CANDIDATE or `riftbound-dotnet.sln` change.
- DOC_MATRIX approved scope remains `4D-03SU..03SV`; this A_MAIN slice does not widen DOC writes.
- Project remains **NOT READY**.

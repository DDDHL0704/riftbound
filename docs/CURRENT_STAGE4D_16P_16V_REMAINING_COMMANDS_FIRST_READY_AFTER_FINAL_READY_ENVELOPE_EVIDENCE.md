2026-05-24 Stage 4D-16P..16V evidence.

Changed file:

- `tests/Riftbound.ConformanceTests/OfficialOpeningTests.cs`

Focused command:

```bash
source scripts/dev-env.sh && dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~PromptIdOnlyRevealCardWithFirstReadyPromptAfterFinalReadyRejectsWithoutMutation|FullyQualifiedName~SnapshotOnlyRevealCardWithFirstReadyPromptAfterFinalReadyRejectsWithoutMutation|FullyQualifiedName~PromptIdOnlyLegendActWithFirstReadyPromptAfterFinalReadyRejectsWithoutMutation|FullyQualifiedName~SnapshotOnlyLegendActWithFirstReadyPromptAfterFinalReadyRejectsWithoutMutation|FullyQualifiedName~PromptIdOnlyAssembleEquipmentWithFirstReadyPromptAfterFinalReadyRejectsWithoutMutation|FullyQualifiedName~SnapshotOnlyAssembleEquipmentWithFirstReadyPromptAfterFinalReadyRejectsWithoutMutation|FullyQualifiedName~PromptIdOnlyPayCostWithFirstReadyPromptAfterFinalReadyRejectsWithoutMutation|FullyQualifiedName~SnapshotOnlyPayCostWithFirstReadyPromptAfterFinalReadyRejectsWithoutMutation|FullyQualifiedName~PromptIdOnlyAssignCombatDamageWithFirstReadyPromptAfterFinalReadyRejectsWithoutMutation|FullyQualifiedName~SnapshotOnlyAssignCombatDamageWithFirstReadyPromptAfterFinalReadyRejectsWithoutMutation|FullyQualifiedName~PromptIdOnlyOrderTriggersWithFirstReadyPromptAfterFinalReadyRejectsWithoutMutation|FullyQualifiedName~SnapshotOnlyOrderTriggersWithFirstReadyPromptAfterFinalReadyRejectsWithoutMutation|FullyQualifiedName~PromptIdOnlyChooseHandCardsWithFirstReadyPromptAfterFinalReadyRejectsWithoutMutation|FullyQualifiedName~SnapshotOnlyChooseHandCardsWithFirstReadyPromptAfterFinalReadyRejectsWithoutMutation"
```

Focused result: passed `14/14`.

Adjacent command:

```bash
source scripts/dev-env.sh && dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~OfficialOpeningTests|FullyQualifiedName~ConformanceFixtureShapeTests|FullyQualifiedName~Prompt|FullyQualifiedName~Hash|FullyQualifiedName~GameHub"
```

Adjacent result: passed `1017/1017`.

Backend full command:

```bash
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

Backend full result: passed `5969/5969`.

Mechanical checks:

- `git diff --check`: passed.
- Conflict-marker scan over `docs`, `tests` and `src`: no matches.
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`: passed.

Continuity:

- No runtime, frontend, protocol, official catalog, matrix JSON, Chrome/browser/formal E2E script, `fullOfficial`, READY / READY-CANDIDATE or `riftbound-dotnet.sln` change.
- DOC_MATRIX approved scope remains `4D-03SU..03SV`; this A_MAIN slice does not widen DOC writes.
- Project remains **NOT READY**.

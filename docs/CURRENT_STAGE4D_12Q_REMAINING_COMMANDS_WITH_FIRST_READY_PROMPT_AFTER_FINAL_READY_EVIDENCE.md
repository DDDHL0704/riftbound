# Stage 4D-12Q Remaining Commands With First Ready Prompt After Final Ready Evidence

Status: accepted.

## Code

- `tests/Riftbound.ConformanceTests/OfficialOpeningTests.cs`
  - Added `LegendActWithFirstReadyPromptAfterFinalReadyRejectsWithoutMutation`.
  - Added `AssembleEquipmentWithFirstReadyPromptAfterFinalReadyRejectsWithoutMutation`.
  - Added `PayCostWithFirstReadyPromptAfterFinalReadyRejectsWithoutMutation`.
  - Added `AssignCombatDamageWithFirstReadyPromptAfterFinalReadyRejectsWithoutMutation`.
  - Added `OrderTriggersWithFirstReadyPromptAfterFinalReadyRejectsWithoutMutation`.
  - Added `ChooseHandCardsWithFirstReadyPromptAfterFinalReadyRejectsWithoutMutation`.
  - Added shared helper `AssertCommandWithFirstReadyPromptAfterFinalReadyRejectsWithoutMutation`.

## Focused Validation

```text
/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~LegendActWithFirstReadyPromptAfterFinalReadyRejectsWithoutMutation|FullyQualifiedName~AssembleEquipmentWithFirstReadyPromptAfterFinalReadyRejectsWithoutMutation|FullyQualifiedName~PayCostWithFirstReadyPromptAfterFinalReadyRejectsWithoutMutation|FullyQualifiedName~AssignCombatDamageWithFirstReadyPromptAfterFinalReadyRejectsWithoutMutation|FullyQualifiedName~OrderTriggersWithFirstReadyPromptAfterFinalReadyRejectsWithoutMutation|FullyQualifiedName~ChooseHandCardsWithFirstReadyPromptAfterFinalReadyRejectsWithoutMutation|FullyQualifiedName~RevealCardWithFirstReadyPromptAfterFinalReadyRejectsWithoutMutation|FullyQualifiedName~HideCardWithFirstReadyPromptAfterFinalReadyRejectsWithoutMutation|FullyQualifiedName~RecycleRuneWithFirstReadyPromptAfterFinalReadyRejectsWithoutMutation|FullyQualifiedName~TapRuneWithFirstReadyPromptAfterFinalReadyRejectsWithoutMutation|FullyQualifiedName~ActivateAbilityWithFirstReadyPromptAfterFinalReadyRejectsWithoutMutation|FullyQualifiedName~PlayCardWithFirstReadyPromptAfterFinalReadyRejectsWithoutMutation|FullyQualifiedName~DeclareBattleWithFirstReadyPromptAfterFinalReadyRejectsWithoutMutation|FullyQualifiedName~MoveUnitWithFirstReadyPromptAfterFinalReadyRejectsWithoutMutation|FullyQualifiedName~PassFocusWithFirstReadyPromptAfterFinalReadyRejectsWithoutMutation|FullyQualifiedName~PassPriorityWithFirstReadyPromptAfterFinalReadyRejectsWithoutMutation|FullyQualifiedName~SurrenderWithFirstReadyPromptAfterFinalReadyRejectsWithoutMutation|FullyQualifiedName~EndTurnWithFirstReadyPromptAfterFinalReadyRejectsWithoutMutation|FullyQualifiedName~PassWithFirstReadyPromptAfterFinalReadyRejectsWithoutMutation|FullyQualifiedName~WrongPlayerMulliganWithFirstReadyPromptAfterFinalReadyRejectsWithoutMutation|FullyQualifiedName~MulliganWithFirstReadyPromptAfterFinalReadyRejectsWithoutMutation"
```

Result: passed `21/21`.

## Adjacent Validation

```text
/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~OfficialOpeningTests|FullyQualifiedName~ConformanceFixtureShapeTests|FullyQualifiedName~Prompt|FullyQualifiedName~MatchStateHasher|FullyQualifiedName~GameHubJoinTests"
```

Result: passed `519/519`.

## Backend Full Validation

```text
/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --no-restore
```

Result: passed `5475/5475`.

## Mechanical Validation

```text
git diff --check
rg -n '^(<<<<<<<|=======|>>>>>>>)' docs tests src
rg -n '[ \t]+$' docs/CURRENT_STAGE4D_{05,06,07,08,09,10,11,12}*.md
```

Result: passed. `git diff --check` produced no output; conflict-marker scan over `docs`, `tests` and `src` found no matches; trailing-whitespace scan for the 05J through 12Q evidence/docs set found no matches.

## DOC Matrix Guard

`DOC_MATRIX_CURRENT` remains clean at `4c999922`; A_MAIN did not integrate or reject that source commit because main still has active 05J-12Q dirty runtime/test/docs slices.

Project remains **NOT READY**.

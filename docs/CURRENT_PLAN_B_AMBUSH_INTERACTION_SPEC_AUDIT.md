# Plan B / Ambush Interaction Spec Audit

Date: 2026-06-27

Status: accepted for the narrow Ambush reaction-play source selection slice; project remains **NOT READY**.

## Scope

This slice removes the last `CoreRuleEngine` card-number allow-list from the minimal `PLAY_CARD mode=AMBUSH` battlefield reaction path.

The accepted path remains intentionally narrow:

- main phase only;
- `NEUTRAL_CLOSED` priority window only;
- an existing stack item must be pending;
- command player must hold priority;
- destination must be `BATTLEFIELD:{playerId}-MAIN`;
- no targets and no optional costs;
- source object must be in the command player's hand;
- known source `cardNo` must match the command `cardNo`;
- source object must expose the `伏击` tag;
- the command `cardNo` must expose Ambush through `BehaviorSpec.Keywords` or `BehaviorSpec.TemplateIds`;
- the command player must already control a public battlefield unit.

`CoreRuleEngine.TryBuildMinimalAmbushPlayCardPlan(...)` now calls `AmbushInteractionSpecRules.HasAmbush(command.CardNo)` instead of comparing against `UNL-021/219`. `AmbushInteractionSpecRules` builds its source set from the official catalog through `BehaviorSpecCatalogBuilder`, so adding another supported zero-target Ambush unit to existing play behavior data does not require a new Core card branch.

## Boundaries

This does not implement full Ambush breadth. Ambush target handling, optional costs, alternate battlefield placement rules, full multi-battlefield coordinates, and card-specific Ambush rider text remain open unless already covered by a separate ordinary play path.

The existing non-Ambush rejection and identity guards remain in force. A hand source without `伏击`, an unknown source, an opponent hand source, a source outside hand, a source card-number mismatch, a target payload, an optional-cost payload, or missing friendly battlefield unit still rejects without mutation.

## Validation

- TDD red: focused Ambush tests failed on `GloomyApothecaryCardNo` and on `UNL-176/219` being rejected by the old allow-list.
- Focused: `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~Ambush" --nologo` passed `29/29`.
- Adjacent: `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~Ambush|FullyQualifiedName~InteractionKeyword|FullyQualifiedName~CardCatalogBaseline|FullyQualifiedName~MatchRecovery" --nologo` passed `2348/2348`.
- Full backend: `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo` passed `8811/8811`.

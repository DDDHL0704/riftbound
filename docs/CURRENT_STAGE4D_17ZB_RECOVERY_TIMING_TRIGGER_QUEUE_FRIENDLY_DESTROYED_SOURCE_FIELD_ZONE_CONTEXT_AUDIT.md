# Stage 4D-17ZB Recovery Timing Trigger Queue Friendly-Destroyed Source Field-Zone Context Audit

Date: 2026-06-03

Status: accepted for this narrow server P1-004 recovery/replay determinism slice. Project remains **NOT READY**.

## Scope

This slice tightens `MatchRecoveryValidator` for recovered snapshot, authoritative state and spectator replay-frame timing `triggerQueue[]` entries in the Ghostly Centaur, Resonant Soul, Savage Jawfish and Viktor destroyed-non-minion trigger families.

Runtime builds these triggers only from source objects enumerated from player `base` plus `battlefields`, still present on field through `IsObjectOnField(...)`, not removed/pending, and effectively controlled by the trigger controller before queueing `TRIGGER-{stackItem.StackItemId}-{sourceObjectId}-{destroyedObjectId}-{effectKind}` with `UNIT_DESTROYED`.

The validator now rejects a readable source object when it is controlled by the trigger controller but absent from that controller's recovered or authoritative field-zone list (`base` plus `battlefields`).

Legacy payloads that do not expose a readable source controller or a field-zone registry remain compatible.

## Files Changed

- `src/Riftbound.Engine/MatchRecovery.cs`
- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`
- current checkpoint/completion/P0-P1/next-dispatch docs
- `docs/CURRENT_STAGE4D_SHARED_COORDINATION_BOARD.md`

No matrix JSON, frontend, protocol, official catalog, `fullOfficial`, formal E2E, browser/Chrome smoke or `riftbound-dotnet.sln` changes were made.

## Validation

- Focused new friendly-destroyed source field-zone context tests: `3/3`
- `TriggerQueue` filter: `314/314`
- `Recovery` filter: `999/999`
- Adjacent `Recovery|OfficialOpening|PostgresRecoveryStore` filter: `1579/1579`
- Backend full: `6944/6944`
- `dotnet format whitespace` on touched code/test files passed
- `git diff --check` passed
- Anchored conflict-marker scan over `docs`, `tests`, `src` passed
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed

## Remaining Work

This narrows replay/recovery determinism only. Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.

# Stage 4D-17WM Recovery Timing Trigger Queue Watchful Sentinel Last-Breath Draw Context Audit

Date: 2026-06-03

Status: accepted for this checkpoint. Project remains **NOT READY**.

## Scope

A_MAIN narrowed P1-004 recovery/replay determinism for Watchful Sentinel standard last-breath draw trigger queue payloads.

Runtime `CoreRuleEngine.BuildLastBreathTriggerQueueItem` constructs this trigger shape as `TRIGGER-{stackItemId}-{sourceObjectId}-WATCHFUL_SENTINEL_LAST_BREATH_DRAW_1`, with effect kind `WATCHFUL_SENTINEL_LAST_BREATH_DRAW_1` and triggered event kind `UNIT_DESTROYED`.

The new recovery validation rejects retained recovered snapshot, authoritative state and spectator replay-frame timing `triggerQueue[]` entries when the Watchful Sentinel last-breath draw context drifts away from that runtime construction:

- snapshot/spectator source visibility must remain `VISIBLE`;
- effect kind must be `WATCHFUL_SENTINEL_LAST_BREATH_DRAW_1` when readable;
- triggered event kind must be `UNIT_DESTROYED` when readable;
- source object membership remains handled by the existing trigger-queue source-object membership validator because the standard trigger id is hyphen-ambiguous across stack and source ids.

## Files Touched

- `src/Riftbound.Engine/MatchRecovery.cs`
- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`
- `docs/CURRENT_A_MASTER_CHECKPOINT.md`
- `docs/CURRENT_COMPLETION_AUDIT.md`
- `docs/CURRENT_STAGE4D_P0_P1_CLOSURE_PLAN.md`
- `docs/CURRENT_STAGE4D_NEXT_DISPATCH_AND_WRITELOCKS.md`
- `docs/CURRENT_STAGE4D_SHARED_COORDINATION_BOARD.md`
- `docs/CURRENT_STAGE4D_17WM_RECOVERY_TIMING_TRIGGER_QUEUE_WATCHFUL_SENTINEL_LAST_BREATH_DRAW_CONTEXT_AUDIT.md`

## Validation

- `dotnet format whitespace src/Riftbound.Engine/Riftbound.Engine.csproj --include src/Riftbound.Engine/MatchRecovery.cs`
- `dotnet format whitespace tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --include tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`
- `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "WatchfulSentinelLastBreathDrawContextDrift"`: passed `3/3`
- `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "TriggerQueue"`: passed `113/113`
- `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "Recovery"`: passed `798/798`
- `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "Recovery|OfficialOpening|Postgres"`: passed `1378/1378`
- `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj`: passed `6743/6743`
- `git diff --check`: passed
- anchored conflict-marker scan over `src`, `tests` and `docs`: passed
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`: passed

## Remaining Risk

This checkpoint changes only recovery frame and authoritative-state validation. It does not change protocol shape, frontend behavior, matrix coverage rows, official catalog semantics, Chrome/browser/formal E2E scripts, `fullOfficial` status, or final readiness status.

Project remains **NOT READY** pending broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E and `fullOfficial`.

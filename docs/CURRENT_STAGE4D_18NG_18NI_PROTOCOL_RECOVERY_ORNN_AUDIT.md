# Stage 4D 18NG/18NH/18NI Protocol Recovery Ornn Audit

Date: 2026-06-06 05:28 CST

Status: accepted into A_MAIN after review. Project remains **NOT READY**.

Scope:

- `tests/Riftbound.ConformanceTests/GameHubJoinTests.cs`: `SeedScenarioProductionRejectionRedactsSentinelInputsAndDoesNotBroadcast` proves production-only `SeedScenario` rejection keeps the stable `UnsupportedCommand` contract, redacts room/player/scenario/seed/raw/client-intent sentinel inputs, and emits no caller/group events, snapshots or prompts.
- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`: `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueJhinMovementResourceDestinationBaseLocationContextDrift` proves spectator replay timing Jhin movement-resource payloads reject a move-to-base trigger when authoritative object locations still place the source on the battlefield.
- `tests/Riftbound.ConformanceTests/OrnnFriendlyEquipmentStaticPowerTests.cs`: `OrnnStaticAuraMetadataDisappearsAfterAcceptedSourceLeavesFieldCommandAcrossPlayerViews` proves a real accepted destroy/source-leaves command removes stale Ornn static-aura metadata and hidden source/participant references from both player snapshots.

Worker source commits:

- `c62ac5d2` for 18NG GameHub production rejection redaction.
- `ab25640e` for 18NH spectator Jhin recovery coverage. A_MAIN corrected this slice during review from an impossible origin-object-location assertion to the validator's actual destination-location diagnostic for move-to-base object-location drift.
- `5a53e7b8` for 18NI Ornn accepted source-leaves metadata redaction.

Validation:

- Focused new tests: `3/3`.
- Touched class filter: `1463/1463`.
- Broader adjacent server filter: `5385/5385`.
- Backend full via tracked `Riftbound.slnx` under the current no-DB environment: `7311/7311`.
- `git diff --cached --check`, `git diff --check`, anchored conflict-marker scan and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed before docs sync.

Open gates:

- Runtime validation code, matrix JSON, frontend, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and `riftbound-dotnet.sln` remain locked.
- Real DB-backed Postgres smoke remains open because no connection string was available in this environment.
- This is server test breadth only; it does not close broader P0/P1, command/recovery/random determinism, full LayerEngine breadth or final readiness.

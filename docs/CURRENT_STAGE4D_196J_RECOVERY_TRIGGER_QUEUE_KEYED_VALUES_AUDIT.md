2026-06-08 Stage 4D-196J recovery trigger queue keyed values audit

Scope
- A_MAIN directly integrated one single-agent server-test slice for spectator replay timing `triggerQueue` keyed value drift.
- Runtime changed: no. Test coverage only.
- No subagent and no new worktree were created.

Code change
- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs` now includes `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedValuesWithoutCountMismatch`.
- The test builds an authoritative state with exactly one visible trigger queue item, mutates the redacted spectator replay frame's single `triggerQueue[0]` `controllerId`, `sourceObjectId`, `sourceVisibility`, `effectKind` and `triggeredByEventKind`, and keeps the redacted trigger count equal to the authoritative trigger count.
- The assertions prove keyed authoritative trigger-queue value diagnostics and aggregate trigger-queue field disagreement diagnostics are emitted while `spectator replay frame timing trigger queue count` is not emitted.

Validation
- Focused test: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedValuesWithoutCountMismatch"`: passed `1/1`.
- Changed-class filter: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MatchRecoveryTests"`: passed `1404/1404`.
- Adjacent recovery filter: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~Recovery"`: passed `1409/1409`.
- Backend full conformance project: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj -m:1 -p:UseSharedCompilation=false`: passed `7679/7679`.
- Mechanical checks before docs sync: `git diff --check` passed; `rg -n "^(<<<<<<<|=======|>>>>>>>)" docs tests src` found no conflict markers.

Coordination
- Main code commit: `23ce3f5f test: cover spectator trigger queue keyed values`.
- Push after the code commit succeeded via SSH.
- DOC_MATRIX_CURRENT was observed clean on branch `codex/stage4d-matrix-docs-current` at HEAD `17bde0c3`.
- Runtime validation code, matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final status and `riftbound-dotnet.sln` remain locked.

Residual status
- This slice narrows recovery spectator replay timing trigger-queue keyed-value validation only.
- Broader P0/P1, command/recovery/random determinism, remaining recovery payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final readiness remain open.
- Project remains **NOT READY**.

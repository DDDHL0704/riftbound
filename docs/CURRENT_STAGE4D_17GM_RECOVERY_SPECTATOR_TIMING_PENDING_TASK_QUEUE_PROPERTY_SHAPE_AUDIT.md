# Stage 4D-17GM Recovery Spectator Timing Pending Task Queue Property Shape Audit

Date: 2026-05-25

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidateSpectatorReplayFrame` now validates spectator replay timing object property names for `Timing["pendingTaskQueue"]`, nested `Timing["pendingTaskQueue"]["tasks"][]` items and nested `Timing["pendingTaskQueue"]["metadata"]`. Properties must have non-blank names without surrounding whitespace, and duplicate normalized property names are rejected before pending-task queue field extraction consumes the payload. `pendingTaskQueue.tasks[]` items also now require object payloads explicitly before list-field extraction. This prevents duplicate JSON properties or spectator pending-task queue property-name drift from being silently resolved by dictionary / `JsonElement` key lookup during recovery-frame spectator validation.

Test change: `RecoveryValidatorRejectsSpectatorReplayTimingPendingTaskQueuePropertyNameDrift` proves duplicate, whitespace-mutated and blank property names across the queue object, task item payloads and metadata produce explicit recovery diagnostics.

Validation:

- Focused single test: new pending-task queue property-name drift test passed `1/1`.
- Focused recovery: `MatchRecoveryTests` passed `214/214`.
- Adjacent recovery/opening/store-smoke: passed `795/795`.
- Backend full: passed `6160/6160`.
- Mechanical checks: `git diff --check`, anchored conflict-marker scan over `src`/`tests`/`docs`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness gate, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes only the spectator timing pending-task queue property-name shape slice. Broader command/recovery/random determinism, full LayerEngine breadth, battlefield/battle lifecycle breadth, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and final readiness remain open.

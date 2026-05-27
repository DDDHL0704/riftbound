# Stage 4D-17LA Recovery Spectator Timing Pending Payload Shape Audit

Date: 2026-05-27 20:27 CST

Scope: A_MAIN server P1-004 replay/recovery determinism slice for spectator replay-frame timing pending queue/payment/hand-choice top-level object payload shape validation.

Runtime change:
- `MatchRecoveryValidator.ValidateSpectatorReplayFrame` now distinguishes missing/null pending timing semantics from present malformed non-object payloads for `pendingTaskQueue`, `pendingPayment` and `pendingHandChoice`.
- Present non-null non-object `Timing["pendingTaskQueue"]`, `Timing["pendingPayment"]` and `Timing["pendingHandChoice"]` payloads now produce explicit payload-shape diagnostics before property-name, value-shape and parity consumers consume those payloads.
- Missing pending payloads and required null payloads keep their existing required diagnostics.
- No-pending authoritative payment and hand-choice null compatibility is unchanged.

Test coverage:
- Added `RecoveryValidatorRejectsSpectatorReplayTimingPendingPayloadShapeDrift` in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- The regression mutates spectator replay-frame `Timing["pendingTaskQueue"]`, `Timing["pendingPayment"]` and `Timing["pendingHandChoice"]` to non-object payloads and asserts explicit payload-shape diagnostics for each pending payload.

Validation:
- Focused single test: `1/1`.
- Focused recovery suite: `332/332`.
- Adjacent recovery/opening/store-smoke filter: `913/913`.
- Backend full suite: `6278/6278`.
- Mechanical checks passed: `git diff --check`, anchored conflict-marker scan over `docs`/`tests`/`src`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`.

Locks:
- Touched only recovery runtime validation, recovery tests, current checkpoint/completion/P0-P1/next-dispatch docs, this dedicated audit doc and the shared coordination board.
- Matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final status and `riftbound-dotnet.sln` remain locked.

Status: This narrows replay/recovery determinism only. P0/P1, broader command/recovery/random determinism, remaining recovered/spectator nested payload shape breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final status remain open. Project remains **NOT READY**.

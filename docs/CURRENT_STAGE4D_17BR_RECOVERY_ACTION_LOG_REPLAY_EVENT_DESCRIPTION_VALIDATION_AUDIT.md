# Stage 4D-17BR Recovery Action-Log Replay Event-Description Validation Audit

2026-05-24 Stage 4D-17BR recovery action-log replay event-description validation accepted.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change:
- `MatchActionLogReplayer.VerifyFinalStateAsync` now compares replayed event descriptions against recovered event descriptions for each recovered command span.
- A recovered event-description mismatch is now rejected even when command accepted/error/tick/event count and final state hash still match.
- This is recovery action-log replay validation only; command resolution, protocol shape, frontend and matrix JSON are unchanged.

Coverage added:
- `ActionLogReplayerReportsRecoveredEventDescriptionMismatch`

Behavior proved:
- Corrupted recovered event-description metadata is rejected before event-stream drift can pass recovery audit under a matching final state hash.

Validation:
- Focused: `96/96`.
- Adjacent recovery/opening: `677/677`.
- Backend full: `6042/6042`.

This narrows P1-004 replay/recovery determinism only. Project remains **NOT READY**.

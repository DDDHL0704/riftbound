# Stage 4D-17BP Recovery Action-Log Replay Event-Kind Validation Audit

2026-05-24 Stage 4D-17BP recovery action-log replay event-kind validation accepted.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change:
- `MatchActionLogReplayer.ValidateRecoveryFrameAsync` now passes recovered events from `MatchRecoveryFrame.Events` into replay verification.
- `VerifyFinalStateAsync` now compares replayed event kinds against recovered event kinds for each recovered command span.
- A recovered event-kind mismatch is now rejected even when command accepted/error/tick/event count and final state hash still match.
- This is recovery action-log replay validation only; command resolution, protocol shape, frontend and matrix JSON are unchanged.

Coverage added:
- `ActionLogReplayerReportsRecoveredEventKindMismatch`
- Existing registry recovery audit tests now provide recovered events into the replay audit path.

Behavior proved:
- Corrupted recovered event-kind metadata is rejected before event-stream drift can pass recovery audit under a matching final state hash.

Validation:
- Focused: `94/94`.
- Adjacent recovery/opening: `675/675`.
- Backend full: `6040/6040`.

This narrows P1-004 replay/recovery determinism only. Project remains **NOT READY**.

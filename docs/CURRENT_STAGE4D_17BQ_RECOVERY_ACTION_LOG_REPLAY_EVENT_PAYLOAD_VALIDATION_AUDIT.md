# Stage 4D-17BQ Recovery Action-Log Replay Event-Payload Validation Audit

2026-05-24 Stage 4D-17BQ recovery action-log replay event-payload validation accepted.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change:
- `MatchActionLogReplayer.VerifyFinalStateAsync` now compares replayed event payload hashes against recovered event payload hashes for each recovered command span.
- Payload hashes use canonical JSON via `MatchStateHasher.HashValue`, so dictionary/object payloads are compared deterministically instead of by reference.
- A recovered event-payload mismatch is now rejected even when command accepted/error/tick/event count and final state hash still match.
- This is recovery action-log replay validation only; command resolution, protocol shape, frontend and matrix JSON are unchanged.

Coverage added:
- `ActionLogReplayerReportsRecoveredEventPayloadMismatch`

Behavior proved:
- Corrupted recovered event-payload metadata is rejected before event-stream drift can pass recovery audit under a matching final state hash.

Validation:
- Focused: `95/95`.
- Adjacent recovery/opening: `676/676`.
- Backend full: `6041/6041`.

This narrows P1-004 replay/recovery determinism only. Project remains **NOT READY**.

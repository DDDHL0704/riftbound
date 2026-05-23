2026-05-24 Stage 4D-17W recovery event payload validation accepted.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change:
- `MatchRecoveryValidator.ValidateEvents` now rejects recovered events whose `GameEvent.Payload` is null.
- This is recovery frame validation only; command resolution, protocol shape, snapshots/prompts, frontend and matrix JSON are unchanged.

Coverage added:
- `RecoveryValidatorRejectsNullRecoveredEventPayload`

Behavior proved:
- Corrupted recovered event payload metadata is rejected before spectator redaction, state-hash audit, action-log replay restoration, or downstream event consumers can consume the payload.

Validation:
- Focused: `49/49`.
- Adjacent recovery/opening: `630/630`.
- Backend full: `5995/5995`.

This narrows P1-004 replay/recovery determinism only. Project remains **NOT READY**.

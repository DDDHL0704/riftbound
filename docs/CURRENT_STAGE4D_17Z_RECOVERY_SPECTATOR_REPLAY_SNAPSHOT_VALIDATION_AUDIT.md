2026-05-24 Stage 4D-17Z recovery spectator replay snapshot validation accepted.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change:
- `MatchRecoveryValidator.ValidateSpectatorReplayFrame` now rejects spectator replay frames with missing spectator snapshot payloads.
- This is recovery frame validation only; command resolution, protocol shape, player snapshots/prompts, frontend and matrix JSON are unchanged.

Coverage added:
- `RecoveryValidatorRejectsMissingSpectatorReplaySnapshot`

Behavior proved:
- Corrupted spectator replay snapshot metadata is rejected before timing redaction checks dereference malformed replay-frame data.

Validation:
- Focused: `52/52`.
- Adjacent recovery/opening: `633/633`.
- Backend full: `5998/5998`.

This narrows P1-004 replay/recovery determinism only. Project remains **NOT READY**.

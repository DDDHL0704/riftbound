2026-05-24 Stage 4D-17J recovery prompt metadata presence validation accepted.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change:
- `MatchRecoveryValidator.ValidatePlayerViews` now checks that recovered prompt row metadata and payload are present together.
- `PromptTick` / `PromptEventSequence` without a `Prompt` payload is invalid.
- A `Prompt` payload without row `PromptTick` / `PromptEventSequence` metadata is invalid.
- This is recovery frame validation only; command resolution, protocol shape, snapshots/prompts, frontend and matrix JSON are unchanged.

Coverage added:
- `RecoveryValidatorRejectsPromptMetadataWithoutMatchingPayload`

Behavior proved:
- Corrupted prompt metadata/payload mismatch is rejected before player-view restoration.

Validation:
- Focused: `36/36`.
- Adjacent recovery/opening: `617/617`.
- Backend full: `5982/5982`.

This narrows P1-004 replay/recovery determinism only. Project remains **NOT READY**.

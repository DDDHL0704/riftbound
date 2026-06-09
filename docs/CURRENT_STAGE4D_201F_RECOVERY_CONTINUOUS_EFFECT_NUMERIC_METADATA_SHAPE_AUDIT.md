# Stage 4D-201F Recovery Continuous Effect Numeric Metadata Shape Audit

Date: 2026-06-09

Owner: A_MAIN

## Scope

Stage 4D-201F adds one server-test shard for spectator recovery replay timing continuous-effect power-modifier numeric metadata payload-shape validation when the spectator continuous effect count still matches authoritative state and aggregate same-count comparisons remain active.

Runtime changed: no. Test coverage only.

Files changed:

- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`

## Coverage

New test:

- `RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectKeyedPowerModifierNumericMetadataShapeWithoutCountMismatch`

The test builds an authoritative tracked power-modifier continuous effect for `effect-1` on `target-1` from `source-1`. It starts from the redacted spectator replay frame, keeps the single spectator continuous effect keyed to authoritative `effect-1`, mutates only numeric power-modifier metadata fields to invalid payload shapes, and leaves the spectator continuous effect count equal to authoritative count.

Numeric metadata shape drift covered:

- `requestedPowerDelta` changed from an integer to a string payload.
- `appliedPowerDelta` changed from an integer to a fractional number payload.
- `minimumPower` changed from an integer to an array payload.
- `resultingPower` changed from an integer to a string payload.
- `appliedOrder` changed from an integer to an object payload.

Assertions prove recovery validation emits:

- Invalid-shape diagnostics for requested power delta, applied power delta, minimum power, resulting power and applied order.
- Keyed authoritative mismatch diagnostics for requested power delta, applied power delta, minimum power, resulting power and applied order for effect id `effect-1`.
- Aggregate continuous-effect disagreement diagnostics for requested power deltas, applied power deltas, minimum powers, resulting powers and applied orders.

Assertions also prove this same-count path does not emit a continuous-effect count mismatch diagnostic.

## Validation

Passed:

- Focused: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectKeyedPowerModifierNumericMetadataShapeWithoutCountMismatch"` -> `1/1`.
- Changed class: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecoveryTests"` -> `1530/1530`.
- Adjacent recovery: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~Recovery"` -> `1535/1535`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx` -> `7805/7805`.
- Mechanical: `git diff --check` passed.
- Mechanical: `rg -n "^(<<<<<<<|=======|>>>>>>>)" docs tests src` returned no conflict markers.

## Coordination

- Code commit: `75abaae8`.
- Push after the code commit succeeded.
- No subagent and no new worktree were created.
- DOC_MATRIX_CURRENT was observed clean at `17bde0c3` on 2026-06-09 16:35 CST before docs sync.
- Project remains **NOT READY**.

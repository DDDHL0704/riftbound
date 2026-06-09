# Stage 4D-202G Recovery Continuous Effect Static Aura Source Order Missing Field Audit

Date: 2026-06-09

Owner: A_MAIN

## Scope

Stage 4D-202G adds one server-test shard for spectator recovery replay timing continuous-effect static-aura `sourceOrder` missing-field validation when the spectator continuous effect count still matches authoritative state and aggregate same-count comparisons remain active.

Runtime changed: no. Test coverage only.

Files changed:

- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`

## Coverage

New test:

- `RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectKeyedStaticAuraSourceOrderMissingFieldWithoutCountMismatch`

The test builds an authoritative battlefield static-aura continuous effect keyed as `STATIC_AURA:BATTLEFIELD_ALL_UNITS_POWER_PLUS_ONE:battlefield-1:participant-1`. It starts from the redacted spectator replay frame, keeps the single spectator continuous effect keyed to authoritative state, removes only the static-aura `sourceOrder` field, and leaves the spectator continuous effect count equal to authoritative count.

Field covered:

- `sourceOrder`

Assertions prove recovery validation emits:

- Static-aura source-order required diagnostics.
- Keyed authoritative `sourceOrder` mismatch diagnostics for the static-aura effect id.
- Aggregate continuous-effect source-order disagreement diagnostics while counts still match.

Assertions also prove this same-count path does not emit invalid source-order shape diagnostics or a continuous-effect count mismatch diagnostic.

## Validation

Passed:

- Focused: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectKeyedStaticAuraSourceOrderMissingFieldWithoutCountMismatch"` -> `1/1`.
- Changed class: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecoveryTests"` -> `1557/1557`.
- Adjacent recovery: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~Recovery"` -> `1562/1562`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx` -> `7832/7832`.
- Mechanical: `git diff --check` passed.
- Mechanical: `rg -n "^(<<<<<<<|=======|>>>>>>>)" docs tests src` returned no conflict markers.

## Coordination

- Code commit: `08c0a94d`.
- Push after the code commit succeeded.
- No subagent and no new worktree were created.
- DOC_MATRIX_CURRENT was observed clean at `17bde0c3` on 2026-06-09 21:16 CST before docs sync.
- Project remains **NOT READY**.

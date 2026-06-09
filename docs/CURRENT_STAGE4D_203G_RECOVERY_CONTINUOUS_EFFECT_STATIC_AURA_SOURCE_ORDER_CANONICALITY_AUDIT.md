# Stage 4D-203G Recovery Continuous Effect Static Aura Source Order Canonicality Audit

Date: 2026-06-10

Owner: A_MAIN

## Scope

Stage 4D-203G adds one server-test shard for spectator recovery replay timing continuous-effect static-aura `sourceOrder` positive-value canonicality without a continuous-effect count mismatch while aggregate source-order comparison remains active.

Runtime changed: no. Test coverage only.

Files changed:

- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`

## Coverage

New test:

- `RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectKeyedStaticAuraSourceOrderCanonicalityWithoutCountMismatch`

The test builds one authoritative static-aura continuous effect keyed as `STATIC_AURA:BATTLEFIELD_ALL_UNITS_POWER_PLUS_ONE:battlefield-1:participant-1`. It starts from the redacted spectator replay frame, keeps the spectator continuous-effect count equal to authoritative count, and changes only the spectator `sourceOrder` field from the positive authoritative value to `-1`.

Assertions prove recovery validation emits:

- Source-order positive-value diagnostics.
- Keyed authoritative source-order mismatch diagnostics for the static-aura effect id.
- Aggregate same-count continuous-effect source-order disagreement diagnostics.

Assertions also prove this same-count source-order canonicality path does not emit:

- Continuous-effect count mismatch diagnostics.

## Validation

Passed:

- Focused: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectKeyedStaticAuraSourceOrderCanonicalityWithoutCountMismatch"` -> `1/1`.
- Changed class: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecoveryTests"` -> `1583/1583`.
- Adjacent recovery: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~Recovery"` -> `1588/1588`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx` -> `7858/7858`.
- Mechanical: `git diff --check` passed.
- Mechanical: `rg -n "^(<<<<<<<|=======|>>>>>>>)" docs tests src` returned no conflict markers.

## Coordination

- Code commit: `3c27cf24`.
- Push after the code commit succeeded.
- No subagent and no new worktree were created.
- DOC_MATRIX_CURRENT was observed clean at `17bde0c3` on 2026-06-10 01:57 CST before docs sync.
- Project remains **NOT READY**.

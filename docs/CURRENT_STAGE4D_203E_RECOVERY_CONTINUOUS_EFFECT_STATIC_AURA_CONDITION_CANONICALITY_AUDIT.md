# Stage 4D-203E Recovery Continuous Effect Static Aura Condition Canonicality Audit

Date: 2026-06-10

Owner: A_MAIN

## Scope

Stage 4D-203E adds one server-test shard for spectator recovery replay timing continuous-effect static-aura `condition` surrounding-whitespace canonicality without a continuous-effect count mismatch while aggregate condition comparison remains active.

Runtime changed: no. Test coverage only.

Files changed:

- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`

## Coverage

New test:

- `RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectKeyedStaticAuraConditionCanonicalityWithoutCountMismatch`

The test builds one authoritative static-aura continuous effect keyed as `STATIC_AURA:BATTLEFIELD_ALL_UNITS_POWER_PLUS_ONE:battlefield-1:participant-1`. It starts from the redacted spectator replay frame, keeps the spectator continuous-effect count equal to authoritative count, and changes only the spectator `condition` field to the canonical value with surrounding whitespace.

Assertions prove recovery validation emits:

- Condition surrounding-whitespace diagnostics.
- Keyed authoritative condition mismatch diagnostics for the static-aura effect id.
- Aggregate same-count continuous-effect condition disagreement diagnostics.

Assertions also prove this same-count condition canonicality path does not emit:

- Continuous-effect count mismatch diagnostics.

## Validation

Passed:

- Focused: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectKeyedStaticAuraConditionCanonicalityWithoutCountMismatch"` -> `1/1`.
- Changed class: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecoveryTests"` -> `1581/1581`.
- Adjacent recovery: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~Recovery"` -> `1586/1586`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx` -> `7856/7856`.
- Mechanical: `git diff --check` passed.
- Mechanical: `rg -n "^(<<<<<<<|=======|>>>>>>>)" docs tests src` returned no conflict markers.

## Coordination

- Code commit: `3c5ed266`.
- Push after the code commit succeeded.
- No subagent and no new worktree were created.
- DOC_MATRIX_CURRENT was observed clean at `17bde0c3` on 2026-06-10 01:37 CST before docs sync.
- Project remains **NOT READY**.

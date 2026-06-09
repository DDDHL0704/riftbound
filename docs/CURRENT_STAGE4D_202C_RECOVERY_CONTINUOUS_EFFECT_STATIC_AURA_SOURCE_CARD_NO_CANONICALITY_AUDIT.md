# Stage 4D-202C Recovery Continuous Effect Static Aura Source Card No Canonicality Audit

Date: 2026-06-09

Owner: A_MAIN

## Scope

Stage 4D-202C adds one server-test shard for spectator recovery replay timing continuous-effect static-aura `sourceCardNo` surrounding-whitespace canonicality validation when the spectator continuous effect count still matches authoritative state and aggregate same-count comparisons remain active.

Runtime changed: no. Test coverage only.

Files changed:

- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`

## Coverage

New test:

- `RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectKeyedStaticAuraSourceCardNoCanonicalityWithoutCountMismatch`

The test builds an authoritative battlefield static-aura continuous effect keyed as `STATIC_AURA:BATTLEFIELD_ALL_UNITS_POWER_PLUS_ONE:battlefield-1:participant-1`. It starts from the redacted spectator replay frame, keeps the single spectator continuous effect keyed to authoritative state, changes only the static-aura `sourceCardNo` field from `OGN·294/298` to the same value with surrounding whitespace, and leaves the spectator continuous effect count equal to authoritative count.

Canonicality field covered:

- `sourceCardNo`

Assertions prove recovery validation emits:

- Surrounding-whitespace diagnostics for `sourceCardNo`.
- Keyed authoritative source-card-number mismatch diagnostics for the static-aura effect id.
- Aggregate continuous-effect source-card-number disagreement diagnostics while counts still match.

Assertions also prove this same-count path does not emit a continuous-effect count mismatch diagnostic.

## Validation

Passed:

- Focused: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectKeyedStaticAuraSourceCardNoCanonicalityWithoutCountMismatch"` -> `1/1`.
- Changed class: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecoveryTests"` -> `1553/1553`.
- Adjacent recovery: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~Recovery"` -> `1558/1558`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx` -> `7828/7828`.
- Mechanical: `git diff --check` passed.
- Mechanical: `rg -n "^(<<<<<<<|=======|>>>>>>>)" docs tests src` returned no conflict markers.

## Coordination

- Code commit: `56c24a79`.
- Push after the code commit succeeded.
- No subagent and no new worktree were created.
- DOC_MATRIX_CURRENT was observed clean at `17bde0c3` on 2026-06-09 20:34 CST before docs sync.
- Project remains **NOT READY**.

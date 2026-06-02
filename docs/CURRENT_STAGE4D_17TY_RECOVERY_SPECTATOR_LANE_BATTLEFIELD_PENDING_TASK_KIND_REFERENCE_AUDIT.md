# Stage 4D-17TY Recovery Spectator Lane Battlefield Pending-Task-Kind Reference Audit

Date: 2026-06-02

Owner: A_MAIN

Status: accepted checkpoint slice. Project remains **NOT READY**.

## Scope

Stage 4D-17TY tightens spectator replay-frame snapshot lane battlefield pending-task-kind validation under lane battlefield count mismatch. This slice adds same-payload authoritative-membership diagnostics before existing broad list parity diagnostics continue to run.

## Runtime Change

Changed `src/Riftbound.Engine/MatchRecovery.cs`.

`MatchRecoveryValidator` now validates spectator replay-frame snapshot lane `battlefields[]` `pendingTaskKinds[]` values against the authoritative pending task kind set for the same battlefield.

This preserves existing validation for:

- `pendingTaskKinds[]` string-list shape and value shape
- exact broad pending-task-kind list parity
- lane battlefield count parity

## Test Coverage

Changed `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Added:

- `RecoveryValidatorRejectsSpectatorReplaySnapshotLaneBattlefieldPendingTaskKindsOutsideAuthoritativeKindsWithCountMismatch`

The test builds a contested authoritative battlefield with visible pending task kinds, forges one spectator `pendingTaskKinds[]` entry, and verifies the explicit missing-authoritative-kind diagnostic plus the existing lane battlefield count and broad pending-task-kind mismatch diagnostics.

## Validation

- focused pending-task-kind test `1/1`
- focused SpectatorReplaySnapshotLane filter `11/11`
- focused SpectatorReplaySnapshotBattlefield filter `6/6`
- focused recovery `652/652`
- adjacent recovery/opening/store-smoke `1233/1233`
- backend full `6598/6598`
- `git diff --check` passed
- anchored conflict-marker scan over `docs`, `src` and `tests` passed
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed

Known global `dotnet format Riftbound.slnx --verify-no-changes` remains blocked by unrelated pre-existing whitespace diagnostics outside this slice; no unrelated formatting was applied.

## Locked Surfaces

No matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness status or `riftbound-dotnet.sln` changes.

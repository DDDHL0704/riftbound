# Stage 4D-211F Recovery Timing Map Property Name Count Audit

Date: 2026-06-12 14:23 CST

## Scope

- Owner: A_MAIN
- Worktree: `/Users/dinghaolin/IdeaProjects/riftbound`
- Branch: `main`
- Code commit: `b43d38d2`
- Runtime changed: no, server test coverage only
- Primary test file: `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`
- New test: `RecoveryValidatorRejectsSpectatorReplayTimingMapPropertyNameDriftWithCountMismatch`

## Coverage Added

This slice covers spectator replay timing top-level map property-name validation when the same frame also has a trigger queue count mismatch.

- Authoritative timing state starts with no trigger queue items.
- The spectator timing map adds ` phase ` alongside canonical `phase` to create a duplicate canonical property name and surrounding-whitespace property-name drift.
- The spectator timing map also adds an empty property name.
- The spectator `triggerQueue[]` is changed from empty to one visible-source trigger item, so spectator count is `1` while authoritative state has `0`.

The validator now has explicit regression coverage proving it reports timing map property-name diagnostics together with trigger queue count mismatch diagnostics in the same spectator replay frame.

## Validation

- Focused: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingMapPropertyNameDriftWithCountMismatch"` -> `1/1`
- Focused trigger queue: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --filter "TriggerQueue"` -> `701/701`
- Changed class: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --filter "FullyQualifiedName~MatchRecoveryTests"` -> `1790/1790`
- Adjacent recovery: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --filter "Recovery"` -> `1795/1795`
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx` -> `8065/8065`
- Mechanical: `git diff --check` passed.
- Mechanical: conflict-marker scan over `docs`, `tests`, and `src` passed.

## Status

This narrows recovery spectator replay timing top-level map property-name validation with trigger queue count mismatch only. Broader P0/P1, command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final status remain open.

Project remains **NOT READY**.

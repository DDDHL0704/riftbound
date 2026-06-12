# Stage 4D-211G Recovery Order Prompt Field Count Audit

Date: 2026-06-12 14:32 CST

## Scope

- Owner: A_MAIN
- Worktree: `/Users/dinghaolin/IdeaProjects/riftbound`
- Branch: `main`
- Code commit: `116206c4`
- Runtime changed: no, server test coverage only
- Primary test file: `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`
- New test: `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueOrderPromptFieldAbsenceDriftWithCountMismatch`

## Coverage Added

This slice covers spectator replay timing order-trigger prompt field absence validation when the same frame also has a trigger queue count mismatch.

- Authoritative timing state starts in `NeutralOpen` with no trigger queue items.
- The spectator timing map adds stale order-trigger prompt fields: `orderingPlayerId`, `orderedTriggerIds`, `triggerIds`, `triggers`, `triggerChoices`, `legalOrderingConstraints`, `triggeredByEventKind`, and `orderingState`.
- The spectator `triggerQueue[]` is changed from empty to one visible-source trigger item, so spectator count is `1` while authoritative state has `0`.

The validator now has explicit regression coverage proving it reports all eight order-trigger prompt field absence diagnostics together with trigger queue count mismatch diagnostics in the same spectator replay frame.

## Validation

- Focused: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueOrderPromptFieldAbsenceDriftWithCountMismatch"` -> `1/1`
- Focused trigger queue: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --filter "TriggerQueue"` -> `702/702`
- Changed class: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --filter "FullyQualifiedName~MatchRecoveryTests"` -> `1791/1791`
- Adjacent recovery: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --filter "Recovery"` -> `1796/1796`
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx` -> `8066/8066`
- Mechanical: `git diff --check` passed.
- Mechanical: conflict-marker scan over `docs`, `tests`, and `src` passed.

## Status

This narrows recovery spectator replay timing order-trigger prompt field absence validation with trigger queue count mismatch only. Broader P0/P1, command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final status remain open.

Project remains **NOT READY**.

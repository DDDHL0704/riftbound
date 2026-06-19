# Stage 4D-223EA Battlefield State Diagnostics Audit

Date: 2026-06-19 18:22 CST

Owner: A_MAIN

Worktree: `/Users/dinghaolin/IdeaProjects/riftbound`

Branch / commit: `main` code commit `eb5a2077` (`test: detail battlefield state diagnostics`); docs checkpoint follows.

Project status: **NOT READY**.

## Scope

A_MAIN accepted one direct `main`-branch runtime diagnostic shard for `MatchRecoveryValidator` triggerQueue battlefield-state missing checks.

Runtime changed: yes, diagnostic detail only. Valid recovery replay behavior, battlefield-state construction, lane parsing, source visibility redaction, trigger queue construction, trigger ordering, Last Breath / movement-resource behavior and gameplay behavior are unchanged.

Frontend changed: no.

Files changed in the code commit:

- `src/Riftbound.Engine/MatchRecovery.cs`
- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`

## Accepted Behavior

The following triggerQueue battlefield-state missing diagnostics now preserve their existing prefixes while appending stable expected/actual detail:

- Blue Sentinel delayed-resource referenced battlefield object id missing from battlefield states.
- Jhin movement-resource origin/destination battlefield object id missing from battlefield states.
- Kogmaw Last Breath battlefield object id missing from battlefield states.

The suffix reports the sorted known battlefield-state object id set, for example `expected [battlefield-1] but got battlefield-2`. Existing `MatchRecoveryTests` coverage now asserts the detailed suffix across recovered snapshot timing, authoritative-state triggerQueue and spectator replay timing triggerQueue surfaces.

## Rule Source

Rule source was checked through `AGENTS.md`, `docs/CURRENT_CORE_RULE_PDF_READING_NOTES.md`, the five root PDFs and `/tmp/riftbound_rules_pdf_text/`.

Relevant anchors remain latest core rules 108.7.c-d, 109, 128, 129.3-129.4, 130.5-130.6, 160-166, 319-321, 323.4, 333-334, 382-383 and 808.1.c-d. No rule behavior changed.

## External Worktrees

At the post-code-commit check:

- `main...origin/main`: `1 0`
- `main...codex/ui-followup-20260616`: `323 0`
- `main...codex/rule-audit-remaining-20260615`: `396 0`

The UI followup worktree `/Users/dinghaolin/MyProjects/riftbound-codex-ui-followup-20260616` was clean and no-ahead at the opening check. The historical `rule-audit-remaining-20260615` worktree path is no longer present in `git worktree list` or `/Users/dinghaolin/MyProjects`, but the local branch remained no-ahead of `main` at this check. Exact divergence must be rechecked before any later integration.

## Validation

Passed:

- `env DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH dotnet build src/Riftbound.Engine/Riftbound.Engine.csproj --no-restore`
- `env DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~BattlefieldStateContextDrift"`: `16/16`
- `env DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-build --filter "FullyQualifiedName~MatchRecoveryTests&FullyQualifiedName~TriggerQueue"`: `747/747`
- `env DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-build --filter "FullyQualifiedName~MatchRecoveryTests"`: `1979/1979`
- `env DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-build --filter "FullyQualifiedName~Recovery|FullyQualifiedName~SpectatorReplay|FullyQualifiedName~Snapshot|FullyQualifiedName~Timing|FullyQualifiedName~ContinuousEffect|FullyQualifiedName~TriggerQueue|FullyQualifiedName~OrderTriggers|FullyQualifiedName~Trigger|FullyQualifiedName~Stack|FullyQualifiedName~Battle"`: `3572/3572`
- `env DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH dotnet test Riftbound.slnx`: `8317/8317`
- `git diff --check`
- `rg -n "^(<<<<<<<|=======|>>>>>>>)" src tests docs --glob '!**/bin/**' --glob '!**/obj/**'`
- `rg -n "battlefield object id battlefield-2 is missing from .*battlefield states\"" tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs` returned no findings.

## Remaining Not Ready Scope

This narrows triggerQueue battlefield-state missing diagnostics only. It does not close broader command/recovery/random determinism, continuous-effect breadth, remaining triggerQueue keyed/detail breadth, full LayerEngine breadth, P0/P1, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` or final readiness.

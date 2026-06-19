# Stage 4D-223DW Face-Down Source Diagnostics Audit

Date: 2026-06-19 17:31 CST

Owner: A_MAIN

Worktree: `/Users/dinghaolin/IdeaProjects/riftbound`

Branch / commit: `main` code commit `c18d871c` (`test: detail face-down diagnostics`); docs checkpoint follows.

Project status: **NOT READY**.

## Scope

A_MAIN accepted one direct `main`-branch runtime diagnostic shard for `MatchRecoveryValidator` triggerQueue source-object face-down checks.

Runtime changed: yes, diagnostic detail only. Valid recovery replay behavior, source visibility redaction, trigger queue construction, trigger ordering, Last Breath / friendly-destroyed semantics, standby semantics, card typing and gameplay behavior are unchanged.

Frontend changed: no.

Files changed in the code commit:

- `src/Riftbound.Engine/MatchRecovery.cs`
- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`

## Accepted Behavior

The following triggerQueue source-object face-down rejection diagnostics now preserve their existing prefixes while appending stable expected/actual detail:

- Blue Sentinel delayed-resource source object
- OGS Lux high-cost spell source object
- Teemo on-play self-power source object
- Scouting Warhawk / standard Last Breath source object
- Ghostly Centaur friendly-destroyed source object
- Jhin movement-resource source object
- Kogmaw Last Breath source object

The suffix is `expected false but got true`, sourced from the recovered `objectFaceDowns` value.

Existing `MatchRecoveryTests` coverage now asserts the detailed suffix across recovered snapshot timing, authoritative-state triggerQueue and spectator replay timing triggerQueue surfaces.

## Rule Source

Rule source was checked through `AGENTS.md`, `docs/CURRENT_CORE_RULE_PDF_READING_NOTES.md`, the five root PDFs and `/tmp/riftbound_rules_pdf_text/`.

Relevant anchors remain latest core rules 128, 129.3, 157.3, 157.3.a, 160-166, 303.2.a, 319-321, 323.4, 333-334, 382-383 and 808.1.d. No rule behavior changed.

## External Worktrees

At the post-code-commit check:

- `main...origin/main`: `1 0`
- `main...codex/ui-followup-20260616`: `312 0`
- `main...codex/rule-audit-remaining-20260615`: `385 0`

Both external branches remained no-ahead of local `main` at this check. Exact divergence must be rechecked before any later integration.

## Validation

Passed:

- `env DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH dotnet build src/Riftbound.Engine/Riftbound.Engine.csproj --no-restore`
- `env DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~SourceVisibilityStateContextDrift"`: `20/20`
- `env DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~MatchRecoveryTests&FullyQualifiedName~TriggerQueue"`: `747/747`
- `env DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~MatchRecoveryTests"`: `1979/1979`
- `env DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~Recovery|FullyQualifiedName~SpectatorReplay|FullyQualifiedName~Snapshot|FullyQualifiedName~Timing|FullyQualifiedName~ContinuousEffect|FullyQualifiedName~TriggerQueue|FullyQualifiedName~OrderTriggers|FullyQualifiedName~Trigger|FullyQualifiedName~Stack|FullyQualifiedName~Battle"`: `3572/3572`
- `env DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH dotnet test Riftbound.slnx --no-restore`: `8317/8317`
- `git diff --check`
- `rg -n "^(<<<<<<<|=======|>>>>>>>)" docs src tests`
- `rg -n "must not be face down(?!.*expected false but got true)" tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs --pcre2` returned no findings.

## Remaining Not Ready Scope

This narrows triggerQueue face-down source diagnostics only. It does not close broader command/recovery/random determinism, continuous-effect breadth, remaining triggerQueue keyed/detail breadth, full LayerEngine breadth, P0/P1, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` or final readiness.


# Stage 4D-223DP Blue Sentinel Location Zone Details Audit

Date: 2026-06-19 16:10 CST

Worktree: `/Users/dinghaolin/IdeaProjects/riftbound`

Branch: `main`

Code commit: `4094db5e` (`test: detail blue sentinel location diagnostics`)

Status: accepted as a narrow P1-004/server runtime diagnostic shard. Project remains **NOT READY**.

## Scope

- Runtime changed: yes, diagnostic detail only in `src/Riftbound.Engine/MatchRecovery.cs`.
- Frontend changed: no.
- Rule behavior changed: no.
- Test coverage changed: yes, `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs` now covers Blue Sentinel delayed-resource triggerQueue source location-zone drift across recovered snapshot timing, authoritative state and spectator replay timing surfaces.

## Runtime Detail

`MatchRecoveryValidator` already rejected Blue Sentinel delayed-resource triggerQueue entries whose source object location zone was not `BATTLEFIELD`. This shard preserves the existing diagnostic prefix and appends `FormatExpectedActualForRecovery("BATTLEFIELD", sourceZone)`, so the mismatch now reports `BATTLEFIELD` as expected and the recovered source location zone as got.

The change is diagnostic-only. It does not alter Blue Sentinel delayed-resource behavior, trigger id parsing, trigger queue construction, battlefield state validation, source-object visibility, object location validation, replay recovery, or any legal gameplay path.

## Rule Source

Rule source checked through `AGENTS.md`, `docs/CURRENT_CORE_RULE_PDF_READING_NOTES.md`, the five root PDFs and `/tmp/riftbound_rules_pdf_text/`: latest core rules 128, 129.3, 157.3, 157.3.a, 160-166, 303.2.a, 319-321, 323.4, 333-334, 382-383 and 808.1.d. No rules behavior changed.

## Validation

- `dotnet build src/Riftbound.Engine/Riftbound.Engine.csproj --no-restore` passed.
- `dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~SourceLocationZoneContextDrift"` passed `3/3` after an initial concurrent `dotnet test` attempt hit a transient `obj` file lock while another `dotnet test` was building.
- `dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~MatchRecoveryTests&FullyQualifiedName~TriggerQueue"` passed `747/747`.
- `dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~MatchRecoveryTests"` passed `1979/1979`.
- `dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Recovery|FullyQualifiedName~SpectatorReplay|FullyQualifiedName~Snapshot|FullyQualifiedName~Timing|FullyQualifiedName~ContinuousEffect|FullyQualifiedName~TriggerQueue|FullyQualifiedName~OrderTriggers|FullyQualifiedName~Trigger|FullyQualifiedName~Stack|FullyQualifiedName~Battle"` passed `3572/3572`.
- `dotnet test Riftbound.slnx --no-restore` passed `8316/8316`.
- `git diff --check` passed.
- Anchored conflict-marker scan over `src`, `tests` and `docs` had no findings.
- Residual Blue Sentinel source location-zone diagnostic assertion search found the selected triggerQueue assertions all carry expected/actual detail.

## Coordination

A_MAIN continued directly on `/Users/dinghaolin/IdeaProjects/riftbound` local `main` per user request and created no subagent. External UI followup worktree `/Users/dinghaolin/MyProjects/riftbound-codex-ui-followup-20260616` on `codex/ui-followup-20260616` remained no-ahead at the opening check; after code commit `4094db5e`, `main...codex/ui-followup-20260616` divergence was `286 0`. `codex/rule-audit-remaining-20260615` had no commits ahead of `main`; after code commit `4094db5e`, divergence was `359 0`. Exact divergence must be rechecked before integration.

## Remaining Risk

This narrows one Blue Sentinel delayed-resource triggerQueue source location-zone diagnostic path only. It does not close broader command/recovery/random determinism, continuous-effect breadth, remaining triggerQueue keyed/detail breadth, full LayerEngine breadth, P0/P1, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` or final readiness. Project remains **NOT READY**.

# Stage 4D-223DR Teemo Location Zone Details Audit

Date: 2026-06-19 16:32 CST

Worktree: `/Users/dinghaolin/IdeaProjects/riftbound`

Branch: `main`

Code commit: `6d394c93` (`test: detail teemo location diagnostics`)

Status: accepted as a narrow P1-004/server runtime diagnostic shard. Project remains **NOT READY**.

## Scope

- Runtime changed: yes, diagnostic detail only in `src/Riftbound.Engine/MatchRecovery.cs`.
- Frontend changed: no.
- Rule behavior changed: no.
- Test coverage changed: yes, existing `MatchRecoveryTests` now require stable expected/actual suffixes for Teemo on-play self-power triggerQueue source location-zone drift across recovered snapshot timing, authoritative state and spectator replay timing surfaces.

## Runtime Detail

`MatchRecoveryValidator` already rejected Teemo on-play self-power triggerQueue entries whose visible source object location zone was not `BASE`. This shard preserves the existing diagnostic prefix and appends `FormatExpectedActualForRecovery("BASE", sourceZone)`, so the mismatch now reports the expected base zone and the recovered source location zone.

The change is diagnostic-only. It does not alter Teemo on-play self-power behavior, trigger id parsing, trigger queue construction, source-object visibility, object location validation, replay recovery, or any legal gameplay path.

## Rule Source

Rule source checked through `AGENTS.md`, `docs/CURRENT_CORE_RULE_PDF_READING_NOTES.md`, the five root PDFs and `/tmp/riftbound_rules_pdf_text/`: latest core rules 128, 129.3, 157.3, 157.3.a, 160-166, 303.2.a, 319-321, 323.4, 333-334, 382-383 and 808.1.d. No rules behavior changed.

## Validation

- `dotnet build src/Riftbound.Engine/Riftbound.Engine.csproj --no-restore` passed.
- `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~TeemoOnPlaySelfPowerSourceLocationContextDrift"` passed `4/4`.
- `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~MatchRecoveryTests&FullyQualifiedName~TriggerQueue"` passed `747/747`.
- `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~MatchRecoveryTests"` passed `1979/1979`.
- `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "Recovery|SpectatorReplay|Snapshot|Timing|ContinuousEffect|TriggerQueue|OrderTriggers|Trigger|Stack|Battle"` passed `3572/3572`.
- `dotnet test Riftbound.slnx --no-restore` passed `8316/8316`.
- `git diff --check` passed.
- Anchored conflict-marker scan over `src`, `tests` and `docs` had no findings.
- Residual Teemo source location-zone diagnostic assertion search found the selected triggerQueue assertions all carry expected/actual detail.

## Coordination

A_MAIN continued directly on `/Users/dinghaolin/IdeaProjects/riftbound` local `main` per user request and created no subagent. External UI followup worktree `/Users/dinghaolin/MyProjects/riftbound-codex-ui-followup-20260616` on `codex/ui-followup-20260616` remained no-ahead at the opening check; after code commit `6d394c93`, `main...codex/ui-followup-20260616` divergence was `293 0`. `codex/rule-audit-remaining-20260615` had no commits ahead of `main`; after code commit `6d394c93`, divergence was `366 0`. Exact divergence must be rechecked before integration.

## Remaining Risk

This narrows one Teemo on-play self-power triggerQueue source location-zone diagnostic path only. It does not close broader command/recovery/random determinism, continuous-effect breadth, remaining triggerQueue keyed/detail breadth, full LayerEngine breadth, P0/P1, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` or final readiness. Project remains **NOT READY**.

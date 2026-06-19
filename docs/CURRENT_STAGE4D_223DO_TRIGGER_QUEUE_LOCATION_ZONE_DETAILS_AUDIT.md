# Stage 4D-223DO Trigger Queue Location Zone Details Audit

Date: 2026-06-19 15:51 CST

Worktree: `/Users/dinghaolin/IdeaProjects/riftbound`

Branch: `main`

Code commit: `2e13d54c` (`test: detail trigger queue location diagnostics`)

Status: accepted as a narrow P1-004/server runtime diagnostic shard. Project remains **NOT READY**.

## Scope

- Runtime changed: yes, diagnostic detail only in `src/Riftbound.Engine/MatchRecovery.cs`.
- Frontend changed: no.
- Rule behavior changed: no.
- Test coverage changed: yes, `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs` now requires stable expected/actual suffixes for Kogmaw Last Breath triggerQueue battlefield location-zone mismatch diagnostics across recovered snapshot timing, authoritative state and spectator replay timing surfaces.

## Runtime Detail

`MatchRecoveryValidator` already rejected Kogmaw Last Breath triggerQueue entries whose trigger id battlefield object resolved to an object location outside the battlefield zone. This shard preserves the existing diagnostic prefix and appends `FormatExpectedActualForRecovery("BATTLEFIELD", battlefieldObjectZone)`, so the mismatch now reports `BATTLEFIELD` as expected and the recovered object-location zone as got.

The change is diagnostic-only. It does not alter Last Breath behavior, trigger id parsing, trigger queue construction, battlefield token validation, object location validation, source visibility redaction, replay recovery, or any legal gameplay path.

## Rule Source

Rule source checked through `AGENTS.md`, `docs/CURRENT_CORE_RULE_PDF_READING_NOTES.md`, the five root PDFs and `/tmp/riftbound_rules_pdf_text/`: latest core rules 128, 129.3, 157.3, 157.3.a, 160-166, 303.2.a, 319-321, 323.4, 333-334, 382-383 and 808.1.d. No rules behavior changed.

## Validation

- `dotnet build src/Riftbound.Engine/Riftbound.Engine.csproj --no-restore` passed.
- `dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~MatchRecoveryTests&FullyQualifiedName~TriggerQueue"` passed `744/744`.
- `dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~MatchRecoveryTests"` passed `1976/1976`.
- `dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Recovery|FullyQualifiedName~SpectatorReplay|FullyQualifiedName~Snapshot|FullyQualifiedName~Timing|FullyQualifiedName~ContinuousEffect|FullyQualifiedName~TriggerQueue|FullyQualifiedName~OrderTriggers|FullyQualifiedName~Trigger|FullyQualifiedName~Stack|FullyQualifiedName~Battle"` passed `3569/3569`.
- `dotnet test Riftbound.slnx --no-restore` passed `8313/8313`.
- `git diff --check` passed.
- Anchored conflict-marker scan over `src`, `tests` and `docs` had no findings.
- Residual Kogmaw location-zone diagnostic assertion search found the selected triggerQueue assertions all carry expected/actual detail.

## Coordination

A_MAIN continued directly on `/Users/dinghaolin/IdeaProjects/riftbound` local `main` per user request and created no subagent. External UI followup worktree `/Users/dinghaolin/MyProjects/riftbound-codex-ui-followup-20260616` on `codex/ui-followup-20260616` remained no-ahead at the opening check; after code commit `2e13d54c`, `main...codex/ui-followup-20260616` divergence was `278 0`. `codex/rule-audit-remaining-20260615` had no commits ahead of `main`; after code commit `2e13d54c`, divergence was `351 0`. Exact divergence must be rechecked before integration.

## Remaining Risk

This narrows one Kogmaw Last Breath triggerQueue location-zone diagnostic path only. It does not close broader command/recovery/random determinism, continuous-effect breadth, remaining triggerQueue keyed/detail breadth, full LayerEngine breadth, P0/P1, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` or final readiness. Project remains **NOT READY**.

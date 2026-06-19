# Stage 4D-223DV Standby Card Diagnostics Audit

Date: 2026-06-19 17:18 CST

Owner: `A_MAIN`

Worktree: `/Users/dinghaolin/IdeaProjects/riftbound`

Branch / commit: `main` code commit `8cac46fa`

## Scope

This shard tightens triggerQueue standby-card diagnostics in `MatchRecoveryValidator` without changing valid recovery replay behavior, trigger construction, trigger ordering, Standby rules, card typing or gameplay behavior.

The updated diagnostics preserve existing prefixes while appending stable expected/actual detail:

- Blue Sentinel delayed-resource source object must not be a standby card.
- OGS Lux high-cost spell source object must not be a standby card.
- Jhin movement-resource source object must not be a standby card.
- Kogmaw Last Breath source object must not be a standby card.
- Standard Last Breath source object must not be a standby card.
- Friendly-destroyed source object must not be a standby card.

The appended detail is `expected <non-standby card> but got 待命`.

## Files

- `src/Riftbound.Engine/MatchRecovery.cs`
- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`

## Validation

Passed:

- `dotnet build src/Riftbound.Engine/Riftbound.Engine.csproj --no-restore`
- `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~SourceVisibilityStateContextDrift"`: `20/20`
- `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~MatchRecoveryTests&FullyQualifiedName~TriggerQueue"`: `747/747`
- `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~MatchRecoveryTests"`: `1979/1979`
- `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~Recovery|FullyQualifiedName~SpectatorReplay|FullyQualifiedName~Snapshot|FullyQualifiedName~Timing|FullyQualifiedName~ContinuousEffect|FullyQualifiedName~TriggerQueue|FullyQualifiedName~OrderTriggers|FullyQualifiedName~Trigger|FullyQualifiedName~Stack|FullyQualifiedName~Battle"`: `3572/3572`
- `dotnet test Riftbound.slnx --no-restore`: `8317/8317`
- `git diff --check`
- Anchored conflict-marker scan over `docs`, `src` and `tests`
- Residual selected test assertion scan for standby-card diagnostics without the expected/actual suffix

## Rule Source

Checked `AGENTS.md`, `docs/CURRENT_CORE_RULE_PDF_READING_NOTES.md`, the five root PDFs and `/tmp/riftbound_rules_pdf_text/`. Relevant rule anchors remain the established trigger, visibility, public/private state and Standby set: latest core rules 128, 129.3, 157.3, 157.3.a, 160-166, 303.2.a, 319-321, 323.4, 333-334, 382-383 and 808.1.d. No rules behavior changed.

## Coordination

No subagent was created. A_MAIN continued directly on `/Users/dinghaolin/IdeaProjects/riftbound` local `main` per user request.

At the post-code-commit check, `main...origin/main` was `1 0`, `main...codex/ui-followup-20260616` was `307 0`, and `main...codex/rule-audit-remaining-20260615` was `380 0`; both external branches remained no-ahead. Exact divergence must be rechecked before future integration.

Project remains **NOT READY**.

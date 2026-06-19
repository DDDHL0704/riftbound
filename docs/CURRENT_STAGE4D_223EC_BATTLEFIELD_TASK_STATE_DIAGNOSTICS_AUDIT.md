# Stage 4D-223EC Battlefield Task State Diagnostics Audit

Date: 2026-06-19 18:53 CST

Owner: A_MAIN

Worktree: `/Users/dinghaolin/IdeaProjects/riftbound`

Branch / commit: `main` code commit `f7a20d3c` (`test: detail battlefield task state diagnostics`); docs checkpoint follows.

## Scope

This slice narrows P1-004 runtime/server recovery diagnostics for battlefield task state membership. It changes diagnostic detail only in `MatchRecoveryValidator`; valid battlefield task construction, battlefield state construction, object-tag checks, object locations, spectator redaction, snapshots and gameplay behavior are unchanged.

The affected missing battlefield-state diagnostics now preserve their existing prefixes while appending a stable expected/actual suffix:

- Snapshot timing battlefield task battlefield object id missing from battlefield states.
- Spectator replay timing battlefield task battlefield object id missing from authoritative state battlefield states.

The suffix reports the sorted known battlefield-state object id set, for example `expected [] but got ghost-battlefield`.

## Rule Anchors

Rule source was checked through `AGENTS.md`, `docs/CURRENT_CORE_RULE_PDF_READING_NOTES.md`, the five root PDFs and `/tmp/riftbound_rules_pdf_text/`. Relevant anchors remain:

- Latest core rules 107.2 and 107.3 for battlefield and standby-zone structure.
- Latest core rules 109 for public information in shared game zones.
- Latest core rules 120-130 for game objects, card privacy, card backs and face-up public card state.
- Latest core rules 141.1 and 144 for unit battlefield/base movement surfaces.
- Latest core rules 383.4 for triggered-skill battlefield context.

No rules behavior changed.

## Validation

Passed:

- `dotnet build src/Riftbound.Engine/Riftbound.Engine.csproj --no-restore`
- Focused `OutsideBattlefieldStates` filter: `2/2`
- Focused `MatchRecoveryTests` BattlefieldTask filter: `54/54`
- Changed-class `MatchRecoveryTests`: `1981/1981`
- Adjacent Recovery/SpectatorReplay/Snapshot/Timing/ContinuousEffect/TriggerQueue/OrderTriggers/Trigger/Stack/Battle filter: `3574/3574`
- Backend full `dotnet test Riftbound.slnx --no-restore`: `8319/8319`
- `git diff --check`
- Anchored conflict-marker scan over `src`, `tests`, and `docs`
- Residual battlefield task battlefield-state assertion search found no selected assertion without expected/actual detail

## Coordination

No subagent was created. A_MAIN continued directly on `/Users/dinghaolin/IdeaProjects/riftbound` local `main` per user request.

External UI followup worktree `/Users/dinghaolin/MyProjects/riftbound-codex-ui-followup-20260616` was clean/no-ahead at the opening check. After code commit `f7a20d3c`, `main...codex/ui-followup-20260616` divergence was `330 0`.

The historical `rule-audit-remaining-20260615` worktree path remains absent from `git worktree list` and `/Users/dinghaolin/MyProjects`, but the local branch remained no-ahead after code commit `f7a20d3c`; `main...codex/rule-audit-remaining-20260615` divergence was `403 0`.

Project remains **NOT READY**.

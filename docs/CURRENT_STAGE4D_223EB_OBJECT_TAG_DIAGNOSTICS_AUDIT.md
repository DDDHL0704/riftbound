# Stage 4D-223EB Object Tag Diagnostics Audit

Date: 2026-06-19 18:39 CST

Owner: A_MAIN

Worktree: `/Users/dinghaolin/IdeaProjects/riftbound`

Branch / commit: `main` code commit `03f1400d` (`test: detail object tag diagnostics`); docs checkpoint follows.

## Scope

This slice narrows P1-004 runtime/server recovery diagnostics for object-tag index gaps. It changes diagnostic detail only in `MatchRecoveryValidator`; valid object tags, battlefield task membership, Kogmaw Last Breath trigger construction, object visibility, trigger ordering, snapshots and gameplay behavior are unchanged.

The affected missing object-tag diagnostics now preserve their existing prefixes while appending a stable expected/actual suffix:

- Snapshot timing battlefield task battlefield object id missing from object tags.
- Snapshot timing battlefield task participant object id missing from object tags.
- Snapshot timing triggerQueue Kogmaw Last Breath battlefield object id missing from object tags.

The suffix reports the sorted object ids currently present in the object-tag index, for example `expected contains battlefield-a but got [participant-a]`.

## Rule Anchors

Rule source was checked through `AGENTS.md`, `docs/CURRENT_CORE_RULE_PDF_READING_NOTES.md`, the five root PDFs and `/tmp/riftbound_rules_pdf_text/`. Relevant anchors remain:

- Latest core rules 108.7.c-d and 109 for hidden/public information boundaries.
- Latest core rules 120-130 for game objects, card privacy, card backs and face-up public card state.
- Latest core rules 383.4 trigger categories for triggered-skill timing context.
- Latest core rules 808.1.c-d for Last Breath trigger conditions.

No rules behavior changed.

## Validation

Passed:

- `dotnet build src/Riftbound.Engine/Riftbound.Engine.csproj --no-restore`
- Focused `ObjectTagContextDrift` filter: `3/3`
- Focused `MatchRecoveryTests` BattlefieldTask filter: `54/54`
- Focused `MatchRecoveryTests` TriggerQueue filter: `747/747`
- Changed-class `MatchRecoveryTests`: `1981/1981`
- Adjacent Recovery/SpectatorReplay/Snapshot/Timing/ContinuousEffect/TriggerQueue/OrderTriggers/Trigger/Stack/Battle filter: `3574/3574`
- Backend full `dotnet test Riftbound.slnx --no-restore`: `8319/8319`
- `git diff --check`
- Anchored conflict-marker scan over `src`, `tests`, and `docs`
- Residual object-tags assertion search found no selected object-tag missing assertion without expected/actual detail

## Coordination

No subagent was created. A_MAIN continued directly on `/Users/dinghaolin/IdeaProjects/riftbound` local `main` per user request.

External UI followup worktree `/Users/dinghaolin/MyProjects/riftbound-codex-ui-followup-20260616` was clean/no-ahead at the opening check. After code commit `03f1400d`, `main...codex/ui-followup-20260616` divergence was `325 0`.

The historical `rule-audit-remaining-20260615` worktree path remains absent from `git worktree list` and `/Users/dinghaolin/MyProjects`, but the local branch remained no-ahead after code commit `03f1400d`; `main...codex/rule-audit-remaining-20260615` divergence was `398 0`.

Project remains **NOT READY**.

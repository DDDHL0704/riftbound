# Stage 4D-223DC Spectator Trigger Queue Identity Redaction Details Audit

Date: 2026-06-19 12:40 CST

Status: accepted on local `main` as code commit `bc74f809`; docs checkpoint follows. Project remains **NOT READY**.

## Scope

A_MAIN directly integrated a narrow runtime diagnostic shard in `/Users/dinghaolin/IdeaProjects/riftbound` on `main`.

- Runtime changed: yes, diagnostic detail only.
- Frontend changed: no.
- Files touched by the code commit: `src/Riftbound.Engine/MatchRecovery.cs`, `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- `MatchRecoveryValidator` now appends stable expected/actual details to trigger-queue identity redaction sentinel diagnostics while preserving the existing diagnostic prefixes.
- Covered diagnostics:
  - trigger id must not be redacted: `expected <non-HIDDEN> but got HIDDEN`.
  - controller id must not be redacted: `expected <non-HIDDEN> but got HIDDEN`.
  - triggered event kind must not be redacted: `expected <non-HIDDEN> but got HIDDEN`.

## Rule Source

Checked the user-supplied root PDF gate through `docs/CURRENT_CORE_RULE_PDF_READING_NOTES.md` and `/tmp/riftbound_rules_pdf_text/core_rules_260330.txt`.

- Latest core rules 128 and 129.3 for private/hidden card information and card-back hiding.
- Latest core rules 157.3 and 157.3.a for completing spell/skill resolution before pending trigger/task handling.
- Latest core rules 303.2.a, 319-321, 323.4, 333-334 and 382-383 for triggered-skill timing, pending task/stack placement and simultaneous trigger ordering.
- Latest core rule 808.1.d for Last Breath pending-item/source snapshot context.

No rules behavior changed.

## Validation

Passed before docs sync:

- Focused triggerQueue identity-redaction tests: `8/8`.
- Focused spectator replay timing triggerQueue shard: `525/525`.
- Changed-class `MatchRecoveryTests`: `1976/1976`.
- Adjacent `Recovery|SpectatorReplay|Snapshot|Timing|ContinuousEffect|TriggerQueue|OrderTriggers|Trigger|Stack|Battle` filter: `3569/3569`.
- Backend full via `dotnet test Riftbound.slnx`: `8311/8311`.
- `git diff --check`.
- Anchored conflict-marker scan over `src`, `tests`, `docs`.

## Coordination

- No subagent was created.
- A_MAIN continued directly on `/Users/dinghaolin/IdeaProjects/riftbound` local `main` per user request.
- External UI followup worktree `/Users/dinghaolin/MyProjects/riftbound-codex-ui-followup-20260616` remained clean at `01364ee2`; `main...codex/ui-followup-20260616` was `221 0` after code commit `bc74f809`.
- `codex/rule-audit-remaining-20260615` had no commits ahead of `main`; divergence was `294 0` from local `main`.

## Non-Goals

This slice does not change valid recovery replay behavior, trigger construction, trigger ordering, hidden-source redaction behavior, stack placement, pending item resolution, priority/focus, payment, legality, battle, prompt rendering, authoritative state serialization, random determinism, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial`, L2P-RG-005 or final readiness.

## Next

Continue remaining trigger queue keyed/detail edge diagnostics, recovered/spectator/authoritative nested payload breadth, recovery timing remaining breadth, battle assignment remaining matrix breadth, remaining raw/mapper/protocol surfaces, or another higher-priority P0/P1 server audit surface after re-reading the coordination board, `AGENTS.md`, and the PDF gate, while checking `codex/ui-followup-20260616` and `codex/rule-audit-remaining-20260615` before integration.

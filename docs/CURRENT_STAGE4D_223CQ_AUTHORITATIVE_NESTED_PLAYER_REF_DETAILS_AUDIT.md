# Stage 4D-223CQ Authoritative Nested Player-Reference Detail Audit

Date: 2026-06-19 09:17 CST

Owner: A_MAIN

Worktree: `/Users/dinghaolin/IdeaProjects/riftbound`

Branch / commit: `main` at code commit `56184c64`

Project status: **NOT READY**

## Scope

A_MAIN continued directly on local `main` and accepted a narrow recovery validation diagnostic-detail shard. Runtime changed: yes, diagnostic detail only. Frontend changed: no.

`MatchRecoveryValidator` now appends stable expected/actual details to authoritative-state nested player-reference diagnostics while preserving the existing missing-player prefixes. Covered surfaces:

- card object owner and controller player ids
- object location player ids
- stack item and trigger queue controller player ids
- pending payment, pending hand choice and temporary payment resource player ids
- battlefield resolution player, previous-controller and controller player ids
- battle resolution attacking, defending and winner player ids

The validator still performs the same seat membership checks. This checkpoint only improves rejected authoritative-state payload diagnostics by reporting the sorted known seat-player set and the offending player id.

## Files Touched

- `src/Riftbound.Engine/MatchRecovery.cs`
- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`

## Validation

Passed:

- Focused authoritative-state nested player-reference detail tests: `4/4`
- Changed-class `MatchRecoveryTests`: `1974/1974`
- Adjacent Recovery/AuthoritativeState/Player/Seat/CardObject/ObjectLocation/Stack/TriggerQueue/PendingPayment/PendingHandChoice/TemporaryPayment/BattlefieldResolution/BattleResolution filter: `3029/3029`
- Backend full via `Riftbound.slnx`: `8307/8307`
- Code/test `git diff --check`
- Code/test/docs anchored conflict-marker scan had no findings
- PCRE2 residual check found no selected authoritative nested missing-player assertions without expected/actual details before docs sync

## Rule Source

Rule source checked through `docs/rules-authority-and-audit.md`, `docs/rules-evidence-index.md`, `docs/CURRENT_CORE_RULE_PDF_READING_NOTES.md` and `/tmp/riftbound_rules_pdf_text/` from the five root PDFs.

Relevant anchors re-read for this slice: latest core rules 107-129 for player zones, hidden/private/public boundaries and card-back redaction; 333 for stack creator / priority context; 382-383 for triggered-skill controller and ordering context; 401-405 for active/triggered skill controller, choices and costs; 454-461 for battle participant, controller and result player context; and 649-652 for surrender, player removal and removed-player inability to affect the game.

## Coordination

No subagent was created. A_MAIN continued in `/Users/dinghaolin/IdeaProjects/riftbound` on local `main` per user request.

External UI followup worktree `/Users/dinghaolin/MyProjects/riftbound-codex-ui-followup-20260616` on `codex/ui-followup-20260616` was clean at `01364ee2`, `155` commits behind current local `main` and `0` commits ahead at the pre-docs-sync divergence check. Exact divergence must be rechecked before integration.

`codex/rule-audit-remaining-20260615` had no commits ahead of `main`; divergence was `228 0` from `main...codex/rule-audit-remaining-20260615`.

## Non-Goals

This narrows authoritative-state nested player-reference detail diagnostics only. It does not change valid recovery replay behavior, player identity normalization, seat validation, trigger ordering, hidden-source redaction, hidden standby redaction, battlefield lane semantics, stack placement, skill activation/trigger confirmation, payment, legality, battle creation, battle damage assignment computation, damage legality, battle cleanup, battlefield control, prompt rendering, source-object serialization, authoritative state serialization, random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial`, L2P-RG-005 or final readiness.

Project remains **NOT READY**.

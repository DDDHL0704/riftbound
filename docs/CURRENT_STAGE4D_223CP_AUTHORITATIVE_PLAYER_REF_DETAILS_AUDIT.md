# Stage 4D-223CP Authoritative Player-Reference Detail Audit

Date: 2026-06-19 09:03 CST

Owner: A_MAIN

Worktree: `/Users/dinghaolin/IdeaProjects/riftbound`

Branch / commit: `main` at code commit `1afd3d70`

Project status: **NOT READY**

## Scope

A_MAIN continued directly on local `main` and accepted a narrow recovery validation diagnostic-detail shard. Runtime changed: yes, diagnostic detail only. Frontend changed: no.

`MatchRecoveryValidator` now appends stable expected/actual details to authoritative-state top-level player-reference diagnostics while preserving the existing missing-player prefixes. Covered surfaces:

- required player pointers: active player and turn player
- optional player pointers: priority player, focus player, winner player, opening second action player and extra turn player
- player-id lists: ready player, passed priority player, passed focus player, destroyed unit owner and mulligan completed player
- player-keyed maps: rune pool player, zone player, score player, experience player, cards played player and decklist player

The validator still performs the same seat membership checks. This checkpoint only improves rejected authoritative-state payload diagnostics by reporting the sorted known seat-player set and the offending player id.

## Files Touched

- `src/Riftbound.Engine/MatchRecovery.cs`
- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`

## Validation

Passed:

- Focused authoritative-state player-reference detail tests: `4/4`
- Changed-class `MatchRecoveryTests`: `1974/1974`
- Adjacent Recovery/AuthoritativeState/Player/Seat/Snapshot/SpectatorReplay/TriggerQueue/Stack/Battle/Pending/TemporaryPayment filter: `3752/3752`
- Backend full via `Riftbound.slnx`: `8307/8307`
- Code/test `git diff --check` before code commit
- Code/test anchored conflict-marker scan had no findings before code commit
- PCRE2 residual check found no selected authoritative top-level missing-player assertions without expected/actual details before docs sync

## Rule Source

Rule source checked through `docs/rules-authority-and-audit.md`, `docs/rules-evidence-index.md`, `docs/CURRENT_CORE_RULE_PDF_READING_NOTES.md` and `/tmp/riftbound_rules_pdf_text/` from the five root PDFs.

Relevant anchors re-read for this slice: latest core rules 107-129 for player snapshot visibility and hidden/private boundaries, 307-313 for turn state / priority / focus player identity, 315-317 for readying, turn player and end-of-turn player context, 333-340 for stack/pass timing context, 382-383 and 401-405 for triggered/active skill controller context, 454-461 for battle participant/controller context, and 649-652 for player removal / inability to affect the game after removal.

## Coordination

No subagent was created. A_MAIN continued in `/Users/dinghaolin/IdeaProjects/riftbound` on local `main` per user request.

External UI followup worktree `/Users/dinghaolin/MyProjects/riftbound-codex-ui-followup-20260616` on `codex/ui-followup-20260616` was clean at `01364ee2`, `150` commits behind current local `main` and `0` commits ahead at the pre-docs-sync divergence check. Exact divergence must be rechecked before integration.

`codex/rule-audit-remaining-20260615` had no commits ahead of `main`; divergence was `223 0` from `main...codex/rule-audit-remaining-20260615`.

## Non-Goals

This narrows authoritative-state top-level player-reference detail diagnostics only. It does not change valid recovery replay behavior, player identity normalization, seat validation, trigger ordering, hidden-source redaction, hidden standby redaction, battlefield lane semantics, stack placement, skill activation/trigger confirmation, payment, legality, battle creation, battle damage assignment computation, damage legality, battle cleanup, battlefield control, prompt rendering, source-object serialization, authoritative state serialization, random determinism, remaining authoritative-state object/card/stack/trigger/pending/battle-resolution player diagnostics, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial`, L2P-RG-005 or final readiness.

Project remains **NOT READY**.

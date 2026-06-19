# Stage 4D-223CX Spectator Object Location Details Audit

Date: 2026-06-19

Status: accepted on local `main` as code commit `cb55b127`; docs checkpoint `0c54810a`; latest post-merge sync followed as merge commit `263ea31b`. Project remains **NOT READY**.

## Scope

A_MAIN continued directly in `/Users/dinghaolin/IdeaProjects/riftbound` on `main`. Before the runtime slice, local `main` fast-forwarded remote `main` commit `390adec3` (`增加线框对象上下文投影`), accepting DevUi object-context projection and QA artifact updates.

Runtime changed: yes, diagnostic detail only. Frontend changed: yes, only through the accepted remote DevUi fast-forward; this A_MAIN code slice made no frontend edits.

`MatchRecoveryValidator` now appends stable expected/actual details to spectator replay frame snapshot visible player object location mismatch diagnostics while preserving the existing diagnostic prefixes. Covered fields are the parent `location` object, `location.playerId`, `location.zone` and `location.battlefieldObjectId` on both the normal visible-object path and the extra-object visibility-mismatch path.

The detail path reports complete expected locations as `{playerId: ..., zone: ..., battlefieldObjectId: ...}` and reports actual unreadable or absent payloads as `<unreadable>` / `<missing>`. Empty battlefield ids are represented as `<empty>`.

## Rule Source

Rule source was checked through `docs/CURRENT_CORE_RULE_PDF_READING_NOTES.md` and `/tmp/riftbound_rules_pdf_text/core_rules_260330.txt` from the five root PDFs.

Relevant latest core-rule anchors: 107 for public zones including base/battlefield; 108.7 for hand privacy and public hand size; 128 for private information; 129 for card backs, private and face-down cards; 355.9.a.3 for face-down cards; 355.10 for public target zones; and 421 for standby face-down handling.

This slice changes diagnostics only. It does not change object location construction, redaction, hidden/private hand or deck behavior, face-down standby behavior, public zone construction, snapshot construction or replay semantics.

## Validation

- Focused spectator object location detail tests: `8/8`.
- Changed-class `MatchRecoveryTests`: `1976/1976`.
- Adjacent Recovery/SpectatorReplay/Snapshot/Player/Object/Zone/Location/Seat/AuthoritativeState/Timing/TriggerQueue/Stack/Battle filter: `3763/3763`.
- DevUi build after accepting remote `390adec3`: passed, including event-label, user-facing text, tabletop layout, wire-table layout, strict typecheck and Vite production build.
- Backend full via `Riftbound.slnx`: `8310/8310`.
- `git diff --check` passed before code commit.
- Anchored conflict-marker scan over `src`/`tests`/`docs` had no findings.
- Residual search found no selected spectator object location mismatch diagnostics without expected/actual details before docs sync.

## Coordination

External UI followup worktree `/Users/dinghaolin/MyProjects/riftbound-codex-ui-followup-20260616` on `codex/ui-followup-20260616` was clean at `01364ee2`; `main...codex/ui-followup-20260616` was `199 0` at the pre-docs-sync check. A_MAIN must inspect that worktree before integration and must not develop directly there.

`codex/rule-audit-remaining-20260615` had no commits ahead of `main`; divergence was `272 0` from local `main` at the pre-docs-sync check.

## Non-Goals

This does not close valid recovery replay behavior, object location construction, hidden/private hand/deck redaction, hidden standby redaction, public zone construction, snapshot construction, player identity normalization, seat validation, trigger ordering, battlefield lane semantics, stack placement, skill activation or trigger confirmation, payment, legality, battle creation, battle damage assignment computation, damage legality, battle cleanup, battlefield control, prompt rendering, source-object serialization, authoritative state serialization, random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial`, L2P-RG-005 or final status.

## Next

Continue the next executable server slice directly on `/Users/dinghaolin/IdeaProjects/riftbound` `main` after re-reading this board, `AGENTS.md`, the PDF gate, the UI followup worktree status and `codex/rule-audit-remaining-20260615`.

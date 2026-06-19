# Stage 4D-223CW Spectator Object Details Audit

Date: 2026-06-19

Status: accepted on local `main` as code commit `aa74ed46`; docs checkpoint follows. Project remains **NOT READY**.

## Scope

A_MAIN continued directly in `/Users/dinghaolin/IdeaProjects/riftbound` on `main`. This slice narrows spectator replay frame snapshot visible player object diagnostics only.

Runtime changed: yes, diagnostic detail only. Frontend changed: no.

`MatchRecoveryValidator` now appends stable expected/actual details to spectator replay frame snapshot visible player object scalar/list mismatch diagnostics while preserving the existing diagnostic prefixes. Covered object fields are card number / `cardNo`, `ownerId`, `controllerId`, `attachedToObjectId`, `damage`, `power`, `basePower`, `effectivePower`, `untilEndOfTurnPowerModifier`, `manaCost`, `isExhausted`, `isAttacking`, `isDefending`, `tags` and `untilEndOfTurnEffects`.

The detail path also covers unreadable object payloads by reporting `<unreadable>` as the actual value. This makes visible-object parity failures actionable without changing valid recovery replay behavior.

## Rule Source

Rule source was checked through `docs/CURRENT_CORE_RULE_PDF_READING_NOTES.md` and `/tmp/riftbound_rules_pdf_text/core_rules_260330.txt` from the five root PDFs.

Relevant latest core-rule anchors: 107 for public zones including base/battlefield; 108.7 for hand privacy and public hand size; 128 for private information; 129 for card backs, private and face-down cards; 355.9.a.3 for face-down cards; 355.10 for public target zones; and 421 for standby face-down handling.

This slice changes diagnostics only. It does not change redaction, hidden/private hand or deck behavior, face-down standby behavior, public zone construction, snapshot construction or replay semantics.

## Validation

- Focused spectator visible object detail tests: `4/4`.
- Changed-class `MatchRecoveryTests`: `1976/1976`.
- Adjacent Recovery/SpectatorReplay/Snapshot/Player/Object/Zone/Seat/AuthoritativeState/Timing/TriggerQueue/Stack/Battle filter: `3760/3760`.
- Backend full via `Riftbound.slnx`: `8310/8310`.
- `git diff --check` passed before code commit.
- Anchored conflict-marker scan over `src`/`tests`/`docs` had no findings.
- Residual search found no selected spectator player object mismatch diagnostics without expected/actual details before docs sync.

## Coordination

External UI followup worktree `/Users/dinghaolin/MyProjects/riftbound-codex-ui-followup-20260616` on `codex/ui-followup-20260616` was clean at `01364ee2`; `main...codex/ui-followup-20260616` was `193 0` at the pre-docs-sync check. A_MAIN must inspect that worktree before integration and must not develop directly there.

`codex/rule-audit-remaining-20260615` had no commits ahead of `main`; divergence was `266 0` from local `main` at the pre-docs-sync check.

## Non-Goals

This does not close valid recovery replay behavior, hidden/private hand/deck redaction, hidden standby redaction, public zone construction, snapshot construction, player identity normalization, seat validation, trigger ordering, battlefield lane semantics, stack placement, skill activation or trigger confirmation, payment, legality, battle creation, battle damage assignment computation, damage legality, battle cleanup, battlefield control, prompt rendering, source-object serialization, authoritative state serialization, random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial`, L2P-RG-005 or final status.

## Next

Continue the next executable server slice directly on `/Users/dinghaolin/IdeaProjects/riftbound` `main` after re-reading this board, `AGENTS.md`, the PDF gate, the UI followup worktree status and `codex/rule-audit-remaining-20260615`.

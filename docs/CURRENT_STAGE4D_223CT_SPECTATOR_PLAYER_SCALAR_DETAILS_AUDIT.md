# Stage 4D-223CT Spectator Player Scalar Details Audit

Date: 2026-06-19
Owner: A_MAIN
Worktree: `/Users/dinghaolin/IdeaProjects/riftbound`
Branch: `main`
Code commit: `b13ec9d3`

## Scope

223CT accepted one narrow runtime validation shard for spectator replay frame snapshot player scalar diagnostics.

Runtime changed: yes, diagnostic detail only. Frontend changed: no.

`MatchRecoveryValidator` now appends stable `expected ... but got ...` details when spectator replay frame snapshot player scalar values differ from authoritative state or the player map key. Covered scalar fields:

- `id`
- `name`
- `ready`
- `handSize`
- `score`
- `experience`
- `cardsPlayedThisTurn`
- `deckSubmitted`
- `mulliganCompleted`

The existing diagnostic prefixes are preserved so broad callers and existing log matching remain stable.

## Rule Source

Rule source was checked through `docs/CURRENT_CORE_RULE_PDF_READING_NOTES.md` and `/tmp/riftbound_rules_pdf_text/` from the five root PDFs. Relevant standing anchors remain latest core rules 107-129 for player/card visibility and public snapshot boundaries plus 649-652 for player removal / remaining-player continuity context.

This slice changes only recovery validator diagnostics. It does not change legal game behavior, snapshot construction, player identity normalization, readiness/deck/mulligan semantics, hand-size privacy, scoring, experience, card-play counters, event replay, random determinism, or protocol shape.

## Validation

- Focused: `RecoveryValidatorRejectsSpectatorReplaySnapshotPlayerScalarMismatch` passed `1/1`.
- Changed class: `MatchRecoveryTests` passed `1976/1976`.
- Adjacent filter `Recovery|SpectatorReplay|Snapshot|Player|Seat|AuthoritativeState|Timing|TriggerQueue|Stack|Battle` passed `3722/3722`.
- Backend full: `dotnet test Riftbound.slnx` passed `8309/8309`.
- `git diff --check` passed.
- Anchored conflict-marker scan `rg -n "^(<<<<<<<|=======|>>>>>>>)" src tests docs` had no findings.
- Selected residual search found no spectator replay frame snapshot player scalar mismatch diagnostics in the chosen set without expected/actual details.

## Coordination

No subagent was created. A_MAIN continued directly on `/Users/dinghaolin/IdeaProjects/riftbound` local `main` per user request.

External UI followup worktree `/Users/dinghaolin/MyProjects/riftbound-codex-ui-followup-20260616` on `codex/ui-followup-20260616` was clean at `01364ee2`; `main...codex/ui-followup-20260616` was `168 0` at the pre-docs-sync check. A_MAIN must inspect that worktree before integrating UI followup changes and must not develop directly there.

`codex/rule-audit-remaining-20260615` had no commits ahead of `main`; `main...codex/rule-audit-remaining-20260615` was `241 0` at the pre-docs-sync check.

## Status

This narrows spectator replay player scalar diagnostic detail only. Remaining recovered/spectator/authoritative nested payload breadth, recovery timing breadth, battle assignment matrix breadth, raw/mapper/protocol surfaces, full LayerEngine breadth, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial`, L2P-RG-005 and final readiness remain open.

Project remains **NOT READY**.

# Stage 4D-223CV Spectator Zone Details Audit

Date: 2026-06-19
Owner: A_MAIN
Worktree: `/Users/dinghaolin/IdeaProjects/riftbound`
Branch: `main`
Code commit: `3631a8c2`

## Scope

223CV accepted one narrow runtime validation shard for spectator replay frame snapshot player zone diagnostics.

Runtime changed: yes, diagnostic detail only. Frontend changed: no.

`MatchRecoveryValidator` now appends stable `expected ... but got ...` details when spectator replay frame snapshot player zone payloads differ from authoritative state. Covered fields:

- `mainDeckCount`
- `runeDeckCount`
- redacted `hand`
- `handHidden`
- `base`
- `battlefields`
- hidden standby battlefield count
- `graveyard`
- `banished`
- `legendZone`
- `championZone`

The existing diagnostic prefixes are preserved so broad log matching remains stable. Public zone list details use the shared recovery diagnostic formatter, including stable object-id ordering from the authoritative snapshot.

## Rule Source

Rule source was checked through `docs/CURRENT_CORE_RULE_PDF_READING_NOTES.md` and `/tmp/riftbound_rules_pdf_text/core_rules_260330.txt` from the five root PDFs. Relevant standing anchors remain latest core rules 108.7 for hand privacy and public hand size, 128 and 129 for card privacy and backs, 355.9.a.3 plus 355.10.a / 355.10.a.1 for face-down cards and public target zones, and 421 for Standby face-down placement.

This slice changes only recovery validator diagnostics. It does not change hidden/private hand or deck redaction, face-down standby redaction, public zone construction, snapshot construction, recovery replay semantics, protocol shape or gameplay rules behavior.

## Validation

- Focused: `RecoveryValidatorRejectsSpectatorReplaySnapshotPlayerZoneMismatch` passed `1/1`.
- Changed class: `MatchRecoveryTests` passed `1976/1976`.
- Adjacent filter `Recovery|SpectatorReplay|Snapshot|Player|Zone|Object|Seat|AuthoritativeState|Timing|TriggerQueue|Stack|Battle` passed `3760/3760`.
- Backend full: `dotnet test Riftbound.slnx` passed `8310/8310`.
- `git diff --check` passed.
- Anchored conflict-marker scan `rg -n "^(<<<<<<<|=======|>>>>>>>)" src tests docs` had no findings.
- Selected residual search found no selected spectator replay frame snapshot player zone diagnostics without expected/actual details.

## Coordination

No subagent was created. A_MAIN continued directly on `/Users/dinghaolin/IdeaProjects/riftbound` local `main` per user request.

External UI followup worktree `/Users/dinghaolin/MyProjects/riftbound-codex-ui-followup-20260616` on `codex/ui-followup-20260616` was clean at `01364ee2`; `main...codex/ui-followup-20260616` was `188 0` at the pre-docs-sync check. A_MAIN must inspect that worktree before integrating UI followup changes and must not develop directly there.

`codex/rule-audit-remaining-20260615` had no commits ahead of `main`; `main...codex/rule-audit-remaining-20260615` was `261 0` at the pre-docs-sync check.

## Status

This narrows spectator replay player zone diagnostic detail only. Remaining recovered/spectator/authoritative nested payload breadth, recovery timing breadth, battle assignment matrix breadth, raw/mapper/protocol surfaces, full LayerEngine breadth, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial`, L2P-RG-005 and final readiness remain open.

Project remains **NOT READY**.

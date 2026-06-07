# Stage 4D-191H-191M Raw Replay Audit

Date: 2026-06-07 11:01 CST

Owner: A_MAIN

Status: accepted. Project remains **NOT READY**.

## Scope

- 191H added reordered stale raw `PLAY_CARD` rejected-cache replay coverage for Rek'Sai no-optional haste/overwhelm.
- 191I added reordered stale raw `PLAY_CARD` rejected-cache replay coverage for Berserk Impulse.
- 191J added reordered stale raw `PLAY_CARD` rejected-cache replay coverage for Vengeance.
- 191K added reordered stale raw `PLAY_CARD` rejected-cache replay coverage for Draven keyword unit.
- 191L added reordered stale raw `PLAY_CARD` rejected-cache replay coverage for Ride the Wind.
- 191M added reordered stale raw `PLAY_CARD` rejected-cache replay coverage for Edge of Night direct play.

Runtime changed: no. Server test coverage only. These edits strengthen existing stale replay tests rather than adding new test methods, so the full conformance test count remains unchanged.

## Commits

- 191H source `b5fb9067` -> main `27265138`
- 191I source `f0b64068` -> main `4d35f650`
- 191J source `6b220c4a` -> main `55dab2e3`
- 191K source `e6f2f6f8` -> main `f1a60e01`
- 191L source `879608cd` -> main `33d43ce8`
- 191M source `b9a9f618` -> main `1bb8a072`

## Validation

- Worktree focused tests: `1/1` for each 191H through 191M.
- Main focused batch: `6/6`.
- Main changed-class filter: `79/79`.
- Main adjacent raw/replay/client-intent filter: `980/980`.
- Backend full conformance project: `7577/7577`.
- `git diff --check`: passed before docs.

## Coordination Notes

- Workers remained patch-only and did not stage, commit, push or edit docs.
- A_MAIN reviewed the worker diffs, corrected 191H/191L/191M so their semantic helper still preserves strict standard raw property-order assertions by default, and then reran focused tests for those slices.
- A_MAIN handled focused validation, source commits, cherry-picks, main validation and docs checkpointing.

## Remaining Open

This closes only a narrow reordered stale raw `PLAY_CARD` replay audit slice. Broader P0/P1, command/recovery/random determinism, recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final readiness remain open.

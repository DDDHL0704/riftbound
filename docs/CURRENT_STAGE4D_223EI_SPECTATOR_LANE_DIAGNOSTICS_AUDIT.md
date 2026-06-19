# Stage 4D-223EI Spectator Lane Diagnostics Audit

Date: 2026-06-19 20:46 CST

Owner: A_MAIN

Worktree: `/Users/dinghaolin/IdeaProjects/riftbound`

Branch: `main`

Code commit: `7f1b13f5` (`test: detail spectator lane diagnostics`)

## Summary

223EI tightens spectator replay snapshot lane aggregate diagnostics in `MatchRecoveryValidator`.

Runtime changed: yes, diagnostic detail only. Valid lane construction, battlefield state construction, spectator redaction, hidden standby redaction, snapshot construction, object locations and gameplay behavior are unchanged.

The updated diagnostics preserve their existing prefixes while appending stable expected/actual detail for:

- spectator replay frame snapshot lane battlefield count
- spectator replay frame snapshot lane battlefield object id player/object pairs
- spectator replay frame snapshot lane battlefield ids

Representative examples now include:

- `expected 3 but got 1`
- `expected [alice:battlefield-a, alice:alice-unit-a, bob:bob-unit-a] but got [bob:wrong-object, alice:alice-unit-a, bob:bob-unit-a]`
- `expected [battlefield-a] but got [wrong-battlefield]`

## Rule Source

Checked `AGENTS.md`, the five root PDFs and `/tmp/riftbound_rules_pdf_text/` before the slice. Relevant current core-rule surfaces include 107.2 battlefield zones, 107.3 standby zones, 108.7 hand privacy, 109 public shared-zone objects, 120-130 game objects/privacy/card backs, 128 privacy levels and 129 card backs. This slice changes diagnostics only and does not change rules behavior.

## Validation

- `dotnet build src/Riftbound.Engine/Riftbound.Engine.csproj --no-restore`
- focused lane aggregate diagnostics tests: `2/2`
- focused `SpectatorReplaySnapshotLane` filter: `15/15`
- changed-class `MatchRecoveryTests`: `1981/1981`
- adjacent Recovery/SpectatorReplay/Snapshot/Lane/Battlefield/Object/Zone/Location/Timing/TriggerQueue/Stack/Battle filter: `3501/3501`
- backend full via `Riftbound.slnx`: `8319/8319`
- `git diff --check`
- anchored conflict-marker scan over `src`, `tests`, `docs`
- residual selected old-format lane aggregate assertion scan

## Coordination

No subagent was created. A_MAIN continued directly on `/Users/dinghaolin/IdeaProjects/riftbound` local `main` per user request.

External UI followup worktree `/Users/dinghaolin/MyProjects/riftbound-codex-ui-followup-20260616` remained clean and no-ahead. After code commit `7f1b13f5`, `main...codex/ui-followup-20260616` divergence was `366 0`.

The historical `rule-audit-remaining-20260615` worktree path remains absent, and local branch `codex/rule-audit-remaining-20260615` remained no-ahead with divergence `439 0`.

## Next

Project remains **NOT READY**. Next executable server slice can continue remaining missing battlefield/lane diagnostics, triggerQueue keyed/detail edge diagnostics, recovered/spectator/authoritative nested payload breadth, recovery timing remaining breadth, battle assignment remaining matrix breadth, raw/mapper/protocol surfaces, or another higher-priority P0/P1 server audit surface after re-reading the board, `AGENTS.md`, PDF gate, UI followup and `codex/rule-audit-remaining-20260615`.

# Stage 4D-223EK Pending Task Queue Diagnostics Audit

Date: 2026-06-19 21:16 CST

Owner: A_MAIN

Worktree: `/Users/dinghaolin/IdeaProjects/riftbound`

Branch: `main`

Code commit: `55792eaf` (`test: detail pending task queue diagnostics`)

## Summary

223EK tightens recovered snapshot timing pending task queue self-consistency diagnostics in `MatchRecoveryValidator`.

Runtime changed: yes, diagnostic detail only. Valid pending task queue construction, cleanup task semantics, timing snapshots, spectator replay redaction and gameplay behavior are unchanged.

The updated diagnostics preserve their existing prefixes while appending stable expected/actual detail for:

- recovered snapshot timing pending task queue dangling active task id
- recovered snapshot timing pending task queue missing active task id when tasks are present
- recovered snapshot timing pending task queue metadata task count mismatch
- recovered snapshot timing pending task queue metadata state-based task kind mismatch
- recovered and spectator timing pending task queue has-tasks and blocking flag task-count mismatches

Representative examples now include:

- `expected [task-1, task-2] but got task-missing`
- `expected [task-1] but got <empty>`
- `expected 2 but got 1`
- `expected [DESTROY_LETHAL_UNIT, RECALL_UNATTACHED_EQUIPMENT] but got [DESTROY_LETHAL_UNIT]`
- `expected true but got false`

## Rule Source

Checked `AGENTS.md`, the five root PDFs and `/tmp/riftbound_rules_pdf_text/` before the slice. Relevant current core-rule surfaces include 120-130 game objects/privacy/card backs and 383.4 triggered-skill families. This slice changes diagnostics only and does not change rules behavior.

## Validation

- `dotnet build src/Riftbound.Engine/Riftbound.Engine.csproj --no-restore`
- focused `SnapshotTimingPendingTaskQueue` tests: `14/14`
- changed-class `MatchRecoveryTests`: `1981/1981`
- adjacent Recovery/SpectatorReplay/Snapshot/Timing/PendingTask/TriggerQueue/Stack/BattlefieldTask/Battlefield/Battle filter: `3427/3427`
- backend full via `Riftbound.slnx`: `8319/8319`
- `git diff --check`
- anchored conflict-marker scan over `src`, `tests`, `docs`
- residual selected old-format pending task queue assertion scan

## Coordination

No subagent was created. A_MAIN continued directly on `/Users/dinghaolin/IdeaProjects/riftbound` local `main` per user request.

External UI followup worktree `/Users/dinghaolin/MyProjects/riftbound-codex-ui-followup-20260616` remained clean and no-ahead. After code commit `55792eaf`, `main...codex/ui-followup-20260616` divergence was `375 0`.

The historical `rule-audit-remaining-20260615` worktree path remains absent, and local branch `codex/rule-audit-remaining-20260615` remained no-ahead with divergence `448 0`.

## Next

Project remains **NOT READY**. Next executable server slice can continue spectator pending task queue authoritative parity diagnostics, remaining missing battlefield/lane diagnostics, triggerQueue keyed/detail edge diagnostics, recovered/spectator/authoritative nested payload breadth, recovery timing remaining breadth, battle assignment remaining matrix breadth, raw/mapper/protocol surfaces, or another higher-priority P0/P1 server audit surface after re-reading the board, `AGENTS.md`, PDF gate, UI followup and `codex/rule-audit-remaining-20260615`.

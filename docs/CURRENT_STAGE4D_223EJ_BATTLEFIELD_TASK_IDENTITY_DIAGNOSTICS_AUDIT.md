# Stage 4D-223EJ Battlefield Task Identity Diagnostics Audit

Date: 2026-06-19 21:05 CST

Owner: A_MAIN

Worktree: `/Users/dinghaolin/IdeaProjects/riftbound`

Branch: `main`

Code commit: `a38045bd` (`test: detail battlefield task identity diagnostics`)

## Summary

223EJ tightens battlefield task identity and reason mismatch diagnostics in `MatchRecoveryValidator`.

Runtime changed: yes, diagnostic detail only. Valid battlefield task construction, spell-duel/battle lifecycle state, task ids, timing snapshots, spectator replay redaction and gameplay behavior are unchanged.

The updated diagnostics preserve their existing prefixes while appending stable expected/actual detail for:

- recovered snapshot timing battlefield task derived task id mismatch
- recovered snapshot timing battlefield task spell duel id mismatch
- recovered snapshot timing battlefield task battle id mismatch
- recovered snapshot timing battlefield task kind-specific reason mismatch
- spectator replay timing battlefield task derived task id mismatch
- spectator replay timing battlefield task spell duel id mismatch
- spectator replay timing battlefield task battle id mismatch
- spectator replay timing battlefield task kind-specific reason mismatch

Representative examples now include:

- `expected task:start-spell-duel:battlefield-a but got task:start-battle:battlefield-a`
- `expected spell-duel:battlefield-a but got spell-duel:drift`
- `expected battle:battlefield-c but got battle:drift`
- `expected BATTLEFIELD_CONTESTED but got SPELL_DUEL_AFTER_BATTLEFIELD_CONTEST`
- `expected SPELL_DUEL_AFTER_BATTLEFIELD_CONTEST but got BATTLEFIELD_CONTESTED`

## Rule Source

Checked `AGENTS.md`, the five root PDFs and `/tmp/riftbound_rules_pdf_text/` before the slice. Relevant current core-rule surfaces include 120-130 game objects/privacy/card backs, 144.4 standard movement and battlefield destination restrictions, 146.1 unit location and 383.4 triggered-skill families. This slice changes diagnostics only and does not change rules behavior.

## Validation

- `dotnet build src/Riftbound.Engine/Riftbound.Engine.csproj --no-restore`
- focused battlefield task identity/reason diagnostics tests: `6/6`
- changed-class `MatchRecoveryTests`: `1981/1981`
- adjacent Recovery/SpectatorReplay/Snapshot/Timing/BattlefieldTask/Battlefield/Object/Zone/Location/TriggerQueue/Stack/Battle filter: `3479/3479`
- backend full via `Riftbound.slnx`: `8319/8319`
- `git diff --check`
- anchored conflict-marker scan over `src`, `tests`, `docs`
- residual selected old-format battlefield task identity/reason assertion scan

## Coordination

No subagent was created. A_MAIN continued directly on `/Users/dinghaolin/IdeaProjects/riftbound` local `main` per user request.

External UI followup worktree `/Users/dinghaolin/MyProjects/riftbound-codex-ui-followup-20260616` remained clean and no-ahead. After code commit `a38045bd`, `main...codex/ui-followup-20260616` divergence was `374 0`.

The historical `rule-audit-remaining-20260615` worktree path remains absent, and local branch `codex/rule-audit-remaining-20260615` remained no-ahead with divergence `447 0`.

## Next

Project remains **NOT READY**. Next executable server slice can continue remaining missing battlefield/lane diagnostics, triggerQueue keyed/detail edge diagnostics, recovered/spectator/authoritative nested payload breadth, recovery timing remaining breadth, battle assignment remaining matrix breadth, raw/mapper/protocol surfaces, or another higher-priority P0/P1 server audit surface after re-reading the board, `AGENTS.md`, PDF gate, UI followup and `codex/rule-audit-remaining-20260615`.

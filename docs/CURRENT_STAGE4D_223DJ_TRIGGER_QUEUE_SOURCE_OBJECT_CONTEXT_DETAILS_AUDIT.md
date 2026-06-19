# Stage 4D-223DJ Trigger Queue Source Object Context Details Audit

Date: 2026-06-19

Status: accepted on local `main` as code commit `da1851bd`; project remains **NOT READY**.

## Scope

A_MAIN continued directly in `/Users/dinghaolin/IdeaProjects/riftbound` on `main`.

This slice tightens triggerQueue context-specific `source object id` diagnostics only. `MatchRecoveryValidator` now routes Blue Sentinel delayed resource and Jhin movement resource source-object-id versus trigger-id source-object-id checks through a shared helper. Existing diagnostic prefixes are preserved and now append stable `expected ... but got ...` detail.

No valid recovery replay behavior, trigger detection, trigger ordering, source visibility redaction, trigger construction, stack placement, payment, battle, prompt rendering, authoritative serialization, random determinism, protocol behavior, frontend behavior or card rule behavior changed.

## Files

- `src/Riftbound.Engine/MatchRecovery.cs`
- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`

## Rule Source

Root PDFs remained present:

- `/Users/dinghaolin/IdeaProjects/riftbound/《符文战场》核心规则_260330.pdf`
- `/Users/dinghaolin/IdeaProjects/riftbound/铸魂淬炼系列_官方FAQ_260114.pdf`
- `/Users/dinghaolin/IdeaProjects/riftbound/裁判FAQ_251023.pdf`
- `/Users/dinghaolin/IdeaProjects/riftbound/《符文战场》破限系列_裁判FAQ_260416.pdf`
- `/Users/dinghaolin/IdeaProjects/riftbound/铸魂淬炼系列_裁判FAQ.pdf`

Checked `docs/CURRENT_CORE_RULE_PDF_READING_NOTES.md` and `/tmp/riftbound_rules_pdf_text/core_rules_260330.txt` anchors 128, 129.3, 157.3, 157.3.a, 303.2.a, 319-321, 323.4, 333-334, 382-383 and 808.1.d. This slice changes diagnostics only; no rules behavior changed.

## Coordination

No subagent was created. External UI followup worktree `/Users/dinghaolin/MyProjects/riftbound-codex-ui-followup-20260616` remained clean on `codex/ui-followup-20260616`; after code commit `da1851bd`, `main...codex/ui-followup-20260616` was `253 0`. `codex/rule-audit-remaining-20260615` had no commits ahead of `main`; after code commit `da1851bd`, divergence was `326 0`. Exact divergence must be rechecked before future integration.

## Validation

- Focused `MatchRecoveryTests` triggerQueue filter: `744/744`
- Changed-class `MatchRecoveryTests`: `1976/1976`
- Adjacent Recovery/SpectatorReplay/Snapshot/Timing/ContinuousEffect/TriggerQueue/OrderTriggers/Trigger/Stack/Battle filter: `3569/3569`
- Backend full via `Riftbound.slnx`: `8312/8312`
- `git diff --check`
- Anchored conflict-marker scan over `src`, `tests` and `docs`

## Remaining Work

This narrows triggerQueue source-object context diagnostic detail only. It does not close broader command/recovery/random determinism, continuous-effect breadth, remaining triggerQueue keyed/detail breadth, full LayerEngine breadth, P0/P1, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` or final readiness.

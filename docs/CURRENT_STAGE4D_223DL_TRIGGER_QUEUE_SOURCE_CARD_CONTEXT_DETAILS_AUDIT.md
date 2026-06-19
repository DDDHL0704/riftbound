# Stage 4D-223DL Trigger Queue Source Card Context Details Audit

Date: 2026-06-19

Conclusion: **ACCEPTED / PROJECT NOT READY**

## Scope

A_MAIN accepted code commit `a9487928` on local `main`.

This slice is diagnostic-only. `MatchRecoveryValidator` now routes triggerQueue source-card mismatch diagnostics through a shared expected/actual detail helper for:

- Blue Sentinel delayed resource source-card checks.
- Jhin movement resource source-card checks.
- Kogmaw last-breath source-card checks.
- OGS Lux high-cost spell source-card checks.
- Teemo on-play self-power source-card checks.
- Standard last-breath and friendly-destroyed source-card checks already covered by Watchful Sentinel and Ghostly Centaur recovery tests.

The existing diagnostic prefixes are preserved and now append stable `expected ... but got ...` details. Runtime behavior, trigger queue construction, source visibility redaction, trigger ordering, replay restoration and rule resolution semantics are unchanged.

## Rule Source

Rule gate rechecked through `docs/CURRENT_CORE_RULE_PDF_READING_NOTES.md`, the five root PDFs and `/tmp/riftbound_rules_pdf_text/`:

- Latest core rules 128 and 129.3 for hidden/private information boundaries.
- Core rules 157.3 and 157.3.a for player/action identity context.
- Core rules 160-166 for object/card identity context.
- Core rules 303.2.a, 319-321, 323.4, 333-334 and 382-383 for stack, triggered skills, active/triggered timing and replay-visible public context.
- Core rule 808.1.d for Last Breath pending item/source context.

No rule behavior changed in this slice.

## Validation

- `dotnet build src/Riftbound.Engine/Riftbound.Engine.csproj --no-restore`: passed.
- Focused `MatchRecoveryTests&TriggerQueue`: `744/744` passed.
- Changed-class `MatchRecoveryTests`: `1976/1976` passed.
- Adjacent Recovery/SpectatorReplay/Snapshot/Timing/ContinuousEffect/TriggerQueue/OrderTriggers/Trigger/Stack/Battle filter: `3569/3569` passed.
- Backend full via `Riftbound.slnx`: `8312/8312` passed.
- `git diff --check`: passed.
- Anchored conflict-marker scan over `src`, `tests` and `docs`: no findings.
- Residual triggerQueue source-card diagnostic search found no matching card-no test assertion without expected/actual detail.

## Coordination

A_MAIN continued directly in `/Users/dinghaolin/IdeaProjects/riftbound` on `main`; no subagent or additional worktree was created. External UI followup worktree `/Users/dinghaolin/MyProjects/riftbound-codex-ui-followup-20260616` remained clean on `codex/ui-followup-20260616` with no commits ahead of main. `codex/rule-audit-remaining-20260615` also had no commits ahead of main at the post-code-commit check.

## Remaining

This narrows triggerQueue source-card diagnostic detail only. It does not close broader command/recovery/random determinism, continuous-effect breadth, remaining triggerQueue keyed/detail breadth, full LayerEngine breadth, P0/P1, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` or final readiness.

Project remains **NOT READY**.

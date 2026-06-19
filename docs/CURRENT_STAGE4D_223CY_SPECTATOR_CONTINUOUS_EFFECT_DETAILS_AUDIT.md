# Stage 4D-223CY Spectator Continuous Effect Details Audit

Date: 2026-06-19

Status: accepted on local `main` as code commit `f4d2b11c`; docs checkpoint follows. Project remains **NOT READY**.

## Scope

A_MAIN continued directly in `/Users/dinghaolin/IdeaProjects/riftbound` on `main`.

Runtime changed: yes, diagnostic detail only. Frontend changed: no.

`MatchRecoveryValidator` now appends stable expected/actual ordered-list details to spectator replay frame timing `continuousEffects` aggregate mismatch diagnostics while preserving the existing diagnostic prefixes. Covered aggregate fields include effect ids, scopes, layers, durations, target/source object ids, power/base/effective power values, sequence, effect metadata, LayerEngine foundation status, requested/applied/minimum/resulting power metadata, applied/source order metadata, condition/lifecycle metadata, participant/dependency object id lists, and deferred LayerEngine residual lists.

Existing keyed continuous-effect diagnostics already reported field-level expected/actual details; this slice narrows the remaining ordered aggregate `disagree` diagnostics so a recovery failure shows the authoritative ordered values and the spectator payload values in the same message.

## Rule Source

Rule source was checked through `docs/CURRENT_CORE_RULE_PDF_READING_NOTES.md` and `/tmp/riftbound_rules_pdf_text/core_rules_260330.txt` from the five root PDFs.

Relevant latest core-rule anchors: 157.3 for resolving a spell or skill fully before pending items, 303.2.a for ordering simultaneous actions/effects/triggers, 317.2.c for end-of-turn effect expiry, and 333-340 for stack/HOT pending item handling. This slice changes diagnostics only; it does not change continuous-effect construction, LayerEngine ordering, stack resolution, timing windows, trigger handling, object visibility or replay semantics.

## Validation

- Focused mismatch test: `1/1`.
- Focused spectator replay timing continuous-effect shard: `279/279`.
- Changed-class `MatchRecoveryTests`: `1976/1976`.
- Adjacent Recovery/SpectatorReplay/Snapshot/Timing/ContinuousEffect/TriggerQueue/Stack/Battle filter: `3422/3422`.
- Backend full via `Riftbound.slnx`: `8310/8310`.
- `git diff --check` passed before docs sync.
- Anchored conflict-marker scan over `src`/`tests`/`docs` had no findings before docs sync.

## Coordination

External UI followup worktree `/Users/dinghaolin/MyProjects/riftbound-codex-ui-followup-20260616` on `codex/ui-followup-20260616` was clean at `01364ee2`; `main...codex/ui-followup-20260616` was `207 0` after the code commit. A_MAIN must inspect that worktree before integration and must not develop directly there.

`codex/rule-audit-remaining-20260615` had no commits ahead of `main`; divergence was `280 0` from local `main` after the code commit.

## Non-Goals

This does not close valid recovery replay behavior, continuous-effect construction, LayerEngine breadth, trigger queue breadth, stack placement, player identity normalization, seat validation, battlefield lane semantics, skill activation or trigger confirmation, payment, legality, battle creation, battle damage assignment computation, damage legality, battle cleanup, battlefield control, prompt rendering, source-object serialization, authoritative state serialization, random determinism, remaining recovered/spectator/authoritative nested payload breadth, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial`, L2P-RG-005 or final status.

## Next

Continue the next executable server slice directly on `/Users/dinghaolin/IdeaProjects/riftbound` `main` after re-reading the shared board, `AGENTS.md`, the PDF gate, the UI followup worktree status and `codex/rule-audit-remaining-20260615`.

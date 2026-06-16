2026-06-16 17:57 CST

Stage 4D-223AW recovery spectator continuous-effect missing/null-payload count-drift validation accepted.

Scope:
- A_MAIN worked directly on local `main` in `/Users/dinghaolin/IdeaProjects/riftbound`.
- Runtime changed: yes, narrow recovery validation diagnostic only.
- Frontend changed: no.
- Touched code: `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Touched coordination docs: current completion audit, P0/P1 closure plan, dispatch/write locks, shared coordination board and this audit file.

What changed:
- `ValidateSpectatorContinuousEffectPayloads` now reports spectator replay-frame timing continuous-effect count `0` versus a non-empty authoritative continuous-effect list when `continuousEffects` is missing or null, while preserving the required-payload error.
- Added `RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectsMissingPayloadWithoutCountMismatch` and `RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectsNullPayloadWithoutCountMismatch`.
- Renamed and strengthened the non-empty authoritative companions as `RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectsMissingPayloadWithCountMismatch` and `RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectsNullPayloadWithCountMismatch`.
- Empty-authoritative missing/null payloads still omit count mismatch; non-empty authoritative missing/null payloads now emit both required-payload and count `0` mismatch diagnostics.

Rule source checked:
- `docs/CURRENT_CORE_RULE_PDF_READING_NOTES.md`
- `/tmp/riftbound_rules_pdf_text/core_rules_260330.txt`
- Latest core rules 135-137 for rule/effect text, attached text and power-bonus context.
- Latest core rules 143.2, 143.2.b and 143.2.b.1 for unit power, power floor/reference semantics and actual power value context.
- Latest core rule 317.2.c for until-end-of-turn effect expiration timing.
- Latest core rules 355 and 356 for choices, target legality and cost modification context that can create or depend on continuous-effect state.

Coordination:
- No subagent was created.
- A_MAIN continued directly on `/Users/dinghaolin/IdeaProjects/riftbound` `main` per user request.
- External UI followup worktree `/Users/dinghaolin/MyProjects/riftbound-codex-ui-followup-20260616` on `codex/ui-followup-20260616` was checked before the slice and before docs sync; it was clean at `01364ee2` with no commits ahead of `main`.
- `rule-audit-remaining-20260615` had no new commits ahead of `main` before code commit or docs sync.
- Root PDF rule files remained present.

Validation passed:
- Focused missing/null payload pair: `4/4`.
- Changed-class `MatchRecoveryTests`: `1952/1952`.
- Adjacent ContinuousEffect/SpectatorReplayTiming/TriggerQueue filter: `1540/1540`.
- Backend full: `8282/8282`.
- `git diff --check` passed before code commit.
- Runtime/test anchored conflict-marker scan passed before code commit.

Code commit:
- `b944d216 fix: report recovery continuous effect payload count drift`

Non-goals:
- Does not change valid recovery replay behavior.
- Does not change continuous-effect creation, ordering, LayerEngine semantics, power-modifier runtime behavior, effect expiration, prompt rendering, hidden-source redaction, source-object serialization or authoritative state serialization.
- Does not close random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial`, L2P-RG-005 or final readiness.

Project remains **NOT READY**.

# 4D-03RA..03RE Candidate Audit

Status: audit prepared on `DOC_MATRIX_CURRENT`; validation must be rerun on the final dirty tree before commit. Project remains **NOT READY**.

## Authorization And Write Scope

Shared-board latest actionable entry is `2026-05-21 23:33 A_MAIN`: 03QV-03QZ was accepted into main and `DOC_MATRIX_CURRENT` was released under `APPROVED_ACTIVE_NO_IDLE`. This 03RA-03RE draft keeps the allowed matrix lane scope:

- Allowed: `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`, current checkpoint docs, per-bundle candidate/audit docs and the narrow `PaymentEngineCoverageAuditTests.cs` expected-count/current-slice baseline sync.
- Not touched: server runtime, frontend runtime, API/protocol core fields, official catalog data, browser/Chrome/formal E2E scripts, `fullOfficial`, final readiness flags and `riftbound-dotnet.sln`.

## Evidence Audit

| Row | Existing implementation | Existing test / fixture evidence | Rules / FAQ evidence |
|---|---|---|---|
| `FU-aa66712b50` / `UNL-173/219` / 牺牲 | `CardBehaviorRegistry` direct behavior `SACRIFICE_DESTROY_FRIENDLY_POWERFUL_DRAW_2_CALL_RUNE` | `CoreRuleEnginePlaysSacrificeDestroyFriendlyPowerfulDrawCallRune`; reject-without-cost and non-powerful-cost guards | `rules-evidence-index.md` row `p2-preflight-play-sacrifice-destroy-friendly-powerful-draw-call-rune`; no FAQ refs in matrix |
| `FU-88e6bf6e77` / `UNL-178/219` / 波比 | direct behavior `POPPY_AMBUSH_BARRIER_NO_EXPERIENCE_STATIC` | Poppy preflight fixture, experience-spend representative, explicit target rejection | `rules-evidence-index.md` rows `p2-preflight-play-poppy-no-experience-ambush-barrier-static` and `p4-play-poppy-spend-experience-reduce-cost`; no FAQ refs in matrix |
| `FU-976bb37cdd` / `UNL-178a/219` / 波比 | direct behavior `POPPY_ALT_A_AMBUSH_BARRIER_NO_EXPERIENCE_STATIC` | Poppy alt preflight fixture and shared experience-cost representative | `rules-evidence-index.md` row `p2-preflight-play-poppy-alt-a-no-experience-ambush-barrier-static`; no FAQ refs in matrix |
| `FU-c7b4c62435` / `UNL-182/219` / 完美谢幕 | direct behaviors `PERFECT_FINALE_*` | draw, battlefield-damage, base-damage and battlefield-power fixtures | `rules-evidence-index.md` rows `p2-preflight-play-perfect-finale-*`; no FAQ refs in matrix |
| `FU-22895e628a` / `UNL-185/219` / 血港鬼影 | `LEGEND_ACTION_DOMAIN` non-play representative | `P79LegendActPykeReturnsBattlefieldUnitAndCreatesCoin` and existing legend-action representative tests | Existing legend-action domain evidence; no FAQ refs in matrix |

## Count Audit

The draft moves:

- all functional units `NEEDS_ENGINE_SUPPORT`: 456 -> 451
- payment-cost `NEEDS_ENGINE_SUPPORT`: 58 -> 53
- primary payment-cost residual: 23 -> 18
- targeting-stack-timing `NEEDS_ENGINE_SUPPORT`: 225 -> 220
- cleanup-replacement-duration `NEEDS_ENGINE_SUPPORT`: 179 -> 176
- hidden-info-random-zone `NEEDS_ENGINE_SUPPORT`: 153 -> 150
- payment-or-targeting-stack-timing `NEEDS_ENGINE_SUPPORT`: 245 -> 240
- payment-and-targeting-stack-timing `NEEDS_ENGINE_SUPPORT`: 38 -> 33
- `NEEDS_AUTOMATED_TEST_EVIDENCE`: remains 328
- `NEEDS_FAQ_REVIEW`: remains 92
- primary FAQ residual: remains 61
- `fullOfficialTrue`: remains 0
- `ready`: remains false

## Closure Judgment

This is acceptable only as row-level `NEEDS_ENGINE_SUPPORT` evidence synchronization. It is not a full-official or project-readiness claim.

Closed:

- row-level `NEEDS_ENGINE_SUPPORT` on the five selected FUs / snapshot entries.

Open:

- automated evidence disposition for all selected FUs
- complete PaymentEngine / `PAY_COST` matrix
- complete FEPR target/stack lifecycle
- Poppy ambush / Barrier breadth
- Sacrifice cleanup/hidden/layer breadth
- Perfect Finale Echo and all-mode official breadth
- Pyke legend-action / standby / hidden / cleanup official breadth
- frontend/browser/formal E2E gates

## Validation Plan

Required before commit:

- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- `git diff --check`
- conflict-marker scan
- focused `PaymentEngineCoverageAuditTests`
- focused/adjacent evidence tests matching Sacrifice, Poppy, Perfect Finale and Pyke where available
- backend full test because matrix and audit baseline are both changed

Frontend build and Chrome smoke are not required for this batch because no frontend runtime or browser script changed.

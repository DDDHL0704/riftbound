# 4D-03GI-E Payment-Cost Lux Spell-Only Resource Targeting-Stack Blocker Closure Candidate

## Scope

This candidate records the next isolated `E_CARD_MATRIX_READINESS` payment-cost blocker reduction after 4D-03GH-E.

Selected row:

- functionalUnitId: `FU-97d6c39d73`
- card: `OGS·014/024` 拉克丝
- effect: `OGS_014_LUX_TAP_SPELL_RESOURCE_PLAY_UNIT`
- partition: `bd-engine-support-payment-cost`
- primary row query: `payment-cost`
- secondary row query: `payment-and-targeting-stack-timing`

The row is intentionally narrowed from `NEEDS_ENGINE_SUPPORT` to `IMPLEMENTED_UNTESTED` only. It keeps `NEEDS_AUTOMATED_TEST_EVIDENCE`, `fullOfficial=false`, and `ready=false`.

## Evidence

Existing anchors:

- `docs/CURRENT_STAGE4D_03CR_PAYMENT_ENGINE_LUX_RESOURCE_SKILL_AUDIT.md`
- `docs/CURRENT_STAGE4D_03CR_PAYMENT_ENGINE_LUX_RESOURCE_SKILL_EVIDENCE.md`
- `src/Riftbound.Engine/P4ActivatedAbilityCatalog.cs`
- `src/Riftbound.Engine/MatchSession.cs`
- `tests/Riftbound.ConformanceTests/LuxResourceSkillTests.cs`
- `docs/rules-evidence-index.md`
- `docs/p2-rules-preflight.md`
- `docs/CURRENT_P2_STATUS.md`

This batch does not reclassify the Lux row as full official. The existing Lux runtime and P2 evidence is enough for the narrow engine-support blocker reduction, but the matrix still records missing FU-level automated evidence.

## Count Impact

- payment-cost functionalUnits: 360 -> 360
- payment-cost snapshotEntries: 446 -> 446
- NEEDS_ENGINE_SUPPORT: 335 -> 334
- primary residual: 191 -> 190
- payment-or-targeting-stack-timing NEEDS_ENGINE_SUPPORT: 523 -> 522
- payment-and-targeting-stack-timing NEEDS_ENGINE_SUPPORT: 232 -> 231
- NEEDS_AUTOMATED_TEST_EVIDENCE residual: 328 -> 328
- NEEDS_FAQ_REVIEW residual: 92 -> 92
- primary FAQ residual: 61 -> 61
- fullOfficialTrue: 0 -> 0
- ready: false -> false

## Non-Closure

Payment-cost blocker closure remains partially open. `B/D_ENGINE_SUPPORT`, `A_CONFORMANCE_AUTOMATED_TEST_EVIDENCE`, `E_CARD_MATRIX_FAQ_REVIEW`, `E_CARD_MATRIX_READINESS`, card matrix closure, P0/P1 and READY all remain open.

## Validation

- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed for this batch.
- focused `PaymentEngineCoverageAuditTests` 346/346 passed for this batch.
- current-head backend full `dotnet test Riftbound.slnx --no-restore` 4917/4917 passed for this batch.
- `git diff --check` passed for this batch.

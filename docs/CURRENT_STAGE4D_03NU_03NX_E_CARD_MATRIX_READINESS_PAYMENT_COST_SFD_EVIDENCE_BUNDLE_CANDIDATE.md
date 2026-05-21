# Stage 4D 03NU-03NX Payment-Cost SFD Evidence Bundle Candidate

Status: validated in `DOC_MATRIX_CURRENT` under the 2026-05-21 17:01 A_MAIN rolling approval; validation passed for this commit.

## Selected Rows

| Slice | Functional unit | Card | Effect | Evidence basis |
| --- | --- | --- | --- | --- |
| 4D-03NU-E | `FU-302cd59caa` | `SFD·168/221` 先锋军备 | `VANGUARD_ARMORY_PLAY_EQUIPMENT` | Existing direct-card runtime, Vanguard Armory play / no-target behavior, target-reject fixture evidence, and `rules-evidence-index.md` entries. |
| 4D-03NV-E | `FU-5914e986bb` | `SFD·179/221` 卡银娜·薇蕊泽 | `KARINA_VERAZE_PLAY_UNIT_NO_OPTIONAL_HASTE` | Existing direct-card runtime, no-optional-haste fixture evidence, Haste optional-ready branch tests, and `rules-evidence-index.md` entries. |
| 4D-03NW-E | `FU-95cb8f2f4f` | `SFD·200/221` 奥术跃迁 | `ARCANE_SHIFT_BANISH_FRIENDLY_UNIT_PLAY_TO_BASE_DAMAGE_ENEMY_BATTLEFIELD_BANISH_SOURCE` | Existing direct-card runtime, Arcane Shift friendly-banish / enemy-damage tests, target-reject tests, and `rules-evidence-index.md` entries. |
| 4D-03NX-E | `FU-7e974d2ee6` | `SFD·206/221` 劳伦特心眼刀 | `LAURENT_DUELIST_COUNTER_SPELL_POWER_BY_SPELL_COST` | Existing direct-card runtime, Laurent Duelist counter-spell power-by-cost tests, stack-item / enemy-target rejection tests, and `rules-evidence-index.md` entries. |

## Count Delta

| Residual | Before | After |
| --- | ---: | ---: |
| all functional units `NEEDS_ENGINE_SUPPORT` | 540 | 536 |
| payment-cost `NEEDS_ENGINE_SUPPORT` | 142 | 138 |
| primary residual | 102 | 98 |
| targeting-stack-timing `NEEDS_ENGINE_SUPPORT` | 284 | 281 |
| cleanup-replacement-duration `NEEDS_ENGINE_SUPPORT` | 211 | 210 |
| hidden-info-random-zone `NEEDS_ENGINE_SUPPORT` | 175 | 175 |
| payment-or-targeting-stack-timing `NEEDS_ENGINE_SUPPORT` | 329 | 325 |
| payment-and-targeting-stack-timing `NEEDS_ENGINE_SUPPORT` | 97 | 94 |
| payment-cost `NEEDS_AUTOMATED_TEST_EVIDENCE` | 328 | 328 |
| payment-cost `NEEDS_FAQ_REVIEW` | 92 | 92 |
| primary FAQ residual | 61 | 61 |
| `fullOfficialTrue` | 0 | 0 |
| `ready` | false | false |

## Non-Closure

This bundle closes only the row-level `NEEDS_ENGINE_SUPPORT` blocker for the selected matrix rows. Automated evidence disposition, Vanguard Armory equipment activated-skill breadth, Karina Veraze Haste optional-cost breadth, Arcane Shift banish / replay ownership-control and battle / spell-duel breadth, Laurent Duelist counter-spell target / cleanup breadth, complete FEPR target / stack timing, complete PaymentEngine / PAY_COST matrix, P0/P1, Chrome / formal E2E, `fullOfficial` and final readiness remain open.

# 4D-03SH..03SL E_CARD_MATRIX_READINESS Implemented-Tested Evidence Bundle Candidate

Status: candidate prepared on `DOC_MATRIX_CURRENT`; project remains **NOT READY**.

## Scope

This bundle is matrix/current-docs plus `PaymentEngineCoverageAuditTests.cs` baseline synchronization only. It does not modify server runtime, frontend runtime, protocol core fields, official catalog data, browser/Chrome scripts, formal 18-step E2E scripts, `fullOfficial`, final readiness flags or `riftbound-dotnet.sln`.

## Selected Rows

| Stage | functionalUnit | collectorId | Card | Effect / oracle | Evidence basis |
|---|---|---|---|---|---|
| 4D-03SH-E | `FU-67c6b0186e` | `SFD·049/221` | 厄斐琉斯 | `SFD_049_APHELIOS_WEAPON_TRIGGER_PLAY_UNIT;SFD_APHELIOS_PLAY_UNIT;SFD_APHELIOS_PROMO_PLAY_UNIT` | Existing representative implemented-tested shared-oracle row; FAQ, weapon/promo, cleanup, layer and target-stack breadth remain open. |
| 4D-03SI-E | `FU-5cea85e7c3` | `SFD·128/221` | 狂热粉丝 | `FERVID_FAN_DEFENSE_TRIGGER_PLAY_UNIT` | Existing representative implemented-tested row; FAQ, battle, cleanup, control and target-stack breadth remain open. |
| 4D-03SJ-E | `FU-422b450261` | `SFD·170/221` | 雷克塞 | `SFD_170_REKSAI_ATTACK_REVEAL_PLAY_UNIT;SFD_170A_REKSAI_ATTACK_REVEAL_PLAY_UNIT` | Existing representative implemented-tested shared-oracle row; FAQ, hidden, layer and target-stack breadth remain open. |
| 4D-03SK-E | `FU-6582231b22` | `UNL-036/219` | 变异猫咪 | `MUTANT_KITTEN_PLAY_KEYWORD_UNIT` | Existing representative implemented-tested keyword-unit row; battle breadth remains representative-only. |
| 4D-03SL-E | `FU-1fdf2a082a` | `UNL-090/219` | 乐芙兰 | `LEBLANC_PLAY_KEYWORD_UNIT` | Existing representative implemented-tested keyword-unit row; battle and cleanup breadth remain representative-only. |

## Blocker Disposition

| Stage | Closed in this bundle | Still open after this bundle |
|---|---|---|
| 4D-03SH-E | `NEEDS_ENGINE_SUPPORT` | `NEEDS_FAQ_REVIEW`; shared-oracle weapon/promo breadth, cleanup/replacement-duration, layer/continuous-effect and target-stack breadth remain open. |
| 4D-03SI-E | `NEEDS_ENGINE_SUPPORT` | `NEEDS_FAQ_REVIEW`; battle-spell-duel, cleanup/replacement-duration, control-zone movement and target-stack breadth remain open. |
| 4D-03SJ-E | `NEEDS_ENGINE_SUPPORT` | `NEEDS_FAQ_REVIEW`; shared-oracle reveal, hidden/random-zone, layer/continuous-effect and target-stack breadth remain open. |
| 4D-03SK-E | `NEEDS_ENGINE_SUPPORT` | Full official breadth remains open; keyword-unit and battle breadth remain representative-only. |
| 4D-03SL-E | `NEEDS_ENGINE_SUPPORT` | Full official breadth remains open; keyword-unit, battle and cleanup breadth remain representative-only. |

## Count Movement

| Metric | Before | After |
|---|---:|---:|
| all functional units `NEEDS_ENGINE_SUPPORT` | 423 | 418 |
| implemented-tested evidence residual | 8 | 3 |
| payment-cost `NEEDS_ENGINE_SUPPORT` | 35 | 35 |
| targeting-stack-timing `NEEDS_ENGINE_SUPPORT` | 200 | 197 |
| cleanup-replacement-duration `NEEDS_ENGINE_SUPPORT` | 161 | 158 |
| hidden-info-random-zone `NEEDS_ENGINE_SUPPORT` | 144 | 143 |
| payment-or-targeting-stack-timing `NEEDS_ENGINE_SUPPORT` | 214 | 211 |
| payment-and-targeting-stack-timing `NEEDS_ENGINE_SUPPORT` | 21 | 21 |
| `NEEDS_AUTOMATED_TEST_EVIDENCE` | 328 | 328 |
| `NEEDS_FAQ_REVIEW` | 92 | 92 |
| primary FAQ residual | 61 | 61 |
| `fullOfficialTrue` | 0 | 0 |
| `ready` | false | false |

## Validation

- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed.
- `git diff --check` passed.
- conflict-marker scan over `docs` and `tests` was clean.
- matrix count script passed with all FU `NEEDS_ENGINE_SUPPORT=418`, implemented-tested evidence residual `3`, payment-cost `35`, targeting-stack-timing `197`, cleanup-replacement-duration `158`, hidden-info-random-zone `143`, payment-or-targeting-stack-timing `211`, payment-and-targeting-stack-timing `21`, `fullOfficialTrue=0`, `ready=false`.
- PaymentEngineCoverageAuditTests `697/697` passed.
- ConformanceFixtureRunnerTests `3019/3019` passed.
- backend full `5344/5344` passed.

Frontend build / Chrome smoke are not part of this candidate because this bundle changes no frontend/runtime/browser asset or script.

## Why Not Ready

This bundle reduces only row-level `NEEDS_ENGINE_SUPPORT` where existing representative evidence is already present. FAQ review, full official breadth, frontend/browser/formal E2E and final readiness remain open.

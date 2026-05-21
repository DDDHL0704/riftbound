# 4D-03RX..03SB E_CARD_MATRIX_READINESS Implemented-Tested Evidence Bundle Candidate

Status: candidate prepared on `DOC_MATRIX_CURRENT`; project remains **NOT READY**.

## Scope

This bundle is matrix/current-docs plus `PaymentEngineCoverageAuditTests.cs` baseline synchronization only. It does not modify server runtime, frontend runtime, protocol core fields, official catalog data, browser/Chrome scripts, formal 18-step E2E scripts, `fullOfficial`, final readiness flags or `riftbound-dotnet.sln`.

## Selected Rows

| Stage | functionalUnit | cardId | collectorId | Card | Effect / oracle | Evidence basis |
|---|---|---:|---|---|---|---|
| 4D-03RX-E | `FU-44f29ad8f7` | 31208 | `OGN·004/298` | 顺劈 | `CLEAVE_OVERWHELM_3` | Existing representative automated evidence: `p4-play-swift-cleave-in-spell-duel-focus` and `3C-spell-duel-focus-pass`; row remains representative-only for battle/cleanup/layer/target-stack breadth. |
| 4D-03RY-E | `FU-441cb9fb7f` | 31215 | `OGN·009/298` | 海克斯射线 | `HEXTECH_RAY_DAMAGE_3` | Existing representative automated evidence and Stage 4C-72 overlay; FAQ refs remain open, so only `NEEDS_ENGINE_SUPPORT` is removed. |
| 4D-03RZ-E | `FU-7f4a387b92` | 34842 | `OGN·056/298` | 自适应机器人 | `ADAPTIVE_ROBOT_CONQUER_BOON_PLAY_UNIT` | Existing representative conquer/boon automated evidence; battle/cleanup/layer/target-stack breadth remains representative-only. |
| 4D-03SA-E | `FU-bf81341dd2` | 31321 | `OGN·103/298` | 拉文布鲁姆学生 | `RAVENBLOOM_STUDENT_SPELL_TRIGGER_PLAY_UNIT` | Existing representative spell-trigger evidence; FAQ refs remain open, so only `NEEDS_ENGINE_SUPPORT` is removed. |
| 4D-03SB-E | `FU-2779c06158` | 31352 | `OGN·128/298` | 决斗 | `DUEL_MUTUAL_POWER_DAMAGE` | Existing Stage 4C-82 representative duel damage evidence; battle/layer/target-stack breadth remains representative-only. |

## Count Movement

| Metric | Before | After |
|---|---:|---:|
| all functional units `NEEDS_ENGINE_SUPPORT` | 433 | 428 |
| implemented-tested evidence residual | 18 | 13 |
| payment-cost `NEEDS_ENGINE_SUPPORT` | 35 | 35 |
| targeting-stack-timing `NEEDS_ENGINE_SUPPORT` | 208 | 203 |
| cleanup-replacement-duration `NEEDS_ENGINE_SUPPORT` | 167 | 164 |
| hidden-info-random-zone `NEEDS_ENGINE_SUPPORT` | 144 | 144 |
| payment-or-targeting-stack-timing `NEEDS_ENGINE_SUPPORT` | 222 | 217 |
| payment-and-targeting-stack-timing `NEEDS_ENGINE_SUPPORT` | 21 | 21 |
| `NEEDS_AUTOMATED_TEST_EVIDENCE` | 328 | 328 |
| `NEEDS_FAQ_REVIEW` | 92 | 92 |
| primary FAQ residual | 61 | 61 |
| `fullOfficialTrue` | 0 | 0 |
| `ready` | false | false |

## Why Not Ready

This bundle reduces only row-level `NEEDS_ENGINE_SUPPORT` where existing representative evidence is already present. FAQ review, full official breadth, frontend/browser/formal E2E and READY remain open.

## Validation Results

Passed on `DOC_MATRIX_CURRENT`:

- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- `git diff --check`
- conflict-marker scan over `docs` and `tests` returned no matches
- matrix count script: all `NEEDS_ENGINE_SUPPORT=428`, implemented-tested evidence residual `13`
- PaymentEngineCoverageAuditTests: 697/697 passed
- ConformanceFixtureRunnerTests: 3019/3019 passed
- backend full test: 5344/5344 passed

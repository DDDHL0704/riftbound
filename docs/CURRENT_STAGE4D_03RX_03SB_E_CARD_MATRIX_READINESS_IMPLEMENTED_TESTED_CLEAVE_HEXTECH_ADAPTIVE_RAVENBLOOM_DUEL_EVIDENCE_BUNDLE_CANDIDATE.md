# 4D-03RX..03SB E_CARD_MATRIX_READINESS Implemented-Tested Evidence Bundle Candidate

Status: candidate prepared on `DOC_MATRIX_CURRENT`; project remains **NOT READY**.

## Scope

This bundle is matrix/current-docs plus `PaymentEngineCoverageAuditTests.cs` baseline synchronization only. It does not modify server runtime, frontend runtime, protocol core fields, official catalog data, browser/Chrome scripts, formal 18-step E2E scripts, `fullOfficial`, final readiness flags or `riftbound-dotnet.sln`.

The shared-board authorization used for this candidate is the `2026-05-22 01:07 A_MAIN` entry: select the next executable 3-5 current-matrix rows where `stage4B.freezeStatus=IMPLEMENTED_TESTED`, `stage4B.fullOfficialBlockers` contains `NEEDS_ENGINE_SUPPORT`, and representative automated/runtime-window evidence already exists.

## Selected Rows

| Stage | functionalUnit | cardId | collectorId | Card | Effect / oracle | Evidence basis |
|---|---|---:|---|---|---|---|
| 4D-03RX-E | `FU-44f29ad8f7` | 31208 | `OGN·004/298` | 顺劈 | `CLEAVE_OVERWHELM_3` | Existing representative automated evidence: `p2-preflight-play-cleave-overwhelm-attacking-power.fixture.json`, `p4-play-swift-cleave-in-spell-duel-focus.fixture.json`, `ConformanceFixtureRunnerTests`, `rules-evidence-index.md`, `CURRENT_P4_STATUS.md`. |
| 4D-03RY-E | `FU-441cb9fb7f` | 31215 | `OGN·009/298` | 海克斯射线 | `HEXTECH_RAY_DAMAGE_3` | Existing representative automated evidence and Stage 4C-72 overlay: `p2-preflight-play-hextech-ray-damage-stack.fixture.json`, `p2-preflight-hextech-ray-damage-clears-end-turn.fixture.json`, `p6-play-swift-hextech-ray-in-spell-duel-focus.fixture.json`, `ConformanceFixtureRunnerTests`, `rules-evidence-index.md`, `CURRENT_SERVER_RULE_AUDIT.md`. |
| 4D-03RZ-E | `FU-7f4a387b92` | 34842 | `OGN·056/298` | 自适应机器人 | `ADAPTIVE_ROBOT_CONQUER_BOON_PLAY_UNIT` | Existing representative conquer/boon automated evidence: `p2-preflight-play-adaptive-robot-conquer-boon-static.fixture.json`, `ConformanceFixtureRunnerTests`, `rules-evidence-index.md`, `CURRENT_FRONTEND_REBUILD_PLAN.md`. |
| 4D-03SA-E | `FU-bf81341dd2` | 31321 | `OGN·103/298` | 拉文布鲁姆学生 | `RAVENBLOOM_STUDENT_SPELL_TRIGGER_PLAY_UNIT` | Existing representative spell-trigger evidence: `p2-preflight-play-ravenbloom-student-spell-trigger-static.fixture.json`, `p2-preflight-play-ravenbloom-student-spell-power-plus-1.fixture.json`, `p4-play-ravenbloom-student-target-rejected.fixture.json`, `ConformanceFixtureRunnerTests`, `rules-evidence-index.md`, `CURRENT_P4_STATUS.md`. |
| 4D-03SB-E | `FU-2779c06158` | 31352 | `OGN·128/298` | 决斗 | `DUEL_MUTUAL_POWER_DAMAGE` | Existing Stage 4C-82 representative duel damage evidence: `p2-preflight-play-duel-mutual-power-damage.fixture.json`, `p4-play-duel-target-order-rejected.fixture.json`, `ConformanceFixtureRunnerTests`, `rules-evidence-index.md`, `CURRENT_SERVER_RULE_AUDIT.md`, `CURRENT_P4_STATUS.md`. |

## Blocker Disposition

| Stage | Closed in this bundle | Still open after this bundle |
|---|---|---|
| 4D-03RX-E | `NEEDS_ENGINE_SUPPORT` | Full official breadth remains open; battle / cleanup / layer / target-stack breadth remains representative-only. |
| 4D-03RY-E | `NEEDS_ENGINE_SUPPORT` | `NEEDS_FAQ_REVIEW`; full official breadth remains open. |
| 4D-03RZ-E | `NEEDS_ENGINE_SUPPORT` | Full official breadth remains open; battle / cleanup / layer / target-stack breadth remains representative-only. |
| 4D-03SA-E | `NEEDS_ENGINE_SUPPORT` | `NEEDS_FAQ_REVIEW`; full official breadth remains open. |
| 4D-03SB-E | `NEEDS_ENGINE_SUPPORT` | Full official breadth remains open; battle / layer / target-stack breadth remains representative-only. |

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
| payment-cost `NEEDS_AUTOMATED_TEST_EVIDENCE` | 328 | 328 |
| payment-cost `NEEDS_FAQ_REVIEW` | 92 | 92 |
| primary FAQ residual | 61 | 61 |
| `fullOfficialTrue` | 0 | 0 |
| `ready` | false | false |

## Developer-Window Gaps

- FAQ / official adjudication remains required before 海克斯射线 and 拉文布鲁姆学生 can lose `NEEDS_FAQ_REVIEW`.
- Full official coverage remains open for all five selected rows; this candidate does not claim exhaustive battle, layer, cleanup, target-stack, frontend, browser, formal E2E or 1009/811 readiness coverage.
- Any future reduction of currently open automated-evidence, FAQ, payment-cost, hidden-info or fullOfficial blockers must be handled in a separately authorized batch with matching runtime/test/rules evidence.

## Validation Plan

- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- `git diff --check`
- conflict-marker scan over `docs` and `tests`
- matrix count script over the current JSON
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PaymentEngineCoverageAuditTests"`
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureRunnerTests"`
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`

Frontend build / Chrome smoke are not part of this candidate because this bundle changes no frontend/runtime/browser asset or script.

## Validation Results

- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`: passed.
- `git diff --check`: passed.
- conflict-marker scan over `docs` and `tests`: clean.
- matrix count script: passed; current counts are total functional units `811`, snapshot entries `1009`, `NEEDS_ENGINE_SUPPORT=428`, payment-cost `35`, targeting-stack-timing `203`, cleanup-replacement-duration `164`, hidden-info-random-zone `144`, payment-or-targeting-stack-timing `217`, payment-and-targeting-stack-timing `21`, payment-cost automated evidence `328`, payment-cost FAQ review `92`, primary FAQ residual `61`, `fullOfficialTrue=0`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PaymentEngineCoverageAuditTests"`: passed, `697/697`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureRunnerTests"`: passed, `3019/3019`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`: passed, `5344/5344`.
- Frontend build / Chrome smoke were skipped because this bundle changes no frontend/runtime/browser asset or script.

## Why Not Ready

This bundle reduces only row-level `NEEDS_ENGINE_SUPPORT` where existing representative evidence is already present. FAQ review, full official breadth, frontend/browser/formal E2E and final readiness remain open.

## Validation Results

Passed on `DOC_MATRIX_CURRENT`:

- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- `git diff --check`
- conflict-marker scan over `docs` and `tests` returned no matches
- matrix count script: all `NEEDS_ENGINE_SUPPORT=428`, implemented-tested evidence residual `13`
- PaymentEngineCoverageAuditTests: 697/697 passed
- ConformanceFixtureRunnerTests: 3019/3019 passed
- backend full test: 5344/5344 passed

# 4D-03PR..03PV Candidate Audit

Status: validation passed on DOC_MATRIX branch. Project remains **NOT READY**.

## Evidence Audit

The candidate is limited to five row-level matrix blocker reductions. Each selected row has direct-card-behavior runtime support, conformance fixture evidence already present in `ConformanceFixtureRunnerTests.cs`, rules evidence already indexed in `docs/rules-evidence-index.md`, and no FAQ refs in the matrix.

Selected rows: 4D-03PR-E FU-e5572fb7f3 / UNL-082/219 莉莉娅 / LILLIA_PLAY_UNIT_NO_OPTIONAL_HASTE；4D-03PS-E FU-c2cad0d9f7 / UNL-082a/219 莉莉娅 / LILLIA_ALT_A_PLAY_UNIT_NO_OPTIONAL_HASTE；4D-03PT-E FU-6893d75a52 / UNL-088/219 倾颓宫殿 / CRUMBLING_PALACE_PLAY_EQUIPMENT；4D-03PU-E FU-1076624b75 / UNL-089/219 烬 / JHIN_PLAY_UNIT_PREDICT；4D-03PV-E FU-16f3fb60b9 / UNL-089a/219 烬 / JHIN_ALT_A_PLAY_UNIT_PREDICT.

## Count Audit

Expected after counts: all FU NEEDS_ENGINE_SUPPORT 486; payment-cost NEEDS_ENGINE_SUPPORT 88; primary residual 53; targeting-stack-timing NEEDS_ENGINE_SUPPORT 240; cleanup-replacement-duration NEEDS_ENGINE_SUPPORT 189; hidden-info-random-zone NEEDS_ENGINE_SUPPORT 159; payment-or-targeting-stack-timing NEEDS_ENGINE_SUPPORT 275; payment-and-targeting-stack-timing NEEDS_ENGINE_SUPPORT 53. Automated-test evidence remains 328 and FAQ review remains 92.

## Non-Closure Audit

This batch does not mark the project ready, does not close full official coverage, does not close full PaymentEngine/PAY_COST breadth, does not adjudicate FAQ rows, and does not add runtime or test implementation. Any remaining real engine/test/frontend gaps must stay with development windows.

## Validation Plan

Required before commit: jq matrix JSON parse, git diff --check, conflict-marker scan, PaymentEngineCoverageAuditTests, selected conformance fixture evidence, and backend full test. Frontend build / Chrome smoke are not required for this branch-local docs/matrix/audit-baseline sync because no frontend or browser-script files are touched.

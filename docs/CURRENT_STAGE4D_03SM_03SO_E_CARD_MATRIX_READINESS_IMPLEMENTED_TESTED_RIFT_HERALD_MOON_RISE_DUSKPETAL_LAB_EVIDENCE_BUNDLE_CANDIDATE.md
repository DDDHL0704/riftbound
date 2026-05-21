# 4D-03SM..03SO E_CARD_MATRIX_READINESS Implemented-Tested Evidence Bundle Candidate

Status: candidate prepared on `DOC_MATRIX_CURRENT`; project remains **NOT READY**.

## Scope

This bundle is matrix/current-docs plus `PaymentEngineCoverageAuditTests.cs` baseline synchronization only. It does not modify server runtime, frontend runtime, protocol core fields, official catalog data, browser/Chrome scripts, formal 18-step E2E scripts, `fullOfficial`, final readiness flags or `riftbound-dotnet.sln`.

## Selected Rows

| Stage | functionalUnit | collectorId | Card | Effect / oracle | Evidence basis |
|---|---|---|---|---|---|
| 4D-03SM-E | `FU-4e2e19359f` | `UNL-179/219` | 峡谷先锋 | `RIFT_HERALD_MOVE_LAST_BREATH_STATIC` | Existing representative implemented-tested row; payment, cleanup, hidden, layer and target-stack breadth remain open. |
| 4D-03SN-E | `FU-4329e00e20` | `UNL-198/219` | 月之降临 | `MOON_RISE_ENEMY_BATTLEFIELD_MINUS_2_NO_MOVE` | Existing representative implemented-tested row; battle, cleanup, control and target-stack breadth remain open. |
| 4D-03SO-E | `FU-d18ac7cbec` | `UNL-209/219` | 暮色玫瑰实验室 | `BATTLEFIELD_RULE_DOMAIN` | Existing representative implemented-tested battlefield row; cleanup, control, hidden and target-stack breadth remain open. |

## Count Movement

| Metric | Before | After |
|---|---:|---:|
| all functional units `NEEDS_ENGINE_SUPPORT` | 418 | 415 |
| implemented-tested evidence residual | 3 | 0 |
| payment-cost `NEEDS_ENGINE_SUPPORT` | 35 | 34 |
| targeting-stack-timing `NEEDS_ENGINE_SUPPORT` | 197 | 194 |
| cleanup-replacement-duration `NEEDS_ENGINE_SUPPORT` | 158 | 155 |
| hidden-info-random-zone `NEEDS_ENGINE_SUPPORT` | 143 | 141 |
| payment-or-targeting-stack-timing `NEEDS_ENGINE_SUPPORT` | 211 | 208 |
| payment-and-targeting-stack-timing `NEEDS_ENGINE_SUPPORT` | 21 | 20 |
| `NEEDS_AUTOMATED_TEST_EVIDENCE` | 328 | 328 |
| `NEEDS_FAQ_REVIEW` | 92 | 92 |
| primary FAQ residual | 61 | 61 |
| `fullOfficialTrue` | 0 | 0 |
| `ready` | false | false |

## Validation Passed

- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed.
- `git diff --check` passed.
- conflict-marker scan over `docs` and `tests` passed.
- matrix count script over the current JSON passed.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PaymentEngineCoverageAuditTests"` passed 697/697.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureRunnerTests"` passed 3019/3019.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` passed 5344/5344.

Frontend build / Chrome smoke are not part of this candidate because this bundle changes no frontend/runtime/browser asset or script.

## Why Not Ready

This bundle reduces only row-level `NEEDS_ENGINE_SUPPORT` where existing representative evidence is already present. It also closes the current no-idle implemented-tested evidence residual to 0, but FAQ review, full official breadth, frontend/browser/formal E2E and final readiness remain open.

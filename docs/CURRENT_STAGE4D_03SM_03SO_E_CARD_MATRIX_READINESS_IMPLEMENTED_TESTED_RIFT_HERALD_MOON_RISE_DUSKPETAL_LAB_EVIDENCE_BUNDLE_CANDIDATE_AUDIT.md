# 4D-03SM..03SO Candidate Audit

Status: audit prepared on `DOC_MATRIX_CURRENT`; project remains **NOT READY**.

## Authorization And Write Scope

This batch stays under the A_MAIN no-idle implemented-tested evidence scope. It edits only matrix/current docs and `PaymentEngineCoverageAuditTests.cs` baseline synchronization.

## Evidence Audit

| Row | Existing implementation / evidence | Audit note |
|---|---|---|
| `FU-4e2e19359f` / `UNL-179/219` / 峡谷先锋 | Implemented-tested `RIFT_HERALD_MOVE_LAST_BREATH_STATIC` representative row. | Can remove only `NEEDS_ENGINE_SUPPORT`; payment, cleanup, hidden, layer and target-stack breadth remain open. |
| `FU-4329e00e20` / `UNL-198/219` / 月之降临 | Implemented-tested `MOON_RISE_ENEMY_BATTLEFIELD_MINUS_2_NO_MOVE` representative row. | Can remove only `NEEDS_ENGINE_SUPPORT`; battle, cleanup, control and target-stack breadth remain open. |
| `FU-d18ac7cbec` / `UNL-209/219` / 暮色玫瑰实验室 | Implemented-tested `BATTLEFIELD_RULE_DOMAIN` representative battlefield row. | Can remove only `NEEDS_ENGINE_SUPPORT`; cleanup, control, hidden, non-play and target-stack breadth remain open. |

## Count Audit

- all functional units `NEEDS_ENGINE_SUPPORT`: 418 -> 415
- implemented-tested evidence residual: 3 -> 0
- payment-cost `NEEDS_ENGINE_SUPPORT`: 35 -> 34
- targeting-stack-timing `NEEDS_ENGINE_SUPPORT`: 197 -> 194
- cleanup-replacement-duration `NEEDS_ENGINE_SUPPORT`: 158 -> 155
- hidden-info-random-zone `NEEDS_ENGINE_SUPPORT`: 143 -> 141
- payment-or-targeting-stack-timing `NEEDS_ENGINE_SUPPORT`: 211 -> 208
- payment-and-targeting-stack-timing `NEEDS_ENGINE_SUPPORT`: 21 -> 20
- `NEEDS_AUTOMATED_TEST_EVIDENCE`: remains 328
- `NEEDS_FAQ_REVIEW`: remains 92
- primary FAQ residual: remains 61
- `fullOfficialTrue`: remains 0
- `ready`: remains false

## Validation Passed

- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed.
- `git diff --check` passed.
- conflict-marker scan over `docs` and `tests` passed.
- matrix count script over the current JSON passed.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PaymentEngineCoverageAuditTests"` passed 697/697.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureRunnerTests"` passed 3019/3019.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` passed 5344/5344.

Frontend build / Chrome smoke are intentionally skipped for this candidate because no frontend/browser files, server runtime or protocol behavior are changed.

## Closure Judgment

This closes the current implemented-tested evidence `NEEDS_ENGINE_SUPPORT` residual to 0 under the no-idle scope. It is not a final readiness claim: FAQ closure, full official breadth, frontend/browser/formal E2E and final readiness remain open.

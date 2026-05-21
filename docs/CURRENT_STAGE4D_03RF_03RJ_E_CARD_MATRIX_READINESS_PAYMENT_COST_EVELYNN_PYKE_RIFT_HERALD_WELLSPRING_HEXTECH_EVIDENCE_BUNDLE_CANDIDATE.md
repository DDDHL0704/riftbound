# 4D-03RF..03RJ E_CARD_MATRIX_READINESS Payment-Cost Evidence Bundle Candidate

Status: candidate validated on `DOC_MATRIX_CURRENT`; project remains **NOT READY**.

## Scope

This bundle is a matrix/current-docs plus `PaymentEngineCoverageAuditTests.cs` baseline synchronization only. It does not modify server runtime, frontend runtime, protocol core fields, official catalog data, browser/Chrome scripts, formal 18-step E2E scripts, `fullOfficial`, final readiness flags or `riftbound-dotnet.sln`.

## Selected Rows

| Stage | functionalUnit | cardId | collectorId | Card | Effect / oracle | Evidence basis |
|---|---|---:|---|---|---|---|
| 4D-03RF-E | `FU-7d0b8868b7` | 34684 | `UNL-141/219` | 伊芙琳 | `EVELYNN_STANDBY_BACK_ROW_MOVE_STATIC` | Existing direct-card-behavior matrix implementation; automated disposition remains open. |
| 4D-03RG-E | `FU-f076dbf9ee` | 34689 | `UNL-145/219` | 派克 | `PYKE_STANDBY_BACK_ROW_GOLD_STATIC` | Existing direct-card-behavior matrix implementation; automated disposition remains open. |
| 4D-03RH-E | `FU-f9eb8c6f71` | 34732 | `UNL-179a/219` | 峡谷先锋 | `RIFT_HERALD_ALT_A_MOVE_LAST_BREATH_STATIC` | Existing direct-card-behavior matrix implementation; automated disposition remains open. |
| 4D-03RI-E | `FU-f9291060df` | 34739 | `UNL-186/219` | 涌泉之恨 | `WELLSPRING_OF_HATRED_DESTROY_BATTLEFIELD_UNIT` | Existing direct-card-behavior matrix implementation; automated disposition remains open. |
| 4D-03RJ-E | `FU-3febd422bc` | 34741 | `UNL-188/219` | 海克斯科技护手 | `HEXTECH_GAUNTLET_PLAY_EQUIPMENT` | Existing direct-card-behavior matrix implementation; automated disposition remains open. |

## Count Movement

| Metric | Before | After |
|---|---:|---:|
| all functional units `NEEDS_ENGINE_SUPPORT` | 451 | 446 |
| payment-cost `NEEDS_ENGINE_SUPPORT` | 53 | 48 |
| primary payment-cost residual | 18 | 13 |
| targeting-stack-timing `NEEDS_ENGINE_SUPPORT` | 220 | 216 |
| cleanup-replacement-duration `NEEDS_ENGINE_SUPPORT` | 176 | 173 |
| hidden-info-random-zone `NEEDS_ENGINE_SUPPORT` | 150 | 146 |
| payment-or-targeting-stack-timing `NEEDS_ENGINE_SUPPORT` | 240 | 235 |
| payment-and-targeting-stack-timing `NEEDS_ENGINE_SUPPORT` | 33 | 29 |
| `NEEDS_AUTOMATED_TEST_EVIDENCE` | 328 | 328 |
| `NEEDS_FAQ_REVIEW` | 92 | 92 |
| primary FAQ residual | 61 | 61 |
| `fullOfficialTrue` | 0 | 0 |
| `ready` | false | false |

## Blocker Disposition

Closed only for the selected row-level matrix entries:

- `NEEDS_ENGINE_SUPPORT` is removed from the five selected functional units and corresponding snapshot entries.
- Their `stage4B.freezeStatus` moves from `NEEDS_ENGINE_SUPPORT` to `IMPLEMENTED_UNTESTED`.

Still open:

- `NEEDS_AUTOMATED_TEST_EVIDENCE` remains open for all five selected functional units.
- Complete PaymentEngine / `PAY_COST` breadth remains open.
- Standby / back-row movement, Last Breath movement, equipment/layer/control, cleanup, hidden-info and targeting breadth remain open beyond this row-level evidence sync.

## Validation Results

Passed on `DOC_MATRIX_CURRENT`:

- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- `git diff --check`
- conflict-marker scan over `docs` and `tests` returned no matches
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PaymentEngineCoverageAuditTests"`: 697/697 passed
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureRunnerTests"`: 3019/3019 passed
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`: 5344/5344 passed

Frontend build and Chrome smoke were not run because this batch changed only matrix/current docs and the narrow payment audit baseline, with no frontend runtime or browser-script changes.

## Why Not Ready

This bundle only closes five row-level `NEEDS_ENGINE_SUPPORT` blockers where existing implementation evidence already exists. It does not claim complete official coverage, automated evidence closure, FAQ closure, P0/P1 closure, frontend validation, Chrome/browser smoke, formal 18-step E2E or final project readiness.

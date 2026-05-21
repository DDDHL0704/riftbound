# 4D-03RF..03RJ Candidate Audit

Status: audit validated on `DOC_MATRIX_CURRENT`; project remains **NOT READY**.

## Authorization And Write Scope

Shared-board latest actionable entry releases `DOC_MATRIX_CURRENT` under `APPROVED_ACTIVE_NO_IDLE`. This 03RF-03RJ batch keeps the allowed matrix lane scope:

- Allowed: `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`, current checkpoint docs, per-bundle candidate/audit docs and the narrow `PaymentEngineCoverageAuditTests.cs` expected-count/current-slice baseline sync.
- Not touched: server runtime, frontend runtime, API/protocol core fields, official catalog data, browser/Chrome/formal E2E scripts, `fullOfficial`, final readiness flags and `riftbound-dotnet.sln`.

## Evidence Audit

| Row | Existing implementation | Evidence note |
|---|---|---|
| `FU-7d0b8868b7` / `UNL-141/219` / 伊芙琳 | direct-card-behavior `EVELYNN_STANDBY_BACK_ROW_MOVE_STATIC` | Row-level engine-support blocker can move to automated-evidence residual only; full official breadth remains open. |
| `FU-f076dbf9ee` / `UNL-145/219` / 派克 | direct-card-behavior `PYKE_STANDBY_BACK_ROW_GOLD_STATIC` | Row-level engine-support blocker can move to automated-evidence residual only; full official breadth remains open. |
| `FU-f9eb8c6f71` / `UNL-179a/219` / 峡谷先锋 | direct-card-behavior `RIFT_HERALD_ALT_A_MOVE_LAST_BREATH_STATIC` | Row-level engine-support blocker can move to automated-evidence residual only; full official breadth remains open. |
| `FU-f9291060df` / `UNL-186/219` / 涌泉之恨 | direct-card-behavior `WELLSPRING_OF_HATRED_DESTROY_BATTLEFIELD_UNIT` | Row-level engine-support blocker can move to automated-evidence residual only; full official breadth remains open. |
| `FU-3febd422bc` / `UNL-188/219` / 海克斯科技护手 | direct-card-behavior `HEXTECH_GAUNTLET_PLAY_EQUIPMENT` | Row-level engine-support blocker can move to automated-evidence residual only; full official breadth remains open. |

## Count Audit

The draft moves:

- all functional units `NEEDS_ENGINE_SUPPORT`: 451 -> 446
- payment-cost `NEEDS_ENGINE_SUPPORT`: 53 -> 48
- primary payment-cost residual: 18 -> 13
- targeting-stack-timing `NEEDS_ENGINE_SUPPORT`: 220 -> 216
- cleanup-replacement-duration `NEEDS_ENGINE_SUPPORT`: 176 -> 173
- hidden-info-random-zone `NEEDS_ENGINE_SUPPORT`: 150 -> 146
- payment-or-targeting-stack-timing `NEEDS_ENGINE_SUPPORT`: 240 -> 235
- payment-and-targeting-stack-timing `NEEDS_ENGINE_SUPPORT`: 33 -> 29
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
- complete FEPR target/stack lifecycle where applicable
- cleanup / hidden / control / layer / battle-spell-duel breadth where applicable
- frontend/browser/formal E2E gates

## Validation Results

Passed on `DOC_MATRIX_CURRENT`:

- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- `git diff --check`
- conflict-marker scan over `docs` and `tests` returned no matches
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PaymentEngineCoverageAuditTests"`: 697/697 passed
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureRunnerTests"`: 3019/3019 passed
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`: 5344/5344 passed

Frontend build and Chrome smoke are not required for this batch because no frontend runtime or browser script changed.

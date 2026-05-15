# Stage 4D-03BO PaymentEngine Official Matrix Downstream Aggregate Handoff

Audit date: 2026-05-16
Conclusion: **A-SIDE HANDOFF / FUTURE B DISPATCH BOUNDARY / PROJECT NOT READY**

## 1. Scope

4D-03BO follows the accepted 4D-03BN card matrix alignment all-window verifier.

This handoff does not implement a new verifier. It fixes the next narrow B-side boundary: aggregate the current official PaymentEngine matrix row schema with the three downstream all-window representative matrices that now exist after 4D-03BL-B, 4D-03BM and 4D-03BN.

This batch only changes A-side handoff / baseline / checkpoint docs. It does not change runtime behavior, tests, frontend behavior, browser smoke scripts, card matrix JSON, `fullOfficial`, READY status, P0-005 closure, or `riftbound-dotnet.sln`.

## 2. Current Inputs

The current `PaymentEngineCoverageAuditTests` shape is:

- 12 official matrix residual axes in `OfficialPaymentEngineMatrixResidualManifest`
- 13 official row-schema entries in `OfficialPaymentEngineMatrixSeedRowManifest`
- 9 `representative-seed` rows
- 3 `missing-official-row` rows
- 1 `policy-deferred-row` for `ROW_MOVE_UNIT_POLICY_DEFERRED`

The three missing official rows now each have representative downstream coverage:

| Missing official row | Family manifest | Family count | All-window manifest | Matrix count | Latest evidence |
|---|---:|---:|---|---:|---|
| `ROW_ROLLBACK_FAILURES_OFFICIAL_MATRIX_MISSING` | `RollbackFailureRowManifest` | 7 | `RollbackFailureAllWindowMatrixManifest` | 42 | 4D-03BE / 4D-03BL-B |
| `ROW_CROSS_WINDOW_GENERATION_CONSUMPTION_MISSING` | `CrossWindowGenerationConsumptionRowManifest` | 7 | `CrossWindowGenerationConsumptionAllWindowMatrixManifest` | 42 | 4D-03BF / 4D-03BM |
| `ROW_CARD_MATRIX_ALIGNMENT_MISSING` | `CardMatrixAlignmentRowManifest` | 8 | `CardMatrixAlignmentAllWindowMatrixManifest` | 48 | 4D-03BG / 4D-03BN |

## 3. Future B Scope

Future 4D-03BO-B should be a test/docs-only verifier unless it exposes a concrete mismatch.

Allowed write scope:

- `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`
- `docs/CURRENT_STAGE4D_03BO_PAYMENT_ENGINE_OFFICIAL_MATRIX_DOWNSTREAM_AGGREGATE_AUDIT.md`
- `docs/CURRENT_STAGE4D_03BO_PAYMENT_ENGINE_OFFICIAL_MATRIX_DOWNSTREAM_AGGREGATE_EVIDENCE.md`

Expected verifier contract:

1. Preserve official row-schema counts: 9 representative seeds, 3 missing official rows and 1 MOVE_UNIT policy-deferred row.
2. Require every missing official row to have exactly one downstream aggregate entry.
3. Require downstream representative family counts and all-window matrix counts to match current manifests: 7/42, 7/42 and 8/48.
4. Require every downstream all-window matrix row to point back to its matching official row id.
5. Require aggregate doc anchors to include 4D-03BO audit / evidence and the source downstream docs.
6. Keep `MOVE_UNIT`, `HIDE_CARD` and `LEGEND_ACT` outside PaymentEngine payment surfaces for this aggregate.
7. Continue to assert `NOT READY`, P0-005 open, no `fullOfficial=true` and no READY claim.

## 4. No-Go Scope

Future 4D-03BO-B must not touch:

- runtime files under `src/**`
- frontend runtime or browser smoke scripts
- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- broad PaymentEngine rewrites
- battle lifecycle / cleanup queues
- LayerEngine or P1 keyword implementation
- `fullOfficial` / READY status
- `riftbound-dotnet.sln`

## 5. Required Validation

Future B-side focused validation:

```sh
source scripts/dev-env.sh
dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter FullyQualifiedName~PaymentEngineCoverageAuditTests
git diff --check
```

A-side acceptance validation after B returns:

```sh
source scripts/dev-env.sh
dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter FullyQualifiedName~PaymentEngineCoverageAuditTests
dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PaymentEngineCoverageAuditTests|FullyQualifiedName~PaymentEngineUnificationTests|FullyQualifiedName~ResourceSkill|FullyQualifiedName~SpellshieldTax|FullyQualifiedName~HasteReady|FullyQualifiedName~TriggerPayment|FullyQualifiedName~LegendAct|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub"
dotnet test Riftbound.slnx --no-restore
git diff --check
```

## 6. Remaining Risk

This handoff does not close full official PaymentEngine coverage. It only reserves the next aggregate verifier boundary so the existing downstream representative matrices can be checked against the official matrix row schema without promoting representative evidence to full official status.

Project status remains **NOT READY**; P0-005 remains open.

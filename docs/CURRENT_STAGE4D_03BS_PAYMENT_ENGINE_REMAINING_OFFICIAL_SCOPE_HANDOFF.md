# Stage 4D-03BS PaymentEngine Remaining Official Scope Handoff

Audit date: 2026-05-16
Conclusion: **A-SIDE HANDOFF / FUTURE B OR E DISPATCH BOUNDARY / PROJECT NOT READY**

## 1. Scope

4D-03BS follows the accepted 4D-03BR-B target / tax activated ability matrix verifier and the 4D-FE build / Chrome smoke / formal 18 fresh-runs.

This handoff does not implement runtime behavior, tests, frontend behavior or card matrix changes. It records the next A-side routing boundary for the remaining P0-005 PaymentEngine official scope after 4D-03BR-B: future work must move from representative all-window verifier coverage toward full official payment breadth, without promoting the current representative matrix to READY or `fullOfficial=true`.

The handoff is intentionally a routing checkpoint. It prevents the next worker from treating the green 4D-03BR-B matrix, backend full test, Chrome smoke or formal 18 E2E as a completion proxy.

## 2. Current Inputs

Current accepted PaymentEngine evidence:

- 4D-03BR-B: 8 target-bearing / typed / experience / Spellshield-tax activated ability entries x 6 target/payment dimensions = 48 executable target/tax rows.
- 4D-03BQ-B: 6 resource-skill family entries x 6 payment surfaces = 36 executable resource-skill rows.
- 4D-03BP-B: 8 keyword payment branch entries x 6 payment surfaces = 48 executable keyword branch rows.
- 4D-03BO-B: official row schema plus downstream all-window matrices aggregated into one executable audit contract.
- 4D-03BL-B / 03BM / 03BN: rollback, cross-window generated-resource and card-matrix alignment all-window matrices accepted.

Current matrix facts:

- Fixed source catalog: `data/official/card-catalog.zh-CN.json`, fetched at `2026-04-27`.
- Snapshot entries: 1009.
- Functional units: 811.
- Current full-official count: 0 functional units.
- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` must remain untouched until A opens an explicit E-side matrix write window.

Current remaining official breadth is still broad:

- complete target-bearing activated ability family beyond the current 8 representative entries
- complete `[A]` / `[C]` resource skill family
- complete keyword payment branch parity
- complete target tax, optional / extra / alternative costs, cost modifiers and replacement / prevention payment interactions
- complete resource action windows across rune / legend / battlefield / trigger routes
- complete rollback, cross-window generated-resource lifetime and duplicate-spend matrix
- card-matrix full-official alignment for 1009 card entries / 811 functional units

## 3. Future Dispatch Boundary

Future work after this handoff must choose one explicit owner and one explicit write window:

1. **B-side PaymentEngine official breadth verifier / implementation slice**
   Allowed only after a fresh A dispatch. Candidate scope: expand one remaining official PaymentEngine family into executable prompt / command / audit / rollback tests, or minimally fix a concrete mismatch found by those tests.

2. **E-side card matrix readiness slice**
   Allowed only after a fresh A dispatch. Candidate scope: map accepted PaymentEngine representatives back to affected functional units and blockers without upgrading `fullOfficial=true`.

3. **D-side completion / P0 audit slice**
   Allowed only after a fresh A dispatch. Candidate scope: prove which residual P0-005 items remain after the current all-window matrices and which require runtime work rather than more representative evidence.

This 4D-03BS handoff does not dispatch any of those workers. It records the boundary and current baseline only.

## 4. No-Go Scope

This batch and any future worker inheriting this handoff must not touch without a fresh A write lock:

- runtime files under `src/**`
- frontend runtime, stores, views or browser smoke scripts
- formal 18-step scripts
- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- broad PaymentEngine rewrites
- battle lifecycle / cleanup queues
- LayerEngine or keyword execution implementation
- `fullOfficial` / READY status
- `riftbound-dotnet.sln`

## 5. Required Validation

Baseline validation for this handoff:

```sh
source scripts/dev-env.sh
dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter FullyQualifiedName~PaymentEngineCoverageAuditTests
dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PaymentEngineCoverageAuditTests|FullyQualifiedName~PaymentEngineUnificationTests|FullyQualifiedName~ResourceSkill|FullyQualifiedName~SpellshieldTax|FullyQualifiedName~HasteReady|FullyQualifiedName~TriggerPayment|FullyQualifiedName~LegendAct|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub"
dotnet test Riftbound.slnx --no-restore
git diff --check
```

Future acceptance for any dispatched implementation or verifier must rerun the focused command, an adjacent command chosen for the actual files touched, backend full test and `git diff --check`.

## 6. Remaining Risk

4D-03BS does not close P0-005, P1, full-card matrix or READY. It only converts the post-4D-03BR-B state into an explicit next-routing boundary so the next batch can make real progress without confusing representative matrices with full official closure.

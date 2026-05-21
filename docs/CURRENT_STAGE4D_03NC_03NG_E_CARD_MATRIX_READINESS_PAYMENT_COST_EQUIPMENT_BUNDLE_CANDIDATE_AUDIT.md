# 4D-03NC-E..4D-03NG-E Candidate Audit

Authorization: main shared-board `2026-05-21 15:07 A_MAIN` approved DOC_MATRIX_CURRENT for one post-03NB payment-cost matrix/audit-baseline bundle, with runtime/frontend/protocol/official catalog/general tests/READY locked.

Why these rows are docs/audit-test syncable:

- Each selected row has existing service behavior and existing conformance fixtures for the representative play-equipment route.
- Each selected row has existing rules-evidence index entries tying the fixture to official catalog and core rules evidence.
- The selected rows do not require new FAQ interpretation and do not reduce FAQ counts.
- The selected rows do not require hidden-info adjudication, runtime work, frontend work, protocol fields, or official catalog changes.

Before -> after:

- all FU `NEEDS_ENGINE_SUPPORT`: 558 -> 553
- payment-cost `NEEDS_ENGINE_SUPPORT`: 160 -> 155
- primary residual: 119 -> 115
- targeting-stack-timing: 288 -> 288
- cleanup-replacement-duration: 215 -> 215
- hidden-info-random-zone: 176 -> 176
- payment-or-targeting-stack-timing: 347 -> 342
- payment-and-targeting-stack-timing: 101 -> 101
- automated evidence residual: 328 -> 328
- FAQ residual: 92 -> 92
- fullOfficialTrue: 0 -> 0
- ready: false -> false

Closed in this bundle:

- 4D-03NC-E: Warmog's Armor row-level `NEEDS_ENGINE_SUPPORT`
- 4D-03ND-E: Trinity Force row-level `NEEDS_ENGINE_SUPPORT`
- 4D-03NE-E: Bone Club ordinary/promo shared-oracle row-level `NEEDS_ENGINE_SUPPORT`
- 4D-03NF-E: Doran's Ring row-level `NEEDS_ENGINE_SUPPORT`
- 4D-03NG-E: Boots of Swiftness row-level `NEEDS_ENGINE_SUPPORT`

Still open:

- `NEEDS_AUTOMATED_TEST_EVIDENCE` for all selected rows
- full equipment attach/follow lifecycle and LayerEngine / continuous-effect breadth
- control-zone movement breadth
- complete PaymentEngine / PAY_COST matrix
- P0/P1 closure, frontend/formal E2E, `fullOfficial`, E_CARD_MATRIX_READINESS, full card matrix, and READY

Validation results:

- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed.
- `git diff --check` passed.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PaymentEngineCoverageAuditTests"` passed 661/661.
- selected evidence filter for WarmogsArmor / TrinityForce / BoneClub / BoneclubPromo / DoransRing / BootsOfSwiftness passed 24/24.
- backend full `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` passed 5237/5237 because `PaymentEngineCoverageAuditTests.cs` is touched.

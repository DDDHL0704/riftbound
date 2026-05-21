# 4D-03LB-E payment-cost Hirana Monastery battlefield boon-draw hidden/layer targeting-stack blocker closure candidate

4D-03LB-E records a single row-level E_CARD_MATRIX_READINESS blocker reduction for the Hirana Monastery battlefield rule-domain representative row. The selected functional unit is `FU-f7196a5ead`; selected card is `OGN·282/298` 希拉娜修道院; selected effect is `BATTLEFIELD_RULE_DOMAIN`.

## Scope
- Matrix-only row transition: `NEEDS_ENGINE_SUPPORT` -> `IMPLEMENTED_UNTESTED`.
- The FU and snapshot entry keep `fullOfficial=false` and `ready=false`.
- Runtime, frontend, Chrome/browser scripts, protocol core fields and official catalog are locked.

## Evidence
- `CoreRuleEngine` has representative battlefield conquered resolution for OGN·282/298.
- `ConformanceFixtureRunnerTests.P79BattlefieldConquerConsumesBoonAndDraws` covers conquered trigger, boon consumption and draw.
- `ConformanceFixtureRunnerTests.P79BattlefieldConquerConsumesControlledBoonWhenDirtyBoonIsOpponentOwned` covers controlled-boon selection and dirty opponent-owned boon rejection.
- `GameHubJoinTests.P79BattlefieldConquerBoonDrawSeedOffersBattlefieldDestinationAndConsumesBoon` covers server-authored prompt destination and authoritative snapshot result.
- `CardCatalogBaselineTests.P6BattlefieldRuleDomainSurfacesReportManualBoundaryCoverage` covers battlefield rule-domain catalog implementation boundary for OGN·282/298.

## Non Closure
- Hirana Monastery automated evidence disposition remains open.
- Complete battlefield rule-domain official breadth remains open.
- Complete conquer / combat-assignment / battle-spell-duel lifecycle remains open.
- Boon consume / draw hidden-info breadth remains open.
- Complete LayerEngine / continuous-effect matrix remains open.
- Complete FEPR target / stack lifecycle matrix remains open.
- Complete PaymentEngine / PAY_COST matrix remains open.
- READY remains open.

## Validation
validation passed: matrix JSON valid (jq empty); 03LB active-goal guard 1/1; PaymentEngineCoverageAuditTests 580/580; Hirana/Battlefield focused regression 77/77; adjacent prompt/battlefield/hidden/boon regression 452/452; backend full 5151/5151; git diff --check passed.

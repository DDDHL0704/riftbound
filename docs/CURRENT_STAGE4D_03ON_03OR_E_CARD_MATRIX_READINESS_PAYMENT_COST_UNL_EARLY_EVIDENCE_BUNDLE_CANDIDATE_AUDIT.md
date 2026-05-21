# 4D-03ON..03OR E_CARD_MATRIX_READINESS Payment-Cost UNL Early Evidence Bundle Audit

Status: validated on DOC_MATRIX_CURRENT branch before checkpoint commit.

## Audit Result

The selected Arena Councilor, Merfolk Rabble Rouser, Revna, Withered Battleaxe and Dancing Grenade rows are eligible for a matrix + audit-test baseline synchronization because the repository already contains runtime handlers, existing fixture tests and indexed rules evidence, and the matrix has no FAQ refs for these rows. The bundle does not claim full official coverage.

## Functional Unit Review

- FU-a0023d7dc7 / UNL-001/219 竞技场理事: direct-card behavior exists; fixture evidence covers base-cost active unit play-to-base; rules evidence records the RULE_AUDITED row and explicitly leaves tap / exhausted power-modifier breadth open.
- FU-194c419d09 / UNL-003/219 鲛人滋事者: direct-card behavior exists; fixture evidence covers base-cost standby unit play-to-base; rules evidence records the RULE_AUDITED row and explicitly leaves face-down standby placement, reaction play and enemy battlefield damage breadth open.
- FU-fae4cb2b90 / UNL-005/219 传承者雷芙纳: direct-card behavior exists; fixture evidence covers roam keyword unit play-to-base; existing runner target assertions keep the representative base target wired.
- FU-c751ddbacb / UNL-019/219 枯萎战斧: direct-card behavior exists; fixture evidence covers play-equipment, explicit target rejection and assemble-red attach; rules evidence records all three rows while complete equipment attach/follow lifecycle remains open.
- FU-3afa21c91d / UNL-020/219 曼舞手雷: direct-card behavior exists; fixture evidence covers base unit damage without recast; rules evidence records the RULE_AUDITED row and explicitly leaves optional recast / repeated-damage breadth open.

## Non-Closure

This is not a project readiness closure. Automated evidence disposition, standby hidden-information breadth, roam/control-zone breadth, equipment lifecycle, recast branch, cleanup / layer / targeting-stack breadth, complete PaymentEngine, P0/P1 and final readiness remain open.

## Validation

- jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json: passed.
- git diff --check: passed.
- source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PaymentEngineCoverageAuditTests": passed, 675/675.
- source scripts/dev-env.sh && dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~ConformanceFixtureRunnerTests": passed, 3019/3019.
- source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore: passed, 5251/5251.
- Frontend build / Chrome smoke not run because this bundle has no frontend or browser-script changes.

# 4D-03OS..03OW E_CARD_MATRIX_READINESS Payment-Cost UNL Early Evidence Bundle Audit

Status: validated on DOC_MATRIX_CURRENT branch before checkpoint commit.

## Audit Result

The selected Jhin, Jhin alt, Undead Legion, Xerath and Pyke alt rows are eligible for a matrix + audit-test baseline synchronization because the repository already contains runtime handlers, existing fixture tests and indexed rules evidence, and the matrix has no FAQ refs for these rows. The bundle does not claim full official coverage.

## Functional Unit Review

- FU-88a67bee9c / UNL-022/219 烬: direct-card behavior exists; fixture evidence covers base-cost Spellshield / roam keyword unit play-to-base; rules evidence records the RULE_AUDITED row while movement resource-trigger breadth remains open.
- FU-6167a33f64 / UNL-022a/219 烬: direct-card behavior exists; fixture evidence covers the alt-A Spellshield / roam keyword unit play-to-base; rules evidence records the RULE_AUDITED row while movement resource-trigger breadth remains open.
- FU-fa568fbe53 / UNL-025/219 不死军团: direct-card behavior exists; fixture evidence covers hand play as a Spirit unit; rules evidence records the RULE_AUDITED row while graveyard encourage, extra-cost and broader cleanup / hidden-info paths remain open.
- FU-523f9b22de / UNL-026/219 泽拉斯: direct-card behavior exists; fixture evidence covers ordinary hand play as a unit; indexed P4 evidence separately covers the representative activated damage skill, Spellshield tax, insufficient-tax rejection and related target/source guards while full activated-skill breadth remains open.
- FU-88fe3a652e / UNL-028a/219 派克: direct-card behavior exists; fixture evidence covers both no-optional and optional red ready-power branches; rules evidence records both rows while standby reaction timing and roam movement breadth remain open.

## Non-Closure

This is not a project readiness closure. Automated evidence disposition, Jhin / Jhin alt Spellshield roam breadth, Undead Legion graveyard and extra-cost breadth, Xerath activated-skill breadth, Pyke standby / reaction / roam breadth, cleanup / hidden-info / targeting-stack breadth, complete PaymentEngine, P0/P1 and final readiness remain open.

## Validation

- jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json: passed.
- git diff --check: passed.
- source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PaymentEngineCoverageAuditTests": passed, 676/676.
- source scripts/dev-env.sh && dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~ConformanceFixtureRunnerTests": passed, 3019/3019.
- source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore: passed, 5252/5252.
- Frontend build / Chrome smoke not run because this bundle has no frontend or browser-script changes.

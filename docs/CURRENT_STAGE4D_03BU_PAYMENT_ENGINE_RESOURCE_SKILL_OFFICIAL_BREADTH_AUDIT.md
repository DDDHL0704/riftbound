# Stage 4D-03BU PaymentEngine Resource Skill Official Breadth Audit

Audit date: 2026-05-16
Conclusion: **TEST-ONLY OFFICIAL BREADTH RECONCILIATION / PROJECT NOT READY**

## 1. Scope

4D-03BU follows the 4D-03BU A-side handoff / baseline and turns the resource skill official breadth boundary into executable audit evidence.

This batch only modifies `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs` and A-side audit / checkpoint docs. It does not modify runtime behavior, frontend behavior, browser scripts, formal 18-step scripts, card matrix JSON, `fullOfficial`, READY status or `riftbound-dotnet.sln`.

## 2. Implemented Verifier

`PaymentEngineCoverageAuditTests` now includes `ResourceSkillOfficialBreadthManifest`.

The verifier reads the fixed `data/official/card-catalog.zh-CN.json` snapshot and scans for official resource-skill candidates using the official resource-skill reminder text:

- `{{获得}}`
- `获得费用资源的技能无法成为其他法术的反应目标`

It fixes the current official breadth as:

- 32 official resource-skill candidate snapshot entries.
- 19 current implemented `P4ActivatedAbilityCatalog.IsResourceSkill=true` source card nos.
- 13 deferred official resource-skill candidates that still need a future B-side verifier / implementation slice.

The implemented candidates remain representative evidence only. The deferred candidates are explicitly retained as NOT READY / P0-005-open official breadth.

## 3. Deferred Official Candidates

The deferred set remains concrete and executable:

- Jhin movement-triggered mana plus power generated resource skill.
- Honeyfruit reaction resource skill plus level-six upgraded branch.
- Blue Sentinel held-battlefield delayed next-main generated power branch.
- Diana spell-duel-only generated mana restriction branch.
- Ornn Forge equipment-only generated power restriction branch and reprint.
- Lux spell-only generated mana restriction branch.
- KaiSa spell-only generated power restriction branch and premium variants.
- Darius Inspire-gated generated mana branch and premium variants.

Future B-side work must prove prompt, command, audit, generated-resource lifetime, restriction text and no-mutation rollback behavior before any of these sources can leave deferred status.

## 4. Validation

Commands run:

```sh
source scripts/dev-env.sh && dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter FullyQualifiedName~PaymentEngineCoverageAuditTests
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PaymentEngineCoverageAuditTests|FullyQualifiedName~PaymentEngineUnificationTests|FullyQualifiedName~ResourceSkill|FullyQualifiedName~SpellshieldTax|FullyQualifiedName~HasteReady|FullyQualifiedName~TriggerPayment|FullyQualifiedName~LegendAct|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub"
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
git diff --check
```

Results:

- Focused PaymentEngine coverage guard: passed 115/115.
- Adjacent PaymentEngine / resource skill / prompt / hub regression: passed 673/673.
- Backend full: passed 4552/4552.
- `git diff --check`: passed.

## 5. Non-Closure

4D-03BU does not close P0-005, P1, full-card matrix, frontend final validation or READY. It makes the resource skill official breadth gap executable and preserves all 13 deferred official candidates for future implementation or stronger verifier work.

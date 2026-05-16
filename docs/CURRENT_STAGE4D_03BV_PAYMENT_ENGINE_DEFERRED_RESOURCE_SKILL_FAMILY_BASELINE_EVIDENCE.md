# Stage 4D-03BV PaymentEngine Deferred Resource Skill Family Baseline Evidence

Audit date: 2026-05-16
Conclusion: **BASELINE / NO WORKER DISPATCHED / PROJECT NOT READY**

## 1. Baseline Scope

This baseline records the current repository state before any future 4D-03BV-B deferred resource-skill family dispatch.

This batch is docs-only. It does not modify runtime, tests, frontend, browser scripts, card matrix JSON, `fullOfficial`, READY status or `riftbound-dotnet.sln`.

## 2. Repository Facts

- Branch: `main`.
- Latest commit before this handoff batch: `7bba02ee test: verify resource skill official breadth`.
- Expected untracked file: `riftbound-dotnet.sln`.
- 4D-03BU official breadth verifier has been accepted.
- Active goal remains **NOT READY**.

## 3. Current Evidence Facts

- `PaymentEngineCoverageAuditTests.cs` contains `ResourceSkillOfficialBreadthManifest`, which fixes 32 official resource-skill candidate snapshot entries, 19 implemented source card nos and 13 deferred official candidates.
- The 13 deferred official candidates are `UNL-022/219`, `UNL-049/219`, `UNL-087/219`, `UNL-197/219`, `SFD·189/221`, `SFD·244/221`, `OGS·014/024`, `OGN·247/298`, `OGN·253/298`, `OGN·299/298`, `OGN·299*/298`, `OGN·302/298` and `OGN·302*/298`.
- Fixed official catalog text groups those candidates into movement-triggered, equipment reaction, held-battlefield delayed, spell-duel-only, equipment-only, spell-only and Inspire-gated generated-resource branches.
- `MatchSession.cs` and `CoreRuleEngine.cs` already contain Darius / Diana / KaiSa / Ornn `LEGEND_ACT` resource definitions, and `ConformanceFixtureRunnerTests.cs` has focused representative `LEGEND_ACT` assertions for those legend resource actions.
- Those existing legend representatives do not close the 4D-03BU `RESOURCE_SKILLS` deferred set by proxy because the current PaymentEngine resource-skill closure contract is still tied to `IsResourceSkill=true`, official breadth, generated-resource lifecycle, payment-only restrictions and matrix closure.
- `CardBehaviorRegistry.cs` and fixture coverage include play / preflight evidence for Jhin, Honeyfruit, Blue Sentinel and Lux, but 4D-03BU proves their official resource-skill text is not currently represented by `P4ActivatedAbilityCatalog.IsResourceSkill=true`.
- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` remains full-official incomplete and must not be updated by this baseline.

## 4. Baseline Validation

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

This baseline does not prove full official PaymentEngine resource skill closure. It preserves the open state for:

- complete official `[A]` / `[C]` resource skill family breadth;
- the 9 legend resource-action candidates that need an explicit bridge into resource-skill closure or a stronger verifier;
- the 4 non-legend unit / equipment / delayed resource-skill candidates that still need runtime / verifier breadth;
- generated-resource lifecycle, payment-only restrictions, invalid timing, invalid target, wrong trait, duplicate source and no-mutation rollback branches;
- official card-matrix alignment for 1009 snapshot entries / 811 functional units.

Project status remains **NOT READY** and the active goal must remain open.

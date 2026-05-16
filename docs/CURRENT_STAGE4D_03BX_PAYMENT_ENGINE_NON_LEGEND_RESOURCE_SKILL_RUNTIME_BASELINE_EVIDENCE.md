# Stage 4D-03BX PaymentEngine Non-Legend Resource Skill Runtime Baseline Evidence

Audit date: 2026-05-16
Conclusion: **BASELINE / NO WORKER DISPATCHED / PROJECT NOT READY**

## 1. Baseline Scope

This baseline records the current repository state before any future 4D-03BX-B non-legend deferred resource-skill runtime / verifier dispatch.

This batch is docs-only. It does not modify runtime, tests, frontend, browser scripts, card matrix JSON, `fullOfficial`, READY status or `riftbound-dotnet.sln`.

## 2. Repository Facts

- Branch: `main`.
- Latest commit before this handoff batch: `9e22fd03 test: verify deferred resource skill families`.
- Expected untracked file: `riftbound-dotnet.sln`.
- 4D-03BW deferred resource skill family verifier has been accepted.
- Active goal remains **NOT READY**.

## 3. Current Evidence Facts

- `PaymentEngineCoverageAuditTests.cs` contains `DeferredResourceSkillFamilyManifest`, which exactly splits the 13 deferred official resource-skill candidates into 9 legend bridge candidates and 4 non-legend runtime / verifier candidates.
- The 4 non-legend candidates are `UNL-022/219`, `UNL-049/219`, `UNL-087/219` and `OGS·014/024`.
- Current Jhin, Honeyfruit, Blue Sentinel and Lux play / preflight evidence is useful as an implementation input, but 4D-03BW proves it does not close `RESOURCE_SKILLS` by proxy.
- The future B implementation must prove server-owned prompt exposure, command validation, generated resource audit, payment-only lifetime and no-mutation rollback.
- The 9 legend `LEGEND_ACT` bridge candidates remain outside this 4D-03BX non-legend runtime slice and still require their own explicit A dispatch.
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

- Focused PaymentEngine coverage guard: passed 119/119.
- Adjacent PaymentEngine / resource skill / prompt / hub regression: passed 677/677.
- Backend full: passed 4556/4556.
- `git diff --check`: passed.

## 5. Non-Closure

This baseline does not prove full official PaymentEngine resource skill closure. It preserves the open state for:

- Jhin movement-triggered generated mana / power resource skill;
- Honeyfruit equipment reaction and level-six upgraded generated resource branch;
- Blue Sentinel held-battlefield delayed next-main generated power branch;
- Lux spell-only tap reaction generated mana branch;
- the 9 legend bridge candidates outside this slice;
- generated-resource lifecycle, payment-only restrictions, invalid timing, invalid target / source, wrong resource / spell and no-mutation rollback branches;
- official card-matrix alignment for 1009 snapshot entries / 811 functional units.

Project status remains **NOT READY** and the active goal must remain open.

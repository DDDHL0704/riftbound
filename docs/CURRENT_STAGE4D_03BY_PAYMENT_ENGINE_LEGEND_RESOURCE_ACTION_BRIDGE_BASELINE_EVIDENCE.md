# Stage 4D-03BY PaymentEngine Legend Resource Action Bridge Baseline Evidence

Audit date: 2026-05-16
Conclusion: **BASELINE / NO WORKER DISPATCHED / PROJECT NOT READY**

## 1. Baseline Scope

This baseline records the current repository state before any future 4D-03BY-B legend resource-action bridge / verifier dispatch.

This batch is docs-only. It does not modify runtime, tests, frontend, browser scripts, card matrix JSON, `fullOfficial`, READY status or `riftbound-dotnet.sln`.

## 2. Repository Facts

- Branch: `main`.
- Latest commit before this handoff batch: `7d0bc6a1 docs: route non-legend resource skills`.
- Expected untracked file: `riftbound-dotnet.sln`.
- 4D-03BX non-legend deferred resource skill runtime handoff / baseline has been accepted.
- Active goal remains **NOT READY**.

## 3. Current Evidence Facts

- `PaymentEngineCoverageAuditTests.cs` contains `DeferredResourceSkillFamilyManifest`, which exactly splits the 13 deferred official resource-skill candidates into 9 legend bridge candidates and 4 non-legend runtime / verifier candidates.
- The 9 legend bridge candidates are `UNL-197/219`, `SFD·189/221`, `SFD·244/221`, `OGN·247/298`, `OGN·253/298`, `OGN·299/298`, `OGN·299*/298`, `OGN·302/298` and `OGN·302*/298`.
- `MatchSession.cs` contains Darius / Diana / KaiSa / Ornn `LEGEND_ACT` resource-action definitions for generated mana / power branches and their timing restrictions.
- `ConformanceFixtureRunnerTests.cs` contains representative `LEGEND_ACT` assertions for Darius, Diana, KaiSa and Ornn.
- Those representatives are evidence inputs, not closure by proxy. A future bridge verifier must explicitly connect them to the official `RESOURCE_SKILLS` closure contract or preserve them as open deferred candidates.
- The 4 non-legend 4D-03BX candidates remain outside this bridge slice.
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

- Diana spell-duel-only generated mana bridge semantics;
- Ornn equipment-only generated power bridge semantics and reprint parity;
- KaiSa spell-only generated power bridge semantics and premium / alternate parity;
- Darius Inspire-gated generated mana bridge semantics and premium / alternate parity;
- explicit generated resource lifetime / payment-only restriction / timing rejection / stale-source rollback checks under a resource-skill closure contract;
- the 4 non-legend runtime candidates outside this slice;
- official card-matrix alignment for 1009 snapshot entries / 811 functional units.

Project status remains **NOT READY** and the active goal must remain open.

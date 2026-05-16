# Stage 4D-03CD PaymentEngine Blue Sentinel Resource Skill Handoff

Audit date: 2026-05-16
Conclusion: **A-SIDE HANDOFF / FUTURE B BLUE SENTINEL LANE BOUNDARY / PROJECT NOT READY**

## 1. Scope

4D-03CD follows the accepted 4D-03CA non-legend resource skill runtime lane gate, the 4D-03CB Jhin lane handoff and the 4D-03CC Honeyfruit lane handoff. It reserves only `LANE_BLUE_SENTINEL_DELAYED_NEXT_MAIN_RESOURCE_SKILL` for a future B-side implementation / verifier slice.

This handoff does not implement runtime behavior, tests, frontend behavior, browser scripts, formal 18-step scripts, card matrix changes, `fullOfficial`, READY status or `riftbound-dotnet.sln`.

## 2. Current Inputs

The fixed official catalog entry for `UNL-087/219` Blue Sentinel / `苍蓝雕纹魔像` contains these relevant facts:

- card category: unit;
- color / region: blue / Targon;
- cost / stats: 4 mana, 4 power, 1 return energy;
- official keyword text: `坚守2`;
- official held-battlefield text: when it is held at a battlefield, that held effect triggers one extra time;
- official resource-skill text: when it holds a battlefield, at the start of the next main phase its controller gains `A`, and generated resource skills cannot be targeted as responses by other spells.

Current evidence is intentionally insufficient for closure:

- `CardBehaviorRegistry.cs` marks `UNL-087/219` and `UNL-087a/219` as units that play to base with 4 power and `坚守2`.
- `ConformanceFixtureRunnerTests.cs` covers zero-target play / target-rejection shape for `UNL-087/219` and `UNL-087a/219`.
- `p2-preflight-play-azure-glyph-golem-steadfast2-keyword-unit.fixture.json` proves ordinary main-phase play to base and keyword tag capture, but explicitly defers defense power modifier, held extra trigger and next-main resource paths.
- `PaymentEngineCoverageAuditTests.cs` keeps `UNL-087/219` as a deferred non-legend resource-skill lane, not an implemented `P4ActivatedAbilityCatalog.IsResourceSkill=true` source.

## 3. Future B Dispatch Boundary

Future 4D-03CD-B work requires a fresh explicit A dispatch and a fresh write lock. Candidate B-side scope:

- prove server-owned held-battlefield trigger capture when Blue Sentinel holds a battlefield;
- record delayed next-main resource generation without leaking stale battlefield or source state;
- ensure prompt eligibility is filtered by held-battlefield capture, delayed next-main timing and a pending payment context that can legally consume generated power;
- require command-side revalidation of delayed trigger identity, source identity, battlefield state, generated power request and payment-only use before mutation;
- connect audit evidence to Blue Sentinel through trigger capture, generated power availability, generated-resource spend and cleanup events;
- prove generated power lifetime, duplicate-spend prevention, cleanup after the payment window and the reminder-text restriction that generated resource skills cannot be reaction targets;
- prove no-mutation rollback for wrong main phase, missing held trigger, stale source, stale battlefield, unsupported generated amount and handwritten command attempts.

Likely future B write scope:

- focused tests: a new Blue Sentinel resource-skill test file or a narrow addition to `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`;
- verifier if the slice remains test-only: `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`;
- runtime only if A explicitly opens it after the handoff: `src/Riftbound.Engine/P4ActivatedAbilityCatalog.cs`, `src/Riftbound.Engine/CoreRuleEngine.cs`, `src/Riftbound.Engine/MatchSession.cs` and minimal contract types if required;
- docs: 4D-03CD-B audit / evidence docs plus A-side checkpoint / completion / closure / dispatch / server audit / checklist updates.

This 4D-03CD handoff does not dispatch B and does not open those files now.

## 4. No-Go Scope

This batch and any future worker inheriting it must not touch without a fresh A write lock:

- Jhin, Honeyfruit or Lux non-legend lanes;
- the 9 `LEGEND_ACT` resource-action bridge candidates;
- frontend runtime, stores, views, smoke scripts or formal 18-step scripts;
- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`;
- broad PaymentEngine rewrites outside the Blue Sentinel held-battlefield delayed next-main resource-skill lane;
- broad battle lifecycle / battlefield-control rewrites beyond the future narrow trigger capture if explicitly dispatched;
- LayerEngine or unrelated keyword implementation;
- `fullOfficial` / READY status;
- `riftbound-dotnet.sln`.

## 5. Required Validation

Baseline validation for this handoff:

```sh
source scripts/dev-env.sh && dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter FullyQualifiedName~PaymentEngineCoverageAuditTests
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PaymentEngineCoverageAuditTests|FullyQualifiedName~PaymentEngineUnificationTests|FullyQualifiedName~ResourceSkill|FullyQualifiedName~SpellshieldTax|FullyQualifiedName~HasteReady|FullyQualifiedName~TriggerPayment|FullyQualifiedName~LegendAct|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub"
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
git diff --check
```

Future acceptance for a dispatched B-side Blue Sentinel lane must rerun focused Blue Sentinel tests or verifier coverage, adjacent PaymentEngine / resource skill / battlefield-held / prompt / hub regression, backend full and `git diff --check`.

## 6. Remaining Risk

4D-03CD does not close P0-005, P1, full-card matrix or READY. It only reserves a single Blue Sentinel lane so future B-side work can implement or verify the held-battlefield delayed next-main generated-resource behavior without borrowing unit play / `坚守2` preflight evidence as `RESOURCE_SKILLS` closure.

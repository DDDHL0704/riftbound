# Stage 4D-03CB PaymentEngine Jhin Movement Resource Skill Handoff

Audit date: 2026-05-16
Conclusion: **A-SIDE HANDOFF / FUTURE B JHIN LANE BOUNDARY / PROJECT NOT READY**

## 1. Scope

4D-03CB follows the accepted 4D-03CA non-legend resource skill runtime lane gate. It reserves only `LANE_JHIN_MOVE_TRIGGERED_RESOURCE_SKILL` for a future B-side implementation / verifier slice.

This handoff does not implement runtime behavior, tests, frontend behavior, browser scripts, formal 18-step scripts, card matrix changes, `fullOfficial`, READY status or `riftbound-dotnet.sln`.

## 2. Current Inputs

The fixed official catalog entry for `UNL-022/219` Jhin contains these relevant facts:

- card category: hero unit;
- keywords already represented by current play/preflight evidence: Spellshield and Roam;
- official resource-skill text: when Jhin moves, gain 1 mana and 1 power, and generated resource skills cannot be targeted as responses by other spells.

Current evidence is intentionally insufficient for closure:

- `CardBehaviorRegistry.cs` and existing fixtures cover Jhin entering as a unit with Spellshield / Roam.
- `ConformanceFixtureRunnerTests.cs` has Roam / preflight evidence for `UNL-022/219` and `UNL-022a/219`.
- `PaymentEngineCoverageAuditTests.cs` keeps `UNL-022/219` as a deferred non-legend resource-skill lane, not an implemented `P4ActivatedAbilityCatalog.IsResourceSkill=true` source.

## 3. Future B Dispatch Boundary

Future 4D-03CB-B work requires a fresh explicit A dispatch and a fresh write lock. Candidate B-side scope:

- prove a server-owned prompt or equivalent runtime verifier appears only after a real movement trigger is captured for Jhin;
- ensure the prompt is filtered by source state, movement context and a pending payment context that can legally consume generated mana plus generated power;
- require command-side revalidation of source identity, movement trigger identity, generated mana / power request and payment-only use before mutation;
- connect audit evidence to Jhin through `ABILITY_ACTIVATED`, `COST_PAID` or generated-resource spend / clear events;
- prove generated mana / power lifetime, duplicate-spend prevention and cleanup after the payment window;
- prove no-mutation rollback for wrong window, missing trigger, stale source, stale movement context, wrong resource use and handwritten command attempts.

Likely future B write scope:

- focused tests: a new Jhin resource-skill test file or a narrow addition to `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`;
- verifier if the slice remains test-only: `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`;
- runtime only if A explicitly opens it after the handoff: `src/Riftbound.Engine/P4ActivatedAbilityCatalog.cs`, `src/Riftbound.Engine/CoreRuleEngine.cs`, `src/Riftbound.Engine/MatchSession.cs` and minimal contract types if required;
- docs: 4D-03CB-B audit / evidence docs plus A-side checkpoint / completion / closure / dispatch / server audit / checklist updates.

This 4D-03CB handoff does not dispatch B and does not open those files now.

## 4. No-Go Scope

This batch and any future worker inheriting it must not touch without a fresh A write lock:

- Honeyfruit, Blue Sentinel or Lux non-legend lanes;
- the 9 `LEGEND_ACT` resource-action bridge candidates;
- frontend runtime, stores, views, smoke scripts or formal 18-step scripts;
- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`;
- broad PaymentEngine rewrites outside the Jhin movement-triggered resource-skill lane;
- battle lifecycle / cleanup queues;
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

Future acceptance for a dispatched B-side Jhin lane must rerun focused Jhin tests or verifier coverage, adjacent PaymentEngine / resource skill / movement / prompt / hub regression, backend full and `git diff --check`.

## 6. Remaining Risk

4D-03CB does not close P0-005, P1, full-card matrix or READY. It only reserves a single Jhin lane so future B-side work can implement or verify the movement-triggered generated-resource behavior without borrowing Roam / Spellshield / preflight evidence as `RESOURCE_SKILLS` closure.

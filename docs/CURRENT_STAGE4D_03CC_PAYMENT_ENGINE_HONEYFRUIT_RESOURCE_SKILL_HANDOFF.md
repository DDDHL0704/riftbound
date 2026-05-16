# Stage 4D-03CC PaymentEngine Honeyfruit Resource Skill Handoff

Audit date: 2026-05-16
Conclusion: **A-SIDE HANDOFF / FUTURE B HONEYFRUIT LANE BOUNDARY / PROJECT NOT READY**

## 1. Scope

4D-03CC follows the accepted 4D-03CA non-legend resource skill runtime lane gate and the 4D-03CB Jhin lane handoff. It reserves only `LANE_HONEYFRUIT_EQUIPMENT_REACTION_RESOURCE_SKILL` for a future B-side implementation / verifier slice.

This handoff does not implement runtime behavior, tests, frontend behavior, browser scripts, formal 18-step scripts, card matrix changes, `fullOfficial`, READY status or `riftbound-dotnet.sln`.

## 2. Current Inputs

The fixed official catalog entry for `UNL-049/219` Honeyfruit / `蜜糖果实` contains these relevant facts:

- card category: equipment;
- cost: 2 mana;
- enters play exhausted;
- official resource-skill text: reaction-speed tap to gain `A`, and generated resource skills cannot be targeted as responses by other spells;
- level-six upgraded branch: reaction-speed tap to gain `1` and `A`, usable only with at least 6 experience.

Current evidence is intentionally insufficient for closure:

- `CardBehaviorRegistry.cs` marks Honeyfruit as equipment that plays to base exhausted.
- `ConformanceFixtureRunnerTests.cs` covers Honeyfruit play as exhausted equipment and rejects explicit targets for the zero-target play path.
- `PaymentEngineCoverageAuditTests.cs` keeps `UNL-049/219` as a deferred non-legend resource-skill lane, not an implemented `P4ActivatedAbilityCatalog.IsResourceSkill=true` source.
- Existing play / rejection fixtures do not expose a P4 resource-skill prompt / command / audit path for tap reaction timing, level-six branch selection or generated mana / power lifetime.

## 3. Future B Dispatch Boundary

Future 4D-03CC-B work requires a fresh explicit A dispatch and a fresh write lock. Candidate B-side scope:

- prove a server-owned prompt or equivalent runtime verifier appears only in a legal equipment reaction window with a ready Honeyfruit source;
- ensure prompt eligibility is filtered by tap reaction timing, source readiness, equipment-reaction source identity, pending payment context and level-six upgraded branch availability;
- require command-side revalidation of source identity, tap readiness, reaction timing, generated mana / power amount, level-six branch legality and payment-only use before mutation;
- connect audit evidence to Honeyfruit through `ABILITY_ACTIVATED`, generated mana / power availability, generated-resource spend and cleanup events;
- prove generated resource lifetime, duplicate-spend prevention, cleanup after the payment window and the reminder-text restriction that generated resource skills cannot be reaction targets;
- prove no-mutation rollback for wrong timing, exhausted source, stale source, non-Honeyfruit source, illegal upgraded branch, unsupported generated amount and handwritten command attempts.

Likely future B write scope:

- focused tests: a new Honeyfruit resource-skill test file or a narrow addition to `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`;
- verifier if the slice remains test-only: `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`;
- runtime only if A explicitly opens it after the handoff: `src/Riftbound.Engine/P4ActivatedAbilityCatalog.cs`, `src/Riftbound.Engine/CoreRuleEngine.cs`, `src/Riftbound.Engine/MatchSession.cs` and minimal contract types if required;
- docs: 4D-03CC-B audit / evidence docs plus A-side checkpoint / completion / closure / dispatch / server audit / checklist updates.

This 4D-03CC handoff does not dispatch B and does not open those files now.

## 4. No-Go Scope

This batch and any future worker inheriting it must not touch without a fresh A write lock:

- Jhin, Blue Sentinel or Lux non-legend lanes;
- the 9 `LEGEND_ACT` resource-action bridge candidates;
- frontend runtime, stores, views, smoke scripts or formal 18-step scripts;
- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`;
- broad PaymentEngine rewrites outside the Honeyfruit equipment-reaction resource-skill lane;
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

Future acceptance for a dispatched B-side Honeyfruit lane must rerun focused Honeyfruit tests or verifier coverage, adjacent PaymentEngine / resource skill / prompt / hub regression, backend full and `git diff --check`.

## 6. Remaining Risk

4D-03CC does not close P0-005, P1, full-card matrix or READY. It only reserves a single Honeyfruit lane so future B-side work can implement or verify the equipment-reaction generated-resource behavior without borrowing exhausted-equipment play / target-rejection fixture evidence as `RESOURCE_SKILLS` closure.

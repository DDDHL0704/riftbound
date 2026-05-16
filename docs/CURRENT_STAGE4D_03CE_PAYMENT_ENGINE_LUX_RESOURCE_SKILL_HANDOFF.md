# Stage 4D-03CE PaymentEngine Lux Resource Skill Handoff

Audit date: 2026-05-16
Conclusion: **A-SIDE HANDOFF / FUTURE B LUX LANE BOUNDARY / PROJECT NOT READY**

## 1. Scope

4D-03CE follows the accepted 4D-03CA non-legend resource skill runtime lane gate plus the separate 4D-03CB Jhin, 4D-03CC Honeyfruit and 4D-03CD Blue Sentinel handoffs. It reserves only `LANE_LUX_SPELL_ONLY_TAP_REACTION_RESOURCE_SKILL` for a future B-side implementation / verifier slice.

This handoff does not implement runtime behavior, tests, frontend behavior, browser scripts, formal 18-step scripts, card matrix changes, `fullOfficial`, READY status or `riftbound-dotnet.sln`.

## 2. Current Inputs

The fixed official catalog entry for `OGS·014/024` Lux / `拉克丝` contains these relevant facts:

- card category: hero unit;
- color / region: yellow / Demacia;
- cost / stats: 4 mana, 2 power;
- official resource-skill text: `横置` reaction obtains 2 generated resources, but those resources may only be used to play spells;
- official reminder text: generated resource skills cannot be targeted as responses by other spells.

Current evidence is intentionally insufficient for closure:

- `CardBehaviorRegistry.cs` marks `OGS·014/024` as a unit that plays to base with 2 power.
- `ConformanceFixtureRunnerTests.cs` covers zero-target play / target-rejection shape for `OGS·014/024`.
- `p2-preflight-play-ogs-014-lux-tap-resource-static.fixture.json` proves ordinary main-phase play to base, but explicitly defers tap, reaction timing, spell-only resource usage and reaction-target restriction paths.
- `PaymentEngineCoverageAuditTests.cs` keeps `OGS·014/024` as a deferred non-legend resource-skill lane, not an implemented `P4ActivatedAbilityCatalog.IsResourceSkill=true` source.

## 3. Future B Dispatch Boundary

Future 4D-03CE-B work requires a fresh explicit A dispatch and a fresh write lock. Candidate B-side scope:

- prove server-owned tap reaction timing for Lux's spell-only resource skill;
- ensure prompt eligibility is filtered by reaction timing, ready source state and a pending spell payment that can legally consume generated mana;
- require command-side revalidation of tap source, pending spell identity, generated mana request and spell-only payment restriction before mutation;
- connect audit evidence to Lux through `ABILITY_ACTIVATED`, generated mana availability, generated-resource spend and cleanup events;
- prove generated mana lifetime, duplicate-spend prevention, cleanup after the payment window and the spell-only restriction;
- reject non-spell payment attempts, wrong timing, exhausted source, stale source, unsupported generated amount and handwritten command attempts with no mutation;
- preserve the reminder-text boundary that generated resource skills cannot be response targets.

Likely future B write scope:

- focused tests: a new Lux non-legend resource-skill test file or a narrow addition to `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`;
- verifier if the slice remains test-only: `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`;
- runtime only if A explicitly opens it after the handoff: `src/Riftbound.Engine/P4ActivatedAbilityCatalog.cs`, `src/Riftbound.Engine/CoreRuleEngine.cs`, `src/Riftbound.Engine/MatchSession.cs` and minimal contract types if required;
- docs: 4D-03CE-B audit / evidence docs plus A-side checkpoint / completion / closure / dispatch / server audit / checklist updates.

This 4D-03CE handoff does not dispatch B and does not open those files now.

## 4. No-Go Scope

This batch and any future worker inheriting it must not touch without a fresh A write lock:

- Jhin, Honeyfruit or Blue Sentinel non-legend lanes;
- KaiSa spell-only `LEGEND_ACT` bridge rows or any of the other 9 `LEGEND_ACT` resource-action bridge candidates;
- frontend runtime, stores, views, smoke scripts or formal 18-step scripts;
- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`;
- broad PaymentEngine rewrites outside the Lux spell-only tap-reaction resource-skill lane;
- broad spell-duel / stack / pending-payment rewrites beyond the future narrow spell-only payment context if explicitly dispatched;
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

Future acceptance for a dispatched B-side Lux lane must rerun focused Lux tests or verifier coverage, adjacent PaymentEngine / resource skill / spell-only pending-payment / prompt / hub regression, backend full and `git diff --check`.

## 6. Remaining Risk

4D-03CE does not close P0-005, P1, full-card matrix or READY. It only reserves a single Lux lane so future B-side work can implement or verify the spell-only tap-reaction generated-resource behavior without borrowing hero-unit play / preflight evidence or KaiSa `LEGEND_ACT` spell-only evidence as `RESOURCE_SKILLS` closure.

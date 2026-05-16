# Stage 4D-03BX PaymentEngine Non-Legend Resource Skill Runtime Handoff

Audit date: 2026-05-16
Conclusion: **A-SIDE HANDOFF / FUTURE B RUNTIME-VERIFIER BOUNDARY / PROJECT NOT READY**

## 1. Scope

4D-03BX follows the accepted 4D-03BW deferred resource skill family verifier.

This handoff does not implement runtime behavior, tests, frontend behavior, browser scripts or card matrix changes. It narrows the next possible B-side implementation slice to the 4 non-legend deferred official resource-skill candidates that 4D-03BW separated from the 9 existing `LEGEND_ACT` bridge candidates.

The purpose is to keep the next runtime / verifier work small enough to accept safely. The 9 legend resource-action bridge candidates still require their own explicit closure bridge and must not be merged into this B slice.

## 2. Current Inputs

4D-03BW fixed the deferred family split:

- 13 deferred official resource-skill candidates remain open after 4D-03BU / 4D-03BV.
- 9 are existing `LEGEND_ACT` resource-action bridge candidates.
- 4 are non-legend runtime / verifier candidates.
- Existing preflight, play or representative evidence does not close `RESOURCE_SKILLS` by proxy.

The 4 non-legend candidates reserved by this handoff are:

| Card no | Family | Current evidence shape | Future B question |
|---|---|---|---|
| `UNL-022/219` | Jhin movement-triggered mana plus power generated resource skill | Jhin play / preflight / Roam evidence exists, but no `P4ActivatedAbilityCatalog.IsResourceSkill=true` prompt / command / audit implementation covers the official move-triggered generated-resource text. | Prove movement-triggered generation, generated mana plus generated power lifetime, payment-only restrictions and no-mutation rollback. |
| `UNL-049/219` | Honeyfruit equipment reaction resource skill plus level-six upgraded mana / power branch | Honeyfruit play and rejection fixture evidence exists, but no P4 resource-skill prompt / command / audit implementation covers the official tap reaction and upgraded resource text. | Prove tap reaction timing, level-six branch, generated mana / power lifetime, payment-only restrictions and no-mutation rollback. |
| `UNL-087/219` | Blue Sentinel held-battlefield delayed next-main generated power branch | Blue Sentinel play / preflight evidence exists, but no P4 resource-skill prompt / command / audit implementation covers the official delayed next-main generated-resource text. | Prove held-battlefield trigger capture, delayed next-main generation, generated power lifetime and no-mutation rollback. |
| `OGS·014/024` | Lux spell-only tap reaction resource skill | Lux play / preflight evidence exists, but no P4 resource-skill prompt / command / audit implementation covers the official spell-only tap reaction resource text. | Prove tap reaction timing, spell-only generated mana consumption, invalid non-spell use and no-mutation rollback. |

## 3. Future B Dispatch Boundary

Future 4D-03BX-B work requires a fresh explicit A dispatch and a fresh write lock. Candidate B-side scope:

- implement or prove the 4 non-legend official resource-skill candidates as server-authoritative resource skill behavior;
- expose legal server prompts only when timing, source state, trigger condition and pending payment context allow the official resource-skill action;
- reject handwritten illegal commands with no mutation;
- emit or preserve authoritative audit evidence for `ABILITY_ACTIVATED`, `COST_PAID` and temporary generated-resource spend / clear events where applicable;
- prove generated mana / power lifetime, payment-only restrictions, wrong-window, wrong-resource, wrong-spell and stale-source branches;
- keep card-matrix `fullOfficial`, READY and frontend contract changes outside the B slice unless A opens a separate E or C window.

Likely future B write scope:

- primary runtime / catalog: `src/Riftbound.Engine/P4ActivatedAbilityCatalog.cs`, `src/Riftbound.Engine/MatchSession.cs`, `src/Riftbound.Engine/CoreRuleEngine.cs`;
- optional, only if the verifier exposes a concrete need: `src/Riftbound.Engine/CardBehaviorRegistry.cs`;
- protocol / mapper only if an existing command shape cannot represent the official behavior: `src/Riftbound.Contracts/Protocol.cs`, `src/Riftbound.Engine/GameCommandJsonMapper.cs`;
- focused tests: existing or new resource-skill tests under `tests/Riftbound.ConformanceTests`, preferably reusing existing `ACTIVATE_ABILITY`, optional-cost and temporary payment resource helpers;
- docs: 4D-03BX-B audit / evidence docs plus A-side checkpoint / completion / closure / dispatch / server audit / checklist updates.

This 4D-03BX handoff does not dispatch B and does not open those files now.

## 4. No-Go Scope

This batch and any future worker inheriting this handoff must not touch without a fresh A write lock:

- the 9 legend `LEGEND_ACT` resource-action bridge candidates;
- frontend runtime, stores, views or browser scripts;
- formal 18-step scripts;
- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`;
- broad PaymentEngine rewrites outside the 4 non-legend candidates;
- battle lifecycle / cleanup queues;
- LayerEngine or unrelated keyword implementation;
- `fullOfficial` / READY status;
- `riftbound-dotnet.sln`.

## 5. Required Validation

Baseline validation for this handoff:

```sh
source scripts/dev-env.sh
dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter FullyQualifiedName~PaymentEngineCoverageAuditTests
dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PaymentEngineCoverageAuditTests|FullyQualifiedName~PaymentEngineUnificationTests|FullyQualifiedName~ResourceSkill|FullyQualifiedName~SpellshieldTax|FullyQualifiedName~HasteReady|FullyQualifiedName~TriggerPayment|FullyQualifiedName~LegendAct|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub"
dotnet test Riftbound.slnx --no-restore
git diff --check
```

Future acceptance for a dispatched B-side implementation must rerun a focused command that proves the 4 cards, an adjacent PaymentEngine / resource skill / prompt / hub command, backend full and `git diff --check`.

## 6. Remaining Risk

4D-03BX does not close P0-005, P1, full-card matrix or READY. It only turns the 4 non-legend deferred official resource-skill candidates into a separate future runtime / verifier boundary so later B-side work can address Jhin, Honeyfruit, Blue Sentinel and Lux without mixing them with the legend bridge family.

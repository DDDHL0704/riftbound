# Stage 4D-03BV PaymentEngine Deferred Resource Skill Family Handoff

Audit date: 2026-05-16
Conclusion: **A-SIDE HANDOFF / FUTURE B DISPATCH BOUNDARY / PROJECT NOT READY**

## 1. Scope

4D-03BV follows the 4D-03BU resource skill official breadth verifier.

This handoff does not implement runtime behavior, tests, frontend behavior, browser scripts or card matrix changes. It converts the 13 deferred official resource-skill candidates from the 4D-03BU executable manifest into a narrower future B-side family plan.

The handoff is intentionally split-aware. It prevents existing `LEGEND_ACT` resource-action tests, 4D-03BU 32 / 19 / 13 reconciliation, backend full, Chrome smoke or formal 18-step evidence from being treated as full official PaymentEngine resource-skill closure.

## 2. Current Inputs

4D-03BU fixed the official resource-skill candidate snapshot at 32 entries:

- 19 current source card nos are represented by `P4ActivatedAbilityCatalog.IsResourceSkill=true`.
- 13 current source card nos remain deferred official candidates.
- The deferred candidates are still part of `RESOURCE_SKILL_A_C_FAMILY` and `RESOURCE_SKILLS` open breadth.

The 13 deferred candidates split into two implementation shapes:

| Family | Official candidates | Current evidence shape | Future B question |
|---|---|---|---|
| Existing legend resource actions outside current PaymentEngine resource-skill closure | `UNL-197/219`; `SFD·189/221`; `SFD·244/221`; `OGN·247/298`; `OGN·253/298`; `OGN·299/298`; `OGN·299*/298`; `OGN·302/298`; `OGN·302*/298` | `MatchSession.cs` / `CoreRuleEngine.cs` contain Diana / Ornn / KaiSa / Darius `LEGEND_ACT` resource definitions and `ConformanceFixtureRunnerTests.cs` has focused representative assertions. They are not current `IsResourceSkill=true` catalog entries and are outside current PaymentEngine payment-surface aggregate rows. | Decide whether official closure needs a PaymentEngine resource-skill verifier bridge, a `LEGEND_ACT` resource-skill family verifier, or a narrow runtime/catalog unification fix. |
| Non-legend unit / equipment / delayed resource skills | `UNL-022/219`; `UNL-049/219`; `UNL-087/219`; `OGS·014/024` | Current code has play / preflight / card-behavior evidence for these cards, but 4D-03BU proves no current `IsResourceSkill=true` resource-skill implementation covers their official generated-resource text. | Implement or verify movement-triggered, equipment reaction, held-battlefield delayed-next-main, and spell-only tap reaction resource skill behavior with prompt / command / audit / no-mutation coverage. |

## 3. Future B Dispatch Boundary

Future 4D-03BV-B work requires a fresh explicit A dispatch and a fresh write lock. Candidate first slice:

- build an executable deferred-family manifest for the 13 source card nos;
- bind every row to fixed official catalog text, current code evidence, current matrix functional unit, action domain, timing, generated resource amount/type, payment-only restriction and no-mutation expectation;
- prove that existing `LEGEND_ACT` representatives cannot close `RESOURCE_SKILLS` by proxy unless the verifier explicitly bridges them into the official resource-skill closure contract;
- split future implementation work into at least two follow-up branches: legend resource-action bridge and non-legend resource-skill runtime / verifier breadth.

Likely future B write scope:

- primary verifier: `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`;
- focused tests only if needed: `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs` or a focused resource-skill test file;
- runtime/catalog only if the verifier exposes a concrete mismatch: `src/Riftbound.Engine/P4ActivatedAbilityCatalog.cs`, `src/Riftbound.Engine/MatchSession.cs`, `src/Riftbound.Engine/CoreRuleEngine.cs`, `src/Riftbound.Engine/CardBehaviorRegistry.cs`, or related minimal helpers;
- docs: 4D-03BV audit / evidence docs and the A-side checkpoint / completion / closure / dispatch docs.

This 4D-03BV handoff does not dispatch B and does not open those files now.

## 4. No-Go Scope

This batch and any future worker inheriting this handoff must not touch without a fresh A write lock:

- frontend runtime, stores, views or browser scripts;
- formal 18-step scripts;
- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`;
- broad PaymentEngine rewrites;
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

Future acceptance for a dispatched B-side verifier or implementation must rerun the focused command, an adjacent command chosen for the actual files touched, backend full test and `git diff --check`.

## 6. Remaining Risk

4D-03BV does not close P0-005, P1, full-card matrix or READY. It only turns the 13 deferred official resource-skill candidates into a family-level dispatch boundary so later B-side work can address real official breadth instead of looping on the already accepted 4D-03BU reconciliation.

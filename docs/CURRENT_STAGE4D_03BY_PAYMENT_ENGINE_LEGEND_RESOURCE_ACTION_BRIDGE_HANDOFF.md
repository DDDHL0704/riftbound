# Stage 4D-03BY PaymentEngine Legend Resource Action Bridge Handoff

Audit date: 2026-05-16
Conclusion: **A-SIDE HANDOFF / FUTURE B BRIDGE-VERIFIER BOUNDARY / PROJECT NOT READY**

## 1. Scope

4D-03BY follows the accepted 4D-03BW deferred resource skill family verifier and the 4D-03BX non-legend runtime handoff.

This handoff does not implement runtime behavior, tests, frontend behavior, browser scripts or card matrix changes. It reserves the counterpart future B-side bridge / verifier slice for the 9 legend deferred official resource-skill candidates that are currently represented as `LEGEND_ACT` resource actions.

The purpose is to prevent two opposite mistakes:

- treating existing `LEGEND_ACT` representative evidence as automatic `RESOURCE_SKILLS` closure;
- mixing the legend bridge problem with the 4D-03BX non-legend runtime problem.

## 2. Current Inputs

4D-03BW fixed the legend bridge set at 9 deferred official candidates:

| Card no | Family | Current evidence shape | Future B question |
|---|---|---|---|
| `UNL-197/219` | Diana spell-duel-only legend resource action | Existing `LEGEND_ACT` tests prove Diana gains mana during spell-duel focus. | Bridge spell-duel timing, generated mana restriction and resource-skill closure semantics explicitly. |
| `SFD·189/221` | Ornn equipment-only legend resource action | Existing `LEGEND_ACT` tests prove Ornn gains power for pending equipment. | Bridge equipment-only restriction, generated power lifetime and resource-skill closure semantics explicitly. |
| `SFD·244/221` | Ornn reprint equipment-only legend resource action | Existing `LEGEND_ACT` definitions include the Ornn reprint. | Bridge reprint parity, equipment-only restriction and generated power lifetime explicitly. |
| `OGN·247/298` | KaiSa spell-only legend resource action | Existing `LEGEND_ACT` tests prove KaiSa gains power for pending spell. | Bridge pending-spell priority timing, spell-only generated power restriction and resource-skill closure semantics explicitly. |
| `OGN·253/298` | Darius Inspire-gated legend resource action | Existing `LEGEND_ACT` tests prove Darius gains mana after another card this turn. | Bridge Inspire gating, generated mana lifetime and resource-skill closure semantics explicitly. |
| `OGN·299/298` | KaiSa premium spell-only legend resource action | Existing `LEGEND_ACT` definitions include the KaiSa premium source. | Bridge premium source parity and spell-only generated power restriction explicitly. |
| `OGN·299*/298` | KaiSa premium alternate spell-only legend resource action | Existing `LEGEND_ACT` definitions include the KaiSa premium alternate source. | Bridge premium alternate source parity and spell-only generated power restriction explicitly. |
| `OGN·302/298` | Darius premium Inspire-gated legend resource action | Existing `LEGEND_ACT` definitions include the Darius premium source. | Bridge premium source parity, Inspire gating and generated mana lifetime explicitly. |
| `OGN·302*/298` | Darius premium alternate Inspire-gated legend resource action | Existing `LEGEND_ACT` definitions include the Darius premium alternate source. | Bridge premium alternate source parity, Inspire gating and generated mana lifetime explicitly. |

Current code anchors:

- `src/Riftbound.Engine/MatchSession.cs` exposes Darius / Diana / KaiSa / Ornn `LEGEND_ACT` definitions, prompt metadata and source requirements.
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs` contains representative `LEGEND_ACT` command / rejection assertions for Darius, Diana, KaiSa and Ornn.
- `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs` keeps these 9 candidates in `DeferredResourceSkillFamilyManifest` as `legend-bridge` rows, not implemented resource-skill rows.

## 3. Future B Dispatch Boundary

Future 4D-03BY-B work requires a fresh explicit A dispatch and a fresh write lock. Candidate B-side scope:

- build an executable legend bridge verifier for the 9 official candidates;
- bind every row to its current `LEGEND_ACT` ability id, source card no group, timing restriction, generated resource type, representative test anchor and official resource-skill closure gap;
- prove that the bridge is explicit: existing Darius / Diana / KaiSa / Ornn tests cannot count as `RESOURCE_SKILLS` closure unless the verifier names the closure contract and its remaining breadth;
- decide whether the accepted bridge is test-only documentation / verifier coverage, a `LEGEND_ACT` resource-skill family verifier, or a narrow runtime / catalog unification fix;
- keep the 4D-03BX non-legend candidates outside this bridge slice.

Likely future B write scope:

- primary verifier: `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`;
- focused legend tests only if a concrete bridge gap is exposed: `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs` or a new focused legend resource-action test file;
- runtime only if the verifier exposes an actual mismatch: `src/Riftbound.Engine/MatchSession.cs`, `src/Riftbound.Engine/CoreRuleEngine.cs`, `src/Riftbound.Engine/P6LegendAbilityCatalog.cs`, and only with A approval;
- docs: 4D-03BY-B audit / evidence docs plus A-side checkpoint / completion / closure / dispatch / server audit / checklist updates.

This 4D-03BY handoff does not dispatch B and does not open those files now.

## 4. No-Go Scope

This batch and any future worker inheriting this handoff must not touch without a fresh A write lock:

- the 4 non-legend 4D-03BX runtime candidates: Jhin, Honeyfruit, Blue Sentinel and Lux;
- frontend runtime, stores, views or browser scripts;
- formal 18-step scripts;
- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`;
- broad PaymentEngine rewrites outside the legend bridge;
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

Future acceptance for a dispatched B-side bridge / verifier must rerun the focused verifier command, an adjacent PaymentEngine / resource skill / legend / prompt / hub command, backend full and `git diff --check`.

## 6. Remaining Risk

4D-03BY does not close P0-005, P1, full-card matrix or READY. It only turns the 9 legend deferred official resource-skill candidates into a separate future bridge / verifier boundary so later B-side work can address Diana, Ornn, KaiSa and Darius resource-action closure without weakening the `RESOURCE_SKILLS` official closure contract.

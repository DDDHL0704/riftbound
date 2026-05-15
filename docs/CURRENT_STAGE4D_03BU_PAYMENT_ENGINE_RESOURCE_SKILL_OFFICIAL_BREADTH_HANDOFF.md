# Stage 4D-03BU PaymentEngine Resource Skill Official Breadth Handoff

Audit date: 2026-05-16
Conclusion: **A-SIDE HANDOFF / FUTURE B DISPATCH BOUNDARY / PROJECT NOT READY**

## 1. Scope

4D-03BU follows the 4D-03BT remaining official closure gate verifier and the 4D-03BQ-B resource skill all-window matrix verifier.

This handoff does not implement runtime behavior, tests, frontend behavior, browser scripts or card matrix changes. It reserves the next concrete B-side PaymentEngine official breadth slice for the complete `[A]` / `[C]` resource skill family, moving beyond the current catalog-bound representatives and the current all-window representative matrix.

The handoff is intentionally narrow. It prevents 4D-03BQ-B 36-row resource skill matrix, 4D-03BT closure gate, backend full, Chrome smoke or formal 18 E2E evidence from being treated as complete official `[A]` / `[C]` resource skill coverage.

## 2. Current Inputs

Current resource-skill evidence:

- `ResourceSkillCoverageManifest`: 6 resource skill family entries and 19 current `IsResourceSkill=true` ability ids.
- Covered families: Malzahar target-as-cost payment-only resource skill; Dragon Soul Sage reaction mana resource skill; SFD Sigil typed payment-only resource skill family; OGN Sigil typed payment-only resource skill family; resource conversion equipment skill family; Gold token payment-only resource skill family.
- `ResourceSkillAllWindowMatrixManifest`: 36 executable rows, equal to 6 resource skill families x 6 current PaymentEngine payment surfaces.
- `ResidualBlockerManifest` still classifies `RESOURCE_SKILL_A_C_FAMILY` as `catalog-bound-representative`.
- `OfficialPaymentEngineMatrixResidualManifest` still classifies `RESOURCE_SKILLS` as `remaining-official-gap`.
- 4D-03BT matrix guard still proves 1009 snapshot entries / 811 functional units, `fullOfficial=true` count 0 and freeze ready=false.

The accepted 4D-03BQ-B matrix proves representative prompt / command / `ABILITY_ACTIVATED` / generated-resource / rollback contracts across current payment surfaces. It does not prove every official `[A]` / `[C]` resource skill, printed timing permission, generated resource type, payment-only consumption branch, cross-window lifetime, conversion ordering, Gold token bonus interaction or no-mutation failure branch.

## 3. Future B Dispatch Boundary

Future 4D-03BU-B work requires a fresh explicit A dispatch and a fresh write lock. Candidate B-side scope:

- reconcile the official resource-skill catalog against the current 19 `IsResourceSkill=true` ability ids;
- enumerate every official `[A]` / `[C]` resource skill family row expected by the fixed 2026-04-27 catalog;
- produce an executable verifier for current representative coverage versus official resource-skill breadth;
- where a verifier exposes a concrete mismatch, implement the smallest server-side fix needed to make prompt / command / audit / rollback behavior match the authoritative server rules;
- keep generated resource lifecycle, payment-only misuse, invalid timing, invalid target, wrong trait, duplicate source and stale generated-resource branches explicit.

Likely future B write scope:

- primary: `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`;
- if a concrete implementation mismatch is exposed: minimally `src/Riftbound.Engine/P4ActivatedAbilityCatalog.cs`, `src/Riftbound.Engine/CardBehaviorRegistry.cs` or related catalog/profile helpers, `src/Riftbound.Engine/CoreRuleEngine.cs`, `src/Riftbound.Engine/MatchSession.cs`, plus focused resource-skill conformance tests;
- docs: 4D-03BU audit / evidence docs and the A-side checkpoint / completion / closure / dispatch docs.

This 4D-03BU handoff does not dispatch B and does not open those files now.

## 4. No-Go Scope

This batch and any future worker inheriting this handoff must not touch without a fresh A write lock:

- runtime files under `src/**`;
- frontend runtime, stores, views or browser scripts;
- formal 18-step scripts;
- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`;
- broad PaymentEngine rewrites;
- battle lifecycle / cleanup queues;
- LayerEngine or keyword implementation;
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

Future acceptance for any dispatched B-side implementation or verifier must rerun the focused command, an adjacent command chosen for the actual files touched, backend full test and `git diff --check`.

## 6. Remaining Risk

4D-03BU does not close P0-005, P1, full-card matrix or READY. It only converts the post-4D-03BT routing state into a concrete resource-skill official breadth handoff so a later B-side batch can make progress without confusing representative resource-skill evidence with full official closure.

# 4D-03DC-B PaymentEngine Selected Resource Skill Runtime/Card-Row Parity Evidence

日期：2026-05-16
状态：**ACCEPTED B-SIDE FOCUSED VERIFIER / PROJECT NOT READY**

## Evidence Summary

`SelectedResourceSkillOfficialRuntimeCardRowParityManifest` covers six high-signal rows from the 4D-03DC dispatch contract:

- Malzahar `OGN·113/298`: `[A A]` target-as-cost generated payment-only power.
- Lux `OGS·014/024`: spell-only generated mana with payment-step consumption and cleanup.
- Dragon Soul Sage `UNL-093/219`: reaction-speed generated mana.
- Ancient Stele `SFD·117/221`: conversion resource skill producing generic temporary payment resource.
- Gold token `UNL·T05`: destroy-cost generated generic temporary resource.
- Ornn `SFD·189/221` / `SFD·244/221`: bridge-closed equipment-only generated power, bound through `LegendResourceBridgeResourceSkillClosureManifest`.

Each selected row binds:

- official `ResourceSkillOfficialBreadthManifest` candidate and classification;
- runtime/card-row evidence from `ResourceSkillOfficialRuntimeCardRowEvidenceManifest`;
- source-card parity from `ResourceSkillOfficialSourceCardRuntimeParityManifest`;
- prompt, command revalidation, audit, generated-resource lifetime and rollback text;
- focused verifier type and executable `[Fact]` method names;
- exact source card or exact source-card group;
- exact card matrix row with `fullOfficial=false`;
- 03DC-B audit/evidence doc anchors.

## Boundary

This verifier is stronger than the 03CV / 03CY representative proxy evidence for the selected rows, but it is still not full official closure. It does not modify `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`, does not upgrade `fullOfficial`, does not touch runtime/frontend/browser/formal scripts, and does not claim READY.

## Validation

Focused:

```sh
source scripts/dev-env.sh && dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter FullyQualifiedName~PaymentEngineCoverageAuditTests
```

Observed result: 165/165 passed.

Adjacent:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PaymentEngineCoverageAuditTests|FullyQualifiedName~PaymentEngineUnificationTests|FullyQualifiedName~ResourceSkill|FullyQualifiedName~LegendResourceBridgeVerifierTests|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub"
```

Observed result: 673/673 passed.

Backend full:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

Observed result: 4734/4734 passed.

Diff check:

```sh
git diff --check
```

Observed result: passed.

## Non-Closure

P0-005, P1, full official PaymentEngine breadth, complete `[A]` / `[C]` resource-skill runtime/card-row interactions, full-card matrix, final frontend validation, formal 18-step validation and READY remain open.

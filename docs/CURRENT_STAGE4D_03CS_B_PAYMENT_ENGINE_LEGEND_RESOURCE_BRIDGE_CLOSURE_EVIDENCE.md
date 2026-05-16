# Stage 4D-03CS-B PaymentEngine Legend Resource Bridge Closure Evidence

Audit date: 2026-05-16
Conclusion: **FOCUSED TEST / MANIFEST CLOSURE ONLY / PROJECT NOT READY**

## Files Changed

- `tests/Riftbound.ConformanceTests/LegendResourceBridgeVerifierTests.cs`
- `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`
- `docs/CURRENT_STAGE4D_03CS_B_PAYMENT_ENGINE_LEGEND_RESOURCE_BRIDGE_CLOSURE_AUDIT.md`
- `docs/CURRENT_STAGE4D_03CS_B_PAYMENT_ENGINE_LEGEND_RESOURCE_BRIDGE_CLOSURE_EVIDENCE.md`

## Verifier Coverage

- Success profiles: Diana, Ornn base / reprint, KaiSa base / premium / alternate, Darius base / premium / alternate.
- Prompt: legal source requirements are server filtered by timing and source card; wrong gate states do not expose the bridge source candidate.
- Command: ability id, source object, source-card group, timing, stale source, exhausted source and illegal ability are revalidated server-side.
- Audit: `MANA_GAINED` / `POWER_GAINED` events expose source object, current card no, source card group, ability id, bridge group, resource kind, amount, generated-resource marker and rune-pool lifetime metadata.
- Lifecycle: generated mana / power can pay a later legal `PAY_COST`, is cleared at end turn, and cannot be spent twice.
- Rollback: wrong timing, stale source, exhausted source, illegal ability, duplicate legend use and duplicate spend all reject without mutation.
- Reminder boundary: generated resource skills remain resource production / payment evidence and are not response targets.

## Validation

Fresh validation run for this slice:

```sh
source scripts/dev-env.sh && dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~PaymentEngineCoverageAuditTests|FullyQualifiedName~LegendResourceBridgeVerifierTests"
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PaymentEngineCoverageAuditTests|FullyQualifiedName~LegendResourceBridgeVerifierTests|FullyQualifiedName~ResourceSkill|FullyQualifiedName~LegendAct|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub"
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
git diff --check
```

Results:

- Focused PaymentEngine / legend bridge verifier: passed 217/217.
- Adjacent PaymentEngine / resource skill / legend / prompt / GameHub regression slice: passed 655/655.
- Backend full: passed 4705/4705.
- `git diff --check`: passed.

## Remaining Open Items

`P0-005`, `P1`, full card matrix alignment, full official `[A]` / `[C]` resource-skill breadth, `fullOfficial=false`, final frontend reruns, formal 18-step rerun and READY remain open.

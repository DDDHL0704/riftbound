# Stage 4D-03CL PaymentEngine Legend Resource Bridge Acceptance Verifier Audit

Audit date: 2026-05-16
Conclusion: **TEST-ONLY ACCEPTANCE CONTRACT GUARD ACCEPTED / PROJECT NOT READY**

## 1. Scope

4D-03CL follows the 4D-03CK legend resource bridge implementation handoff / baseline and turns that handoff into executable acceptance-contract coverage in `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`.

This batch does not implement runtime behavior, modify frontend code, edit browser scripts, edit formal 18-step scripts, update card matrix JSON, change `fullOfficial`, change READY status or touch `riftbound-dotnet.sln`.

## 2. Acceptance Manifest

`LegendResourceBridgeImplementationAcceptanceManifest` binds the accepted 4D-03CJ exact 9-card aggregate to the concrete future B-side acceptance contract for four legend bridge groups:

| Bridge group | Source cards | Ability id | Acceptance axes |
|---|---|---|---|
| Diana spell-duel mana | `UNL-197/219` | `LEGEND_SPELL_DUEL_EXHAUST_GAIN_1_MANA` | Server-filtered prompt, command revalidation, generated mana audit / lifetime / consumption / cleanup, rollback and reminder boundary. |
| Ornn equipment power | `SFD·189/221`, `SFD·244/221` | `LEGEND_REACTION_EXHAUST_GAIN_1_POWER_FOR_EQUIPMENT` | Equipment-only prompt, command revalidation, generated equipment power audit / lifetime / consumption / cleanup, rollback, source-card parity and reminder boundary. |
| KaiSa spell power | `OGN·247/298`, `OGN·299/298`, `OGN·299*/298` | `LEGEND_REACTION_EXHAUST_GAIN_1_POWER_FOR_SPELL` | Spell-only prompt, command revalidation, generated spell power audit / lifetime / consumption / cleanup, rollback, source-card parity and reminder boundary. |
| Darius Inspire mana | `OGN·253/298`, `OGN·302/298`, `OGN·302*/298` | `LEGEND_ENCOURAGE_EXHAUST_GAIN_1_MANA` | Inspire / previous-card prompt, command revalidation, generated mana audit / lifetime / consumption / cleanup, rollback, source-card parity and reminder boundary. |

## 3. New Test Assertions

The 4D-03CL verifier adds three focused assertions:

- `PaymentEngineLegendResourceBridgeImplementationAcceptanceManifestMatchesAggregateInputs` proves the acceptance contract exactly inherits the 4D-03CJ aggregate card set, bridge group ids and ability ids.
- `PaymentEngineLegendResourceBridgeImplementationAcceptanceManifestRequiresPromptCommandAuditRollbackAndReminder` proves every bridge group carries prompt, command, audit / resource lifetime, rollback, source-card parity, reminder-boundary and 03CL / 03CK doc anchors.
- `PaymentEngineLegendResourceBridgeImplementationAcceptanceManifestRejectsRuntimeAndReadyClosure` proves this batch remains an acceptance contract only, with runtime / frontend / matrix / READY locked and P0-005 open.

## 4. Validation

Commands to run:

```sh
source scripts/dev-env.sh && dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter FullyQualifiedName~PaymentEngineCoverageAuditTests
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PaymentEngineCoverageAuditTests|FullyQualifiedName~PaymentEngineUnificationTests|FullyQualifiedName~ResourceSkill|FullyQualifiedName~SpellshieldTax|FullyQualifiedName~HasteReady|FullyQualifiedName~TriggerPayment|FullyQualifiedName~LegendAct|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub"
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
git diff --check
```

Results:

- Focused PaymentEngine coverage guard: passed 133/133.
- Adjacent PaymentEngine / resource skill / prompt / hub regression: passed 691/691.
- Backend full: passed 4570/4570.
- `git diff --check`: passed after final 4D-03CL doc sync.

## 5. Non-Closure

4D-03CL does not close P0-005, P1, full-card matrix or READY. It only makes the 03CK implementation acceptance contract executable so future B-side work cannot claim legend `RESOURCE_SKILLS` closure without prompt, command, generated-resource audit / lifetime, rollback, source-card parity and reminder-boundary evidence.

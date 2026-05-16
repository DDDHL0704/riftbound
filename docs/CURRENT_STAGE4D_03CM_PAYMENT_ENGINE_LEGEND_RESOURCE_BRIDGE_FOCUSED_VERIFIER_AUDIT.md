# Stage 4D-03CM PaymentEngine Legend Resource Bridge Focused Verifier Audit

Audit date: 2026-05-16
Conclusion: **FOCUSED VERIFIER ACCEPTED / PROJECT NOT READY**

## 1. Scope

4D-03CM follows the accepted 4D-03CL legend resource bridge acceptance verifier. This batch opens a narrow B-side focused verifier for the existing Diana / Ornn / KaiSa / Darius `LEGEND_ACT` resource bridge family and adds a minimal runtime audit payload field required by that verifier.

This batch does not implement payment-only temporary ledger semantics, generated-resource consumption / cleanup, frontend behavior, browser scripts, formal 18-step scripts, card matrix JSON, `fullOfficial`, READY status or `riftbound-dotnet.sln`.

## 2. Runtime Change

`CoreRuleEngine.GainLegendMana` and `CoreRuleEngine.GainLegendPower` now include a normalized `amount` field in the existing `MANA_GAINED` / `POWER_GAINED` payloads.

The existing `mana`, `manaAfter`, `power` and `powerAfter` payload fields remain unchanged. Resource arithmetic, prompt legality, source exhaustion, timing checks and rollback behavior are unchanged.

## 3. Verifier Coverage

`tests/Riftbound.ConformanceTests/LegendResourceBridgeVerifierTests.cs` adds focused coverage for the exact 9-card legend bridge set:

| Group | Source cards | Ability id | Success gate |
|---|---|---|---|
| Diana | `UNL-197/219` | `LEGEND_SPELL_DUEL_EXHAUST_GAIN_1_MANA` | Spell-duel focus. |
| Ornn | `SFD·189/221`, `SFD·244/221` | `LEGEND_REACTION_EXHAUST_GAIN_1_POWER_FOR_EQUIPMENT` | Pending equipment stack item. |
| KaiSa | `OGN·247/298`, `OGN·299/298`, `OGN·299*/298` | `LEGEND_REACTION_EXHAUST_GAIN_1_POWER_FOR_SPELL` | Pending spell stack item. |
| Darius | `OGN·253/298`, `OGN·302/298`, `OGN·302*/298` | `LEGEND_ENCOURAGE_EXHAUST_GAIN_1_MANA` | Previous-card-this-turn gate. |

The success verifier proves:

- server-generated `LEGEND_ACT` prompt candidate and `sourceRequirements`;
- exact `sourceObjectId`, `cardNo` and `abilityId` binding;
- accepted command resolution and source exhaustion;
- resource gain event payload with `playerId`, `sourceObjectId`, `abilityId` and normalized `amount = 1`.

The rejection verifier proves no-mutation rollback for:

- Diana outside spell-duel focus;
- Ornn with wrong pending spell stack item;
- KaiSa with wrong pending equipment stack item;
- Darius without a previous card played this turn.

## 4. Validation

Commands run:

```sh
source scripts/dev-env.sh && dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter FullyQualifiedName~LegendResourceBridgeVerifierTests
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~LegendResourceBridgeVerifierTests|FullyQualifiedName~PaymentEngineCoverageAuditTests|FullyQualifiedName~PaymentEngineUnificationTests|FullyQualifiedName~ResourceSkill|FullyQualifiedName~LegendAct|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub"
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
git diff --check
```

Results:

- Focused legend bridge verifier: passed 18/18.
- Adjacent PaymentEngine / resource skill / legend / prompt / hub regression: passed 578/578.
- Backend full: passed 4588/4588.
- `git diff --check`: passed after final doc sync.

## 5. Non-Closure

4D-03CM does not close P0-005, P1, generated-resource lifetime / consumption / cleanup, full-card matrix, frontend final validation or READY. It only makes the current legend bridge source-card / prompt / command / rollback / audit-amount evidence executable so future work cannot claim `RESOURCE_SKILLS` closure without the remaining generated-resource lifetime and payment-only ledger evidence.

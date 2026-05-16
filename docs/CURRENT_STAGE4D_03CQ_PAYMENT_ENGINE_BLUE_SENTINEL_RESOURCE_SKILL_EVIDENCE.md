# Stage 4D-03CQ PaymentEngine Blue Sentinel Resource Skill Evidence

Evidence date: 2026-05-16
Conclusion: **ACCEPTED FOCUSED BLUE SENTINEL RESOURCE-SKILL SLICE / NOT READY**

## 1. Changed Files

- `src/Riftbound.Engine/P4ActivatedAbilityCatalog.cs`
  - Adds `BLUE_SENTINEL_HELD_DELAYED_NEXT_MAIN_GAIN_GENERIC_POWER` for `UNL-087/219`, one generated generic payment-only power, the delayed action prefix and the Blue Sentinel-specific restriction.
- `src/Riftbound.Engine/CoreRuleEngine.cs`
  - Captures held-battlefield delayed triggers, revalidates next-main payment use, materializes the temporary payment-only resource and clears consumed triggers.
- `src/Riftbound.Engine/MatchSession.cs`
  - Exposes Blue Sentinel delayed payment actions from authoritative trigger state and suppresses ordinary activate-ability prompts for this delayed resource skill.
- `tests/Riftbound.ConformanceTests/BlueSentinelResourceSkillTests.cs`
  - Adds focused trigger, prompt, payment, audit and no-mutation coverage.
- `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`
  - Moves Blue Sentinel from deferred non-legend runtime lane to implemented representative evidence.
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
  - Registers the new implemented ability id in the catalog-bound official-text audit.

No frontend runtime, Chrome smoke script, formal 18-step script, card matrix JSON, `fullOfficial` / READY or `riftbound-dotnet.sln` changes were made.

## 2. Focused Verifier

Command:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter FullyQualifiedName~BlueSentinelResourceSkillTests
```

Coverage:

- Held-battlefield resolution queues a server-owned delayed trigger for the controller.
- The next-main pending rune payment prompt exposes the generated-resource action.
- `PAY_COST` revalidates the trigger and materializes payment-only generated power.
- Trigger resolution, ability activation, generated power, spend and cleanup audit events are asserted.
- Wrong phase, late next-main window, missing trigger, stale source, stale battlefield, unsupported amount, duplicate spend, non-rune payment and forged temporary-resource commands reject without mutation.

Result:

```txt
included in focused audit recheck: passed 146/146
```

## 3. Focused Audit Rechecks

Commands:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter FullyQualifiedName~PaymentEngineCoverageAuditTests
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~BlueSentinelResourceSkillTests|FullyQualifiedName~PaymentEngineCoverageAuditTests|FullyQualifiedName~P4ActivatedAbilityCatalogAuditsDeferredSkillSurfacesAgainstOfficialText"
```

Result:

```txt
focused Blue Sentinel / coverage / fixture-runner catalog audit recheck: passed 146/146
```

## 4. Adjacent Regression

Command:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~BlueSentinelResourceSkillTests|FullyQualifiedName~PaymentEngineCoverageAuditTests|FullyQualifiedName~PaymentEngineUnificationTests|FullyQualifiedName~ResourceSkill|FullyQualifiedName~SpellshieldTax|FullyQualifiedName~HasteReady|FullyQualifiedName~TriggerPayment|FullyQualifiedName~LegendAct|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub"
```

Result:

```txt
passed 733/733
```

## 5. Backend Full

Command:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

Result:

```txt
passed 4648/4648
```

## 6. Remaining Risks

- Lux remains the only non-legend resource-skill lane.
- Legend resource bridge closure remains explicit future work.
- Resource-skill all-window rows increased to 54 representative rows, but that is not full official closure.
- Frontend final validation, Chrome smoke, formal 18-step E2E, card matrix JSON, `fullOfficial`, P0/P1 and READY remain open.

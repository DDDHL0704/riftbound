# Stage 4D-03CO PaymentEngine Jhin Movement Resource Skill Evidence

Evidence date: 2026-05-16
Conclusion: **ACCEPTED FOCUSED JHIN RESOURCE-SKILL SLICE / NOT READY**

## 1. Changed Files

- `src/Riftbound.Engine/P4ActivatedAbilityCatalog.cs`
  - Adds `JHIN_MOVE_TRIGGER_GAIN_1_MANA_1_POWER` for `UNL-022/219` with 1 generated mana, 1 generated power and Jhin-specific payment-only power restriction.
- `src/Riftbound.Engine/CoreRuleEngine.cs`
  - Captures Jhin movement triggers, validates the service-issued trigger optional cost, resolves the resource skill immediately, writes generated mana to `RunePool`, writes generated power to a temporary payment-only ledger, and expires unused triggers at turn end.
- `src/Riftbound.Engine/MatchSession.cs`
  - Filters Jhin prompt visibility by live movement trigger and exposes Jhin-specific source requirement / temporary resource metadata.
- `tests/Riftbound.ConformanceTests/JhinMovementResourceSkillTests.cs`
  - Adds focused prompt, command, lifecycle, Roam and no-mutation coverage.
- `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`
  - Moves Jhin from deferred non-legend lane to implemented representative evidence and keeps the remaining non-legend lanes explicit.
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
  - Registers the new implemented ability id for fixture-runner coverage expectations.

No frontend runtime, Chrome smoke script, formal 18-step script, card matrix JSON, `fullOfficial` / READY or `riftbound-dotnet.sln` changes were made.

## 2. Focused Verifier

Command:

```sh
source scripts/dev-env.sh && dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter FullyQualifiedName~JhinMovementResourceSkillTests
```

Result:

```txt
passed 14/14
```

Coverage:

- Prompt is hidden before movement and exposed only after server-captured movement.
- Stale movement context removes the Jhin prompt before command submission.
- Successful activation gains 1 mana plus 1 temporary payment-only power.
- Precise Roam battlefield-to-battlefield movement queues and resolves the same trigger path.
- Generated resources can pay a later legal rune cost and then clear.
- Unused generated resources and unused trigger context expire at turn end.
- Wrong window, missing trigger, stale source, stale context, wrong resource use and handwritten trigger attempts reject without mutation.

## 3. Adjacent Regression

Command:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~JhinMovementResourceSkillTests|FullyQualifiedName~PaymentEngineCoverageAuditTests|FullyQualifiedName~PaymentEngineUnificationTests|FullyQualifiedName~ResourceSkill|FullyQualifiedName~SpellshieldTax|FullyQualifiedName~HasteReady|FullyQualifiedName~TriggerPayment|FullyQualifiedName~LegendAct|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub"
```

Result:

```txt
passed 705/705
```

## 4. Backend Full

Command:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

Result:

```txt
passed 4620/4620
```

## 5. Diff Hygiene

Command:

```sh
git diff --check
```

Result:

```txt
passed after final doc sync
```

## 6. Remaining Risks

- Jhin is accepted only for `UNL-022/219`; alternate / reprint parity remains future work.
- The remaining non-legend resource-skill lanes are Honeyfruit, Blue Sentinel and Lux.
- Resource-skill all-window rows increased to 42 representative rows, but that is not full official closure.
- Frontend final validation, Chrome smoke, formal 18-step E2E, card matrix JSON, `fullOfficial`, P0/P1 and READY remain open.

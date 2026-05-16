# Stage 4D-03CR PaymentEngine Lux Resource Skill Evidence

Evidence date: 2026-05-16
Conclusion: **ACCEPTED FOCUSED LUX RESOURCE-SKILL SLICE / NOT READY**

## 1. Changed Files

- `src/Riftbound.Engine/P4ActivatedAbilityCatalog.cs`
  - Adds `LUX_REACTION_EXHAUST_GAIN_2_SPELL_ONLY_MANA` for `OGS·014/024`, generated mana amount, spell-only restriction and Lux resource-skill helper.
- `src/Riftbound.Engine/MatchSession.cs`
  - Exposes ready controlled Lux sources as `PLAY_CARD` payment resource choices when a spell needs spell-only generated mana.
- `src/Riftbound.Engine/CoreRuleEngine.cs`
  - Revalidates Lux payment resource actions, exhausts the source, applies inline generated mana, commits payment and cleans leftover generated mana before publishing state.
- `tests/Riftbound.ConformanceTests/LuxResourceSkillTests.cs`
  - Adds focused catalog, prompt, command, audit and rollback coverage.
- `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`
  - Moves Lux from deferred non-legend runtime lane to implemented representative evidence.

No frontend runtime, Chrome smoke script, formal 18-step script, card matrix JSON, `fullOfficial` / READY or `riftbound-dotnet.sln` changes were made.

## 2. Focused Verifier

Command:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~LuxResourceSkillTests"
```

Result:

```txt
passed 9/9
```

Coverage:

- Prompt exposes Lux only for spell payment resource use.
- `PLAY_CARD` exhausts Lux, gains 2 spell-only mana, consumes needed mana and cleans the leftover.
- Non-spell, exhausted source, wrong source card, unnecessary resource and duplicate resource branches reject without mutation.

## 3. Batch Validation

Commands:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~LuxResourceSkillTests|FullyQualifiedName~PaymentEngineCoverageAuditTests"
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~LuxResourceSkillTests|FullyQualifiedName~PaymentEngineCoverageAuditTests|FullyQualifiedName~PaymentEngineUnificationTests|FullyQualifiedName~ResourceSkill|FullyQualifiedName~SpellshieldTax|FullyQualifiedName~HasteReady|FullyQualifiedName~TriggerPayment|FullyQualifiedName~LegendAct|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub"
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
git diff --check
```

Results:

```txt
focused Lux: passed 9/9
Lux + coverage + fixture catalog audit: passed 143/143
adjacent PaymentEngine / resource skill / legend / prompt / hub regression: passed 742/742
backend full: passed 4657/4657
git diff --check: passed
```

## 4. Remaining Risks

- Legend resource bridge closure remains explicit future work.
- Resource-skill all-window rows increased to 60 representative rows, but that is not full official closure.
- Frontend final validation, Chrome smoke, formal 18-step E2E, card matrix JSON, `fullOfficial`, P0/P1 and READY remain open.

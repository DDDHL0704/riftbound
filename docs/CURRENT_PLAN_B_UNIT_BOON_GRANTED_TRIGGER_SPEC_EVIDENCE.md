# Plan B / Unit Boon-Granted Trigger Spec Evidence

Date: 2026-06-27

Project status: **NOT READY**.

## Rule Sources

- `data/official/card-catalog.zh-CN.json`: `SFD·047/221` 山猿老祖 has official text `当你给予我增益时，让我变为活跃状态。`
- `docs/rules-authority-and-audit.md` and `docs/rules-evidence-index.md` remain the local rule authority/evidence inputs for gameplay behavior changes.

## Automated Evidence

- `CardCatalogBaselineTests.BehaviorSpecCatalogParsesUnitBoonGrantedReadySelfTrigger` proves the official card text parses to `TriggerSpec.Kind = UNIT_BOON_GRANTED_READY_SELF`, `Timing = UNIT_BOON_GRANTED`, `TargetScope = SOURCE_UNIT`, and `ReadiesSource = true`.
- `CardCatalogBaselineTests.UnitBoonGrantedReadySelfTriggerDoesNotUseCardNumberAllowList` guards against reintroducing the old Core constants `MountainApeElderCardNo` and `MountainApeElderBoonReadyEffectKind`.
- `ConformanceFixtureRunnerTests.P79MountainApeElderReadiesWhenGrantedBoon` proves a new server-authoritative boon grant emits `TRIGGER_RESOLVED.trigger/effectKind = UNIT_BOON_GRANTED_READY_SELF`, emits `UNIT_READIED.reason = UNIT_BOON_GRANTED_READY_SELF`, and updates the source unit to active.
- `ConformanceFixtureRunnerTests.P79MountainApeElderAlreadyBoonedDoesNotReadyAgain` proves an already-booned target does not re-trigger the ready path.

## Runtime Evidence

`CoreRuleEngine.ApplyBoon(...)` now resolves the ready-self effect by looking up the target unit's `BehaviorSpec.Triggers` through `UnitBoonGrantedTriggerSpecRules`. The resolver validates the parsed timing, target scope, and `ReadiesSource` shape before mutating exhaustion state.

The accepted event payload shape is:

- `BOON_GRANTED`: unchanged authoritative boon grant event.
- `TRIGGER_RESOLVED.trigger = UNIT_BOON_GRANTED_READY_SELF`.
- `TRIGGER_RESOLVED.effectKind = UNIT_BOON_GRANTED_READY_SELF`.
- `UNIT_READIED.reason = UNIT_BOON_GRANTED_READY_SELF`.

This removes the runtime dependency on the old Mountain Ape card-number/effect-kind branch while preserving existing snapshot and hidden-information boundaries.

## Validation

- Focused TDD: `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~UnitBoonGrantedReadySelf|FullyQualifiedName~MountainApeElder" --nologo` passed 5/5.
- Adjacent: `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~UnitBoonGrantedReadySelf|FullyQualifiedName~MountainApeElder|FullyQualifiedName~Boon|FullyQualifiedName~ApplyBoon|FullyQualifiedName~MatchRecovery|FullyQualifiedName~CardCatalogBaseline" --nologo` passed 2356/2356.
- Full backend conformance: `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo` passed 8809/8809.
- `git diff --check` must still pass before handoff.

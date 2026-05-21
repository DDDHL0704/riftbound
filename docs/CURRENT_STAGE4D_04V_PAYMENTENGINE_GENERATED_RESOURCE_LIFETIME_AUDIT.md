# Stage 4D-04V PaymentEngine Generated Resource Lifetime Audit

Date: 2026-05-21

Status: NOT READY

## Scope

This B_SERVER slice covers focused PaymentEngine generated-resource lifetime and stale-prompt protection only.

Allowed files touched:

- `tests/Riftbound.ConformanceTests/PaymentEngineUnificationTests.cs`
- `tests/Riftbound.ConformanceTests/ResourceConversionEquipmentSkillTests.cs`

Runtime files were inspected but not changed. Matrix JSON, `PaymentEngineCoverageAuditTests.cs`, frontend, official catalog, browser/Chrome/formal E2E scripts, protocol core fields, `fullOfficial`, READY flags, and `riftbound-dotnet.sln` were not touched.

A_MAIN acceptance reran focused generated-resource coverage (258/258), adjacent payment/prompt coverage (1089/1089), backend full test (5247/5247), and `git diff --check`; all passed.

## Findings

- Pending `PAY_COST` temporary payment resources are replay-rejected after successful spend and cleanup.
- Pending `PAY_COST` temporary payment resources are rejected for wrong player, wrong payment id, and wrong payment window without mutation.
- Successful temporary-resource payment clears the pending payment and no longer exposes a PayCost prompt for the consumed resource.
- Energy Channel generated rune-pool mana cannot be spent twice; a replay after the first spend is rejected without mutating rune pools or stack.
- Existing Gold, Honeyfruit, Blue Sentinel, Lux, Legend Resource Bridge, and temporary resource tests continue to pass under the required generated-resource filter.

## Runtime Assessment

No runtime bug was exposed by this focused slice. No server runtime code changed.

## Hidden Info And Protocol

No hidden-information leakage was found. No protocol shape changed.

## Remaining Open Items

- Full PaymentEngine breadth remains open.
- Full card-matrix readiness and `fullOfficial` remain open.
- P0/P1 project closure remains open.
- Frontend build, Chrome smoke, and formal 18-step E2E were not in this slice.

Final conclusion: NOT READY.

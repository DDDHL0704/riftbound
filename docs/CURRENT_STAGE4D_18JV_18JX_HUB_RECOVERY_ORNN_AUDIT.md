# Stage 4D-18JV/18JW/18JX Hub Recovery Ornn Audit

Date: 2026-06-05 23:50 CST

Owner: A_MAIN

Status: accepted into main after A_MAIN review and validation. Project remains **NOT READY**.

## Scope

- 18JV integrated worker commit `7a932739`: `GameHubJoinTests` now covers seeded `DECLARE_BATTLE` duplicate `clientIntentId` raw-payload behavior. Exact same raw payload replays stable output without journal growth; changed raw payload with the same intent returns `CLIENT_INTENT_CONFLICT` without group/caller mutation, snapshot drift or changed journal entries.
- 18JW integrated worker commit `95f08587`: `MatchRecoveryTests` now covers spectator replay-frame timing battle damage assignment `requiredAssignments[]` item fields and values. Missing `sourceObjectId`, `damage` and `legalTargetObjectIds` emit stable item-field diagnostics; negative damage and legal-target whitespace, empty and duplicate values emit stable item-value diagnostics.
- 18JX integrated worker commit `987ec2d0`: `OrnnFriendlyEquipmentStaticPowerTests` now covers remaining friendly-equipment static-aura participant metadata. When one friendly equipment remains on the public field and another has left, authoritative metadata and P1/P2 snapshot views retain only the remaining equipment and do not leak the departed equipment through participant dependency fields.

## Validation

- Focused new tests: `4/4`.
- Touched class filter `GameHubJoinTests|MatchRecoveryTests|OrnnFriendlyEquipmentStaticPowerTests`: `1443/1443`.
- Broader adjacent server filter `GameHubJoinTests|MatchRecoveryTests|OrnnFriendlyEquipmentStaticPowerTests|OfficialOpeningTests|PostgresMatchRecoveryStoreSmokeTests|ConformanceFixtureRunnerTests|LayerEngineTimestampDependencyTests`: `5079/5079`.
- Backend full via tracked `Riftbound.slnx` with `ConnectionStrings__Riftbound` unset: `7259/7259`.
- Mechanical checks after code integration: `git diff --check` and `git diff --cached --check` passed before docs sync.

## Residual Risk

- This is test coverage only; it does not close broader P0/P1 readiness.
- Real DB-backed Postgres smoke remains open because no `ConnectionStrings__Riftbound` was configured.
- Frontend build, Chrome smoke, formal E2E, `fullOfficial`, remaining recovery payload breadth, full LayerEngine breadth and final readiness remain open.

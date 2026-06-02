# Stage 4D-17UK Recovery Spectator Visible Object Unreadable Field Audit

Date: 2026-06-02

Owner: A_MAIN

Status: accepted. Project remains **NOT READY**.

## Scope

This slice narrows P1-004 recovery/replay determinism for spectator replay-frame snapshot player visible object payloads. It only touches recovery validation and recovery conformance tests.

The gap closed here was that visible player object parity already detected readable scalar/list value drift and missing scalar/list fields, but several unreadable-present fields only produced generic shape diagnostics. That made the authoritative visible-object drift less explicit for malformed spectator payloads that carried invalid `cardNo`, owner/controller ids, attached object ids, numeric combat/card scalars, booleans, `tags[]` or `untilEndOfTurnEffects[]`.

## Runtime Change

`MatchRecoveryValidator` now emits authoritative parity mismatch diagnostics for spectator replay-frame snapshot player visible object fields when those fields are present but unreadable.

The helper coverage includes:

- optional-present string scalars: `cardNo`, `ownerId`, `controllerId`, `attachedToObjectId`;
- numeric scalars: `damage`, `power`, `basePower`, `effectivePower`, `untilEndOfTurnPowerModifier`, `manaCost`;
- boolean scalars: `isExhausted`, `isAttacking`, `isDefending`;
- string-list scalars: `tags[]`, `untilEndOfTurnEffects[]`.

Readable value drift keeps the existing diagnostic wording. Missing field behavior is unchanged. Optional string parity stays conservative for authoritative-empty optional fields.

No protocol shape, frontend, official catalog, matrix JSON, command execution, randomness or solution-file changes were made.

## Test Coverage

Added `RecoveryValidatorRejectsSpectatorReplaySnapshotVisiblePlayerObjectUnreadableScalarParity`.

The test mutates one spectator-visible battlefield object with unreadable-present values across string, integer, boolean and list fields while authoritative `CardObjectState` remains fully populated.

Expected diagnostics are authoritative object mismatch diagnostics for:

- card number, owner id, controller id and attached object id;
- damage, power, base power, effective power, until-end-of-turn power modifier and mana cost;
- exhausted, attacking and defending state;
- tags and until-end-of-turn effects.

## Validation

- Focused unreadable scalar/list parity test: `1/1`.
- Focused visible/spectator player object filter: `15/15`.
- Focused `MatchRecoveryTests` filter: `664/664`.
- Adjacent recovery/opening/store-smoke filter: `1245/1245`.
- Backend full: `6610/6610`.
- `git diff --check`: passed.
- Anchored conflict-marker scan over `docs`, `src` and `tests`: no matches.
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`: passed.
- Touched-file scoped `dotnet format --verify-no-changes --no-restore --include src/Riftbound.Engine/MatchRecovery.cs tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`: passed.
- Full `dotnet format --verify-no-changes --no-restore`: still blocked by unrelated pre-existing whitespace diagnostics outside this slice, including `CoreRuleEngine.cs`, `MatchSession.cs`, `ConformanceFixtureRunnerTests.cs`, `GameHubJoinTests.cs`, `PaymentEngineCoverageAuditTests.cs` and `TriggerPaymentTests.cs`; no unrelated formatting was applied.

## Remaining Work

This slice does not close broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` or final readiness status.

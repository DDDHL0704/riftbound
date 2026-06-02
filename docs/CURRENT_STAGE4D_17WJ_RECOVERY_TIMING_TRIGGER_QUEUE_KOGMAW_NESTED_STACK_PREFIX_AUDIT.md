# Stage 4D-17WJ Recovery Timing Trigger Queue Kogmaw Nested Stack Prefix Audit

Date: 2026-06-03

Status: accepted for A_MAIN checkpoint. Project remains **NOT READY**.

## Scope

Stage 4D-17WJ tightened the Kogmaw last-breath `triggerQueue[]` recovery recognizer across recovered snapshots, authoritative state and spectator replay frames.

The validator still applies Kogmaw context validation to true Kogmaw trigger queue entries, but it now skips that guard when a Kogmaw marker is embedded inside a previous stack item id prefix and the current trigger id ends with the current non-Kogmaw effect tail. This prevents downstream last-breath triggers caused by a Kogmaw stack resolution from being misread as Kogmaw trigger entries.

## Runtime Basis

Runtime Kogmaw trigger queue ids include `OGN_KOGMAW_LAST_BREATH_AOE_PLAY_UNIT::BATTLEFIELD::...`. Later trigger queue entries can include the resolved stack item id in their own `TRIGGER-{stackItemId}-{sourceObjectId}-{effectKind}` prefix. If that resolved stack item was a Kogmaw trigger, the later non-Kogmaw trigger id can legitimately contain the Kogmaw marker before its own source/effect tail.

This slice keeps true Kogmaw marker validation from Stage 4D-17WI and adds the nested-prefix boundary. It changes recovery frame and authoritative-state validation only. It does not change command resolution, protocol shape, frontend code, matrix JSON, official catalog data, browser/Chrome/formal E2E scripts, `fullOfficial`, final readiness status or `riftbound-dotnet.sln`.

## Coverage

New tests:

- `RecoveryValidatorAcceptsSnapshotTimingTriggerQueueNestedKogmawStackPrefix`
- `RecoveryValidatorAcceptsAuthoritativeStateTriggerQueueNestedKogmawStackPrefix`
- `RecoveryValidatorAcceptsSpectatorReplayTimingTriggerQueueNestedKogmawStackPrefix`

## Validation

- Focused new nested Kogmaw stack-prefix tests: `3/3`
- Focused Kogmaw context/nested filter: `6/6`
- Focused `TriggerQueue` filter: `104/104`
- Focused recovery filter: `789/789`
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1369/1369`
- Backend full conformance: `6734/6734`
- Touched-file scoped whitespace format: passed
- `git diff --check`: passed
- Anchored conflict-marker scan: passed
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`: passed

## Remaining Risk

This narrows P1-004 replay/recovery determinism and Kogmaw trigger-queue nested prefix correctness only. Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final status remain open.

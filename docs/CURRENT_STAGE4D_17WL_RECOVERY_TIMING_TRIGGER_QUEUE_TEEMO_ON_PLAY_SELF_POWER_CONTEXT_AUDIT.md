# Stage 4D-17WL Recovery Timing Trigger Queue Teemo On-Play Self-Power Context Audit

Date: 2026-06-03

Project status: **NOT READY**.

## Scope

Stage 4D-17WL tightened the Teemo on-play self-power trigger-queue recovery recognizer across recovered snapshots, authoritative state and spectator replay frames.

Runtime creates these trigger queue items via `CoreRuleEngine.BuildOnPlayTriggerQueueItem` as `TRIGGER-{stackItemId}-{effectKind}`, with effect kind `TEEMO_PLAY_UNIT_SELF_POWER_PLUS_3`, `TEEMO_ALT_A_PLAY_UNIT_SELF_POWER_PLUS_3`, `TEEMO_ALT_B_PLAY_UNIT_SELF_POWER_PLUS_3` or `FND_TEEMO_PLAY_UNIT_SELF_POWER_PLUS_3`, and triggered event kind `UNIT_PLAYED_TO_BASE`.

## Runtime Basis

- `CoreRuleEngine.BuildOnPlayTriggerQueueItem` queues on-play source-power triggers with the current stack item id plus the concrete Teemo self-power effect kind suffix.
- Runtime Teemo self-power trigger queue items retain visible source context, the concrete effect kind and `UNIT_PLAYED_TO_BASE` as the trigger event.
- Source object membership stays covered by existing trigger-queue object membership validation; this slice adds Teemo-specific source visibility, effect-kind and triggered-event-kind context checks.

## Coverage

New `MatchRecoveryTests`:

- `RecoveryValidatorRejectsSnapshotTimingTriggerQueueTeemoOnPlaySelfPowerContextDrift`
- `RecoveryValidatorRejectsAuthoritativeStateTriggerQueueTeemoOnPlaySelfPowerContextDrift`
- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueTeemoOnPlaySelfPowerContextDrift`

The tests prove recovered snapshot, authoritative state and spectator replay-frame timing payloads reject Teemo on-play self-power effect/event drift.

## Validation

- Focused new Teemo on-play self-power context tests: `3/3`
- Focused `TriggerQueue` filter: `110/110`
- Focused recovery filter: `795/795`
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1375/1375`
- Backend full conformance: `6740/6740`
- Touched-file scoped whitespace format passed.
- `git diff --check` passed.
- Anchored conflict-marker scan over `src`, `tests` and `docs` passed.
- Matrix JSON parse passed.

## Remaining Risk

This narrows replay/recovery determinism only. Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, `fullOfficial`, P0/P1 closure and final status remain open.

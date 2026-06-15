# Stage 4D-223P Recovery Spectator Trigger Queue TriggerId Missing Field Count Audit

Date: 2026-06-15

Status: accepted as a narrow A_MAIN server-test slice on local `main`.

Runtime changed: no. Test coverage changed: yes.

## Scope

This slice extends `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

The new `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueTriggerIdMissingFieldWithCountMismatch` coverage builds a spectator replay frame from an authoritative state with one natural visible trigger queue item, removes `triggerId` from that existing item, then appends an extra spectator trigger. It proves recovery validation reports all of the relevant diagnostics together:

- the malformed trigger queue item still emits `trigger id is required`;
- the authoritative `trigger-visible` id is still required by authoritative state;
- the extra spectator `trigger-extra` id is rejected as not present in authoritative state;
- the trigger queue count mismatch is reported as `2` spectator items versus `1` authoritative item.

The existing `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueTriggerIdMissingFieldWithoutCountMismatch` path remains intact and still proves the same required-id diagnostics without a trigger queue count mismatch. This slice only locks recovery-frame validation behavior; it does not change command resolution, trigger creation, hidden information redaction, stack timing or runtime behavior.

## Rule Source

Per the core-rule PDF gate in `docs/CURRENT_CORE_RULE_PDF_READING_NOTES.md`, A_MAIN re-checked the extracted local rule text before this slice:

- `/tmp/riftbound_rules_pdf_text/core_rules_260330.txt`
- Core rules 327 and 333: stack zone and stack creation from played cards, tokens, active skills and triggered skills.
- Core rules 342 and 376: spell-duel and active-skill timing anchors adjacent to stack windows.
- Core rules 382-383: triggered skills, trigger conditions, putting triggered skills on the stack, controller ordering and trigger limits.
- Core rules 401-404: active or triggered skill pending item confirmation, choices, costs and declining triggered-skill costs.
- Core rule 808.1.d: Last Breath is a triggered-skill keyword and records required source details before the destroyed permanent moves to discard.

The rule check confirms trigger queue identity is public recovery metadata needed to match pending trigger items. The test does not reveal hidden source card identities, private deck order, random seeds or other private state.

## Validation

- Focused: `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueTriggerIdMissingFieldWithCountMismatch` passed `1/1`.
- Changed class: `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter FullyQualifiedName~MatchRecoveryTests` passed `1939/1939`.
- Adjacent: `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~MatchRecovery|FullyQualifiedName~RecoveryStore|FullyQualifiedName~TriggerQueue|FullyQualifiedName~SpectatorReplay|FullyQualifiedName~Recovery"` passed `2004/2004`.
- Backend full: `dotnet test Riftbound.slnx --no-restore` passed `8262/8262`.
- Mechanical: `git diff --check` passed and anchored conflict-marker scan over `docs src tests` found no matches before the code commit.
- Standing merge source: `rule-audit-remaining-20260615` had no committed changes ahead of `main` during the pre-batch and pre-code-commit checks for this slice.

Note: validation used the project `.NET 10.0.100` runtime from `/Users/dinghaolin/.dotnet` with the same `DOTNET_ROOT`/PATH values as `scripts/dev-env.sh`.

Project remains **NOT READY**.

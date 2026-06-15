# Stage 4D-223Q GameHub Wrapper Replay Message Type Audit

Date: 2026-06-15

Status: accepted as a narrow A_MAIN server-test slice on local `main`.

Runtime changed: no. Test coverage changed: yes.

## Scope

This slice extends `tests/Riftbound.ConformanceTests/GameHubJoinTests.cs`.

`ReadyWrapperDuplicateClientIntentRawPayloadReplaysButSubmitIntentChangedRawConflictsWithoutMutation` now proves the idempotent replayed wrapper `READY` group event envelope preserves default protocol/schema versions, and that replayed group `SNAPSHOT` and `PROMPT` fanout messages explicitly carry `MessageType.SNAPSHOT` / `MessageType.PROMPT` plus default protocol/schema versions.

`PassWrapperDuplicateClientIntentRawPayloadReplaysButSubmitIntentChangedRawConflictsWithoutMutation` now proves the same replay-envelope contract for the `PASS` wrapper path: replayed `EVENTS` preserves default protocol/schema versions, and replayed group `SNAPSHOT` / `PROMPT` fanout messages carry their explicit message types and default protocol/schema versions.

Both tests retain their existing raw-payload idempotency and conflict assertions: exact wrapper replay does not add a journal entry or mutate state, while a later `SubmitIntent` with the same client intent id and changed raw payload returns `CLIENT_INTENT_CONFLICT` without broadcasting.

## Rule Source

Per the core-rule PDF gate in `docs/CURRENT_CORE_RULE_PDF_READING_NOTES.md`, A_MAIN re-checked the standing rule notes before this slice. This was a pure protocol-envelope coverage change, not a runtime rule-behavior change.

Relevant anchors:

- GameHub replay/protocol-envelope tests must preserve player identity normalization, event kind stability, snapshot/prompt fanout, server tick stability and protocol/schema defaults without asserting private information leakage.
- Pass/turn sequencing remains rule-sensitive, so this slice kept the existing accepted event-kind and server-tick assertions intact and only added envelope type/default checks around the already-replayed fanout.
- No assertions expose hidden card identities, private deck order, random seeds or other private state.

## Validation

- Focused: `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~ReadyWrapperDuplicateClientIntentRawPayloadReplaysButSubmitIntentChangedRawConflictsWithoutMutation|FullyQualifiedName~PassWrapperDuplicateClientIntentRawPayloadReplaysButSubmitIntentChangedRawConflictsWithoutMutation"` passed `2/2`.
- Changed class: `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter FullyQualifiedName~GameHubJoinTests` passed `217/217`.
- Adjacent: `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~GameHub|FullyQualifiedName~Protocol|FullyQualifiedName~Ready|FullyQualifiedName~Pass|FullyQualifiedName~Replay|FullyQualifiedName~ClientIntent|FullyQualifiedName~Wrapper"` passed `2303/2303`.
- Backend full: `dotnet test Riftbound.slnx --no-restore` passed `8262/8262`.
- Mechanical: `git diff --check` passed and anchored conflict-marker scan over `docs src tests` found no matches before the code commit.
- Standing merge source: `rule-audit-remaining-20260615` had no committed changes ahead of `main` during the pre-batch and pre-code-commit checks for this slice.

Note: validation used the project `.NET 10.0.100` runtime from `/Users/dinghaolin/.dotnet` with the same `DOTNET_ROOT`/PATH values as `scripts/dev-env.sh`.

Project remains **NOT READY**.

# Stage 4D-222U GameHub OrderTriggers Replay Protocol Envelope Audit

Date: 2026-06-15

Status: accepted as a narrow A_MAIN server-test slice on local `main`.

Runtime changed: no. Test coverage changed: yes.

## Scope

This slice extends `OrderTriggersDuplicateClientIntentRawPayloadReplaysButChangedRawConflictsWithoutMutation` in `tests/Riftbound.ConformanceTests/GameHubJoinTests.cs`.

The test now proves the replayed `ORDER_TRIGGERS` raw-command idempotency path:

- accepts the replay through whitespace-normalized player identity (`" P1 "` -> `P1`);
- emits a replayed `EVENTS` message with default protocol/schema versions;
- preserves normalized player routing, the accepted server tick and event-kind sequence;
- preserves the accepted ordered-trigger payload;
- emits replayed `SNAPSHOT` and `PROMPT` messages with default protocol/schema versions;
- preserves accepted snapshot/prompt player fanout and prompt actions;
- keeps the existing raw-command conflict rejection and no-mutation checks intact.

## Rule Source

Per the core-rule PDF gate in `docs/CURRENT_CORE_RULE_PDF_READING_NOTES.md`, A_MAIN re-checked the extracted local rule text before this slice:

- `/tmp/riftbound_rules_pdf_text/core_rules_260330.txt`
- `/tmp/riftbound_rules_pdf_text/judge_faq_251023.txt`
- `《符文战场》核心规则_260330.pdf` section 382/383: triggered skills are placed on the stack after triggering conditions are satisfied; same-controller simultaneous triggers are ordered by that controller, while multi-controller simultaneous triggers are ordered from the turn player in turn order.
- Core rules around 401/402: active or triggered skills become pending stack items, require related choices, and trigger choices are part of the skill confirmation flow.
- Core rules around 459.2.d and 459.2.d.1: combat identity establishment can create pending triggered skills and orders attack/defense trigger insertion with the focused attacker first, then non-defenders in turn order, then defenders.
- `裁判FAQ_251023.pdf` question 2.2 and 2.3: confirms same-time triggered skills are ordered by controller and turn-player order, with battle initial-chain attack/defense trigger ordering handled by the special battle rule.

This slice only validates protocol-envelope replay behavior for an already implemented development order-triggers scenario; it does not change trigger ordering, prompt construction, stack insertion, priority/focus, battle timing, trigger legality or runtime behavior. The replay assertions are limited to public envelope metadata and already accepted snapshot/prompt fanout.

## Validation

- Focused: `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~OrderTriggersDuplicateClientIntentRawPayloadReplaysButChangedRawConflictsWithoutMutation"` passed `2/2`.
- Changed class: `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~GameHubJoinTests"` passed `217/217`.
- Adjacent: `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~Hub|FullyQualifiedName~protocol|FullyQualifiedName~OrderTriggers|FullyQualifiedName~Replay|FullyQualifiedName~Raw|FullyQualifiedName~ClientIntent|FullyQualifiedName~Development"` passed `2015/2015`.
- Backend full: `dotnet test Riftbound.slnx --no-restore` passed `8260/8260`.
- Mechanical: `git diff --check` passed and anchored conflict-marker scan over `docs src tests` found no matches before the code commit.
- Standing merge source: `rule-audit-remaining-20260615` had no committed changes ahead of `main` during the pre-batch and pre-checkpoint checks for this slice; its docs-only untracked full-pass audit artifact was imported as `docs/CURRENT_RULE_AUDIT_FULL_PASS_2026-06-15.md` in the docs checkpoint.

Note: validation used the project `.NET 10.0.100` runtime from `/Users/dinghaolin/.dotnet` with the same `DOTNET_ROOT`/PATH values as `scripts/dev-env.sh`; the script itself probes `psql`/`redis-cli`, which were not on this shell PATH.

Project remains **NOT READY**.

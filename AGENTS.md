# Riftbound Project Agent Guide

Current phase: [BACKEND_RULES_GOAL](docs/BACKEND_RULES_GOAL_2026-10-23.md). Keep the existing ACTIVE Goal and all applicable official backend rules in scope. Client development, packaging iteration, visible desktop pairs and Windows hardware acceptance remain OPEN for the later client phase; retain existing tests and CI.

Current collaboration: two existing windows each own one complete mechanism module and assigned shared hooks. Main owns governance and integration. Notify only interface/source changes, conflicts, blockers or serious regressions immediately; otherwise send one complete delivery without acknowledgement loops. Do not wake stopped chats or create extra windows.

## Current scope and entry points

- Work in this repository, /Users/dinghaolin/MyProjects/riftbound. The product is the Godot .NET macOS / Windows client with a server-authoritative engine; React DevUi is a debug and protocol client.
- Read docs/PROJECT_STATUS.md, docs/PROJECT_GOVERNANCE.md and the relevant part of docs/RULE_TEST_WORKFLOW.md first. Then read only the source manifest, scoped audit and tests relevant to the change.
- Old CURRENT_*, P-stage and coordination documents are historical records, not active tasks, write locks or rule certification. Do not load giant cumulative lists to recover current work.
- The preparation round is complete. On 2026-10-12 the user explicitly authorized a new active official-audit Goal. Continue until the complete source-version coverage and required acceptance evidence close, or the user explicitly pauses. Do not stop at a batch or green CI.

## Development lifecycle

- Intermediate development versions are not compatibility targets. Replace obsolete implementations rather than adding migrations/fallbacks for old development matches or protocols.
- Keep current-version reconnect, persistence validation, hidden-information protection and server authority. Never rewrite accepted action history to make a new engine pass.
- Delete only with evidence of an obsolete purpose or unused runtime path; account for tests and references. Date, file length, duplicate bytes or zero incoming links alone are insufficient.

## Rule authority and trust

- Official regional card text, rules, FAQ/QA, errata and restriction documents are authorities. Record URL, content hash, publication/effective dates, language, region and exact scope.
- Downloaded data/official is a snapshot, not proof that the live site is unchanged. Reuse verified source identity and applicability when unchanged; check relevant new announcements/errata and final effective boundaries. Mark inaccessible or unverified applicability UNKNOWN; never infer RAD is future-effective.
- September and earlier rule implementations and green/coverage claims have no default trust. Re-certify the exact scope against current applicable official sources. Keep October scoped evidence within its stated scope.
- Community platforms, TTS, old Java and generated planning documents are references only. Registration, implemented, RepresentativeRulePass, counts and CI green do not certify whole-card semantics.
- Generic keywords, timing, costs, zones/information, lifecycle and triggers belong in shared engine flow. Avoid card-specific bypasses.
- Preserve the full source universe and unresolved items. Complete certification needs every applicable card/rule/QA item, source gaps, executable evidence and required native gates closed.
- The latest source coverage is linked from docs/PROJECT_STATUS.md. The 2026-10-11 checker remains a check of that historical rule/index baseline, not proof that later live-source deltas are absent.

## Project map

- src/Riftbound.Contracts: shared commands, events, snapshots and prompts.
- src/Riftbound.CardCatalog: official catalog and parsed metadata; metadata is not certification.
- src/Riftbound.Engine: legality, shared gameplay flow, serialized commands, projection, replay and recovery.
- src/Riftbound.Persistence: PostgreSQL journal and recovery adapters.
- src/Riftbound.Api: HTTP/SignalR, rooms, matchmaking and process composition.
- clients/godot: native rendering, command adapters and packaging.
- src/Riftbound.DevUi: debug/protocol rendering and browser verification.
- tests/Riftbound.*, scripts/tests: behavioral and verification guardrails.
- data/official, docs/evidence: source snapshots and bounded evidence.

## Engine and client contracts

- Trace protocol -> shared rule -> event/snapshot -> real command test. Keep rejection-state invariance and action/event/state replay evidence.
- Prompts expose legal, safe choices; clients submit intent with prompt/tick identity. Clients never decide payment, damage, control, trigger order or victory.
- Protect opponent hand/deck order, unrevealed rune identities and face-down standby identities in every projection, prompt and log.
- Distinguish legacy fixtures from official expected behavior. Preserve meaningful assertions and Theory rows during refactors.
- Recovery security tests retain independent expected checks; reusing production derivation as the expected value is insufficient.

## Validation and delivery

- Apply L1/L2/L3 in docs/RULE_TEST_WORKFLOW.md. L1 is an isolated consumer of an already verified mechanism; shared timing, legality/permissions, hidden information, events/serialization, persistence/recovery, new mechanisms or uncertain impact require L2 or full.
- L1: source, focused and affected regression plus legal/illegal/boundary assertions and one compact ledger row. Accumulate check/full in daily integration/nightly CI. L2: module focus, adjacent domains, affected original games and one check per actual integration batch; verify affected SQL/API/restart/replay and security in that batch. L3: weekly milestone/candidate complete backend acceptance.
- CI tier phase 1 is implemented locally: new independent test classes may use L1; mapped backend mechanism paths use L2 check plus all SQL/network jobs. Card registry/catalog-only additions and unknown paths still require full until safe consumer classification exists. Nightly/weekly/manual runs remain full; stable full gates keep their original meaning. Remote execution of this workflow is pending.
- Preserve all original methods/Theory rows, 143 full-game methods/151 cases, 110 ordered pairs, 11 decks, seed 424242 and victory/replay assertions. Source-corrected expectations retain original failures and rationale.
- Reuse results only for identical verified fixed inputs; integration adds actual differences. Do not edit running frozen source/bin. A previous full is a baseline, not current certification.
- Put new large logs/TRX/SQL output/binaries in ignored local output or CI artifacts, not Git. Git keeps source/tests, compact provenance/results and retrievable paths/run IDs. Preserve committed historical evidence and useful original failures. A hash alone is not an accessible artifact.
- Whole-card certification covers every effect/keyword/applicable branch and mechanism interaction with official evidence and executable semantic assertions. Registry/test counts and old obligation totals are not completion rates.
- Run git diff --check and appropriate checks; commit/push coherent batches under existing authorization. Do not repeat unchanged full verification or generate cumulative evidence copies for every batch.
- Only the latest fixed candidate with all applicable source/semantic/backend gates closed can finish the backend Goal. Never infer READY from a local batch or green CI.

## Future page design

- Future page design uses frontend-design and Product Design. Audit current screenshots first; preserve existing product styles for scoped maintenance.
- Use Rift Atlas, Untap.in and Riftbound TTS as design references; refresh relevant screenshots before comparison. They are not rule authorities.
- This round does not modify game pages. References and requirements are in docs/PROJECT_GOVERNANCE.md.

## Agent skills

### Issue tracker

Track audit problems, changes and specs in GitHub Issues for DDDHL0704/riftbound. See `docs/agents/issue-tracker.md`.

### Triage labels

Use the five canonical triage labels with one category and one state per triaged issue. See `docs/agents/triage-labels.md`.

### Domain docs

Single-context: root `GLOSSARY.md` and `docs/adr/`, created when terms or decisions are resolved. See `docs/agents/domain.md`.

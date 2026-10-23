# Riftbound Project Agent Guide

Current phase: [BACKEND_RULES_GOAL](docs/BACKEND_RULES_GOAL_2026-10-23.md). All applicable official backend rules remain in scope. The two replaced chats have paused Goals, not completed Goals. Integration works on delivery events without an automatic continuation Goal; the three development chats continue their assigned backend goals. Client development, packaging iteration, visible desktop pairs and Windows hardware acceptance remain OPEN for the later client phase; retain existing tests and CI.

Current collaboration: four authorized chats own integration/acceptance, combat/triggers/victory, costs/resources/action legality, and card effects/mechanism consumers. Exact chat IDs, branches, worktrees and method ownership are in the backend goal. Integration alone maintains governance, certification summaries and CI tools, reviews shared API/event contracts, and publishes main. Notify only real interface/source changes, conflicts, shared blockers or serious regressions immediately; otherwise send one complete delivery to integration without acknowledgement loops. Stopped chats remain read-only.

## Current scope and entry points

- Use the assigned /Users/dinghaolin/MyProjects/.worktrees/riftbound-{integration,combat,payment,cards}-20261023 worktree with an explicit workdir for every Git/source operation. MyProjects itself is not a Git repository. The original /Users/dinghaolin/MyProjects/riftbound checkout is for read-only checks and necessary integration publication; old card and frozen check trees are not development trees. Preserve their source, bin, untracked evidence and databases.
- Read docs/PROJECT_STATUS.md, docs/PROJECT_GOVERNANCE.md and the relevant part of docs/RULE_TEST_WORKFLOW.md first. Then read only the source manifest, scoped audit and tests relevant to the change.
- Old CURRENT_*, P-stage and coordination documents are historical records, not active tasks, write locks or rule certification. Do not load giant cumulative lists to recover current work.
- The preparation round is complete. The 2026-10-23 four-line authorization supersedes the old two-window/sole-core-writer arrangement. Development chats continue their assigned goals until closure or an explicit pause. Integration completes each actual handover/review batch and returns idle; no idle polling, infinite continuation Goal or claimed automatic CI monitoring.

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
- Preserve the full source universe and unresolved items. Complete backend certification needs every applicable card/rule/QA item, source gaps, executable evidence and required backend gates closed; later client gates retain their OPEN identity.
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
- Keep one main work package and at most one delivered batch awaiting integration per development chat. Shared files are divided by actual methods, not exclusive ownership of the whole Engine. Ask integration once for a conflicting hook and continue independent work. Commit/push each line's own codex/backend-* branch; integration alone reviews/merges/publishes main after checking remote ancestry and concurrent changes, without force/reset.
- This host has 10 logical cores and 24 GiB. Targeted development tests use --jobs 1 or 2 when supported. Integration schedules new heavy check/full work, initially one new heavy task at a time; retain the existing N54/N55 frozen checks without duplicate runs. Use independent output, API ports and database identities; do not stop another line's service. Existing nightly CI needs no new Codex polling automation.
- CI tier phase 1 is implemented locally: new independent test classes may use L1; mapped backend mechanism paths use L2 check plus all SQL/network jobs. Card registry/catalog-only additions and unknown paths still require full until safe consumer classification exists. Nightly/weekly/manual runs remain full; stable full gates keep their original meaning. Remote execution of this workflow is pending.
- Preserve all original methods/Theory rows, 143 full-game methods/151 cases, 110 ordered pairs, 11 decks, seed 424242 and victory/replay assertions. Source-corrected expectations retain original failures and rationale.
- Reuse results only for identical verified fixed inputs; integration adds actual differences. Do not edit running frozen source/bin. A previous full is a baseline, not current certification.
- Put new large logs/TRX/SQL output/binaries in ignored local output or CI artifacts, not Git. Git keeps source/tests, compact provenance/results and retrievable paths/run IDs. Preserve committed historical evidence and useful original failures. A hash alone is not an accessible artifact.
- Whole-card certification covers every effect/keyword/applicable branch and mechanism interaction with official evidence and executable semantic assertions. Registry/test counts and old obligation totals are not completion rates.
- Run git diff --check and appropriate checks; commit/push coherent batches under existing authorization. Do not repeat unchanged full verification or generate cumulative evidence copies for every batch.
- Only the latest fixed candidate with all applicable source/semantic/backend gates closed can finish the backend stage. Never infer READY from a local batch or green CI, or mark the replaced paused Goals complete.

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

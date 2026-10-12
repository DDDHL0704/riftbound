# Riftbound Project Agent Guide

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
- Downloaded data/official is a snapshot, not proof that the live site is unchanged. Refresh applicable official sources before gameplay changes; mark inaccessible/future-effective versions explicitly.
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

- Develop with python3 scripts/test-rules.py focus <domain> and affected adjacent tests. Run one check at a coherent batch end. Use full for broad changes, integration and final acceptance; do not repeat it for an unchanged input.
- Code CI retains the complete suite and packaging gates. Ordinary rules start in the first wave; all methods/cases and the full preconstructed matrix remain.
- Only an explicit reviewed documentation path list may use light CI. Official data, fixture, build configuration, scripts, unknown paths and uncertain diffs default to full.
- CI verifies a fixed commit; continue the next batch independently. Local parallel verification uses a fixed separate checkout. Do not edit or rebuild the source/bin being tested.
- Record commit/input hashes, source versions, profile, environment, TRX and matrix identities. An earlier full run is a baseline, not current-HEAD evidence.
- Native controls, network/reconnect, visible paired clients, Windows runtime and database execution are separate gates. Returning without a database connection is not database validation.
- Run git diff --check and appropriate checks before each coherent commit/push. Do not create a full-regression bookkeeping loop for documentation-only evidence updates.
- Never mark READY or complete the official audit from a local batch or all-green tests.

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

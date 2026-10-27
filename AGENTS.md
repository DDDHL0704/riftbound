# Riftbound Project Agent Guide

Current phase: [BACKEND_RULES_GOAL](docs/BACKEND_RULES_GOAL_2026-10-23.md). All applicable official backend rules remain in scope. Replaced development chats retain their unfinished Goals as history. Integration works on delivery events without an automatic continuation Goal; the three development chats continue their assigned backend goals. Client development, packaging iteration, visible desktop pairs and Windows hardware acceptance remain OPEN for the later client phase; retain existing tests and CI.

Current collaboration: four authorized chats own integration/acceptance, combat/triggers/victory, costs/resources/action legality, and card effects/mechanism consumers. Exact chat IDs, branches, worktrees and method ownership are in the backend goal. Integration maintains governance, certification summaries and CI tools. Each development line may implement its owned methods, open a PR and merge after the current L2 check passes; no blanket human-review requirement. Only concurrent edits to the same method require integration to assign ownership/order. Interface dependencies use explicit commit dependencies. Send one complete deliverable, actual method conflict or blocker; no acknowledgement loops or per-helper/expression approvals. Stopped chats remain read-only.

2026-10-27 replacement: the four local work chats have moved to new chat IDs in their existing worktrees; the four replaced local chats are archived and read-only. The coordinator is unchanged. The macbook-uu replacement is deferred while SSH/UU is offline; its old T01–T08 Goal is complete and public c27b remains available for finite read-only intake. Do not wake or archive that remote chat, move its work to this host, or block the four local lines on its availability. Current IDs are recorded only in the backend goal.

2026-10-26 backlog drain completed: PR165 ordinary-merged as main `645a045e435dbb4521eca5e5318c10ffcee36511`. All 12 original/current heads are main ancestors, all 12 PRs are actually merged, and open PRs were verified 22→10. The prerequisite freeze on new feature PRs is released. Keep latest complete L2, strict Actions15368/admin enforcement, zero skips, serial main publication and nightly L3 unchanged. Integration has no Goal or monitor and continues the already assigned finite S3/P7 acceptance wiring task. Never close undelivered work merely to lower a count.

Storage: retain source as Git commits plus necessary patches. Execution trees contain only build/test inputs, excluding nested var/.git/bin/obj, large documentation/evidence and client resources. Use verified local `cp -c` (or filtered `cp -cR`) for necessary large copies; each line adds only its current execution tree and necessary failure comparison while preserving named unique evidence. Below 20 GiB free, defer new large copies, report once and continue source/remote work. Do not bulk-delete another line's files; heavy full/SQL/network/restart runs stay in Actions.

## Current scope and entry points

- Use the assigned /Volumes/ExternalSSD/MyProjects/.worktrees/riftbound-{integration,combat,payment,cards}-20261023 worktree with an explicit workdir for every Git/source operation. MyProjects itself is not a Git repository. The original /Volumes/ExternalSSD/MyProjects/riftbound checkout is for read-only checks and necessary integration publication; old card and frozen check trees are not development trees. Preserve their source, bin, untracked evidence and databases.
- Read docs/PROJECT_STATUS.md, docs/PROJECT_GOVERNANCE.md and the relevant part of docs/RULE_TEST_WORKFLOW.md first. Then read only the source manifest, scoped audit and tests relevant to the change.
- Old CURRENT_*, P-stage and coordination documents are historical records, not active tasks, write locks or rule certification. Do not load giant cumulative lists to recover current work.
- The 2026-10-24 human authorization supersedes the old sole-publisher and fixed-candidate admission policies. Daily merges require targeted tests and the rule suite (L2). L3 runs once nightly on one fixed main starting SHA; unrelated packages do not wait for it. Old B1/source queues are migration inventories, not admission gates. Development chats continue their assigned goals until closure or an explicit pause. The finite 2026-10-26 backlog drain reached 22→10; integration now continues the assigned acceptance-wiring task without a Goal or monitor, and otherwise works on actual delivery/review events. The active local-four mapping and unchanged coordinator are in the backend goal; replaced chats remain read-only.

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

- Apply L1/L2/L3 in docs/RULE_TEST_WORKFLOW.md. L1 is focused development feedback. Every PR/main input runs L2: targeted/adjacent semantic assertions, the mechanism rule suite and both original full-game smokes; exact full-opening/cold-journal acceptance methods are declared in scripts/l3-rule-acceptance-methods.json and remain in nightly full. The required aggregate is `L2 required`; failed, cancelled, skipped, missing or wrong-SHA execution cannot pass. SQL/native/related-network and their aggregates are not L2 dependencies.
- Each line commits its own coherent package, opens a PR and may merge after the newest PR merge-result SHA passes L2. Use branch protection where available; otherwise mechanically verify the latest head/base/merge SHA and L2 immediately before merging. Do not force/reset. Integration is not the sole rule-package publisher; ordinary private helpers, complete packages and single expressions need no extra approval.
- Coordinate only actual same-method overlap. One main package per development line keeps work bounded; waiting for CI does not block an independent package. Known production-rule, hidden-information or recovery failures go once to the owning line with the minimal reproduction and assertion. Fix those in independent PRs; do not freeze unrelated packages or relabel all failures as tooling faults.
- Local focused/affected tests use --jobs 1 or 2 on this 10-core/24-GiB host. New heavy full/matrices/complete SQL/SignalR restart acceptance run in Actions. Minimal isolated failure debugging is allowed. Preserve existing N54/N55 fixed source/bin and databases; do not stop another line's process. No new cloud chat, idle polling Goal or Codex polling automation.
- `.github/workflows/backend-l2.yml` handles PR/main/manual L2. `.github/workflows/native-desktop.yml` handles one 02:00 Asia/Shanghai nightly L3 and explicit manual investigation. L3 fixes main once, then uses that complete SHA for full rules, all SQL partitions, SignalR, real process restart and original replay. Retain meaningful running candidates (including A7) as historical evidence; do not retarget/cancel/retry them merely because HEAD moved. Do not launch A8/A9 acceptance for each rule package.
- A failed nightly prevents a complete-acceptance claim and blocks changes related to the known defect; it does not block unrelated daily L2-green merges. Backend stage completion still requires applicable source/semantic scope and complete fixed-input L3 evidence. A green L2 is not whole-card or product certification.
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

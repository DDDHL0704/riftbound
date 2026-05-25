4D-03SU-E..4D-03SV-E non-payment FAQ / rule-source disposition audit

Status: DOC_MATRIX_CURRENT branch audit. Project remains **NOT READY**.

Preconditions

- Fresh GitHub clone `/Users/dinghaolin/MyProjects/riftbound-dotnet` is on `main` at `09d57f6f checkpoint: stage 4D recovery spectator temporary payment resources`.
- DOC_MATRIX worktree `/Users/dinghaolin/MyProjects/riftbound-dotnet-stage4d-matrix-docs-current` was created from that `main` baseline on branch `codex/stage4d-matrix-docs-current`.
- Shared board approved exactly this scope: `4D-03SU..03SV` docs/matrix-only FAQ / rule-source disposition for `FU-5cea85e7c3` 狂热粉丝 and `FU-422b450261` 雷克塞.
- The main clone shared board and the DOC_MATRIX worktree shared board were re-read before finalizing this bundle; no scope conflict was observed.

Decision

Proceed with the two approved rows because both rows already have:

- `stage4B.freezeStatus=IMPLEMENTED_TESTED`.
- Existing representative automated evidence.
- Existing FAQ / rule-source refs in the matrix.
- No remaining selected-row `NEEDS_ENGINE_SUPPORT` or `NEEDS_AUTOMATED_TEST_EVIDENCE` blocker.

Candidate evidence checks

| row | implementation evidence | automated/runtime evidence | FAQ/rules refs | blocker before | blocker after |
| --- | --- | --- | --- | --- | --- |
| `FU-5cea85e7c3` 狂热粉丝 | `FERVID_FAN_DEFENSE_TRIGGER_PLAY_UNIT` direct card behavior | `future-order-triggers-defense` representative evidence | `BREAK-JFAQ-260416 p4` | `NEEDS_FAQ_REVIEW` | none |
| `FU-422b450261` 雷克塞 | `SFD_170_REKSAI_ATTACK_REVEAL_PLAY_UNIT`; `SFD_170A_REKSAI_ATTACK_REVEAL_PLAY_UNIT` direct behavior | `3C-attack-trigger-boundary`, `future-order-triggers-pressure`, Stage 4C-51 representative guard evidence | `BREAK-JFAQ-260416 p3`; `SOUL-JFAQ-260114 p19`; `SOUL-OFAQ-260114 p4` | `NEEDS_FAQ_REVIEW` | none |

Count audit

- Functional-unit FAQ blockers: `174 -> 172`.
- Snapshot-entry FAQ blockers: `227 -> 224`.
- Non-payment FAQ blockers: `82 -> 80`.
- Payment-cost FAQ blockers remain `92`.
- Payment-or-targeting-stack-timing FAQ blockers: `124 -> 122`.
- Payment-and-targeting-stack-timing FAQ blockers remain `65`.
- Primary `freezeStatus=NEEDS_FAQ_REVIEW` remains `128`.
- `fullOfficialTrue=0`; `ready=false`.

Write scope

- Updated `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` only for the approved functional units / snapshot entries and a new `stage4D03Su03SvFaqRuleSourceDispositionBundle` metadata section.
- Updated `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs` only to synchronize current FAQ residual baselines for all-functional-units and payment-or-targeting-stack-timing queries.
- Added this audit and the paired evidence document.
- Runtime, frontend, protocol, official catalog, ordinary test implementation, Chrome/browser/formal E2E scripts, `fullOfficial`, final readiness status and `riftbound-dotnet.sln` remain locked.

Non-closure status

This bundle closes only two already-implemented, representative-tested, non-payment FAQ / rule-source blockers. It does not close payment-cost FAQ review, automated-evidence residuals, engine-support breadth, P0/P1, formal E2E, `fullOfficial`, or final readiness. Project remains **NOT READY**.

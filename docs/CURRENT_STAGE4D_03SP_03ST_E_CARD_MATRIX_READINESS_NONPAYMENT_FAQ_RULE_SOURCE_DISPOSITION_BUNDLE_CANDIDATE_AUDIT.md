4D-03SP-E..4D-03ST-E non-payment FAQ / rule-source disposition audit

Status: DOC_MATRIX_CURRENT branch audit. Project remains **NOT READY**.

Decision

Proceed with a five-row docs/matrix-only FAQ / rule-source disposition bundle because all selected rows already have:

- An existing implementation route in the matrix.
- Existing representative automated/runtime evidence in `stage4B.automatedTests` and prior stage overlays.
- Existing FAQ / rules refs in the matrix.
- No selected-row `NEEDS_ENGINE_SUPPORT` or `NEEDS_AUTOMATED_TEST_EVIDENCE` blocker.

Candidate evidence checks

| row | implementation evidence | automated/runtime evidence | FAQ/rules refs | blocker before | blocker after |
| --- | --- | --- | --- | --- | --- |
| `FU-441cb9fb7f` 海克斯射线 | `HEXTECH_RAY_DAMAGE_3` direct card behavior | stage4C72 and `REPRESENTATIVE_AUTOMATED_EVIDENCE_PRESENT` | BREAK/JFAQ/SOUL refs plus `CORE-260330 p59` | `NEEDS_FAQ_REVIEW` | none |
| `FU-bf81341dd2` 拉文布鲁姆学生 | `RAVENBLOOM_STUDENT_SPELL_TRIGGER_PLAY_UNIT` direct card behavior | stage4C1 order-trigger evidence and representative automated status | BREAK/JFAQ/SOUL refs | `NEEDS_FAQ_REVIEW` | none |
| `FU-6c99fc0e2e` 后巷酒吧 | `BATTLEFIELD_RULE_DOMAIN` representative | `3B-cleanup-control-faq-smoke`; prior 03SD accepted matrix evidence | `JFAQ-251023 p5`; `JFAQ-251023 p6` | `NEEDS_FAQ_REVIEW` | none |
| `FU-90673ef9fd` 劫掠船巷 | `BATTLEFIELD_RULE_DOMAIN` representative | `3B-battlefield-faq-smoke`; prior 03SE accepted matrix evidence | `JFAQ-251023 p5`; `JFAQ-251023 p6` | `NEEDS_FAQ_REVIEW` | none |
| `FU-67c6b0186e` 厄斐琉斯 | Aphelios shared oracle direct behavior | `future-order-triggers-aphelios`; prior 03SH accepted matrix evidence | SOUL J/O FAQ refs | `NEEDS_FAQ_REVIEW` | none |

Count audit

- Before: total FAQ blockers `179`; non-payment FAQ blockers `87`; payment-cost FAQ blockers `92`; payment-or-targeting FAQ blockers `128`.
- After: total FAQ blockers `174`; non-payment FAQ blockers `82`; payment-cost FAQ blockers `92`; payment-or-targeting FAQ blockers `124`.
- Unchanged: `NEEDS_ENGINE_SUPPORT=415`, payment-cost `NEEDS_ENGINE_SUPPORT=34`, implemented-tested `NEEDS_ENGINE_SUPPORT=0`, `NEEDS_AUTOMATED_TEST_EVIDENCE=328`, `fullOfficialTrue=0`, `ready=false`.

Audit-test synchronization

- `PaymentEngineCoverageAuditTests.cs` is touched only to adjust current live FAQ residual comparisons for the five non-payment FAQ dispositions:
  - all-functional-units FAQ count: `179 -> 174`
  - payment-or-targeting-stack-timing FAQ count: `128 -> 124`
  - payment-cost FAQ count remains `92`
  - payment-and-targeting FAQ count remains `65`
- Historical manifest evidence counts remain intact; only current-matrix residual comparison is adjusted.

Rules / FAQ index disposition

- No new official rule/FAQ source is introduced in this bundle.
- `docs/rules-evidence-index.md` does not need a source-index change because selected refs already exist in matrix evidence.

Validation plan

- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- matrix count script
- `git diff --check`
- conflict-marker scan over `docs` and `tests`
- focused `PaymentEngineCoverageAuditTests`
- `ConformanceFixtureRunnerTests`
- backend full `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` unless stopped by a validation failure

Validation result

- Matrix JSON parse: passed.
- Matrix count script: passed (`snapshotEntries=1009`, `functionalUnits=811`, `allFaq=174`, `paymentCostFaq=92`, `paymentOrTargetingFaq=124`, `fullOfficialTrue=0`, `ready=false`).
- `git diff --check`: passed.
- Conflict-marker scan over `docs` and `tests`: passed.
- `PaymentEngineCoverageAuditTests`: passed `697/697`.
- `ConformanceFixtureRunnerTests`: passed `3019/3019`.
- Backend full: passed `5344/5344`.
- Frontend / Chrome smoke: skipped because this bundle did not change frontend, browser, runtime assets or scripts.

Stop / non-READY audit

- No hidden-info leak evidence was introduced; this bundle changes only public matrix/document evidence.
- No runtime/frontend/protocol/official catalog change is included.
- No `fullOfficial=true` row is produced.
- No final readiness wording is emitted.
- Project remains **NOT READY** and still depends on Stage 4 total gate.

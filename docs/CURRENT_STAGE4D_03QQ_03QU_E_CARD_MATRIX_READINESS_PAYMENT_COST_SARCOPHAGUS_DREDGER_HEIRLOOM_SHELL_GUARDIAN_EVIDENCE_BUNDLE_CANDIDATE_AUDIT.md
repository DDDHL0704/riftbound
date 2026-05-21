# 4D-03QQ..03QU Candidate Audit

Status: audit companion for DOC_MATRIX source checkpoint `795ad8fe`. This audit verifies that 03QQ-03QU is a matrix/current-docs + payment audit-baseline sync only. Project remains **NOT READY**.

## Scope Audit

Allowed write scope:

- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- current checkpoint / completion / dispatch docs
- this candidate and audit document pair
- `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs` only for residual expected-count and manifest synchronization

Locked scope:

- `src/Riftbound.Engine/**`
- `src/Riftbound.Api/**`
- `src/Riftbound.DevUi/**`
- official snapshot data
- frontend/browser scripts
- protocol core field shape
- non-selected matrix rows
- general tests outside the payment audit baseline
- `fullOfficial`, final readiness flags and `riftbound-dotnet.sln`

Audit result: the 03QQ-03QU source checkpoint is within the allowed matrix/audit-baseline scope.

## Row Audit

| Stage | Row | Before | After | Closed | Still open |
|---|---|---|---|---|---|
| 4D-03QQ-E | `FU-f054818b88` / `UNL-148/219` / `CURSED_SARCOPHAGUS_PLAY_EQUIPMENT_BANISH_GRAVEYARD_UNITS` | `NEEDS_ENGINE_SUPPORT`, `NEEDS_AUTOMATED_TEST_EVIDENCE` | `NEEDS_AUTOMATED_TEST_EVIDENCE` | row-level engine support | activated branch, automated evidence disposition, hidden/control breadth |
| 4D-03QR-E | `FU-b829fb32b9` / `UNL-153/219` / `MUDDY_DREDGER_LAST_BREATH_WARHAWK_STATIC` | `NEEDS_ENGINE_SUPPORT`, `NEEDS_AUTOMATED_TEST_EVIDENCE` | `NEEDS_AUTOMATED_TEST_EVIDENCE` | row-level engine support | full Last Breath family, true stack multiplicity, cleanup/target-stack breadth |
| 4D-03QS-E | `FU-f856ad0504` / `UNL-158/219` / `SHEPHERDS_HEIRLOOM_WEAPON_EQUIPMENT_STATIC` | `NEEDS_ENGINE_SUPPORT`, `NEEDS_AUTOMATED_TEST_EVIDENCE` | `NEEDS_AUTOMATED_TEST_EVIDENCE` | row-level engine support | equipment lifecycle, layer/control breadth, automated evidence disposition |
| 4D-03QT-E | `FU-6637f983e6` / `UNL-161/219` / `SCRYING_SHELL_PLAY_EQUIPMENT_PREDICT` | `NEEDS_ENGINE_SUPPORT`, `NEEDS_AUTOMATED_TEST_EVIDENCE` | `NEEDS_AUTOMATED_TEST_EVIDENCE` | row-level engine support | Predict breadth, activated branch, hidden/layer/cleanup/target-stack breadth |
| 4D-03QU-E | `FU-9f03f538d4` / `UNL-162/219` / `STUNNING_GUARDIAN_PLAY_KEYWORD_UNIT` | `NEEDS_ENGINE_SUPPORT`, `NEEDS_AUTOMATED_TEST_EVIDENCE` | `NEEDS_AUTOMATED_TEST_EVIDENCE` | row-level engine support | experience-buff branch, battle/layer breadth, automated evidence disposition |

## Evidence Audit

Evidence used is existing-only:

- Cursed Sarcophagus: `CardBehaviorRegistry` direct behavior, `p2-preflight-play-cursed-sarcophagus-equipment-banish-graveyard-units`, `p4-play-cursed-sarcophagus-target-rejected`, and `docs/rules-evidence-index.md` rows.
- Muddy Dredger: `CardBehaviorRegistry`, `CoreRuleEngine` last-breath effect, Stage 4C-22 evidence/audit docs, `RealTriggerQueueTests`, `p2-preflight-play-muddy-dredger-mechanical-static`, and `docs/rules-evidence-index.md` rows.
- Shepherd's Heirloom: `CardBehaviorRegistry`, assemble handling in existing runtime, `p2-preflight-play-shepherds-heirloom-weapon-equipment`, `p4-play-shepherds-heirloom-target-rejected`, `p4-assemble-equipment-shepherds-heirloom-experience-attach`, and `docs/rules-evidence-index.md` rows.
- Scrying Shell: `CardBehaviorRegistry`, `p2-preflight-play-scrying-shell-equipment-predict-recycle`, `p2-preflight-play-scrying-shell-equipment-predict-no-recycle`, `p4-scrying-shell-predict-outside-top-card-rejected`, and `docs/rules-evidence-index.md` rows.
- Stunning Guardian: `CardBehaviorRegistry`, `p2-preflight-play-stunning-guardian-keyword-unit`, target-rejection inline coverage in `ConformanceFixtureRunnerTests`, and `docs/rules-evidence-index.md` row.

No live official-site fetch was used, and the fixed `1009` snapshot-entry / `811` functional-unit scope is unchanged.

## Count Audit

The 03QQ-03QU bundle synchronizes these residual counts:

- all FU `NEEDS_ENGINE_SUPPORT 466 -> 461`
- payment-cost `NEEDS_ENGINE_SUPPORT 68 -> 63`
- primary payment residual `33 -> 28`
- targeting-stack-timing `231 -> 229`
- cleanup-replacement-duration `183 -> 181`
- hidden-info-random-zone `156 -> 154`
- payment-or-targeting-stack-timing `255 -> 250`
- payment-and-targeting-stack-timing `44 -> 42`
- automated-test evidence `328 -> 328`
- FAQ review `92 -> 92`
- primary FAQ residual `61 -> 61`
- `fullOfficialTrue 0 -> 0`
- `ready false -> false`

These count movements match the 03QQ-03QU manifest continuity and the `PaymentEngineCoverageAuditTests` residual-baseline table.

## Not-Ready Audit

This batch is not a final-readiness signal. It leaves P0/P1 closure, full PaymentEngine/PAY_COST breadth, complete selected-card official breadth, automated evidence disposition, FAQ residuals, frontend build, Chrome smoke, formal E2E and `fullOfficial` open.

## Validation Audit

Recorded source validation:

- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`: passed
- `git diff --check`: passed
- conflict-marker scan: clean
- `PaymentEngineCoverageAuditTests`: passed `695/695`
- `ConformanceFixtureRunnerTests`: passed `3019/3019`
- backend full test: passed `5342/5342`

Frontend build / Chrome smoke were intentionally not run because the batch had no frontend, browser-script, runtime UI or protocol-surface changes.

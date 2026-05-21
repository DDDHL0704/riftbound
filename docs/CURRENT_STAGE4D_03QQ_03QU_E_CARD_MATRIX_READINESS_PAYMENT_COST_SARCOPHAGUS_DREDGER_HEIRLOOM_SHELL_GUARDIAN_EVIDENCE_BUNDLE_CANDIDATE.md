# 4D-03QQ..03QU E_CARD_MATRIX_READINESS Payment-Cost Sarcophagus/Dredger/Heirloom/Shell/Guardian Evidence Bundle Candidate

Status: DOC_MATRIX source checkpoint `795ad8fe` prepared the 03QQ-03QU matrix + `PaymentEngineCoverageAuditTests.cs` residual-baseline sync. This candidate closes only row-level `NEEDS_ENGINE_SUPPORT` for the selected functional units below. Project remains **NOT READY**.

## Selected Rows

| Stage | Functional unit | Card | Effect / oracle | Evidence basis |
|---|---|---|---|---|
| 4D-03QQ-E | `FU-f054818b88` | `UNL-148/219` 受诅咒的石棺 | `CURSED_SARCOPHAGUS_PLAY_EQUIPMENT_BANISH_GRAVEYARD_UNITS` | Existing runtime `CardBehaviorRegistry` direct behavior; fixtures `p2-preflight-play-cursed-sarcophagus-equipment-banish-graveyard-units` and `p4-play-cursed-sarcophagus-target-rejected`; rules evidence rows in `docs/rules-evidence-index.md`. |
| 4D-03QR-E | `FU-b829fb32b9` | `UNL-153/219` 腐泥疏浚工 | `MUDDY_DREDGER_LAST_BREATH_WARHAWK_STATIC` | Existing runtime card behavior and `CoreRuleEngine` Last Breath effect; Stage 4C-22 Muddy Dredger evidence/audit; fixture `p2-preflight-play-muddy-dredger-mechanical-static`; `RealTriggerQueueTests` cleanup trigger coverage; rules evidence rows in `docs/rules-evidence-index.md`. |
| 4D-03QS-E | `FU-f856ad0504` | `UNL-158/219` 牧人的传家宝 | `SHEPHERDS_HEIRLOOM_WEAPON_EQUIPMENT_STATIC` | Existing runtime `CardBehaviorRegistry` behavior and Shepherd's Heirloom assemble handling; fixtures `p2-preflight-play-shepherds-heirloom-weapon-equipment`, `p4-play-shepherds-heirloom-target-rejected`, and `p4-assemble-equipment-shepherds-heirloom-experience-attach`; rules evidence rows in `docs/rules-evidence-index.md`. |
| 4D-03QT-E | `FU-6637f983e6` | `UNL-161/219` 占卜贝壳 | `SCRYING_SHELL_PLAY_EQUIPMENT_PREDICT` | Existing runtime `CardBehaviorRegistry` behavior; fixtures `p2-preflight-play-scrying-shell-equipment-predict-recycle`, `p2-preflight-play-scrying-shell-equipment-predict-no-recycle`, and `p4-scrying-shell-predict-outside-top-card-rejected`; rules evidence rows in `docs/rules-evidence-index.md`. |
| 4D-03QU-E | `FU-9f03f538d4` | `UNL-162/219` 惊艳守护者 | `STUNNING_GUARDIAN_PLAY_KEYWORD_UNIT` | Existing runtime `CardBehaviorRegistry` behavior; fixture `p2-preflight-play-stunning-guardian-keyword-unit`; target-rejection inline coverage in `ConformanceFixtureRunnerTests`; rules evidence row in `docs/rules-evidence-index.md`. |

## Count Movement

Before this bundle, the accepted 03QL-03QP baseline was:

- all FU `NEEDS_ENGINE_SUPPORT`: `466`
- payment-cost `NEEDS_ENGINE_SUPPORT`: `68`
- primary payment residual: `33`
- targeting-stack-timing `NEEDS_ENGINE_SUPPORT`: `231`
- cleanup-replacement-duration `NEEDS_ENGINE_SUPPORT`: `183`
- hidden-info-random-zone `NEEDS_ENGINE_SUPPORT`: `156`
- payment-or-targeting-stack-timing `NEEDS_ENGINE_SUPPORT`: `255`
- payment-and-targeting-stack-timing `NEEDS_ENGINE_SUPPORT`: `44`

After this bundle:

- all FU `NEEDS_ENGINE_SUPPORT`: `461`
- payment-cost `NEEDS_ENGINE_SUPPORT`: `63`
- primary payment residual: `28`
- targeting-stack-timing `NEEDS_ENGINE_SUPPORT`: `229`
- cleanup-replacement-duration `NEEDS_ENGINE_SUPPORT`: `181`
- hidden-info-random-zone `NEEDS_ENGINE_SUPPORT`: `154`
- payment-or-targeting-stack-timing `NEEDS_ENGINE_SUPPORT`: `250`
- payment-and-targeting-stack-timing `NEEDS_ENGINE_SUPPORT`: `42`

Unchanged global gates:

- `NEEDS_AUTOMATED_TEST_EVIDENCE`: `328`
- `NEEDS_FAQ_REVIEW`: `92`
- primary FAQ residual: `61`
- `fullOfficialTrue=0`
- `ready=false`

## Closed Blockers

The only blocker removed from each selected row is row-level `NEEDS_ENGINE_SUPPORT`. The selected rows move from `NEEDS_ENGINE_SUPPORT` to `IMPLEMENTED_UNTESTED` in `stage4B.freezeStatus`, remove `NEEDS_ENGINE_SUPPORT` from `stage4B.statusFlags`, and retain `NEEDS_AUTOMATED_TEST_EVIDENCE` in `stage4B.fullOfficialBlockers`.

## Open Blockers

This bundle does not close automated-evidence disposition for the five selected rows. It also does not close full PaymentEngine/PAY_COST breadth, complete target/stack lifecycle breadth, complete cleanup/replacement-duration breadth, complete hidden-info/random-zone breadth, complete layer/continuous-effect breadth, complete battle/spell-duel breadth, formal browser/E2E evidence, `fullOfficial`, P0/P1 closure or final project readiness.

Card-specific residuals remain open:

- Cursed Sarcophagus: activated tap/destroy self and play banished unit branch remains open.
- Muddy Dredger: complete Last Breath family, true stack route and simultaneous destruction multiplicity remain open.
- Shepherd's Heirloom: equipment power modifier, attach/follow lifecycle breadth and complex destination branches remain open.
- Scrying Shell: broad Predict visibility/UI semantics and swift destroy/tap +2 power activated branch remain open.
- Stunning Guardian: experience-consumption self-buff branch, keyword battle/layer breadth and complete battle lifecycle remain open.

## Why This Is Not Ready

This is a matrix/audit-baseline synchronization slice only. It does not claim `fullOfficial`, does not change runtime, does not change frontend/browser behavior, does not touch official catalog data, and does not clear Stage 4 overall gates. The project remains governed by the Stage 4 master checkpoint and completion audit.

## Development Handoff

Development windows still need real implementation/test work for the residual categories above when A_MAIN decides those are in scope. This DOC_MATRIX slice should not be used as evidence that any selected card is fully official-complete; it only documents that existing runtime + existing tests + existing rules evidence are enough to remove the row-level engine-support blocker for this payment-cost matrix pass.

## Validation

The source checkpoint recorded:

- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- `git diff --check`
- conflict-marker scan
- `PaymentEngineCoverageAuditTests` passed `695/695`
- `ConformanceFixtureRunnerTests` passed `3019/3019`
- backend full test passed `5342/5342`

Frontend build / Chrome smoke were not run because this bundle changed no frontend, browser script, runtime UI, or protocol surface.

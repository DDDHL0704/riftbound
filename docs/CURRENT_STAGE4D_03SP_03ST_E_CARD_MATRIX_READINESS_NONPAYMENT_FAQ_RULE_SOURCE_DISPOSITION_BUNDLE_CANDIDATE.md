4D-03SP-E..4D-03ST-E E_CARD_MATRIX_READINESS non-payment FAQ / rule-source disposition bundle candidate

Status: branch-local DOC_MATRIX_CURRENT candidate. Project remains **NOT READY**.

Scope

- Worktree: `/Users/dinghaolin/MyProjects/riftbound-dotnet-stage4d-matrix-docs-current`
- A_MAIN authorization: shared board `2026-05-22 12:55 A_MAIN`, docs-only residual re-scan / 3-5 row bundle scope.
- Lane: non-payment `NEEDS_FAQ_REVIEW` rows with existing implementation, existing representative automated/runtime evidence, and existing FAQ / rules refs.
- Exhausted lanes skipped: implemented-tested `NEEDS_ENGINE_SUPPORT` lane and primary payment-cost B/D residual lane.
- Runtime/frontend/protocol/official catalog/fullOfficial/READY: unchanged and locked.

Selected rows

| stage | functionalUnit | card / collector | effect / oracle | implementation | automated evidence | FAQ / rules evidence |
| --- | --- | --- | --- | --- | --- | --- |
| 4D-03SP-E | `FU-441cb9fb7f` | `OGN·009/298` 海克斯射线 | `HEXTECH_RAY_DAMAGE_3` | `direct-card-behavior` | `OfficialDeckSubmitReadyAndMulliganFlowWorksThroughHub`; `official opening smoke`; stage4C72 representative Hextech Ray evidence | `BREAK-JFAQ-260416 p7`; `BREAK-JFAQ-260416 p9`; `JFAQ-251023 p7`; `SOUL-JFAQ-260114 p10`; `CORE-260330 p59` |
| 4D-03SQ-E | `FU-bf81341dd2` | `OGN·103/298` 拉文布鲁姆学生 | `RAVENBLOOM_STUDENT_SPELL_TRIGGER_PLAY_UNIT` | `direct-card-behavior` | `future-order-triggers-spell-trigger`; stage4C1 order-trigger representative evidence | `BREAK-JFAQ-260416 p10`; `JFAQ-251023 p2`; `SOUL-JFAQ-260114 p10`; `SOUL-JFAQ-260114 p15` |
| 4D-03SR-E | `FU-6c99fc0e2e` | `OGN·277/298` 后巷酒吧 | `BATTLEFIELD_RULE_DOMAIN` | `non-play-domain-representative` | `3B-cleanup-control-faq-smoke`; prior 03SD implemented-tested matrix evidence | `JFAQ-251023 p5`; `JFAQ-251023 p6` |
| 4D-03SS-E | `FU-90673ef9fd` | `OGN·285/298` 劫掠船巷 | `BATTLEFIELD_RULE_DOMAIN` | `non-play-domain-representative` | `3B-battlefield-faq-smoke`; prior 03SE implemented-tested matrix evidence | `JFAQ-251023 p5`; `JFAQ-251023 p6` |
| 4D-03ST-E | `FU-67c6b0186e` | `SFD·049/221` 厄斐琉斯 | `SFD_049_APHELIOS_WEAPON_TRIGGER_PLAY_UNIT`; `SFD_APHELIOS_PLAY_UNIT`; `SFD_APHELIOS_PROMO_PLAY_UNIT` | `direct-card-behavior` / shared oracle | `future-order-triggers-aphelios`; prior 03SH implemented-tested matrix evidence | `SOUL-JFAQ-260114 p21`; `SOUL-JFAQ-260114 p24`; `SOUL-OFAQ-260114 p19` |

Matrix blocker changes

- `NEEDS_FAQ_REVIEW` full-functional-unit blockers: `179 -> 174`.
- Non-payment `NEEDS_FAQ_REVIEW`: `87 -> 82`.
- Payment-cost `NEEDS_FAQ_REVIEW`: `92 -> 92`.
- Targeting-stack/timing `NEEDS_FAQ_REVIEW`: `101 -> 97`.
- Cleanup/replacement/duration `NEEDS_FAQ_REVIEW`: `59 -> 56`.
- Hidden-info/random-zone `NEEDS_FAQ_REVIEW`: `53 -> 53`.
- Payment-or-targeting-stack/timing `NEEDS_FAQ_REVIEW`: `128 -> 124`.
- Payment-and-targeting-stack/timing `NEEDS_FAQ_REVIEW`: `65 -> 65`.
- Primary freezeStatus `NEEDS_FAQ_REVIEW`: `128 -> 128`.
- `NEEDS_ENGINE_SUPPORT`: unchanged at `415`.
- Implemented-tested `NEEDS_ENGINE_SUPPORT`: unchanged at `0`.
- Payment-cost `NEEDS_ENGINE_SUPPORT`: unchanged at `34`.
- `NEEDS_AUTOMATED_TEST_EVIDENCE`: unchanged at `328`.
- `fullOfficialTrue`: `0 -> 0`.
- `ready`: `false -> false`.

Closed blockers

- Closed `NEEDS_FAQ_REVIEW` only for the five selected non-payment rows.
- Each selected row keeps `freezeStatus=IMPLEMENTED_TESTED`, keeps `fullOfficial=false`, removes `NEEDS_FAQ_REVIEW` from `statusFlags` / `fullOfficialBlockers`, and records `faqEvidenceStatus=FAQ_RULE_SOURCE_DISPOSITIONED_REPRESENTATIVE_EVIDENCE_ONLY`.

Still open

- Payment-cost FAQ residual remains `92`; primary payment-cost FAQ residual remains `61`.
- Global primary `freezeStatus=NEEDS_FAQ_REVIEW` remains `128`.
- Engine-support rows remain `415`; targeting/cleanup/hidden high-risk lanes still need runtime or stronger conformance evidence outside this FAQ-only bundle.
- Automated evidence residual remains `328`.
- Full official breadth, formal E2E, P0/P1, frontend/browser gates, `fullOfficial`, final readiness and READY remain open.

Why this is not READY

This bundle only dispositions five already-referenced FAQ/rule-source blockers for non-payment rows. It does not prove full official behavior for any row, does not close payment-cost FAQ review, does not close engine-support or automated-test-evidence residuals, and does not touch runtime/frontend/protocol/official catalog. Project status remains **NOT READY**.

Developer-window follow-up

- No runtime/test/frontend implementation is requested by this bundle.
- Real code gaps remain in engine-support rows, automated evidence residuals, complete PaymentEngine/PAY_COST breadth, targeting-stack/timing, cleanup/replacement/duration, hidden-info/random-zone, layer, battle/spell-duel and formal full-official coverage.

Validation

- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed.
- Matrix count script passed: 1009 snapshot entries, 811 functional units, `fullOfficialTrue=0`, `ready=false`, total FAQ `174`, payment-cost FAQ `92`, payment-or-targeting FAQ `124`.
- `git diff --check` passed.
- Conflict-marker scan over `docs` and `tests` passed.
- `PaymentEngineCoverageAuditTests` passed `697/697`.
- `ConformanceFixtureRunnerTests` passed `3019/3019`.
- Backend full `dotnet test Riftbound.slnx --no-restore` passed `5344/5344`.
- Frontend build / Chrome smoke skipped because no frontend/browser/runtime assets or scripts changed.

# Stage 4D-03BG PaymentEngine Card Matrix Alignment Row Manifest Audit

审计日期：2026-05-16
结论：**FOCUSED VERIFIER ACCEPTED / PROJECT NOT READY**

## 1. Scope

本批在 4D-03BF cross-window generation / consumption row manifest 后，继续拆解 4D-03BC 仍标记为 missing 的 `ROW_CARD_MATRIX_ALIGNMENT_MISSING`。目标是把 PaymentEngine official row 与 card matrix 的 ID 口径、代表性测试证据、fullOfficial gate、FAQ / rules source、frontend contract、JSON sync 和 blocker count 维度固定成可执行 manifest，防止后续以局部 representative evidence 误升级 card matrix 或 READY。

本批只修改 conformance test 与 docs，不修改 runtime、frontend、card matrix JSON，不升级 `fullOfficial=true`，不关闭 P0-005 或 READY。

## 2. Card Matrix Families

`CardMatrixAlignmentRowManifest` 固定 8 个 representative families：

| Family | Purpose |
| --- | --- |
| `MATRIX_ID_AND_STATUS_FIELDS` | cardId / collectorId / oracleId / effectId / status fields |
| `PAYMENT_ROW_TO_CARD_MATRIX_MAPPING` | PaymentEngine row schema to official card/effect rows |
| `REPRESENTATIVE_TEST_EVIDENCE_LINKS` | focused tests and audit docs as representative evidence only |
| `FULL_OFFICIAL_GATE_AND_COMPLETION_BLOCK` | fullOfficial flag, blockers and completion gate |
| `FAQ_RULE_SOURCE_TRACE` | rules / FAQ / errata source traceability |
| `FRONTEND_CONTRACT_AND_SNAPSHOT_TRACE` | server ActionPrompt / authoritative snapshot contract |
| `MATRIX_JSON_SYNC_AND_DRIFT_GUARD` | JSON, checkpoint and completion audit consistency |
| `DEFERRED_BLOCKER_AND_STATUS_COUNTS` | blocker/status-count visibility until full official closure |

每个 family 必须保留：

- `OfficialMatrixRowId = ROW_CARD_MATRIX_ALIGNMENT_MISSING`
- matrix scope
- representative surface
- prompt anchor
- command anchor
- audit anchor
- matrix anchor
- remaining official breadth
- NOT READY / P0-005 remains open closure status
- resolving docs anchors

## 3. Guards Added

新增 focused verifier 覆盖以下约束：

- required card matrix families exactly once
- every row links back to `ROW_CARD_MATRIX_ALIGNMENT_MISSING`
- prompt / command / audit / matrix / doc anchors are present
- matrix dimensions remain explicit: `cardId`, `collectorId`, `oracleId`, `effectId`, `fullOfficial`, prompt, command, audit, matrix, FAQ, `ActionPrompt`, snapshot, frontend, blocker, JSON and official status
- no row claims `FullOfficialRulePass`, `fullOfficial=true`, P0-005 closure or READY

## 4. Validation

通过的验证：

- Focused PaymentEngine coverage guard: 61/61 passed
- Adjacent PaymentEngine / resource skill / prompt / hub regression: 619/619 passed
- Backend full `dotnet test Riftbound.slnx --no-restore`: 4498/4498 passed

## 5. Remaining Risk

4D-03BG proves card matrix alignment row routing is visible and executable as a representative manifest. It does not update matrix JSON, does not prove full official card matrix closure, does not clear all `NEEDS_ENGINE_SUPPORT` / `NEEDS_FAQ_REVIEW` / shared-oracle rows, and does not replace frontend final validation or final completion audit.

项目仍 **NOT READY**；P0-005 remains open.

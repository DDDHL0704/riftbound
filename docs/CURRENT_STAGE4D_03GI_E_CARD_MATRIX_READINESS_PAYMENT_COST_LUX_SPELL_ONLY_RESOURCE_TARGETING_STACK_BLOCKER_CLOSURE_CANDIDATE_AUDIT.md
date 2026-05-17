# 4D-03GI-E Card Matrix Readiness Audit

## Decision

本审计记录 4D-03GI-E payment-cost Lux spell-only resource targeting-stack blocker closure candidate。它只把 `FU-97d6c39d73 / OGS·014/024 拉克丝 / OGS_014_LUX_TAP_SPELL_RESOURCE_PLAY_UNIT` 这一 direct-card-behavior 代表行从 `NEEDS_ENGINE_SUPPORT` 收窄到 `IMPLEMENTED_UNTESTED`，并保留 `NEEDS_AUTOMATED_TEST_EVIDENCE`、`fullOfficial=false` 与 `ready=false`。

## Why This Row

- 03GH 后只剩一个 exact `payment-cost,targeting-stack-timing` direct-card-behavior size=1 representative row still carrying `NEEDS_ENGINE_SUPPORT`。
- Lux 的 runtime/resource-skill lane 已在 4D-03CR 由服务端验收，包含 spell-only generated mana prompt、command revalidation、source exhaust、resource lifetime and rollback evidence。
- P2 preflight 仍覆盖 `p2-preflight-play-ogs-014-lux-tap-resource-static` 的基础单位打出路径，规则锚点包括 `CORE-260330 p9`。

## Guardrails

本批不改 runtime、frontend、Chrome/browser scripts、formal 18-step scripts、official catalog、非选中矩阵行、`fullOfficial` 或 READY。`riftbound-dotnet.sln` 继续保持未跟踪且不纳入提交。

## Expected Matrix Delta

- selected row freezeStatus: `NEEDS_ENGINE_SUPPORT` -> `IMPLEMENTED_UNTESTED`
- selected row statusFlags: `IMPLEMENTED_UNTESTED + NEEDS_ENGINE_SUPPORT` -> `IMPLEMENTED_UNTESTED`
- selected row fullOfficialBlockers: `NEEDS_ENGINE_SUPPORT + NEEDS_AUTOMATED_TEST_EVIDENCE` -> `NEEDS_AUTOMATED_TEST_EVIDENCE`
- payment-cost NEEDS_ENGINE_SUPPORT: 335 -> 334
- primary residual: 191 -> 190
- payment-or-targeting-stack-timing NEEDS_ENGINE_SUPPORT: 523 -> 522
- payment-and-targeting-stack-timing NEEDS_ENGINE_SUPPORT: 232 -> 231

## Open Work

- payment-cost blocker closure remains partially open
- `B/D_ENGINE_SUPPORT` payment-cost residual remains open
- `A_CONFORMANCE_AUTOMATED_TEST_EVIDENCE` payment-cost residual remains open
- `E_CARD_MATRIX_FAQ_REVIEW` payment-cost residual remains open
- `E_CARD_MATRIX_READINESS` remains open
- card matrix closure remains open
- P0/P1 and READY remain open

## Validation

- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed for this batch.
- focused `PaymentEngineCoverageAuditTests` 346/346 passed for this batch.
- current-head backend full `dotnet test Riftbound.slnx --no-restore` 4917/4917 passed for this batch.
- `git diff --check` passed for this batch.

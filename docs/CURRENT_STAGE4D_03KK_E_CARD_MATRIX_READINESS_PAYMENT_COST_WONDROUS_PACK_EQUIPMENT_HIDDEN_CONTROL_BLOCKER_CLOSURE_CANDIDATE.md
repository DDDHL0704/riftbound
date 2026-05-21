# 4D-03KK-E Card Matrix Readiness Payment-Cost Wondrous Pack Equipment Hidden-Control Blocker Closure Candidate

## Scope

This candidate records one E_CARD_MATRIX_READINESS row-level blocker reduction for `FU-b9e39d550d / OGN·181/298 / 奇妙行囊 / WONDROUS_PACK_PLAY_EQUIPMENT`.

It is a matrix, evidence and audit sync only. It does not change runtime, frontend, Chrome / browser scripts, protocol core fields, official catalog, fullOfficial status, final readiness status, or `riftbound-dotnet.sln`.

## Evidence

- `data/official/card-catalog.zh-CN.json` 固定官网快照包含 `OGN·181/298`《奇妙行囊》，费用 2，类型为装备，官方文本包含横置回手友方装备、单位或待命卡牌。
- `CardBehaviorRegistry` 已将 `OGN·181/298` 绑定到 `WONDROUS_PACK_PLAY_EQUIPMENT`，本批不改 runtime。
- `p2-preflight-play-wondrous-pack-equipment.fixture.json` 覆盖基础 2 费、0 目标入栈、结算后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象。
- `p4-play-wondrous-pack-target-rejected.fixture.json` 覆盖当前 0 目标装备打出路径携带显式目标时拒绝，且不支付费用、不移动手牌、不入场装备、不创建结算链。
- `docs/rules-evidence-index.md`、`docs/p2-rules-preflight.md`、`docs/CURRENT_P2_STATUS.md` 与 `docs/CURRENT_P4_STATUS.md` 已记录 accepted representative evidence 和 deferred activated-skill breadth。

## Matrix Impact

- `NEEDS_ENGINE_SUPPORT`: 230 -> 229.
- Primary residual: 158 -> 157.
- `payment-or-targeting-stack-timing` `NEEDS_ENGINE_SUPPORT`: 418 -> 417.
- `payment-and-targeting-stack-timing` `NEEDS_ENGINE_SUPPORT`: 141 -> 141.
- `NEEDS_AUTOMATED_TEST_EVIDENCE`: 328 -> 328.
- `NEEDS_FAQ_REVIEW`: 92 -> 92.
- Primary FAQ residual: 61 -> 61.
- `fullOfficialTrue`: 0 -> 0.
- `ready`: false -> false.

This candidate does not close Wondrous Pack automated evidence disposition, tap-return activated skill, equipment-control movement breadth, hidden-info / standby return breadth, complete PaymentEngine / PAY_COST matrix, formal 18-step E2E, or READY.

## Validation Results

Validation passed: matrix JSON valid; PaymentEngineCoverageAuditTests 560/560; Wondrous Pack focused 3021/3021; adjacent prompt/payment/equipment/target/stack 2145/2145; backend full 5131/5131; git diff --check passed.

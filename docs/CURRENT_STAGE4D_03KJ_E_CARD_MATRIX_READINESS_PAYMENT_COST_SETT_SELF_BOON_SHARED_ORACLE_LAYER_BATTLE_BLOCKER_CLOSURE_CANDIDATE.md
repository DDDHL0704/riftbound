# 4D-03KJ-E Card Matrix Readiness Payment-Cost Sett Self-Boon Shared-Oracle Layer/Battle Blocker Closure Candidate

## Scope

This candidate records one E_CARD_MATRIX_READINESS row-level blocker reduction for `FU-22ac86e3d6 / OGN·164/298 + OGN·164a/298 + SFD·232*/221 + SFD·232/221 / 瑟提 / OGN_SETT_ALT_A_PLAY_UNIT_GRANT_SELF_BOON;OGN_SETT_PLAY_UNIT_GRANT_SELF_BOON;SETT_PLAY_UNIT_GRANT_SELF_BOON;SETT_PROMO_PLAY_UNIT_GRANT_SELF_BOON`.

It is a matrix, evidence and audit sync only. It does not change runtime, frontend, Chrome / browser scripts, protocol core fields, official catalog, fullOfficial status, final readiness status, or `riftbound-dotnet.sln`.

## Evidence

- `data/official/card-catalog.zh-CN.json` 固定官网快照包含 `OGN·164/298`、`OGN·164a/298`、`SFD·232*/221` 与 `SFD·232/221` 《瑟提》，费用 5，战力 4，官方文本包含打出或征服战场时给予自身增益，以及消耗自身增益获得本回合 +4。
- `CardBehaviorRegistry` 已将四个 card no 绑定到 `OGN_SETT_ALT_A_PLAY_UNIT_GRANT_SELF_BOON`、`OGN_SETT_PLAY_UNIT_GRANT_SELF_BOON`、`SETT_PLAY_UNIT_GRANT_SELF_BOON` 与 `SETT_PROMO_PLAY_UNIT_GRANT_SELF_BOON`，本批不改 runtime。
- `p2-preflight-play-sett-self-boon.fixture.json`、`p2-preflight-play-sett-promo-self-boon.fixture.json`、`p2-preflight-play-ogn-sett-self-boon.fixture.json` 与 `p2-preflight-play-ogn-sett-alt-a-self-boon.fixture.json` 覆盖基础 5 费、0 目标入栈、结算后进入控制者基地成为 4 战力 `CARD_TYPE:UNIT`，并给予自身 `增益` 标签和永久 +1 战力。
- `docs/rules-evidence-index.md`、`docs/p2-rules-preflight.md` 与 `docs/CURRENT_P2_STATUS.md` 已记录 accepted representative evidence 和 deferred official breadth。

## Matrix Impact

- `NEEDS_ENGINE_SUPPORT`: 231 -> 230.
- Primary residual: 158 -> 158 because this row remains `SHARED_ORACLE_IMPLEMENTATION`.
- `payment-or-targeting-stack-timing` `NEEDS_ENGINE_SUPPORT`: 419 -> 418.
- `payment-and-targeting-stack-timing` `NEEDS_ENGINE_SUPPORT`: 141 -> 141.
- `NEEDS_AUTOMATED_TEST_EVIDENCE`: 328 -> 328.
- `NEEDS_FAQ_REVIEW`: 92 -> 92.
- Primary FAQ residual: 61 -> 61.
- `fullOfficialTrue`: 0 -> 0.
- `ready`: false -> false.

This candidate does not close Sett automated evidence disposition, conquest-trigger self-boon branch, consume-boon activated skill, battle / spell-duel lifecycle breadth, cleanup / replacement duration breadth, complete layer / continuous-effect breadth, complete PaymentEngine / PAY_COST matrix, formal 18-step E2E, or READY.

## Validation Results

Validation passed: matrix JSON valid; PaymentEngineCoverageAuditTests 558/558; Sett focused 3029/3029; adjacent prompt/payment/boon/layer/battle/stack 1266/1266; backend full 5129/5129; git diff --check passed.

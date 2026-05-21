# 4D-03KK-E Audit - Wondrous Pack Equipment Hidden Control

本审计记录 4D-03KK-E 对 `FU-b9e39d550d` / `OGN·181/298`《奇妙行囊》 / `WONDROUS_PACK_PLAY_EQUIPMENT` 的 row-level blocker closure candidate。结论限定为：已有基础打出、支付、0 目标、装备对象入场和显式目标拒绝代表路径足以移除一枚 `NEEDS_ENGINE_SUPPORT` blocker；完整 automated evidence disposition、横置回手激活技能、equipment-control movement breadth、hidden-info / standby return breadth 和 full PaymentEngine 仍 open。

## Source Evidence

- 官方目录行存在于 `data/official/card-catalog.zh-CN.json`，card no 为 `OGN·181/298`，card name 为《奇妙行囊》。
- runtime registry 已存在 `WONDROUS_PACK_PLAY_EQUIPMENT` 绑定。
- P2 preflight fixture 覆盖基础 2 费、0 目标入栈、双方让过、源牌进入控制者基地和 `CARD_TYPE:EQUIPMENT` 装备对象标签。
- P4 target-rejection fixture 覆盖当前打出路径携带显式目标时拒绝，且不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备、不创建结算链。
- `rules-evidence-index.md`、`p2-rules-preflight.md`、`CURRENT_P2_STATUS.md` 与 `CURRENT_P4_STATUS.md` 已将该 evidence 绑定到 catalog 与核心规则。

## Accepted Closure

- 仅接受 `NEEDS_ENGINE_SUPPORT 230 -> 229` 的行级 reduction。
- 仅接受 `primary residual 158 -> 157` 的 primary row count reduction。
- 仅接受 `payment-or-targeting-stack-timing 418 -> 417` 的派生 row count reduction。
- `payment-and-targeting-stack-timing` 保持 `141 -> 141`，因为该 row 不属于 `targeting-stack-timing`。
- `freezeStatus` 从 `NEEDS_ENGINE_SUPPORT` 变为 `IMPLEMENTED_UNTESTED`。
- `fullOfficialBlockers` 从 `NEEDS_ENGINE_SUPPORT+NEEDS_AUTOMATED_TEST_EVIDENCE` 变为 `NEEDS_AUTOMATED_TEST_EVIDENCE`。
- `statusFlags` 从 `IMPLEMENTED_UNTESTED+NEEDS_ENGINE_SUPPORT` 变为 `IMPLEMENTED_UNTESTED`。

## Rejected Closure

- 不关闭 automated evidence disposition。
- 不关闭横置回手激活技能。
- 不关闭 equipment-control movement breadth。
- 不关闭 hidden-info / standby return breadth。
- 不关闭 complete PaymentEngine / PAY_COST matrix。
- 不改变 fullOfficial 或 READY。

## Validation

Validation passed: matrix JSON valid; PaymentEngineCoverageAuditTests 560/560; Wondrous Pack focused 3021/3021; adjacent prompt/payment/equipment/target/stack 2145/2145; backend full 5131/5131; git diff --check passed.

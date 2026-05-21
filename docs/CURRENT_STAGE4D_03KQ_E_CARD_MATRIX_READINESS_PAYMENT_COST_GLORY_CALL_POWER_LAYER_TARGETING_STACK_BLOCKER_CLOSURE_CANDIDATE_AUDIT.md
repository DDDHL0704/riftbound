# 4D-03KQ-E Audit - Glory Call Power Layer Targeting-Stack

本审计记录 4D-03KQ-E 对 `FU-d2ae717e65` / `OGN·207/298`《荣耀召唤》 / `GLORY_CALL_POWER_PLUS_3` 的 row-level blocker closure candidate。结论限定为：已有手牌打出、支付 3、任意单位目标、入栈、双方让过、目标本回合内战力 +3 的代表路径，足以移除一枚 `NEEDS_ENGINE_SUPPORT` blocker；完整 automated evidence disposition、消耗增益额外费用无视法力费用分支、回合结束清理/替代持续时间、层/持续效果广度、完整 FEPR target / stack lifecycle 和 full PaymentEngine 仍 open。

## Evidence Checked

- 官方目录记录《荣耀召唤》为费用 3 的反应法术，打出时可选择消耗一个增益作为额外费用以无视本法术费用，并令一名单位本回合内战力 +3。
- `src/Riftbound.Engine/CardBehaviorRegistry.cs` 记录 `GLORY_CALL_POWER_PLUS_3`，费用 3，一个任意单位目标，战力修正 +3。
- `p2-preflight-play-glory-call-power-plus-3.fixture.json` 覆盖支付 3、入栈、双方让过、结算后目标单位 `UntilEndOfTurnPowerModifier=3` 且源法术入废牌堆。
- `docs/rules-evidence-index.md` 和 `docs/p2-rules-preflight.md` 已把该 fixture 绑定到官方目录、从手牌打出法术、选择目标、结算链与本回合内战力修正清理规则。

## Non-Closure

- 不声明消耗增益额外费用无视法力费用分支完成。
- 不声明本回合内修正的完整 cleanup / replacement duration 矩阵完成。
- 不声明完整 layer / continuous-effect 或 FEPR target / stack lifecycle 完成。
- 不升级 `fullOfficial`、不关闭 `E_CARD_MATRIX_READINESS`、不输出 READY。

## Validation

Validation passed: matrix JSON valid (jq empty); PaymentEngineCoverageAuditTests 572/572; Glory Call focused 3021/3021; adjacent prompt/payment/power/layer/target/stack 2191/2191; backend full 5143/5143; git diff --check passed.

# 4D-03KN-E Audit - Nocturne Roam Hidden/Layer/Control Targeting-Stack

本审计记录 4D-03KN-E 对 `FU-4db1229ebc` / `OGN·194/298`《魔腾》 / `NOCTURNE_ROAM_PLAY_UNIT` 的 row-level blocker closure candidate。结论限定为：已有基础手牌打出、支付、0 目标、单位对象入场、`游走` 标签和显式目标拒绝代表路径足以移除一枚 `NEEDS_ENGINE_SUPPORT` blocker；完整 automated evidence disposition、游走移动、牌堆顶部查看后的替代打出支付分支、hidden-info / random-zone breadth、layer / continuous-effect breadth、完整 FEPR target / stack lifecycle 和 full PaymentEngine 仍 open。

## Evidence

- 官方目录行存在于 `data/official/card-catalog.zh-CN.json`，card no 为 `OGN·194/298`，card name 为《魔腾》。
- 当前官方文本为 `{{游走}}（我可以向其他战场进行移动。）\n当你查看主牌堆顶部的卡牌（不是抽牌）并看到我时，可以选择支付{{A}}将我打出。`
- `src/Riftbound.Engine/CardBehaviorRegistry.cs` 记录 `NOCTURNE_ROAM_PLAY_UNIT`，费用 4，0 targets，并把源牌作为 4 战力、带 `游走` 标签的单位对象进入控制者基地。
- `p2-preflight-play-nocturne-roam-keyword-unit.fixture.json` 覆盖当前手牌打出英雄单位路径。
- `ConformanceFixtureRunnerTests.cs` 覆盖当前 0 目标路径携带显式目标时拒绝。

## Audit Decision

- 可以移除：`FU-4db1229ebc` 的 row-level `NEEDS_ENGINE_SUPPORT` blocker。
- 不可以移除：`NEEDS_AUTOMATED_TEST_EVIDENCE`。
- 不可以声明：`fullOfficial=true`、READY、完整游走移动、完整牌堆顶部查看替代打出、完整 hidden-info / random-zone、完整 layer / continuous-effect、完整 FEPR target / stack lifecycle。
- 不改 runtime、frontend、Chrome/browser script、official catalog、protocol core fields、formal 18-step E2E 或 final readiness flags。

## Validation

Validation passed: matrix JSON valid; PaymentEngineCoverageAuditTests 566/566; Nocturne focused 3021/3021; adjacent prompt/payment/roam/target/stack 1882/1882; backend full 5137/5137; git diff --check passed.

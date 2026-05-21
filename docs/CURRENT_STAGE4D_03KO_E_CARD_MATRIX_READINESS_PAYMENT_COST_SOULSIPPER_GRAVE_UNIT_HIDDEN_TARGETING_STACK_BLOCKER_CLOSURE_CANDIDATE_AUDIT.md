# 4D-03KO-E Audit - Soulsipper Grave-Unit Hidden/Targeting-Stack

本审计记录 4D-03KO-E 对 `FU-5f679644d4` / `OGN·196/298`《咂魂者》 / `OGN_SOULSIPPER_GRAVE_UNIT_PLAY_UNIT` 的 row-level blocker closure candidate。结论限定为：已有基础手牌打出、支付、0 目标、单位对象入场、`灵体` 标签和显式目标拒绝代表路径足以移除一枚 `NEEDS_ENGINE_SUPPORT` blocker；完整 automated evidence disposition、废牌堆单位选择、无视法力费用打出目的地、仍支付符能费用分支、hidden-info / random-zone breadth、完整 FEPR target / stack lifecycle 和 full PaymentEngine 仍 open。

## Evidence

- 官方目录行存在于 `data/official/card-catalog.zh-CN.json`，card no 为 `OGN·196/298`，card name 为《咂魂者》。
- 当前官方文本为 `当你打出我时，你可以选择从废牌堆中打出一名单位，无视其法力费用（仍需支付所有符能费用）。`
- `src/Riftbound.Engine/CardBehaviorRegistry.cs` 记录 `OGN_SOULSIPPER_GRAVE_UNIT_PLAY_UNIT`，费用 8，0 targets，并把源牌作为 5 战力、带 `灵体` 标签的单位对象进入控制者基地。
- `p2-preflight-play-ogn-soulsipper-spirit-static.fixture.json` 覆盖当前手牌打出单位路径。
- `ConformanceFixtureRunnerTests.cs` 覆盖当前 0 目标路径携带显式目标时拒绝。

## Audit Decision

- 可以移除：`FU-5f679644d4` 的 row-level `NEEDS_ENGINE_SUPPORT` blocker。
- 不可以移除：`NEEDS_AUTOMATED_TEST_EVIDENCE`。
- 不可以声明：`fullOfficial=true`、READY、完整废牌堆单位选择、完整免费打出目的地、完整符能费用分支、完整 hidden-info / random-zone、完整 FEPR target / stack lifecycle。
- 不改 runtime、frontend、Chrome/browser script、official catalog、protocol core fields、formal 18-step E2E 或 final readiness flags。

## Validation

Validation passed: matrix JSON valid; PaymentEngineCoverageAuditTests 568/568; Soulsipper focused 3021/3021; adjacent prompt/payment/hidden/target/stack 1895/1895; backend full 5139/5139; git diff --check passed.

# 4D-03KP-E Audit - Harrowing Grave-Unit Hidden

本审计记录 4D-03KP-E 对 `FU-c21b09595c` / `OGN·198/298`《蚀魂夜》 / `HARROWING_PLAY_GRAVEYARD_UNIT_TO_BASE` 的 row-level blocker closure candidate。结论限定为：已有手牌打出、支付 6、己方废牌堆单位目标、入栈、双方让过、目标单位打出到基地、非单位目标拒绝和墓地控制权护栏代表路径，足以移除一枚 `NEEDS_ENGINE_SUPPORT` blocker；完整 automated evidence disposition、完整目的地选择、仍支付符能费用分支、hidden-info / random-zone breadth 和 full PaymentEngine 仍 open。

## Evidence

- 官方目录行存在于 `data/official/card-catalog.zh-CN.json`，card no 为 `OGN·198/298`，card name 为《蚀魂夜》。
- 当前官方文本为 `从你的废牌堆中打出一名单位，无视其法力费用（仍需支付所有符能费用）。`
- `src/Riftbound.Engine/CardBehaviorRegistry.cs` 记录 `HARROWING_PLAY_GRAVEYARD_UNIT_TO_BASE`，费用 6，1 target，并把己方废牌堆单位打出到基地。
- `p2-preflight-play-harrowing-play-graveyard-unit-base.fixture.json` 覆盖当前代表路径。
- `ConformanceFixtureRunnerTests.cs` 覆盖当前代表路径、非单位废牌堆目标拒绝，以及脏控制权墓地目标不会被打出到当前玩家基地。

## Audit Decision

- 可以移除：`FU-c21b09595c` 的 row-level `NEEDS_ENGINE_SUPPORT` blocker。
- 不可以移除：`NEEDS_AUTOMATED_TEST_EVIDENCE`。
- 不可以声明：`fullOfficial=true`、READY、完整目的地选择、完整符能费用分支、完整 hidden-info / random-zone、完整 PaymentEngine。
- 不改 runtime、frontend、Chrome/browser script、official catalog、protocol core fields、formal 18-step E2E 或 final readiness flags。

## Validation

Validation passed: matrix JSON valid; PaymentEngineCoverageAuditTests 570/570; Harrowing focused 3021/3021; adjacent prompt/payment/graveyard/hidden/stack 733/733; backend full 5141/5141; git diff --check passed.

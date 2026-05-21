# 4D-03KL-E Audit - Siren Equipment Control

本审计记录 4D-03KL-E 对 `FU-071b942b0a` / `OGN·184/298`《塞壬号》 / `SIREN_PLAY_EQUIPMENT` 的 row-level blocker closure candidate。结论限定为：已有基础打出、支付、0 目标、装备对象入场和显式目标拒绝代表路径足以移除一枚 `NEEDS_ENGINE_SUPPORT` blocker；完整 automated evidence disposition、支付并横置移动技能、control-zone movement breadth 和 full PaymentEngine 仍 open。

## Evidence

- 官方目录行存在于 `data/official/card-catalog.zh-CN.json`，card no 为 `OGN·184/298`，card name 为《塞壬号》。
- 当前官方文本为 `支付{{1}}，{{横置}}：将战场上的一名友方单位移动到其所属的基地。`
- `src/Riftbound.Engine/CardBehaviorRegistry.cs` 记录 `SIREN_PLAY_EQUIPMENT`，费用 2，0 targets，并把源牌作为装备对象进入控制者基地。
- `p2-preflight-play-siren-equipment.fixture.json` 覆盖当前手牌打出装备路径。
- `p4-play-siren-target-rejected.fixture.json` 覆盖当前 0 目标路径携带显式目标时拒绝，且不支付费用、不移动手牌、不入场装备、不创建 stack item。

## Audit Decision

- 可以移除：`FU-071b942b0a` 的 row-level `NEEDS_ENGINE_SUPPORT` blocker。
- 不可以移除：`NEEDS_AUTOMATED_TEST_EVIDENCE`。
- 不可以声明：`fullOfficial=true`、READY、完整支付横置移动技能、完整 control-zone movement。
- 不改 runtime、frontend、Chrome/browser script、official catalog、protocol core fields、formal 18-step E2E 或 final readiness flags。

## Validation

Validation passed: matrix JSON valid; PaymentEngineCoverageAuditTests 562/562; Siren focused 3021/3021; adjacent prompt/payment/equipment/target/stack 2147/2147; backend full 5133/5133; git diff --check passed.

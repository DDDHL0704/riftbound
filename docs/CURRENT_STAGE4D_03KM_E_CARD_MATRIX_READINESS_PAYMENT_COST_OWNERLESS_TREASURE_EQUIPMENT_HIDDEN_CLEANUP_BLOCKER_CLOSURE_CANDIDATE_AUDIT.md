# 4D-03KM-E Audit - Ownerless Treasure Equipment Hidden/Cleanup

本审计记录 4D-03KM-E 对 `FU-c85e993e85` / `OGN·186/298`《无主宝藏》 / `OWNERLESS_TREASURE_PLAY_EQUIPMENT` 的 row-level blocker closure candidate。结论限定为：已有基础打出、支付、0 目标、装备对象入场和显式目标拒绝代表路径足以移除一枚 `NEEDS_ENGINE_SUPPORT` blocker；完整 automated evidence disposition、离场抽牌并召出休眠符文触发、支付并横置自毁技能、cleanup / replacement duration breadth、hidden-info / random-zone breadth 和 full PaymentEngine 仍 open。

## Evidence

- 官方目录行存在于 `data/official/card-catalog.zh-CN.json`，card no 为 `OGN·186/298`，card name 为《无主宝藏》。
- 当前官方文本为 `当此牌离场时，抽一张牌，然后召出一枚休眠的符文。支付{{紫色}}，{{横置}}：摧毁此牌。`
- `src/Riftbound.Engine/CardBehaviorRegistry.cs` 记录 `OWNERLESS_TREASURE_PLAY_EQUIPMENT`，费用 2，0 targets，并把源牌作为装备对象进入控制者基地。
- `p2-preflight-play-ownerless-treasure-equipment.fixture.json` 覆盖当前手牌打出装备路径。
- `p4-play-ownerless-treasure-target-rejected.fixture.json` 覆盖当前 0 目标路径携带显式目标时拒绝，且不支付费用、不移动手牌、不入场装备、不创建 stack item。

## Audit Decision

- 可以移除：`FU-c85e993e85` 的 row-level `NEEDS_ENGINE_SUPPORT` blocker。
- 不可以移除：`NEEDS_AUTOMATED_TEST_EVIDENCE`。
- 不可以声明：`fullOfficial=true`、READY、完整离场触发、完整支付横置自毁技能、完整 cleanup / replacement duration、完整 hidden-info / random-zone。
- 不改 runtime、frontend、Chrome/browser script、official catalog、protocol core fields、formal 18-step E2E 或 final readiness flags。

## Validation

Validation passed: matrix JSON valid; PaymentEngineCoverageAuditTests 564/564; Ownerless Treasure focused 3021/3021; adjacent prompt/payment/equipment/target/stack 2149/2149; backend full 5135/5135; git diff --check passed.

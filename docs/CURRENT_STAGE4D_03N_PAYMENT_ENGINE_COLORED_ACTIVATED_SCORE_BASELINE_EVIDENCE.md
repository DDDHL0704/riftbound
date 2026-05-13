# Stage 4D-03N PaymentEngine Colored Activated Score Baseline Evidence

日期：2026-05-14
结论：**BASELINE GREEN / IMPLEMENTATION NOT STARTED / PROJECT NOT READY**

本文记录 4D-03N 实现前的 A 侧测试基线。当前 HEAD 已完成 4D-03M Renata Glasc colored activated draw representative，但尚未实现 `SFD·088/221` / `SFD·088a/221` 烈娜塔 score activated ability。本基线只证明实现前 Renata / activated ability / payment 相邻路径绿色，可作为后续 B 侧实现验收对照。

## 1. Baseline Scope

当前已存在行为：

- Renata `SFD·088/221` / `SFD·088a/221` 普通手牌打出、0 目标入栈、结算后成为 4 战力英雄单位对象的 representative fixtures 通过。
- Renata 普通打出携带显式目标会 rejected no-mutation。
- 4D-03M 已实现 Renata draw `ACTIVATE_ABILITY`：支付 1 mana + 1 blue typed power，创建普通 stack item，pass-pass 后抽 1 张；两张 collector 均通过 source alias 支持。
- `P4ActivatedAbilityCatalog.GetDeferredSurfaces()` 仍记录 `DEFERRED_PAY_4_BLUE4_EXHAUST_SCORE_1`，并用官方文本锚点 `支付{{4}}和{{蓝色}}{{蓝色}}{{蓝色}}{{蓝色}}，{{横置}}：获得1分` 审计。
- Vi、Xerath、Malzahar、Dragon Soul Sage 与 Renata draw executable `ACTIVATE_ABILITY` representative 当前绿色。
- 4D-03K 后 Malzahar temporary payment resource pending / inline consumption 当前绿色；4D-03M 已固定 temporary resource 不可支付 Renata typed blue draw cost。

当前未实现行为：

- Renata score `ACTIVATE_ABILITY` prompt source requirement。
- Renata score command success path：支付 4 mana + 4 blue typed power，横置 source，创建普通 stack item。
- Renata score stack resolution：双方让过后获得 1 分，并沿用既有胜利分语义。
- score skill 的 typed blue payment-resource quote / commit parity、exhaust-as-cost no-mutation 与 `TEMP_PAYMENT_RESOURCE:*` typed-cost rejection。

## 2. Focused Baseline

命令：

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Renata|FullyQualifiedName~ActivateAbility|FullyQualifiedName~ResourceSkill|FullyQualifiedName~PaymentEngineUnificationTests"
```

结果：

```text
Passed! - Failed: 0, Passed: 163, Skipped: 0, Total: 163
```

覆盖含义：

- Renata 普通入场、4D-03M draw skill 与 score deferred surface 代表路径绿色。
- 已执行 Vi、Xerath、Malzahar、Dragon Soul Sage `ACTIVATE_ABILITY` representative 当前绿色。
- `PaymentEngineUnificationTests` 当前绿色。
- 该基线不证明 Renata score ability 已实现；它只固定实现前状态。

## 3. Adjacent Baseline

命令：

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Renata|FullyQualifiedName~ActivateAbility|FullyQualifiedName~ResourceSkill|FullyQualifiedName~PaymentResource|FullyQualifiedName~SpendPower|FullyQualifiedName~RunePool|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub"
```

结果：

```text
Passed! - Failed: 0, Passed: 335, Skipped: 0, Total: 335
```

覆盖含义：

- Activated ability、payment resource、typed spend power、rune pool、ActionPrompt 与 GameHub 相邻路径当前绿色。
- 该基线适合作为 4D-03N 实现后的 regression 对照。

## 4. Worktree Note

`riftbound-dotnet.sln` 仍是既有未跟踪文件；本基线未读取、未修改、未暂存。

## 5. Verdict

4D-03N 可以进入 B 侧实现交接。实现前风险集中在 4 mana + 4 blue typed-power cost modeling、source exhaust-as-cost rollback、prompt quote parity、ordinary activated ability stack resolution、score / winning-score lifecycle、functional reprint support、temporary payment-only resource typed-cost rejection 与现有 Renata draw / Vi / Xerath / Malzahar / Dragon Soul Sage 回归边界；若 B 只能完成 prompt 或 command 的部分路径，必须在审计中保留 explicit residual risk，不能关闭 P0-005。

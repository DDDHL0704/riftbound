# Stage 4D-03M PaymentEngine Colored Activated Draw Baseline Evidence

日期：2026-05-14
结论：**BASELINE GREEN / IMPLEMENTATION NOT STARTED / PROJECT NOT READY**

本文记录 4D-03M 实现前的 A 侧测试基线。当前 HEAD 已完成 4D-03L Dragon Soul Sage reaction resource skill representative，但尚未实现 `SFD·088/221` / `SFD·088a/221` 烈娜塔抽牌 activated ability。本基线只证明实现前 Renata / activated ability / payment 相邻路径绿色，可作为后续 B 侧实现验收对照。

## 1. Baseline Scope

当前已存在行为：

- Renata `SFD·088/221` / `SFD·088a/221` 普通手牌打出、0 目标入栈、结算后成为 4 战力英雄单位对象的 representative fixtures 通过。
- Renata 普通打出携带显式目标会 rejected no-mutation。
- `P4ActivatedAbilityCatalog.GetDeferredSurfaces()` 仍记录 `DEFERRED_PAY_1_BLUE_DRAW_1`，并用官方文本锚点 `支付{{1}}和{{蓝色}}：抽一张牌` 审计。
- Vi、Xerath、Malzahar、Dragon Soul Sage 四条 executable activated ability representative 当前绿色。
- 4D-03K 后 Malzahar temporary payment resource pending / inline consumption 当前绿色。

当前未实现行为：

- Renata draw `ACTIVATE_ABILITY` prompt source requirement。
- Renata draw command success path：支付 1 mana + 1 blue typed power，创建普通 stack item。
- Renata draw stack resolution：双方让过后抽 1 张。
- typed blue payment-resource quote / commit parity、wrong-trait no-mutation 与 `TEMP_PAYMENT_RESOURCE:*` typed-cost rejection。

## 2. Focused Baseline

命令：

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Renata|FullyQualifiedName~ActivateAbility|FullyQualifiedName~ResourceSkill|FullyQualifiedName~PaymentEngineUnificationTests"
```

结果：

```text
Passed! - Failed: 0, Passed: 144, Skipped: 0, Total: 144
```

覆盖含义：

- Renata 当前普通入场 / deferred activated surface 代表路径绿色。
- 已执行 Vi、Xerath、Malzahar、Dragon Soul Sage `ACTIVATE_ABILITY` representative 当前绿色。
- `PaymentEngineUnificationTests` 当前绿色。
- 该基线不证明 Renata draw ability 已实现；它只固定实现前状态。

## 3. Adjacent Baseline

命令：

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Renata|FullyQualifiedName~ActivateAbility|FullyQualifiedName~ResourceSkill|FullyQualifiedName~PaymentResource|FullyQualifiedName~SpendPower|FullyQualifiedName~RunePool|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub"
```

结果：

```text
Passed! - Failed: 0, Passed: 316, Skipped: 0, Total: 316
```

覆盖含义：

- Activated ability、payment resource、typed spend power、rune pool、ActionPrompt 与 GameHub 相邻路径当前绿色。
- 该基线适合作为 4D-03M 实现后的 regression 对照。

## 4. Tooling Note

A 曾并发启动 focused 与 adjacent `dotnet test`，adjacent 构建进程因共享 `obj/Debug/net10.0/ref/Riftbound.Engine.dll` 输出文件产生一次 MSB3883 file-lock error。随后 A 在 focused 完成后单独重跑 adjacent baseline，结果为 316/316 通过；该 file-lock 不计为规则或测试失败。

## 5. Worktree Note

`riftbound-dotnet.sln` 仍是既有未跟踪文件；本基线未读取、未修改、未暂存。

## 6. Verdict

4D-03M 可以进入 B 侧实现交接。实现前风险集中在 colored typed-power cost modeling、prompt quote parity、ordinary activated ability stack resolution、draw hidden-info lifecycle、functional reprint support、temporary payment-only resource typed-cost rejection 与现有 Vi / Xerath / Malzahar / Dragon Soul Sage 回归边界；若 B 只能完成 prompt 或 command 的部分路径，必须在审计中保留 explicit residual risk，不能关闭 P0-005。

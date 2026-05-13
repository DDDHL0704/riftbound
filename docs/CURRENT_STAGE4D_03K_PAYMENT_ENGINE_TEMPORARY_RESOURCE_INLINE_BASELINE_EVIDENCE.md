# Stage 4D-03K PaymentEngine Temporary Resource Inline Baseline Evidence

日期：2026-05-14
结论：**BASELINE GREEN / IMPLEMENTATION NOT STARTED / PROJECT NOT READY**

本文记录 4D-03K 实现前的 A 侧测试基线。当前 HEAD 已完成 4D-03J temporary payment-only ledger 与 pending `PAY_COST` 消费 representative，但尚未实现 `PLAY_CARD` / `ACTIVATE_ABILITY` / `ASSEMBLE_EQUIPMENT` inline payment-window temporary resource consumption。本基线只证明实现前 Malzahar、inline payment、ActionPrompt 与 GameHub 相邻路径绿色，可作为后续 B 侧实现验收对照。

## 1. Baseline Scope

当前已存在行为：

- Malzahar open-main 与 spell-duel focus resource skill representative 通过。
- Malzahar 成功后创建 temporary payment-only resource ledger。
- pending `PAY_COST` 可通过 `TEMP_PAYMENT_RESOURCE:*` 消费该 ledger 支付 generic rune power cost。
- `RECYCLE_RUNE:*` payment resource actions 已覆盖 `PLAY_CARD`、`ACTIVATE_ABILITY`、`ASSEMBLE_EQUIPMENT` 等代表路径。

当前未实现行为：

- `PLAY_CARD` source requirements / payment metadata 暴露 `TEMP_PAYMENT_RESOURCE:*` inline 候选。
- `ACTIVATE_ABILITY` inline payment 可提交 temporary resource action。
- `ASSEMBLE_EQUIPMENT` inline payment 可提交 temporary resource action。
- inline shared helper 对 stale / wrong-owner / duplicate / unnecessary / typed shortfall / mana-only temporary resource action 的统一 no-mutation guard。

## 2. Focused Baseline

命令：

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Malzahar|FullyQualifiedName~TemporaryPaymentResource|FullyQualifiedName~PaymentEngineUnificationTests|FullyQualifiedName~PaymentResource|FullyQualifiedName~PlayCard|FullyQualifiedName~ActivateAbility|FullyQualifiedName~AssembleEquipment"
```

结果：

```text
Passed! - Failed: 0, Passed: 331, Skipped: 0, Total: 331
```

覆盖含义：

- 4D-03J Malzahar lifecycle 与 pending temporary resource representative 当前绿色。
- `PLAY_CARD`、`ACTIVATE_ABILITY`、`ASSEMBLE_EQUIPMENT` 与 `PaymentEngineUnificationTests` 相邻支付路径当前绿色。
- 该基线不证明 inline temporary resource consumption 已实现；它只固定实现前状态。

## 3. Adjacent Baseline

命令：

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PlayCard|FullyQualifiedName~ActivateAbility|FullyQualifiedName~AssembleEquipment|FullyQualifiedName~HideCard|FullyQualifiedName~LegendAct|FullyQualifiedName~PaymentResource|FullyQualifiedName~SpendPower|FullyQualifiedName~RunePool|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub"
```

结果：

```text
Passed! - Failed: 0, Passed: 526, Skipped: 0, Total: 526
```

覆盖含义：

- Inline payment windows、non-play payment windows、ActionPrompt 与 GameHub prompt surfaces 当前绿色。
- 该基线适合作为 4D-03K 实现后的 regression 对照。

## 4. Worktree Note

`riftbound-dotnet.sln` 仍是既有未跟踪文件；本基线未读取、未修改、未暂存。

## 5. Verdict

4D-03K 可以进入 B 侧实现交接。实现前风险集中在 temporary payment-only resource 与 inline payment commit 的 quote / parse / authorize / consume / cleanup 一致性；若 B 只能完成部分 inline window，必须在审计中保留 explicit residual risk，不能关闭 P0-005。

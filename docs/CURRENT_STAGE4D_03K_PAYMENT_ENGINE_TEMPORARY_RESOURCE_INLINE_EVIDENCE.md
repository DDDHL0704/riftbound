# Stage 4D-03K PaymentEngine Temporary Resource Inline Evidence

日期：2026-05-14
结论：**EVIDENCE GREEN / PROJECT NOT READY**

本文记录 4D-03K focused slice 的测试证据。该证据只证明 Malzahar temporary payment-only generic rune resource 已在 `PLAY_CARD`、`ACTIVATE_ABILITY` 与 `ASSEMBLE_EQUIPMENT` representative inline payment windows 中通过服务端 prompt、命令、commit、cleanup 与审计 payload；不关闭 P0-005 full official PaymentEngine。

## 1. Implementation Evidence

代码证据：

- `MatchSession` 在 play / activate / any-power assemble source metadata 中新增 temporary payment resource choices，并保留 per-choice `paymentOnly`、resource id、power、allowed kind 与 restriction metadata。
- `MatchSession` 的 generic shortfall quote 会拒绝 typed shortfall；对 damage-by-paid-power play cards，会把 temporary resource 纳入 optional generic power ceiling。
- `CoreRuleEngine` 新增 inline payment resource parser，统一拆分 behavior optional costs、`RECYCLE_RUNE:*` actions 与 `TEMP_PAYMENT_RESOURCE:*` actions。
- `CoreRuleEngine` 在 `ResolvePlayCard`、`ResolveAssembleEquipment`、Vi / Xerath `ResolveActivateAbility` 中于 shared payment plan commit 前应用 temporary resource contribution，并在同一 transaction 内扣费、清理 ledger、输出 spent / cleared audit events。
- `PaymentEngineUnificationTests` 覆盖 play quote / commit、play optional power ceiling、insufficient temp resource no-mutation、assemble any-power quote / commit、typed assemble rejection、Vi mixed recycle + temporary quote / commit、invalid temporary action no-mutation 和 Xerath Spellshield mana-tax rejection。
- `ConformanceFixtureRunnerTests` 将 assemble recycle payment resource audit expectation 对齐到 behavior optional costs 与 payment resource actions 分离的 server contract。

## 2. Focused Regression

命令：

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Malzahar|FullyQualifiedName~TemporaryPaymentResource|FullyQualifiedName~PaymentEngineUnificationTests|FullyQualifiedName~PaymentResource|FullyQualifiedName~PlayCard|FullyQualifiedName~ActivateAbility|FullyQualifiedName~AssembleEquipment"
```

结果：

```text
Passed! - Failed: 0, Passed: 344, Skipped: 0, Total: 344
```

覆盖含义：

- Malzahar temporary ledger 与 pending `PAY_COST` 既有 lifecycle 仍绿色。
- `PLAY_CARD`、`ACTIVATE_ABILITY`、`ASSEMBLE_EQUIPMENT` inline temporary resource quote / command / audit representative 通过。
- stale / wrong-owner / duplicate / wrong-kind / unnecessary / insufficient / typed / mana-tax negative guards 均保持 no-mutation。

## 3. Adjacent Regression

命令：

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PlayCard|FullyQualifiedName~ActivateAbility|FullyQualifiedName~AssembleEquipment|FullyQualifiedName~HideCard|FullyQualifiedName~LegendAct|FullyQualifiedName~PaymentResource|FullyQualifiedName~SpendPower|FullyQualifiedName~RunePool|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub"
```

结果：

```text
Passed! - Failed: 0, Passed: 539, Skipped: 0, Total: 539
```

覆盖含义：

- PlayCard、ActivateAbility、AssembleEquipment、HideCard、LegendAct、PaymentResource、SpendPower、RunePool、ActionPrompt 与 GameHub 相邻路径未被 4D-03K 破坏。
- Existing recycle rune payment resource prompt / command / audit behavior remains compatible with temporary resource additions.

## 4. Backend Full

命令：

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

结果：

```text
Passed! - Failed: 0, Passed: 3860, Skipped: 0, Total: 3860
```

## 5. Diff Check

命令：

```sh
git diff --check
```

结果：无输出。

## 6. Worktree Note

`riftbound-dotnet.sln` 仍是既有未跟踪文件，本切片未修改、未暂存。B worker Raman 的 draft 由 A 接管复核；A 完成补测、修复、全量验证与文档收口。

## 7. Verdict

4D-03K evidence green。该证据接受 temporary resource inline representative，但不接受完整 PaymentEngine、完整 resource skill family、所有 official payment windows、reaction/counter full target-filter model 或 final READY audit。项目仍 **NOT READY**。

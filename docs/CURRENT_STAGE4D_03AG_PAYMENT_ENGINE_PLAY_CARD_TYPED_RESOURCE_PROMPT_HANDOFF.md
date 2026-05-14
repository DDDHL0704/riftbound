# Stage 4D-03AG PaymentEngine PLAY_CARD Typed Resource Prompt Handoff

日期：2026-05-14
结论：**HANDOFF READY / PROJECT NOT READY**

本文件把 4D-03AF 后续的下一批 P0-005 PaymentEngine 工作收窄为一个 concrete prompt / command parity gap：`PLAY_CARD` typed optional power costs 的 inline payment-resource prompt metadata 需要与 command side payability 对齐。

## 背景

4D-03AF A 侧审计建议补 action-window coverage verifier。随后 Raman 只读审计指出一个更具体、优先级更高的服务端 authoritative prompt gap：`PLAY_CARD` command path 已能按 typed cost 校验 `RECYCLE_RUNE:*` / `TEMP_PAYMENT_RESOURCE:*`，但 prompt quote 仍可能暴露 wrong-trait resource 或漏报 typed temporary resource。

本切片仍只收窄 P0-005，不关闭 full official PaymentEngine，不进入 LayerEngine / frontend / coverage matrix。

## Current Code Facts

- `CoreRuleEngine.TryBuildPlayCardPlan` 会通过 `TryBuildOptionalCostPlan` 汇总 `extraPowerCost` 与 `extraPowerCostByTrait`，并把 inline temporary payment window 交给 `TryApplyTemporaryPaymentResourcesToPendingPayment`，所以 command side 对 typed payment resource 已较严格。
- `MatchSession.PlayCardPaymentResourceChoicesForBehavior` 当前只要判定需要 payment resource，就把基地中所有可回收 rune 暴露为 `RECYCLE_RUNE:*`，没有按具体 typed optional cost 判断该 rune 是否能补足。
- 同一方法对 temporary resource 只传入 `PlayCardTemporaryPaymentGenericPowerCost(...)` 与空 typed trait map，因此 typed temporary payment resource 不会为 typed optional cost 正确 quote。
- `MatchSession.PlayCardPaymentResourcePowerByTraitForBehavior` 与 `PlayCardPaymentResourcePowerByChoiceForBehavior` 基于上述 choices 输出 metadata，因此可能让 wrong-trait rune 抬高 prompt 的 `availablePowerByTraitWithPaymentResources` / `paymentResourcePowerByChoice`。
- 现有代表卡包含 `OGN·044` green optional draw、`UNL-028` red optional ready、`SFD·013` red optional target effect、`SFD·067` blue optional target effect，以及多张 typed Haste cards。

## Required Behavior

- Prompt side must only quote payment resource actions that can help at least one legal `PLAY_CARD` optional power cost for that source / mode.
- `RECYCLE_RUNE:*` choices must be filtered by typed requirement when the currently relevant optional cost is typed.
- `TEMP_PAYMENT_RESOURCE:*` choices must be quoted for typed `PLAY_CARD` optional costs when the temporary resource can satisfy the typed requirement.
- Wrong-trait recycle rune or temporary resource must not inflate `availablePowerWithPaymentResources`, `availablePowerByTraitWithPaymentResources`, or `paymentResourcePowerByChoice`.
- Generic optional power payment behavior from 4D-03K must stay intact.
- Command side rejection / rollback behavior must remain unchanged unless a new test proves a command-side mismatch.

## Suggested Write Scope

- `src/Riftbound.Engine/MatchSession.cs`
- `tests/Riftbound.ConformanceTests/PaymentEngineUnificationTests.cs`
- Optional only if needed for hub prompt regression: `tests/Riftbound.ConformanceTests/GameHubJoinTests.cs`

Avoid changing `CoreRuleEngine.cs` unless a new regression test proves command side is inconsistent.

## Expected Tests

Add focused tests that prove:

- `PLAY_CARD` typed optional cost prompt quotes a matching typed temporary resource.
- wrong-trait `RECYCLE_RUNE:*` is not exposed or counted for a typed optional cost.
- wrong-trait `TEMP_PAYMENT_RESOURCE:*` is not exposed or counted for a typed optional cost.
- successful command with matching typed temporary resource still commits and emits existing audit fields.
- wrong-trait command rejection remains no-mutation.
- existing generic `PLAY_CARD` temporary resource prompt / command tests remain green.

Recommended focused command:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PaymentEngineUnificationTests|FullyQualifiedName~PlayCard|FullyQualifiedName~Haste|FullyQualifiedName~PaymentResource|FullyQualifiedName~TemporaryPaymentResource|FullyQualifiedName~RunePool|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub"
```

After focused green, run backend full before A acceptance:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

## No-Go

- Do not implement the broader action-window coverage verifier in this slice.
- Do not add new resource-skill cards or expand `[A]` / `[C]` families.
- Do not change frontend code or protocol fields.
- Do not update `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`.
- Do not treat representative green tests as P0-005 full official closure.
- Do not mark READY.

## Acceptance

A acceptance requires focused tests, backend full test, `git diff --check`, staged diff review, and an implementation evidence / audit document that states the project remains **NOT READY**.

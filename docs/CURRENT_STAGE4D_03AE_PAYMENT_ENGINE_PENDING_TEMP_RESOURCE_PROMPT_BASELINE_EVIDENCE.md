# Stage 4D-03AE PaymentEngine Pending Temporary Resource Prompt Baseline Evidence

日期：2026-05-14
结论：**BASELINE READY / PROJECT NOT READY**

本文件记录 4D-03AE 实现前基线。当前代码已通过 4D-03AD 支持 SFD Fiora `TRIGGER_PAYMENT` 消费 legal typed temporary resources，但 pending payment prompt metadata 的 aggregate available-power 口径仍需要修正；本基线只证明相邻 trigger payment、PaymentEngine、temporary resource 与 prompt 回归在实现前为绿色。

## Baseline Findings

- SFD Fiora `TRIGGER_PAYMENT` currently supports legal typed-yellow temporary payment resource command consumption.
- Pending payment metadata already exposes `paymentResourceChoices`, `paymentResourcePowerByChoice`, and typed resource contribution.
- Code audit shows `availablePowerWithPaymentResources` can double count legal typed temporary resources because `PendingPaymentResourcePowerByTrait` already includes them and the aggregate also adds `TemporaryPaymentResourcePower`.
- Code audit shows wrong-trait temporary resources can inflate `availablePowerWithPaymentResources` because `TemporaryPaymentResourcePower` does not filter by `TemporaryPaymentResourceCanHelpPowerCost`.
- Command legality remains stricter than the aggregate metadata, so this is a prompt metadata parity slice, not a command behavior expansion.

## Validation Commands

Focused trigger / payment / temporary resource / prompt baseline:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~TriggerPaymentTests|FullyQualifiedName~PaymentEngineUnificationTests|FullyQualifiedName~PayCost|FullyQualifiedName~ActionPrompt|FullyQualifiedName~TemporaryPaymentResource|FullyQualifiedName~RunePool"
```

Result: passed 167/167.

Diff hygiene:

```sh
git diff --check
```

Result: passed.

## Verdict

4D-03AE is ready for implementation as a narrow pending payment temporary resource prompt parity slice. The project remains **NOT READY**.

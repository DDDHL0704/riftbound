# Stage 4D-03AG PaymentEngine PLAY_CARD Typed Resource Prompt Baseline Evidence

日期：2026-05-14
结论：**BASELINE READY / PROJECT NOT READY**

本文件记录 4D-03AG 实现前基线。该基线只证明当前 PaymentEngine / `PLAY_CARD` / Haste / resource prompt 邻近测试在修改前为绿色；它不证明 typed resource prompt gap 已修复，也不关闭 P0-005。

## Baseline Findings

- `PLAY_CARD` command side already routes optional typed power costs through shared `PaymentPlan` and inline temporary payment handling.
- Prompt side still needs a narrower legal-choice filter for `PLAY_CARD` typed optional power payment resources.
- 4D-03AG is authorized as a narrow server prompt parity slice, not a runtime behavior expansion.

## Validation Commands

Focused PaymentEngine / PLAY_CARD / prompt adjacent baseline:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PaymentEngineUnificationTests|FullyQualifiedName~PlayCard|FullyQualifiedName~Haste|FullyQualifiedName~PaymentResource|FullyQualifiedName~TemporaryPaymentResource|FullyQualifiedName~RunePool|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub"
```

Result: passed 450/450.

Diff hygiene:

```sh
git diff --check
```

Result: passed.

## Verdict

4D-03AG is ready to hand off for implementation after focused baseline completes. The project remains **NOT READY**.

# Stage 4D-03AG PaymentEngine PLAY_CARD Typed Resource Prompt Audit

日期：2026-05-14
结论：**FOCUSED SLICE ACCEPTED / PROJECT NOT READY**

本文件记录 A 侧对 4D-03AG implementation diff 的验收。该切片只收口 `PLAY_CARD` typed optional power costs 的 inline payment-resource prompt parity，不关闭 P0-005 full official，不进入 frontend / LayerEngine / coverage matrix。

## Scope Accepted

- `PLAY_CARD` prompt now derives unmet power payment requirements before quoting payment resource actions.
- `RECYCLE_RUNE:*` choices are filtered through `PlayCardRecycleRuneCanHelpPowerPayment`, so typed optional costs no longer expose wrong-trait recycle rune choices when those choices cannot help a legal payment requirement.
- `TEMP_PAYMENT_RESOURCE:*` choices now use the same generic + typed requirement shape, so matching typed temporary payment resources can be quoted for typed `PLAY_CARD` optional power costs.
- `paymentResourcePowerByChoice`, `availablePowerWithPaymentResources`, and `availablePowerByTraitWithPaymentResources` are now based on the legal quoted choices rather than every recyclable rune in base.
- Existing generic `PLAY_CARD` temporary resource behavior remains covered by prior tests.

## Prompt / Command Parity Evidence

- New focused tests prove `OGN·044/298` Tiny Guardian green optional draw prompt quotes a matching green typed temporary resource.
- New focused tests prove wrong-trait red recycle / temporary resources are not quoted or counted for that green optional cost.
- New focused tests prove matching green typed temporary resource command commit spends the temporary resource and emits `COST_PAID` typed power audit metadata.
- New focused tests keep wrong-trait typed temporary resource command rejection no-mutation.
- Hub Haste prompt regression now expects only the matching purple rune payment resource for a purple Haste ready cost.

## A Review Notes

- `CoreRuleEngine.cs` was not changed; command-side typed payment authorization remains on the existing `PaymentPlan` / inline temporary payment path.
- Five unused `Needs*PaymentResource` helpers left by the refactor were removed after A review.
- Existing current implemented `PLAY_CARD` typed optional power costs in `CardBehaviorRegistry` are 1-power requirements; multi-resource typed prompt combinations remain outside this representative slice and full official P0-005 closure.

## Remaining Risk

- This is representative prompt parity, not an exhaustive generated PaymentEngine action-window verifier.
- Complete P0-005 still requires action-window coverage / full-card matrix reconciliation and broader P0/P1 closure.
- Project remains **NOT READY**.

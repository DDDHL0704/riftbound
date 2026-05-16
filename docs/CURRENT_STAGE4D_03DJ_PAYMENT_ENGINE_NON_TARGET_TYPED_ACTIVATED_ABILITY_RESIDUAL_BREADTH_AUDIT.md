# 4D-03DJ PaymentEngine Non-Target/Typed Activated Ability Residual Breadth Audit

日期：2026-05-16
结论：**ACCEPTED AS A-SIDE TEST/DOCS-ONLY DISPATCH GUARD / PROJECT NOT READY**

## 1. Scope

4D-03DJ 是 03DI 之后的 A-side focused dispatch guard。它不实现 runtime，不修改 frontend、Chrome / browser scripts、formal 18-step scripts、card matrix JSON、`fullOfficial` / READY，也不触碰 `riftbound-dotnet.sln`。

本批只把 03DH 留下的 non-target/typed activated ability residual partition 收窄为下一枚 concrete B-side gate：`B_PAYMENT_ENGINE_OFFICIAL_BREADTH_NON_TARGET_TYPED_ACTIVATED_ABILITY_RESIDUAL_VERIFIER`。

## 2. Dispatch Shape

`PaymentEngineCoverageAuditTests` 新增：

- `NonTargetTypedActivatedAbilityResidualBreadthDispatchManifest`：复用 `NonTargetTypedActivatedAbilityResidualPartitionManifest`，只包含 Vi `PAY_2_RED_DOUBLE_POWER` 与 Fluft Poro `FLUFT_PORO_EXHAUST_CREATE_TWO_SPELLSHIELD_WARHAWKS` 两行。
- `PaymentEngineNonTargetTypedActivatedAbilityResidualBreadthDispatchRoutesViAndFluftPoroOnly`：固定 03DJ 只派发这两个 residual rows，并要求 future B 证明 prompt / Command / audit / outcome / lifetime / rollback / card-row trace。
- `PaymentEngineOfficialBreadthGateRecords03DJAsNonTargetTypedResidualDispatchOnly`：把 03DJ 写入 `B_PAYMENT_ENGINE_OFFICIAL_BREADTH` gate，同时保持 03DH、03DG、03DE、03DD 等历史 evidence 只能作为 representative evidence。

## 3. Future B Boundary

Future B implementation / verifier must stay inside:

- Vi no-target paid activated ability：open-main prompt、2 mana + red power quote、Command revalidation、stack item、power-doubling until end of turn、rollback / no-mutation and source-card/card-row parity。
- Fluft Poro no-target battlefield-only token activated ability：ready battlefield source prompt、exhaust-as-cost Command revalidation、ordinary stack before token creation、two `UNL·T02` Warhawk tokens with Spellshield in controller base、rollback / no-mutation and source-card/card-row parity。

## 4. Non-Closure Boundary

4D-03DJ is dispatch evidence only:

- P0-005 remains open。
- P1 remains open。
- full official PaymentEngine matrix remains open。
- full target-bearing / typed / experience / Spellshield-tax activated ability official family remains open。
- full non-target/typed activated ability residual breadth remains open until future B verifier is implemented and accepted。
- full-card matrix remains open。
- `fullOfficial` remains false。
- card matrix JSON remains unchanged。
- Chrome smoke and formal 18-step reruns remain last-known evidence only。
- READY upgrade remains forbidden。

## 5. Validation

Validation:

```txt
focused PaymentEngineCoverageAuditTests=184/184
git diff --check=passed
Chrome smoke=not required; no frontend/runtime/browser script changes
formal 18-step=not required; no frontend/runtime/formal script changes
```

This batch is accepted only as a focused dispatch / conformance / docs slice; it does not close P0/P1 or final readiness.

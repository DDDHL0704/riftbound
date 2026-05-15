# Stage 4D-03BA PaymentEngine Official Matrix Residual Manifest Audit

日期：2026-05-16
结论：**FOCUSED VERIFIER ACCEPTED / PROJECT NOT READY**

本切片补强 server-side conformance verifier，把 4D-03AV 的 `OFFICIAL_PAYMENT_ENGINE_MATRIX` residual blocker 拆成 axis-level executable manifest。它不修改 runtime、不修改前端、不修改 card matrix、不升级 fullOfficial，也不关闭 P0-005、P1 或 READY。

## Scope

写入范围：

- `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`
- `docs/CURRENT_STAGE4D_03BA_PAYMENT_ENGINE_OFFICIAL_MATRIX_RESIDUAL_MANIFEST_AUDIT.md`
- `docs/CURRENT_STAGE4D_03BA_PAYMENT_ENGINE_OFFICIAL_MATRIX_RESIDUAL_MANIFEST_EVIDENCE.md`
- A-side checkpoint / completion / server-rule / closure-plan / dispatch docs

No-go：

- 不改 `src/Riftbound.Engine` runtime。
- 不改 `ActionPrompt` contracts、API、前端、Chrome smoke、formal E2E 或 card matrix JSON。
- 不触碰 `riftbound-dotnet.sln`。
- 不把 representative manifests 解释为完整 official PaymentEngine matrix closure。

## Manifest Policy

`OfficialPaymentEngineMatrixResidualManifest` 现在锁定 12 个 remaining-official-gap axes：

| Axis | Required future verifier |
| --- | --- |
| `ACTION_WINDOWS` | 每个 playable payment action window 与未来官方窗口必须有 prompt / command / audit / rollback matrix |
| `PAYMENT_SOURCES` | mana、generic power、typed power、experience、recycle、generated 与 temporary source 必须跨窗口枚举 |
| `RESOURCE_SKILLS` | `[A]` / `[C]` resource skill family、generated resource type、timing permission 与 payment-only use 必须完整证明 |
| `TARGET_TAXES` | target tax、dependency target、target count、controller 与 stale legality branch 必须完整证明 |
| `KEYWORD_BRANCHES` | Haste、Echo、Spellshield、experience、replacement、modifier、optional / extra / alternative 与 temporary parity 必须跨窗口证明 |
| `COST_MODIFIERS` | cost reduction、increase、minimum rule、modifier stacking 与 modified quote branch 必须完整证明 |
| `OPTIONAL_EXTRA_ALTERNATIVE_COSTS` | accept / decline、target-scoped option、mixed branch 与 invalid branch 必须完整证明 |
| `REPLACEMENT_PREVENTION` | payment-adjacent replacement / prevention / declined replacement / ordering branch 必须完整证明 |
| `RESOURCE_ACTIONS` | legend、battlefield、trigger 与未来 resource-action payment windows 必须完整证明 |
| `ROLLBACK_FAILURE_BRANCHES` | every illegal payment command、stale prompt、stale command、invalid resource 与 no-effect branch 必须证明 no-mutation |
| `CROSS_WINDOW_GENERATION_CONSUMPTION` | generated resource creation、restriction、spend、expiry、cleanup 与 invalid reuse 必须跨窗口证明 |
| `CARD_MATRIX_ALIGNMENT` | 每个 official card row 必须能追到 prompt shape、command path、audit event、rollback guard 与 docs |

## Guardrails

- Manifest axes 必须 exactly once。
- 每个 axis 必须继续是 `remaining-official-gap`。
- 每个 axis 必须保留 representative evidence、future verifier、prompt / command / audit expectation、no-mutation rollback、remaining official breadth、doc anchors 与 NOT READY closure。
- Manifest 必须继续回连 4D-03AV `OFFICIAL_PAYMENT_ENGINE_MATRIX` residual blocker。
- Combined breadth 必须显式保留 action-window、payment source、resource skill、target tax、keyword、cost modifier、optional、extra、alternative、replacement、resource action、no-mutation、cross-window 与 card matrix residuals。
- Manifest 必须继续写明 `NOT READY` 与 `P0-005 remains open`。
- Manifest 不得出现 `FullOfficialRulePass` 或 `fullOfficial=true`。

## Validation

- Focused PaymentEngine coverage guard: **39 / 39 passed**.
- Adjacent PaymentEngine / resource skill / prompt / hub regression: **597 / 597 passed**.
- Backend full: **4476 / 4476 passed**.
- `git diff --check`: passed.

## Verdict

4D-03BA 把 P0-005 的 broad `OFFICIAL_PAYMENT_ENGINE_MATRIX` gap 从一句 blocker 拆成 12 个可回归 axis-level guard。它改善后续 routing 与回归可见性，但不证明完整 PaymentEngine official matrix、完整 `[A]` / `[C]` resource skill official family、target / keyword / replacement / rollback / card matrix breadth、P0/P1、frontend final validation、card matrix full-official coverage 或 READY。项目仍 **NOT READY**。

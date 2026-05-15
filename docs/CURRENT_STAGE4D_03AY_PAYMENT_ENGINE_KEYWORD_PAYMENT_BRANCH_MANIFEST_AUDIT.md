# Stage 4D-03AY PaymentEngine Keyword Payment Branch Manifest Audit

日期：2026-05-16
结论：**FOCUSED VERIFIER ACCEPTED / PROJECT NOT READY**

本切片新增 server-side conformance verifier，把 4D-03AV 中 `KEYWORD_PAYMENT_BRANCHES` residual family 拆成更细的 executable manifest。它不修改 runtime、不修改前端、不修改 card matrix、不升级 fullOfficial，也不关闭 P0-005、P1 或 READY。

## Scope

写入范围：

- `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`
- `docs/CURRENT_STAGE4D_03AY_PAYMENT_ENGINE_KEYWORD_PAYMENT_BRANCH_MANIFEST_AUDIT.md`
- `docs/CURRENT_STAGE4D_03AY_PAYMENT_ENGINE_KEYWORD_PAYMENT_BRANCH_MANIFEST_EVIDENCE.md`
- A-side checkpoint / completion / server-rule / closure-plan / dispatch docs

No-go：

- 不改 `src/Riftbound.Engine` runtime。
- 不改 `ActionPrompt` contracts、API、前端、Chrome smoke、formal E2E 或 card matrix JSON。
- 不触碰 `riftbound-dotnet.sln`。
- 不把 verifier 通过解释为 full official keyword PaymentEngine closure。

## Manifest Policy

`PaymentEngineKeywordPaymentBranchManifest*` tests 精确锁定 8 个 keyword payment branches：

| Branch | Representative scope | Residual |
| --- | --- | --- |
| `HASTE_READY` | Registry/profile-bound HASTE_READY play fixtures plus Rek'Sai red exactness | 完整 Haste official breadth、non-hand granting、strong/overflow interactions 与 all-window parity 未关闭 |
| `ECHO_OPTIONAL_PAYMENT` | Selected Echo / optional payment prompt, commit and audit representatives | 完整 Echo timing、repeated spell windows、replacement ordering 与 all-window optional payment parity 未关闭 |
| `SPELLSHIELD_TARGET_TAX` | Xerath、Crimson Rose、Shadow target tax representatives | 完整 Spellshield official tax breadth、cross-window target taxes、dependency targets 与 all-window audit parity 未关闭 |
| `EXPERIENCE_PAYMENT` | Crimson Rose ready-unit experience payment and adjacent tax interactions | 完整 experience-payment family、experience generation、prevention ordering 与 all-window parity 未关闭 |
| `BATTLEFIELD_REPLACEMENT_COSTS` | Battlefield held payment, Brush context and score-prevention-adjacent representatives | 完整 battlefield replacement ordering、prevention variants、response timing 与 cross-window resource generation 未关闭 |
| `COST_MODIFIER_PAYMENTS` | Selected reduction, increase, minimum-cost and equipment/battlefield modifier branches | 完整 cost reduction / increase / minimum replacement ordering and stacking 未关闭 |
| `OPTIONAL_EXTRA_ALTERNATIVE_COSTS` | Play optional, hide-card alternative, equipment optional/extra and Azir reattach branches | 完整 optional / extra / alternative target-scoped branch 与 all-window payment parity 未关闭 |
| `TEMPORARY_RESOURCE_PARITY` | Play, activated ability, battlefield held, trigger payment and pending PAY_COST temporary-resource windows | 完整 temporary-resource official matrix、cross-window generation/consumption ordering 与 invalid-resource failures 未关闭 |

## Guardrails

- Manifest branches 必须 exactly once。
- 每个 entry 必须保留 prompt、command、`COST_PAID` audit、no-mutation rollback anchors。
- Manifest 必须继续回连 4D-03AV residual blocker family `KEYWORD_PAYMENT_BRANCHES`，且该 family 仍是 `remaining-official-gap`。
- Remaining official breadth 必须显式保留 Haste、Echo、Spellshield、experience、replacement、optional、extra、temporary-resource 与 all-window parity。
- Manifest 必须继续写明 `NOT READY` 与 `P0-005 remains open`。
- Manifest 不得出现 `FullOfficialRulePass`、`fullOfficial=true` 或 READY closure。

## Validation

- Focused PaymentEngine coverage guard: **32 / 32 passed**.
- Adjacent PaymentEngine / prompt / hub / keyword regression: **590 / 590 passed**.
- Backend full: **4469 / 4469 passed**.
- `git diff --check`: passed.

## Verdict

4D-03AY 把 4D-03AV 中的 `KEYWORD_PAYMENT_BRANCHES` residual family 进一步拆成 executable branch-level manifest。它改善后续 routing 与回归可见性，但不证明完整 keyword payment family、all-window quote-command-audit parity、full PaymentEngine official matrix、P0/P1、frontend final validation、card matrix full-official coverage 或 READY。项目仍 **NOT READY**。

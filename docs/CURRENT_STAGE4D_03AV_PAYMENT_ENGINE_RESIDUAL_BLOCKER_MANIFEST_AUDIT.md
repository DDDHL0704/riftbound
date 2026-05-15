# Stage 4D-03AV PaymentEngine Residual Blocker Manifest Audit

日期：2026-05-16
结论：**FOCUSED VERIFIER ACCEPTED / PROJECT NOT READY**

本切片新增 server-side conformance verifier，按 4D-03AU handoff 把 P0-005 PaymentEngine 剩余官方 blocker families 固定为 test-guarded manifest。它不修改 runtime、不修改前端、不修改 card matrix、不升级 fullOfficial，也不关闭 P0-005、P1 或 READY。

## Scope

写入范围：

- `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`
- `docs/CURRENT_STAGE4D_03AV_PAYMENT_ENGINE_RESIDUAL_BLOCKER_MANIFEST_AUDIT.md`
- `docs/CURRENT_STAGE4D_03AV_PAYMENT_ENGINE_RESIDUAL_BLOCKER_MANIFEST_EVIDENCE.md`
- A-side checkpoint / completion / server-rule / closure-plan / dispatch docs

No-go：

- 不改 `src/Riftbound.Engine` runtime。
- 不改 `ActionPrompt` contracts、API、前端、Chrome smoke、formal E2E 或 card matrix JSON。
- 不触碰 `riftbound-dotnet.sln`。
- 不把 verifier 通过解释为 full official PaymentEngine closure。

## Manifest Policy

`PaymentEngineResidualBlockerManifest*` tests 现在要求 residual blocker manifest 精确列出 6 个 family：

| Family | Classification | Why it remains open |
| --- | --- | --- |
| `OFFICIAL_PAYMENT_ENGINE_MATRIX` | `remaining-official-gap` | 缺完整官方 action window / payment source / modifier / rollback / failure-branch matrix。 |
| `RESOURCE_SKILL_A_C_FAMILY` | `catalog-bound-representative` | 当前 19 个 catalog resource skill representatives 已绑定，但完整 `[A]` / `[C]` family 未证明。 |
| `TARGET_BEARING_COLORED_ACTIVATED_ABILITIES` | `covered-representative` | Azir / Maduli / Ezreal / Renata / Crimson Rose / Shadow 等为代表性覆盖，不等于官方 family breadth。 |
| `LEGEND_BATTLEFIELD_TRIGGER_RESOURCE_ACTIONS` | `covered-representative` | `LEGEND_ACT`、battlefield held、trigger payment 有代表路径，完整 legend / battlefield / trigger resource breadth 未关闭。 |
| `KEYWORD_PAYMENT_BRANCHES` | `remaining-official-gap` | Haste / Echo / Spellshield / experience / replacement / cost modifier / optional-extra branches 仍未 all-window official closure。 |
| `MOVE_UNIT_MOVEMENT_PERMISSION_POLICY` | `policy-deferred` | 当前仍是 movement-permission / optional-cost policy；若未来官方 movement 支付出现，必须重新接入 PaymentEngine parity。 |

## Guardrails

- Manifest family 必须 exactly once。
- Classification 只允许 `catalog-bound-representative`、`covered-representative`、`remaining-official-gap`、`policy-deferred`。
- 每个 family 必须保留 current evidence、existing representative evidence、missing official breadth、no-mutation rollback expectation、doc anchors 和 NOT READY closure。
- `remaining-official-gap` 必须显式包含 official matrix 与 keyword payment branches。
- Combined missing breadth 必须继续提到 `[A]`、`[C]`、target、Haste、Echo、Spellshield、`LEGEND_ACT`。
- Manifest 不得出现 `FullOfficialRulePass`、`fullOfficial=true` 或 READY closure。

## Validation

- Focused PaymentEngine coverage guard: **18 / 18 passed**.
- Adjacent payment / prompt / hub / keyword regression: **576 / 576 passed**.
- Backend full: **4455 / 4455 passed**.
- `git diff --check`: passed.

## Verdict

4D-03AV turns the remaining P0-005 PaymentEngine official blocker families into an executable verifier. It improves audit discipline and future routing, but it does not prove full PaymentEngine official closure, P0/P1 completion, frontend final validation, card matrix full-official coverage, or READY. Project remains **NOT READY**.

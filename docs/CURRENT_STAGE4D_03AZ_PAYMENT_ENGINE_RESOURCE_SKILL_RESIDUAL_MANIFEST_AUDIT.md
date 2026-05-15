# Stage 4D-03AZ PaymentEngine Resource Skill Residual Manifest Audit

日期：2026-05-16
结论：**FOCUSED VERIFIER ACCEPTED / PROJECT NOT READY**

本切片补强 server-side conformance verifier，把 4D-03AL 的 catalog-bound resource skill coverage 明确回连到 4D-03AV 的 `RESOURCE_SKILL_A_C_FAMILY` residual blocker。它不修改 runtime、不修改前端、不修改 card matrix、不升级 fullOfficial，也不关闭 P0-005、P1 或 READY。

## Scope

写入范围：

- `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`
- `docs/CURRENT_STAGE4D_03AZ_PAYMENT_ENGINE_RESOURCE_SKILL_RESIDUAL_MANIFEST_AUDIT.md`
- `docs/CURRENT_STAGE4D_03AZ_PAYMENT_ENGINE_RESOURCE_SKILL_RESIDUAL_MANIFEST_EVIDENCE.md`
- A-side checkpoint / completion / server-rule / closure-plan / dispatch docs

No-go：

- 不改 `src/Riftbound.Engine` runtime。
- 不改 `ActionPrompt` contracts、API、前端、Chrome smoke、formal E2E 或 card matrix JSON。
- 不触碰 `riftbound-dotnet.sln`。
- 不把 19 个 current catalog representatives 解释为完整 `[A]` / `[C]` official family closure。

## Manifest Policy

`PaymentEngineResourceSkillCoverageManifest*` tests 继续锁定当前 6 个 representative families / 19 个 catalog `IsResourceSkill=true` ability ids：

| Family | Representative scope | Residual |
| --- | --- | --- |
| Malzahar target-as-cost payment-only resource skill | target-as-cost、generated power、payment-only restriction、spell-duel focus timing | 完整 target-as-cost official breadth 与 cross-window payment-only use 未关闭 |
| Dragon Soul Sage reaction mana resource skill | reaction-speed source、generated mana、no-target shape | 完整 reaction resource skill timing 与 all-window use 未关闭 |
| SFD Sigil typed payment-only resource skill family | six SFD typed temporary payment-only Sigil resources | 完整 typed-resource family、trait edge cases 与 cross-window payment parity 未关闭 |
| OGN Sigil typed payment-only resource skill family | six OGN reprint typed temporary payment-only Sigil resources | 完整 reprint parity、typed-resource family 与 cross-window payment parity 未关闭 |
| Resource conversion equipment skill family | Energy Channel / Ancient Stele / Hextech Anomaly conversion choices | 完整 conversion ordering、invalid conversion breadth 与 all-window resource conversion parity 未关闭 |
| Gold token payment-only resource skill family | UNL / SFD Gold token destruction, payment-only temporary resource and Renata bonus | 完整 token resource lifecycle、bonus interactions 与 cross-window generated-resource use 未关闭 |

## Guardrails

- Manifest ability ids 必须继续 exactly match `P4ActivatedAbilityCatalog.GetAll().Where(IsResourceSkill)`.
- Current representative catalog size 必须继续是 19 个 ability ids，且不得重复。
- 每个 family 必须保留 prompt、command、`ABILITY_ACTIVATED` audit、no-mutation rollback、doc 与 NOT READY closure anchors。
- Manifest 必须继续回连 4D-03AV residual blocker family `RESOURCE_SKILL_A_C_FAMILY`，且该 family 仍是 `catalog-bound-representative`。
- Remaining official breadth 必须显式保留 `[A]`、`[C]`、cross-window、temporary、conversion、Gold token、generated-resource 与 payment-only residuals。
- Manifest 必须继续写明 `NOT READY` 与 `P0-005 remains open`。
- Manifest 不得出现 `FullOfficialRulePass` 或 READY closure。

## Validation

- Focused PaymentEngine coverage guard: **34 / 34 passed**.
- Adjacent PaymentEngine / resource skill / prompt / hub regression: **475 / 475 passed**.
- Backend full: **4471 / 4471 passed**.
- `git diff --check`: passed.

## Verdict

4D-03AZ 把 4D-03AL resource skill catalog-bound evidence 与 4D-03AV `RESOURCE_SKILL_A_C_FAMILY` residual blocker 的关系变成更直接的 executable guard。它改善后续 routing 与回归可见性，但不证明完整 `[A]` / `[C]` resource skill official family、cross-window resource-skill use、full PaymentEngine official matrix、P0/P1、frontend final validation、card matrix full-official coverage 或 READY。项目仍 **NOT READY**。

# 4D-03DC-B PaymentEngine Selected Resource Skill Runtime/Card-Row Parity Audit

日期：2026-05-16
结论：**ACCEPTED B-SIDE FOCUSED VERIFIER / PROJECT NOT READY**

## 范围

4D-03DC-B 承接 A 主控在 4D-03DC 建立的 `B_PAYMENT_ENGINE_RESOURCE_SKILL_RUNTIME_CARD_ROW_PARITY_VERIFIER` contract。本批保持 test/docs-only，不修改 runtime、frontend、Chrome / browser scripts、formal 18-step scripts、card matrix JSON、`fullOfficial`、READY 或 `riftbound-dotnet.sln`。

本批在 `PaymentEngineCoverageAuditTests` 中新增 selected high-signal verifier，把 03DC contract 从 future boundary 推进为可执行 focused guard。它只选择一组高信号 `[A]` / `[C]` resource-skill official source-card groups，并把每行绑定到 current official candidate、runtime/card-row evidence、focused verifier methods、source-card parity、exact card-row `fullOfficial=false` 与 03DC-B docs anchors。

## Selected Rows

| Selected group | Representative row | Evidence focus |
| --- | --- | --- |
| `MALZAHAR_A_A_TARGET_AS_COST` | `OGN·113/298` | `[A A]` target-as-cost payment-only generated power |
| `LUX_SPELL_ONLY_PAYMENT_STEP` | `OGS·014/024` | spell-only generated mana payment-step consumption |
| `DRAGON_SOUL_SAGE_REACTION_MANA` | `UNL-093/219` | reaction-speed generated mana |
| `ANCIENT_STELE_CONVERSION_TEMP_RESOURCE` | `SFD·117/221` | conversion from mana to generic temporary payment resource |
| `GOLD_TOKEN_GENERIC_TEMP_RESOURCE` | `UNL·T05` | destroy-cost generated generic temporary resource |
| `ORNN_BRIDGE_EQUIPMENT_ONLY_POWER` | `SFD·189/221` / `SFD·244/221` source-card group | bridge-closed equipment-only generated power via `LegendResourceBridgeResourceSkillClosureManifest` |

## 改动文件

- `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`
  - 新增 `SelectedResourceSkillOfficialRuntimeCardRowParityManifest`。
  - 新增 focused guards，要求 selected rows 绑定 official candidate、ability id、prompt、command revalidation、audit、generated-resource lifetime、rollback、source-card parity、card-row parity 与 03DC-B docs。
  - Ornn bridge row 必须绑定 `LegendResourceBridgeResourceSkillClosureManifest` exact source-card group，不能退回 `LEGEND_ACT` proxy。
- `docs/CURRENT_STAGE4D_03DC_B_PAYMENT_ENGINE_SELECTED_RESOURCE_SKILL_RUNTIME_CARD_ROW_PARITY_AUDIT.md`
- `docs/CURRENT_STAGE4D_03DC_B_PAYMENT_ENGINE_SELECTED_RESOURCE_SKILL_RUNTIME_CARD_ROW_PARITY_EVIDENCE.md`

## 非闭合项

本批不关闭 P0-005、P1、full official PaymentEngine matrix、完整 `[A]` / `[C]` resource-skill runtime/card-row interactions、完整 target-bearing activated ability official family、full-card matrix 或 READY。03CV / 03CY 仍是 representative proxy evidence；03DC-B 只把 selected high-signal rows 提升为更强的 focused parity verifier。

## 验证

- Focused：`source scripts/dev-env.sh && dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter FullyQualifiedName~PaymentEngineCoverageAuditTests` -> 165/165 passed。
- Adjacent：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PaymentEngineCoverageAuditTests|FullyQualifiedName~PaymentEngineUnificationTests|FullyQualifiedName~ResourceSkill|FullyQualifiedName~LegendResourceBridgeVerifierTests|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub"` -> 673/673 passed。
- Full：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` -> 4734/4734 passed。
- `git diff --check`：passed。

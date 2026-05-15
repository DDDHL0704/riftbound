# Stage 4D-03AW PaymentEngine Target / Colored Activated Ability Manifest Audit

日期：2026-05-16
结论：**FOCUSED VERIFIER ACCEPTED / PROJECT NOT READY**

本切片新增 server-side conformance verifier，把 P0-005 中当前已实现的 target-bearing / typed-color / experience activated ability representatives 固定为 catalog-bound manifest。它不修改 runtime、不修改前端、不修改 card matrix、不升级 fullOfficial，也不关闭 P0-005、P1 或 READY。

## Scope

写入范围：

- `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`
- `docs/CURRENT_STAGE4D_03AW_PAYMENT_ENGINE_TARGET_COLORED_ACTIVATED_ABILITY_MANIFEST_AUDIT.md`
- `docs/CURRENT_STAGE4D_03AW_PAYMENT_ENGINE_TARGET_COLORED_ACTIVATED_ABILITY_MANIFEST_EVIDENCE.md`
- A-side checkpoint / completion / server-rule / closure-plan / dispatch docs

No-go：

- 不改 `src/Riftbound.Engine` runtime。
- 不改 `ActionPrompt` contracts、API、前端、Chrome smoke、formal E2E 或 card matrix JSON。
- 不触碰 `riftbound-dotnet.sln`。
- 不把 verifier 通过解释为 full official activated ability 或 PaymentEngine closure。

## Manifest Policy

`PaymentEngineTargetColoredActivatedAbilityManifest*` tests 直接读取 `P4ActivatedAbilityCatalog.GetAll()`，并锁定以下 predicate：

- `IsResourceSkill=false`
- 且满足 `RequiredTargetCount > 0`、typed `PowerCostByTrait`、`ExperienceCost > 0` 或 `AppliesSpellshieldTargetTax=true` 任一条件

当前 manifest 精确包含 8 个 ability ids：

| Ability | Representative scope | Residual |
| --- | --- | --- |
| `PAY_RED_EXHAUST_DAMAGE_3` | Xerath target damage + Spellshield tax | 完整 damage-skill target breadth 未关闭 |
| `RENATA_GLASC_PAY_1_BLUE_DRAW_1` | Renata typed-blue draw | 完整 colored activated draw breadth 未关闭 |
| `RENATA_GLASC_PAY_4_BLUE4_EXHAUST_SCORE_1` | Renata typed-blue score | 完整 colored activated score breadth 未关闭 |
| `AZIR_SWIFT_PAY_GREEN_SWAP_WITH_CONTROLLED_UNIT` | Azir green swift target swap + optional reattach representative | 完整 swift timing / optional armament breadth 未关闭 |
| `GATEKEEPER_MADULI_PAY_PURPLE_MOVE_TO_WEAKER_ENEMY_BATTLEFIELD` | Maduli purple battlefield move | 完整 movement target / static interaction breadth 未关闭 |
| `EZREAL_PAY_BLUE_SWIFT_MOVE_TO_BASE` | Ezreal blue swift no-target self move-to-base | attack/defense trigger、cannot-combat static 与 full swift timing 未关闭 |
| `CRIMSON_ROSE_EXPERIENCE3_EXHAUST_READY_UNIT` | Crimson Rose experience ready target + Spellshield tax | 完整 experience / ready-prevention breadth 未关闭 |
| `SHADOW_SWIFT_PAY_1_A_EXHAUST_STUN_ATTACKER` | Shadow swift stun target + Spellshield tax | 完整 battle-response swift family 未关闭 |

明确排除：

- `PAY_2_RED_DOUBLE_POWER`：Vi no-target generic power representative，不属于本 predicate。
- `FLUFT_PORO_EXHAUST_CREATE_TWO_SPELLSHIELD_WARHAWKS`：Fluft Poro no-target token representative，不属于本 predicate。
- 所有 `IsResourceSkill=true` entries：仍由 4D-03AL resource skill manifest 负责。

## Guardrails

- Manifest ability ids 必须等于当前 catalog predicate 结果，且 exactly once。
- 每个 entry 必须有 prompt、command、audit、no-mutation rollback anchors。
- typed payment entry 必须在 payment profile 中保留 trait 和数量。
- target-bearing entry 必须在 target profile 中保留 `RequiredTargetCount`。
- experience entry 必须保留 experience 数量。
- Spellshield tax entry 必须保留 Spellshield tax payment profile。
- Manifest 必须继续写明 remaining official breadth、`NOT READY` 与 `P0-005 remains open`。
- Manifest 不得出现 `FullOfficialRulePass`、`fullOfficial=true` 或 READY closure。

## Validation

- Focused PaymentEngine coverage guard: **22 / 22 passed**.
- Adjacent target / payment / prompt / hub regression: **530 / 530 passed**.
- Backend full: **4459 / 4459 passed**.
- `git diff --check`: passed.

## Verdict

4D-03AW 把 4D-03AV 中的 `TARGET_BEARING_COLORED_ACTIVATED_ABILITIES` residual family 进一步拆成 executable catalog-bound manifest。它改善后续 routing 与回归可见性，但不证明完整 target-bearing activated ability family、完整 typed activated ability family、完整 experience / Spellshield / swift interactions、P0/P1、frontend final validation、card matrix full-official coverage 或 READY。项目仍 **NOT READY**。

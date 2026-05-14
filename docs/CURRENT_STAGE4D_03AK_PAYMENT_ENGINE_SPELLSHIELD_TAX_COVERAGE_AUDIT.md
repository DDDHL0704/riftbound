# Stage 4D-03AK PaymentEngine Spellshield Tax Coverage Audit

日期：2026-05-15
结论：**FOCUSED AUDIT ACCEPTED / PROJECT NOT READY**

本切片新增 server-side conformance verifier，锁定当前 activated ability catalog 中声明 `AppliesSpellshieldTargetTax=true` 的能力必须有 prompt、command、audit 与 no-mutation rollback anchors。它不修改 runtime，不新增卡，不修改前端或 coverage matrix；只防止后续新增 Spellshield tax activated ability 时漏掉支付覆盖证据。

## Scope

写入范围：

- `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`
- `docs/CURRENT_STAGE4D_03AK_PAYMENT_ENGINE_SPELLSHIELD_TAX_COVERAGE_AUDIT.md`
- `docs/CURRENT_STAGE4D_03AK_PAYMENT_ENGINE_SPELLSHIELD_TAX_COVERAGE_EVIDENCE.md`
- A-side checkpoint / completion / server-rule / closure-plan docs

No-go：

- 不改 `src/Riftbound.Engine` runtime。
- 不改 API、前端、Chrome smoke 或 formal E2E。
- 不更新 card coverage matrix。
- 不关闭 P0-005、P1 或 READY。

## Manifest Policy

`PaymentEngineSpellshieldTaxCoverageManifestMatchesActivatedAbilityCatalog` 直接读取 `P4ActivatedAbilityCatalog.GetAll()`，并要求 manifest ability ids 与所有 `AppliesSpellshieldTargetTax=true` 的 ability ids 完全一致。

当前 catalog-bound manifest：

| Ability | Representative surface | Evidence requirement |
| --- | --- | --- |
| `XerathDamageAbilityId` | Xerath target damage representative | Prompt target choices、command commit、`COST_PAID` Spellshield tax audit、insufficient-tax no-mutation guard |
| `CrimsonRoseReadyAbilityId` | Crimson Rose ready-unit representative | Prompt target choices、command commit、`COST_PAID` Spellshield tax audit、insufficient-tax no-mutation guard |
| `ShadowStunAbilityId` | Shadow swift stun representative | Battle-response prompt metadata、command commit、`COST_PAID` Spellshield tax audit、insufficient-tax no-mutation guard |

## Guardrails

- Manifest entries must stay in the `ACTIVATE_ABILITY` payment window.
- Every entry must retain non-empty representative surface and target scope.
- Every entry must include prompt, command, `COST_PAID` audit and no-mutation rollback anchors.
- Every doc anchor must remain under `docs/*.md`.
- The verifier must not claim full official P0-005 closure.

## Verdict

4D-03AK strengthens Spellshield target-tax audit discipline for activated abilities. It proves catalog coverage for the current three tax-bearing activated abilities, but it does **not** prove all future payment windows, all Spellshield tax surfaces, keyword payment branches, replacement / alternative / additional costs, LayerEngine, frontend final validation, full-card matrix, or READY.

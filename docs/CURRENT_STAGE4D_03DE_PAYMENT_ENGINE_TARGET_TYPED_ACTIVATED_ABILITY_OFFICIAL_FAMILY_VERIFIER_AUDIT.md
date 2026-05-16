# 4D-03DE PaymentEngine Target/Typed Activated Ability Official Family Verifier Audit

日期：2026-05-16
结论：**ACCEPTED / A-VALIDATED / PROJECT NOT READY**

## 1. 范围

4D-03DE 是 03DD concrete dispatch 后的窄 test/docs-only verifier。它不实现 runtime，不修改 frontend、Chrome / browser scripts、formal 18-step scripts、card matrix JSON、`fullOfficial` / READY，也不触碰 `riftbound-dotnet.sln`。

本批只更新 `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`、本 audit / evidence docs 与 A-side routing docs 顶部口径。

## 2. Verifier Scope

`TargetTypedActivatedAbilityOfficialFamilyVerifierManifest` 覆盖当前 `TargetColoredActivatedAbilityCoverageManifest` 的 8 个 target / typed / experience / Spellshield-tax activated ability representatives：

- Xerath target damage。
- Renata Glasc typed-blue draw。
- Renata Glasc typed-blue score。
- Azir green swift swap。
- Gatekeeper Maduli purple battlefield move。
- Ezreal blue swift move-to-base。
- Crimson Rose experience ready-unit。
- Shadow swift stun。

每个 row 必须绑定：

- `TargetTypedActivatedAbilityOfficialRuntimeCardRowEvidenceManifest` 的 exact source-card group、focused verifier methods、prompt / command / `COST_PAID` / `ABILITY_ACTIVATED` audit、runtime outcome、rollback 与 card-row evidence。
- `TargetTaxActivatedAbilityMatrixManifest` 的 6 个 dimensions。
- `P4ActivatedAbilityCatalog.SourceCardNosForAbility(abilityId)` exact source-card group。
- 当前 card matrix exact rows `fullOfficial=false`，且 card matrix JSON unchanged。
- 03DE、03DD、03DA、03BR、03DB 与 completion / next-dispatch docs anchors。

## 3. Non-Closure Boundary

4D-03DE 是 representative official-family verifier evidence only。它不关闭：

- P0-005。
- P1。
- full official PaymentEngine matrix。
- full-card matrix / `E_CARD_MATRIX_READINESS`。
- `D_COMPLETION_P0_AUDIT`。
- Chrome smoke / formal 18-step final readiness reruns。
- `fullOfficial` / READY。

## 4. Validation

- Focused: `source scripts/dev-env.sh && dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter FullyQualifiedName~PaymentEngineCoverageAuditTests` -> passed, 171/171.
- Adjacent target/typed/payment filter: `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PaymentEngineCoverageAuditTests|FullyQualifiedName~PaymentEngineUnificationTests|FullyQualifiedName~RenataActivatedAbilityTests|FullyQualifiedName~AzirSwiftSwapActivatedAbilityTests|FullyQualifiedName~GatekeeperMaduliActivatedAbilityTests|FullyQualifiedName~EzrealBlueSwiftMoveToBaseActivatedAbilityTests|FullyQualifiedName~CrimsonRoseActivatedAbilityTests|FullyQualifiedName~ShadowActivatedAbilityTests|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub"` -> passed, 605/605.
- Full backend: `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` -> passed, 4740/4740.
- Chrome / formal 18-step: not required for this test/docs-only slice。
- `git diff --check`: passed.

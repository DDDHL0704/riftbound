# 4D-03DA PaymentEngine Target / Typed Activated Ability Official Runtime Card-Row Audit

日期：2026-05-16
结论：**ACCEPTED / PROJECT NOT READY**

## 范围

本批锁定 `TargetColoredActivatedAbilityCoverageManifest` 的 8 个 current `ACTIVATE_ABILITY` representatives：

- Xerath：`UNL-026/219`
- Renata Glasc draw / score：`SFD·088/221`、`SFD·088a/221`
- Azir swift swap：`SFD·050/221`、`SFD·050a/221`
- Gatekeeper Maduli：`UNL-144/219`
- Ezreal blue swift move-to-base：`SFD·082/221`、`SFD·082a/221`、`SFD·082b/221·P`
- Crimson Rose：`UNL-109/219`
- Shadow：`UNL-194/219`

本批只补强 target / typed activated ability runtime/card-row evidence：`PaymentEngineCoverageAuditTests` 新增 `TargetTypedActivatedAbilityOfficialRuntimeCardRowEvidenceManifest`，把 03AW 的 8 个 ability entries 与 03BR target/tax matrix 回连到真实 focused verifier 类型/方法、`P4ActivatedAbilityCatalog.SourceCardNosForAbility` exact source-card group、prompt / command / `COST_PAID` / `ABILITY_ACTIVATED` / outcome / rollback evidence，以及 `CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` exact `cardNo` / `collectorId` rows。

## 改动

- `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`
  - 新增 `target-typed-activated-ability-official-runtime-card-row-evidence` classification。
  - 新增 `TargetTypedActivatedAbilityOfficialRuntimeCardRowEvidenceManifest`，覆盖 8 个 ability ids 与 11 张 exact source-card rows。
  - 新增 guard，确认 focused methods 存在并带 `[Fact]` / `[Theory]`，card matrix rows 仍 `fullOfficial=false`，card matrix JSON 未改。
- `docs/CURRENT_STAGE4D_03DA_PAYMENT_ENGINE_TARGET_TYPED_ACTIVATED_ABILITY_OFFICIAL_RUNTIME_CARD_ROW_AUDIT.md`
- `docs/CURRENT_STAGE4D_03DA_PAYMENT_ENGINE_TARGET_TYPED_ACTIVATED_ABILITY_OFFICIAL_RUNTIME_CARD_ROW_EVIDENCE.md`

## 非闭合项

本批不修改 runtime `src/**`、focused tests、前端、Chrome / browser scripts、formal 18-step scripts、card matrix JSON、`fullOfficial` 或 READY，也不触碰 `riftbound-dotnet.sln`。它不关闭完整 target-bearing activated ability family、完整 target-tax / typed / experience / Spellshield breadth、P0-005、P1、full official PaymentEngine matrix、full-card matrix 或最终 completion audit。

## 验证

- `source scripts/dev-env.sh && dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~PaymentEngineCoverageAuditTests|FullyQualifiedName~PaymentEngineUnificationTests|FullyQualifiedName~RenataActivatedAbilityTests|FullyQualifiedName~AzirSwiftSwapActivatedAbilityTests|FullyQualifiedName~GatekeeperMaduliActivatedAbilityTests|FullyQualifiedName~EzrealBlueSwiftMoveToBaseActivatedAbilityTests|FullyQualifiedName~CrimsonRoseActivatedAbilityTests|FullyQualifiedName~ShadowActivatedAbilityTests"`：406/406 passed
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PaymentEngineCoverageAuditTests|FullyQualifiedName~PaymentEngineUnificationTests|FullyQualifiedName~RenataActivatedAbilityTests|FullyQualifiedName~AzirSwiftSwapActivatedAbilityTests|FullyQualifiedName~GatekeeperMaduliActivatedAbilityTests|FullyQualifiedName~EzrealBlueSwiftMoveToBaseActivatedAbilityTests|FullyQualifiedName~CrimsonRoseActivatedAbilityTests|FullyQualifiedName~ShadowActivatedAbilityTests|FullyQualifiedName~ActivateAbility|FullyQualifiedName~Spellshield|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub"`：666/666 passed
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`：4727/4727 passed
- `git diff --check`：passed

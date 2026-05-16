# 4D-03CZ PaymentEngine Typed Sigil Resource Skill Runtime Card-Row Audit

日期：2026-05-16
结论：**ACCEPTED / PROJECT NOT READY**

## 范围

本批锁定 12 张 typed Sigil official resource-skill rows：

- SFD：`SFD·222/221`、`SFD·226/221`、`SFD·229/221`、`SFD·231/221`、`SFD·234/221`、`SFD·238/221`
- OGN：`OGN·040/298`、`OGN·081/298`、`OGN·120/298`、`OGN·163/298`、`OGN·204/298`、`OGN·245/298`

本批只补强 typed Sigil runtime/card-row audit evidence：focused tests 逐行绑定 `P4ActivatedAbilityCatalog.GetSigilTypedResourceProfiles()` 的 `SourceCardNo`、`AbilityId`、`EffectKind`、trait、`ResourceRestriction`、`IsResourceSkill=true`，并确认 prompt sourceRequirements、command revalidation、`ABILITY_ACTIVATED` / `POWER_GAINED` audit metadata、typed temporary resource lifetime、wrong-color / mana-only / stale / wrong-print rollback 与 card matrix exact row。

## 改动

- `src/Riftbound.Engine/CoreRuleEngine.cs`
  - typed Sigil `POWER_GAINED` payload 增加 `cardNo` 与 `effectKind`，与既有 `ABILITY_ACTIVATED` source-card audit metadata 对齐。
- `tests/Riftbound.ConformanceTests/RageSigilResourceSkillTests.cs`
- `tests/Riftbound.ConformanceTests/SfdSigilResourceSkillTests.cs`
- `tests/Riftbound.ConformanceTests/OgnSigilResourceSkillTests.cs`
  - focused runtime tests 断言 exact source card、ability id、effect kind、trait power、resource restriction、allowed payment kinds 与 temporary ledger audit metadata。
- `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`
  - 新增 `TypedSigilOfficialRuntimeCardRowAuditManifest`，固定 12 张 typed Sigil rows 的 runtime/card-row audit verifier，并要求 exact matrix rows 继续 `fullOfficial=false`。

## 非闭合项

本批不修改前端、不运行 Chrome smoke / formal 18-step、不修改 card matrix JSON、不升级 `fullOfficial`，也不声明 P0-005、P1 或 READY 关闭。完整 PaymentEngine official matrix、完整 `[A]` / `[C]` resource-skill row interactions、full-card matrix 与最终 completion audit 仍未完成。

## 验证

- `source scripts/dev-env.sh && dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~PaymentEngineCoverageAuditTests|FullyQualifiedName~RageSigilResourceSkillTests|FullyQualifiedName~SfdSigilResourceSkillTests|FullyQualifiedName~OgnSigilResourceSkillTests"`：219/219 passed
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PaymentEngineCoverageAuditTests|FullyQualifiedName~RageSigilResourceSkillTests|FullyQualifiedName~SfdSigilResourceSkillTests|FullyQualifiedName~OgnSigilResourceSkillTests|FullyQualifiedName~ResourceSkill|FullyQualifiedName~PaymentEngineUnificationTests|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub"`：581/581 passed
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`：4723/4723 passed
- `git diff --check`：passed

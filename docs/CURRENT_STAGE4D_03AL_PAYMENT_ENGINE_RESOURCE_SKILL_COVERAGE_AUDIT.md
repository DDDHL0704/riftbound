# Stage 4D-03AL PaymentEngine Resource Skill Coverage Audit

日期：2026-05-15
结论：**FOCUSED AUDIT ACCEPTED / PROJECT NOT READY**

本切片新增 server-side conformance verifier，锁定当前 `P4ActivatedAbilityCatalog.GetAll()` 中所有 `IsResourceSkill=true` 的 representative resource skills 必须映射到 family-level prompt、command、audit 与 rollback anchors。它不修改 runtime，不新增卡，不修改前端或 coverage matrix；只把当前 resource skill 代表性覆盖边界变成可回归检查的 catalog-bound manifest。

## Scope

写入范围：

- `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`
- `docs/CURRENT_STAGE4D_03AL_PAYMENT_ENGINE_RESOURCE_SKILL_COVERAGE_AUDIT.md`
- `docs/CURRENT_STAGE4D_03AL_PAYMENT_ENGINE_RESOURCE_SKILL_COVERAGE_EVIDENCE.md`
- A-side checkpoint / completion / server-rule / closure-plan docs

No-go：

- 不改 `src/Riftbound.Engine` runtime。
- 不改 API、前端、Chrome smoke 或 formal E2E。
- 不更新 card coverage matrix。
- 不关闭 P0-005、P1 或 READY。

## Manifest Policy

`PaymentEngineResourceSkillCoverageManifestMatchesActivatedAbilityCatalog` 直接读取 `P4ActivatedAbilityCatalog.GetAll()`，并要求 manifest ability ids 与所有 `IsResourceSkill=true` 的 ability ids 完全一致。

当前 catalog-bound manifest 覆盖 19 个 representative resource skill ability ids：

| Family | Ability count | Evidence requirement |
| --- | ---: | --- |
| Malzahar target-as-cost payment-only resource skill | 1 | Prompt source/target/cost metadata、command creation、temporary payment-only ledger audit、invalid / stale rollback |
| Dragon Soul Sage reaction mana resource skill | 1 | Reaction prompt、command mana gain、`ABILITY_ACTIVATED` / `MANA_GAINED` audit、timing / target rollback |
| SFD Sigil typed payment-only resource skill family | 6 | SFD typed prompt parity、command typed temporary resource creation、typed audit metadata、wrong source / trait rollback |
| OGN Sigil typed payment-only resource skill family | 6 | OGN typed prompt parity、command typed temporary resource creation、typed audit metadata、wrong source / trait rollback |
| Resource conversion equipment skill family | 3 | Energy Channel / Ancient Stele / Hextech Anomaly conversion prompt / command / audit / rollback anchors |
| Gold token payment-only resource skill family | 2 | UNL / SFD Gold prompt / command / temporary ledger / Renata bonus audit / rollback anchors |

## Guardrails

- Manifest ability ids must exactly match the catalog `IsResourceSkill=true` set.
- Every family must retain prompt, command, `ABILITY_ACTIVATED` audit and no-mutation rollback anchors.
- Every family closure status must keep project **NOT READY** and P0-005 open.
- Every doc anchor must remain under `docs/*.md`.
- The verifier must not claim `FullOfficialRulePass` or READY.

## Verdict

4D-03AL strengthens resource skill audit discipline for the current representative catalog. It proves all currently executable resource skill representatives have family-level evidence anchors, but it does **not** prove the full official `[A]` / `[C]` resource skill family, future resource skills, all payment windows, LayerEngine interactions, frontend final validation, full-card matrix, or READY.

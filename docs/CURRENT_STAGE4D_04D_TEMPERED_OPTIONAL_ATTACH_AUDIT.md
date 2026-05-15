# Stage 4D-04D Tempered Optional Attach Audit

日期：2026-05-15
结论：**ACCEPTED / PROJECT NOT READY**

本审计记录 4D-04D Tempered optional attach representative 的 A 侧验收结果。B-Implementation / Bacon `019e2ba3-4b9a-7710-a702-1e8e28ecd8ea` 已完成窄实现，A 已按 handoff 验收 focused、adjacent equipment regression、backend full test 与 diff hygiene。该批只减少 P1-002 equipment keyword residual，不关闭完整 `百炼`、完整装备规则、LayerEngine、card matrix、frontend final validation 或 READY。

## 1. Scope

4D-04D 只覆盖一条 `百炼` optional attach representative：

- source unit：`SFD·008/221` 哨兵好手
- selected armament：`SFD·186/221` 旋转飞斧
- optional token：`TEMPERED_ATTACH:<equipmentObjectId>`
- attachment event reason：`TEMPERED_OPTIONAL_ATTACH`

服务端现在允许 `SFD·008/221` 从手牌 `PLAY_CARD` 时选择当前玩家受控、公开、在场的 `SFD·186/221` 装备对象。合法命令保留既有 no-target play-card 支付 / stack 行为，结算时重验该装备仍合法后，把《旋转飞斧》的 `AttachedToObjectId` 设置为新入场的《哨兵好手》单位，并发出 `EQUIPMENT_ATTACHED`。缺失、敌方、非装备、手牌、face-down、stale、wrong-card 或 wrong-controller 选择会 no-mutation reject；结算前装备变 stale 时只跳过 attach，不伪造 attach event。

## 2. Accepted Changes

- `CardEquipmentKeywordRules` 新增 `HasImplementedRepresentativeTemperedOptionalAttachBoundary` 与 `IsTemperedOptionalAttachRepresentativeCardNo`，仅标记 `SFD·008/221` 的代表边界，同时保留 full tempered official breadth deferred。
- `CoreRuleEngine` 接受并校验 `TEMPERED_ATTACH:<equipmentObjectId>` optional token，仅在 `SFD·008/221` play-card 代表路径中允许 `SFD·186/221`；结算时二次重验并写入 attachment event。
- `MatchSession` / `ActionPromptBuilder` 在 `SFD·008/221` source requirement 中公开 server-filtered optional cost choices，不公开非法对象。
- `TemperedEquipmentOptionalAttachTests` 覆盖 prompt shape、合法 attach、no-optional no-attach、invalid choice no-mutation、resolution stale no-effect。
- `CardCatalogBaselineTests` 锁住 equipment keyword profile / coverage report 对 `百炼` representative + deferred breadth 的口径。

## 3. A-Side Validation

完整命令与输出摘要见 `docs/CURRENT_STAGE4D_04D_TEMPERED_OPTIONAL_ATTACH_EVIDENCE.md`。A 侧已验证：

- focused / keyword guard：14/14 passed
- adjacent equipment regression：139/139 passed
- backend full：4380/4380 passed
- `git diff --check` passed

## 4. Residual Risks

P1-002 仍 open。4D-04D 不实现或不关闭：

- full printed `百炼` breadth beyond `SFD·008/221`
- Jax weapon attach payment/draw trigger integration through `百炼`
- Ornn static equipment modifiers, Akshan enemy equipment movement, Armed Assaulter haste branch, and colored/dynamic `百炼` cost breadth
- owner/controller changes, equipment follow movement, or full attach lifecycle breadth
- Agile reaction timing, Jax-granted Agile, ephemeral/static equipment breadth
- LayerEngine
- card matrix full-official upgrade
- frontend final build / smoke / formal E2E fresh-run
- final READY

## 5. Verdict

4D-04D is accepted as a narrow server-authoritative Tempered optional attach representative. The project remains **NOT READY** and the active goal must not be marked complete.

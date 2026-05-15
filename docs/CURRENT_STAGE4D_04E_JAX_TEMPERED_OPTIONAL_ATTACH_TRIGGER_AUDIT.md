# Stage 4D-04E Jax Tempered Optional Attach Trigger Audit

日期：2026-05-15
结论：**ACCEPTED / PROJECT NOT READY**

本审计记录 4D-04E Jax Tempered optional attach trigger integration 的 A 侧验收结果。B-Implementation / Bacon `019e2ba3-4b9a-7710-a702-1e8e28ecd8ea` 已完成窄实现，A 已按 handoff 验收 focused、adjacent regression、backend full test 与 diff hygiene。该批只把 Jax 的 `百炼` optional attach representative 接到既有 Jax weapon attach trigger payment 上，不关闭完整 `百炼`、完整装备规则、LayerEngine、card matrix、frontend final validation 或 READY。

## 1. Scope

4D-04E 只覆盖一条 Jax + Spinning Axe representative：

- source unit：`SFD·119/221` / `SFD·119a/221` Jax
- selected armament：`SFD·186/221` 旋转飞斧
- optional token：`TEMPERED_ATTACH:<equipmentObjectId>`
- attachment event reason：`TEMPERED_OPTIONAL_ATTACH`
- trigger payment：`TRIGGER_PAYMENT` / `JAX_WEAPON_ATTACH_PAY_1_DRAW_1`

服务端现在允许 Jax 从手牌 `PLAY_CARD` 时选择当前玩家受控、公开、在场的 `SFD·186/221` 装备对象。合法命令保留既有 no-target play-card 支付 / stack 行为，结算时重验该装备仍合法后，把《旋转飞斧》的 `AttachedToObjectId` 设置为新入场的 Jax，并打开既有 Jax weapon attach trigger payment window。缺失、敌方、非装备、手牌、face-down、stale、wrong-card 或 wrong-controller 选择会 no-mutation reject；结算前装备变 stale 时只跳过 attach 和 payment，不伪造事件。

## 2. Accepted Changes

- `CardEquipmentKeywordRules` 将 `SFD·119/221` 与 `SFD·119a/221` 纳入 Tempered optional attach representative card set，同时保留 full tempered official breadth deferred。
- `CoreRuleEngine` 将 stack resolution 内产生的 Jax trigger payment 通过 `StackResolutionResult.PendingPayment` 窄承载回 `ResolvePassPriority(...)`；只有 stack clear、无 hand choice、无 winner、无 queued trigger 时才进入 pending payment，并优先于 Fiora event-scan payment。
- `TryAttachTemperedOptionalEquipmentToSource(...)` 在 successful tempered attach 后复用 `TryOpenJaxWeaponAttachPaymentWindow(...)`，避免重复构造 trigger payment payload。
- `MatchSession` / `ActionPromptBuilder` 在 Jax source requirement 中公开 server-filtered `TEMPERED_ATTACH:<equipmentObjectId>` optional cost choices。
- `JaxTemperedOptionalAttachTests` 覆盖 prompt shape、合法 attach + payment open、pay/decline/insufficient branches、no-optional no-payment、invalid choice no-mutation 与 resolution stale no-effect。
- `CardCatalogBaselineTests` 锁住 Jax `百炼` representative + deferred breadth 的 profile 口径。

## 3. A-Side Validation

完整命令与输出摘要见 `docs/CURRENT_STAGE4D_04E_JAX_TEMPERED_OPTIONAL_ATTACH_TRIGGER_EVIDENCE.md`。A 侧已验证：

- focused / keyword guard：41/41 passed
- adjacent equipment / payment regression：243/243 passed
- backend full：4397/4397 passed
- `git diff --check` passed

## 4. Residual Risks

P1-002 仍 open。4D-04E 不实现或不关闭：

- full printed `百炼` breadth beyond Jax + Spinning Axe and Sentinel + Spinning Axe representatives
- Ornn static equipment modifiers, Akshan enemy equipment movement, Armed Assaulter haste branch, colored/dynamic `百炼` cost breadth
- owner/controller changes, equipment follow movement, already-attached breadth or full attach lifecycle breadth
- Agile reaction timing, Jax-granted Agile outside current representative, ephemeral/static equipment breadth
- LayerEngine
- card matrix full-official upgrade
- frontend final build / smoke / formal E2E fresh-run
- final READY

## 5. Verdict

4D-04E is accepted as a narrow server-authoritative Jax Tempered optional attach trigger-payment representative. The project remains **NOT READY** and the active goal must not be marked complete.

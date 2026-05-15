# Stage 4D-04F Akshan Orange Extra Equipment Steal Audit

日期：2026-05-15
结论：**ACCEPTED / PROJECT NOT READY**

本审计记录 4D-04F Akshan orange extra equipment steal representative 的 A 侧验收结果。B-Implementation / Bacon `019e2ba3-4b9a-7710-a702-1e8e28ecd8ea` 完成窄实现，A 侧 code review 后补了一个最小语法集成修复与 orange rune recycle focused guard，并复跑 focused、adjacent regression、backend full test 与 diff hygiene。本批只改善 `SFD·109/221`《阿克尚》的一条 official branch representative，不关闭完整 `百炼`、完整装备规则、LayerEngine、card matrix、frontend final validation 或 READY。

## 1. Scope

4D-04F 只覆盖一条 Akshan representative：

- source unit：`SFD·109/221` 阿克尚
- optional token：`AKSHAN_STEAL_EQUIPMENT:<equipmentObjectId>`
- typed extra cost：2 orange power
- selected object：legal enemy field equipment
- reason：`AKSHAN_ORANGE_EXTRA_EQUIPMENT_STEAL`

服务端现在允许 Akshan 从手牌 `PLAY_CARD` 时选择合法敌方在场装备。合法命令支付基础 4 mana + 2 orange power，保留 selected optional token on stack；结算时 Akshan 入 P1 base，并重验所选装备仍为合法敌方装备。若合法，装备移动到 P1 base，`ControllerId` 改为 P1、`OwnerId` 保留；若该装备是 `武装`，再把 `AttachedToObjectId` 设置为 Akshan。结算前装备 stale 时只跳过 equipment side effect，不伪造 control / attach event。

## 2. Accepted Changes

- `CoreRuleEngine` 新增 Akshan-specific optional cost validation、typed orange payment plan、stack-time equipment move/control/attach resolution 与 Akshan-leaves return cleanup。
- `CoreRuleEngine` 对被 Akshan 夺取的装备记录窄 marker；只要 Akshan 仍在场，end turn 不归还；当 Akshan 离场时装备返回 owner base、controller 恢复 owner、attachment 清空并 emits `EQUIPMENT_CONTROL_RETURNED`。
- `MatchSession` / `ActionPromptBuilder` 在 Akshan source requirement 中公开 server-filtered `AKSHAN_STEAL_EQUIPMENT:<equipmentObjectId>` optional cost choices，并通过现有 payment-resource metadata 支持必要 orange rune recycle。
- `AkshanGuardTests` 覆盖 prompt shape、legal weapon steal / attach、orange rune recycle payment-resource path、legal non-weapon control without attach、invalid choice / insufficient / wrong trait no-mutation、malformed / duplicate optional costs、resolution stale no-effect、end turn no-return 与 Akshan leaving returns equipment。

## 3. A-Side Validation

完整命令与输出摘要见 `docs/CURRENT_STAGE4D_04F_AKSHAN_ORANGE_EXTRA_EQUIPMENT_STEAL_EVIDENCE.md`。A 侧已验证：

- focused / keyword guard：28/28 passed
- adjacent equipment / payment regression：209/209 passed
- backend full：4417/4417 passed
- `git diff --check` passed

## 4. Residual Risks

P1-002 仍 open。4D-04F 不实现或不关闭：

- full printed `百炼` breadth beyond current Sentinel / Jax / Akshan representatives
- Ornn static equipment modifiers, Armed Assaulter branch, colored/dynamic `百炼` cost breadth
- broad owner/controller replacement model or full attach lifecycle breadth
- full LayerEngine / continuous effect model
- card matrix full-official upgrade
- frontend final build / smoke / formal E2E fresh-run
- final READY

The Akshan “until leaves” duration uses a narrow representative marker, not a generalized continuous-control layer. That is acceptable for this slice but remains a blocker for full official breadth.

## 5. Verdict

4D-04F is accepted as a narrow server-authoritative Akshan orange extra equipment steal representative. The project remains **NOT READY** and the active goal must not be marked complete.

# 4D-03AI PaymentEngine Status Refresh Audit

日期：2026-05-15
结论：**NOT READY**

## Scope

本审计是 A 侧 doc-only 状态刷新，用于纠正 PaymentEngine 总览段落落后于逐条验收记录的问题。它不修改 runtime、不新增测试、不更新 frontend、不更新 card matrix，也不关闭 P0-005 / P1 / READY。

刷新目标：

- 将 4D-03 当前状态从旧的 “through 4D-03Q / 4D-03T / 4D-03V” 口径对齐到现有证据中的 4D-03AH。
- 明确 4D-03X / 4D-03Y / 4D-03Z / 4D-03AA / 4D-03AB 是 catalog / token representative 收口，不等价于 P0-005 full PaymentEngine closure。
- 继续保留 P0-005 residual：完整 `[A]` / `[C]` resource skill family、remaining payment windows、target-bearing colored-cost activated abilities、keyword payment branches、replacement / optional / alternative / tax quote-command-audit parity，以及 full-card / LayerEngine / frontend final gates。

## Accepted PaymentEngine Facts

截至本刷新，P0-005 PaymentEngine 已有以下代表性证据：

- 4D-03 foundation through 4D-03W 已验收，覆盖 shared `PaymentPlan` / authorize / commit foundation、non-play windows、`PLAY_CARD` optional / extra / payment-resource representatives、`ACTIVATE_ABILITY` payment resource representatives、`HIDE_CARD`、pending `PAY_COST` resource action、battlefield held score resource action、trigger payment resource action、Malzahar / Dragon Soul Sage resource skills、Renata colored activated draw / score、Crimson Rose ready-unit、Fluft Poro Warhawk token、Shadow swift stun、SFD / OGN Sigil typed payment-only resources、resource conversion equipment、Gold token resource skill 与 Renata Gold bonus representative。
- 4D-03AC 已验收：battlefield held temporary payment resource parity representative；focused 221/221、backend full 4158/4158、`git diff --check` 通过。
- 4D-03AD 已验收：SFD Fiora trigger temporary payment resource parity representative；focused 149/149、backend full 4170/4170、`git diff --check` 通过。
- 4D-03AE 已验收：pending `PAY_COST` / `TRIGGER_PAYMENT` temporary resource aggregate prompt parity representative；focused 170/170、backend full 4173/4173、`git diff --check` 通过。
- 4D-03AF 已建立 A-side remaining-scope audit / baseline；focused baseline 587/587 通过，并确认 P0-005 仍不能关闭。
- 4D-03AG 已验收：`PLAY_CARD` typed resource prompt parity representative；focused 454/454、backend full 4177/4177、`git diff --check` 通过。
- 4D-03AH 已验收：PaymentEngine action-window coverage verifier；focused 717/717、backend full 4182/4182、`git diff --check` 通过。该 verifier 只新增 server-side conformance audit manifest，将 `PLAY_CARD` / `PAY_COST` / `TRIGGER_PAYMENT` / `ASSEMBLE_EQUIPMENT` / `ACTIVATE_ABILITY` / `LEGEND_ACT` / battlefield held score payment / `HIDE_CARD` 标记为 representative-covered，并将 `MOVE_UNIT` 标记为 policy-non-resource。

相关非 P0-005 closure 证据：

- 4D-03X / 4D-03Y 已验收：legend / battlefield deferred catalog hygiene。
- 4D-03Z / 4D-03AA / 4D-03AB 已验收：Baron Nest static、Image copy-token、Brush battlefield replacement representatives。

## Remaining P0-005 Work

P0-005 仍未 full-official 关闭，主要因为：

- 完整 `[A]` / `[C]` resource skill family 还不是 full official coverage。
- target-bearing colored-cost activated abilities 与剩余 activation / trigger / battlefield payment windows 仍是 representative breadth。
- Spellshield tax、Echo、Haste / special / alternative / additional cost branches 仍需要完整 payment-window quote / command / audit parity。
- replacement / optional / alternative / discount / tax cost interactions 仍需服务端统一计划、失败回滚与证据矩阵对齐。
- 4D-03AH verifier 是 coverage manifest，不是 card matrix 升级，也不是 frontend / E2E / LayerEngine closure。

## No-Go

- 不声明 READY。
- 不修改 `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`。
- 不启动 runtime / frontend / LayerEngine 实现。
- 不把 4D-03X through 4D-03AB 的 catalog / token representative 误算为 P0-005 full closure。

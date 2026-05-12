# Stage 4C-70 Skullcrack Battlefield Stun Audit

审计日期：2026-05-13
结论：**代表性证据已验证；项目整体仍 NOT READY。**

## 范围

- 代表 FU：`FU-ee886701e4`
- 代表卡：强手裂颅 / Skullcrack `OGN·220/298` / cardId `31458`
- 代表 effect：`SKULLCRACK_STUN_FRIENDLY_AND_ENEMY_BATTLEFIELD_UNITS`
- 本批是 evidence-only overlay，不修改功能代码；只覆盖 ordinary hand `PLAY_CARD`、支付 2 mana、按顺序选择一名友方战场单位和一名敌方战场单位、入栈并在 pass-pass 后对两名目标施加 `STUNNED`。

## 证据事实

- `CardBehaviorRegistry` 已登记 `OGN·220/298` 为 direct card behavior：`Cost: 2`、`TargetCount: 2`、`StatusEffectId: STUNNED`、`TargetScope: FriendlyBattlefieldThenEnemyBattlefieldUnits`。
- `p2-preflight-play-skullcrack-stun-friendly-and-enemy-battlefield-units.fixture.json` 已记录官方卡面、核心规则/FAQ 证据和完整 pass-pass 结算预期。
- `CoreRuleEngineRejectsSkullcrackAgainstWrongOrderOrBaseUnits` 已覆盖目标顺序错误、友方基地单位、敌方基地单位和同阵营第二目标拒绝，且拒绝时不支付、不入栈、不施加状态。
- `docs/rules-evidence-index.md` 已有该 fixture 的 `RULE_AUDITED` 索引。

## 验证

- focused：2/2 passed。
- Stun / battlefield status regression：64/64 passed。
- backend full：3754/3754 passed。
- frontend build：passed。
- Chrome smoke：passed。

## 非覆盖

不声明 same-battlefield precision、standby / reaction、quick / spell-duel timing、full FEPR target / stack lifecycle、PaymentEngine、status duration / cleanup / replacement / prevention、LayerEngine、hidden-info / redaction matrix、完整 `SOUL-JFAQ-260114 p23` 裁定、1009/811 full-official 或 formal 18-step E2E 已完成。

# Stage 4C-87 Shield Wall Move Guard Audit

审计日期：2026-05-13
结论：**代表性移动目标 guard 证据已验证；项目整体仍 NOT READY。**

## 范围

- 代表 FU：`FU-a7fbef72ba`
- 代表卡：禁军之墙 / Shield Wall `SFD·043/221` / cardId `33119`
- 代表 effect：`SHIELD_WALL_MOVE_ANY_FRIENDLY_BATTLEFIELD_UNITS_TO_BASE`
- 本批是 evidence-only overlay，不修改功能代码；只把既有服务端权威代表路径和测试入账到 Stage 4C-87 矩阵。
- 本批不声明完整 multi-battlefield movement matrix、待命 / 迅捷窗口、完整 FEPR、完整 PaymentEngine、hidden-info / redaction、1009/811 full-official 或 READY。

## 证据事实

- `src/Riftbound.Engine/CardBehaviorRegistry.cs` 已登记 `SFD·043/221`：基础费用 2，`TargetScope = FriendlyBattlefieldUnit`，`MinTargetCount = 0`，`MovesTargetToBase = true`，`UsesFriendlyBattlefieldUnitCountAsMaxTargetCount = true`。
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-shield-wall-move-any-friendly-battlefield-units-to-base.fixture.json` 覆盖从手牌打出、支付 2、加入结算链、双方让过后将所选友方战场单位移动到基地。
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs` 覆盖 fixture 路径，以及敌方单位、友方基地单位、重复目标的 no-mutation 拒绝。

## 验证

- focused Shield Wall regression：2/2 passed。
- MoveFriendly / MoveUnit / FriendlyBattlefieldUnit adjacent regression：63/63 passed。
- backend full：3771/3771 passed。
- frontend build / Chrome smoke：本批未重跑；无前端或功能代码变更。

## 非覆盖

不声明完整多战场选择、每战场 movement matrix、待命 / 迅捷 timing windows、完整 FEPR target / stack / timing、完整 PaymentEngine、control-zone movement 全矩阵、hidden-info / replay / redaction、1009/811 full-official 或 READY。

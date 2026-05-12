# Stage 4C-86 Imperial Shrine Conquer Sand Soldier Audit

审计日期：2026-05-13
结论：**代表性战场征服触发证据已验证；项目整体仍 NOT READY。**

## 范围

- 代表 FU：`FU-ec31812b00`
- 代表卡：帝王神坛 / Imperial Shrine `SFD·207/221` / cardId `33306`
- 代表 effect：`BATTLEFIELD_RULE_DOMAIN`
- 代表触发：征服该战场时，支付 1 法力，返回一个控制的该战场单位到其拥有者手牌，并在该战场打出 2 战力 `SFD·T02` 黄沙士兵。
- 本批是 evidence-only overlay，不修改功能代码；只把既有服务端权威代表路径和测试入账到 Stage 4C-86 矩阵。
- 本批不声明完整 optional trigger prompt / decline、完整 PaymentEngine、完整战场 / 法术对决 / 战斗生命周期、APNAP 触发排序、FAQ p22 完整裁定、hidden-info / redaction、1009/811 full-official 或 READY。

## 证据事实

- `src/Riftbound.Engine/CoreRuleEngine.cs` 已有 `BattlefieldConquerPayOneReturnUnitCreateSandSoldierCardNo = "SFD·207/221"` 和 `TryResolveBattlefieldConquerPayOneReturnUnitCreateSandSoldierTrigger`。
- `DECLARE_BATTLE` 征服流程在战场结果后检查 `SFD·207/221`，当征服玩家有 1 法力且存在确定的受控战场单位时，扣 1 法力、返回该单位到拥有者手牌，并创建 `SFD·T02` 黄沙士兵到战场区。
- 服务端事件包含 `BATTLEFIELD_TRIGGER_RESOLVED` / `COST_PAID` / `UNIT_RETURNED_TO_HAND` / `UNIT_TOKEN_CREATED`，其中 trigger / abilityId 为 `BATTLEFIELD_CONQUERED_PAY_1_RETURN_UNIT_CREATE_SAND_SOLDIER`。
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs` 覆盖支付成功、返回单位、创建 2 战力黄沙士兵，以及无法力时不触发、不返回、不创建。
- `tests/Riftbound.ConformanceTests/GameHubJoinTests.cs` 覆盖 `battlefield-conquer-sand-soldier` development seed，从 ActionPrompt 暴露战场 destination 到 Hub submit 后 authoritative event / snapshot 的代表路径。

## 验证

- focused Imperial Shrine regression：3/3 passed。
- battlefield conquer adjacent regression：45/45 passed。
- backend full：3771/3771 passed。
- frontend build / Chrome smoke：本批未重跑；无前端或功能代码变更。

## 非覆盖

不声明完整 optional trigger prompt / decline choice、trigger payment window UI、完整 PaymentEngine quote / authorize / commit、完整 battle / spell-duel / battlefield lifecycle、multi-battlefield APNAP trigger ordering、完整 FAQ p22 adjudication、hidden-info / replay / redaction 全矩阵、1009/811 full-official 或 READY。

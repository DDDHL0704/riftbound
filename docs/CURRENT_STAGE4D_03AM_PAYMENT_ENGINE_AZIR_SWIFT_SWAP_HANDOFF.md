# Stage 4D-03AM PaymentEngine Azir Swift Swap Handoff

日期：2026-05-15
结论：**HANDOFF READY / PROJECT NOT READY**

本 handoff 是 A 主控给下一枚 P0-005 PaymentEngine breadth 切片的写锁和验收规格。它不实现 runtime，不修改前端，不修改测试代码，不更新 card matrix。目标是把 P0-005 剩余的 “target-bearing colored-cost activated abilities” 从总清单落到一个具体、可验收、可回滚的代表切片。

## 1. 目标

实现并验收 `SFD·050/221` 与 `SFD·050a/221` 阿兹尔的绿色迅捷位置交换 activated ability representative：

```txt
支付{{绿色}}：{{迅捷}} — 选择一个受你控制的单位，将我移动到它的位置，再将它移动到我原来的位置。如果该单位已配有武装，则你可以选择将其中一件武装贴附到我身上。每回合仅可使用一次。
```

本切片只收窄 P0-005；不得宣称完整 target-bearing activated ability family、完整 swift timing、完整 equipment reattach branch、full official Azir、card matrix full-official 或 READY。

## 2. 输入事实

- `data/official/card-catalog.zh-CN.json` 固定 2026-04-27 快照中存在 `SFD·050/221` 与 `SFD·050a/221`，文本相同，颜色为 green。
- `docs/rules-evidence-index.md` 仅记录 `p2-preflight-play-sfd-050-azir-swap-skill-static` / `p2-preflight-play-sfd-050a-azir-swap-skill-static` 的普通手牌打出代表证据，并明确绿色支付迅捷位置交换、原位置记忆和武装贴附路径暂缓。
- `P4ActivatedAbilityCatalog.GetAll()` 当前有 26 个 ability id constants / executable representative ability ids，但没有 `SFD·050/221` 或 `SFD·050a/221` 的 Azir swift swap ability id。
- P0-005 剩余清单仍点名 target-bearing colored-cost activated abilities、单阵营/多阵营费用、Haste / Echo / Spellshield 全窗口与 trigger payment family。
- 4D-03AK 已锁定现有 `AppliesSpellshieldTargetTax=true` activated abilities；4D-03AL 已锁定现有 `IsResourceSkill=true` activated abilities。本切片不能重复这两个 verifier。

## 3. 写入范围

建议 B 侧服务端实现写锁：

- `src/Riftbound.Engine/P4ActivatedAbilityCatalog.cs`
- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `src/Riftbound.Engine/MatchSession.cs`
- `src/Riftbound.Contracts` 中已有 `ActionPrompt` / source requirement 字段，如确有缺口才最小扩展
- `tests/Riftbound.ConformanceTests/AzirSwiftSwapActivatedAbilityTests.cs` 或相邻 focused test file

建议 A/D 文档写锁：

- `docs/CURRENT_STAGE4D_03AM_PAYMENT_ENGINE_AZIR_SWIFT_SWAP_AUDIT.md`
- `docs/CURRENT_STAGE4D_03AM_PAYMENT_ENGINE_AZIR_SWIFT_SWAP_EVIDENCE.md`
- `docs/CURRENT_A_MASTER_CHECKPOINT.md`
- `docs/CURRENT_COMPLETION_AUDIT.md`
- `docs/CURRENT_SERVER_RULE_AUDIT.md`
- `docs/CURRENT_STAGE4D_P0_P1_CLOSURE_PLAN.md`

禁止本切片修改：

- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- 前端运行时代码
- unrelated LayerEngine / battle lifecycle / cleanup queue files
- unrelated activated abilities

## 4. Runtime Acceptance

最低成功路径：

1. `P4ActivatedAbilityCatalog` 为 `SFD·050/221` 与 `SFD·050a/221` 建立同一个 executable ability definition 或等价 alias。
2. `ActionPrompt` 只在服务端判定合法时公开 Azir source requirement，包含 `ACTIVATE_ABILITY`、绿色 typed cost、swift timing marker、target slot、每回合一次限制提示和 required source / target metadata。
3. 支付必须走 shared PaymentEngine / `PaymentCostRules` 口径：`SPEND_POWER:green:1` 或必要的 `RECYCLE_RUNE:<objectId>` 先产生 green power，再支付绿色费用。
4. wrong trait、generic temporary resource、重复/无效/不必要 recycle、费用不足、unsupported optional cost 均 rejected no-mutation。
5. target 必须是 controller 控制的公开正面单位对象；敌方单位、装备、符文、战场、手牌、面朝下、stale object、source self、dirty controller target 都必须 rejected no-mutation。
6. 成功命令创建可审计的 swift activated ability resolution path；不得由前端自行交换位置。
7. resolution 后 Azir 与目标单位交换位置，并同步 authoritative `ObjectLocations` / snapshot / event payload。
8. 每回合只能使用一次；同回合第二次尝试 rejected no-mutation，回合结束后限制按既有 turn-memory 清理语义恢复。
9. 如果本切片不实现 “目标已配有武装时可选择其中一件贴附到 Azir” 分支，则必须把该分支写入 audit residual risk，且成功 fixture 应使用无武装目标，不能宣称 full official Azir。

## 5. Prompt / Command Parity

必须有成对测试证明：

- prompt 公开的 source / target / payment resource choices 与 command-side acceptance 一致。
- prompt 不公开的 source / target / payment action，command 也不能绕过。
- `ActionPrompt` 只提供服务端 authoritative candidate；前端无需新增本地费用、目标、位置交换或 once-per-turn 判断。
- event payload / snapshot 足以让前端展示结果，但不要求前端推导规则。

## 6. No-Mutation Requirements

失败分支至少断言以下状态不变：

- tick / event count
- source zone、target zone、`ObjectLocations`
- source / target exhaustion and tags
- runePool mana / power / `powerByTrait`
- hand / deck / discard / equipped attachments
- stack / priority / focus
- turn-memory once-per-turn marker
- temporary payment resources

## 7. Baseline Commands

Implementation-before baseline 已建立，见 `docs/CURRENT_STAGE4D_03AM_PAYMENT_ENGINE_AZIR_SWIFT_SWAP_BASELINE_EVIDENCE.md`。

实现后建议验证：

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~AzirSwiftSwap|FullyQualifiedName~Azir|FullyQualifiedName~ActivateAbility|FullyQualifiedName~MoveUnit|FullyQualifiedName~PaymentEngine"
```

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Azir|FullyQualifiedName~ActivateAbility|FullyQualifiedName~MoveUnit|FullyQualifiedName~PaymentEngine|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub|FullyQualifiedName~Priority"
```

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
git diff --check
```

## 8. Non-Goals

- 不实现 OGN·242 海兽钓钩、SFD·090 Z 型驱动、UNL-144 守门者马杜里、OGN·023 来路不明的武器等其他 colored activated abilities。
- 不把 Azir 普通手牌打出或 Haste Azir `SFD·177` 的证据混作本技能证据。
- 不关闭完整 swift / reaction timing model。
- 不关闭完整 target-bearing colored-cost activated ability family。
- 不关闭 P0-005、P1、card matrix、frontend final validation 或 READY。

## 9. Handoff Verdict

4D-03AM 可作为下一枚 B 侧 PaymentEngine breadth implementation slice。它应该只围绕 Azir `SFD·050/221` / `SFD·050a/221` 的绿色迅捷位置交换技能建立 prompt / payment / target / command / event / rollback representative。项目仍 **NOT READY**。

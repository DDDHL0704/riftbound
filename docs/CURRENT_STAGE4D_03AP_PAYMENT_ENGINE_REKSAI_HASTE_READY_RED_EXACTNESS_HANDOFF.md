# Stage 4D-03AP PaymentEngine Rek'Sai HASTE_READY Red Exactness Handoff

日期：2026-05-15
结论：**HANDOFF READY / PROJECT NOT READY**

本 handoff 是 A 主控给下一枚 P0-005 PaymentEngine breadth 切片的写锁和验收规格。它不实现 runtime，不修改前端，不修改 card matrix。目标是把 4C-52 留下的 Rek'Sai `HASTE_READY` paid branch / red resource exactness holdback 收窄成一个可验收的 server-authoritative representative。

## 1. Target

补强并验收 `SFD·029/221` 与 `SFD·029a/221` 雷克塞的急速活跃进场红色支付精确性：

```txt
{{急速}}（你可以选择额外支付{{1}}和{{红色}}，让我以活跃状态进场。）
{{强攻}}（如果我是进攻方，则{{S}}+1。）
从手牌以外位置被打出的友方单位获得{{急速}}。
```

本切片只收窄 P0-005 / keyword optional payment / `PLAY_CARD` HASTE_READY typed-red payment representative。不得宣称 full official Rek'Sai、强攻 battle modifier、ASSIGN_COMBAT_DAMAGE overflow、从手牌以外打出友方单位获得急速、LayerEngine、FAQ full adjudication、card matrix full-official 或 READY。

## 2. Input Facts

- `data/official/card-catalog.zh-CN.json` 固定 2026-04-27 快照中存在 `SFD·029/221` / `33104` 与 `SFD·029a/221` / `33105`，energy 3，power 3，官方文本相同。
- `docs/rules-evidence-index.md` 已记录 4C-52 ordinary no-optional play-unit + keyword tag guard，以及 P4 `p4-play-reksai-haste-ready` / `p4-play-reksai-alt-a-haste-ready` fixture rows；这些旧 rows 仍把 red resource exactness、强攻、非手牌授予急速等列为 residual。
- `docs/CURRENT_CARD_EFFECT_RISK_TOP20.md` §55E 仍明确 `HASTE_READY` paid branch full matrix、red resource exactness、Overwhelm / 强攻 battle modifier、`ASSIGN_COMBAT_DAMAGE` overflow、non-hand friendly unit gains haste、LayerEngine、hidden-info and FAQ adjudication remain holdback。
- `src/Riftbound.Engine/CardBehaviorRegistry.cs` 当前为 `SFD·029/221` 与 `SFD·029a/221` 记录 `HasteReadyManaCost: 1`、`HasteReadyPowerCost: 1`、`HasteReadyPowerTrait: RuneTrait.Red`。
- `src/Riftbound.Engine/CardPermissionKeywordRules.cs` 会把 `HASTE_READY` optional cost 解析为 extra mana + typed trait power。
- `src/Riftbound.Engine/CoreRuleEngine.cs` 当前 `PLAY_CARD` payment plan 使用 shared `PaymentCostRules` 与 `powerCostByTrait`，理论上可 enforce typed red cost；本切片需要用 focused tests / audit 把该行为锁住。
- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 将该 card family 记为 `FU-1945f6918c`，functional unit size 2，`stage4B.fullOfficial=false`，flags 包含 `IMPLEMENTED_TESTED`、`SHARED_ORACLE_IMPLEMENTATION`、`NEEDS_ENGINE_SUPPORT` 与 `NEEDS_FAQ_REVIEW`。

## 3. Suggested B Write Scope

Default test / evidence write scope:

- `tests/Riftbound.ConformanceTests/ReksaiHasteReadyRedPaymentTests.cs` or an adjacent focused test file.
- Minimal append-only edits to `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs` only if reusing existing fixture helpers is clearly smaller.

Runtime write scope only if focused tests reveal an actual code gap:

- `src/Riftbound.Engine/CardPermissionKeywordRules.cs`
- `src/Riftbound.Engine/CardBehaviorRegistry.cs`
- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `src/Riftbound.Engine/PaymentCostRules.cs` only if shared typed-power authorization itself is wrong.

Suggested A/D doc write scope after implementation:

- `docs/CURRENT_STAGE4D_03AP_PAYMENT_ENGINE_REKSAI_HASTE_READY_RED_EXACTNESS_AUDIT.md`
- `docs/CURRENT_STAGE4D_03AP_PAYMENT_ENGINE_REKSAI_HASTE_READY_RED_EXACTNESS_EVIDENCE.md`
- checkpoint / completion audit / server audit / closure plan

Forbidden in this slice:

- frontend runtime
- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- broad Haste keyword rewrite
- strong/overwhelm battle modifier implementation
- `ASSIGN_COMBAT_DAMAGE` overflow implementation
- non-hand friendly unit gains haste implementation
- LayerEngine / cleanup / battle lifecycle broad changes
- unrelated `PLAY_CARD` optional branches
- `riftbound-dotnet.sln`

## 4. Runtime / Test Acceptance

Minimum acceptance:

1. `CardBehaviorRegistry` / `CardPermissionKeywordRules.BuildProfile` prove both Rek'Sai printings expose `HASTE_READY` as implemented representative with extra 1 mana + 1 red typed power.
2. `ActionPrompt` for a legal Rek'Sai hand source exposes `PLAY_CARD`, `HASTE_READY`, base cost 3, total HASTE_READY cost 4 mana + 1 red power, and server-provided payment-resource choices when an eligible red rune can be recycled.
3. Command with existing red power and `HASTE_READY` succeeds for both `SFD·029/221` and `SFD·029a/221`, emits shared `COST_PAID` payload with `baseManaCost=3`, `totalManaCost=4`, `genericPower=0`, `totalPowerCost=1`, `powerByTrait.red=1`, and resolves the unit active in base with `hasteReadyOptionalCostPaid=true`.
4. Command with necessary `RECYCLE_RUNE:<redRuneObjectId>` plus `HASTE_READY` succeeds, recycles that red rune, records `paymentResourceActions` / `recycledRuneObjectIds`, and spends typed red power through `PaymentCostRules`.
5. Wrong trait power, generic temporary resource, insufficient red, duplicate / invalid / unnecessary recycle and unsupported optional cost are rejected no-mutation.
6. Command cannot bypass prompt by sending targets for the no-target Rek'Sai play route.
7. Failure branches assert unchanged tick, events, hand/base/rune deck zones, `ObjectLocations`, stack, rune mana, generic power and `powerByTrait`.
8. Existing no-optional Rek'Sai play-unit and P4 fixture evidence remains green.

## 5. Prompt / Command Parity

Required parity checks:

- prompt `optionalCostChoices` includes `HASTE_READY` exactly when command accepts it.
- prompt `paymentResourceChoices` for red rune recycle matches command-side accepted `RECYCLE_RUNE:*`.
- wrong-trait prompt resources are not presented as legal red payment-resource choices and command rejects them if forced.
- command-side `powerByTrait` audit payload uses red, not generic power.

## 6. Non-Goals

- Do not implement Rek'Sai 强攻 / Overwhelm battle modifier.
- Do not implement damage assignment overflow.
- Do not implement "friendly units played from outside hand gain Haste".
- Do not update card matrix full-official status.
- Do not close P0-005, P1, frontend final validation or READY.

## 7. Suggested Verification

Implementation-before baseline is recorded in `docs/CURRENT_STAGE4D_03AP_PAYMENT_ENGINE_REKSAI_HASTE_READY_RED_EXACTNESS_BASELINE_EVIDENCE.md`.

Post-implementation:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ReksaiHasteReady|FullyQualifiedName~Reksai|FullyQualifiedName~HasteOptional|FullyQualifiedName~PaymentEngine"
```

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Reksai|FullyQualifiedName~HasteOptional|FullyQualifiedName~PaymentEngine|FullyQualifiedName~PlayCard|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub|FullyQualifiedName~Priority"
```

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
git diff --check
```

## 8. Handoff Verdict

4D-03AP is ready as the next focused P0-005 test / evidence slice. It should lock Rek'Sai's HASTE_READY red typed payment exactness without broadening into battle modifier, non-hand haste granting, LayerEngine, frontend or card matrix work. Project remains **NOT READY**.

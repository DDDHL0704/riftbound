# Stage 4D-03AP PaymentEngine Rek'Sai HASTE_READY Red Exactness Audit

日期：2026-05-15
结论：**4D-03AP-B ACCEPTED / PROJECT NOT READY**

本审计验收 4D-03AP-B 的最小测试 / 证据补强。该切片只锁定 Rek'Sai `HASTE_READY` extra 1 mana + 1 red typed power 的 prompt / command / audit exactness；不修改 runtime、不修改 frontend、不更新 card matrix。

## 1. Scope

Accepted file:

- `tests/Riftbound.ConformanceTests/ReksaiHasteReadyRedPaymentTests.cs`

No runtime files changed.

Target official cards:

- `SFD·029/221` / `33104` / 雷克塞
- `SFD·029a/221` / `33105` / 雷克塞

## 2. Acceptance Findings

The focused tests prove:

1. Both printings expose `HASTE_READY` as implemented representative with `HasteReadyManaCost=1`, `HasteReadyPowerCost=1` and `HasteReadyPowerTrait=RuneTrait.Red`.
2. Server `ActionPrompt` source requirement exposes no-target `PLAY_CARD`, `HASTE_READY`, red typed payment metadata and a red `RECYCLE_RUNE:*` payment-resource choice only when that red recycle can satisfy the shortfall.
3. Prompt does not offer wrong-trait green rune recycle as a legal red payment-resource choice.
4. Existing red power + `HASTE_READY` succeeds for both printings, emits `COST_PAID` with `baseManaCost=3`, `totalManaCost=4`, `genericPower=0`, `totalPowerCost=1`, `powerByTrait.red=1`, and resolves to base active with `hasteReadyOptionalCostPaid=true`.
5. Necessary `RECYCLE_RUNE:<redRuneObjectId>` + `HASTE_READY` succeeds, moves that rune back to rune deck, emits `RUNE_RECYCLED` / `POWER_GAINED`, and records `paymentResourceActions` / `recycledRuneObjectIds`.
6. Wrong trait power, generic temporary resource, insufficient red, wrong-trait recycle, duplicate recycle, invalid recycle, unnecessary recycle, unsupported optional cost and submitted target all reject no-mutation via `MatchStateHasher`.

## 3. Residuals

Still deferred and not closed by this slice:

- Rek'Sai strong / Overwhelm battle modifier.
- `ASSIGN_COMBAT_DAMAGE` overflow behavior.
- "Friendly units played from outside hand gain Haste".
- LayerEngine / continuous effects.
- Hidden-info / FAQ full adjudication.
- Card matrix full-official status.
- P0-005, P1, frontend final validation and READY.

## 4. Verification

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ReksaiHasteReadyRedPaymentTests"
```

Result: 17/17 passed.

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ReksaiHasteReady|FullyQualifiedName~Reksai|FullyQualifiedName~HasteOptional|FullyQualifiedName~PaymentEngine"
```

Result: 126/126 passed.

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Reksai|FullyQualifiedName~HasteOptional|FullyQualifiedName~PaymentEngine|FullyQualifiedName~PlayCard|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub|FullyQualifiedName~Priority"
```

Result: 442/442 passed.

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

Result: 4338/4338 passed.

```sh
git diff --check
```

Result: passed.

## 5. Verdict

4D-03AP-B is accepted as a narrow P0-005 keyword payment exactness guard. It improves confidence that Rek'Sai HASTE_READY uses red typed payment through the server PaymentEngine, but it does not close full official Rek'Sai or project READY.

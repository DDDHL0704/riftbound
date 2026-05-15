# Stage 4D-03AP Rek'Sai HASTE_READY Red Exactness Baseline Evidence

日期：2026-05-15
结论：**BASELINE RECORDED / IMPLEMENTATION NOT STARTED / PROJECT NOT READY**

This baseline records the pre-implementation state for the next P0-005 keyword optional payment representative: `SFD·029/221` / `SFD·029a/221` Rek'Sai HASTE_READY red typed payment exactness.

## 1. Current Evidence

- Official catalog text exists in `data/official/card-catalog.zh-CN.json`: `{{急速}}（你可以选择额外支付{{1}}和{{红色}}，让我以活跃状态进场。）` / `{{强攻}}（如果我是进攻方，则{{S}}+1。）` / `从手牌以外位置被打出的友方单位获得{{急速}}。`
- Current 4C-52 evidence covers ordinary no-optional hand play, keyword tags, invalid input no-mutation, and old P4 HASTE_READY fixtures.
- Current risk docs still list `HASTE_READY` paid branch full matrix and red resource exactness as holdback for `FU-1945f6918c`.
- Current implementation already has a likely runtime path: `HasteReadyPowerTrait: RuneTrait.Red` in `CardBehaviorRegistry`, profile exposure in `CardPermissionKeywordRules`, and typed `powerCostByTrait` payment plan in `CoreRuleEngine`.
- Missing A-accepted focused evidence: Rek'Sai-specific prompt / command / cost audit / wrong-trait / recycle no-mutation tests for red exactness.

## 2. Baseline Commands

Focused adjacent baseline:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Reksai|FullyQualifiedName~HasteOptional|FullyQualifiedName~PaymentEngine"
```

Result: 109/109 passed.

Broader adjacent baseline:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Reksai|FullyQualifiedName~HasteOptional|FullyQualifiedName~PaymentEngine|FullyQualifiedName~PlayCard|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub|FullyQualifiedName~Priority"
```

Result: 425/425 passed.

Whitespace / diff baseline:

```sh
git diff --check
```

Result: passed.

## 3. Baseline Verdict

The adjacent test surface is green before adding the Rek'Sai HASTE_READY red exactness focused guard. This baseline does not implement runtime behavior and does not close P0-005, full official Rek'Sai or READY.

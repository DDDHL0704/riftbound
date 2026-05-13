# Stage 4D-03J PaymentEngine Resource Skill Lifecycle Acceptance Checklist

日期：2026-05-14
结论：**ACCEPTED / PROJECT NOT READY**

本文是 A 主控对 4D-03J B 侧实现的验收清单。B worker Raman 产出初稿后已暂停，A 收回写入锁并完成修补、复核、测试与审计；正式 accepted audit 见 `docs/CURRENT_STAGE4D_03J_PAYMENT_ENGINE_RESOURCE_SKILL_LIFECYCLE_AUDIT.md`。

## 1. Objective Restatement

4D-03J 只验收 `OGN·113/298` 玛尔扎哈 `[A A]` resource skill 在 4D-03I open-main representative 之外的 lifecycle 收窄：

- spell-duel / swift timing：`SPELL_DUEL_OPEN` 且当前玩家是 `FocusPlayerId` 时允许使用。
- reaction prohibition：该 resource skill 不创建普通可反应 stack item，不能成为 counter / reaction spell 的目标。
- payment-only lifecycle：生成的 2 点通用符能必须有服务端可审计、可约束、可清理的 payment-only lifecycle。

本切片仍不关闭 P0-005，不升级完整 `[A]` / `[C]` family，不修改 coverage matrix，不修改前端。

## 2. Prompt-To-Artifact Checklist

| Requirement | Required Evidence | Status |
| --- | --- | --- |
| `SPELL_DUEL_OPEN && FocusPlayerId == playerId` 公开 `ACTIVATE_ABILITY` | `MatchSession.BuildPrompts` / `ActionPrompt` diff；spell-duel focus prompt test | Accepted |
| 非焦点玩家不公开 Malzahar resource skill | prompt guard test | Accepted |
| closed timing / ordinary stack priority / cleanup blocking 不公开 | prompt or command no-mutation tests | Accepted |
| Command 接受 open-main 与 spell-duel focus 两类 timing | `CoreRuleEngine.ResolveMalzaharResourceSkill` diff；open-main regression 与 spell-duel success test | Accepted |
| wrong focus / wrong timing rejected no-mutation | snapshot or cloned-state no-mutation assertions | Accepted |
| Spell-duel 成功后保持 `TimingState=SPELL_DUEL_OPEN` | success test asserts timing / focus state | Accepted |
| 成功路径不创建普通 stack item | event and state assertions: no `STACK_ITEM_ADDED`, stack empty | Accepted |
| counter / reaction choices 不包含 resource skill | immediate no-stack success and post-result prompt assertions | Accepted representative |
| payment-only resource 不是无约束永久 `RunePool.Power` | temporary ledger / state / snapshot / payment commit diff and test | Accepted representative |
| payment-only resource 只能支付 rune-cost window | negative mana-only test and positive generic rune-cost payment test | Accepted representative |
| payment step 后 cleanup / remaining lifecycle 可审计 | spent / cleared event assertions and end-turn cleanup diff | Accepted representative |
| 4D-03I open-main path 不回退 | existing Malzahar open-main focused tests | Accepted |
| Vi / Xerath / PaymentEngine adjacent paths 不回退 | adjacent filter test | Accepted |
| 不修改 coverage matrix / frontend / `riftbound-dotnet.sln` | `git status --short`, `git diff --name-only` | Accepted |

## 3. Required Commands

Focused:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Malzahar|FullyQualifiedName~SpellDuel|FullyQualifiedName~Reaction|FullyQualifiedName~PaymentEngineUnificationTests|FullyQualifiedName~ResourceSkill"
```

Adjacent:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ActivateAbility|FullyQualifiedName~SpellDuel|FullyQualifiedName~Priority|FullyQualifiedName~Reaction|FullyQualifiedName~PaymentResource|FullyQualifiedName~SpendPower|FullyQualifiedName~RunePool|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub"
```

Formatting:

```sh
git diff --check
```

Backend full is required if A judges the diff touches shared payment lifecycle, timing state, or prompt generation broadly:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

## 4. A Review Notes

- Verify tests fail against the old 4D-03I behavior in principle: old tests that assert spell-duel does not open must be replaced, not merely left alongside new code.
- Do not accept event metadata alone as proof of payment-only lifecycle if state still permanently merges the generated power into unrestricted `RunePool.Power`.
- Do not accept “no stack item” alone as full reaction prohibition unless target-choice / reaction prompt evidence also proves there is no reaction target exposure.
- If B leaves payment-only lifecycle partial, record explicit residual risk and keep P0-005 open.

## 5. A Result

- Focused regression: 116/116 passed.
- Adjacent regression: 340/340 passed.
- Backend full: 3847/3847 passed.
- `git diff --check`: no output.
- Accepted audit / evidence: `docs/CURRENT_STAGE4D_03J_PAYMENT_ENGINE_RESOURCE_SKILL_LIFECYCLE_AUDIT.md` and `docs/CURRENT_STAGE4D_03J_PAYMENT_ENGINE_RESOURCE_SKILL_LIFECYCLE_EVIDENCE.md`.

# Stage 4D-03O PaymentEngine Crimson Rose Ready Unit Audit

日期：2026-05-14
结论：**4D-03O FOCUSED SLICE ACCEPTED / PROJECT NOT READY**

本审计验收 4D-03O Crimson Rose ready-unit focused slice。A 主控结论：本切片满足 `docs/CURRENT_STAGE4D_03O_PAYMENT_ENGINE_CRIMSON_ROSE_READY_UNIT_HANDOFF.md` 的最小推进要求，把 `UNL-109/219` 猩红玫瑰 `消耗3经验，{{横置}}：让一名单位变为活跃状态` 接入服务端 authoritative prompt / command / stack resolution / audit representative；但 P0-005 仍不宣称 full-official resolved，项目整体仍 **NOT READY**。

## 1. Accepted Evidence

- 实现文件：
  - `src/Riftbound.Engine/P4ActivatedAbilityCatalog.cs`
  - `src/Riftbound.Engine/CoreRuleEngine.cs`
  - `src/Riftbound.Engine/MatchSession.cs`
- 更新测试：
  - `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
  - `tests/Riftbound.ConformanceTests/CrimsonRoseActivatedAbilityTests.cs`
- 证据记录：
  - `docs/CURRENT_STAGE4D_03O_PAYMENT_ENGINE_CRIMSON_ROSE_READY_UNIT_EVIDENCE.md`
- Focused regression：169/169 passed
- Adjacent regression：396/396 passed
- Backend full：3940/3940 passed
- Whitespace：`git diff --check` no output

## 2. A Review Notes

- `P4ActivatedAbilityCatalog` now exposes `CRIMSON_ROSE_EXPERIENCE3_EXHAUST_READY_UNIT` as an executable representative with `ExperienceCost=3`, `RequiredTargetCount=1`, `ExhaustsSourceAsCost=true`, `AppliesSpellshieldTargetTax=true` and controlled base equipment source semantics.
- The previous `DEFERRED_EXPERIENCE_EXHAUST_READY_UNIT` surface is removed while Fluft Poro token creation and Shadow swift stun deferred surfaces remain.
- `ActionPromptBuilder` exposes Crimson Rose only from current-player controlled, public, face-up, ready `UNL-109/219` base equipment in the current active player's open-main window.
- Prompt metadata includes `experienceCost=3`, no base mana / power cost, `requiredTargetCount=1`, target choices for public units in base or battlefield, `exhaustsSource=true`, enemy Spellshield target tax policy and ordinary stack-before-ready semantics.
- Enemy Spellshield targets are filtered by current mana availability and taxed through the same target-tax helper used by Xerath; friendly Spellshield targets pay no tax.
- `CoreRuleEngine.ResolveCrimsonRoseReadyAbility` rejects wrong timing, invalid source, non-base source, exhausted / face-down / wrong-controller / wrong-card source, missing / too many / invalid targets, insufficient experience, insufficient target-tax mana, unsupported optional costs, `RECYCLE_RUNE:*` and `TEMP_PAYMENT_RESOURCE:*` with no-mutation coverage.
- Successful activation uses shared `PaymentPlan` / `AuthorizePayment` / `TryCommitPayment`, pays 3 experience plus any enemy Spellshield mana tax, exhausts the source after payment succeeds, emits audit metadata and creates an ordinary stack item without immediately readying the target.
- Pass-pass stack resolution writes `ABILITY_RESOLVED` / `UNIT_READIED`, sets the target unit active and leaves Crimson Rose in the controller base exhausted.
- Raman produced the B-side draft under the 4D-03O write lock. A reviewed the diff and reran focused, adjacent, backend full and whitespace validation. Raman did not commit.

## 3. Remaining No-Ready Items

- P0-005 remains open: this slice covers one target-bearing equipment activated ready-unit representative, not the full PaymentEngine or complete activated ability family.
- Crimson Rose first-line unit-play optional pay-1 gain-experience trigger remains deferred.
- Fluft Poro Warhawk token creation, Shadow swift stun, broader `[A]` / `[C]` resource skills, complete target-bearing skill family and full Spellshield target-tax propagation remain open.
- Full payment quote parity across every window, replacement / optional / extra cost breadth, P1 LayerEngine / keyword pass, 1009/811 matrix completion and final Browser / hidden-info / replay-hash audit remain open.

## 4. Next Step

Continue P0-005 breadth. Highest-value next candidates are Shadow swift stun, Fluft Poro token creation, the Crimson Rose first-line experience trigger or a wider resource skill/payment-window representative, depending on which residual surface gives the best coverage without mixing unrelated rule families.

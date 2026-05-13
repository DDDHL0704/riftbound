# Stage 4D-03R PaymentEngine Rage Sigil Typed Resource Audit

日期：2026-05-14
结论：**4D-03R FOCUSED SLICE ACCEPTED / PROJECT NOT READY**

本审计验收 4D-03R Rage Sigil typed resource focused slice。A 主控结论：本切片满足 `docs/CURRENT_STAGE4D_03R_PAYMENT_ENGINE_RAGE_SIGIL_TYPED_RESOURCE_HANDOFF.md` 的最小推进要求，把 `SFD·222/221` 暴怒之印 `{{横置}}：{{反应}}—{{获得}}{{红色}}，用以支付符能费用` 接入服务端 authoritative prompt / command / typed temporary payment ledger / audit representative；但 P0-005 仍不宣称 full-official resolved，项目整体仍 **NOT READY**。

## 1. Accepted Evidence

- 实现文件：
  - `src/Riftbound.Engine/P4ActivatedAbilityCatalog.cs`
  - `src/Riftbound.Engine/CoreRuleEngine.cs`
  - `src/Riftbound.Engine/MatchSession.cs`
- 更新测试：
  - `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
  - `tests/Riftbound.ConformanceTests/RageSigilResourceSkillTests.cs`
- 证据记录：
  - `docs/CURRENT_STAGE4D_03R_PAYMENT_ENGINE_RAGE_SIGIL_TYPED_RESOURCE_EVIDENCE.md`
- Focused regression：191/191 passed
- Adjacent regression：439/439 passed
- Backend full：4021/4021 passed
- Whitespace：`git diff --check` no output

## 2. A Review Notes

- `P4ActivatedAbilityCatalog` now exposes `RAGE_SIGIL_REACTION_EXHAUST_GAIN_1_RED_POWER` as an executable representative with `IsResourceSkill=true`, `ReactionSpeed=true`, `PaymentOnlyResource=true`, `RequiresBaseEquipmentSource=true`, `ExhaustsSourceAsCost=true`, `RequiredTargetCount=0` and `GeneratedPowerByTrait.red=1`.
- `TemporaryPaymentResourceState` now carries typed `GeneratedPowerByTrait` / `RemainingPowerByTrait` while preserving existing generic temporary resource fields for Malzahar.
- `ActionPromptBuilder` exposes Rage Sigil only in the existing stack-priority reaction representative window, from current-player controlled, public, face-up, ready base equipment `SFD·222/221` sources.
- Prompt metadata includes `resourceSkill=true`, `reactionSpeed=true`, `typedPaymentOnlyResource=true`, `requiresBaseEquipmentSource=true`, `exhaustsSource=true`, `generatedPowerByTrait.red=1`, `resourceRestriction=PAY_RUNE_COSTS_ONLY_TYPED_RED_TEMPORARY_LEDGER_4D_03R` and `resourceLifecycle=temporary-payment-resource-ledger`.
- `CoreRuleEngine.ResolveRageSigilResourceSkill` rejects targets, optional costs and payment resource actions on activation; success exhausts the source, creates a red typed payment-only temporary ledger, emits resource-skill audit events and does not create an ordinary stack item.
- Temporary payment resource quote / commit now understands typed remaining power. Rage Sigil red resource can pay red typed rune cost and, by existing typed-power semantics, generic rune cost; it rejects blue typed-only, mana-only and non-rune payment misuse no-mutation.
- Lifecycle cleanup follows the temporary payment ledger path: consumed resources are removed and audit events report consumed / remaining typed power.
- Raman produced the B-side draft under the 4D-03R write lock. A reviewed the diff and reran focused, adjacent, backend full and whitespace validation. Raman did not commit.

## 3. Remaining No-Ready Items

- P0-005 remains open: this slice covers one SFD Rage Sigil typed red payment-only resource skill representative, not the full Sigil family, OGN variants, full `[A]` / `[C]` resource skill family, complete PaymentEngine quote parity or every payment window.
- Reaction lifecycle remains representative-scoped: this slice reuses the existing stack-priority reaction window and does not implement a complete reaction/counter target model.
- LayerEngine / keywords, 1009/811 full-official matrix, final Browser / hidden-info / replay-hash audit and final completion audit remain open.

## 4. Next Step

Continue P0-005 breadth after committing 4D-03R. Highest-value next candidates are wider `[A]` / `[C]` resource skill coverage, remaining target-bearing/payment windows, or typed temporary resource inline quote parity across more official cost profiles.

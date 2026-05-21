# 4D-03KS-E Audit - Cold-Blooded Aristocrat Destroy-Friendly-Unit FAQ Cleanup

本审计记录 4D-03KS-E 对 `FU-a597c5db86` / `OGN·208/298`《冷血贵族》 / `COLD_BLOODED_ARISTOCRAT_DESTROY_FRIENDLY_UNIT_STATIC` 的 row-level blocker closure candidate。结论限定为：已有支付 4 点费用、摧毁友方单位作为强制额外费用、0 目标入栈、双方让过后源牌进入控制者基地并成为 6 战力单位对象的代表路径，足以移除一枚 `NEEDS_ENGINE_SUPPORT` blocker；完整 automated evidence disposition、FAQ adjudication、cleanup / replacement-duration breadth 和 full PaymentEngine 仍 open。

证据：

- Official catalog row: `OGN·208/298` / `COLD_BLOODED_ARISTOCRAT_DESTROY_FRIENDLY_UNIT_STATIC`.
- Runtime binding: `src/Riftbound.Engine/CardBehaviorRegistry.cs`.
- Fixture: `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-cold-blooded-aristocrat-destroy-friendly-unit.fixture.json`.
- Positive execution: `CoreRuleEnginePlaysColdBloodedAristocratDestroyFriendlyUnitAdditionalCost`.
- Negative execution: `CoreRuleEngineRejectsColdBloodedAristocratWithoutAdditionalCost` and `CoreRuleEngineRejectsColdBloodedAristocratWhenAdditionalCostTargetsEquipment`.
- Evidence index: `docs/rules-evidence-index.md` / `p2-preflight-play-cold-blooded-aristocrat-destroy-friendly-unit`.
- Preflight doc: `docs/p2-rules-preflight.md`.

规则边界：

- `BREAK-JFAQ-260416 p2` remains a FAQ review source; this batch does not adjudicate every FAQ consequence.
- `CORE-260330 p42` and related play/stack/payment references remain evidence for the representative play route.
- Cleanup and replacement-duration breadth remains open because this batch only records the selected cost unit entering graveyard through the representative cost path, not every simultaneous cleanup/replacement edge.
- The candidate does not prove the whole additional-cost family or full PAY_COST official matrix.

审计结论：

- Selected row: `FU-a597c5db86` / `OGN·208/298` / `COLD_BLOODED_ARISTOCRAT_DESTROY_FRIENDLY_UNIT_STATIC`.
- Row transition: `IMPLEMENTED_UNTESTED + NEEDS_ENGINE_SUPPORT + NEEDS_FAQ_REVIEW` -> `IMPLEMENTED_UNTESTED + NEEDS_FAQ_REVIEW`.
- `fullOfficialBlockers`: `NEEDS_ENGINE_SUPPORT + NEEDS_FAQ_REVIEW + NEEDS_AUTOMATED_TEST_EVIDENCE` -> `NEEDS_FAQ_REVIEW + NEEDS_AUTOMATED_TEST_EVIDENCE`.
- `fullOfficial` remains false.
- READY remains open.

Validation passed: matrix JSON valid (jq empty); PaymentEngineCoverageAuditTests 576/576; Cold-Blooded Aristocrat focused 3021/3021; adjacent prompt/payment/FAQ/cleanup 410/410; backend full 5147/5147; git diff --check passed.

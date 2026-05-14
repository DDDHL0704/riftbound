# Stage 4D-03AH PaymentEngine Action-Window Coverage Baseline Evidence

日期：2026-05-14
结论：**BASELINE READY / PROJECT NOT READY**

本文件记录 4D-03AH implementation 前基线。该基线只证明当前 PaymentEngine / movement / prompt adjacent representative tests 为绿色；它不证明 action-window coverage verifier 已存在，也不关闭 P0-005。

## Baseline Findings

- 4D-03AF identified that full official PaymentEngine closure still lacks a single action-window coverage verifier / checklist.
- 4D-03AG fixed the concrete `PLAY_CARD` typed resource prompt parity gap before this verifier.
- No existing `PaymentEngineCoverageAuditTests` or equivalent generated classification verifier exists yet.
- `MOVE_UNIT` remains a movement-permission optional-cost / non-resource policy window for current representatives.

## Validation Commands

Focused PaymentEngine / movement / prompt adjacent baseline:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PaymentEngineUnificationTests|FullyQualifiedName~TriggerPaymentTests|FullyQualifiedName~MoveUnit|FullyQualifiedName~ActivateAbility|FullyQualifiedName~Haste|FullyQualifiedName~Echo|FullyQualifiedName~Spellshield|FullyQualifiedName~AssembleEquipment|FullyQualifiedName~ResourceSkill|FullyQualifiedName~RunePool|FullyQualifiedName~GameHub"
```

Result: passed 712/712.

Diff hygiene:

```sh
git diff --check
```

Result: passed.

## Verdict

4D-03AH is ready to hand off for a test-only action-window coverage verifier after baseline validation completes. Project remains **NOT READY**.

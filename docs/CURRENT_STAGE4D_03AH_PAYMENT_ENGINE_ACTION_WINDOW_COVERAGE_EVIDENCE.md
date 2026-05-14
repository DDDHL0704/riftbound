# Stage 4D-03AH PaymentEngine Action-Window Coverage Evidence

日期：2026-05-14
结论：**FOCUSED VERIFIED / PROJECT NOT READY**

本文件记录 4D-03AH verifier implementation 后的验证结果。该切片只新增 conformance audit test 与审计文档，不修改 runtime，不修改前端，不修改 coverage matrix，不关闭 P0-005 / P1 / READY。

## Validation Commands

Focused PaymentEngine / movement / prompt adjacent regression:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PaymentEngineCoverageAuditTests|FullyQualifiedName~PaymentEngineUnificationTests|FullyQualifiedName~TriggerPaymentTests|FullyQualifiedName~MoveUnit|FullyQualifiedName~ActivateAbility|FullyQualifiedName~Haste|FullyQualifiedName~Echo|FullyQualifiedName~Spellshield|FullyQualifiedName~AssembleEquipment|FullyQualifiedName~ResourceSkill|FullyQualifiedName~RunePool|FullyQualifiedName~GameHub"
```

Result: passed 717/717.

Backend full:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

Result: passed 4182/4182.

Diff hygiene:

```sh
git diff --check
```

Result: passed.

## Evidence Summary

- `PaymentEngineCoverageAuditTests` lists every required action window exactly once: `PLAY_CARD`, `PAY_COST`, `TRIGGER_PAYMENT`, `ASSEMBLE_EQUIPMENT`, `ACTIVATE_ABILITY`, `LEGEND_ACT`, `BATTLEFIELD_HELD_SCORE_PAYMENT`, `HIDE_CARD`, and `MOVE_UNIT`.
- Every entry has classification, evidence summary, test anchors, doc anchors, and explicit closure status.
- `PLAY_CARD` is locked to 4D-03AG typed resource prompt parity evidence.
- `MOVE_UNIT` is locked as `policy-non-resource`, with explicit movement-permission / optional-cost policy wording and no resource-payment claim.
- All manifest closure statuses keep project **NOT READY** and P0-005 open.

## Residual Risk

This verifier is an audit checklist, not a new runtime feature. It does not prove full official PaymentEngine closure for all future cards, all replacement windows, all LayerEngine interactions, or all P1 keyword families. Project remains **NOT READY**.

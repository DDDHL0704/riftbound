# Stage 4D-03AF PaymentEngine Remaining Scope Baseline Evidence

日期：2026-05-14
结论：**BASELINE READY / PROJECT NOT READY**

本文件记录 4D-03AF A 侧审计基线。该基线只证明当前 PaymentEngine / movement / activated ability / keyword adjacent representative tests 仍为绿色；它不关闭 P0-005。

## Baseline Findings

- `PaymentCostRules.PaymentPlan` remains the shared primitive for rune / experience payment windows.
- pending temporary resource prompt aggregate parity from 4D-03AE remains part of the focused baseline.
- `P4ActivatedAbilityCatalog.GetDeferredSurfaces()` is empty, so current P4 deferred catalog is not the next blocker.
- `MOVE_UNIT` has direct movement resolver coverage for coarse movement, precise `ROAM`, and Baron Nest no-ROAM movement, but lacks an explicit PaymentEngine action-window coverage policy.
- Full official PaymentEngine closure still needs an action-window coverage verifier or equivalent checklist before any P0-005 closure claim.

## Validation Commands

Focused PaymentEngine / movement / activated ability / keyword adjacent baseline:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PaymentEngineUnificationTests|FullyQualifiedName~TriggerPaymentTests|FullyQualifiedName~MoveUnit|FullyQualifiedName~ActivateAbility|FullyQualifiedName~Haste|FullyQualifiedName~Echo|FullyQualifiedName~Spellshield|FullyQualifiedName~AssembleEquipment|FullyQualifiedName~ResourceSkill|FullyQualifiedName~RunePool"
```

Result: passed 587/587.

Diff hygiene:

```sh
git diff --check
```

Result: passed.

## Verdict

4D-03AF baseline is green for the audited representative surfaces. The next useful step is an action-window coverage verifier / handoff, not a READY claim.

# Stage 4D Next Dispatch and Writelocks

日期：2026-05-15
结论：**CURRENT BATCH ACCEPTED / PROJECT NOT READY**

本文件是 A 主控对下一批 B/C/D/E 工作的调度队列与写锁边界。它只做 planning / handoff / acceptance 归档，不实现 runtime，不修改前端，不修改测试代码，不修改 card matrix。当前 active goal 仍未完成，不得调用 `update_goal complete`。

## 1. 输入事实

- 当前分支为 `main`，仓库当前只保留未跟踪 `riftbound-dotnet.sln`；该文件不得被本批任务触碰或纳入提交。
- `docs/CURRENT_STAGE4D_03AN_PAYMENT_ENGINE_GATEKEEPER_MADULI_PURPLE_MOVE_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03AN_PAYMENT_ENGINE_GATEKEEPER_MADULI_PURPLE_MOVE_EVIDENCE.md` 已记录 4D-03AN-B implemented / A-validated：focused 25/25、handoff focused 188/188、adjacent 381/381、backend full 4293/4293、`git diff --check` 通过。
- `docs/CURRENT_STAGE4D_03AM_PAYMENT_ENGINE_AZIR_SWIFT_SWAP_HANDOFF.md` 已把下一枚 P0-005 implementation slice 锁定为 `SFD·050/221` / `SFD·050a/221` 阿兹尔 green swift position-swap activated ability representative。
- `docs/CURRENT_STAGE4D_03AM_PAYMENT_ENGINE_AZIR_SWIFT_SWAP_BASELINE_EVIDENCE.md` 已记录 implementation-before baseline：Azir / ActivateAbility / MoveUnit / PaymentEngine / ActionPrompt / GameHub / Priority 361/361 通过；PaymentEngineCoverageAuditTests / ActivateAbility / Renata / CrimsonRose / Shadow / Xerath / Fluft / Spellshield / ActionPrompt / GameHub 449/449 通过。
- `docs/CURRENT_ACTIVE_GOAL_PROMPT_ARTIFACT_CHECKLIST.md` 已确认 active goal 的 READY 门槛仍未满足：P0/P1 未清零，1009 / 811 card matrix 仍无 full-official coverage，frontend build / Chrome smoke / formal 18-step 仍需在最终代码状态 fresh run。
- A 当前只完成下一批调度与写锁记录；没有向任何 runtime implementation agent 发出新写入任务。

## 2. Dispatch Queue

| Queue | Owner | Status | Purpose | Write scope | Must not touch |
|---|---|---|---|---|---|
| 4D-NEXT-A | A 主控 | This batch done after this doc is committed | 记录下一批任务、写锁、验收与暂停点 | `docs/CURRENT_STAGE4D_NEXT_DISPATCH_AND_WRITELOCKS.md`、checkpoint / audit / closure docs | `src/**`、`tests/**`、frontend runtime、card matrix |
| 4D-03AM-B | B-Implementation / Raman `019e2257-8d40-7630-9201-28df44dd689a` | Implemented and A-validated in `docs/CURRENT_STAGE4D_03AM_PAYMENT_ENGINE_AZIR_SWIFT_SWAP_AUDIT.md` | 实现 Azir green swift position-swap representative | `src/Riftbound.Engine/P4ActivatedAbilityCatalog.cs`、`src/Riftbound.Engine/CoreRuleEngine.cs`、`src/Riftbound.Engine/MatchSession.cs`、focused Azir tests | frontend runtime、card matrix JSON、LayerEngine、battle lifecycle / cleanup queue unrelated files、unrelated activated abilities、`riftbound-dotnet.sln` |
| 4D-03AN-B | B-Implementation / Raman `019e2257-8d40-7630-9201-28df44dd689a` | Implemented and A-validated in `docs/CURRENT_STAGE4D_03AN_PAYMENT_ENGINE_GATEKEEPER_MADULI_PURPLE_MOVE_AUDIT.md` | 实现 Gatekeeper Maduli purple enemy-battlefield move representative | `src/Riftbound.Engine/P4ActivatedAbilityCatalog.cs`、`src/Riftbound.Engine/CoreRuleEngine.cs`、`src/Riftbound.Engine/MatchSession.cs`、focused Maduli tests | frontend runtime、card matrix JSON、LayerEngine broad rewrite、unrelated battle lifecycle / cleanup queue files、unrelated activated abilities、`riftbound-dotnet.sln` |
| 4D-03AN-E | E-Review / Poincare | Read-only readiness audit recorded in `docs/CURRENT_STAGE4D_03AN_CARD_MATRIX_READINESS_AUDIT.md` | 检查 Maduli `UNL-144/219` matrix readiness and full-official blockers | card coverage docs in read-only mode | `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` unless A opens a future matrix write window |
| 4D-03AM-D | D-Review / Nash after B diff exists | Audit / evidence recorded | 审查 B diff、tests、rules evidence and residuals | 4D-03AM audit / evidence docs、checkpoint、completion audit、server audit、closure plan | runtime code, frontend code, tests unless a docs-only verifier is explicitly authorized |
| 4D-FE-C | C-Review / Copernicus | Read-only preflight recorded in `docs/CURRENT_STAGE4D_FRONTEND_FINAL_VALIDATION_PREFLIGHT.md` | 准备 final frontend contract / Chrome smoke fresh-run checklist | DevUi scripts and existing frontend-contract docs in read-only mode; no code write unless A opens a separate C write window | server runtime, card matrix, local rule inference in frontend |
| 4D-MATRIX-E | E-Review / Poincare | Read-only readiness audit recorded in `docs/CURRENT_STAGE4D_03AM_CARD_MATRIX_READINESS_AUDIT.md` | 检查 Azir `SFD·050/221` / `SFD·050a/221` matrix readiness and full-official blocker | card coverage docs in read-only mode | `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` until runtime evidence is accepted |

## 3. Exclusive Writelocks

- At this stop point, no runtime, frontend or matrix write lock remains open.
- While 4D-03AM-B or 4D-03AN-B was active, only B could modify `P4ActivatedAbilityCatalog.cs`, `CoreRuleEngine.cs`, `MatchSession.cs`, and focused tests inside the explicit slice scope.
- D may not start final audit docs for future slices until A has inspected B diff and at least the focused / adjacent verification commands have run.
- C remains read-only while B might alter server prompt shape. Any frontend write window must wait until server `ActionPrompt` payload and event shape are stable.
- E remains read-only until A accepts runtime evidence. The matrix must not be upgraded to `fullOfficial=true` for Azir while optional armament reattach or other official text branches remain residual.
- No parallel task may edit card matrix JSON, frontend stores, `ActionPrompt` contracts, battle state machine, stack, cleanup, hidden-info redaction, or E2E fixtures without an explicit owner and a fresh write-lock note.

## 4. 4D-03AM-B Acceptance Gate

B implementation is acceptable only if A can verify all of the following:

1. `P4ActivatedAbilityCatalog` exposes executable definitions or aliases for both Azir collector Nos.
2. Server prompt exposes `ACTIVATE_ABILITY` only when legal, with green typed cost, swift marker, target slot, source requirement and once-per-turn metadata.
3. Payment goes through shared PaymentEngine / `PaymentCostRules` with green power spend and necessary `RECYCLE_RUNE:<objectId>` support.
4. Wrong trait, generic temporary resource, duplicate / invalid / unnecessary recycle, insufficient cost, and unsupported optional cost are rejected no-mutation.
5. Target validation rejects enemy unit, equipment, rune, battlefield, hand, face-down, stale object, source self and dirty-controller target.
6. Resolution is server-authoritative and swaps Azir with the selected controlled unit, updating `ObjectLocations`, snapshot and event payload.
7. Once-per-turn restriction rejects a same-turn second activation and resets only through existing turn-memory lifecycle.
8. Optional armament reattach is either implemented with tests or explicitly recorded as residual risk; success fixtures must not claim full-official Azir if this branch remains unimplemented.

Suggested post-implementation commands:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~AzirSwiftSwap|FullyQualifiedName~Azir|FullyQualifiedName~ActivateAbility|FullyQualifiedName~MoveUnit|FullyQualifiedName~PaymentEngine"
```

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Azir|FullyQualifiedName~ActivateAbility|FullyQualifiedName~MoveUnit|FullyQualifiedName~PaymentEngine|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub|FullyQualifiedName~Priority"
```

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
git diff --check
```

## 5. C / E Preflight Boundaries

C may prepare a final validation checklist, but must not turn historical frontend evidence into final READY evidence. Final frontend validation still requires fresh runs in the final code state:

```sh
cd src/Riftbound.DevUi
source ../../scripts/dev-env.sh && npm run build
source ../../scripts/dev-env.sh && npm run smoke:chrome -- --start-api
source ../../scripts/dev-env.sh && npm run e2e:formal-18 -- --start-api
```

E may identify matrix rows and official text blockers for Azir, but must not update `fullOfficial` status until A accepts runtime, rules evidence, tests, optional-branch handling and FAQ review.

## 6. Current Batch Stop Point

This batch stops after committing the 4D-03AN Maduli implementation, A-side audit / evidence, and checkpoint references. The project remains **NOT READY**. The next active work item should be opened only by a fresh explicit dispatch; no frontend or matrix write window is open, and `riftbound-dotnet.sln` remains untouched.

# Stage 4D Next Dispatch and Writelocks

日期：2026-05-15
结论：**CURRENT BATCH PAUSED / PROJECT NOT READY**

本文件是 A 主控对下一批 B/C/D/E 工作的调度队列与写锁边界。它只做 planning / handoff / acceptance 归档，不实现 runtime，不修改前端，不修改测试代码，不修改 card matrix。当前 active goal 仍未完成，不得调用 `update_goal complete`。

## 1. 输入事实

- 当前分支为 `main`，仓库当前只保留未跟踪 `riftbound-dotnet.sln`；该文件不得被本批任务触碰或纳入提交。
- `docs/CURRENT_STAGE4D_03AO_PAYMENT_ENGINE_EZREAL_BLUE_SWIFT_MOVE_TO_BASE_HANDOFF.md` 已把下一枚 P0-005 implementation slice 锁定为 `SFD·082/221` / `SFD·082a/221` / `SFD·082b/221·P` 伊泽瑞尔 blue swift no-target self move-to-base activated ability representative。
- `docs/CURRENT_STAGE4D_03AO_PAYMENT_ENGINE_EZREAL_BLUE_SWIFT_MOVE_TO_BASE_BASELINE_EVIDENCE.md` 已记录 implementation-before baseline：Ezreal / ActivateAbility / MoveUnit / PaymentEngine 179/179 通过；Ezreal / ActivateAbility / MoveUnit / PaymentEngine / ActionPrompt / GameHub / Priority 372/372 通过；`git diff --check` 通过。
- `docs/CURRENT_STAGE4D_03AO_CARD_MATRIX_READINESS_AUDIT.md` 已确认 Ezreal `FU-2dca1ad450` 当前仍为 `fullOfficial=false`；4C-49 ordinary play-unit evidence 不代理 blue swift move-to-base、attack / defense trigger、cannot-combat-damage static、full swift timing 或 FAQ breadth。
- `docs/CURRENT_STAGE4D_03AN_PAYMENT_ENGINE_GATEKEEPER_MADULI_PURPLE_MOVE_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03AN_PAYMENT_ENGINE_GATEKEEPER_MADULI_PURPLE_MOVE_EVIDENCE.md` 已记录 4D-03AN-B implemented / A-validated：focused 25/25、handoff focused 188/188、adjacent 381/381、backend full 4293/4293、`git diff --check` 通过。
- `docs/CURRENT_STAGE4D_03AM_PAYMENT_ENGINE_AZIR_SWIFT_SWAP_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03AM_PAYMENT_ENGINE_AZIR_SWIFT_SWAP_EVIDENCE.md` 已记录 4D-03AM-B implemented / A-validated：focused 23/23、handoff focused 191/191、adjacent 384/384、backend full 4268/4268、`git diff --check` 通过。
- `docs/CURRENT_ACTIVE_GOAL_PROMPT_ARTIFACT_CHECKLIST.md` 已确认 active goal 的 READY 门槛仍未满足：P0/P1 未清零，1009 / 811 card matrix 仍无 full-official coverage，frontend build / Chrome smoke / formal 18-step 仍需在最终代码状态 fresh run。
- A 当前只完成 4D-03AO handoff / baseline / matrix readiness / checkpoint 归档；没有向任何 runtime implementation agent 发出新写入任务。

## 2. Dispatch Queue

| Queue | Owner | Status | Purpose | Write scope | Must not touch |
|---|---|---|---|---|---|
| 4D-NEXT-A | A 主控 | This batch done after this doc is committed | 记录 4D-03AO 任务、写锁、验收与暂停点 | `docs/CURRENT_STAGE4D_NEXT_DISPATCH_AND_WRITELOCKS.md`、checkpoint / audit / closure docs | `src/**`、`tests/**`、frontend runtime、card matrix |
| 4D-03AO-B | B-Implementation / Raman `019e2257-8d40-7630-9201-28df44dd689a` | Queued / not dispatched | 实现 Ezreal blue swift no-target self move-to-base representative | `src/Riftbound.Engine/P4ActivatedAbilityCatalog.cs`、`src/Riftbound.Engine/CoreRuleEngine.cs`、`src/Riftbound.Engine/MatchSession.cs`、minimal `ActionPrompt` contract only if needed、focused Ezreal tests | frontend runtime、card matrix JSON、LayerEngine broad rewrite、attack / defense trigger runtime、unrelated battle lifecycle / cleanup queue files、unrelated activated abilities、`riftbound-dotnet.sln` |
| 4D-03AO-E | E-Review / Poincare | Read-only readiness audit recorded in `docs/CURRENT_STAGE4D_03AO_CARD_MATRIX_READINESS_AUDIT.md` | 检查 Ezreal `FU-2dca1ad450` matrix readiness and full-official blockers | card coverage docs in read-only mode | `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` unless A opens a future matrix write window |
| 4D-03AM-B | B-Implementation / Raman `019e2257-8d40-7630-9201-28df44dd689a` | Implemented and A-validated | 实现 Azir green swift position-swap representative | completed runtime / focused tests | frontend runtime、card matrix JSON、unrelated files |
| 4D-03AN-B | B-Implementation / Raman `019e2257-8d40-7630-9201-28df44dd689a` | Implemented and A-validated | 实现 Gatekeeper Maduli purple enemy-battlefield move representative | completed runtime / focused tests | frontend runtime、card matrix JSON、unrelated files |
| 4D-FE-C | C-Review / Copernicus | Read-only preflight recorded in `docs/CURRENT_STAGE4D_FRONTEND_FINAL_VALIDATION_PREFLIGHT.md` | 准备 final frontend contract / Chrome smoke fresh-run checklist | DevUi scripts and existing frontend-contract docs in read-only mode; no code write unless A opens a separate C write window | server runtime, card matrix, local rule inference in frontend |
| 4D-MATRIX-E | E-Review / Poincare | Read-only audits recorded for Azir / Maduli / Ezreal | 检查 latest payment representative rows and full-official blockers | card coverage docs in read-only mode | `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` until runtime evidence is accepted and A opens matrix write window |

## 3. Exclusive Writelocks

- At this stop point, no runtime, frontend or matrix write lock remains open.
- 4D-03AO-B is queued only; A has not dispatched B and no implementation agent may write runtime files for Ezreal yet.
- If 4D-03AO-B is opened later, only B may modify `P4ActivatedAbilityCatalog.cs`, `CoreRuleEngine.cs`, `MatchSession.cs`, minimal contracts if necessary and focused Ezreal tests inside the explicit slice scope.
- D may not start final audit docs for 4D-03AO until A has inspected B diff and at least the focused / adjacent verification commands have run.
- C remains read-only while B might alter server prompt shape. Any frontend write window must wait until server `ActionPrompt` payload and event shape are stable.
- E remains read-only until A accepts runtime evidence. The matrix must not be upgraded to `fullOfficial=true` for Ezreal while damage trigger, cannot-combat-damage static, swift timing or FAQ branches remain residual.
- No parallel task may edit card matrix JSON, frontend stores, `ActionPrompt` contracts, battle state machine, stack, cleanup, hidden-info redaction, or E2E fixtures without an explicit owner and a fresh write-lock note.

## 4. 4D-03AO-B Acceptance Gate

B implementation is acceptable only if A can verify all of the following:

1. `P4ActivatedAbilityCatalog` exposes executable definitions or aliases for `SFD·082/221`, `SFD·082a/221` and `SFD·082b/221·P`.
2. Ability metadata uses blue typed power cost 1, zero mana cost, no target, no exhaust cost and `swift=true` / reaction-speed marker.
3. Server prompt exposes `ACTIVATE_ABILITY` only when legal, with source requirement, blue typed cost, no-target policy, destination base / self-movement metadata and stack-before-move policy.
4. Payment goes through shared PaymentEngine / `PaymentCostRules` with blue power spend and necessary `RECYCLE_RUNE:<objectId>` support.
5. Wrong trait, generic temporary resource, duplicate / invalid / unnecessary recycle, insufficient blue and unsupported optional cost are rejected no-mutation.
6. Source validation rejects base, hand, deck, graveyard, face-down, enemy-controlled, stale, wrong-card and dirty-source Ezreal attempts; accepted sources must be controlled public Ezreal units in precise battlefield locations.
7. Command rejects submitted targets, battlefield destinations and arbitrary destination overrides because the skill is no-target self movement.
8. Resolution is server-authoritative and moves Ezreal to the activating player's base, updating `ObjectLocations`, snapshot and `UNIT_MOVED_TO_BASE` event payload with a distinguishable movement permission.
9. Stale source / no-longer-controlled / no-longer-battlefield / already-base source at resolution becomes no-effect without frontend inference.
10. Attack / defense damage trigger, cannot-combat-damage static and full swift / reaction timing are either implemented with tests or explicitly recorded as residual risk; success fixtures must not claim full-official Ezreal if these branches remain unimplemented.

Suggested post-implementation commands:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~EzrealBlueSwift|FullyQualifiedName~Ezreal|FullyQualifiedName~ActivateAbility|FullyQualifiedName~MoveUnit|FullyQualifiedName~PaymentEngine"
```

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Ezreal|FullyQualifiedName~ActivateAbility|FullyQualifiedName~MoveUnit|FullyQualifiedName~PaymentEngine|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub|FullyQualifiedName~Priority"
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

E may identify matrix rows and official text blockers for Ezreal, but must not update `fullOfficial` status until A accepts runtime, rules evidence, tests, residual handling and FAQ review.

## 6. Current Batch Stop Point

This batch stops after committing the 4D-03AO Ezreal handoff / baseline / matrix readiness docs and checkpoint references. The project remains **NOT READY**. The next runtime work item should be opened only by a fresh explicit dispatch; no frontend or matrix write window is open, no implementation task has been sent, and `riftbound-dotnet.sln` remains untouched.

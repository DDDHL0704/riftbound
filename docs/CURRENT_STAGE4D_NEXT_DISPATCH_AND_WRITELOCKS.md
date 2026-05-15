# Stage 4D Next Dispatch and Writelocks

日期：2026-05-15
结论：**4D-03AS-B ACCEPTED / PROJECT NOT READY**

本文件是 A 主控对下一批 B/C/D/E 工作的调度队列与写锁边界。它只做 planning / handoff / acceptance 归档，不实现 runtime，不修改前端，不修改测试代码，不修改 card matrix。当前 active goal 仍未完成，不得调用 `update_goal complete`。

## 1. 输入事实

- 当前分支为 `main`，仓库当前只保留未跟踪 `riftbound-dotnet.sln`；该文件不得被本批任务触碰或纳入提交。
- `docs/CURRENT_STAGE4D_03AS_AZIR_OPTIONAL_ARMAMENT_REATTACH_HANDOFF.md` 已把下一枚 Azir full-text follow-up 锁定为 `SFD·050/221` / `SFD·050a/221` 阿兹尔 optional armament reattach branch：目标单位已配武装时，可以选择 0 或 1 件武装贴附到 Azir。
- `docs/CURRENT_STAGE4D_03AS_AZIR_OPTIONAL_ARMAMENT_REATTACH_BASELINE_EVIDENCE.md` 已记录 implementation-before baseline：Azir / ActivateAbility / MoveUnit / PaymentEngine 194/194 通过；Azir / ActivateAbility / MoveUnit / PaymentEngine / ActionPrompt / GameHub / Priority 387/387 通过；`git diff --check` 通过。
- `docs/CURRENT_STAGE4D_03AS_CARD_MATRIX_READINESS_AUDIT.md` 已确认 Azir `FU-105abedc17` 当前仍为 `fullOfficial=false`；4D-03AM unarmed / no-reattach position-swap evidence 不能代理 optional armament reattach full official branch，matrix JSON 未更新。
- 用户恢复 active goal 后，4D-03AS-B 已派发给 B-Implementation / Raman `019e2b49-28c3-7ad2-b3f8-ef1347b56996`。B 派发期间独占 Azir optional armament reattach runtime/test 写锁：`CoreRuleEngine.cs`、`MatchSession.cs`、`P4ActivatedAbilityCatalog.cs`、`tests/Riftbound.ConformanceTests/AzirSwiftSwapActivatedAbilityTests.cs`；可选且仅最小必要时触碰 `src/Riftbound.Contracts/Protocol.cs` / `GameCommandJsonMapper.cs` / focused contract serialization tests。该写锁已在 A 验收后关闭；frontend、card matrix JSON、broad equipment rewrite、unrelated abilities、swift timing breadth、battle lifecycle、LayerEngine、HASTE_READY 与 `riftbound-dotnet.sln` 均未触碰。
- `docs/CURRENT_STAGE4D_03AS_AZIR_OPTIONAL_ARMAMENT_REATTACH_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03AS_AZIR_OPTIONAL_ARMAMENT_REATTACH_EVIDENCE.md` 已记录 4D-03AS-B implemented / A-validated：focused 204/204、adjacent 397/397、backend full 4355/4355、`git diff --check` 通过。
- `docs/CURRENT_STAGE4D_03AS_CARD_MATRIX_READINESS_AUDIT.md` 已刷新为 readiness improved：Azir `FU-105abedc17` optional armament reattach blocker 已有 accepted runtime evidence, but matrix JSON remains unchanged and `fullOfficial=false` until a future explicit matrix write window.
- `docs/CURRENT_STAGE4D_03AR_GATEKEEPER_MADULI_CANNOT_READY_STATIC_HANDOFF.md` 已把下一枚 Gatekeeper Maduli static slice 锁定为 `UNL-144/219` 守门者马杜里 `我无法变为活跃状态。` cannot-ready representative。
- `docs/CURRENT_STAGE4D_03AR_GATEKEEPER_MADULI_CANNOT_READY_STATIC_BASELINE_EVIDENCE.md` 已记录 implementation-before baseline：Maduli / Gatekeeper / CrimsonRose / HuntReadyGuardTests 61/61 通过；Maduli / Gatekeeper / CrimsonRose / HuntReadyGuardTests / ActivateAbility / PaymentEngine / ActionPrompt / GameHub / Priority 371/371 通过；`git diff --check` 通过。
- 4D-03AR-B 已派发给 B-Implementation / Schrodinger `019e291f-dd42-75c3-8f12-05220a1629df`。B 当前独占 Maduli cannot-ready static runtime/test 写锁：`CoreRuleEngine.cs`、`MatchSession.cs`、focused Maduli / Crimson Rose / Hunt tests；frontend、card matrix、broad LayerEngine、unrelated ready effects、HASTE_READY / swift timing、battle lifecycle 与 `riftbound-dotnet.sln` 仍禁止触碰。
- `docs/CURRENT_STAGE4D_03AR_GATEKEEPER_MADULI_CANNOT_READY_STATIC_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03AR_GATEKEEPER_MADULI_CANNOT_READY_STATIC_EVIDENCE.md` 已记录 4D-03AR-B implemented / A-validated：focused 65/65、adjacent 375/375、backend full 4345/4345、`git diff --check` 通过。
- `docs/CURRENT_STAGE4D_03AR_CARD_MATRIX_READINESS_AUDIT.md` 已确认 Maduli `FU-d5d5707b0e` matrix readiness improved by 4D-03AN movement + 4D-03AR cannot-ready static evidence, but matrix JSON remains unchanged and `fullOfficial=false` until a future explicit matrix write window.
- `docs/CURRENT_STAGE4D_03AQ_PAYMENT_ENGINE_HASTE_READY_COVERAGE_VERIFIER_HANDOFF.md` 已把下一枚 P0-005 test-only guard 锁定为 implemented HASTE_READY registry/profile set 与 existing P4 fixture evidence 的 catalog-bound verifier。
- `docs/CURRENT_STAGE4D_03AQ_PAYMENT_ENGINE_HASTE_READY_COVERAGE_VERIFIER_BASELINE_EVIDENCE.md` 已记录 implementation-before baseline：PaymentEngineCoverageAuditTests / HasteOptional / HasteReady 102/102 通过；PaymentEngineCoverageAuditTests / HasteOptional / HasteReady / PlayCard / ActionPrompt / GameHub / Priority 442/442 通过；`git diff --check` 通过。
- 4D-03AQ-B 已派发并验收，B-Implementation / Halley `019e290f-73b2-7d62-a19e-2a252ad6ef2e` 的 `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs` test-only 写锁已关闭；runtime、frontend、card matrix、broad Haste rewrite、battle lifecycle 与 `riftbound-dotnet.sln` 均未触碰。
- `docs/CURRENT_STAGE4D_03AQ_PAYMENT_ENGINE_HASTE_READY_COVERAGE_VERIFIER_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03AQ_PAYMENT_ENGINE_HASTE_READY_COVERAGE_VERIFIER_EVIDENCE.md` 已记录 4D-03AQ-B test-only verifier implemented / A-validated：focused 105/105、adjacent 445/445、backend full 4341/4341、`git diff --check` 通过。
- `docs/CURRENT_STAGE4D_03AP_PAYMENT_ENGINE_REKSAI_HASTE_READY_RED_EXACTNESS_HANDOFF.md` 已把下一枚 P0-005 focused guard 锁定为 `SFD·029/221` / `SFD·029a/221` 雷克塞 HASTE_READY extra 1 mana + 1 red typed power exactness representative。
- `docs/CURRENT_STAGE4D_03AP_PAYMENT_ENGINE_REKSAI_HASTE_READY_RED_EXACTNESS_BASELINE_EVIDENCE.md` 已记录 implementation-before baseline：Rek'Sai / HasteOptional / PaymentEngine 109/109 通过；Rek'Sai / HasteOptional / PaymentEngine / PlayCard / ActionPrompt / GameHub / Priority 425/425 通过；`git diff --check` 通过。
- `docs/CURRENT_STAGE4D_03AP_CARD_MATRIX_READINESS_AUDIT.md` 已确认 Rek'Sai `FU-1945f6918c` 当前仍为 `fullOfficial=false`；4C-52 ordinary no-optional evidence 与 old P4 HASTE_READY fixtures 不代理 red exactness、strong/overflow、non-hand haste granting、LayerEngine 或 FAQ breadth。
- 4D-03AP-B 已派发给 B-Implementation / Archimedes `019e2900-bcc5-7763-8f3a-db41a0aaa0a1`，默认 focused test write lock；runtime edits only if tests expose an actual typed-red payment gap。
- `docs/CURRENT_STAGE4D_03AP_PAYMENT_ENGINE_REKSAI_HASTE_READY_RED_EXACTNESS_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03AP_PAYMENT_ENGINE_REKSAI_HASTE_READY_RED_EXACTNESS_EVIDENCE.md` 已记录 4D-03AP-B test-only guard implemented / A-validated：focused 17/17、handoff focused 126/126、adjacent 442/442、backend full 4338/4338、`git diff --check` 通过。
- `docs/CURRENT_STAGE4D_03AO_PAYMENT_ENGINE_EZREAL_BLUE_SWIFT_MOVE_TO_BASE_HANDOFF.md` 已把下一枚 P0-005 implementation slice 锁定为 `SFD·082/221` / `SFD·082a/221` / `SFD·082b/221·P` 伊泽瑞尔 blue swift no-target self move-to-base activated ability representative。
- `docs/CURRENT_STAGE4D_03AO_PAYMENT_ENGINE_EZREAL_BLUE_SWIFT_MOVE_TO_BASE_BASELINE_EVIDENCE.md` 已记录 implementation-before baseline：Ezreal / ActivateAbility / MoveUnit / PaymentEngine 179/179 通过；Ezreal / ActivateAbility / MoveUnit / PaymentEngine / ActionPrompt / GameHub / Priority 372/372 通过；`git diff --check` 通过。
- `docs/CURRENT_STAGE4D_03AO_CARD_MATRIX_READINESS_AUDIT.md` 已确认 Ezreal `FU-2dca1ad450` 当前仍为 `fullOfficial=false`；4C-49 ordinary play-unit evidence 不代理 blue swift move-to-base、attack / defense trigger、cannot-combat-damage static、full swift timing 或 FAQ breadth。
- `docs/CURRENT_STAGE4D_03AN_PAYMENT_ENGINE_GATEKEEPER_MADULI_PURPLE_MOVE_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03AN_PAYMENT_ENGINE_GATEKEEPER_MADULI_PURPLE_MOVE_EVIDENCE.md` 已记录 4D-03AN-B implemented / A-validated：focused 25/25、handoff focused 188/188、adjacent 381/381、backend full 4293/4293、`git diff --check` 通过。
- `docs/CURRENT_STAGE4D_03AM_PAYMENT_ENGINE_AZIR_SWIFT_SWAP_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03AM_PAYMENT_ENGINE_AZIR_SWIFT_SWAP_EVIDENCE.md` 已记录 4D-03AM-B implemented / A-validated：focused 23/23、handoff focused 191/191、adjacent 384/384、backend full 4268/4268、`git diff --check` 通过。
- `docs/CURRENT_ACTIVE_GOAL_PROMPT_ARTIFACT_CHECKLIST.md` 已确认 active goal 的 READY 门槛仍未满足：P0/P1 未清零，1009 / 811 card matrix 仍无 full-official coverage，frontend build / Chrome smoke / formal 18-step 仍需在最终代码状态 fresh run。
- `docs/CURRENT_STAGE4D_03AO_PAYMENT_ENGINE_EZREAL_BLUE_SWIFT_MOVE_TO_BASE_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03AO_PAYMENT_ENGINE_EZREAL_BLUE_SWIFT_MOVE_TO_BASE_EVIDENCE.md` 已记录 4D-03AO-B implemented / A-validated：focused 28/28、handoff focused 207/207、adjacent 400/400、backend full 4321/4321、`git diff --check` 通过。

## 2. Dispatch Queue

| Queue | Owner | Status | Purpose | Write scope | Must not touch |
|---|---|---|---|---|---|
| 4D-NEXT-A | A 主控 | 4D-03AS-B accepted | 记录 4D-03AS handoff、baseline、B 派发、验收、matrix readiness 与暂停点 | `docs/CURRENT_STAGE4D_NEXT_DISPATCH_AND_WRITELOCKS.md`、checkpoint / audit / closure docs | `src/**`、`tests/**`、frontend runtime、card matrix |
| 4D-03AS-B | B-Implementation / Raman `019e2b49-28c3-7ad2-b3f8-ef1347b56996` | Implemented and A-validated | 实现 Azir optional armament reattach branch | completed runtime / focused tests | frontend runtime、card matrix JSON、broad equipment rewrite、unrelated abilities、swift timing breadth、battle lifecycle、LayerEngine、HASTE_READY、`riftbound-dotnet.sln` |
| 4D-03AS-E | E-Review | Read-only readiness audit recorded in `docs/CURRENT_STAGE4D_03AS_CARD_MATRIX_READINESS_AUDIT.md` | 检查 Azir `FU-105abedc17` optional armament blocker and full-official gate | card coverage docs in read-only mode | `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` unless A opens a future matrix write window |
| 4D-03AR-B | B-Implementation / Schrodinger `019e291f-dd42-75c3-8f12-05220a1629df` | Implemented and A-validated | 实现 Gatekeeper Maduli cannot-ready static representative | completed runtime / focused tests | frontend runtime、card matrix JSON、broad LayerEngine rewrite、unrelated ready effects、HASTE_READY / swift timing、battle lifecycle、`riftbound-dotnet.sln` |
| 4D-03AQ-B | B-Implementation / Halley `019e290f-73b2-7d62-a19e-2a252ad6ef2e` | Implemented and A-validated | 新增 HASTE_READY catalog-bound coverage verifier | completed focused verifier; no runtime changes | `src/**`、frontend runtime、card matrix JSON、broad Haste rewrite、strong/overflow、non-hand haste granting、LayerEngine、battle lifecycle、`riftbound-dotnet.sln` |
| 4D-03AP-B | B-Implementation / Archimedes `019e2900-bcc5-7763-8f3a-db41a0aaa0a1` | Implemented and A-validated | 补强 Rek'Sai HASTE_READY red typed payment exactness focused tests / evidence | completed focused tests; no runtime changes | frontend runtime、card matrix JSON、broad Haste rewrite、strong/overflow、non-hand haste granting、LayerEngine、battle lifecycle、`riftbound-dotnet.sln` |
| 4D-03AP-E | E-Review | Read-only readiness audit recorded in `docs/CURRENT_STAGE4D_03AP_CARD_MATRIX_READINESS_AUDIT.md` | 检查 Rek'Sai `FU-1945f6918c` matrix readiness and full-official blockers | card coverage docs in read-only mode | `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` unless A opens a future matrix write window |
| 4D-03AO-B | B-Implementation / Raman `019e2257-8d40-7630-9201-28df44dd689a` | Implemented and A-validated | 实现 Ezreal blue swift no-target self move-to-base representative | completed runtime / focused tests | frontend runtime、card matrix JSON、LayerEngine broad rewrite、attack / defense trigger runtime、unrelated battle lifecycle / cleanup queue files、unrelated activated abilities、`riftbound-dotnet.sln` |
| 4D-03AO-E | E-Review / Poincare | Read-only readiness audit recorded in `docs/CURRENT_STAGE4D_03AO_CARD_MATRIX_READINESS_AUDIT.md` | 检查 Ezreal `FU-2dca1ad450` matrix readiness and full-official blockers | card coverage docs in read-only mode | `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` unless A opens a future matrix write window |
| 4D-03AM-B | B-Implementation / Raman `019e2257-8d40-7630-9201-28df44dd689a` | Implemented and A-validated | 实现 Azir green swift position-swap representative | completed runtime / focused tests | frontend runtime、card matrix JSON、unrelated files |
| 4D-03AN-B | B-Implementation / Raman `019e2257-8d40-7630-9201-28df44dd689a` | Implemented and A-validated | 实现 Gatekeeper Maduli purple enemy-battlefield move representative | completed runtime / focused tests | frontend runtime、card matrix JSON、unrelated files |
| 4D-FE-C | C-Review / Copernicus | Read-only preflight recorded in `docs/CURRENT_STAGE4D_FRONTEND_FINAL_VALIDATION_PREFLIGHT.md` | 准备 final frontend contract / Chrome smoke fresh-run checklist | DevUi scripts and existing frontend-contract docs in read-only mode; no code write unless A opens a separate C write window | server runtime, card matrix, local rule inference in frontend |
| 4D-MATRIX-E | E-Review / Poincare | Read-only audits recorded for Azir / Maduli / Ezreal | 检查 latest payment representative rows and full-official blockers | card coverage docs in read-only mode | `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` until runtime evidence is accepted and A opens matrix write window |

## 3. Exclusive Writelocks

- At this stop point, no runtime, test, frontend or matrix write lock remains open.
- 4D-03AS-B runtime / focused-test write lock is closed after A validation and commit-ready evidence.
- No frontend or matrix write lock is open.
- 4D-03AR-B runtime / focused-test write lock is closed after A validation and commit-ready evidence.
- 4D-03AQ-B test-only write lock is closed after A validation and commit-ready evidence.
- 4D-03AP-B focused-test write lock is closed after A validation and commit-ready evidence.
- 4D-03AO-B runtime / focused-test write lock is closed after A validation and commit.
- D/A audit docs for 4D-03AO are recorded; no further 4D-03AO runtime edits should occur without a fresh dispatch.
- C remains read-only while B might alter server prompt shape. Any frontend write window must wait until server `ActionPrompt` payload and event shape are stable.
- E remains read-only until A opens an explicit matrix write window. The matrix must not be upgraded to `fullOfficial=true` for Maduli, Ezreal or other latest representatives merely because focused runtime evidence passed.
- No parallel task may edit card matrix JSON, frontend stores, `ActionPrompt` contracts, battle state machine, stack, cleanup, hidden-info redaction, or E2E fixtures without an explicit owner and a fresh write-lock note.

## 4. 4D-03AS-B Acceptance Gate Accepted

B implementation is accepted because A verified all of the following:

1. Server prompt metadata no longer claims Azir `armamentReattachPolicy="deferred"` after implementation; metadata exposes target-scoped armament reattach choices or an equivalent server-owned shape.
2. The command path allows no reattach even when the selected target has attached armament.
3. The command path rejects invalid reattach choices no-mutation: missing object, non-equipment object, unattached equipment, equipment attached to a different unit, multiple selected armaments, opponent-controlled illegal object, or reattach selection without a legal selected target.
4. Stack resolution rechecks the selected armament. If still legal and attached to the selected target, it sets `AttachedToObjectId` to Azir and emits existing-shape equipment reattach evidence such as `EQUIPMENT_REATTACHED` with previous / new attachment payload.
5. If the selected armament becomes stale before resolution, the existing source / target legality still governs position swap; reattach itself becomes no-effect and does not emit a false attach event.
6. `UNIT_LOCATIONS_SWAPPED` or a companion event carries auditable payload for selected armament id, `armamentReattachApplied`, and a non-deferred policy marker.
7. 4D-03AM Azir payment, once-per-turn, target validation, stale target no-effect, rune recycle and no-mutation tests remain green.
8. The slice does not update frontend runtime, card matrix JSON, Azir full-official status, P0/P1 status or READY.

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

## 5. 4D-03AR-B Historical Acceptance Gate Accepted

B implementation is accepted because A verified all of the following:

1. Server runtime has an explicit `UNL-144/219` cannot-ready policy or helper, preferably reused by every ready path touched in this representative slice.
2. Crimson Rose ready-unit prompt does not offer exhausted Maduli as a legal ready target.
3. A hand-written or stale Crimson Rose ready-unit target cannot make Maduli active; resolution must leave `IsExhausted=true` and emit no Maduli `UNIT_READIED`.
4. Hunt mass friendly ready, or an equivalent mass ready representative, readies other legal friendly units while skipping exhausted Maduli.
5. 4D-03AN Maduli purple move prompt / command / typed payment / movement / stale no-effect tests remain green.
6. Prompt metadata no longer claims Maduli cannot-ready static is `deferred` after implementation.
7. The slice does not update frontend runtime, card matrix JSON, Maduli full-official status, P0/P1 status or READY.

Suggested post-implementation commands:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Maduli|FullyQualifiedName~Gatekeeper|FullyQualifiedName~CrimsonRose|FullyQualifiedName~HuntReadyGuardTests"
```

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Maduli|FullyQualifiedName~Gatekeeper|FullyQualifiedName~CrimsonRose|FullyQualifiedName~HuntReadyGuardTests|FullyQualifiedName~ActivateAbility|FullyQualifiedName~PaymentEngine|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub|FullyQualifiedName~Priority"
```

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
git diff --check
```

## 6. 4D-03AQ-B Historical Acceptance Gate

B test-only verifier is acceptable only if A can verify all of the following:

1. `PaymentEngineCoverageAuditTests` binds implemented HASTE_READY registry/profile entries to typed trait metadata and existing P4 fixture anchors.
2. The verifier fails on missing trait, missing fixture, duplicate manifest entry or closure text that claims READY / full official.
3. Closure status explicitly says `NOT READY` and `P0-005 remains open`.
4. No runtime, frontend, card matrix or `riftbound-dotnet.sln` edits occur.

Suggested post-implementation commands:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PaymentEngineCoverageAuditTests|FullyQualifiedName~HasteOptional|FullyQualifiedName~HasteReady"
```

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PaymentEngineCoverageAuditTests|FullyQualifiedName~HasteOptional|FullyQualifiedName~HasteReady|FullyQualifiedName~PlayCard|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub|FullyQualifiedName~Priority"
```

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
git diff --check
```

## 7. 4D-03AP-B Historical Acceptance Gate

B implementation / test guard is acceptable only if A can verify all of the following:

1. Both `SFD·029/221` and `SFD·029a/221` profiles expose `HASTE_READY` as extra 1 mana + 1 red typed power.
2. Server prompt exposes `PLAY_CARD`, `HASTE_READY`, base / total cost metadata and red payment-resource choices only when legal.
3. Command with existing red power succeeds and emits `COST_PAID` with `baseManaCost=3`, `totalManaCost=4`, `genericPower=0`, `totalPowerCost=1`, `powerByTrait.red=1`.
4. Command with necessary `RECYCLE_RUNE:<redRuneObjectId>` succeeds, recycles the rune and records payment-resource audit payload.
5. Wrong trait, generic temporary resource, insufficient red, duplicate / invalid / unnecessary recycle and unsupported optional cost reject no-mutation.
6. Command cannot bypass no-target route by submitting target object ids.
7. Strong / Overwhelm battle modifier, damage overflow, non-hand haste granting, LayerEngine, FAQ, card matrix full-official and READY remain residual unless separately dispatched.

Suggested post-implementation commands:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ReksaiHasteReady|FullyQualifiedName~Reksai|FullyQualifiedName~HasteOptional|FullyQualifiedName~PaymentEngine"
```

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Reksai|FullyQualifiedName~HasteOptional|FullyQualifiedName~PaymentEngine|FullyQualifiedName~PlayCard|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub|FullyQualifiedName~Priority"
```

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
git diff --check
```

## 8. 4D-03AO-B Historical Acceptance Gate

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

## 9. C / E Preflight Boundaries

C may prepare a final validation checklist, but must not turn historical frontend evidence into final READY evidence. Final frontend validation still requires fresh runs in the final code state:

```sh
cd src/Riftbound.DevUi
source ../../scripts/dev-env.sh && npm run build
source ../../scripts/dev-env.sh && npm run smoke:chrome -- --start-api
source ../../scripts/dev-env.sh && npm run e2e:formal-18 -- --start-api
```

E may identify matrix rows and official text blockers for Azir / Ezreal, but must not update `fullOfficial` status until A accepts runtime, rules evidence, tests, residual handling and FAQ review.

## 10. Current Batch Stop Point

This record stops after accepting 4D-03AS-B and refreshing matrix readiness. The project remains **NOT READY**. No frontend, matrix, runtime or test write window remains open, and `riftbound-dotnet.sln` remains untouched.

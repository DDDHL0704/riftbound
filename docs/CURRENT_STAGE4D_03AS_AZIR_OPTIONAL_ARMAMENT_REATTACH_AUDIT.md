# Stage 4D-03AS Azir Optional Armament Reattach Audit

日期：2026-05-15
结论：**4D-03AS-B ACCEPTED / PROJECT NOT READY**

本文件是 A 主控对 4D-03AS-B Azir optional armament reattach runtime / focused-test diff 的验收审计。它不更新 frontend，不修改 card matrix，不声明 full-official 或 READY。

## 1. Scope

4D-03AS-B 的目标是实现 `SFD·050/221` / `SFD·050a/221` 阿兹尔官方文本中的 optional armament reattach branch：

```txt
如果该单位已配有武装，则你可以选择将其中一件武装贴附到我身上。
```

验收范围只覆盖 server-authoritative reattach representative：

- Prompt metadata 不再标记 `armamentReattachPolicy=deferred`。
- Prompt 暴露 target-scoped eligible armament choices。
- Command 可选择 0 或 1 件合法目标武装。
- Command 拒绝 invalid hand-written reattach choices without mutation。
- Stack resolution 重检 selected armament，合法时重贴附到 Azir，stale 时只跳过 reattach。
- 4D-03AM green typed payment、position swap、once-per-turn、target guards 与 no-mutation tests remain green。

## 2. Diff Review

Accepted implementation facts:

- `P4ActivatedAbilityCatalog` adds `AZIR_REATTACH_ARMAMENT:` optional-cost token helpers and central `AzirArmamentReattachPolicy = "implemented"`.
- `MatchSession` changes Azir prompt metadata to implemented, exposes `armamentReattachChoicePrefix`, and publishes `armamentReattachChoicesByTargetObjectId` for every legal target.
- `CoreRuleEngine` splits Azir `OptionalCosts` into payment-resource actions and reattach choice tokens, validates at most one selected target-attached equipment object, and stores the selected reattach token on the stack item.
- `CoreRuleEngine` rechecks the selected armament at resolution. If still legal and attached to the selected target, it sets `AttachedToObjectId` to Azir and emits `EQUIPMENT_REATTACHED`; if stale, it leaves the armament unchanged and emits no false reattach event while preserving legal swap semantics.
- `AzirSwiftSwapActivatedAbilityTests` adds prompt choice, no-reattach, legal reattach, invalid hand-written choices and stale selected armament coverage while retaining existing payment / target / once-per-turn / recycle guards.

No frontend files, card matrix JSON or `riftbound-dotnet.sln` were modified.

## 3. Acceptance Mapping

| Gate | Evidence | Status |
|---|---|---|
| Metadata no longer deferred | Prompt tests expect `AzirArmamentReattachPolicy` and prefix | PASS |
| Target-scoped choices | `PromptExposesImplementedAzirArmamentReattachChoicesByTarget` | PASS |
| No-choice remains legal | `AzirNoReattachChoiceRemainsLegalWhenTargetHasArmament` | PASS |
| Legal selected armament reattaches | `AzirSelectedLegalArmamentReattachesToAzirOnResolution` | PASS |
| Invalid choices reject no-mutation | missing / non-equipment / unattached / other unit / opponent / multiple choices theory | PASS |
| Stale selected armament skips reattach | `AzirStaleSelectedArmamentSkipsReattachWithoutFalseEventAndStillSwaps` | PASS |
| Payment / target / once / recycle remain green | Focused and adjacent command groups | PASS |
| No matrix / frontend / READY overclaim | Diff review and status check | PASS |

## 4. Verification

Recorded in `docs/CURRENT_STAGE4D_03AS_AZIR_OPTIONAL_ARMAMENT_REATTACH_EVIDENCE.md`:

- focused 204/204 passed
- adjacent 397/397 passed
- backend full 4355/4355 passed
- `git diff --check` passed

## 5. Non-Completion Notes

- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` still records `FU-105abedc17` as `fullOfficial=false`.
- This audit does not open a card matrix write window.
- This audit does not close broader swift timing / FAQ / full-card coverage / P0/P1 / frontend final validation / READY.
- Historical 4D-03AM position-swap evidence remains valid; 4D-03AS adds optional armament reattach evidence on top.

## 6. Verdict

4D-03AS-B is accepted as a narrow server-authoritative optional armament reattach representative for Azir. The project remains **NOT READY**.

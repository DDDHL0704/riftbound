# Stage 4D-04A Keyword Deferred Surface Audit

日期：2026-05-15
结论：**A-SIDE HANDOFF RECORDED / P1-002 STILL OPEN / PROJECT NOT READY**

本文件是 4D-04A 对 P1-002 keyword deferred surface 的 A-side 审计。4D-03AT 之后，本批不再继续做 matrix evidence overlay，而是回到 P1 keyword execution-boundary 规则模型。

## 1. Scope

本批覆盖：

- `docs/CURRENT_STAGE4D_04A_KEYWORD_DEFERRED_SURFACE_HANDOFF.md`
- `docs/CURRENT_STAGE4D_04A_KEYWORD_DEFERRED_SURFACE_BASELINE_EVIDENCE.md`
- `docs/CURRENT_SERVER_RULE_AUDIT.md` P1-002 keyword deferred state
- `docs/CURRENT_STAGE4D_P0_P1_CLOSURE_PLAN.md` 4D-04 LayerEngine / Keyword track
- `docs/CURRENT_STAGE4D_NEXT_DISPATCH_AND_WRITELOCKS.md` next dispatch / no-open-writelock state

本批不覆盖 runtime, tests, frontend, browser smoke, card matrix JSON, full-official upgrade or final READY.

## 2. Findings

当前 P1-002 问题不是测试红灯，而是口径与规则完整度之间的结构性缺口：

- `KeywordCoverageReporter` 已公开六类 keyword family status，API consumers 可以看到 deferred rows。
- 多个 representative keyword fixtures 已经稳定通过。
- 但 equipment / interaction / combat / resource families 仍存在 "recognized but deferred" 的内部事实。
- 其中 equipment 是下一批最适合的收口切片，因为 assemble runtime / prompt / fixture evidence 已经经历多轮服务端权威化，但 `CardEquipmentKeywordRules` 的 family status 仍未表达这些已实现边界。

## 3. Baseline Accepted

A 侧本批验证：

- keyword catalog/profile focused baseline：8/8 passed。
- representative keyword fixture baseline：144/144 passed。
- 本批无 runtime / tests / frontend / matrix code changes before docs update。
- `riftbound-dotnet.sln` remains untracked and untouched。

## 4. Remaining Blockers

P1-002 仍 open：

- equipment full official breadth still includes agile reaction attachment, tempered optional attachment, static equipment modifiers, copied text, owner/controller changes and broader attach lifecycle.
- interaction full official breadth still includes broad Standby target damage, Ambush breadth and complex Echo costs.
- combat full official breadth still includes complete combat damage / assignment ordering / Roam movement execution breadth.
- resource full official breadth still includes broader experience, level, encourage and spellshield branches.
- LayerEngine / continuous effects remain separate P1 blockers.
- full-card matrix remains `fullOfficial=false` for current functional units.

## 5. Dispatch Verdict

The next recommended implementation batch is 4D-04B equipment keyword execution-boundary status split, under the write scope in `docs/CURRENT_STAGE4D_04A_KEYWORD_DEFERRED_SURFACE_HANDOFF.md`. This 4D-04A batch does not dispatch B; it closes as A-side handoff / baseline only. Project remains **NOT READY**.

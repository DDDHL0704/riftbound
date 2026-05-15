# Active Goal Prompt-to-Artifact Checklist

日期：2026-05-16
结论：**NOT READY / GOAL NOT COMPLETE**

本文件是 A 主控对当前 active goal 的逐项验收映射。它只做审计与证据归档，不修改 runtime、前端代码、测试代码或卡牌矩阵。任何 verifier、manifest、历史 green test、Chrome smoke 或 18 步 E2E 都只能作为对应门槛的证据，不能单独代理完整 READY。

## 1. 目标重述

当前 active goal 可拆成以下可验收交付：

1. A 作为主控架构 / 规划 / 验收 agent，按 `docs/A_MASTER_AGENT_GOAL.md` 管理本地双人 1v1 标准构筑 Web 游戏基线。
2. A 维护 checkpoint、任务拆分、子 agent 分工、阻断清单、写入范围、验收与 completion audit。
3. 默认不亲自写功能代码；只允许 A 侧审计、文档、checkpoint、测试运行与 diff 检查。
4. 服务端保持唯一规则权威。
5. 前端只展示服务端 authoritative snapshot，并只提交服务端 `ActionPrompt` / `LegalAction` 支持的合法动作。
6. P0/P1 阻断清零。
7. 后端 full test 全绿。
8. 前端 build / typecheck / lint 门槛全绿。
9. Chrome smoke 通过。
10. 正式 18 步 E2E 通过。
11. 1009 张 card entry / 811 functional unit 卡牌覆盖矩阵完成，`cardId` / `collectorId` / `oracleId` / `effectId` / FAQ / tests 映射完整。
12. 最终 completion audit 输出 READY 后，才允许标记 active goal complete。

## 2. 本次检查过的证据

- `git status --short --branch`：当前 `main`，仅 `riftbound-dotnet.sln` 未跟踪。
- `git log --oneline -8`：本批开始前最新提交为 `000233c8 docs: refresh active goal checklist`，其后接 `a07197c6`、`41c1dab2`、`e4da2691`、`5ffa5bd3`、`edd90689`、`2cf098a5`、`c0eb6b50`。
- `docs/A_MASTER_AGENT_GOAL.md`：目标、阶段门槛、18 步 E2E、checkpoint 与 final audit 要求。
- `docs/CURRENT_A_MASTER_CHECKPOINT.md`：最新 A-master 恢复入口，顶部已记录 4D-03BJ / 03BI / 03BH / 03BG / 03BF / 03BE / 03BD / 03BC。
- `docs/CURRENT_COMPLETION_AUDIT.md`：当前 completion audit 结论仍为 NOT READY。
- `docs/CURRENT_SERVER_RULE_AUDIT.md`：当前服务端 full official rule residual risks。
- `docs/CURRENT_STAGE4D_P0_P1_CLOSURE_PLAN.md`：P0/P1 closure plan 与剩余规则域。
- `docs/CURRENT_STAGE4D_NEXT_DISPATCH_AND_WRITELOCKS.md`：当前 4D-03BJ write lock closed；runtime、frontend、card matrix JSON、fullOfficial / READY 与 `riftbound-dotnet.sln` locked。
- `docs/CURRENT_STAGE4D_03BH_PAYMENT_ENGINE_MISSING_ROW_DOWNSTREAM_COVERAGE_AUDIT.md` 与 evidence：确认 4D-03BC 三个 `missing-official-row` 均有 downstream representative manifest，MOVE_UNIT 仍 policy-deferred。
- `docs/CURRENT_FORMAL_18_STEP_E2E_EVIDENCE.md`：formal 18-step、frontend build、Chrome smoke 历史通过证据；该文件明确不替代 P0/P1、full-card matrix、完整 PaymentEngine 或 LayerEngine。
- `docs/CURRENT_CARD_EFFECT_COVERAGE_BASELINE.md` 与 `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`：卡牌矩阵口径、Azir 4D-03AT representative evidence overlay 与当前 full-official 缺口。
- `src/Riftbound.DevUi/package.json`：`npm run build` 包含 event-label check、user-facing-text check、`tsc -b` 与 Vite build；脚本还定义 `smoke:chrome` 与 `e2e:formal-18`。
- `docs/CURRENT_STAGE4D_03BJ_PAYMENT_ENGINE_REPRESENTATIVE_SEED_UPSTREAM_COVERAGE_AUDIT.md` 与 evidence：确认 4D-03BC 九个 `representative-seed` rows 均有 upstream audit manifest anchors，且不混入 missing-row 口径。

矩阵实测统计：

```txt
snapshotEntries=1009
functionalUnits=811
freezeStatus: IMPLEMENTED_TESTED=76, IMPLEMENTED_UNTESTED=4, NEEDS_ENGINE_SUPPORT=501, NEEDS_FAQ_REVIEW=128, SHARED_ORACLE_IMPLEMENTATION=102
fullOfficialTrue=0
fullOfficialFalse=811
```

当前 4D-03BJ 验证：

```txt
focused PaymentEngine coverage guard=67/67
adjacent PaymentEngine / resource skill / prompt / hub regression=625/625
backend full=4504/4504
```

## 3. 主目标门槛映射

| 要求 | 必需 artifact / gate | 已检查证据 | 当前状态 | 缺口 / 下一步 |
|---|---|---|---|---|
| 按 `docs/A_MASTER_AGENT_GOAL.md` 管理 | A-master 目标文档必须存在并作为最高级本地交付口径 | `docs/A_MASTER_AGENT_GOAL.md` 已读取；goal 文本与该文件一致 | OK / ONGOING | 后续任何 READY 判断都必须回到本 checklist 与 final audit |
| A 维护 checkpoint | `docs/CURRENT_A_MASTER_CHECKPOINT.md` 最新、可恢复、含当前结论 | 文件顶部记录 4D-03BJ accepted、focused 67/67、adjacent 625/625、backend full 4504/4504、项目 NOT READY | OK / ONGOING | 后续每批继续保持 checkpoint 同步 |
| A 维护任务拆分 / 子 agent 分工 | A-master agent pool、写锁、下一步计划 | `A_MASTER_AGENT_GOAL.md` §7/§8；`CURRENT_STAGE4D_NEXT_DISPATCH_AND_WRITELOCKS.md` 已记录 4D-03BJ accepted and no open write locks | ONGOING | 后续 B / frontend / matrix 仍需单独写锁 |
| A 维护阻断清单 | P0/P1 closure plan 与 completion audit | `CURRENT_STAGE4D_P0_P1_CLOSURE_PLAN.md` 与 `CURRENT_COMPLETION_AUDIT.md` 仍为 NOT READY | NOT MET | P0/P1 未清零 |
| A 控制写入范围 | 不并行改核心模块；当前批次只做 focused verifier / docs | 4D-03BJ 后 runtime、frontend、matrix、READY 锁仍关闭；本批不打开实现写锁 | OK FOR THIS SLICE | 后续 runtime / frontend / matrix 改动必须按 dispatch 文档独占 owner |
| 默认不写功能代码 | A 不主动承接功能实现 | 4D-03BJ 只新增 test-only audit verifier 与 docs；不改 runtime / frontend / matrix | OK FOR THIS SLICE | 不代表后续功能缺口已解决 |
| 服务端唯一规则权威 | 服务端输出 authoritative snapshot、prompt、事件、规则裁决 | `CURRENT_SERVER_RULE_AUDIT.md` 与 Stage 4D docs 证明大量 representative server-authority paths | PARTIAL | full official battle / PaymentEngine / LayerEngine / card effects 仍未闭合 |
| 前端只展示 authoritative snapshot | 前端不得持有隐藏信息或本地裁决规则 | `CURRENT_FORMAL_18_STEP_E2E_EVIDENCE.md` 断言页面不暴露 raw hidden-info 文本；frontend plan 多处记录不本地推断 | PARTIAL | 最终前端 contract audit 与 fresh Chrome smoke 仍需在 READY 前复跑 |
| 前端只提交 `ActionPrompt` / `LegalAction` | UI 操作必须来自服务端 prompt | Stage 4D docs 多处记录 ActionPrompt / GameHub representative coverage | PARTIAL | 仍需最终全流程 frontend contract audit，不可用 representative coverage 代理 |
| P0/P1 清零 | completion audit 中所有 P0/P1 为 resolved | closure plan / server audit 明确仍 open / partially resolved | NOT MET | 继续 P0-004、P0-005、LayerEngine、关键词、replay/property、full-card evidence |
| 后端 full test | `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` | 4D-03BJ A-side verification 记录 backend full 4504/4504 通过 | PASS AS LATEST CODE EVIDENCE | 只证明当前代码测试绿；不证明 P0/P1 全部满足 |
| 前端 build / typecheck / lint | `source ../../scripts/dev-env.sh && npm run build` | formal 18 evidence 记录 build 通过；package script 包含 checks、`tsc -b`、Vite build | HISTORICAL PASS | READY 前需要在最终代码状态 fresh run |
| Chrome smoke | `source ../../scripts/dev-env.sh && npm run smoke:chrome -- --start-api` | formal 18 evidence 记录 smoke 通过 | HISTORICAL PASS | READY 前需要结合最终前端状态 fresh run |
| 正式 18 步 E2E | `npm run e2e:formal-18 -- --start-api`，覆盖 A_MASTER §11 1-18 | `CURRENT_FORMAL_18_STEP_E2E_EVIDENCE.md` 记录房间 `formal-18-1778623926434-15` 通过 | PASS FOR MAIN FLOW | 该文件明确不替代 P0/P1、full-card matrix、完整 PaymentEngine / LayerEngine |
| 卡牌覆盖矩阵完成 | 1009 card entries / 811 FUs 都有 official text、effect/oracle、FAQ、tests、full-official status | matrix skeleton 有 1009 / 811；实测 `fullOfficialTrue=0` | NOT MET | 仍不得声明 full-card official coverage |
| final completion audit READY | `docs/CURRENT_COMPLETION_AUDIT.md` 最终输出 READY | 当前文件结论 NOT READY | NOT MET | 禁止 `update_goal complete` |

## 4. A_MASTER 阶段门槛映射

| A_MASTER 项 | 要求 | 当前证据 | 状态 |
|---|---|---|---|
| §2.1 服务端规则权威 | 服务端统一裁决规则 | 代表性 server-authority 证据大量存在 | PARTIAL，full official 未闭合 |
| §2.2 前端产品级稳定精美 | 前端页面稳定可用 | formal 18 / smoke 历史通过 | PARTIAL，最终 fresh smoke 未做 |
| §2.3 本地 / 联机 1v1 | 房间、双玩家、开局、对局 | formal 18 通过双浏览器等效流程 | PASS FOR MAIN FLOW |
| §2.4 可长期维护 | 文档、测试、矩阵、写锁 | checkpoint / closure plan / audit docs 持续维护 | PARTIAL |
| §2.5 P0/P1 清零 | 无阻断 | closure plan 仍列 P0/P1 | NOT MET |
| §2.6 后端 full test | full test 绿 | 4D-03BJ 4504/4504 | PASS BUT NOT SUFFICIENT |
| §2.7 Chrome smoke | smoke 绿 | formal evidence 记录通过 | HISTORICAL PASS |
| §2.8 18 步 E2E | 正式 18 steps 通过 | formal evidence 记录通过 | PASS FOR MAIN FLOW |
| §2.9 卡牌覆盖矩阵 | 矩阵完成 | 811/811 `fullOfficial=false` | NOT MET |
| §2.10 completion audit READY | READY 后才能 complete | current audit NOT READY | NOT MET |
| §4.1 固定 2026-04-27 快照 | 不实时抓取官网改范围 | matrix skeleton 指向 `data/official/card-catalog.zh-CN.json`，source `fetchedAt=2026-04-27` | PARTIAL，矩阵未完成 |
| §4.2 不实时抓取 | 禁止 live 官网污染 | 本批无网络抓取、无数据改动 | OK FOR THIS SLICE |
| §4.3 1009 统计口径 | 定义异画 / token / rune / promo 口径 | coverage baseline 已定义 card entry / collector / FU / full-official | OK AS BASELINE |
| §4.4 覆盖字段 | `cardId`、`collectorId`、`oracleId` / `effectId`、FAQ、tests | matrix skeleton 只有骨架与 representative evidence | NOT MET |
| §4.5 cardId 映射完整 | 复用 effect 但 cardId 完整 | 1009 entries 可统计，full-official 映射未完成 | PARTIAL |
| §5 服务端权威 | 前端不得推断目标、费用、胜负等 | server audit / frontend plan 均要求如此 | PARTIAL，需最终 contract audit |
| §6 A 边界 | A 读文档、规划、审计；默认不写功能代码 | 4D-03BJ 只新增 test-only audit verifier 与 docs | OK FOR THIS SLICE |
| §7 常驻子 agent | 优先复用 B/C/D/E，避免无目的重建 | 本批不派发子 agent；当前无开放写锁 | OK FOR THIS SLICE |
| §8 写入边界 | B/C/D/E 各自写入范围，不并行改核心模块 | dispatch 文档已明确 4D-03BJ closed、runtime/frontend/matrix locked | OK FOR THIS SLICE / ONGOING |
| §9 P0 / P1 定义 | 根据 P0/P1 标准判断 READY | closure plan / server audit 仍有 open risks | NOT MET |
| §10 阶段 0-4 | checkpoint、协议、前端、对战桌面、卡牌覆盖 | Stage 0-3 有大量证据；Stage 4 full-card 未完成 | PARTIAL |
| §10 阶段 5 | full test、build、smoke、18-step、hidden info、P0/P1、matrix、READY | full test / historical build / smoke / 18-step 有证据，P0/P1 与 matrix 未满足 | NOT MET |
| §11 18 步 1-18 | 双浏览器、房间、卡组、开局、出牌、移动、窗口、让过、得分、胜负 | formal evidence table 已逐步映射 1-18 | PASS FOR MAIN FLOW |
| §12 checkpoint 1-14 | 时间、阶段、分支、agent id、任务、已完成/未完成、P0/P1/P2、测试、合并、下一步、禁改文件 | `CURRENT_A_MASTER_CHECKPOINT.md` 是恢复入口；本 checklist 与 dispatch / writelock doc 已挂回该文件顶部 | PARTIAL / ONGOING |
| §13 final audit 1-14 | 修改 / 新增文件、规则、前端、契约、矩阵、隐藏信息、测试、build、smoke、E2E、P0/P1、P2、READY | `CURRENT_COMPLETION_AUDIT.md` 仍是 NOT READY current audit | NOT MET |
| §14 防止项 | 防止多 agent 冲突、前端裁决、泄漏、live data、无证据宣称、未测合并、未审计 READY | 本 checklist 专门阻止 proxy evidence 越权 | OK / ONGOING |

## 5. Final Audit 14 项当前状态

| §13 item | 当前 evidence | 状态 |
|---|---|---|
| 1. 修改文件列表 | 最新批次为 4D-03BJ focused verifier / docs | DONE FOR THIS SLICE / NOT FINAL |
| 2. 新增文件列表 | 4D-03BJ 新增 representative-seed upstream coverage audit/evidence | DONE FOR THIS SLICE / NOT FINAL |
| 3. 服务端规则补齐项 | Stage 4D docs 记录大量 focused slices | PARTIAL |
| 4. 前端页面完成项 | frontend rebuild plan 与 formal smoke 有历史证据 | PARTIAL |
| 5. 接口契约说明 | ActionPrompt / LegalAction / snapshot 证据分散在 server audit 与 frontend plan | PARTIAL |
| 6. 卡牌覆盖矩阵摘要 | 1009 entries / 811 FUs，0 full-official | NOT MET |
| 7. 隐藏信息保护检查结果 | formal 18 页面文本断言、server audit P1-004 代表性 redaction/property evidence | PARTIAL |
| 8. 后端 full test 命令和结果 | 4D-03BJ `dotnet test` 4504/4504 | PASS AS LATEST CODE EVIDENCE |
| 9. 前端 build / typecheck / lint | historical `npm run build` pass | HISTORICAL PASS |
| 10. Chrome smoke | historical `npm run smoke:chrome -- --start-api` pass | HISTORICAL PASS |
| 11. 18 步 E2E | historical formal 18 pass | PASS FOR MAIN FLOW |
| 12. P0/P1 清零证明 | closure plan / server audit show open P0/P1 | NOT MET |
| 13. 剩余 P2 项 | 不能只剩 P2，因为仍有 P0/P1 | NOT MET |
| 14. 最终结论 READY / NOT READY | current audit says NOT READY | NOT READY |

## 6. 不能作为 completion 代理的信号

- `dotnet test` 4504/4504 通过不能替代 P0/P1 清零。
- 4D-03BJ representative-seed upstream coverage 67/67 通过不能替代 full official PaymentEngine matrix；它只证明 nine representative seed rows all have upstream audit manifest anchors and remain separate from missing rows。
- 4D-03BH missing-row downstream coverage 64/64 通过不能替代 full official PaymentEngine matrix；它只证明 three missing official rows all have downstream representative manifests and MOVE_UNIT remains policy-deferred。
- 4D-03BG card matrix alignment row manifest 61/61 通过不能替代 full-card official coverage；它只证明 representative alignment dimensions are executable.
- 4D-03BF cross-window generation / consumption manifest 56/56 与 4D-03BE rollback failure manifest 51/51 不能替代 full official failure / lifetime matrix。
- 4D-03BC official matrix row schema 45/45 与 4D-03BA official matrix residual manifest 39/39 不能替代 generated official matrix coverage。
- 4D-04Q static aura source lifecycle、4D-04P minimum-power ordering、4D-04O power modifier order、4D-04N / 04M / 04L LayerEngine metadata foundations 不能替代完整 LayerEngine。
- 4D-04G / 04F / 04E / 04D / 04C / 04B equipment keyword representative slices 不能替代 P1-002 keyword full official closure。
- 4D-03AS Azir optional armament reattach focused slice与 4D-03AT matrix evidence overlay 不能替代 FAQ review、full swift timing breadth、P0/P1 closure 或 full-official Azir。
- 4D-03AR Maduli cannot-ready focused slice与 matrix readiness audit 不能替代 matrix JSON update 或 full-official Maduli。
- formal 18-step pass 不能替代 strict battlefield contest / battle lifecycle / PaymentEngine / LayerEngine / full-card matrix。
- Chrome smoke pass 不能替代 frontend contract full audit。
- 1009/811 matrix skeleton 存在不能替代 full-official coverage，当前 811/811 都是 `fullOfficial=false`。

## 7. 当前完成判定

Active goal **未完成**。不得调用 `update_goal complete`。

当前最新 A-side 状态是 4D-03BJ PaymentEngine representative seed upstream coverage accepted。P0/P1 清零、full official PaymentEngine matrix、完整 LayerEngine、P1 keyword breadth、frontend final fresh-run、Chrome smoke fresh-run、formal 18 fresh-run、full-card matrix 与 final completion audit READY 仍未闭合。

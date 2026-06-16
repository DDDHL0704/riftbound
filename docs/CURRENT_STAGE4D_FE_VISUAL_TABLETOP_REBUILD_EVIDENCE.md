# Stage 4D Frontend Visual Tabletop Rebuild Evidence

日期：2026-06-16
结论：**VALIDATED / PROJECT NOT READY**

## Scope

本批完成的是前端战斗桌面视觉与 QA 设施重构，不是规则引擎重写。

- 对战桌面改为 playmat-first 布局：左侧竖向功能栏、上方对手区、中央双战场、底部我方手牌、右侧大卡预览与服务端行动面板。
- 右侧焦点卡默认展示我方可见卡牌，避免空白占位；卡背改为更接近参考站的蓝色 League 风格背面。
- 顶部状态、连接按钮、规则队列与符文特性改为低占用 HUD，避免遮挡中央战场。
- 卡牌、手牌、战场单位、待命区、符文/基地/场上对象继续只渲染服务端 authoritative snapshot 中当前 viewer 可见的信息。
- Chrome smoke 只验证空对战 shell 不白屏、不泄漏隐藏字段；复杂对战状态和 prompt 由 Playwright seed 场景验证。

## Rule Sources Checked

根目录五份官方 PDF 已用 `pdftotext` 抽取到 `/tmp/riftbound_rules_pdf_text_current` 并检索规则锚点：

- `《符文战场》核心规则_260330.pdf`
- `裁判FAQ_251023.pdf`
- `铸魂淬炼系列_官方FAQ_260114.pdf`
- `铸魂淬炼系列_裁判FAQ.pdf`
- `《符文战场》破限系列_裁判FAQ_260416.pdf`

本批前端边界继续按现有规则索引执行：

- 战场 / 待命 / 控制：`CORE-260330` p4-p8, p22-p33, p35-p36。
- 战斗 / 法术对决 / 伤害 / 征服 / 据守：`CORE-260330` p26-p36, p62-p63, p77-p78，相关 FAQ 见 `rules-evidence-index.md` 战斗与战场章节。
- 费用 / 额外费用 / 触发费用：`CORE-260330` p39-p42, p52-p55, p61-p67，相关 FAQ 见支付章节。
- 隐藏信息：对手手牌、牌堆顺序、对手面朝下待命和 raw metadata 仍不得暴露给前端。

因此前端仍不得计算支付、合法目标、战斗伤害、法术对决关闭、触发排序、战场控制、征服 / 据守得分或胜负；只能展示服务端 snapshot / prompt / candidate 并提交服务端允许的 command。

## Artifacts

截图目录：

- `src/Riftbound.DevUi/artifacts/appshots/home.png`
- `src/Riftbound.DevUi/artifacts/appshots/cards.png`
- `src/Riftbound.DevUi/artifacts/appshots/decks.png`
- `src/Riftbound.DevUi/artifacts/appshots/room.png`
- `src/Riftbound.DevUi/artifacts/appshots/match-midgame-showcase.png`
- `src/Riftbound.DevUi/artifacts/appshots/prompt-pay-cost.png`

基线目录：

- `src/Riftbound.DevUi/artifacts/baselines/*.png`

报告：

- `src/Riftbound.DevUi/artifacts/playwright-qa-report.json`

Visual diff 目录在最终验证后为空：

- `src/Riftbound.DevUi/artifacts/visual-diff/`

## Validation

```sh
cd src/Riftbound.DevUi
PATH=/Users/dinghaolin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/bin:$PATH npm run build
PATH=/Users/dinghaolin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/bin:$PATH npm run qa:appshots:update
PATH=/Users/dinghaolin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/bin:$PATH npm run qa:appshots
PATH=/Users/dinghaolin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/bin:$PATH npm run smoke:chrome
cd ../..
git diff --check
```

结果：

- `npm run build` 通过；包含 event-label check、user-facing text check、`tsc -b --pretty false` 和 Vite build。
- `npm run qa:appshots:update` 通过；更新 6 张截图基线。
- `npm run qa:appshots` 通过；6 张截图 visual diff ratio 均为 `0`，axe violations 均为 `0`，未发现 raw hidden/debug 字段泄漏。
- `npm run smoke:chrome` 通过；覆盖 `/`, `/lobby`, `/decks`, `/cards`, `/rooms/stage3-smoke`, `/matches/stage3-smoke`, `/matches/stage3-smoke/result`。
- `git diff --check` 通过。

## Non-Closure

项目仍 **NOT READY**。本批不关闭完整 P0/P1、完整 PaymentEngine、完整 LayerEngine、完整 battle / spell-duel lifecycle、full official card matrix、formal 18-step E2E、真实 DB-backed Postgres smoke、`fullOfficial` 或最终 READY。

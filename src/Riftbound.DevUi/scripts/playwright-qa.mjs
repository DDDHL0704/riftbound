import { spawn } from "node:child_process";
import { existsSync } from "node:fs";
import { mkdir, readFile, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import path from "node:path";
import { fileURLToPath } from "node:url";
import axe from "axe-core";
import { PNG } from "pngjs";
import pixelmatch from "pixelmatch";
import { chromium } from "playwright-core";
import * as signalR from "@microsoft/signalr";

const scriptDir = path.dirname(fileURLToPath(import.meta.url));
const appRoot = path.resolve(scriptDir, "..");
const repoRoot = path.resolve(scriptDir, "../../..");
const frontendPort = Number(process.env.RIFTBOUND_QA_FRONTEND_PORT ?? 5176);
const serverUrl = process.env.RIFTBOUND_SERVER_URL ?? "http://127.0.0.1:5088";
const frontendUrl = `http://127.0.0.1:${frontendPort}`;
const updateBaseline = process.argv.includes("--update-baseline");
const baselineDiffEnabled = process.env.RIFTBOUND_QA_BASELINE_DIFF === "1";
const outputRoot = path.resolve(process.env.RIFTBOUND_QA_OUTPUT_ROOT ?? path.join(tmpdir(), "riftbound-dev-ui-qa"));
const appshotDir = path.join(outputRoot, "appshots");
const baselineDir = path.resolve(process.env.RIFTBOUND_QA_BASELINE_ROOT ?? path.join(appRoot, "artifacts", "baselines"));
const diffDir = path.join(outputRoot, "visual-diff");
const reportPath = path.join(outputRoot, "playwright-qa-report.json");
const visualThreshold = Number(process.env.RIFTBOUND_VISUAL_DIFF_THRESHOLD ?? 0.035);

const hiddenDebugTexts = [
  "mainDeck",
  "runeDeck",
  "handHidden",
  "stackItemId",
  "reconnectToken",
  "battleState",
  "damageLedger",
  "participantControllerIds",
  "serverPaymentState",
  "resourceLedgerBeforePayment",
  "triggerQueue",
  "handChoices",
  "legalObjectIds",
  "serverHandChoiceState"
];

const staticShots = [
  { name: "home", path: "/", texts: ["符文战场", "进入大厅"] },
  { name: "cards", path: "/cards", texts: ["卡牌图鉴", "官方卡牌视图"] },
  { name: "decks", path: "/decks", texts: ["构筑导入工作台", "导入到服务端提交的交接", "导入入口", "等待服务端验证", "服务端权威"], allowedDebugTexts: ["mainDeck", "runeDeck"] },
  { name: "room", path: "/rooms/qa-visual-room", texts: ["房间", "流程总览", "连接/重连并入座", "卡组提交", "提交回执"] }
];

const scenarioShots = [
  {
    name: "match-midgame-showcase",
    scenario: "midgame-showcase",
    playerId: "P1",
    texts: ["符文战场对战线框", "窗口总览", "优先权轨道", "规则队列地图", "交互语法", "服务端行动提示", "焦点 / 候选 / 规则队列"]
  },
  {
    name: "prompt-pay-cost",
    scenario: "pay-cost-window",
    playerId: "P1",
    texts: ["符文战场对战线框", "支付费用", "服务端行动提示", "提示契约", "paymentId"]
  }
];

const sidePanelTabBySlot = {
  actionMap: "action",
  actionPrompt: "action",
  commandCenter: "action",
  informationBoundary: "detail",
  interaction: "action",
  log: "log",
  overview: "detail",
  promptAuthority: "detail",
  responseCoach: "response",
  ruleQueue: "rules",
  serverFlow: "rules",
  tableAuthority: "detail",
  timelineDetail: "detail",
  turnWindow: "response"
};

const children = [];
let browser;

try {
  await mkdir(appshotDir, { recursive: true });
  if (updateBaseline || baselineDiffEnabled) {
    await mkdir(baselineDir, { recursive: true });
  }
  await mkdir(diffDir, { recursive: true });
  await ensureApi();
  await ensurePreview();

  browser = await chromium.launch({
    executablePath: chromePath(),
    headless: true,
    args: ["--disable-gpu", "--no-first-run", "--no-default-browser-check"]
  });

  const report = {
    generatedAt: new Date().toISOString(),
    frontendUrl,
    serverUrl,
    baselineDiffEnabled,
    outputRoot,
    interactions: [],
    updateBaseline,
    shots: []
  };

  for (const shot of staticShots) {
    const page = await newPage();
    await page.goto(`${frontendUrl}${shot.path}`, { waitUntil: "networkidle" });
    await assertTexts(page, shot.texts);
    if (shot.name === "decks") {
      await assertDeckImportSurface(page);
    }
    if (shot.name === "room") {
      await assertRoomWorkflowSurface(page);
    }
    await captureAndAudit(page, shot, report);
    await page.close();
  }

  for (const shot of scenarioShots) {
    const seeded = await createSeededRoom(shot.scenario);
    try {
      const page = await newPage();
      await openSeededMatch(page, seeded, shot.playerId);
      await assertTexts(page, shot.texts);
      await assertServerFlow(page);
      await assertMatchStateSurface(page, shot);
      await captureAndAudit(page, shot, report);
      await page.close();
    } finally {
      await seeded.close();
    }
  }

  await runObjectCommandTrayInteraction(report);
  await runCommandReceiptInteraction(report);
  await runReadyReceiptInteraction(report);

  await writeFile(reportPath, `${JSON.stringify(report, null, 2)}\n`);
  console.log(`Playwright QA passed. Report: ${reportPath}`);
} finally {
  if (browser) {
    await browser.close().catch(() => undefined);
  }

  for (const child of children.reverse()) {
    await stopChild(child);
  }
}

async function newPage() {
  const context = await browser.newContext({
    deviceScaleFactor: 1,
    viewport: { width: 1440, height: 810 }
  });
  await context.addInitScript(({ server }) => {
    localStorage.setItem("riftbound.serverUrl", server);
    localStorage.setItem("riftbound.animationLevel", "off");
    localStorage.setItem("riftbound.logDensity", "detailed");
  }, { server: serverUrl });

  const page = await context.newPage();
  page.on("pageerror", (error) => {
    throw error;
  });
  page.on("console", (message) => {
    if (message.type() === "error" && !isIgnorableConsoleError(message.text())) {
      throw new Error(`console.error: ${message.text()}`);
    }
  });
  return page;
}

async function captureAndAudit(page, shot, report) {
  await hideDynamicText(page);
  await waitForCardImages(page);
  await expectNoHiddenDebugText(page, shot.allowedDebugTexts ?? []);
  const screenshotPath = path.join(appshotDir, `${shot.name}.png`);
  const buffer = await page.screenshot({ fullPage: false, path: screenshotPath });
  assertNonBlank(buffer, shot.name);
  await assertWireframeVisual(buffer, shot.name);
  const visual = await compareOrUpdateVisual(shot.name, buffer);
  const accessibility = await runAccessibilitySmoke(page, shot.name);
  report.shots.push({
    name: shot.name,
    path: screenshotPath,
    visual,
    accessibility
  });
  console.log(`QA shot OK: ${shot.name}`);
}

async function assertDeckImportSurface(page) {
  const surface = await page.evaluate(() => {
    const textOf = (node) => node?.textContent?.trim().replace(/\s+/g, " ") ?? "";
    const root = document.querySelector("[data-deck-import-surface]");
    const commandPreview = document.querySelector("[data-deck-import-command-preview]");
    const editor = document.querySelector("[data-deck-import-editor]");
    const feedback = document.querySelector("[data-deck-import-feedback]");
    const flowState = document.querySelector("[data-deck-import-flow-state]");
    const handoff = document.querySelector("[data-deck-import-handoff]");
    const input = document.querySelector("[data-deck-import-input]");
    const summary = document.querySelector("[data-deck-import-summary]");
    return {
      actions: Array.from(document.querySelectorAll("[data-deck-import-action]")).map((node) => ({
        id: node.getAttribute("data-deck-import-action") ?? "",
        state: node.getAttribute("data-deck-import-action-state") ?? "",
        text: textOf(node)
      })),
      commandLength: Number(commandPreview?.getAttribute("data-deck-import-command-length") ?? root?.getAttribute("data-deck-import-command-length") ?? 0),
      commandText: textOf(commandPreview),
      editorText: textOf(editor),
      feedbackState: feedback?.getAttribute("data-deck-import-state") ?? "",
      feedbackText: textOf(feedback),
      flowState: flowState?.getAttribute("data-deck-import-flow-state") ?? "",
      flowSteps: Array.from(document.querySelectorAll("[data-deck-import-flow-step]")).map((node) => ({
        id: node.getAttribute("data-deck-import-flow-step") ?? "",
        state: node.getAttribute("data-deck-import-flow-step-state") ?? "",
        text: textOf(node)
      })),
      handoffActiveSection: handoff?.getAttribute("data-deck-import-handoff-active-section") ?? "",
      handoffSections: Array.from(document.querySelectorAll("[data-deck-import-handoff-section]")).map((node) => ({
        id: node.getAttribute("data-deck-import-handoff-section") ?? "",
        source: node.getAttribute("data-deck-import-handoff-source") ?? "",
        state: node.getAttribute("data-deck-import-handoff-state") ?? "",
        text: textOf(node)
      })),
      handoffSummary: handoff?.getAttribute("data-deck-import-handoff-summary") ?? "",
      inputState: input?.getAttribute("data-deck-import-state") ?? "",
      rootState: root?.getAttribute("data-deck-import-state") ?? "",
      summaryMetrics: Array.from(document.querySelectorAll("[data-deck-import-summary-metric]")).map((node) => ({
        key: node.getAttribute("data-deck-import-summary-key") ?? "",
        text: textOf(node),
        value: node.getAttribute("data-deck-import-summary-value") ?? ""
      })),
      summaryText: textOf(summary),
      surfaceText: textOf(root)
    };
  });

  const failures = [];
  if (surface.rootState !== "valid" || surface.inputState !== "valid" || surface.feedbackState !== "valid" || surface.flowState !== "valid") {
    failures.push(`deck import states should be valid for default starter text: ${JSON.stringify({
      feedback: surface.feedbackState,
      flow: surface.flowState,
      input: surface.inputState,
      root: surface.rootState
    })}`);
  }
  if (!Number.isFinite(surface.commandLength) || surface.commandLength < 500) {
    failures.push(`deck import command preview should expose a durable SUBMIT_DECK payload length: ${surface.commandLength}`);
  }
  if (!surface.commandText.includes("legendCardNo") || !surface.commandText.includes("mainDeck") || !surface.commandText.includes("runeDeck")) {
    failures.push(`deck import command preview must expose command fields while leaving server legality authoritative: ${surface.commandText}`);
  }
  if (!surface.editorText.includes("导入入口")) {
    failures.push(`deck import editor must render the paste intake region: ${surface.editorText}`);
  }
  if (!surface.summaryText.includes("主牌堆") || !surface.summaryText.includes("符文牌堆") || !surface.summaryText.includes("战场池")) {
    failures.push(`deck import summary must render main/rune/battlefield sections: ${surface.summaryText}`);
  }
  if (surface.flowSteps.length < 3 || surface.flowSteps.some((step) => !step.id || !step.state)) {
    failures.push(`deck import flow steps must expose ids and states: ${JSON.stringify(surface.flowSteps)}`);
  }
  const expectedHandoffIds = ["intake", "recovery", "current", "command", "server"];
  const handoffIds = surface.handoffSections.map((section) => section.id);
  if (surface.handoffSections.length !== expectedHandoffIds.length || expectedHandoffIds.some((id) => !handoffIds.includes(id))) {
    failures.push(`deck import handoff sections must include ${expectedHandoffIds.join(", ")}: ${JSON.stringify(surface.handoffSections)}`);
  }
  if (surface.handoffActiveSection !== "command") {
    failures.push(`deck import handoff should route default valid import to command, got ${surface.handoffActiveSection}`);
  }
  for (const source of ["local-editor", "local-cache", "local-state", "generated-command", "server-authority"]) {
    if (!surface.handoffSections.some((section) => section.source === source && section.state)) {
      failures.push(`deck import handoff missing sourced section ${source}: ${JSON.stringify(surface.handoffSections)}`);
    }
  }
  if (!surface.handoffSummary.includes("命令：40/12/3")) {
    failures.push(`deck import handoff summary must expose command count context: ${surface.handoffSummary}`);
  }
  const summaryKeys = surface.summaryMetrics.map((metric) => metric.key);
  for (const key of ["legend", "champion", "main", "runes", "battlefields"]) {
    if (!summaryKeys.includes(key)) {
      failures.push(`deck import summary missing ${key}: ${JSON.stringify(surface.summaryMetrics)}`);
    }
  }
  for (const copy of ["服务端权威", "SUBMIT_DECK", "主牌堆", "符文牌堆", "战场池"]) {
    if (!surface.surfaceText.includes(copy)) {
      failures.push(`deck import surface missing ${copy} copy: ${surface.surfaceText}`);
    }
  }
  const actionsById = Object.fromEntries(surface.actions.map((action) => [action.id, action]));
  if (actionsById.apply?.state !== "ready") {
    failures.push(`deck import apply action should be ready for default valid starter text: ${JSON.stringify(surface.actions)}`);
  }
  for (const actionId of ["load-current", "reset"]) {
    if (actionsById[actionId]?.state !== "available") {
      failures.push(`deck import ${actionId} action should stay available: ${JSON.stringify(surface.actions)}`);
    }
  }
  if (failures.length > 0) {
    throw new Error(`Deck import surface assertions failed:\n${failures.join("\n")}`);
  }
}

async function assertRoomWorkflowSurface(page) {
  const surface = await page.evaluate(() => {
    const textOf = (node) => node?.textContent?.trim().replace(/\s+/g, " ") ?? "";
    const workflow = document.querySelector("[data-room-workflow-surface]");
    const errors = document.querySelector("[data-room-errors-region]");
    const submission = document.querySelector("[data-room-submission-region]");
    return {
      activeRegion: workflow?.getAttribute("data-room-workflow-active-region") ?? "",
      errorsState: errors?.getAttribute("data-error-resolution-state") ?? "",
      errorsText: textOf(errors),
      hasActionRegion: Boolean(document.querySelector("[data-room-actions-region]")),
      hasErrorRegion: Boolean(errors),
      hasLogRegion: Boolean(document.querySelector("[data-room-log-region]")),
      hasRecoveryRegion: Boolean(document.querySelector("[data-room-recovery-region]")),
      hasSetupRegion: Boolean(document.querySelector("[data-room-setup-region]")),
      hasSubmissionRegion: Boolean(submission),
      quickActions: Array.from(document.querySelectorAll("[data-room-quick-action]")).map((node) => ({
        commandSource: node.getAttribute("data-room-quick-action-command-source") ?? "",
        commandSourceLabel: node.getAttribute("data-room-quick-action-command-source-label") ?? "",
        disabled: node.hasAttribute("disabled"),
        id: node.getAttribute("data-room-quick-action") ?? "",
        state: node.getAttribute("data-room-quick-action-state") ?? "",
        text: textOf(node)
      })),
      regions: Array.from(document.querySelectorAll("[data-room-workflow-region]")).map((node) => ({
        id: node.getAttribute("data-room-workflow-region") ?? "",
        source: node.getAttribute("data-room-workflow-source") ?? "",
        state: node.getAttribute("data-room-workflow-state") ?? "",
        text: textOf(node)
      })),
      setupSteps: Array.from(document.querySelectorAll("[data-room-setup-step]")).map((node) => ({
        id: node.getAttribute("data-room-setup-step") ?? "",
        text: textOf(node)
      })),
      submissionState: submission?.getAttribute("data-room-submission-state") ?? "",
      submissionText: textOf(submission),
      summary: workflow?.getAttribute("data-room-workflow-summary") ?? "",
      text: textOf(document.body)
    };
  });

  const failures = [];
  const expectedRegions = ["recovery", "setup", "actions", "submission", "errors", "log"];
  const regionIds = surface.regions.map((region) => region.id);
  if (surface.regions.length !== expectedRegions.length || expectedRegions.some((id) => !regionIds.includes(id))) {
    failures.push(`room workflow must expose all workflow regions: ${JSON.stringify(surface.regions)}`);
  }
  for (const source of ["server-connection", "server-snapshot", "server-prompt", "server-receipt", "server-events"]) {
    if (!surface.regions.some((region) => region.source === source && region.state)) {
      failures.push(`room workflow missing sourced region ${source}: ${JSON.stringify(surface.regions)}`);
    }
  }
  if (surface.activeRegion !== "recovery") {
    failures.push(`room workflow should start in recovery before connection, got ${surface.activeRegion}`);
  }
  if (!surface.summary.includes("连接：") || !surface.summary.includes("行动：")) {
    failures.push(`room workflow summary must expose connection and action counts: ${surface.summary}`);
  }
  for (const flag of ["hasRecoveryRegion", "hasSetupRegion", "hasActionRegion", "hasSubmissionRegion", "hasErrorRegion", "hasLogRegion"]) {
    if (!surface[flag]) {
      failures.push(`room workflow missing marked page region ${flag}`);
    }
  }
  const actionsById = Object.fromEntries(surface.quickActions.map((action) => [action.id, action]));
  if (surface.quickActions.length !== 2) {
    failures.push(`room workflow should expose submitDeck and ready quick actions: ${JSON.stringify(surface.quickActions)}`);
  }
  for (const actionId of ["submitDeck", "ready"]) {
    const action = actionsById[actionId];
    if (action?.state !== "missing" || action?.commandSource !== "unavailable" || action?.disabled !== true) {
      failures.push(`room quick action ${actionId} must be unavailable before connection: ${JSON.stringify(action)}`);
    }
  }
  if (surface.setupSteps.length < 3 || surface.setupSteps.some((step) => !step.id || !step.text.includes("下一步"))) {
    failures.push(`room setup steps must expose ids and next steps: ${JSON.stringify(surface.setupSteps)}`);
  }
  if (surface.submissionState !== "empty" || !surface.submissionText.includes("提交回执")) {
    failures.push(`room submission receipt should start empty and readable: ${JSON.stringify({
      state: surface.submissionState,
      text: surface.submissionText
    })}`);
  }
  if (!surface.errorsState || !surface.errorsText.includes("错误处理")) {
    failures.push(`room error resolution surface must expose state and copy: ${JSON.stringify({
      state: surface.errorsState,
      text: surface.errorsText
    })}`);
  }
  for (const copy of ["连接/重连", "卡组提交", "提交回执", "错误处理", "服务端消息"]) {
    if (!surface.text.includes(copy)) {
      failures.push(`room workflow page missing ${copy} copy: ${surface.text}`);
    }
  }
  if (failures.length > 0) {
    throw new Error(`Room workflow surface assertions failed:\n${failures.join("\n")}`);
  }
}

async function assertServerFlow(page) {
  const activeSlot = await page.locator("[data-wire-side-panel-directory]").first().getAttribute("data-wire-side-panel-directory-active-slot");
  await openSidePanelSlot(page, "serverFlow");
  const flow = page.locator("[data-wire-server-flow-state]").first();
  await flow.waitFor({ timeout: 10_000 });
  const state = await flow.getAttribute("data-wire-server-flow-state");
  const text = await flow.textContent() ?? "";
  if (!state) {
    throw new Error("Server flow panel is missing state.");
  }

  if (!text.includes("下一步")) {
    throw new Error(`Server flow panel is missing next-step copy: ${text}`);
  }

  if (activeSlot && activeSlot !== "serverFlow") {
    await openSidePanelSlot(page, activeSlot);
  }
}

async function assertMatchStateSurface(page, shot) {
  const activeSlot = await page.locator("[data-wire-side-panel-directory]").first().getAttribute("data-wire-side-panel-directory-active-slot");
  await openSidePanelSlot(page, "ruleQueue");
  const surface = await page.evaluate(() => {
    const textOf = (node) => node?.textContent?.trim().replace(/\s+/g, " ") ?? "";
    const scoreTokens = Array.from(document.querySelectorAll(".tabletop-score-token")).map((node) => ({
      label: node.getAttribute("aria-label") ?? "",
      text: textOf(node)
    }));
    const battlefieldScoreSurfaces = Array.from(document.querySelectorAll("[data-wire-battlefield-score-state]")).map((node) => ({
      count: node.getAttribute("data-wire-battlefield-scored-player-count") ?? "",
      players: node.getAttribute("data-wire-battlefield-scored-players") ?? "",
      state: node.getAttribute("data-wire-battlefield-score-state") ?? "",
      text: textOf(node)
    }));
    const recoverySurface = document.querySelector("[data-match-recovery-surface]");
    const operation = document.querySelector("[data-wire-side-panel-operation-state]");
    const stateRail = document.querySelector("[data-wire-side-panel-state-rail]");
    const ruleChain = document.querySelector("[data-wire-side-panel-rule-chain-state]");
    return {
      battlefieldScoreSurfaces,
      operationActive: operation?.getAttribute("data-wire-side-panel-operation-active") ?? "",
      operationReadyCount: operation?.getAttribute("data-wire-side-panel-operation-ready-count") ?? "",
      operationSections: Array.from(document.querySelectorAll("[data-wire-side-panel-operation-section]")).map((node) => ({
        active: node.getAttribute("data-wire-side-panel-operation-section-active") ?? "",
        count: node.getAttribute("data-wire-side-panel-operation-section-count") ?? "",
        key: node.getAttribute("data-wire-side-panel-operation-section") ?? "",
        primarySlot: node.querySelector("[data-wire-side-panel-operation-section-primary]")?.getAttribute("data-wire-side-panel-operation-section-primary") ?? "",
        state: node.getAttribute("data-wire-side-panel-operation-section-state") ?? "",
        text: textOf(node)
      })),
      operationRoutes: Array.from(document.querySelectorAll("[data-wire-side-panel-operation-route]")).map((node) => ({
        key: node.getAttribute("data-wire-side-panel-operation-route") ?? "",
        slot: node.getAttribute("data-wire-side-panel-operation-route-slot") ?? "",
        state: node.getAttribute("data-wire-side-panel-operation-route-state") ?? "",
        text: textOf(node)
      })),
      operationState: operation?.getAttribute("data-wire-side-panel-operation-state") ?? "",
      operationText: textOf(operation),
      recoveryActiveRegion: recoverySurface?.getAttribute("data-match-recovery-active-region") ?? "",
      recoveryRegions: Array.from(document.querySelectorAll("[data-match-recovery-region]")).map((node) => ({
        id: node.getAttribute("data-match-recovery-region") ?? "",
        source: node.getAttribute("data-match-recovery-source") ?? "",
        state: node.getAttribute("data-match-recovery-region-state") ?? "",
        text: textOf(node)
      })),
      recoveryState: recoverySurface?.getAttribute("data-match-recovery-state") ?? "",
      recoverySummary: recoverySurface?.getAttribute("data-match-recovery-summary") ?? "",
      recoveryText: textOf(recoverySurface),
      ruleChainAria: ruleChain?.getAttribute("aria-label") ?? "",
      ruleChainLanes: Array.from(document.querySelectorAll("[data-wire-side-panel-rule-chain-lane]")).map((node) => ({
        count: node.getAttribute("data-wire-side-panel-rule-chain-lane-count") ?? "",
        key: node.getAttribute("data-wire-side-panel-rule-chain-lane") ?? "",
        state: node.getAttribute("data-wire-side-panel-rule-chain-lane-state") ?? "",
        text: textOf(node)
      })),
      ruleChainMetrics: Array.from(document.querySelectorAll("[data-wire-side-panel-rule-chain-metric]")).map((node) => ({
        key: node.getAttribute("data-wire-side-panel-rule-chain-metric") ?? "",
        text: textOf(node)
      })),
      ruleChainRoutes: Array.from(document.querySelectorAll("[data-wire-side-panel-rule-chain-route]")).map((node) => ({
        key: node.getAttribute("data-wire-side-panel-rule-chain-route") ?? "",
        state: node.getAttribute("data-wire-side-panel-rule-chain-route-state") ?? "",
        text: textOf(node)
      })),
      ruleChainState: ruleChain?.getAttribute("data-wire-side-panel-rule-chain-state") ?? "",
      ruleChainText: textOf(ruleChain),
      scoreTokens,
      stateRailSummary: stateRail?.getAttribute("data-wire-side-panel-state-summary") ?? "",
      stateRailText: textOf(stateRail),
      stateRailMetrics: Array.from(document.querySelectorAll("[data-wire-side-panel-state-metric]")).map((node) => ({
        key: node.getAttribute("data-wire-side-panel-state-key") ?? "",
        source: node.getAttribute("data-wire-side-panel-state-source") ?? "",
        state: node.getAttribute("data-wire-side-panel-state") ?? "",
        value: node.getAttribute("data-wire-side-panel-state-value") ?? "",
        text: textOf(node)
      }))
    };
  });

  const failures = [];
  if (surface.scoreTokens.length < 2) {
    failures.push(`expected at least two tabletop score tokens, got ${surface.scoreTokens.length}`);
  }
  for (const token of surface.scoreTokens) {
    if (!token.label.includes("分数")) {
      failures.push(`score token missing score label: ${JSON.stringify(token)}`);
    }
    if (!token.text.includes("主牌") || !token.text.includes("符文")) {
      failures.push(`score token must keep deck/rune context: ${JSON.stringify(token)}`);
    }
  }
  if (surface.battlefieldScoreSurfaces.length === 0) {
    failures.push("expected battlefield score surfaces from server snapshot.");
  }
  if (!surface.recoveryState || !surface.recoveryActiveRegion) {
    failures.push(`match recovery surface missing state/active region: ${JSON.stringify({
      activeRegion: surface.recoveryActiveRegion,
      state: surface.recoveryState
    })}`);
  }
  if (surface.recoveryRegions.length !== 4) {
    failures.push(`match recovery surface must expose four regions, got ${surface.recoveryRegions.length}: ${JSON.stringify(surface.recoveryRegions)}`);
  }
  for (const requiredRegion of ["connection", "snapshot", "submission", "errors"]) {
    if (!surface.recoveryRegions.some((region) => region.id === requiredRegion && region.source)) {
      failures.push(`match recovery surface missing sourced region ${requiredRegion}: ${JSON.stringify(surface.recoveryRegions)}`);
    }
  }
  for (const requiredCopy of ["连接", "快照", "提交", "错误"]) {
    if (!surface.recoverySummary.includes(requiredCopy) || !surface.recoveryText.includes(requiredCopy)) {
      failures.push(`match recovery surface missing ${requiredCopy} copy: summary=${surface.recoverySummary} text=${surface.recoveryText}`);
    }
  }
  if (!surface.battlefieldScoreSurfaces.some((surface) => surface.text.includes("本回合") && surface.text.includes("得分"))) {
    failures.push(`battlefield score surfaces must expose this-turn scoring copy: ${JSON.stringify(surface.battlefieldScoreSurfaces)}`);
  }
  for (const battlefieldSurface of surface.battlefieldScoreSurfaces) {
    if (!battlefieldSurface.state || !battlefieldSurface.count) {
      failures.push(`battlefield score surface missing state/count: ${JSON.stringify(battlefieldSurface)}`);
    }
  }
  if (!surface.ruleChainState) {
    failures.push("rule chain strip is missing state.");
  }
  if (surface.stateRailMetrics.length < 10) {
    failures.push(`state rail must expose ten server boundary metrics, got ${surface.stateRailMetrics.length}`);
  }
  for (const requiredKey of ["connection", "snapshot", "prompt", "candidates", "stack", "tasks", "triggers", "events", "submission", "receipt"]) {
    if (!surface.stateRailMetrics.some((metric) => metric.key === requiredKey && metric.source)) {
      failures.push(`state rail missing sourced metric ${requiredKey}: ${JSON.stringify(surface.stateRailMetrics)}`);
    }
  }
  if (!surface.stateRailSummary.includes("tick") || !surface.stateRailSummary.includes("候选")) {
    failures.push(`state rail summary must expose tick and candidate context: ${surface.stateRailSummary}`);
  }
  if (!surface.stateRailText.includes("快照") || !surface.stateRailText.includes("候选")) {
    failures.push(`state rail must render readable snapshot/candidate labels: ${surface.stateRailText}`);
  }
  if (!surface.ruleChainAria.includes("规则链")) {
    failures.push(`rule chain strip is missing aria label: ${surface.ruleChainAria}`);
  }
  if (surface.ruleChainLanes.length === 0) {
    failures.push("rule chain strip is missing lanes.");
  }
  if (surface.ruleChainMetrics.length === 0) {
    failures.push("rule chain strip is missing metrics.");
  }
  if (surface.ruleChainRoutes.length === 0) {
    failures.push("rule chain strip is missing routes.");
  }
  if (!surface.ruleChainText.includes("下一步")) {
    failures.push(`rule chain strip is missing next-step text: ${surface.ruleChainText}`);
  }
  if (!surface.operationState || !surface.operationActive || !surface.operationReadyCount) {
    failures.push(`operation panel missing state/active/ready metrics: ${JSON.stringify({
      active: surface.operationActive,
      readyCount: surface.operationReadyCount,
      state: surface.operationState
    })}`);
  }
  for (const requiredSection of ["focus", "prompt", "rules", "commands"]) {
    if (!surface.operationSections.some((section) => section.key === requiredSection && section.state && section.primarySlot)) {
      failures.push(`operation panel missing sourced section ${requiredSection}: ${JSON.stringify(surface.operationSections)}`);
    }
  }
  for (const requiredSlot of ["interaction", "actionPrompt", "ruleQueue", "serverFlow", "commandCenter", "responseCoach"]) {
    if (!surface.operationRoutes.some((route) => route.slot === requiredSlot && route.state)) {
      failures.push(`operation panel missing sourced route to ${requiredSlot}: ${JSON.stringify(surface.operationRoutes)}`);
    }
  }
  if (!surface.operationText.includes("规则操作") || !surface.operationText.includes("入口")) {
    failures.push(`operation panel must render readable operation/route copy: ${surface.operationText}`);
  }

  if (activeSlot && activeSlot !== "ruleQueue") {
    await openSidePanelSlot(page, activeSlot);
  }

  if (failures.length > 0) {
    throw new Error(`Seeded match state surface failed for ${shot.name}: ${failures.join("; ")}`);
  }
}

async function openSidePanelSlot(page, slot) {
  const tab = sidePanelTabBySlot[slot];
  if (!tab) {
    throw new Error(`Unknown side panel slot for QA: ${slot}`);
  }

  await page.locator(`[data-wire-side-panel-tab="${tab}"]`).click();
  await page.locator(`[data-wire-side-panel-directory-link="${slot}"]`).click();
  await page.locator(`[data-wire-side-panel-directory][data-wire-side-panel-directory-active-slot="${slot}"]`).waitFor({ timeout: 5_000 });
}

async function waitForCardImages(page) {
  await page.waitForFunction(() => {
    const images = Array.from(document.querySelectorAll("img.card-full-image"));
    return images.length === 0 || images.every((image) => image.complete);
  }, null, { timeout: 10_000 }).catch(() => undefined);
  await page.waitForTimeout(150);
}

async function hideDynamicText(page) {
  await page.addStyleTag({
    content: `
      .match-command-meta,
      .match-topbar-title .eyebrow,
      .nav-footnote {
        visibility: hidden !important;
      }
    `
  });
}

async function runAccessibilitySmoke(page, name) {
  await page.addScriptTag({ content: axe.source });
  const result = await page.evaluate(async () => {
    return await globalThis.axe.run(document, {
      resultTypes: ["violations"],
      rules: {
        "color-contrast": { enabled: true },
        "button-name": { enabled: true },
        "label": { enabled: true }
      }
    });
  });

  const violations = result.violations ?? [];
  const blocking = violations.filter((violation) =>
    ["critical", "serious"].includes(String(violation.impact ?? ""))
    || ["button-name", "label", "aria-hidden-focus", "nested-interactive"].includes(violation.id)
  );
  if (blocking.length > 0) {
    const summary = blocking
      .map((violation) => `${violation.id}: ${violation.nodes.map((node) => node.target.join(" ")).slice(0, 3).join(", ")}`)
      .join("\n");
    throw new Error(`Accessibility smoke failed for ${name}:\n${summary}`);
  }

  return {
    checked: true,
    violationCount: violations.length,
    violations: violations.map((violation) => ({
      id: violation.id,
      impact: violation.impact,
      nodeCount: violation.nodes.length
    }))
  };
}

async function compareOrUpdateVisual(name, currentBuffer) {
  const baselinePath = path.join(baselineDir, `${name}.png`);
  const diffPath = path.join(diffDir, `${name}.png`);

  if (!updateBaseline && !baselineDiffEnabled) {
    await rm(diffPath, { force: true });
    return { status: "wireframe-invariant", ratio: null };
  }

  if (updateBaseline || !existsSync(baselinePath)) {
    await writeFile(baselinePath, currentBuffer);
    await rm(diffPath, { force: true });
    return { status: updateBaseline ? "updated-baseline" : "created-baseline", ratio: 0 };
  }

  const baselineBuffer = await readFile(baselinePath);
  const current = PNG.sync.read(currentBuffer);
  const baseline = PNG.sync.read(baselineBuffer);
  if (current.width !== baseline.width || current.height !== baseline.height) {
    throw new Error(`Visual baseline size mismatch for ${name}: current ${current.width}x${current.height}, baseline ${baseline.width}x${baseline.height}`);
  }

  const diff = new PNG({ width: current.width, height: current.height });
  const diffPixels = pixelmatch(current.data, baseline.data, diff.data, current.width, current.height, {
    threshold: 0.12,
    includeAA: true
  });
  const ratio = diffPixels / (current.width * current.height);
  if (diffPixels > 0) {
    await writeFile(diffPath, PNG.sync.write(diff));
  } else {
    await rm(diffPath, { force: true });
  }
  if (ratio > visualThreshold) {
    throw new Error(`Visual diff for ${name} is ${(ratio * 100).toFixed(2)}%, threshold ${(visualThreshold * 100).toFixed(2)}%. Diff: ${diffPath}`);
  }

  return { status: "compared", ratio, diffPixels };
}

async function assertWireframeVisual(buffer, name) {
  const image = PNG.sync.read(buffer);
  let sampled = 0;
  let nearWhite = 0;
  let nearBlack = 0;
  let dark = 0;
  const stride = 4 * 37;

  for (let index = 0; index < image.data.length; index += stride) {
    const red = image.data[index];
    const green = image.data[index + 1];
    const blue = image.data[index + 2];
    const alpha = image.data[index + 3];
    if (alpha < 16) {
      continue;
    }

    sampled += 1;
    const luminance = (red * 0.2126) + (green * 0.7152) + (blue * 0.0722);
    if (red >= 238 && green >= 238 && blue >= 238) {
      nearWhite += 1;
    }
    if (red <= 72 && green <= 72 && blue <= 72) {
      nearBlack += 1;
    }
    if (luminance < 90) {
      dark += 1;
    }
  }

  const nearWhiteRatio = nearWhite / sampled;
  const nearBlackRatio = nearBlack / sampled;
  const darkRatio = dark / sampled;
  const failures = [];
  if (nearWhiteRatio < 0.34) {
    failures.push(`nearWhite=${nearWhiteRatio.toFixed(3)}`);
  }
  if (nearBlackRatio < 0.002) {
    failures.push(`nearBlack=${nearBlackRatio.toFixed(3)}`);
  }
  if (darkRatio > 0.55) {
    failures.push(`dark=${darkRatio.toFixed(3)}`);
  }
  if (failures.length > 0) {
    throw new Error(`Wireframe visual invariant failed for ${name}: ${failures.join(", ")}`);
  }

  return {
    checked: true,
    darkRatio,
    nearBlackRatio,
    nearWhiteRatio
  };
}

function assertNonBlank(buffer, name) {
  const image = PNG.sync.read(buffer);
  const seen = new Set();
  let opaque = 0;
  for (let index = 0; index < image.data.length; index += 4 * 257) {
    const alpha = image.data[index + 3];
    if (alpha > 0) {
      opaque++;
      seen.add(`${image.data[index]},${image.data[index + 1]},${image.data[index + 2]}`);
    }
  }

  if (opaque < 20 || seen.size < 12) {
    throw new Error(`Screenshot ${name} looks blank or nearly blank.`);
  }
}

async function expectNoHiddenDebugText(page, allowedTexts = []) {
  const bodyText = await page.locator("body").innerText();
  const allowed = new Set(allowedTexts);
  const leaked = hiddenDebugTexts.filter((text) => !allowed.has(text) && bodyText.includes(text));
  if (leaked.length > 0) {
    throw new Error(`Hidden/debug metadata leaked into DOM text: ${leaked.join(", ")}`);
  }
}

async function assertTexts(page, texts) {
  const bodyText = await page.locator("body").evaluate((body) => body.textContent ?? "");
  const missing = texts.filter((text) => !bodyText.includes(text));
  if (missing.length === 0) {
    return;
  }

  for (const text of missing) {
    await page.getByText(text, { exact: false }).first().waitFor({ timeout: 15_000 });
  }
}

async function openSeededMatch(page, seeded, playerId) {
  await page.goto(frontendUrl, { waitUntil: "networkidle" });
  const session = seeded.sessions[playerId];
  await page.evaluate(({ player, roomId, server, storedSession }) => {
    localStorage.setItem("riftbound.serverUrl", server);
    localStorage.setItem("riftbound.playerId", player);
    localStorage.setItem(`riftbound.session.${roomId}.${player}`, JSON.stringify(storedSession));
  }, {
    player: playerId,
    roomId: seeded.roomId,
    server: serverUrl,
    storedSession: session
  });
  await page.goto(`${frontendUrl}/matches/${seeded.roomId}`, { waitUntil: "networkidle" });
  await page.getByRole("button", { name: /^(连接|连接\/重连)$/ }).click();
  await page.waitForFunction((expectedPlayerId) => document.body.textContent?.includes(expectedPlayerId), playerId, { timeout: 15_000 });
}

async function runCommandReceiptInteraction(report) {
  const seeded = await createSeededRoom("basic-play");
  try {
    const page = await newPage();
    await openSeededMatch(page, seeded, "P1");
    await openSidePanelSlot(page, "commandCenter");
    await assertTexts(page, ["提交反馈", "尚未提交"]);
    const initialState = await page.locator("[data-command-submission-state]").first().getAttribute("data-command-submission-state");
    if (initialState !== "empty") {
      throw new Error(`Command submission feedback should start empty, got ${initialState}`);
    }

    const route = await submitReceiptProbeCommand(page);
    const receipt = await waitForAcceptedSubmissionFeedback(page, "END_TURN");
    await openSidePanelSlot(page, "commandCenter");
    const receiptLayer = await openCommandReceiptLayer(page, "END_TURN");

    report.interactions.push({
      name: "command-receipt-feedback",
      route,
      commandCenterFollowupState: receipt.commandCenterFollowupState,
      commandCenterServerState: receipt.commandCenterServerState,
      layerFollowupState: receiptLayer.followupState,
      layerServerState: receiptLayer.serverState,
      followupState: receipt.followupState,
      state: receipt.state
    });
    console.log(`QA interaction OK: command-receipt-feedback (${route})`);
    await page.close();
  } finally {
    await seeded.close();
  }
}

async function runObjectCommandTrayInteraction(report) {
  const page = await newPage();
  try {
    await page.goto(`${frontendUrl}/matches/qa-layout?fixture=layout`, { waitUntil: "networkidle" });
    await assertTexts(page, ["符文战场对战线框", "焦点 / 候选 / 规则队列"]);
    await assertRailAction(page, "rules", "ruleQueue");
    await openSidePanelSlot(page, "serverFlow");
    const serverFlow = page.locator("[data-wire-server-flow-related-count]").first();
    await serverFlow.waitFor({ timeout: 10_000 });
    const relatedCount = await serverFlow.getAttribute("data-wire-server-flow-related-count");
    if (relatedCount !== "3") {
      throw new Error(`Expected 3 server-flow related objects in layout fixture, got ${relatedCount}`);
    }

    const serverFlowTargetRef = page.locator('.wire-server-flow [data-rule-object-ref="p2-right-1"][data-object-ref-inspectable="true"]').first();
    await serverFlowTargetRef.waitFor({ timeout: 10_000 });
    const serverFlowTargetText = await serverFlowTargetRef.textContent() ?? "";
    if (!serverFlowTargetText.includes("候选目标")) {
      throw new Error(`Server-flow object ref should preserve semantic role, got: ${serverFlowTargetText}`);
    }
    await serverFlowTargetRef.click();
    await page.waitForFunction(
      (expectedObjectId) => document.querySelector("[data-wire-object-command-tray-state]")?.getAttribute("data-wire-object-command-tray-object") === expectedObjectId,
      "p2-right-1",
      { timeout: 10_000 }
    );
    const serverFlowActionBridge = page.locator('.wire-server-flow [data-server-flow-action-object-id="p2-right-1"][data-server-flow-action-state="ready"] [data-server-flow-action-inspectable="true"]').first();
    await serverFlowActionBridge.waitFor({ timeout: 10_000 });
    const bridgeText = await serverFlowActionBridge.textContent() ?? "";
    if (!bridgeText.includes("候选目标") || !bridgeText.includes("目标") || !bridgeText.includes("可作为") || !bridgeText.includes("目标 1/1")) {
      throw new Error(`Server-flow action bridge should expose semantic role and candidate role, got: ${bridgeText}`);
    }
    const bridgeStepSummary = await serverFlowActionBridge.locator("[data-server-flow-action-step-summary]").first().textContent() ?? "";
    if (!bridgeStepSummary.includes("来源* 0/1") || !bridgeStepSummary.includes("目标 1/1")) {
      throw new Error(`Server-flow action bridge should expose server candidate steps, got: ${bridgeStepSummary}`);
    }
    await serverFlowActionBridge.click();
    await page.waitForFunction(
      (expectedObjectId) => document.querySelector("[data-wire-object-command-tray-state]")?.getAttribute("data-wire-object-command-tray-object") === expectedObjectId,
      "p2-right-1",
      { timeout: 10_000 }
    );

    await page.getByRole("button", { name: "打开流程检查层" }).first().click();
    const serverFlowLayer = page.locator(".wire-server-flow-layer").first();
    await serverFlowLayer.waitFor({ timeout: 10_000 });
    const serverFlowLayerResult = await serverFlowLayer.evaluate((layer) => ({
      authority: layer.querySelector("[data-wire-server-flow-layer-authority]")?.getAttribute("data-wire-server-flow-layer-authority") ?? "",
      flowState: layer.getAttribute("data-wire-server-flow-layer-flow-state") ?? "",
      laneCount: layer.getAttribute("data-wire-server-flow-layer-lane-count") ?? "",
      relatedCount: layer.getAttribute("data-wire-server-flow-layer-related-count") ?? "",
      stepCount: layer.getAttribute("data-wire-server-flow-layer-step-count") ?? "",
      stepRoles: Array.from(layer.querySelectorAll("[data-wire-server-flow-layer-step-role]"))
        .map((item) => item.getAttribute("data-wire-server-flow-layer-step-role") ?? ""),
      text: layer.textContent ?? ""
    }));
    if (
      serverFlowLayerResult.authority !== "server" ||
      Number(serverFlowLayerResult.stepCount) < 1 ||
      Number(serverFlowLayerResult.laneCount) < 1 ||
      Number(serverFlowLayerResult.relatedCount) < 1 ||
      !serverFlowLayerResult.stepRoles.includes("candidate") ||
      !serverFlowLayerResult.text.includes("服务端流程检查层")
    ) {
      throw new Error(`Server-flow layer failed structural checks: ${JSON.stringify(serverFlowLayerResult)}`);
    }
    const serverFlowLayerRef = serverFlowLayer.locator('[data-rule-object-ref="p2-right-1"][data-object-ref-inspectable="true"]').first();
    await serverFlowLayerRef.waitFor({ timeout: 10_000 });
    await serverFlowLayerRef.click();
    await page.waitForFunction(
      (expectedObjectId) => document.querySelector("[data-wire-object-command-tray-state]")?.getAttribute("data-wire-object-command-tray-object") === expectedObjectId,
      "p2-right-1",
      { timeout: 10_000 }
    );
    await page.keyboard.press("Escape");
    await page.waitForFunction(() => !document.querySelector(".wire-server-flow-layer"), { timeout: 10_000 });

    const sourceCard = page.locator('button.card-face[data-object-id="p1-hand-spell"][data-timeline-state="rule"]').first();
    await sourceCard.waitFor({ timeout: 10_000 });
    await page.locator('button.card-face[data-object-id="p2-right-1"][data-timeline-state="rule"]').first().waitFor({ timeout: 10_000 });
    await sourceCard.click();

    const tray = page.locator("[data-wire-object-command-tray-state]").first();
    await tray.waitFor({ timeout: 10_000 });
    const state = await tray.getAttribute("data-wire-object-command-tray-state");
    const objectId = await tray.getAttribute("data-wire-object-command-tray-object");
    const text = await tray.textContent() ?? "";
    if (objectId !== "p1-hand-spell") {
      throw new Error(`Object command tray focused wrong object: ${objectId}`);
    }

    if (!["ready", "selecting"].includes(state ?? "")) {
      throw new Error(`Object command tray should be ready or selecting, got ${state}: ${text}`);
    }

    if (!text.includes("服务端对象上下文") || !text.includes("PLAY_CARD")) {
      throw new Error(`Object command tray is missing server context or command: ${text}`);
    }

    const objectContext = page.locator(".wire-object-context").first();
    const objectContextAuthority = await objectContext.getAttribute("data-wire-object-context-authority");
    const objectContextSource = await objectContext.getAttribute("data-wire-object-context-source");
    const objectContextText = await objectContext.textContent() ?? "";
    if (objectContextAuthority !== "server" || objectContextSource !== "服务端对象上下文") {
      throw new Error(`Object context should expose server authority, got ${objectContextAuthority}/${objectContextSource}: ${objectContextText}`);
    }
    if (!objectContextText.includes("权威：服务端对象上下文") || !objectContextText.includes("步骤：来源* 1/1")) {
      throw new Error(`Object context should expose authority label and selection steps, got: ${objectContextText}`);
    }

    const leaked = hiddenDebugTexts.filter((hiddenText) => text.includes(hiddenText));
    if (leaked.length > 0) {
      throw new Error(`Object command tray leaked hidden debug text: ${leaked.join(", ")}`);
    }

    report.interactions.push({
      name: "object-command-tray",
      objectId,
      relatedCount,
      serverFlowRefObjectId: "p2-right-1",
      serverFlowLayer: serverFlowLayerResult,
      state
    });
    console.log(`QA interaction OK: object-command-tray (${objectId}:${state})`);
  } finally {
    await page.close();
  }
}

async function runReadyReceiptInteraction(report) {
  const joined = await createJoinedRoom();
  try {
    const page = await newPage();
    await openSeededMatch(page, joined, "P1");
    await openSidePanelSlot(page, "commandCenter");
    await assertTexts(page, ["提交反馈", "尚未提交"]);

    await clickReadyQuickAction(page, "submitDeck", "SUBMIT_DECK");
    await waitForAcceptedSubmissionFeedback(page, "SUBMIT_DECK");

    const route = await clickReadyQuickAction(page, "ready", "READY");
    const receipt = await waitForAcceptedSubmissionFeedback(page, "READY");

    report.interactions.push({
      name: "ready-receipt-feedback",
      route,
      commandCenterFollowupState: receipt.commandCenterFollowupState,
      commandCenterServerState: receipt.commandCenterServerState,
      followupState: receipt.followupState,
      state: receipt.state
    });
    console.log(`QA interaction OK: ready-receipt-feedback (${route})`);
    await page.close();
  } finally {
    await joined.close();
  }
}

async function submitReceiptProbeCommand(page) {
  await page.waitForFunction(() => {
    const directSubmitAction = Array.from(document.querySelectorAll("[data-topbar-quick-action-state='ready']:not([disabled])"))
      .some((button) => {
        const candidate = button.getAttribute("data-topbar-quick-action-candidate") ?? "";
        return candidate !== "READY" && candidate !== "SUBMIT_DECK";
      });
    return directSubmitAction || Boolean(document.querySelector("[data-action-object-state='enabled']"));
  }, null, { timeout: 10_000 });

  const readyQuickActions = page.locator('[data-topbar-quick-action-state="ready"]:not([disabled])');
  for (let index = 0; index < await readyQuickActions.count(); index += 1) {
    const quickAction = readyQuickActions.nth(index);
    const candidate = await quickAction.getAttribute("data-topbar-quick-action-candidate") ?? "";
    if (candidate === "READY" || candidate === "SUBMIT_DECK") {
      continue;
    }

    const route = await quickAction.getAttribute("data-topbar-quick-action") ?? "quick-action";
    await quickAction.click();
    return `quick-action:${route}:${candidate}`;
  }

  const actionObject = page.locator('[data-action-object-state="enabled"]').first();
  await actionObject.waitFor({ timeout: 10_000 });
  await actionObject.click();
  const submit = page.locator('.wire-command-review-submit[data-command-review-submit-state="ready"]:not([disabled])').first();
  try {
    await submit.waitFor({ timeout: 10_000 });
  } catch (error) {
    throw new Error(`No ready command review submit after selecting enabled action object: ${JSON.stringify(await commandProbeDebug(page))}`, { cause: error });
  }
  await submit.click();
  return "action-map-route";
}

async function clickReadyQuickAction(page, actionId, expectedCandidate) {
  const action = page.locator(`[data-topbar-quick-action="${actionId}"][data-topbar-quick-action-state="ready"]:not([disabled])`).first();
  try {
    await action.waitFor({ timeout: 10_000 });
  } catch (error) {
    throw new Error(`No ready ${actionId} quick action: ${JSON.stringify(await commandProbeDebug(page))}`, { cause: error });
  }

  const candidate = await action.getAttribute("data-topbar-quick-action-candidate") ?? "";
  if (candidate !== expectedCandidate) {
    throw new Error(`Expected ${actionId} quick action candidate ${expectedCandidate}, got ${candidate}`);
  }

  await action.click();
  return `quick-action:${actionId}:${candidate}`;
}

async function assertRailAction(page, rail, expectedSlot) {
  const action = page.locator(`[data-wire-side-panel-rail="${rail}"][data-wire-side-panel-rail-mode="summary"] [data-wire-side-panel-rail-action="${rail}"]`).first();
  await action.waitFor({ timeout: 10_000 });
  await action.click();
  await page.locator(`[data-wire-side-panel-directory][data-wire-side-panel-directory-active-slot="${expectedSlot}"]`).waitFor({ timeout: 5_000 });
}

async function waitForAcceptedSubmissionFeedback(page, cmdType) {
  await page.waitForFunction((expectedCmdType) => {
    const feedback = document.querySelector("[data-command-submission-state]");
    const followupState = feedback?.querySelector("[data-command-followup-state]")?.getAttribute("data-command-followup-state") ?? "";
    const commandCenter = document.querySelector(".wire-command-center");
    const commandCenterFollowup = commandCenter?.querySelector("[data-command-followup-state]");
    const commandCenterFollowupState = commandCenterFollowup?.getAttribute("data-command-followup-state") ?? "";
    const commandCenterServerState = commandCenterFollowup?.getAttribute("data-command-followup-server-state") ?? "";
    const text = feedback?.textContent ?? "";
    const acceptedFollowupStates = ["accepted-events", "accepted-silent", "accepted-snapshot"];
    const acceptedServerStates = ["events", "silent", "snapshot-prompt"];
    return feedback?.getAttribute("data-command-submission-state") === "sent"
      && text.includes("服务端已接受")
      && text.includes("ACCEPTED")
      && text.includes(expectedCmdType)
      && acceptedFollowupStates.includes(followupState)
      && acceptedFollowupStates.includes(commandCenterFollowupState)
      && acceptedServerStates.includes(commandCenterServerState);
  }, cmdType, { timeout: 10_000 });
  const receipt = await page.locator("[data-command-submission-state]").first().evaluate((node) => {
    const commandCenter = document.querySelector(".wire-command-center");
    const commandCenterFollowup = commandCenter?.querySelector("[data-command-followup-state]");
    return {
      commandCenterFollowupState: commandCenterFollowup?.getAttribute("data-command-followup-state") ?? "",
      commandCenterServerState: commandCenterFollowup?.getAttribute("data-command-followup-server-state") ?? "",
      commandCenterText: commandCenter?.textContent ?? "",
      followupState: node.querySelector("[data-command-followup-state]")?.getAttribute("data-command-followup-state") ?? "",
      state: node.getAttribute("data-command-submission-state"),
      text: node.textContent ?? ""
    };
  });
  if (hiddenDebugTexts.some((text) => receipt.text.includes(text))) {
    throw new Error(`Command receipt feedback leaked hidden debug text: ${receipt.text}`);
  }
  if (hiddenDebugTexts.some((text) => receipt.commandCenterText.includes(text))) {
    throw new Error(`Command center followup leaked hidden debug text: ${receipt.commandCenterText}`);
  }

  return receipt;
}

async function openCommandReceiptLayer(page, cmdType) {
  const openLayerButton = page.getByRole("button", { name: "打开回执检查层" }).first();
  const openLayerState = await openLayerButton.getAttribute("data-command-submission-open-layer-state");
  if (openLayerState !== "sent") {
    throw new Error(`Command receipt layer entry should be sent after accepted command, got ${openLayerState}`);
  }

  await openLayerButton.click();
  await page.waitForFunction(() => Boolean(document.querySelector(".wire-command-submission-layer")), { timeout: 5_000 });
  const layerResult = await page.locator(".wire-command-submission-layer").first().evaluate((layer) => ({
    activeText: document.activeElement?.textContent ?? "",
    authority: layer.querySelector("[data-command-submission-layer-authority]")?.getAttribute("data-command-submission-layer-authority") ?? "",
    cmdType: layer.getAttribute("data-command-submission-layer-cmd-type") ?? "",
    followupState: layer.getAttribute("data-command-submission-layer-followup-state") ?? "",
    hiddenCount: layer.querySelector("[data-command-submission-layer-hidden-count]")?.getAttribute("data-command-submission-layer-hidden-count") ?? "",
    modal: layer.getAttribute("aria-modal") ?? "",
    receiptState: layer.getAttribute("data-command-submission-layer-receipt-state") ?? "",
    role: layer.getAttribute("role") ?? "",
    sections: Array.from(layer.querySelectorAll("[data-command-submission-layer-section]"))
      .map((item) => item.getAttribute("data-command-submission-layer-section") ?? ""),
    serverState: layer.getAttribute("data-command-submission-layer-server-state") ?? "",
    state: layer.getAttribute("data-command-submission-layer-state") ?? "",
    text: layer.textContent ?? "",
    title: layer.querySelector("#wire-command-submission-layer-title")?.textContent ?? ""
  }));

  const failures = [];
  if (layerResult.role !== "dialog") failures.push(`role=${layerResult.role}`);
  if (layerResult.modal !== "true") failures.push("modal missing");
  if (layerResult.state !== "open") failures.push(`state=${layerResult.state}`);
  if (layerResult.cmdType !== cmdType) failures.push(`cmdType=${layerResult.cmdType}`);
  if (layerResult.title !== cmdType) failures.push(`title=${layerResult.title}`);
  if (layerResult.receiptState !== "ACCEPTED") failures.push(`receipt=${layerResult.receiptState}`);
  if (!["accepted-events", "accepted-silent", "accepted-snapshot"].includes(layerResult.followupState)) failures.push(`followup=${layerResult.followupState}`);
  if (!["events", "silent", "snapshot-prompt"].includes(layerResult.serverState)) failures.push(`server=${layerResult.serverState}`);
  if (layerResult.authority !== "server") failures.push(`authority=${layerResult.authority}`);
  if (!layerResult.activeText.includes("关闭")) failures.push("close button not focused");
  for (const section of ["receipt", "identity", "authority"]) {
    if (!layerResult.sections.includes(section)) failures.push(`section missing: ${section}`);
  }
  if (!layerResult.text.includes("回执检查层")) failures.push("heading missing");
  if (!layerResult.text.includes("服务端回执")) failures.push("receipt section missing");
  if (!layerResult.text.includes("后续事件、快照和提示均以服务端广播为准")) failures.push("authority copy missing");
  if (!layerResult.text.includes("后续事件")) failures.push("followup panel missing");
  if (hiddenDebugTexts.some((text) => layerResult.text.includes(text))) failures.push("hidden debug text leaked");
  if (failures.length > 0) {
    throw new Error(`Command receipt layer check failed: ${failures.join("; ")}\n${layerResult.text}`);
  }

  await page.keyboard.press("Escape");
  await page.waitForFunction(() => !document.querySelector(".wire-command-submission-layer"), { timeout: 5_000 });
  return layerResult;
}

async function commandProbeDebug(page) {
  return await page.evaluate(() => ({
    actionObjects: Array.from(document.querySelectorAll("[data-action-object-id]")).map((node) => ({
      id: node.getAttribute("data-action-object-id"),
      selected: node.getAttribute("data-selected"),
      state: node.getAttribute("data-action-object-state"),
      text: node.textContent?.trim()
    })),
    commandReview: document.querySelector("[data-command-review-state]")?.textContent?.trim() ?? "",
    commandReviewState: document.querySelector("[data-command-review-state]")?.getAttribute("data-command-review-state") ?? null,
    quickActions: Array.from(document.querySelectorAll("[data-topbar-quick-action]")).map((node) => ({
      candidate: node.getAttribute("data-topbar-quick-action-candidate"),
      disabled: node.hasAttribute("disabled"),
      id: node.getAttribute("data-topbar-quick-action"),
      state: node.getAttribute("data-topbar-quick-action-state"),
      text: node.textContent?.trim()
    }))
  }));
}

async function createJoinedRoom() {
  const roomId = `qa-ready-${Date.now()}-${Math.random().toString(16).slice(2)}`;
  const clients = {
    P1: createSignalRClient("P1", roomId),
    P2: createSignalRClient("P2", roomId)
  };
  await Promise.all([clients.P1.connection.start(), clients.P2.connection.start()]);
  await invokeHub(clients.P1, "JoinRoom", roomId, "P1", null);
  await invokeHub(clients.P2, "JoinRoom", roomId, "P2", null);
  await waitFor(() => Boolean(clients.P1.state.joined && clients.P2.state.joined && clients.P1.state.prompt), `joined room ${roomId}`);

  return {
    roomId,
    sessions: {
      P1: clients.P1.state.joined,
      P2: clients.P2.state.joined
    },
    close: async () => {
      await Promise.all([clients.P1.connection.stop(), clients.P2.connection.stop()]);
    }
  };
}

async function createSeededRoom(scenario) {
  const roomId = `qa-${scenario}-${Date.now()}-${Math.random().toString(16).slice(2)}`;
  const clients = {
    P1: createSignalRClient("P1", roomId),
    P2: createSignalRClient("P2", roomId)
  };
  await Promise.all([clients.P1.connection.start(), clients.P2.connection.start()]);
  await invokeHub(clients.P1, "JoinRoom", roomId, "P1", null);
  await invokeHub(clients.P2, "JoinRoom", roomId, "P2", null);
  await invokeHub(clients.P1, "SeedScenario", roomId, "P1", scenario, `qa-visual-${scenario}`);
  await waitFor(() => Boolean(clients.P1.state.snapshot && clients.P1.state.prompt), `seeded snapshot for ${scenario}`);

  return {
    roomId,
    sessions: {
      P1: clients.P1.state.joined,
      P2: clients.P2.state.joined
    },
    close: async () => {
      await Promise.all([clients.P1.connection.stop(), clients.P2.connection.stop()]);
    }
  };
}

function createSignalRClient(playerId, roomId) {
  const state = {
    events: [],
    errors: [],
    joined: undefined,
    prompt: undefined,
    snapshot: undefined
  };
  const connection = new signalR.HubConnectionBuilder()
    .withUrl(`${serverUrl}/hubs/game`)
    .build();

  connection.on("Joined", (message) => {
    state.joined = message.payload;
  });
  connection.on("Snapshot", (message) => {
    state.snapshot = message.payload;
  });
  connection.on("Prompt", (message) => {
    state.prompt = message.payload;
  });
  connection.on("Events", (message) => {
    state.events.push(...message.payload);
  });
  connection.on("Error", (message) => {
    state.errors.push(message.payload);
  });

  return { playerId, roomId, connection, state };
}

async function invokeHub(client, method, ...args) {
  const errorStart = client.state.errors.length;
  await client.connection.invoke(method, ...args);
  await delay(150);
  if (client.state.errors.length > errorStart) {
    throw new Error(`${client.playerId} hub error: ${JSON.stringify(client.state.errors.slice(errorStart))}`);
  }
}

async function ensureApi() {
  if (await isHttpOk(`${serverUrl}/health`)) {
    console.log(`API already available at ${serverUrl}`);
    return;
  }

  const api = spawnChild("dotnet", ["run", "--project", "src/Riftbound.Api/Riftbound.Api.csproj", "--no-launch-profile"], {
    cwd: repoRoot,
    env: {
      ...process.env,
      ASPNETCORE_ENVIRONMENT: "Development",
      ASPNETCORE_URLS: serverUrl,
      ConnectionStrings__Riftbound: process.env.RIFTBOUND_QA_CONNECTION_STRING ?? ""
    },
    name: "api"
  });
  children.push(api);
  await waitForHttp(`${serverUrl}/health`, 60_000);
}

async function ensurePreview() {
  const preview = spawnChild(viteBin(), ["preview", "--host", "127.0.0.1", "--port", String(frontendPort), "--strictPort"], {
    cwd: appRoot,
    name: "vite-preview"
  });
  children.push(preview);
  await waitForHttp(`${frontendUrl}/`, 30_000);
}

function spawnChild(command, args, options) {
  const child = spawn(command, args, {
    cwd: options.cwd,
    env: options.env ?? process.env,
    stdio: ["ignore", "pipe", "pipe"]
  });
  child.stdout.on("data", (chunk) => process.stdout.write(`[${options.name}] ${chunk}`));
  child.stderr.on("data", (chunk) => process.stderr.write(`[${options.name}] ${chunk}`));
  return child;
}

async function stopChild(child) {
  if (child.exitCode !== null || child.signalCode !== null) {
    return;
  }
  child.kill("SIGTERM");
  if (await waitForChildExit(child, 3_000)) {
    return;
  }
  child.kill("SIGKILL");
  await waitForChildExit(child, 2_000);
}

function waitForChildExit(child, timeoutMs) {
  return new Promise((resolve) => {
    if (child.exitCode !== null || child.signalCode !== null) {
      resolve(true);
      return;
    }
    const timeout = setTimeout(() => {
      child.off("exit", onExit);
      resolve(false);
    }, timeoutMs);
    const onExit = () => {
      clearTimeout(timeout);
      resolve(true);
    };
    child.once("exit", onExit);
  });
}

async function waitForHttp(url, timeoutMs) {
  await waitFor(() => isHttpOk(url), url, timeoutMs);
}

async function waitFor(predicate, label, timeoutMs = 15_000) {
  const deadline = Date.now() + timeoutMs;
  while (Date.now() < deadline) {
    if (await predicate()) {
      return;
    }
    await delay(250);
  }
  throw new Error(`Timed out waiting for ${label}`);
}

async function isHttpOk(url) {
  try {
    const response = await fetch(url);
    return response.ok;
  } catch {
    return false;
  }
}

function viteBin() {
  const suffix = process.platform === "win32" ? ".cmd" : "";
  return path.join(appRoot, "node_modules", ".bin", `vite${suffix}`);
}

function chromePath() {
  const candidates = [
    process.env.CHROME_PATH,
    "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome",
    "/Applications/Chromium.app/Contents/MacOS/Chromium"
  ].filter(Boolean);
  const found = candidates.find((candidate) => existsSync(candidate));
  if (!found) {
    throw new Error("Google Chrome was not found. Set CHROME_PATH to run Playwright QA.");
  }
  return found;
}

function isIgnorableConsoleError(text) {
  return text.includes("Failed to complete negotiation with the server")
    || text.includes("Failed to start the connection")
    || text.includes("net::ERR_CONNECTION_REFUSED")
    || text.includes("Failed to load resource: the server responded with a status of 404");
}

function delay(ms) {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

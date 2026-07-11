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
const qaShotFilter = process.env.RIFTBOUND_QA_SHOT?.trim();
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

const qaPlayerKeys = {
  P1: "pk_formal_18_player_one_0000000000000001",
  P2: "pk_formal_18_player_two_0000000000000002"
};

const qaRunToken = `${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 8)}`;
const qaRoomId = `qa-room-${qaRunToken}`;
const qaResultRoomId = `qa-result-${qaRunToken}`;

const staticShots = [
  { name: "lobby-root", path: "/", texts: ["开始一场对局", "快速匹配", "创建私人房间"] },
  { name: "lobby-mobile", path: "/", texts: ["开始一场对局", "快速匹配", "创建私人房间"], viewport: { width: 390, height: 844 } },
  { name: "cards", path: "/cards", texts: ["卡牌图鉴", "官方卡牌视图"] },
  { name: "decks", path: "/decks", texts: ["构筑导入工作台", "导入到服务端提交的交接", "导入入口", "等待服务端验证", "服务端权威"], allowedDebugTexts: ["mainDeck", "runeDeck"] },
  { name: "room", path: `/rooms/${qaRoomId}`, texts: ["对战房间", "开局准备", "服务端连接", "卡组提交", "连接与诊断"] },
  { name: "result", path: `/matches/${qaResultRoomId}/result`, texts: ["结算", "最终状态", "事件 / 错误", "结果只读取服务端权威快照"] }
];

const scenarioShots = [
  {
    name: "match-midgame-showcase",
    scenario: "midgame-showcase",
    playerId: "P1",
    texts: ["公共战场", "你的手牌", "对手手牌", "连接与规则诊断"],
    viewport: { width: 1440, height: 900 }
  },
  {
    name: "match-wide-playable",
    scenario: "midgame-showcase",
    playerId: "P1",
    texts: ["公共战场", "你的手牌", "对手手牌", "连接与规则诊断"],
    viewport: { width: 1920, height: 1080 }
  },
  {
    name: "match-compact-playable",
    scenario: "midgame-showcase",
    playerId: "P1",
    texts: ["公共战场", "你的手牌", "对手手牌", "连接与规则诊断"],
    viewport: { width: 1280, height: 720 }
  },
  {
    name: "match-mobile-playable",
    scenario: "midgame-showcase",
    playerId: "P1",
    texts: ["公共战场", "你的手牌", "对手手牌", "连接与规则诊断"],
    viewport: { width: 390, height: 844 }
  },
  {
    name: "prompt-pay-cost",
    scenario: "pay-cost-window",
    playerId: "P1",
    texts: ["公共战场", "你的手牌", "连接与规则诊断"],
    viewport: { width: 1440, height: 900 }
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
let qaPageSequence = 0;

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

  for (const shot of staticShots.filter((entry) => !qaShotFilter || entry.name === qaShotFilter)) {
    const page = await newPage(shot.viewport);
    await page.goto(`${frontendUrl}${shot.path}`, { waitUntil: "networkidle" });
    await assertTexts(page, shot.texts);
    if (shot.name === "decks") {
      await assertDeckImportSurface(page);
    }
    if (shot.name === "room") {
      await assertRoomWorkflowSurface(page);
    }
    if (shot.name === "result") {
      await assertResultSurface(page);
    }
    if (shot.name === "lobby-mobile") {
      await assertMobileLobbySurface(page);
    }
    await captureAndAudit(page, shot, report);
    await page.close();
  }

  for (const shot of scenarioShots.filter((entry) => !qaShotFilter || entry.name === qaShotFilter)) {
    const seeded = await createSeededRoom(shot.scenario);
    try {
      const page = await newPage(shot.viewport);
      await openSeededMatch(page, seeded, shot.playerId);
      await assertTexts(page, shot.texts);
      await assertMatchStateSurface(page, shot);
      await captureAndAudit(page, shot, report);
      if (shot.name === "match-midgame-showcase") {
        await runPlayableCardInspectInteraction(page, report);
        await runArenaDirectSelectionInteraction(page, report);
      }
      await page.close();
    } finally {
      await seeded.close();
    }
  }

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

async function newPage(viewport = { width: 1440, height: 810 }) {
  qaPageSequence += 1;
  const playerId = `qa${qaRunToken.replace(/[^a-z0-9]/gi, "").slice(-10)}${qaPageSequence}`;
  const playerKey = `pk_playwright_visual_${qaRunToken}_${qaPageSequence}_0000000000000000`;
  const context = await browser.newContext({
    deviceScaleFactor: 1,
    viewport
  });
  await context.addInitScript(({ server, storedPlayerId, storedPlayerKey }) => {
    localStorage.setItem("riftbound.serverUrl", server);
    localStorage.setItem("riftbound.animationLevel", "off");
    localStorage.setItem("riftbound.logDensity", "detailed");
    if (!localStorage.getItem("riftbound.playerId")) {
      localStorage.setItem("riftbound.playerId", storedPlayerId);
    }
    if (!localStorage.getItem("riftbound.playerKey")) {
      localStorage.setItem("riftbound.playerKey", storedPlayerKey);
    }
  }, { server: serverUrl, storedPlayerId: playerId, storedPlayerKey: playerKey });

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

async function assertMobileLobbySurface(page) {
  const surface = await page.evaluate(() => {
    const nav = document.querySelector(".main-nav");
    const content = document.querySelector(".app-content");
    const brand = document.querySelector(".brand-mark");
    const primaryButtons = Array.from(document.querySelectorAll(".game-primary-nav button"));
    const navRect = nav?.getBoundingClientRect();
    const contentRect = content?.getBoundingClientRect();
    return {
      brandVisible: brand ? getComputedStyle(brand).display !== "none" : false,
      contentRect: contentRect ? { left: contentRect.left, right: contentRect.right } : null,
      navPosition: nav ? getComputedStyle(nav).position : "missing",
      navRect: navRect ? { bottom: navRect.bottom, height: navRect.height, left: navRect.left, right: navRect.right, top: navRect.top } : null,
      navLabels: primaryButtons.map((button) => ({
        label: button.textContent?.trim() ?? "",
        visible: button.getBoundingClientRect().width > 0 && button.getBoundingClientRect().height > 0
      })),
      scrollWidth: document.documentElement.scrollWidth,
      viewportHeight: window.innerHeight,
      viewportWidth: window.innerWidth
    };
  });

  const failures = [];
  if (surface.navPosition !== "fixed" || !surface.navRect || surface.navRect.left !== 0 || surface.navRect.right !== surface.viewportWidth || surface.navRect.bottom !== surface.viewportHeight) {
    failures.push(`mobile navigation must be a full-width bottom bar: ${JSON.stringify(surface)}`);
  }
  if (surface.brandVisible) {
    failures.push("desktop brand block must not consume mobile navigation space.");
  }
  if (!surface.contentRect || surface.contentRect.left !== 0 || surface.contentRect.right !== surface.viewportWidth) {
    failures.push(`mobile content must use the full viewport width: ${JSON.stringify(surface.contentRect)}`);
  }
  if (surface.navLabels.length !== 4 || surface.navLabels.some((entry) => !entry.visible || !entry.label)) {
    failures.push(`mobile navigation must expose four labeled destinations: ${JSON.stringify(surface.navLabels)}`);
  }
  if (surface.scrollWidth > surface.viewportWidth) {
    failures.push(`mobile lobby must not scroll horizontally: ${surface.scrollWidth}/${surface.viewportWidth}`);
  }
  if (failures.length > 0) {
    throw new Error(`Mobile lobby surface assertions failed:\n${failures.join("\n")}`);
  }
}

async function captureAndAudit(page, shot, report) {
  await hideDynamicText(page);
  await waitForCardImages(page);
  await expectNoHiddenDebugText(page, shot.allowedDebugTexts ?? []);
  const screenshotPath = path.join(appshotDir, `${shot.name}.png`);
  const buffer = await page.screenshot({ fullPage: false, path: screenshotPath });
  assertNonBlank(buffer, shot.name);
  assertPlayableVisual(buffer, shot.name);
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
  failures.push(...await assertInvalidDeckImportFlow(page, surface.commandText));
  if (failures.length > 0) {
    throw new Error(`Deck import surface assertions failed:\n${failures.join("\n")}`);
  }
}

async function assertInvalidDeckImportFlow(page, previousCommandText) {
  const input = page.locator("[data-deck-import-input]").first();
  const originalText = await input.inputValue();
  const invalidDeckText = [
    "legend:",
    "champion:",
    "main:",
    "1 NOT-A-CARD"
  ].join("\n");
  const failures = [];

  try {
    await input.fill(invalidDeckText);
    await page.locator('[data-deck-import-surface][data-deck-import-state="invalid"]').waitFor({ timeout: 5_000 });

    const invalidSurface = await page.evaluate(() => {
      const textOf = (node) => node?.textContent?.trim().replace(/\s+/g, " ") ?? "";
      const apply = document.querySelector('[data-deck-import-action="apply"]');
      const commandPreview = document.querySelector("[data-deck-import-command-preview]");
      const feedback = document.querySelector("[data-deck-import-feedback]");
      const flowState = document.querySelector("[data-deck-import-flow-state]");
      const handoff = document.querySelector("[data-deck-import-handoff]");
      const input = document.querySelector("[data-deck-import-input]");
      const root = document.querySelector("[data-deck-import-surface]");
      return {
        applyDisabled: apply?.hasAttribute("disabled") ?? false,
        applyState: apply?.getAttribute("data-deck-import-action-state") ?? "",
        commandText: textOf(commandPreview),
        feedbackState: feedback?.getAttribute("data-deck-import-state") ?? "",
        feedbackText: textOf(feedback),
        flowState: flowState?.getAttribute("data-deck-import-flow-state") ?? "",
        handoffActiveSection: handoff?.getAttribute("data-deck-import-handoff-active-section") ?? "",
        handoffSummary: handoff?.getAttribute("data-deck-import-handoff-summary") ?? "",
        inputState: input?.getAttribute("data-deck-import-state") ?? "",
        issueFields: Array.from(document.querySelectorAll("[data-deck-import-issue-field]")).map((node) =>
          node.getAttribute("data-deck-import-issue-field") ?? ""
        ),
        rootState: root?.getAttribute("data-deck-import-state") ?? ""
      };
    });

    if (
      invalidSurface.rootState !== "invalid"
      || invalidSurface.inputState !== "invalid"
      || invalidSurface.feedbackState !== "invalid"
      || invalidSurface.flowState !== "invalid"
    ) {
      failures.push(`invalid deck import must project invalid state across root/input/feedback/flow: ${JSON.stringify(invalidSurface)}`);
    }
    if (invalidSurface.applyState !== "blocked" || invalidSurface.applyDisabled !== true) {
      failures.push(`invalid deck import must block apply without entering server submission: ${JSON.stringify(invalidSurface)}`);
    }
    if (invalidSurface.handoffActiveSection !== "intake" || !invalidSurface.handoffSummary.includes("结构无效")) {
      failures.push(`invalid deck import handoff must route back to intake: ${JSON.stringify(invalidSurface)}`);
    }
    if (!invalidSurface.feedbackText.includes("导入未应用") || !invalidSurface.feedbackText.includes("服务端权威")) {
      failures.push(`invalid deck import feedback must explain the local block and server authority: ${invalidSurface.feedbackText}`);
    }
    if (invalidSurface.issueFields.length < 1) {
      failures.push(`invalid deck import must expose issue fields for correction: ${JSON.stringify(invalidSurface.issueFields)}`);
    }
    if (invalidSurface.commandText !== previousCommandText || invalidSurface.commandText.includes("NOT-A-CARD")) {
      failures.push(`invalid deck import should keep the previous SUBMIT_DECK payload: ${JSON.stringify({
        after: invalidSurface.commandText,
        before: previousCommandText
      })}`);
    }
  } finally {
    await input.fill(originalText);
    await page.locator('[data-deck-import-surface][data-deck-import-state="valid"]').waitFor({ timeout: 5_000 });
  }

  return failures;
}

async function assertRoomWorkflowSurface(page) {
  await assertConnectionRecoveryPanelSurface(page, "room");

  const surface = await page.evaluate(() => {
    const textOf = (node) => node?.textContent?.trim().replace(/\s+/g, " ") ?? "";
    const workflow = document.querySelector("[data-room-workflow-surface]");
    const errors = document.querySelector("[data-room-errors-region]");
    const submission = document.querySelector("[data-room-submission-region]");
    return {
      activeRegion: workflow?.getAttribute("data-room-workflow-active-region") ?? "",
      errorActions: Array.from(document.querySelectorAll("[data-error-resolution-action]")).map((node) => ({
        disabled: node.hasAttribute("disabled"),
        disabledAttr: node.getAttribute("data-error-resolution-action-disabled") ?? "",
        id: node.getAttribute("data-error-resolution-action") ?? "",
        state: node.getAttribute("data-error-resolution-action-state") ?? "",
        text: textOf(node),
        title: node.getAttribute("title") ?? ""
      })),
      errorEvidenceRows: Array.from(document.querySelectorAll("[data-error-resolution-evidence-row]")).map((node) => ({
        id: node.getAttribute("data-error-resolution-evidence-row") ?? "",
        label: node.getAttribute("data-error-resolution-evidence-label") ?? "",
        text: textOf(node),
        value: node.getAttribute("data-error-resolution-evidence-value") ?? ""
      })),
      errorNextStep: textOf(document.querySelector("[data-error-resolution-next-step]")),
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
  if (surface.activeRegion !== "actions") {
    failures.push(`room workflow should expose the first available server action after automatic connection, got ${surface.activeRegion}`);
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
  const submitDeckAction = actionsById.submitDeck;
  if (submitDeckAction?.state !== "ready" || submitDeckAction?.commandSource !== "direct-action" || submitDeckAction?.disabled !== false) {
    failures.push(`room submitDeck action must open the deck import flow after automatic connection: ${JSON.stringify(submitDeckAction)}`);
  }
  const readyAction = actionsById.ready;
  if (readyAction?.state !== "missing" || readyAction?.commandSource !== "unavailable" || readyAction?.disabled !== true) {
    failures.push(`room ready action must remain unavailable until the server provides READY: ${JSON.stringify(readyAction)}`);
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
  const errorActionsById = Object.fromEntries(surface.errorActions.map((action) => [action.id, action]));
  if (surface.errorActions.length !== 5) {
    failures.push(`room error resolution must expose all recovery actions: ${JSON.stringify(surface.errorActions)}`);
  }
  for (const actionId of ["connect", "resync", "openDecks", "reviewPrompt", "waitServer"]) {
    const action = errorActionsById[actionId];
    if (!action?.state || !action.text || !action.title || !["true", "false"].includes(action.disabledAttr)) {
      failures.push(`room error action ${actionId} must expose state, disabled flag, title and label: ${JSON.stringify(action)}`);
    }
    if (action && action.disabled !== (action.disabledAttr === "true")) {
      failures.push(`room error action ${actionId} disabled DOM state must match data attribute: ${JSON.stringify(action)}`);
    }
  }
  if (errorActionsById.connect?.state !== "disabled" || errorActionsById.connect?.disabled !== true) {
    failures.push(`room error connect action should be disabled after automatic connection: ${JSON.stringify(errorActionsById.connect)}`);
  }
  if (errorActionsById.reviewPrompt?.state !== "secondary" || errorActionsById.reviewPrompt?.disabled !== false) {
    failures.push(`room error review prompt action should remain available in clear state: ${JSON.stringify(errorActionsById.reviewPrompt)}`);
  }
  if (errorActionsById.resync?.state !== "secondary" || errorActionsById.resync?.disabled !== false) {
    failures.push(`room error resync action should be available after automatic connection: ${JSON.stringify(errorActionsById.resync)}`);
  }
  for (const disabledActionId of ["openDecks", "waitServer"]) {
    if (errorActionsById[disabledActionId]?.state !== "disabled" || errorActionsById[disabledActionId]?.disabled !== true) {
      failures.push(`room error action ${disabledActionId} should start disabled without a server issue: ${JSON.stringify(errorActionsById[disabledActionId])}`);
    }
  }
  const evidenceByLabel = Object.fromEntries(surface.errorEvidenceRows.map((row) => [row.label, row]));
  for (const label of ["连接状态", "错误来源"]) {
    const row = evidenceByLabel[label];
    if (!row?.id || !row.value || !row.text.includes(label) || !row.text.includes(row.value)) {
      failures.push(`room error evidence row ${label} must expose label and value: ${JSON.stringify(row)}`);
    }
  }
  if (!surface.errorNextStep.includes("继续按服务端提示")) {
    failures.push(`room error next step must expose server-authority recovery guidance: ${surface.errorNextStep}`);
  }
  for (const copy of ["服务端连接", "卡组提交", "提交回执", "错误处理", "下一步", "连接状态", "错误来源", "服务端消息"]) {
    if (!surface.text.includes(copy)) {
      failures.push(`room workflow page missing ${copy} copy: ${surface.text}`);
    }
  }
  if (failures.length > 0) {
    throw new Error(`Room workflow surface assertions failed:\n${failures.join("\n")}`);
  }
}

async function assertResultSurface(page) {
  const surface = await page.evaluate(() => {
    const textOf = (node) => node?.textContent?.trim().replace(/\s+/g, " ") ?? "";
    const root = document.querySelector("[data-result-surface]");
    const finalState = document.querySelector("[data-result-final-state]");
    const eventSummary = document.querySelector("[data-result-event-summary]");
    const errorSummary = document.querySelector("[data-result-error-summary]");
    const returnPath = document.querySelector("[data-result-return-path]");
    return {
      actionCount: Number(returnPath?.getAttribute("data-result-return-action-count") ?? -1),
      actions: Array.from(document.querySelectorAll("[data-result-action]")).map((node) => ({
        disabled: node.hasAttribute("disabled"),
        id: node.getAttribute("data-result-action") ?? "",
        route: node.getAttribute("data-result-action-route") ?? "",
        state: node.getAttribute("data-result-action-state") ?? "",
        text: textOf(node)
      })),
      authority: root?.getAttribute("data-result-authority") ?? "",
      errorCount: Number(errorSummary?.getAttribute("data-result-error-count") ?? -1),
      eventCount: Number(eventSummary?.getAttribute("data-result-event-count") ?? -1),
      finalAuthority: finalState?.getAttribute("data-result-authority") ?? "",
      finalHasSnapshot: finalState?.getAttribute("data-result-has-snapshot") ?? "",
      finalLabel: finalState?.getAttribute("data-result-final-label") ?? "",
      finalState: finalState?.getAttribute("data-result-final-state") ?? "",
      hasSnapshot: root?.getAttribute("data-result-has-snapshot") ?? "",
      logEntries: Array.from(document.querySelectorAll("[data-result-log-entry]")).map((node) => ({
        kind: node.getAttribute("data-result-log-kind") ?? "",
        type: node.getAttribute("data-result-log-entry") ?? "",
        text: textOf(node)
      })),
      matchId: root?.getAttribute("data-result-match-id") ?? "",
      playerId: root?.getAttribute("data-result-player-id") ?? "",
      players: Array.from(document.querySelectorAll("[data-result-player-score]")).map((node) => ({
        id: node.getAttribute("data-result-player-id") ?? "",
        score: node.getAttribute("data-result-player-score") ?? "",
        text: textOf(node),
        winner: node.getAttribute("data-result-player-winner") ?? ""
      })),
      returnText: textOf(returnPath),
      roomStatus: root?.getAttribute("data-result-room-status") ?? "",
      snapshotTick: root?.getAttribute("data-result-snapshot-tick") ?? "",
      state: root?.getAttribute("data-result-state") ?? "",
      text: textOf(root),
      winnerPlayerId: root?.getAttribute("data-result-winner-player-id") ?? ""
    };
  });

  const failures = [];
  if (surface.authority !== "server-snapshot" || surface.finalAuthority !== "server-snapshot") {
    failures.push(`result surface must declare server snapshot authority: ${JSON.stringify(surface)}`);
  }
  if (!surface.matchId || !surface.playerId || surface.snapshotTick === "" || !surface.state || !surface.finalState) {
    failures.push(`result surface missing match/player/tick/state contract: ${JSON.stringify(surface)}`);
  }
  if (!["true", "false"].includes(surface.hasSnapshot) || surface.finalHasSnapshot !== surface.hasSnapshot) {
    failures.push(`result surface must expose consistent snapshot presence: ${JSON.stringify(surface)}`);
  }
  if (!["finished", "in-progress", "waiting-final", "waiting-snapshot"].includes(surface.state)) {
    failures.push(`result surface state must be a known result state: ${surface.state}`);
  }
  if (surface.actionCount !== 5 || surface.actions.length !== 5) {
    failures.push(`result surface must expose five return/recovery actions: ${JSON.stringify(surface.actions)}`);
  }
  const actionsById = Object.fromEntries(surface.actions.map((action) => [action.id, action]));
  for (const [actionId, route] of Object.entries({ connect: "connection", lobby: "lobby", match: "match", resync: "snapshot", room: "room" })) {
    const action = actionsById[actionId];
    if (action?.route !== route || !action.state || !action.text || action.disabled) {
      failures.push(`result action ${actionId} must expose route/state/text and remain available: ${JSON.stringify(action)}`);
    }
  }
  if (!Number.isFinite(surface.eventCount) || !Number.isFinite(surface.errorCount) || surface.eventCount < 0 || surface.errorCount < 0) {
    failures.push(`result event/error summaries must expose numeric counts: ${JSON.stringify({
      errorCount: surface.errorCount,
      eventCount: surface.eventCount
    })}`);
  }
  if (surface.logEntries.some((entry) => !entry.type || !entry.kind || !entry.text)) {
    failures.push(`result log entries must expose type/kind/text when present: ${JSON.stringify(surface.logEntries)}`);
  }
  if (surface.players.some((player) => !player.id || player.score === "" || !["true", "false"].includes(player.winner))) {
    failures.push(`result player scores must expose id, score, and winner flag when present: ${JSON.stringify(surface.players)}`);
  }
  for (const copy of ["服务端权威", "结果只读取服务端权威快照", "连接状态", "返回房间", "查看对战桌面", "事件 / 错误"]) {
    if (!surface.text.includes(copy) && !surface.returnText.includes(copy)) {
      failures.push(`result surface missing ${copy} copy: ${JSON.stringify(surface)}`);
    }
  }
  if (failures.length > 0) {
    throw new Error(`Result surface assertions failed:\n${failures.join("\n")}`);
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
  const surface = await page.evaluate(() => {
    const root = document.querySelector("[data-playable-match-surface]");
    const table = document.querySelector("[data-game-table]");
    const arena = document.querySelector("[data-arena-table]");
    const battlefield = document.querySelector("[data-arena-battlefield-region]");
    const opponentHand = document.querySelector(".arena-hand.is-opponent");
    const selfHand = document.querySelector(".arena-hand.is-self[data-arena-hand]");
    const opponentHandCards = opponentHand?.querySelector(".wire-card-flow-hand");
    const selfHandCards = selfHand?.querySelector(".wire-card-flow-hand");
    const actionLayer = document.querySelector("[data-arena-action-mode]");
    const tableRect = table?.getBoundingClientRect();
    const arenaRect = arena?.getBoundingClientRect();
    const battlefieldRect = battlefield?.getBoundingClientRect();
    const selfHandRect = selfHand?.getBoundingClientRect();
    const actionLayerRect = actionLayer?.getBoundingClientRect();
    const rectOf = (element) => {
      const rect = element?.getBoundingClientRect();
      return rect ? { bottom: rect.bottom, height: rect.height, left: rect.left, right: rect.right, top: rect.top, width: rect.width } : null;
    };
    const clippedRectOf = (element) => {
      const source = element.getBoundingClientRect();
      let left = Math.max(0, source.left);
      let right = Math.min(window.innerWidth, source.right);
      let top = Math.max(0, source.top);
      let bottom = Math.min(window.innerHeight, source.bottom);
      const clippedOverflow = new Set(["auto", "clip", "hidden", "scroll"]);
      let ancestor = element.parentElement;
      while (ancestor) {
        const style = getComputedStyle(ancestor);
        const bounds = ancestor.getBoundingClientRect();
        if (clippedOverflow.has(style.overflowX)) {
          left = Math.max(left, bounds.left);
          right = Math.min(right, bounds.right);
        }
        if (clippedOverflow.has(style.overflowY)) {
          top = Math.max(top, bounds.top);
          bottom = Math.min(bottom, bounds.bottom);
        }
        ancestor = ancestor.parentElement;
      }
      return right - left > 2 && bottom - top > 2 ? { bottom, left, right, top } : null;
    };
    const isInsideViewport = (element) => clippedRectOf(element) !== null;
    const overlapCount = (elements) => {
      const rects = elements.map(clippedRectOf).filter(Boolean);
      let count = 0;
      for (let index = 0; index < rects.length; index += 1) {
        for (let candidate = index + 1; candidate < rects.length; candidate += 1) {
          const overlapWidth = Math.min(rects[index].right, rects[candidate].right) - Math.max(rects[index].left, rects[candidate].left);
          const overlapHeight = Math.min(rects[index].bottom, rects[candidate].bottom) - Math.max(rects[index].top, rects[candidate].top);
          if (overlapWidth > 2 && overlapHeight > 2) count += 1;
        }
      }
      return count;
    };
    const overlapPairs = (elements) => {
      const visible = elements.map((element) => ({ element, rect: clippedRectOf(element) })).filter((entry) => entry.rect);
      const pairs = [];
      for (let index = 0; index < visible.length; index += 1) {
        const rect = visible[index].rect;
        for (let candidate = index + 1; candidate < visible.length; candidate += 1) {
          const candidateRect = visible[candidate].rect;
          const overlapWidth = Math.min(rect.right, candidateRect.right) - Math.max(rect.left, candidateRect.left);
          const overlapHeight = Math.min(rect.bottom, candidateRect.bottom) - Math.max(rect.top, candidateRect.top);
          if (overlapWidth > 2 && overlapHeight > 2) {
            const label = (element) => element.getAttribute("data-object-id") ?? element.getAttribute("aria-label") ?? element.className;
            const bounds = (value) => `${Math.round(value.left)},${Math.round(value.top)},${Math.round(value.right)},${Math.round(value.bottom)}`;
            pairs.push(`${label(visible[index].element)} [${bounds(rect)}] <> ${label(visible[candidate].element)} [${bounds(candidateRect)}]`);
          }
        }
      }
      return pairs;
    };
    const containsPoint = (rect, x, y) => Boolean(rect && x >= rect.left && x <= rect.right && y >= rect.top && y <= rect.bottom);
    const legalTargets = Array.from(document.querySelectorAll("[data-object-id].is-prompt-enabled"))
      .filter((element) => !element.closest("[data-arena-hand]") && isInsideViewport(element));
    const legalTargetOcclusions = legalTargets
      .filter((element) => {
        const rect = element.getBoundingClientRect();
        return containsPoint(selfHandRect, rect.left + rect.width / 2, rect.top + rect.height / 2);
      })
      .map((element) => element.getAttribute("data-object-id") ?? "unknown");
    const paySubmitButton = Array.from(actionLayer?.querySelectorAll("button") ?? [])
      .find((button) => button.textContent?.includes("提交支付"));
    const paySubmitRect = paySubmitButton?.getBoundingClientRect();
    const opponentCards = Array.from(opponentHandCards?.querySelectorAll(".arena-fan-card") ?? []);
    const selfCards = Array.from(selfHandCards?.querySelectorAll(".arena-fan-card") ?? []);
    const homeCards = Array.from(document.querySelectorAll(".wire-player-home .card-face")).filter(isInsideViewport);
    const pileBoxes = Array.from(document.querySelectorAll(".arena-hand.is-self .wire-stack-box")).filter(isInsideViewport);
    const runeCards = Array.from(document.querySelectorAll(".arena-hand.is-self .wire-rune-card-frame .card-face")).filter(isInsideViewport);
    const battlefieldCards = Array.from(document.querySelectorAll("[data-arena-battlefield-region] .wire-card-flow-battlefield-unit > .card-face"));
    const baseSlots = Array.from(document.querySelectorAll(".wire-player-self .wire-base-card-grid > :where(.card-face, .wire-card-slot)"));
    const opponentBaseSlots = Array.from(document.querySelectorAll(".wire-player-opponent .wire-base-card-grid > :where(.card-face, .wire-card-slot)"));
    const selfBaseGrid = document.querySelector(".wire-player-self .wire-base-card-grid");
    const opponentBaseGrid = document.querySelector(".wire-player-opponent .wire-base-card-grid");
    const selfBaseGridRect = selfBaseGrid?.getBoundingClientRect();
    const opponentBaseGridRect = opponentBaseGrid?.getBoundingClientRect();
    const fullyVisibleSlotCount = (gridRect, slots) => gridRect ? slots.filter((slot) => {
      const slotRect = slot.getBoundingClientRect();
      const visibleWidth = Math.min(slotRect.right, gridRect.right) - Math.max(slotRect.left, gridRect.left);
      return visibleWidth >= slotRect.width * 0.8;
    }).length : 0;
    const baseFullyVisibleSlotCount = fullyVisibleSlotCount(selfBaseGridRect, baseSlots);
    const opponentBaseFullyVisibleSlotCount = fullyVisibleSlotCount(opponentBaseGridRect, opponentBaseSlots);
    const selfHome = document.querySelector(".wire-player-self.wire-player-home");
    const opponentHome = document.querySelector(".wire-player-opponent.wire-player-home");
    const mirrorAxisX = arenaRect ? arenaRect.left + arenaRect.right : window.innerWidth;
    const mirrorAxisY = arenaRect ? arenaRect.top + arenaRect.bottom : window.innerHeight;
    const centerMirrorError = (selfElement, opponentElement) => {
      const selfRect = selfElement?.getBoundingClientRect();
      const opponentRect = opponentElement?.getBoundingClientRect();
      if (!selfRect || !opponentRect) return 10_000;
      return Math.max(
        Math.abs(opponentRect.left - (mirrorAxisX - selfRect.right)),
        Math.abs(opponentRect.right - (mirrorAxisX - selfRect.left)),
        Math.abs(opponentRect.top - (mirrorAxisY - selfRect.bottom)),
        Math.abs(opponentRect.bottom - (mirrorAxisY - selfRect.top)),
        Math.abs(opponentRect.width - selfRect.width),
        Math.abs(opponentRect.height - selfRect.height)
      );
    };
    const playerHomeSymmetryError = Math.max(...[
      null,
      ".wire-home-legend",
      ".wire-home-hero",
      ".wire-home-base",
      ".wire-home-score-token"
    ].map((selector) => centerMirrorError(
      selector ? selfHome?.querySelector(selector) : selfHome,
      selector ? opponentHome?.querySelector(selector) : opponentHome
    )));
    const railSelectors = [".wire-hand-rune-deck", ".wire-hand-rune-track", ".wire-hand-cards", ".wire-hand-piles"];
    const playerRailSymmetryError = Math.max(...railSelectors.map((selector) => centerMirrorError(
      selfHand?.querySelector(selector),
      opponentHand?.querySelector(selector)
    )));
    const selfRailRegions = railSelectors
      .map((selector) => selfHand?.querySelector(selector))
      .filter((element) => element instanceof HTMLElement);
    const opponentRailRegions = railSelectors
      .map((selector) => opponentHand?.querySelector(selector))
      .filter((element) => element instanceof HTMLElement);
    const overlapArea = (first, second) => {
      const firstRect = first?.getBoundingClientRect();
      const secondRect = second?.getBoundingClientRect();
      if (!firstRect || !secondRect) return 0;
      return Math.max(0, Math.min(firstRect.right, secondRect.right) - Math.max(firstRect.left, secondRect.left))
        * Math.max(0, Math.min(firstRect.bottom, secondRect.bottom) - Math.max(firstRect.top, secondRect.top));
    };
    const diagnosticTrigger = document.querySelector("[data-game-debug-drawer]");
    const diagnosticResourceOverlapArea = Math.max(0, ...opponentRailRegions.map((region) => overlapArea(diagnosticTrigger, region)));
    const visibleImageOverlayCount = Array.from(document.querySelectorAll(".arena-table .card-image-cost, .arena-table .card-image-power, .arena-table .card-image-title"))
      .filter((element) => getComputedStyle(element).display !== "none" && isInsideViewport(element)).length;
    const opponentPileCards = Array.from(document.querySelectorAll(".arena-hand.is-opponent .wire-hand-piles > :where(.card-face, .wire-stack-box)"));
    const publicCards = [...homeCards, ...battlefieldCards, ...selfCards, ...opponentCards, ...opponentPileCards];
    const homeLayouts = Array.from(document.querySelectorAll(".wire-player-home")).map((element) => {
      const style = getComputedStyle(element);
      return {
        columns: style.gridTemplateColumns,
        gap: style.columnGap,
        heroArea: style.getPropertyValue("--wire-hero-area-w").trim(),
        signatureArea: style.getPropertyValue("--wire-signature-area-w").trim(),
        width: element.getBoundingClientRect().width
      };
    });
    return {
      actionEntryCount: actionLayer?.querySelectorAll("[data-action-render-action]").length ?? 0,
      actionLayerMode: actionLayer?.getAttribute("data-arena-action-mode") ?? "none",
      actionLayerRect: rectOf(actionLayer),
      actionPanelCount: actionLayer?.querySelectorAll('[data-action-panel-presentation="arena"]').length ?? 0,
      arenaRect: rectOf(arena),
      baseFullyVisibleSlotCount,
      baseGridClientWidth: selfBaseGrid?.clientWidth ?? 0,
      baseGridWidthDifference: Math.abs((selfBaseGrid?.clientWidth ?? 0) - (opponentBaseGrid?.clientWidth ?? 0)),
      baseSlotCount: baseSlots.length,
      baseSlotOverlapCount: overlapCount([...baseSlots, ...opponentBaseSlots]),
      battlefieldClientWidth: battlefield?.clientWidth ?? 0,
      battlefieldHeightRatio: tableRect && battlefieldRect ? battlefieldRect.height / tableRect.height : 0,
      battlefieldScrollWidth: battlefield?.scrollWidth ?? 0,
      bodyScrollHeight: document.documentElement.scrollHeight,
      bodyScrollWidth: document.documentElement.scrollWidth,
      debugOpen: document.querySelector("[data-game-debug-drawer]")?.hasAttribute("open") ?? false,
      handViewportRatio: selfHandRect ? selfHandRect.height / window.innerHeight : 1,
      homeCardCount: homeCards.length,
      homeCardMaxHeight: homeCards.reduce((height, card) => Math.max(height, card.getBoundingClientRect().height), 0),
      homeLayouts,
      hasArena: Boolean(arena),
      hasFixedDock: Boolean(document.querySelector("[data-game-action-dock]")),
      hasRoot: Boolean(root),
      hasTable: Boolean(table),
      legalTargetOcclusions,
      opponentBackCount: opponentCards.filter((card) => card.classList.contains("card-back")).length,
      opponentBaseFullyVisibleSlotCount,
      opponentBaseGridClientWidth: opponentBaseGrid?.clientWidth ?? 0,
      opponentBaseSlotCount: opponentBaseSlots.length,
      opponentCardCount: opponentCards.length,
      opponentFrontCount: opponentCards.reduce((count, card) => count + card.querySelectorAll(".card-full-image").length, 0),
      opponentNeutralLabelCount: opponentCards.filter((card) => card.getAttribute("aria-label") === "未公开卡牌").length,
      paySubmitButton: paySubmitButton ? {
        bottom: paySubmitRect?.bottom ?? 0,
        left: paySubmitRect?.left ?? 0,
        right: paySubmitRect?.right ?? 0,
        top: paySubmitRect?.top ?? 0,
        visible: paySubmitButton instanceof HTMLElement && paySubmitButton.offsetParent !== null
      } : null,
      pileBoxCount: pileBoxes.length,
      pileBoxMaxHeight: pileBoxes.reduce((height, box) => Math.max(height, box.getBoundingClientRect().height), 0),
      diagnosticResourceOverlapArea,
      playerHomeSymmetryError,
      playerRailSymmetryError,
      publicCardOverlapCount: overlapCount(publicCards),
      publicCardOverlapPairs: overlapPairs(publicCards),
      runeCardCount: runeCards.length,
      runeCardMaxHeight: runeCards.reduce((height, card) => Math.max(height, card.getBoundingClientRect().height), 0),
      scoreTokenCount: table?.querySelectorAll(".tabletop-score-token").length ?? 0,
      selfCardCount: selfCards.length,
      selfCardMaxBottom: selfCards.reduce((bottom, card) => Math.max(bottom, card.getBoundingClientRect().bottom), 0),
      selfFrontCount: selfCards.reduce((count, card) => count + card.querySelectorAll(".card-full-image").length, 0),
      selfVisibleFrontCount: selfCards.filter(isInsideViewport).length,
      opponentRailOverlapCount: overlapCount(opponentRailRegions),
      selfRailOverlapCount: overlapCount(selfRailRegions),
      tableRect: rectOf(table),
      quickActionsRect: rectOf(document.querySelector(".game-match-quick-actions")),
      viewportHeight: window.innerHeight,
      viewportWidth: window.innerWidth,
      visibleImageOverlayCount
    };
  });

  const failures = [];
  const mobile = surface.viewportWidth < 900;
  let mobileLaneSwitch = null;
  if (mobile) {
    const laneControls = page.locator("[data-arena-battlefield-lane-control]");
    const controlCount = await laneControls.count();
    if (controlCount === 2) {
      await page.locator('[data-arena-battlefield-lane-control="right"]').click();
      await page.waitForFunction(() => document.querySelector("[data-arena-battlefield-region]")?.getAttribute("data-arena-battlefield-active-lane") === "right");
      await page.waitForTimeout(450);
      mobileLaneSwitch = await page.evaluate(() => {
        const battlefield = document.querySelector("[data-arena-battlefield-region]");
        const rightLane = document.querySelector('[data-wire-battlefield-lane-index="1"]');
        const battlefieldRect = battlefield?.getBoundingClientRect();
        const laneRect = rightLane?.getBoundingClientRect();
        return {
          activeLane: battlefield?.getAttribute("data-arena-battlefield-active-lane") ?? "",
          controlCount: document.querySelectorAll("[data-arena-battlefield-lane-control]").length,
          rightLaneVisibleWidth: battlefieldRect && laneRect
            ? Math.max(0, Math.min(battlefieldRect.right, laneRect.right) - Math.max(battlefieldRect.left, laneRect.left))
            : 0
        };
      });
      await page.locator('[data-arena-battlefield-lane-control="left"]').click();
      await page.waitForFunction(() => document.querySelector("[data-arena-battlefield-region]")?.getAttribute("data-arena-battlefield-active-lane") === "left");
    } else {
      mobileLaneSwitch = { activeLane: "", controlCount, rightLaneVisibleWidth: 0 };
    }
  }
  if (!surface.hasRoot || !surface.hasTable || !surface.hasArena || surface.hasFixedDock) {
    failures.push(`playable match shell is incomplete: ${JSON.stringify(surface)}`);
  }
  if (surface.scoreTokenCount < 2) {
    failures.push(`playable table must show both player score tokens: ${surface.scoreTokenCount}`);
  }
  const minimumHomeCardHeight = mobile ? 89 : surface.viewportWidth >= 1600 ? 134 : surface.viewportWidth < 1400 ? 100 : 114;
  if (surface.homeCardCount > 0 && surface.homeCardMaxHeight < minimumHomeCardHeight) {
    failures.push(`public legend, champion, and base cards are too small: ${JSON.stringify({ actual: surface.homeCardMaxHeight, minimum: minimumHomeCardHeight })}`);
  }
  const minimumPileBoxHeight = mobile ? 83 : surface.viewportWidth >= 1600 ? 122 : surface.viewportWidth < 1400 ? 91 : 105;
  if (surface.pileBoxCount > 0 && surface.pileBoxMaxHeight < minimumPileBoxHeight) {
    failures.push(`deck piles are too small: ${JSON.stringify({ actual: surface.pileBoxMaxHeight, minimum: minimumPileBoxHeight })}`);
  }
  const minimumRuneCardHeight = mobile ? 52 : surface.viewportWidth >= 1600 ? 75 : surface.viewportWidth < 1400 ? 58 : 66;
  if (surface.runeCardCount > 0 && surface.runeCardMaxHeight < minimumRuneCardHeight) {
    failures.push(`rune cards are too small: ${JSON.stringify({ actual: surface.runeCardMaxHeight, minimum: minimumRuneCardHeight })}`);
  }
  if (surface.baseSlotCount < 6 || surface.opponentBaseSlotCount < 6) {
    failures.push(`both bases must expose at least six independent visual slots: ${JSON.stringify({ self: surface.baseSlotCount, opponent: surface.opponentBaseSlotCount })}`);
  }
  const minimumBaseGridWidth = mobile ? 90 : surface.viewportWidth >= 1600 ? 820 : surface.viewportWidth < 1400 ? 590 : 670;
  if (surface.baseGridClientWidth < minimumBaseGridWidth) {
    failures.push(`base rail must use available horizontal space: ${JSON.stringify({ actual: surface.baseGridClientWidth, minimum: minimumBaseGridWidth })}`);
  }
  const minimumVisibleBaseSlots = mobile ? 1 : 6;
  if (surface.baseFullyVisibleSlotCount < minimumVisibleBaseSlots) {
    failures.push(`base rail must reveal enough complete slots: ${JSON.stringify({ actual: surface.baseFullyVisibleSlotCount, minimum: minimumVisibleBaseSlots })}`);
  }
  if (!mobile && surface.opponentBaseFullyVisibleSlotCount < minimumVisibleBaseSlots) {
    failures.push(`opponent base rail must reveal enough complete slots: ${JSON.stringify({ actual: surface.opponentBaseFullyVisibleSlotCount, minimum: minimumVisibleBaseSlots })}`);
  }
  if (!mobile && (surface.playerHomeSymmetryError > 2 || surface.playerRailSymmetryError > 2 || surface.baseGridWidthDifference > 2)) {
    failures.push(`player zones must mirror 180 degrees around the arena center: ${JSON.stringify({ homeSymmetryError: surface.playerHomeSymmetryError, railSymmetryError: surface.playerRailSymmetryError, baseGridWidthDifference: surface.baseGridWidthDifference, selfBaseWidth: surface.baseGridClientWidth, opponentBaseWidth: surface.opponentBaseGridClientWidth })}`);
  }
  if (surface.baseSlotOverlapCount > 0 || surface.publicCardOverlapCount > 0 || surface.selfRailOverlapCount > 0 || surface.opponentRailOverlapCount > 0 || surface.diagnosticResourceOverlapArea > 0) {
    failures.push(`arena elements must not overlap: ${JSON.stringify({ base: surface.baseSlotOverlapCount, cards: surface.publicCardOverlapCount, diagnosticArea: surface.diagnosticResourceOverlapArea, homes: surface.homeLayouts, opponentRail: surface.opponentRailOverlapCount, pairs: surface.publicCardOverlapPairs, selfRail: surface.selfRailOverlapCount })}`);
  }
  if (surface.visibleImageOverlayCount > 0) {
    failures.push(`official card images must not show duplicate cost, power, or title overlays: ${surface.visibleImageOverlayCount}`);
  }
  if (surface.selfCardCount > 0 && surface.selfFrontCount < 1) {
    failures.push(`visible self cards must render official card fronts: ${JSON.stringify({
      cards: surface.selfCardCount,
      fronts: surface.selfFrontCount
    })}`);
  }
  if (surface.selfCardCount > 0 && surface.selfVisibleFrontCount < 1) {
    failures.push(`at least one self-hand card must be visible: ${JSON.stringify({
      cards: surface.selfCardCount,
      visibleFronts: surface.selfVisibleFrontCount
    })}`);
  }
  if (surface.opponentCardCount > 0 && (
    surface.opponentBackCount !== surface.opponentCardCount
    || surface.opponentNeutralLabelCount !== surface.opponentCardCount
    || surface.opponentFrontCount !== 0
  )) {
    failures.push(`opponent hand must stay redacted as card backs: ${JSON.stringify({
      backs: surface.opponentBackCount,
      cards: surface.opponentCardCount,
      fronts: surface.opponentFrontCount,
      neutralLabels: surface.opponentNeutralLabelCount
    })}`);
  }
  const minimumBattlefieldRatio = mobile ? 0.279 : 0.329;
  const maximumBattlefieldRatio = mobile ? 0.301 : 0.351;
  if (surface.battlefieldHeightRatio < minimumBattlefieldRatio) {
    failures.push(`public battlefield is too short: ${JSON.stringify({ actual: surface.battlefieldHeightRatio, minimum: minimumBattlefieldRatio })}`);
  }
  if (surface.battlefieldHeightRatio > maximumBattlefieldRatio) {
    failures.push(`public battlefield is too tall: ${JSON.stringify({ actual: surface.battlefieldHeightRatio, maximum: maximumBattlefieldRatio })}`);
  }
  const maximumHandRatio = mobile ? 0.261 : 0.181;
  if (surface.handViewportRatio > maximumHandRatio) {
    failures.push(`resting hand is too tall: ${JSON.stringify({ actual: surface.handViewportRatio, maximum: maximumHandRatio })}`);
  }
  if (surface.legalTargetOcclusions.length > 0) {
    failures.push(`resting hand must not cover visible legal targets: ${surface.legalTargetOcclusions.join(", ")}`);
  }
  if (surface.selfCardMaxBottom > surface.viewportHeight + 1) {
    failures.push(`resting hand cards must remain inside the viewport: ${surface.selfCardMaxBottom}`);
  }
  if (mobile && surface.battlefieldScrollWidth <= surface.battlefieldClientWidth) {
    failures.push(`mobile arena must keep battlefield lanes internally scrollable: ${JSON.stringify({ client: surface.battlefieldClientWidth, scroll: surface.battlefieldScrollWidth })}`);
  }
  if (mobile && (!mobileLaneSwitch || mobileLaneSwitch.controlCount !== 2 || mobileLaneSwitch.activeLane !== "right" || mobileLaneSwitch.rightLaneVisibleWidth < 160)) {
    failures.push(`mobile battlefield lane switch must reveal the selected right lane: ${JSON.stringify(mobileLaneSwitch)}`);
  }
  if (shot.name === "prompt-pay-cost") {
    const button = surface.paySubmitButton;
    if (surface.actionLayerMode !== "modal" || surface.actionPanelCount < 1 || surface.actionEntryCount < 1) {
      failures.push(`pay-cost scenario must use the dedicated arena modal: ${JSON.stringify(surface)}`);
    }
    if (!button || !button.visible || !surface.actionLayerRect
      || button.left < surface.actionLayerRect.left
      || button.right > surface.actionLayerRect.right
      || button.top < surface.actionLayerRect.top
      || button.bottom > surface.actionLayerRect.bottom) {
      failures.push(`pay-cost submit button must be visible without page scrolling: ${JSON.stringify({ button, layer: surface.actionLayerRect })}`);
    }
  }
  if (surface.debugOpen) {
    failures.push("connection and rule diagnostics must stay collapsed during normal play.");
  }
  for (const [label, rect] of [["table", surface.tableRect], ["arena", surface.arenaRect]]) {
    if (!rect || rect.left < -1 || rect.right > surface.viewportWidth + 1 || rect.top < -1 || rect.bottom > surface.viewportHeight + 1) {
      failures.push(`${label} must fit inside the first viewport: ${JSON.stringify(rect)}`);
    }
  }
  if (surface.actionLayerRect && surface.actionLayerRect.height > 0 && (
    surface.actionLayerRect.left < -1
    || surface.actionLayerRect.right > surface.viewportWidth + 1
    || surface.actionLayerRect.top < -1
    || surface.actionLayerRect.bottom > surface.viewportHeight + 1
  )) {
    failures.push(`visible arena action layer must fit inside the viewport: ${JSON.stringify(surface.actionLayerRect)}`);
  }
  if (surface.quickActionsRect && (surface.quickActionsRect.left < -1 || surface.quickActionsRect.right > surface.viewportWidth + 1)) {
    failures.push(`topbar quick actions must fit inside the viewport: ${JSON.stringify(surface.quickActionsRect)}`);
  }
  if (surface.bodyScrollWidth > surface.viewportWidth + 1 || surface.bodyScrollHeight > surface.viewportHeight + 1) {
    failures.push(`match page must not overflow the viewport: ${JSON.stringify({
      height: [surface.bodyScrollHeight, surface.viewportHeight],
      width: [surface.bodyScrollWidth, surface.viewportWidth]
    })}`);
  }
  if (failures.length > 0) {
    throw new Error(`Playable match surface assertions failed:\n${failures.join("\n")}`);
  }
}

async function assertLegacyMatchStateSurface(page, shot) {
  await assertConnectionRecoveryPanelSurface(page, "match");

  const activeSlot = await page.locator("[data-wire-side-panel-directory]").first().getAttribute("data-wire-side-panel-directory-active-slot");
  await openSidePanelSlot(page, "commandCenter");
  const receiptSurface = await page.evaluate(() => {
    const textOf = (node) => node?.textContent?.trim().replace(/\s+/g, " ") ?? "";
    const receipt = document.querySelector("[data-wire-side-panel-receipt]");
    return {
      bridgeState: receipt?.getAttribute("data-wire-side-panel-receipt-bridge-state") ?? "",
      canOpenLayer: receipt?.getAttribute("data-wire-side-panel-receipt-can-open-layer") ?? "",
      eventCount: receipt?.getAttribute("data-wire-side-panel-receipt-event-count") ?? "",
      hiddenCount: receipt?.getAttribute("data-wire-side-panel-receipt-hidden-count") ?? "",
      mode: receipt?.getAttribute("data-wire-side-panel-receipt-mode") ?? "",
      state: receipt?.getAttribute("data-wire-side-panel-receipt-state") ?? "",
      text: textOf(receipt)
    };
  });

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
      ruleChainActiveLane: ruleChain?.getAttribute("data-wire-side-panel-rule-chain-active-lane") ?? "",
      ruleChainAria: ruleChain?.getAttribute("aria-label") ?? "",
      ruleChainLanes: Array.from(document.querySelectorAll("[data-wire-side-panel-rule-chain-lane]")).map((node) => ({
        count: node.getAttribute("data-wire-side-panel-rule-chain-lane-count") ?? "",
        detailId: node.getAttribute("data-wire-side-panel-rule-chain-lane-detail-id") ?? "",
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
        slot: node.getAttribute("data-wire-side-panel-rule-chain-route-slot") ?? "",
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
  failures.push(...await assertRuleChainBrowserSurface(surface, shot));
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
  if (!receiptSurface.mode || !receiptSurface.state || !receiptSurface.bridgeState) {
    failures.push(`receipt rail missing mode/state/bridge contract: ${JSON.stringify({
      bridgeState: receiptSurface.bridgeState,
      mode: receiptSurface.mode,
      state: receiptSurface.state
    })}`);
  }
  if (!["true", "false"].includes(receiptSurface.canOpenLayer)) {
    failures.push(`receipt rail must expose can-open-layer as a boolean string: ${receiptSurface.canOpenLayer}`);
  }
  if (Number.isNaN(Number(receiptSurface.eventCount)) || Number.isNaN(Number(receiptSurface.hiddenCount))) {
    failures.push(`receipt rail must expose numeric event/hidden counts: ${JSON.stringify({
      eventCount: receiptSurface.eventCount,
      hiddenCount: receiptSurface.hiddenCount
    })}`);
  }
  if (!receiptSurface.text.includes("提交反馈") || !receiptSurface.text.includes("后续")) {
    failures.push(`receipt rail must render readable feedback/followup copy: ${receiptSurface.text}`);
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

async function assertRuleChainBrowserSurface(surface, shot) {
  const failures = [];
  if (!surface.ruleChainActiveLane) {
    failures.push(`rule chain strip must expose an active lane: ${surface.ruleChainActiveLane}`);
  }
  if (!surface.ruleChainAria.includes("规则链")) {
    failures.push(`rule chain strip is missing aria label: ${surface.ruleChainAria}`);
  }
  if (!surface.ruleChainText.includes("下一步")) {
    failures.push(`rule chain strip is missing next-step text: ${surface.ruleChainText}`);
  }
  for (const requiredText of ["结算链", "规则任务", "触发队列", "近期事件"]) {
    if (!surface.ruleChainText.includes(requiredText)) {
      failures.push(`rule chain strip for ${shot.name} must render ${requiredText}: ${surface.ruleChainText}`);
    }
  }
  const lanesByKey = Object.fromEntries(surface.ruleChainLanes.map((lane) => [lane.key, lane]));
  for (const laneKey of ["stack", "task", "trigger", "resolution"]) {
    const lane = lanesByKey[laneKey];
    if (!lane?.state || lane.count === "" || !lane.text) {
      failures.push(`rule chain lane ${laneKey} must expose state/count/text: ${JSON.stringify(lane)}`);
    }
  }
  const metricKeys = new Set(surface.ruleChainMetrics.map((metric) => metric.key));
  for (const metricKey of ["lane", "responsibility", "event", "detail"]) {
    if (!metricKeys.has(metricKey)) {
      failures.push(`rule chain metric ${metricKey} missing: ${JSON.stringify(surface.ruleChainMetrics)}`);
    }
  }
  const routesByKey = Object.fromEntries(surface.ruleChainRoutes.map((route) => [route.key, route]));
  for (const [routeKey, routeSlot] of Object.entries({
    detail: "timelineDetail",
    flow: "serverFlow",
    log: "log",
    queue: "ruleQueue"
  })) {
    const route = routesByKey[routeKey];
    if (route?.slot !== routeSlot || !route.state || !route.text) {
      failures.push(`rule chain route ${routeKey} must expose slot ${routeSlot}, state, and text: ${JSON.stringify(route)}`);
    }
  }
  return failures;
}

async function assertConnectionRecoveryPanelSurface(page, expectedSurface) {
  const surface = await page.evaluate((surfaceName) => {
    const textOf = (node) => node?.textContent?.trim().replace(/\s+/g, " ") ?? "";
    const panel = Array.from(document.querySelectorAll("[data-connection-recovery-panel]"))
      .find((node) => node.getAttribute("data-connection-recovery-surface") === surfaceName);
    return {
      actions: Array.from(panel?.querySelectorAll("[data-connection-recovery-action]") ?? []).map((node) => ({
        disabled: node.hasAttribute("disabled"),
        disabledAttr: node.getAttribute("data-connection-recovery-action-disabled") ?? "",
        id: node.getAttribute("data-connection-recovery-action") ?? "",
        state: node.getAttribute("data-connection-recovery-action-state") ?? "",
        text: textOf(node),
        title: node.getAttribute("title") ?? ""
      })),
      actionGroupLabel: panel?.querySelector(".connection-recovery-actions")?.getAttribute("aria-label") ?? "",
      state: panel?.getAttribute("data-connection-recovery-state") ?? "",
      surface: panel?.getAttribute("data-connection-recovery-surface") ?? "",
      text: textOf(panel),
      tickLabel: panel?.getAttribute("data-connection-recovery-tick-label") ?? ""
    };
  }, expectedSurface);

  const failures = [];
  if (surface.surface !== expectedSurface) {
    failures.push(`connection recovery panel must expose ${expectedSurface} surface: ${JSON.stringify(surface)}`);
  }
  if (!surface.state) {
    failures.push(`connection recovery panel must expose a state: ${JSON.stringify(surface)}`);
  }
  if (!surface.text.includes("连接恢复") || surface.actionGroupLabel !== "连接恢复操作") {
    failures.push(`connection recovery panel must keep readable recovery copy and action group: ${JSON.stringify(surface)}`);
  }
  const actionsById = Object.fromEntries(surface.actions.map((action) => [action.id, action]));
  if (surface.actions.length !== 3) {
    failures.push(`connection recovery panel must expose connect/resync/disconnect actions: ${JSON.stringify(surface.actions)}`);
  }
  for (const actionId of ["connect", "resync", "disconnect"]) {
    const action = actionsById[actionId];
    if (!action?.state || !action.text || !action.title || !["true", "false"].includes(action.disabledAttr)) {
      failures.push(`connection recovery action ${actionId} must expose state, label, title, and disabled flag: ${JSON.stringify(action)}`);
      continue;
    }
    if (action.disabled !== (action.disabledAttr === "true")) {
      failures.push(`connection recovery action ${actionId} disabled attribute must match DOM disabled: ${JSON.stringify(action)}`);
    }
  }
  if (expectedSurface === "room") {
    if (surface.state !== "online" || !surface.tickLabel.includes("快照 tick")) {
      failures.push(`room recovery panel should be online after automatic room connection: ${JSON.stringify(surface)}`);
    }
    if (actionsById.connect?.disabled !== true) {
      failures.push(`room recovery connect action must be disabled after automatic connection: ${JSON.stringify(actionsById.connect)}`);
    }
    if (actionsById.resync?.disabled !== false || actionsById.disconnect?.disabled !== false) {
      failures.push(`room recovery resync/disconnect actions must be available after connection: ${JSON.stringify(surface.actions)}`);
    }
  } else if (expectedSurface === "match") {
    if (surface.state !== "online" || !surface.tickLabel.includes("快照 tick")) {
      failures.push(`match recovery panel should be online after seeded connection: ${JSON.stringify(surface)}`);
    }
    if (actionsById.connect?.disabled !== true) {
      failures.push(`match recovery connect action must be disabled after connection: ${JSON.stringify(actionsById.connect)}`);
    }
    if (actionsById.resync?.disabled !== false || actionsById.disconnect?.disabled !== false) {
      failures.push(`match recovery resync/disconnect actions must remain available after connection: ${JSON.stringify(surface.actions)}`);
    }
  }

  if (failures.length > 0) {
    throw new Error(`Connection recovery panel assertions failed:\n${failures.join("\n")}`);
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
  const result = await page.evaluate(async ({ playerFacingMatch }) => {
    const context = playerFacingMatch
      ? {
          include: [["[data-playable-match-surface]"]],
          exclude: [["[data-game-debug-drawer]"]]
        }
      : document;
    return await globalThis.axe.run(context, {
      resultTypes: ["violations"],
      rules: {
        "color-contrast": { enabled: true },
        "button-name": { enabled: true },
        "label": { enabled: true }
      }
    });
  }, { playerFacingMatch: name.startsWith("match-") || name.startsWith("prompt-") });

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
    return { status: "playable-surface-invariant", ratio: null };
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

function assertPlayableVisual(buffer, name) {
  const image = PNG.sync.read(buffer);
  let sampled = 0;
  let luminanceSum = 0;
  let luminanceSquaredSum = 0;
  let minimumLuminance = 255;
  let maximumLuminance = 0;
  const colorBuckets = new Set();
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
    luminanceSum += luminance;
    luminanceSquaredSum += luminance * luminance;
    minimumLuminance = Math.min(minimumLuminance, luminance);
    maximumLuminance = Math.max(maximumLuminance, luminance);
    colorBuckets.add(`${red >> 4},${green >> 4},${blue >> 4}`);
  }

  const meanLuminance = luminanceSum / sampled;
  const luminanceDeviation = Math.sqrt((luminanceSquaredSum / sampled) - (meanLuminance * meanLuminance));
  const luminanceRange = maximumLuminance - minimumLuminance;
  const failures = [];
  if (sampled < 1_000) {
    failures.push(`sampled=${sampled}`);
  }
  if (luminanceRange < 70) {
    failures.push(`range=${luminanceRange.toFixed(1)}`);
  }
  if (luminanceDeviation < 8) {
    failures.push(`deviation=${luminanceDeviation.toFixed(1)}`);
  }
  if (colorBuckets.size < 12) {
    failures.push(`colorBuckets=${colorBuckets.size}`);
  }
  if (failures.length > 0) {
    throw new Error(`Playable visual invariant failed for ${name}: ${failures.join(", ")}`);
  }

  return {
    checked: true,
    colorBucketCount: colorBuckets.size,
    luminanceDeviation,
    luminanceRange,
    meanLuminance
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
  await page.evaluate(({ player, playerKey, roomId, server, storedSession }) => {
    localStorage.setItem("riftbound.serverUrl", server);
    localStorage.setItem("riftbound.playerId", player);
    localStorage.setItem("riftbound.playerKey", playerKey);
    localStorage.setItem(`riftbound.session.${roomId}.${player}`, JSON.stringify(storedSession));
  }, {
    player: playerId,
    playerKey: qaPlayerKeys[playerId],
    roomId: seeded.roomId,
    server: serverUrl,
    storedSession: session
  });
  await page.goto(`${frontendUrl}/matches/${seeded.roomId}`, { waitUntil: "networkidle" });
  try {
    await page.waitForFunction(() => {
      const connectionText = document.querySelector(".game-connection-state")?.textContent ?? "";
      return Boolean(document.querySelector("[data-playable-match-surface]")) && connectionText.includes("已连接");
    },
    undefined, { timeout: 15_000 });
  } catch (error) {
    const diagnostic = await page.evaluate(() => {
      const panel = document.querySelector('[data-connection-recovery-panel][data-connection-recovery-surface="match"]');
      return {
        body: document.body.textContent?.trim().replace(/\s+/g, " ").slice(0, 800) ?? "",
        connection: document.querySelector(".game-connection-state")?.textContent?.trim() ?? "",
        hasPlayableSurface: Boolean(document.querySelector("[data-playable-match-surface]")),
        recoveryState: document.querySelector("[data-match-recovery-surface]")?.getAttribute("data-match-recovery-state") ?? "missing",
        state: panel?.getAttribute("data-connection-recovery-state") ?? "missing",
        text: panel?.textContent?.trim().replace(/\s+/g, " ") ?? "",
        playerId: localStorage.getItem("riftbound.playerId"),
        hasPlayerKey: Boolean(localStorage.getItem("riftbound.playerKey"))
      };
    });
    throw new Error(`Seeded match did not reconnect: ${JSON.stringify(diagnostic)}`, { cause: error });
  }
  await page.waitForFunction((expectedPlayerId) => document.body.textContent?.includes(expectedPlayerId), playerId, { timeout: 15_000 });
}

async function runPlayableCardInspectInteraction(page, report) {
  const card = page.locator(".wire-hand-self .wire-hand-cards button.card-face").first();
  await card.waitFor({ state: "visible", timeout: 10_000 });
  const cardLabel = await card.getAttribute("aria-label") ?? "可见手牌";
  await card.click();

  const tray = page.locator('[data-wire-object-command-tray-visible="true"][data-wire-object-command-tray-presentation="arena"]').first();
  await tray.waitFor({ state: "visible", timeout: 5_000 });
  const detailButton = tray.getByRole("button", { name: /详情/ }).first();
  await detailButton.click();

  const dialog = page.locator('[data-detail-dialog-state="open"]').first();
  await dialog.waitFor({ state: "visible", timeout: 5_000 });
  const title = (await dialog.locator("#card-detail-title").textContent())?.trim() ?? "";
  if (!title || await dialog.locator(".detail-card-back").count() > 0) {
    throw new Error(`Visible card detail must show its official front: ${JSON.stringify({ cardLabel, title })}`);
  }

  await dialog.locator(".detail-drawer").getByRole("button", { exact: true, name: "关闭" }).click();
  await dialog.waitFor({ state: "hidden", timeout: 5_000 });
  report.interactions.push({
    card: cardLabel,
    name: "playable-card-inspect",
    result: "opened-and-closed",
    title
  });
  console.log(`QA interaction OK: playable-card-inspect (${title})`);
}

async function runArenaDirectSelectionInteraction(page, report) {
  await page.goto(`${frontendUrl}/matches/qa-layout?fixture=layout`, { waitUntil: "networkidle" });
  const arena = page.locator("[data-arena-table]");
  await arena.waitFor({ state: "visible", timeout: 10_000 });

  const source = page.locator('[data-arena-battlefield-region] [data-object-id="p1-right-1"]');
  const position = page.locator('[data-arena-battlefield-region] [data-object-id="fixture-right-battlefield"]');
  const target = page.locator('[data-arena-battlefield-region] [data-object-id="p2-right-1"]');
  await source.click();
  await page.waitForFunction(() => document.querySelector('[data-arena-battlefield-region] [data-object-id="p1-right-1"]')?.classList.contains("is-selected"));

  const targetOccluded = await page.evaluate(() => {
    const layer = document.querySelector("[data-arena-action-mode]")?.getBoundingClientRect();
    const targetCard = document.querySelector('[data-arena-battlefield-region] [data-object-id="p2-right-1"]')?.getBoundingClientRect();
    return Boolean(layer && targetCard
      && targetCard.left < layer.right
      && targetCard.right > layer.left
      && targetCard.top < layer.bottom
      && targetCard.bottom > layer.top);
  });
  if (targetOccluded) {
    throw new Error("Arena context actions must not cover the next legal target.");
  }

  await position.click();
  await target.click();
  await page.waitForFunction(() => (
    document.querySelector("[data-wire-object-route-review-state]")?.getAttribute("data-wire-object-route-review-state") === "ready"
    && document.querySelector("[data-wire-object-route-review-submit-state]")?.getAttribute("data-wire-object-route-review-submit-state") === "ready"
  ));

  const chosenObjectIds = await page.evaluate(() => Array.from(document.querySelectorAll("[data-object-id].is-prompt-chosen"))
    .map((element) => element.getAttribute("data-object-id"))
    .filter(Boolean));
  for (const objectId of ["fixture-right-battlefield", "p2-right-1"]) {
    if (!chosenObjectIds.includes(objectId)) {
      throw new Error(`Arena direct selection did not retain ${objectId}: ${JSON.stringify(chosenObjectIds)}`);
    }
  }

  await page.keyboard.press("Escape");
  await page.waitForFunction(() => (
    document.querySelectorAll('[data-wire-object-command-tray-presentation="arena"]').length === 0
    && document.querySelectorAll("[data-object-id].is-selected").length === 0
    && document.querySelectorAll("[data-object-id].is-prompt-chosen").length === 0
  ));

  report.interactions.push({
    chosenObjectIds: [...new Set(chosenObjectIds)].sort(),
    name: "arena-direct-selection",
    result: "source-position-target-ready-and-escape-cleared",
    targetOccluded
  });
  console.log("QA interaction OK: arena-direct-selection");
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
    P1: createSignalRClient("P1", roomId, qaPlayerKeys.P1),
    P2: createSignalRClient("P2", roomId, qaPlayerKeys.P2)
  };
  await Promise.all([clients.P1.connection.start(), clients.P2.connection.start()]);
  await Promise.all([
    clients.P1.connection.invoke("Authenticate", "P1", qaPlayerKeys.P1),
    clients.P2.connection.invoke("Authenticate", "P2", qaPlayerKeys.P2)
  ]);
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
    P1: createSignalRClient("P1", roomId, qaPlayerKeys.P1),
    P2: createSignalRClient("P2", roomId, qaPlayerKeys.P2)
  };
  await Promise.all([clients.P1.connection.start(), clients.P2.connection.start()]);
  await Promise.all([
    clients.P1.connection.invoke("Authenticate", "P1", qaPlayerKeys.P1),
    clients.P2.connection.invoke("Authenticate", "P2", qaPlayerKeys.P2)
  ]);
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

function createSignalRClient(playerId, roomId, playerKey) {
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

  return { playerId, playerKey, roomId, connection, state };
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
    || text.includes("net::ERR_PROXY_CONNECTION_FAILED")
    || text.includes("Failed to load resource: the server responded with a status of 404");
}

function delay(ms) {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

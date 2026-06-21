import { spawn } from "node:child_process";
import { existsSync } from "node:fs";
import { mkdir, readFile, rm, writeFile } from "node:fs/promises";
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
const outputRoot = path.resolve(appRoot, "artifacts");
const appshotDir = path.join(outputRoot, "appshots");
const baselineDir = path.join(outputRoot, "baselines");
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
  { name: "decks", path: "/decks", texts: ["本地测试卡组", "等待服务端验证"] },
  { name: "room", path: "/rooms/qa-visual-room", texts: ["房间", "连接/重连并入座", "选择卡组"] }
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

const children = [];
let browser;

try {
  await mkdir(appshotDir, { recursive: true });
  await mkdir(baselineDir, { recursive: true });
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
    interactions: [],
    updateBaseline,
    shots: []
  };

  for (const shot of staticShots) {
    const page = await newPage();
    await page.goto(`${frontendUrl}${shot.path}`, { waitUntil: "networkidle" });
    await assertTexts(page, shot.texts);
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
  await expectNoHiddenDebugText(page);
  const screenshotPath = path.join(appshotDir, `${shot.name}.png`);
  const buffer = await page.screenshot({ fullPage: false, path: screenshotPath });
  assertNonBlank(buffer, shot.name);
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

async function assertServerFlow(page) {
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

async function expectNoHiddenDebugText(page) {
  const bodyText = await page.locator("body").innerText();
  const leaked = hiddenDebugTexts.filter((text) => bodyText.includes(text));
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
    await assertTexts(page, ["提交反馈", "尚未提交"]);
    const initialState = await page.locator("[data-command-submission-state]").first().getAttribute("data-command-submission-state");
    if (initialState !== "empty") {
      throw new Error(`Command submission feedback should start empty, got ${initialState}`);
    }

    const route = await submitReceiptProbeCommand(page);
    const receipt = await waitForAcceptedSubmissionFeedback(page, "END_TURN");
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

async function waitForAcceptedSubmissionFeedback(page, cmdType) {
  await page.waitForFunction((expectedCmdType) => {
    const feedback = document.querySelector("[data-command-submission-state]");
    const followupState = feedback?.querySelector("[data-command-followup-state]")?.getAttribute("data-command-followup-state") ?? "";
    const commandCenter = document.querySelector(".wire-command-center");
    const commandCenterFollowup = commandCenter?.querySelector("[data-command-followup-state]");
    const commandCenterFollowupState = commandCenterFollowup?.getAttribute("data-command-followup-state") ?? "";
    const commandCenterServerState = commandCenterFollowup?.getAttribute("data-command-followup-server-state") ?? "";
    const text = feedback?.textContent ?? "";
    const acceptedFollowupStates = ["accepted-events", "accepted-snapshot"];
    const acceptedServerStates = ["events", "snapshot-prompt"];
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
  const feedbackPanel = page.locator("[data-command-submission-state]").first();
  const openLayerButton = feedbackPanel.getByRole("button", { name: "打开回执检查层" });
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
  if (!["accepted-events", "accepted-snapshot"].includes(layerResult.followupState)) failures.push(`followup=${layerResult.followupState}`);
  if (!["events", "snapshot-prompt"].includes(layerResult.serverState)) failures.push(`server=${layerResult.serverState}`);
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
      ASPNETCORE_URLS: serverUrl
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

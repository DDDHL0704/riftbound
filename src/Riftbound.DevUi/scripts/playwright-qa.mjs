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
    texts: ["正式桌面状态", "服务端行动提示", "中央战场"]
  },
  {
    name: "prompt-pay-cost",
    scenario: "pay-cost-window",
    playerId: "P1",
    texts: ["正式桌面状态", "支付费用", "服务端行动提示"]
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
      await captureAndAudit(page, shot, report);
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
  await page.getByRole("button", { name: "连接/重连", exact: true }).click();
  await page.waitForFunction((expectedPlayerId) => document.body.textContent?.includes(expectedPlayerId), playerId, { timeout: 15_000 });
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

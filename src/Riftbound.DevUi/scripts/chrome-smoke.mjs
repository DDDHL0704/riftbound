import { spawn } from "node:child_process";
import { existsSync } from "node:fs";
import { mkdtemp, rm } from "node:fs/promises";
import { tmpdir } from "node:os";
import path from "node:path";
import { fileURLToPath } from "node:url";

const scriptDir = path.dirname(fileURLToPath(import.meta.url));
const appRoot = path.resolve(scriptDir, "..");
const repoRoot = path.resolve(scriptDir, "../../..");
const frontendPort = Number(process.env.RIFTBOUND_SMOKE_FRONTEND_PORT ?? 5173);
const debugPort = Number(process.env.RIFTBOUND_SMOKE_CHROME_DEBUG_PORT ?? 9338);
const serverUrl = process.env.RIFTBOUND_SERVER_URL ?? "http://127.0.0.1:5088";
const frontendUrl = `http://127.0.0.1:${frontendPort}`;
const startApi = process.argv.includes("--start-api");

const routes = [
  { path: "/", texts: ["符文战场", "进入大厅"] },
  { path: "/lobby", texts: ["创建或加入", "玩家名称", "房间码"] },
  { path: "/decks", texts: ["本地测试卡组", "等待服务端验证"] },
  { path: "/cards", texts: ["卡牌图鉴", "官方卡牌视图"] },
  { path: "/rooms/stage3-smoke", texts: ["房间", "连接/重连并入座", "选择卡组"] },
  {
    path: "/matches/stage3-smoke",
    texts: ["符文战场对战线框", "等待开局", "合法操作地图", "交互语法", "焦点 / 候选 / 规则队列", "服务端行动提示", "结算链 / 规则事件", "日志"],
    absentTexts: ["mainDeck", "runeDeck", "handHidden", "stackItemId", "reconnectToken", "battleState", "damageLedger", "participantControllerIds", "serverPaymentState", "resourceLedgerBeforePayment", "triggerQueue", "handChoices", "legalObjectIds", "serverHandChoiceState"]
  },
  { path: "/matches/stage3-smoke/result", texts: ["结算", "结果只读取服务端权威快照"] }
];

const children = [];
let userDataDir;

try {
  if (startApi) {
    await ensureApi();
  }

  if (await isHttpOk(`${frontendUrl}/`)) {
    console.log(`Frontend already available at ${frontendUrl}`);
  } else {
    const preview = spawnChild(viteBin(), ["preview", "--host", "127.0.0.1", "--port", String(frontendPort), "--strictPort"], {
      cwd: appRoot,
      name: "vite-preview"
    });
    children.push(preview);
    await waitForHttp(`${frontendUrl}/`, 30_000);
  }

  userDataDir = await mkdtemp(path.join(tmpdir(), "riftbound-chrome-smoke-"));
  const chrome = spawnChild(chromePath(), [
    "--headless=new",
    "--disable-gpu",
    "--no-first-run",
    "--no-default-browser-check",
    `--remote-debugging-port=${debugPort}`,
    `--user-data-dir=${userDataDir}`,
    "about:blank"
  ], { name: "chrome" });
  children.push(chrome);
  await waitForHttp(`http://127.0.0.1:${debugPort}/json/version`, 15_000);

  const tab = await openChromeTab(`${frontendUrl}/`);
  const cdp = await connectCdp(tab.webSocketDebuggerUrl);
  const browserErrors = [];
  cdp.onEvent((message) => {
    if (message.method === "Runtime.exceptionThrown") {
      browserErrors.push(`exception: ${message.params?.exceptionDetails?.text ?? "unknown"}`);
    }

    if (message.method === "Runtime.consoleAPICalled" && message.params?.type === "error") {
      const text = consoleArgs(message.params.args);
      if (!isIgnorableConsoleError(text)) {
        browserErrors.push(`console.error: ${text}`);
      }
    }

    if (message.method === "Log.entryAdded" && message.params?.entry?.level === "error") {
      const text = String(message.params.entry.text ?? "");
      if (!text.includes(serverUrl) && !isIgnorableResourceLog(text)) {
        browserErrors.push(`log.error: ${text}`);
      }
    }
  });

  await cdp.send("Page.enable");
  await cdp.send("Runtime.enable");
  await cdp.send("Log.enable");

  for (const route of routes) {
    await cdp.send("Page.navigate", { url: `${frontendUrl}${route.path}` });
    await waitForText(cdp, route.texts);
    await expectAbsentText(cdp, route.absentTexts ?? []);
    console.log(`Chrome smoke OK: ${route.path}`);
  }

  await cdp.send("Page.navigate", { url: `${frontendUrl}/matches/local?fixture=layout` });
  await waitForText(cdp, ["符文战场对战线框", "合法操作地图", "焦点 / 候选 / 规则队列"]);
  await runWireClickSelectionSmoke(cdp);
  console.log("Chrome smoke OK: wire click selection");
  await runWireRuleObjectRefSmoke(cdp);
  console.log("Chrome smoke OK: wire rule object refs");

  if (browserErrors.length > 0) {
    throw new Error(`Chrome reported errors:\n${browserErrors.join("\n")}`);
  }

  await cdp.close();
  console.log("Chrome smoke passed.");
} finally {
  for (const child of children.reverse()) {
    child.kill("SIGTERM");
  }

  if (userDataDir) {
    await rm(userDataDir, { force: true, maxRetries: 5, recursive: true, retryDelay: 120 });
  }
}

async function ensureApi() {
  if (await isHttpOk(`${serverUrl}/health`)) {
    console.log(`API already available at ${serverUrl}`);
    return;
  }

  const api = spawnChild("dotnet", ["run", "--project", "src/Riftbound.Api/Riftbound.Api.csproj", "--no-launch-profile"], {
    cwd: repoRoot,
    env: { ...process.env, ASPNETCORE_URLS: serverUrl, ASPNETCORE_ENVIRONMENT: "Development" },
    name: "api"
  });
  children.push(api);
  await waitForHttp(`${serverUrl}/health`, 60_000);
}

function spawnChild(command, args, options) {
  const child = spawn(command, args, {
    cwd: options.cwd,
    env: options.env ?? process.env,
    stdio: ["ignore", "pipe", "pipe"]
  });
  child.stdout.on("data", (chunk) => process.stdout.write(`[${options.name}] ${chunk}`));
  child.stderr.on("data", (chunk) => process.stderr.write(`[${options.name}] ${chunk}`));
  child.on("exit", (code, signal) => {
    if (code && code !== 0 && signal !== "SIGTERM") {
      process.stderr.write(`[${options.name}] exited with ${code}\n`);
    }
  });
  return child;
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
    throw new Error("Google Chrome was not found. Set CHROME_PATH to run Chrome smoke.");
  }

  return found;
}

async function openChromeTab(url) {
  const endpoint = `http://127.0.0.1:${debugPort}/json/new?${encodeURIComponent(url)}`;
  let response = await fetch(endpoint, { method: "PUT" });
  if (!response.ok) {
    response = await fetch(endpoint);
  }

  if (!response.ok) {
    throw new Error(`Failed to open Chrome tab: ${response.status} ${response.statusText}`);
  }

  return response.json();
}

async function connectCdp(webSocketDebuggerUrl) {
  const ws = new WebSocket(webSocketDebuggerUrl);
  const pending = new Map();
  const eventHandlers = [];
  let nextId = 1;

  await new Promise((resolve, reject) => {
    ws.addEventListener("open", resolve, { once: true });
    ws.addEventListener("error", reject, { once: true });
  });

  ws.addEventListener("message", (event) => {
    const message = JSON.parse(event.data);
    if (message.id && pending.has(message.id)) {
      const { resolve, reject } = pending.get(message.id);
      pending.delete(message.id);
      if (message.error) {
        reject(new Error(message.error.message));
      } else {
        resolve(message.result);
      }
      return;
    }

    for (const handler of eventHandlers) {
      handler(message);
    }
  });

  return {
    close: () => ws.close(),
    onEvent: (handler) => eventHandlers.push(handler),
    send: (method, params = {}) => new Promise((resolve, reject) => {
      const id = nextId++;
      pending.set(id, { resolve, reject });
      ws.send(JSON.stringify({ id, method, params }));
    })
  };
}

async function waitForText(cdp, texts) {
  const deadline = Date.now() + 10_000;
  let bodyText = "";
  while (Date.now() < deadline) {
    bodyText = await readBodyText(cdp);
    if (texts.every((text) => bodyText.includes(text))) {
      return;
    }
    await delay(250);
  }

  throw new Error(`Missing expected text ${texts.join(", ")} in page body:\n${bodyText.slice(0, 1000)}`);
}

async function expectAbsentText(cdp, texts) {
  if (texts.length === 0) {
    return;
  }

  const bodyText = await readBodyText(cdp);
  const leaked = texts.filter((text) => bodyText.includes(text));
  if (leaked.length > 0) {
    throw new Error(`Unexpected raw debug text on page: ${leaked.join(", ")}`);
  }
}

async function readBodyText(cdp) {
  const result = await cdp.send("Runtime.evaluate", {
    expression: "document.body ? document.body.innerText : ''",
    returnByValue: true
  });
  return String(result.result?.value ?? "");
}

async function runWireClickSelectionSmoke(cdp) {
  await clickObject(cdp, "p1-hand-spell");
  await delay(150);
  await clickObject(cdp, "p2-left-1");
  await delay(150);
  const targetResult = await evaluateJson(cdp, `(() => {
    const attr = (id) => document.querySelector(\`[data-object-id="\${id}"]\`)?.getAttribute("data-prompt-state") ?? null;
    const selected = (id) => document.querySelector(\`[data-object-id="\${id}"]\`)?.getAttribute("data-selected") ?? null;
    const targetSelect = Array.from(document.querySelectorAll(".wire-focused-actions select")).find((select) =>
      Array.from(select.options).some((option) => option.value === "p2-left-1"));
    return {
      sourceSelected: selected("p1-hand-spell"),
      sourceState: attr("p1-hand-spell"),
      chosenTargetState: attr("p2-left-1"),
      otherTargetState: attr("p2-right-1"),
      detailLayerOpen: Boolean(document.querySelector(".detail-layer")),
      draftText: document.querySelector(".wire-selection-draft")?.textContent ?? "",
      targetSelectValue: targetSelect?.value ?? null
    };
  })()`);

  await clickObject(cdp, "p1-rune-2");
  await delay(150);
  const costResult = await evaluateJson(cdp, `(() => {
    const attr = (id) => document.querySelector(\`[data-object-id="\${id}"]\`)?.getAttribute("data-prompt-state") ?? null;
    const checkedCost = Array.from(document.querySelectorAll(".wire-focused-actions input[type='checkbox']"))
      .filter((input) => input.checked)
      .map((input) => input.closest("label")?.textContent?.trim() ?? "");
    return {
      exhaustedRuneState: attr("p1-rune-2"),
      draftText: document.querySelector(".wire-selection-draft")?.textContent ?? "",
      checkedCost
    };
  })()`);

  await clickObject(cdp, "p1-left-2");
  await delay(150);
  await clickObject(cdp, "fixture-right-battlefield");
  await delay(150);
  const destinationResult = await evaluateJson(cdp, `(() => {
    const attr = (id) => document.querySelector(\`[data-object-id="\${id}"]\`)?.getAttribute("data-prompt-state") ?? null;
    const selected = (id) => document.querySelector(\`[data-object-id="\${id}"]\`)?.getAttribute("data-selected") ?? null;
    const destinationSelect = Array.from(document.querySelectorAll(".wire-focused-actions select")).find((select) =>
      Array.from(select.options).some((option) => option.value === "BATTLEFIELD:fixture-right-battlefield"));
    return {
      moveSourceSelected: selected("p1-left-2"),
      moveSourceState: attr("p1-left-2"),
      destinationState: attr("fixture-right-battlefield"),
      destinationSelectValue: destinationSelect?.value ?? null,
      detailLayerOpen: Boolean(document.querySelector(".detail-layer")),
      draftText: document.querySelector(".wire-selection-draft")?.textContent ?? ""
    };
  })()`);

  const failures = [];
  if (targetResult.sourceSelected !== "true") failures.push("source focus was not preserved after target click");
  if (targetResult.sourceState !== "source") failures.push("source state missing after target click");
  if (targetResult.chosenTargetState !== "chosen") failures.push("clicked target not chosen");
  if (targetResult.otherTargetState !== "target") failures.push("other target no longer legal target");
  if (targetResult.detailLayerOpen) failures.push("target click opened detail");
  if (!targetResult.draftText.includes("目标 1")) failures.push("draft target count missing");
  if (targetResult.targetSelectValue !== "p2-left-1") failures.push("composer target select did not follow target click");
  if (costResult.exhaustedRuneState !== "chosen") failures.push("clicked optional cost not chosen");
  if (!costResult.draftText.includes("费用 1")) failures.push("draft cost count missing");
  if (!costResult.checkedCost.some((text) => text.includes("回收已抽出符文"))) failures.push("composer optional cost not checked");
  if (destinationResult.moveSourceSelected !== "true") failures.push("move source focus was not preserved");
  if (destinationResult.moveSourceState !== "source") failures.push("move source state missing");
  if (destinationResult.destinationState !== "chosen") failures.push("destination not chosen");
  if (destinationResult.destinationSelectValue !== "BATTLEFIELD:fixture-right-battlefield") failures.push("composer destination select did not follow click");
  if (destinationResult.detailLayerOpen) failures.push("destination click opened detail");

  if (failures.length > 0) {
    throw new Error(`Wire click selection smoke failed:\n${failures.join("\n")}`);
  }
}

async function runWireRuleObjectRefSmoke(cdp) {
  const initial = await evaluateJson(cdp, `(() => ({
    battlefieldRefs: document.querySelectorAll('[data-rule-object-ref="fixture-left-battlefield"]').length,
    eventRefs: document.querySelectorAll('[data-event-object-ref="p1-hand-spell"]').length,
    unitRefs: document.querySelectorAll('[data-rule-object-ref="p2-right-1"]').length,
    hiddenRefs: document.querySelectorAll('[data-rule-object-ref="HIDDEN"]').length
  }))()`);

  await clickRuleObjectRef(cdp, "fixture-left-battlefield");
  await delay(150);
  const battlefieldResult = await evaluateJson(cdp, `(() => {
    const tableObject = document.querySelector('[data-object-id="fixture-left-battlefield"]');
    const selectedRef = document.querySelector('[data-rule-object-ref="fixture-left-battlefield"][data-selected="true"]');
    return {
      selected: tableObject?.getAttribute("data-selected") ?? null,
      selectedRef: Boolean(selectedRef),
      detailLayerOpen: Boolean(document.querySelector(".detail-layer"))
    };
  })()`);

  await clickRuleObjectRef(cdp, "p2-right-1");
  await delay(150);
  const unitResult = await evaluateJson(cdp, `(() => {
    const tableObject = document.querySelector('[data-object-id="p2-right-1"]');
    const selectedRef = document.querySelector('[data-rule-object-ref="p2-right-1"][data-selected="true"]');
    return {
      selected: tableObject?.getAttribute("data-selected") ?? null,
      selectedRef: Boolean(selectedRef),
      detailLayerOpen: Boolean(document.querySelector(".detail-layer"))
    };
  })()`);

  await clickEventObjectRef(cdp, "p1-hand-spell");
  await delay(150);
  const eventResult = await evaluateJson(cdp, `(() => {
    const tableObject = document.querySelector('[data-object-id="p1-hand-spell"]');
    const selectedRef = document.querySelector('[data-event-object-ref="p1-hand-spell"][data-selected="true"]');
    return {
      selected: tableObject?.getAttribute("data-selected") ?? null,
      selectedRef: Boolean(selectedRef),
      detailLayerOpen: Boolean(document.querySelector(".detail-layer"))
    };
  })()`);

  const failures = [];
  if (initial.battlefieldRefs < 1) failures.push("battlefield rule object ref missing");
  if (initial.eventRefs < 1) failures.push("event object ref missing");
  if (initial.unitRefs < 1) failures.push("unit rule object ref missing");
  if (initial.hiddenRefs < 1) failures.push("hidden rule object ref missing");
  if (battlefieldResult.selected !== "true") failures.push("battlefield ref did not focus battlefield card");
  if (!battlefieldResult.selectedRef) failures.push("battlefield ref did not show selected state");
  if (battlefieldResult.detailLayerOpen) failures.push("battlefield ref opened detail layer");
  if (unitResult.selected !== "true") failures.push("unit ref did not focus unit card");
  if (!unitResult.selectedRef) failures.push("unit ref did not show selected state");
  if (unitResult.detailLayerOpen) failures.push("unit ref opened detail layer");
  if (eventResult.selected !== "true") failures.push("event ref did not focus source card");
  if (!eventResult.selectedRef) failures.push("event ref did not show selected state");
  if (eventResult.detailLayerOpen) failures.push("event ref opened detail layer");

  if (failures.length > 0) {
    throw new Error(`Wire rule object ref smoke failed:\n${failures.join("\n")}`);
  }
}

async function clickObject(cdp, objectId) {
  const result = await cdp.send("Runtime.evaluate", {
    expression: `(() => {
      const element = document.querySelector(${JSON.stringify(`[data-object-id="${objectId}"]`)});
      if (!element) return false;
      element.click();
      return true;
    })()`,
    returnByValue: true
  });
  if (result.result?.value !== true) {
    throw new Error(`Missing clickable object ${objectId}`);
  }
}

async function clickRuleObjectRef(cdp, objectId) {
  const result = await cdp.send("Runtime.evaluate", {
    expression: `(() => {
      const element = document.querySelector(${JSON.stringify(`[data-rule-object-ref="${objectId}"]`)});
      if (!element) return false;
      element.click();
      return true;
    })()`,
    returnByValue: true
  });
  if (!result.result?.value) {
    throw new Error(`Rule object ref not found: ${objectId}`);
  }
}

async function clickEventObjectRef(cdp, objectId) {
  const result = await cdp.send("Runtime.evaluate", {
    expression: `(() => {
      const element = document.querySelector(${JSON.stringify(`[data-event-object-ref="${objectId}"]`)});
      if (!element) return false;
      element.click();
      return true;
    })()`,
    returnByValue: true
  });
  if (!result.result?.value) {
    throw new Error(`Event object ref not found: ${objectId}`);
  }
}

async function evaluateJson(cdp, expression) {
  const result = await cdp.send("Runtime.evaluate", {
    expression,
    returnByValue: true
  });
  return result.result?.value ?? {};
}

async function waitForHttp(url, timeoutMs) {
  const deadline = Date.now() + timeoutMs;
  while (Date.now() < deadline) {
    if (await isHttpOk(url)) {
      return;
    }
    await delay(300);
  }

  throw new Error(`Timed out waiting for ${url}`);
}

async function isHttpOk(url) {
  try {
    const response = await fetch(url);
    return response.ok;
  } catch {
    return false;
  }
}

function consoleArgs(args = []) {
  return args
    .map((arg) => String(arg.value ?? arg.description ?? arg.type ?? "unknown"))
    .join(" ");
}

function isIgnorableResourceLog(text) {
  return text.includes("Failed to load resource: the server responded with a status of 404")
    || (!startApi && text.includes("Failed to load resource: net::ERR_CONNECTION_REFUSED"));
}

function isIgnorableConsoleError(text) {
  return !startApi
    && (
      text.includes("Failed to complete negotiation with the server")
      || text.includes("Failed to start the connection")
    );
}

function delay(ms) {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

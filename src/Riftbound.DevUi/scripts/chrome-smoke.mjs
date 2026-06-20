import { spawn } from "node:child_process";
import { existsSync } from "node:fs";
import { mkdtemp, rm } from "node:fs/promises";
import { tmpdir } from "node:os";
import path from "node:path";
import { fileURLToPath } from "node:url";
import axe from "axe-core";

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
    texts: ["符文战场对战线框", "等待开局", "窗口总览", "优先权轨道", "合法操作地图", "候选步骤", "交互语法", "焦点 / 候选 / 规则队列", "规则队列地图", "服务端行动提示", "结算链 / 规则事件", "日志"],
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
    await navigateAndWait(cdp, `${frontendUrl}${route.path}`);
    await waitForText(cdp, route.texts);
    await expectAbsentText(cdp, route.absentTexts ?? []);
    await runAccessibilitySmoke(cdp, route.path);
    if (route.path === "/rooms/stage3-smoke") {
      await runRoomLifecycleSmoke(cdp);
    }
    console.log(`Chrome smoke OK: ${route.path}`);
  }

  await navigateAndWait(cdp, `${frontendUrl}/matches/local?fixture=layout`);
  await waitForText(cdp, ["符文战场对战线框", "合法操作地图", "焦点 / 候选 / 规则队列"]);
  await runAccessibilitySmoke(cdp, "/matches/local?fixture=layout");
  await runWireLayoutGeometrySmoke(cdp);
  console.log("Chrome smoke OK: wire layout geometry");
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

async function navigateAndWait(cdp, url) {
  await cdp.send("Page.navigate", { url });
  const deadline = Date.now() + 20_000;
  while (Date.now() < deadline) {
    const state = await cdp.send("Runtime.evaluate", {
      expression: `(() => ({
        hasBody: Boolean(document.body),
        readyState: document.readyState,
        rootChildCount: document.getElementById("root")?.childElementCount ?? 0
      }))()`,
      returnByValue: true
    }).then((result) => result.result?.value ?? {}).catch(() => ({}));

    if (state.hasBody && state.readyState !== "loading" && state.rootChildCount > 0) {
      return;
    }
    await delay(250);
  }

  throw new Error(`Timed out waiting for document to become ready: ${url}`);
}

async function waitForText(cdp, texts) {
  const deadline = Date.now() + 20_000;
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

async function runAccessibilitySmoke(cdp, label) {
  await cdp.send("Runtime.evaluate", {
    expression: axe.source,
    awaitPromise: true
  });
  const result = await cdp.send("Runtime.evaluate", {
    expression: `globalThis.axe.run(document, {
      resultTypes: ["violations"],
      rules: {
        "color-contrast": { enabled: true },
        "button-name": { enabled: true },
        "label": { enabled: true }
      }
    })`,
    awaitPromise: true,
    returnByValue: true
  });

  const violations = result.result?.value?.violations ?? [];
  const blocking = violations.filter((violation) =>
    ["critical", "serious"].includes(String(violation.impact ?? ""))
    || ["button-name", "label", "aria-hidden-focus", "nested-interactive"].includes(String(violation.id ?? ""))
  );
  if (blocking.length > 0) {
    const summary = blocking
      .map((violation) => {
        const targets = (violation.nodes ?? [])
          .map((node) => Array.isArray(node.target) ? node.target.join(" ") : String(node.target ?? "unknown"))
          .slice(0, 3)
          .join(", ");
        return `${violation.id}: ${targets}`;
      })
      .join("\n");
    throw new Error(`Accessibility smoke failed for ${label}:\n${summary}`);
  }
}

async function readBodyText(cdp) {
  const result = await cdp.send("Runtime.evaluate", {
    expression: "document.body ? document.body.innerText : ''",
    returnByValue: true
  });
  return String(result.result?.value ?? "");
}

async function runRoomLifecycleSmoke(cdp) {
  const result = await evaluateJson(cdp, `(() => {
    const entries = Array.from(document.querySelectorAll("[data-room-quick-action]")).map((button) => ({
      candidate: button.getAttribute("data-room-quick-action-candidate") ?? "",
      disabled: button.hasAttribute("disabled"),
      id: button.getAttribute("data-room-quick-action") ?? "",
      state: button.getAttribute("data-room-quick-action-state") ?? "",
      text: button.textContent ?? ""
    }));
    return { entries };
  })()`);

  const entries = result.entries ?? [];
  const byId = Object.fromEntries(entries.map((entry) => [entry.id, entry]));
  if (entries.length !== 2) {
    throw new Error(`Room lifecycle smoke expected 2 server quick actions, got ${entries.length}`);
  }
  if (byId.submitDeck?.state !== "missing" || byId.submitDeck?.disabled !== true) {
    throw new Error(`Room submit deck quick action should be missing and disabled: ${JSON.stringify(byId.submitDeck)}`);
  }
  if (byId.ready?.state !== "missing" || byId.ready?.disabled !== true) {
    throw new Error(`Room ready quick action should be missing and disabled: ${JSON.stringify(byId.ready)}`);
  }
}

async function runWireLayoutGeometrySmoke(cdp) {
  const result = await evaluateJson(cdp, `(() => {
    const failures = [];
    const round = (value) => Math.round(value * 10) / 10;
    const rectOf = (element) => {
      const rect = element.getBoundingClientRect();
      return {
        bottom: rect.bottom,
        height: rect.height,
        left: rect.left,
        right: rect.right,
        top: rect.top,
        width: rect.width
      };
    };
    const sizeKey = (rect) => \`\${round(rect.width)}x\${round(rect.height)}\`;
    const childCards = (element) => Array.from(element.querySelectorAll(":scope > .card-face, :scope > .card-image-only, :scope > .card-back, :scope > .wire-card-slot"));
    const flowGroups = new Map();

    for (const flow of Array.from(document.querySelectorAll(".wire-card-flow"))) {
      const capacity = flow.getAttribute("data-flow-capacity") ?? "";
      const cardHeight = Number(flow.getAttribute("data-flow-card-height") ?? "0");
      const cardWidth = Number(flow.getAttribute("data-flow-card-width") ?? "0");
      const density = flow.getAttribute("data-flow-density") ?? "";
      const fit = flow.getAttribute("data-flow-fit") ?? "";
      const kind = flow.getAttribute("data-flow-kind") ?? "unknown";
      const layout = flow.getAttribute("data-flow-layout") ?? "";
      const count = Number(flow.getAttribute("data-flow-count") ?? "0");
      const overflow = flow.getAttribute("data-flow-overflow") ?? "";
      const overflowCount = Number(flow.getAttribute("data-flow-overflow-count") ?? "-1");
      const scrollAfter = Number(flow.getAttribute("data-flow-scroll-after") ?? "0");
      const slots = Number(flow.getAttribute("data-flow-slots") ?? "0");
      const visibleSlots = Number(flow.getAttribute("data-flow-visible-slots") ?? "0");
      const cards = childCards(flow);
      if (!["single", "sparse", "normal", "dense", "packed"].includes(density)) {
        failures.push(\`flow \${kind} missing density metadata\`);
      }
      if (!["fixed-slot", "elastic-rail", "overflow-rail"].includes(fit)) {
        failures.push(\`flow \${kind} missing fit metadata\`);
      }
      if (!["grid", "rail"].includes(layout)) {
        failures.push(\`flow \${kind} missing layout metadata\`);
      }
      if (!["none", "scroll"].includes(overflow)) {
        failures.push(\`flow \${kind} missing overflow metadata\`);
      }
      if (kind === "signature" && capacity !== "1") {
        failures.push(\`signature flow capacity should be 1, got \${capacity}\`);
      }
      if (kind !== "signature" && capacity !== "unbounded") {
        failures.push(\`unbounded flow \${kind} declared capacity \${capacity}\`);
      }
      if (visibleSlots > slots) {
        failures.push(\`flow \${kind} visible slots exceed rendered slots: \${visibleSlots} > \${slots}\`);
      }
      if (visibleSlots > scrollAfter) {
        failures.push(\`flow \${kind} visible slots exceed scroll threshold: \${visibleSlots} > \${scrollAfter}\`);
      }
      if (overflowCount !== Math.max(0, slots - visibleSlots)) {
        failures.push(\`flow \${kind} overflow count mismatch: \${overflowCount} for \${slots} slots / \${visibleSlots} visible\`);
      }
      if (overflowCount > 0 && (overflow !== "scroll" || fit !== "overflow-rail")) {
        failures.push(\`flow \${kind} overflow is not represented as scroll overflow fit\`);
      }
      if (overflowCount === 0 && overflow !== "none") {
        failures.push(\`flow \${kind} declares overflow without overflowing slots\`);
      }
      if (cardWidth <= 0 || cardHeight <= 0) {
        failures.push(\`flow \${kind} has non-positive plan card size\`);
      }
      if (slots < count) {
        failures.push(\`flow \${kind} has fewer slots than cards: \${slots} < \${count}\`);
      }
      if (cards.length < count) {
        failures.push(\`flow \${kind} rendered fewer card/slot elements than count: \${cards.length} < \${count}\`);
      }

      const firstCard = cards[0];
      if (!firstCard) {
        continue;
      }

      const rect = rectOf(firstCard);
      if (rect.width <= 0 || rect.height <= 0) {
        failures.push(\`flow \${kind} has non-positive card rect\`);
      }
      if (Math.abs(rect.width - cardWidth) > 1 || Math.abs(rect.height - cardHeight) > 1) {
        failures.push(\`flow \${kind} DOM card size drifted from plan: dom \${sizeKey(rect)} plan \${cardWidth}x\${cardHeight}\`);
      }
      if (Math.abs((rect.width / rect.height) - (744 / 1039)) > 0.04 && kind !== "battlefield-unit") {
        failures.push(\`flow \${kind} card ratio drifted: \${sizeKey(rect)}\`);
      }

      const groupKey = \`\${kind}:\${count}\`;
      const group = flowGroups.get(groupKey) ?? new Set();
      group.add(sizeKey(rect));
      flowGroups.set(groupKey, group);
    }

    for (const [groupKey, sizes] of flowGroups.entries()) {
      if (sizes.size > 1) {
        failures.push(\`matching flow group \${groupKey} produced inconsistent card sizes: \${Array.from(sizes).join(", ")}\`);
      }
    }

    for (const pile of Array.from(document.querySelectorAll("[data-wire-pile-kind]"))) {
      const capacity = pile.getAttribute("data-wire-pile-capacity") ?? "";
      const count = Number(pile.getAttribute("data-wire-pile-count") ?? "-1");
      const face = pile.getAttribute("data-wire-pile-face") ?? "";
      const kind = pile.getAttribute("data-wire-pile-kind") ?? "";
      const overflowCount = Number(pile.getAttribute("data-wire-pile-overflow-count") ?? "-1");
      const topObjectId = pile.getAttribute("data-wire-pile-top-object-id") ?? "";
      const visibleCount = Number(pile.getAttribute("data-wire-pile-visible-count") ?? "-1");
      if (!["banished", "graveyard", "library", "runeDeck"].includes(kind)) {
        failures.push(\`pile has unsupported kind metadata: \${kind}\`);
      }
      if (capacity !== "unbounded") {
        failures.push(\`pile \${kind} must be unbounded, got \${capacity}\`);
      }
      if (count < 0) {
        failures.push(\`pile \${kind} has negative count metadata\`);
      }
      if (![0, 1].includes(visibleCount)) {
        failures.push(\`pile \${kind} visible count should be 0 or 1, got \${visibleCount}\`);
      }
      if (overflowCount !== Math.max(0, count - visibleCount)) {
        failures.push(\`pile \${kind} overflow mismatch: count \${count}, visible \${visibleCount}, overflow \${overflowCount}\`);
      }
      if (face === "public-top" && (!topObjectId || visibleCount !== 1)) {
        failures.push(\`public pile \${kind} must expose exactly one top object id\`);
      }
      if ((face === "hidden-stack" || face === "empty") && topObjectId) {
        failures.push(\`non-public pile \${kind} leaked top object id \${topObjectId}\`);
      }
      if (!["empty", "hidden-stack", "public-top"].includes(face)) {
        failures.push(\`pile \${kind} has unsupported face metadata: \${face}\`);
      }
    }

    for (const home of Array.from(document.querySelectorAll(".wire-player-home"))) {
      const source = home.getAttribute("data-wire-base-partition-source") ?? "";
      if (source !== "server") {
        failures.push(\`wire player home should use server base partition source, got \${source || "missing"}\`);
      }
    }

    for (const unitZone of Array.from(document.querySelectorAll(".wire-battlefield-unit-zone"))) {
      const source = unitZone.getAttribute("data-wire-battlefield-split-source") ?? "";
      if (source !== "server-unitsBySide") {
        failures.push(\`wire battlefield unit zone should use server unitsBySide source, got \${source || "missing"}\`);
      }
    }

    const tableAuthority = document.querySelector("[data-wire-table-authority-state]");
    const tableAuthorityState = tableAuthority?.getAttribute("data-wire-table-authority-state") ?? "missing";
    if (tableAuthorityState !== "server") {
      failures.push(\`wire table authority should be server-authored, got \${tableAuthorityState}\`);
    }
    if (document.querySelectorAll("[data-wire-table-authority-player-source='server']").length < 2) {
      failures.push("wire table authority did not expose two server-authored player base partitions");
    }
    if (document.querySelectorAll("[data-wire-table-authority-lane-source='server-unitsBySide']").length < 2) {
      failures.push("wire table authority did not expose two server-authored battlefield lane splits");
    }

    const informationBoundary = document.querySelector("[data-wire-information-boundary-state]");
    const informationBoundaryState = informationBoundary?.getAttribute("data-wire-information-boundary-state") ?? "missing";
    if (informationBoundaryState !== "safe") {
      failures.push(\`wire information boundary should be safe for fixture view, got \${informationBoundaryState}\`);
    }
    const informationBoundaryRows = new Map(Array.from(document.querySelectorAll("[data-wire-information-boundary-row]")).map((row) => [
      row.getAttribute("data-wire-information-boundary-row") ?? "",
      row.getAttribute("data-wire-information-boundary-row-state") ?? ""
    ]));
    const informationBoundaryRowEntries = Array.from(informationBoundaryRows.entries());
    for (const [rowKey, rowState] of informationBoundaryRowEntries) {
      if (rowState !== "safe") {
        failures.push(\`wire information boundary row \${rowKey} is not safe: \${rowState}\`);
      }
    }
    if (informationBoundaryRowEntries.filter(([rowKey]) => rowKey.startsWith("hand:")).length < 2) {
      failures.push("wire information boundary did not expose both player hand rows");
    }
    if (informationBoundaryRowEntries.filter(([rowKey]) => rowKey.startsWith("deck:")).length < 2) {
      failures.push("wire information boundary did not expose both player deck rows");
    }
    for (const rowKey of ["faceDown", "eventRefs"]) {
      if (informationBoundaryRows.get(rowKey) !== "safe") {
        failures.push(\`wire information boundary row \${rowKey} is not safe: \${informationBoundaryRows.get(rowKey) ?? "missing"}\`);
      }
    }
    if (document.querySelectorAll("[data-wire-information-boundary-metric]").length < 6) {
      failures.push("wire information boundary did not expose the expected metric strip");
    }

    for (const pile of Array.from(document.querySelectorAll(".wire-fixed-pile"))) {
      const pileRect = rectOf(pile);
      const child = pile.querySelector(":scope > .card-face, :scope > .card-image-only, :scope > .card-back, :scope > .wire-stack-box");
      if (!child) {
        failures.push("fixed pile missing card or stack box child");
        continue;
      }

      const childRect = rectOf(child);
      if (Math.abs(childRect.width - pileRect.width) > 1 || Math.abs(childRect.height - pileRect.height) > 1) {
        failures.push(\`fixed pile child does not fill slot: pile \${sizeKey(pileRect)} child \${sizeKey(childRect)}\`);
      }
      if (childRect.left < pileRect.left - 1 || childRect.right > pileRect.right + 1 || childRect.top < pileRect.top - 1 || childRect.bottom > pileRect.bottom + 1) {
        failures.push("fixed pile child escaped slot bounds");
      }
    }

    for (const site of Array.from(document.querySelectorAll(".wire-battlefield-site"))) {
      const siteRect = rectOf(site);
      const card = site.querySelector(".card-battlefield-image, .wire-card-slot");
      if (!card) {
        failures.push("battlefield site missing horizontal card or slot");
        continue;
      }

      const cardRect = rectOf(card);
      if (cardRect.width <= cardRect.height) {
        failures.push(\`battlefield site card is not horizontal: \${sizeKey(cardRect)}\`);
      }
      if (cardRect.width < siteRect.width * 0.9 || cardRect.height < siteRect.height * 0.9) {
        failures.push(\`battlefield site card does not fill slot enough: site \${sizeKey(siteRect)} card \${sizeKey(cardRect)}\`);
      }
    }

    const quickActions = new Map(Array.from(document.querySelectorAll("[data-topbar-quick-action]")).map((button) => [
      button.getAttribute("data-topbar-quick-action"),
      {
        candidate: button.getAttribute("data-topbar-quick-action-candidate") ?? "",
        disabled: button.hasAttribute("disabled"),
        state: button.getAttribute("data-topbar-quick-action-state") ?? ""
      }
    ]));
    const passAction = quickActions.get("pass");
    const endTurnAction = quickActions.get("endTurn");
    if (!passAction || passAction.state !== "ready" || passAction.disabled || passAction.candidate !== "PASS") {
      failures.push(\`topbar pass quick action did not bind to server PASS candidate: \${JSON.stringify(passAction)}\`);
    }
    if (!endTurnAction || endTurnAction.state !== "missing" || !endTurnAction.disabled) {
      failures.push(\`topbar end turn quick action should be missing without a server candidate: \${JSON.stringify(endTurnAction)}\`);
    }

    const submissionGate = document.querySelector("[data-action-submission-gate-state]");
    const submissionGateState = submissionGate?.getAttribute("data-action-submission-gate-state") ?? "missing";
    if (submissionGateState !== "connected") {
      failures.push(\`action map submission gate not connected: \${submissionGateState}\`);
    }

    const promptAuthority = document.querySelector("[data-wire-prompt-authority-state]");
    const promptAuthorityState = promptAuthority?.getAttribute("data-wire-prompt-authority-state") ?? "missing";
    if (promptAuthorityState !== "server") {
      failures.push(\`wire prompt authority should be server-authored, got \${promptAuthorityState}\`);
    }
    const promptAuthorityRows = new Map(Array.from(document.querySelectorAll("[data-wire-prompt-authority-row]")).map((row) => [
      row.getAttribute("data-wire-prompt-authority-row") ?? "",
      row.getAttribute("data-wire-prompt-authority-row-state") ?? ""
    ]));
    for (const rowKey of ["candidates", "commandTemplates", "objectContexts", "contract", "submissionGate"]) {
      if (promptAuthorityRows.get(rowKey) !== "server") {
        failures.push(\`wire prompt authority row \${rowKey} is not server-authored: \${promptAuthorityRows.get(rowKey) ?? "missing"}\`);
      }
    }

    const responseCoach = document.querySelector("[data-wire-response-coach-state]");
    const responseCoachState = responseCoach?.getAttribute("data-wire-response-coach-state") ?? "missing";
    const responseCoachStepRole = responseCoach?.getAttribute("data-wire-response-coach-step-role") ?? "missing";
    if (!["blocked", "opponent", "ready", "resolving", "selecting", "waiting"].includes(responseCoachState)) {
      failures.push(\`wire response coach state is unsupported: \${responseCoachState}\`);
    }
    if (!["destination", "mode", "optionalCost", "source", "submit", "sync", "target", "wait", "window"].includes(responseCoachStepRole)) {
      failures.push(\`wire response coach step role is unsupported: \${responseCoachStepRole}\`);
    }
    const responseCoachRows = new Map(Array.from(document.querySelectorAll("[data-wire-response-coach-row]")).map((row) => [
      row.getAttribute("data-wire-response-coach-row") ?? "",
      row.getAttribute("data-wire-response-coach-row-state") ?? ""
    ]));
    for (const rowKey of ["gate", "window", "prompt", "draft", "route", "submit"]) {
      if (!responseCoachRows.get(rowKey)) {
        failures.push(\`wire response coach row \${rowKey} is missing\`);
      }
    }
    if (document.querySelectorAll("[data-wire-response-coach-metric]").length < 4) {
      failures.push("wire response coach did not expose the expected metric strip");
    }

    const ruleAuthority = document.querySelector("[data-wire-rule-authority-state]");
    const ruleAuthorityState = ruleAuthority?.getAttribute("data-wire-rule-authority-state") ?? "missing";
    if (ruleAuthorityState !== "server") {
      failures.push(\`wire rule authority should be server-authored, got \${ruleAuthorityState}\`);
    }
    const ruleAuthorityRows = new Map(Array.from(document.querySelectorAll("[data-wire-rule-authority-row]")).map((row) => [
      row.getAttribute("data-wire-rule-authority-row") ?? "",
      row.getAttribute("data-wire-rule-authority-row-state") ?? ""
    ]));
    for (const rowKey of ["stack", "task", "trigger", "resolution", "eventRefs"]) {
      if (ruleAuthorityRows.get(rowKey) !== "server") {
        failures.push(\`wire rule authority row \${rowKey} is not server-authored: \${ruleAuthorityRows.get(rowKey) ?? "missing"}\`);
      }
    }

    return {
      failures,
      fixedPileCount: document.querySelectorAll(".wire-fixed-pile").length,
      flowCount: document.querySelectorAll(".wire-card-flow").length,
      informationBoundaryState,
      promptAuthorityState,
      quickActionCount: quickActions.size,
      responseCoachState,
      ruleAuthorityState,
      siteCount: document.querySelectorAll(".wire-battlefield-site").length,
      tableAuthorityState
    };
  })()`);

  const failures = result.failures ?? [];
  if (failures.length > 0) {
    throw new Error(`Wire layout geometry smoke failed:\n${failures.join("\n")}`);
  }

  if ((result.flowCount ?? 0) < 1) {
    throw new Error("Wire layout geometry smoke did not find card flows");
  }
  if ((result.fixedPileCount ?? 0) < 1) {
    throw new Error("Wire layout geometry smoke did not find fixed piles");
  }
  if ((result.siteCount ?? 0) < 2) {
    throw new Error("Wire layout geometry smoke did not find battlefield sites");
  }
  if ((result.quickActionCount ?? 0) < 5) {
    throw new Error("Wire layout geometry smoke did not find topbar quick actions");
  }
  if (result.tableAuthorityState !== "server") {
    throw new Error(`Wire layout geometry smoke did not find server table authority: ${result.tableAuthorityState}`);
  }
  if (result.informationBoundaryState !== "safe") {
    throw new Error(`Wire layout geometry smoke did not find safe information boundary: ${result.informationBoundaryState}`);
  }
  if (result.promptAuthorityState !== "server") {
    throw new Error(`Wire layout geometry smoke did not find server prompt authority: ${result.promptAuthorityState}`);
  }
  if (!["blocked", "opponent", "ready", "resolving", "selecting", "waiting"].includes(result.responseCoachState)) {
    throw new Error(`Wire layout geometry smoke did not find response coach: ${result.responseCoachState}`);
  }
  if (result.ruleAuthorityState !== "server") {
    throw new Error(`Wire layout geometry smoke did not find server rule authority: ${result.ruleAuthorityState}`);
  }
}

async function runWireClickSelectionSmoke(cdp) {
  await hoverObject(cdp, "p1-hand-spell");
  await delay(300);
  const earlyPreviewResult = await readWireCardPreview(cdp);
  await delay(450);
  const standardPreviewResult = await readWireCardPreview(cdp);
  await unhoverObject(cdp, "p1-hand-spell");
  await delay(100);
  const clearedPreviewResult = await readWireCardPreview(cdp);

  await hoverObject(cdp, "fixture-left-battlefield");
  await delay(720);
  const battlefieldPreviewResult = await readWireCardPreview(cdp);
  await unhoverObject(cdp, "fixture-left-battlefield");
  await delay(100);

  await clickObject(cdp, "p1-hand-spell");
  await delay(150);
  const focusResult = await evaluateJson(cdp, `(() => {
    const summary = document.querySelector(".wire-focused-action-summary");
    const readiness = document.querySelector(".wire-focused-readiness");
    return {
      state: summary?.getAttribute("data-wire-focused-action-state") ?? null,
      text: summary?.textContent ?? "",
      contextText: document.querySelector(".wire-object-context")?.textContent ?? "",
      grammarState: document.querySelector(".wire-focused-grammar")?.getAttribute("data-wire-focused-grammar-state") ?? null,
      grammarText: document.querySelector(".wire-focused-grammar")?.textContent ?? "",
      grammarRoles: Array.from(document.querySelectorAll("[data-wire-grammar-role]")).map((node) => node.getAttribute("data-wire-grammar-role")),
      readinessCanSubmit: readiness?.getAttribute("data-wire-focused-readiness-can-submit") ?? null,
      readinessCommand: readiness?.getAttribute("data-wire-focused-readiness-command") ?? null,
      readinessEnabledCount: readiness?.getAttribute("data-wire-focused-readiness-enabled-count") ?? null,
      readinessMissingRequiredCount: readiness?.getAttribute("data-wire-focused-readiness-missing-required-count") ?? null,
      readinessState: readiness?.getAttribute("data-wire-focused-readiness-state") ?? null,
      readinessText: readiness?.textContent ?? "",
      nextStep: document.querySelector("[data-wire-focused-next-step]")?.textContent ?? "",
      candidatePlanCount: document.querySelectorAll(".wire-focused-candidate-plan li").length,
      focusedPathCount: document.querySelectorAll(".wire-focused-path article").length,
      composerCount: document.querySelectorAll(".wire-focused-actions .candidate-composer").length,
      focusedActionButtonCount: document.querySelectorAll(".wire-focused-actions button").length,
      detailLayerOpen: Boolean(document.querySelector(".detail-layer"))
    };
  })()`);
  await focusObject(cdp, "p1-hand-spell");
  await clickButtonByText(cdp, "查看详情");
  await delay(150);
  const detailContextResult = await evaluateJson(cdp, `(() => {
    const detail = document.querySelector(".detail-layer");
    const inspector = detail?.querySelector("[data-card-detail-inspector]");
    const actions = detail?.querySelector("[data-card-detail-actions-state]");
    return {
      actionCount: Number(actions?.querySelector("[data-card-detail-action-count]")?.getAttribute("data-card-detail-action-count") ?? "0"),
      actionModes: Array.from(actions?.querySelectorAll("[data-card-detail-action-mode]") ?? []).map((node) => node.getAttribute("data-card-detail-action-mode")),
      actionSource: actions?.getAttribute("data-card-detail-actions-source") ?? "",
      actionState: actions?.getAttribute("data-card-detail-actions-state") ?? null,
      actionText: actions?.textContent ?? "",
      activeText: document.activeElement?.textContent ?? "",
      groups: Array.from(inspector?.querySelectorAll("[data-card-detail-inspector-group]") ?? []).map((node) => node.getAttribute("data-card-detail-inspector-group")),
      inspectorOpen: Boolean(inspector),
      inspectorText: inspector?.textContent ?? "",
      labelledBy: detail?.getAttribute("aria-labelledby") ?? "",
      state: detail?.getAttribute("data-detail-dialog-state") ?? null,
      summaryKeys: Array.from(inspector?.querySelectorAll("[data-card-detail-inspector-summary]") ?? []).map((node) => node.getAttribute("data-card-detail-inspector-summary")),
      text: detail?.textContent ?? "",
      open: Boolean(detail)
    };
  })()`);
  await pressEscape(cdp);
  await delay(100);
  const detailEscapeResult = await evaluateJson(cdp, `(() => ({
    activeObjectId: document.activeElement?.getAttribute("data-object-id") ?? null,
    open: Boolean(document.querySelector(".detail-layer"))
  }))()`);
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
      grammarState: document.querySelector(".wire-focused-grammar")?.getAttribute("data-wire-focused-grammar-state") ?? null,
      grammarText: document.querySelector(".wire-focused-grammar")?.textContent ?? "",
      previewText: document.querySelector(".candidate-command-preview")?.textContent ?? "",
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
      checkedCost,
      grammarText: document.querySelector(".wire-focused-grammar")?.textContent ?? "",
      previewText: document.querySelector(".candidate-command-preview")?.textContent ?? ""
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

  await clickActionMapObject(cdp, "p1-hand-spell");
  await delay(150);
  const actionMapResult = await evaluateJson(cdp, `(() => {
    const tableObject = document.querySelector('[data-object-id="p1-hand-spell"]');
    const actionChip = document.querySelector('[data-action-object-id="p1-hand-spell"]');
    const candidatePlan = document.querySelector('[data-candidate-plan-action="PLAY_CARD"]');
    const windowPlan = document.querySelector(".wire-window-plan");
    const evidence = document.querySelector("[data-wire-window-evidence]");
    const promptInspection = document.querySelector("[data-wire-prompt-inspection]");
    const priorityRail = document.querySelector(".wire-priority-rail");
    const ruleQueue = document.querySelector(".wire-rule-queue");
    const ruleFocus = document.querySelector(".wire-rule-focus");
    const ruleFlow = document.querySelector(".wire-rule-flow");
    const focusBridge = document.querySelector(".wire-action-focus-bridge");
    const route = document.querySelector("[data-action-route-state]");
    const actionButtons = document.querySelector(".wire-action-panel .action-buttons");
    return {
      selected: tableObject?.getAttribute("data-selected") ?? null,
      actionMapText: document.querySelector(".wire-action-map")?.textContent ?? "",
      actionRenderCount: Number(actionButtons?.getAttribute("data-action-render-count") ?? "0"),
      actionRenderKinds: Array.from(actionButtons?.querySelectorAll("[data-action-render-kind]") ?? [])
        .map((node) => node.getAttribute("data-action-render-kind")),
      actionRenderPromptType: actionButtons?.getAttribute("data-action-render-prompt-type") ?? null,
      actionRenderState: actionButtons?.getAttribute("data-action-render-state") ?? null,
      candidatePlanCount: document.querySelectorAll(".wire-action-candidate-plan-card").length,
      candidatePlanEnabled: candidatePlan?.getAttribute("data-candidate-plan-enabled") ?? null,
      candidatePlanNext: candidatePlan?.querySelector("[data-candidate-plan-next-step]")?.textContent ?? "",
      candidatePlanText: candidatePlan?.textContent ?? "",
      candidateStepRefCount: document.querySelectorAll("[data-action-candidate-step-object-id]").length,
      candidateStepRefText: candidatePlan?.querySelector(".wire-action-candidate-step-ref-list")?.textContent ?? "",
      chipSelected: actionChip?.getAttribute("data-selected") ?? null,
      detailLayerOpen: Boolean(document.querySelector(".detail-layer")),
      focusBridgeState: focusBridge?.getAttribute("data-action-focus-state") ?? null,
      focusBridgeText: focusBridge?.textContent ?? "",
      focusText: document.querySelector(".wire-focused-action-summary")?.textContent ?? "",
      windowState: windowPlan?.getAttribute("data-wire-window-state") ?? null,
      windowText: windowPlan?.textContent ?? "",
      promptInspectionGroups: Array.from(document.querySelectorAll("[data-wire-prompt-inspection-group]")).map((node) => node.getAttribute("data-wire-prompt-inspection-group")),
      promptInspectionSummaryKeys: Array.from(document.querySelectorAll("[data-wire-prompt-inspection-summary]")).map((node) => node.getAttribute("data-wire-prompt-inspection-summary")),
      promptInspectionText: promptInspection?.textContent ?? "",
      evidenceKeys: Array.from(document.querySelectorAll("[data-window-evidence-key]")).map((node) => node.getAttribute("data-window-evidence-key")),
      evidenceStackState: document.querySelector('[data-window-evidence-key="stack"]')?.getAttribute("data-window-evidence-state") ?? null,
      evidenceTaskState: document.querySelector('[data-window-evidence-key="tasks"]')?.getAttribute("data-window-evidence-state") ?? null,
      evidenceText: evidence?.textContent ?? "",
      priorityMode: windowPlan?.getAttribute("data-wire-priority-mode") ?? null,
      priorityRailText: priorityRail?.textContent ?? "",
      priorityActiveStep: document.querySelector('[data-priority-step-state="active"]')?.getAttribute("data-priority-step") ?? null,
      ruleFlowText: ruleFlow?.textContent ?? "",
      ruleFocusDetailId: ruleFocus?.getAttribute("data-rule-focus-detail-id") ?? null,
      ruleFocusLane: ruleFocus?.getAttribute("data-rule-focus-lane") ?? null,
      ruleFocusRefCount: ruleFocus?.querySelectorAll("[data-rule-object-ref]").length ?? 0,
      ruleFocusText: ruleFocus?.textContent ?? "",
      ruleLaneCount: document.querySelectorAll("[data-rule-lane]").length,
      ruleSectionKeys: Array.from(document.querySelectorAll("[data-rule-section-key]")).map((node) => node.getAttribute("data-rule-section-key")),
      ruleItemKeys: Array.from(document.querySelectorAll("[data-rule-item-key]")).map((node) => node.getAttribute("data-rule-item-key")),
      routeState: route?.getAttribute("data-action-route-state") ?? null,
      routeText: route?.textContent ?? "",
      ruleQueueState: ruleQueue?.getAttribute("data-wire-rule-queue-state") ?? null,
      ruleSequenceCount: document.querySelectorAll("[data-rule-sequence-lane]").length
    };
  })()`);

  await clickButtonByText(cdp, "展开规则检查");
  await delay(150);
  const ruleInspectorResult = await evaluateJson(cdp, `(() => {
    const inspector = document.querySelector(".wire-rule-inspector");
    return {
      hidden: inspector?.hasAttribute("hidden") ?? true,
      laneCount: inspector?.querySelectorAll("[data-rule-inspector-lane]").length ?? 0,
      sequenceCount: inspector?.querySelectorAll("[data-rule-inspector-sequence-lane]").length ?? 0,
      text: inspector?.textContent ?? "",
      toggleExpanded: document.querySelector("[data-rule-inspector-toggle]")?.getAttribute("aria-expanded") ?? null
    };
  })()`);

  await clickActionCandidateStepObject(cdp, "p1-hand-spell");
  await delay(150);
  const actionCandidateStepResult = await evaluateJson(cdp, `(() => {
    const sourceObject = document.querySelector('[data-object-id="p1-hand-spell"]');
    return {
      detailLayerOpen: Boolean(document.querySelector(".detail-layer")),
      selected: sourceObject?.getAttribute("data-selected") ?? null,
      selectedContext: Boolean(document.querySelector('[data-wire-selected-object-context="p1-hand-spell"]'))
    };
  })()`);

  await clickActionFocusChoiceObject(cdp, "p2-right-1");
  await delay(150);
  const actionFocusChoiceResult = await evaluateJson(cdp, `(() => {
    const sourceObject = document.querySelector('[data-object-id="p1-hand-spell"]');
    const targetObject = document.querySelector('[data-object-id="p2-right-1"]');
    const candidatePlan = document.querySelector('[data-candidate-plan-action="PLAY_CARD"]');
    const route = document.querySelector("[data-action-route-state]");
    const sourceStep = candidatePlan?.querySelector('[data-step-role="source"]');
    const targetStep = candidatePlan?.querySelector('[data-step-role="target"]');
    const targetRouteStep = route?.querySelector('[data-route-step-role="target"]');
    return {
      candidateDraftActive: candidatePlan?.getAttribute("data-candidate-plan-draft-active") ?? null,
      detailLayerOpen: Boolean(document.querySelector(".detail-layer")),
      draftText: document.querySelector(".wire-selection-draft")?.textContent ?? "",
      previewText: document.querySelector(".candidate-command-preview")?.textContent ?? "",
      routeState: route?.getAttribute("data-action-route-state") ?? null,
      routeText: route?.textContent ?? "",
      routeFieldStates: Array.from(route?.querySelectorAll("[data-route-field-state]") ?? [])
        .map((node) => node.getAttribute("data-route-field-state")),
      sourceSelected: sourceObject?.getAttribute("data-selected") ?? null,
      sourceStepProgress: sourceStep?.getAttribute("data-step-progress") ?? null,
      sourceStepProgressText: sourceStep?.querySelector("[data-step-progress-label]")?.textContent ?? "",
      targetRouteStepState: targetRouteStep?.getAttribute("data-route-step-state") ?? null,
      targetStepProgress: targetStep?.getAttribute("data-step-progress") ?? null,
      targetStepProgressText: targetStep?.querySelector("[data-step-progress-label]")?.textContent ?? "",
      targetState: targetObject?.getAttribute("data-prompt-state") ?? null
    };
  })()`);

  await clickButtonByText(cdp, "展开路线检查");
  await delay(150);
  const routeInspectorResult = await evaluateJson(cdp, `(() => {
    const inspector = document.querySelector(".wire-action-route-inspector");
    return {
      fieldStates: Array.from(inspector?.querySelectorAll("[data-route-inspector-field-state]") ?? [])
        .map((node) => node.getAttribute("data-route-inspector-field-state")),
      hidden: inspector?.hasAttribute("hidden") ?? true,
      stepStates: Array.from(inspector?.querySelectorAll("[data-route-inspector-step-state]") ?? [])
        .map((node) => node.getAttribute("data-route-inspector-step-state")),
      text: inspector?.textContent ?? "",
      toggleExpanded: document.querySelector("[data-action-route-inspector-toggle]")?.getAttribute("aria-expanded") ?? null
    };
  })()`);

  await clickActionMapObject(cdp, "p1-base-equip");
  await delay(150);
  const blockedActionMapResult = await evaluateJson(cdp, `(() => {
    const tableObject = document.querySelector('[data-object-id="p1-base-equip"]');
    const actionChip = document.querySelector('[data-action-object-id="p1-base-equip"]');
    const focusBridge = document.querySelector(".wire-action-focus-bridge");
    return {
      selected: tableObject?.getAttribute("data-selected") ?? null,
      chipSelected: actionChip?.getAttribute("data-selected") ?? null,
      chipState: actionChip?.getAttribute("data-action-object-state") ?? null,
      detailLayerOpen: Boolean(document.querySelector(".detail-layer")),
      focusBridgeState: focusBridge?.getAttribute("data-action-focus-state") ?? null,
      focusBridgeText: focusBridge?.textContent ?? ""
    };
  })()`);

  await clickActionMapObject(cdp, "p1-rune-3");
  await delay(150);
  const runeActionMapResult = await evaluateJson(cdp, `(() => {
    const tableObject = document.querySelector('[data-object-id="p1-rune-3"]');
    const actionChip = document.querySelector('[data-action-object-id="p1-rune-3"]');
    const actionButton = Array.from(document.querySelectorAll(".wire-action-panel button"))
      .find((button) => button.textContent?.includes("横置符文样例"));
    const selectedObjectContext = document.querySelector('[data-wire-selected-object-context="p1-rune-3"]');
    return {
      actionButtonText: actionButton?.textContent ?? "",
      selected: tableObject?.getAttribute("data-selected") ?? null,
      chipSelected: actionChip?.getAttribute("data-selected") ?? null,
      detailContextText: selectedObjectContext?.textContent ?? "",
      focusText: document.querySelector(".wire-focused-action-summary")?.textContent ?? "",
      hasSelectedObjectContext: Boolean(selectedObjectContext)
    };
  })()`);

  await clickCandidateObjectRef(cdp, "p2-right-1");
  await delay(150);
  const candidateRefResult = await evaluateJson(cdp, `(() => {
    const tableObject = document.querySelector('[data-object-id="p2-right-1"]');
    const selectedRef = document.querySelector('[data-candidate-object-ref="p2-right-1"][data-selected="true"]');
    const selectedObjectContext = document.querySelector('[data-wire-selected-object-context="p2-right-1"]');
    return {
      selected: tableObject?.getAttribute("data-selected") ?? null,
      selectedRef: Boolean(selectedRef),
      contextText: document.querySelector(".wire-object-context")?.textContent ?? "",
      detailContextText: selectedObjectContext?.textContent ?? "",
      detailLayerOpen: Boolean(document.querySelector(".detail-layer")),
      hasCandidateRefs: document.querySelectorAll("[data-candidate-object-ref]").length,
      hasSelectedObjectContext: Boolean(selectedObjectContext)
    };
  })()`);

  const failures = [];
  if (earlyPreviewResult.exists) failures.push("card preview appeared before planned delay");
  if (!standardPreviewResult.exists) failures.push("standard card preview did not appear after delay");
  if (standardPreviewResult.kind !== "standard") failures.push(`standard card preview kind unexpected: ${standardPreviewResult.kind}`);
  if (standardPreviewResult.orientation !== "portrait") failures.push(`standard card preview orientation unexpected: ${standardPreviewResult.orientation}`);
  if (standardPreviewResult.objectId !== "p1-hand-spell") failures.push(`standard card preview object id unexpected: ${standardPreviewResult.objectId}`);
  if (standardPreviewResult.delayMs !== 680) failures.push(`standard card preview delay unexpected: ${standardPreviewResult.delayMs}`);
  if (clearedPreviewResult.exists) failures.push("card preview did not clear after hover ended");
  if (!battlefieldPreviewResult.exists) failures.push("battlefield card preview did not appear after delay");
  if (battlefieldPreviewResult.kind !== "battlefield") failures.push(`battlefield card preview kind unexpected: ${battlefieldPreviewResult.kind}`);
  if (battlefieldPreviewResult.orientation !== "landscape-counterclockwise") failures.push(`battlefield card preview orientation unexpected: ${battlefieldPreviewResult.orientation}`);
  if (battlefieldPreviewResult.objectId !== "fixture-left-battlefield") failures.push(`battlefield card preview object id unexpected: ${battlefieldPreviewResult.objectId}`);
  if (focusResult.state !== "server-candidate") failures.push("focused action summary did not use server candidate state");
  if (focusResult.readinessState !== "ready") failures.push(`focused readiness state unexpected: ${focusResult.readinessState}`);
  if (focusResult.readinessCanSubmit !== "true") failures.push("focused readiness did not allow submit");
  if (focusResult.readinessCommand !== "PLAY_CARD") failures.push(`focused readiness command unexpected: ${focusResult.readinessCommand}`);
  if (focusResult.readinessEnabledCount !== "1") failures.push(`focused readiness enabled count unexpected: ${focusResult.readinessEnabledCount}`);
  if (focusResult.readinessMissingRequiredCount !== "0") failures.push(`focused readiness missing count unexpected: ${focusResult.readinessMissingRequiredCount}`);
  if (!focusResult.readinessText.includes("行动状态")) failures.push("focused readiness text missing heading");
  if (!focusResult.text.includes("服务端状态")) failures.push("focused action summary status missing");
  if (!focusResult.text.includes("可提交")) failures.push("focused action summary enabled count missing");
  if (!focusResult.contextText.includes("位置")) failures.push("object context position missing");
  if (!focusResult.contextText.includes("我方手牌")) failures.push("object context did not locate hand source");
  if (!focusResult.contextText.includes("下一步")) failures.push("object context next-step plan missing");
  if (!focusResult.contextText.includes("服务端命令")) failures.push("object context command plan missing");
  if (!focusResult.contextText.includes("服务端索引")) failures.push("object context did not use server object candidate index");
  if (!focusResult.contextText.includes("近期事件")) failures.push("object context event section missing");
  if (focusResult.contextText.includes("serverPaymentState")) failures.push("object context leaked hidden server state");
  if (!detailContextResult.open) failures.push("card detail did not open");
  if (detailContextResult.state !== "open") failures.push("card detail dialog state missing");
  if (detailContextResult.labelledBy !== "card-detail-title") failures.push("card detail dialog label binding missing");
  if (!detailContextResult.activeText.includes("关闭")) failures.push("card detail close button did not receive focus");
  if (!detailContextResult.text.includes("规则上下文")) failures.push("card detail context section missing");
  if (!detailContextResult.text.includes("我方手牌")) failures.push("card detail did not reuse object context location");
  if (!detailContextResult.text.includes("服务端命令")) failures.push("card detail command context missing");
  if (!detailContextResult.text.includes("服务端索引")) failures.push("card detail did not use server object candidate index");
  if (!detailContextResult.text.includes("PLAY_CARD")) failures.push("card detail command type missing");
  if (!detailContextResult.text.includes("来源:sourceObjectId*")) failures.push("card detail command field missing");
  if (!detailContextResult.text.includes("服务端字段")) failures.push("card detail command metadata summary missing");
  if (detailContextResult.text.includes("服务端:cardNo*")) failures.push("card detail leaked raw metadata command field");
  if (detailContextResult.actionState !== "ready") failures.push(`card detail action state unexpected: ${detailContextResult.actionState}`);
  if (detailContextResult.actionSource !== "p1-hand-spell") failures.push("card detail action source binding missing");
  if (detailContextResult.actionCount < 1) failures.push("card detail action entries missing");
  if (!detailContextResult.actionModes.includes("composer")) failures.push("card detail composer action entry missing");
  if (!detailContextResult.actionText.includes("服务端可提交操作")) failures.push("card detail action section text missing");
  if (!detailContextResult.actionText.includes("提交服务端候选")) failures.push("card detail composer submit control missing");
  if (!detailContextResult.inspectorOpen) failures.push("card detail inspector missing");
  if (!detailContextResult.inspectorText.includes("卡牌检查")) failures.push("card detail inspector header missing");
  if (!detailContextResult.inspectorText.includes("服务端只公开")) failures.push("card detail inspector boundary missing");
  if (!detailContextResult.summaryKeys.includes("zone")) failures.push("card detail inspector zone summary missing");
  if (!detailContextResult.summaryKeys.includes("candidate")) failures.push("card detail inspector candidate summary missing");
  if (!detailContextResult.groups.includes("identity")) failures.push("card detail inspector identity group missing");
  if (!detailContextResult.groups.includes("candidate")) failures.push("card detail inspector candidate group missing");
  if (!detailContextResult.groups.includes("events")) failures.push("card detail inspector event group missing");
  if (!detailContextResult.inspectorText.includes("服务端检查摘要")) failures.push("card detail inspector server inspection source missing");
  if (!detailContextResult.inspectorText.includes("前端不重算")) failures.push("card detail inspector safe boundary missing");
  if (!detailContextResult.inspectorText.includes("结算链")) failures.push("card detail inspector stack boundary missing");
  if (detailEscapeResult.open) failures.push("card detail did not close on Escape");
  if (detailEscapeResult.activeObjectId !== "p1-hand-spell") failures.push("card detail did not restore focus to source card");
  if (!focusResult.nextStep.includes("下一步")) failures.push("focused action next step missing");
  if (focusResult.candidatePlanCount < 1) failures.push("focused action candidate plan missing");
  if (focusResult.focusedPathCount < 1) failures.push("focused interaction candidate path missing");
  if (focusResult.composerCount < 1) failures.push("focused interaction composer entry missing");
  if (focusResult.focusedActionButtonCount < 1) failures.push("focused interaction action controls missing");
  if (focusResult.grammarState !== "ready") failures.push(`focused interaction grammar state unexpected: ${focusResult.grammarState}`);
  if (!focusResult.grammarText.includes("交互语法")) failures.push("focused interaction grammar header missing");
  if (!focusResult.grammarText.includes("来源")) failures.push("focused interaction grammar source step missing");
  if (!focusResult.grammarText.includes("提交")) failures.push("focused interaction grammar submit step missing");
  if (!focusResult.grammarText.includes("命令：PLAY_CARD")) failures.push("focused interaction grammar command type missing");
  if (!focusResult.grammarRoles.includes("source")) failures.push("focused interaction grammar source role missing");
  if (!focusResult.grammarRoles.includes("submit")) failures.push("focused interaction grammar submit role missing");
  if (focusResult.detailLayerOpen) failures.push("focused action summary opened detail");
  if (targetResult.sourceSelected !== "true") failures.push("source focus was not preserved after target click");
  if (targetResult.sourceState !== "source") failures.push("source state missing after target click");
  if (targetResult.chosenTargetState !== "chosen") failures.push("clicked target not chosen");
  if (targetResult.otherTargetState !== "target") failures.push("other target no longer legal target");
  if (targetResult.detailLayerOpen) failures.push("target click opened detail");
  if (!targetResult.draftText.includes("目标 1")) failures.push("draft target count missing");
  if (targetResult.grammarState !== "ready") failures.push(`target interaction grammar state unexpected: ${targetResult.grammarState}`);
  if (!targetResult.grammarText.includes("目标")) failures.push("target interaction grammar target step missing");
  if (!targetResult.grammarText.includes("已选择")) failures.push("target interaction grammar did not show selected target");
  if (!targetResult.previewText.includes("提交摘要")) failures.push("candidate command preview missing");
  if (targetResult.previewText.includes("目标：无")) failures.push("candidate command preview did not include chosen target");
  if (targetResult.targetSelectValue !== "p2-left-1") failures.push("composer target select did not follow target click");
  if (costResult.exhaustedRuneState !== "chosen") failures.push("clicked optional cost not chosen");
  if (!costResult.draftText.includes("费用 1")) failures.push("draft cost count missing");
  if (!costResult.checkedCost.some((text) => text.includes("回收已抽出符文"))) failures.push("composer optional cost not checked");
  if (!costResult.previewText.includes("回收已抽出符文")) failures.push("candidate command preview did not include chosen optional cost");
  if (!costResult.grammarText.includes("费用")) failures.push("cost interaction grammar cost step missing");
  if (!costResult.grammarText.includes("已选择")) failures.push("cost interaction grammar did not show selected cost");
  if (destinationResult.moveSourceSelected !== "true") failures.push("move source focus was not preserved");
  if (destinationResult.moveSourceState !== "source") failures.push("move source state missing");
  if (destinationResult.destinationState !== "chosen") failures.push("destination not chosen");
  if (destinationResult.destinationSelectValue !== "BATTLEFIELD:fixture-right-battlefield") failures.push("composer destination select did not follow click");
  if (destinationResult.detailLayerOpen) failures.push("destination click opened detail");
  if (actionMapResult.selected !== "true") failures.push("action map object chip did not focus table object");
  if (actionMapResult.chipSelected !== "true") failures.push("action map object chip did not show selected state");
  if (actionMapResult.actionRenderState !== "ready") failures.push(`action panel render plan state unexpected: ${actionMapResult.actionRenderState}`);
  if (actionMapResult.actionRenderPromptType !== "MAIN_ACTION") failures.push(`action panel render prompt type unexpected: ${actionMapResult.actionRenderPromptType}`);
  if (actionMapResult.actionRenderCount < 1) failures.push("action panel render entries missing");
  if (!actionMapResult.actionRenderKinds.includes("candidate-button")) failures.push("action panel render candidate button entry missing");
  if (!actionMapResult.actionMapText.includes("命令字段")) failures.push("action map command field list missing");
  if (!actionMapResult.actionMapText.includes("候选步骤")) failures.push("action map candidate step plan missing");
  if (!actionMapResult.actionMapText.includes("下一步")) failures.push("action map candidate next-step text missing");
  if (!actionMapResult.actionMapText.includes("PLAY_CARD")) failures.push("action map command type missing");
  if (!actionMapResult.actionMapText.includes("服务端字段")) failures.push("action map metadata command field summary missing");
  if (actionMapResult.actionMapText.includes("服务端:cardNo*")) failures.push("action map leaked raw metadata command field");
  if (actionMapResult.focusBridgeState !== "enabled") failures.push(`action map focus bridge state unexpected: ${actionMapResult.focusBridgeState}`);
  if (!actionMapResult.focusBridgeText.includes("角色 来源")) failures.push("action map focus bridge role summary missing");
  if (!actionMapResult.focusBridgeText.includes("可选目标")) failures.push("action map focus bridge next step missing");
  if (!actionMapResult.focusBridgeText.includes("PLAY_CARD")) failures.push("action map focus bridge command type missing");
  if (!actionMapResult.focusBridgeText.includes("对方单位")) failures.push("action map focus bridge next object ref missing");
  if (!["resolving", "you-action"].includes(actionMapResult.windowState)) failures.push(`wire window plan did not show a server-derived active state: ${actionMapResult.windowState}`);
  if (!actionMapResult.windowText.includes("窗口总览")) failures.push("wire window plan header missing");
  if (!actionMapResult.windowText.includes("下一步")) failures.push("wire window plan next step missing");
  if (!actionMapResult.windowText.includes("可提交")) failures.push("wire window plan candidate metric missing");
  if (!actionMapResult.windowText.includes("结算链")) failures.push("wire window plan stack metric missing");
  if (!actionMapResult.windowText.includes("任务")) failures.push("wire window plan task metric missing");
  if (!actionMapResult.promptInspectionText.includes("提示检查")) failures.push("wire prompt inspection header missing");
  if (!actionMapResult.promptInspectionText.includes("服务端提示检查")) failures.push("wire prompt inspection source missing");
  if (!actionMapResult.promptInspectionText.includes("服务端只公开")) failures.push("wire prompt inspection boundary missing");
  if (!actionMapResult.promptInspectionText.includes("前端职责")) failures.push("wire prompt inspection safe boundary missing");
  if (!actionMapResult.promptInspectionSummaryKeys.includes("candidate")) failures.push("wire prompt inspection candidate summary missing");
  if (!actionMapResult.promptInspectionGroups.includes("candidate")) failures.push("wire prompt inspection candidate group missing");
  if (!actionMapResult.promptInspectionGroups.includes("safe-boundary")) failures.push("wire prompt inspection safe-boundary group missing");
  if (!actionMapResult.evidenceText.includes("证据摘要")) failures.push("wire window evidence header missing");
  if (!actionMapResult.evidenceText.includes("服务端结算链")) failures.push("wire window evidence stack source missing");
  if (!actionMapResult.evidenceText.includes("服务端规则任务")) failures.push("wire window evidence task source missing");
  if (actionMapResult.evidenceText.includes("triggerQueue")) failures.push("wire window evidence leaked raw trigger queue key");
  if (actionMapResult.evidenceText.includes("pendingTaskQueue")) failures.push("wire window evidence leaked raw pending task queue key");
  if (!actionMapResult.evidenceKeys.includes("prompt")) failures.push("wire window evidence prompt row missing");
  if (!actionMapResult.evidenceKeys.includes("stack")) failures.push("wire window evidence stack row missing");
  if (!actionMapResult.evidenceKeys.includes("tasks")) failures.push("wire window evidence task row missing");
  if (!actionMapResult.evidenceKeys.includes("spell-duel")) failures.push("wire window evidence spell-duel row missing");
  if (!actionMapResult.evidenceKeys.includes("battle")) failures.push("wire window evidence battle row missing");
  if (actionMapResult.evidenceStackState !== "active") failures.push(`wire window evidence stack state unexpected: ${actionMapResult.evidenceStackState}`);
  if (actionMapResult.evidenceTaskState !== "active") failures.push(`wire window evidence task state unexpected: ${actionMapResult.evidenceTaskState}`);
  if (!["battle", "battlefield-task", "task"].includes(actionMapResult.priorityMode)) failures.push(`wire priority rail mode did not reflect server task context: ${actionMapResult.priorityMode}`);
  if (actionMapResult.priorityActiveStep !== "focus" && actionMapResult.priorityActiveStep !== "tasks") failures.push(`wire priority rail active step missing task/focus state: ${actionMapResult.priorityActiveStep}`);
  if (!actionMapResult.priorityRailText.includes("优先权轨道")) failures.push("wire priority rail header missing");
  if (!actionMapResult.priorityRailText.includes("响应/焦点")) failures.push("wire priority rail focus step missing");
  if (!actionMapResult.priorityRailText.includes("规则任务")) failures.push("wire priority rail task step missing");
  if (!actionMapResult.priorityRailText.includes("操作入口")) failures.push("wire priority rail entry step missing");
  if (!["task-blocked", "task-open", "stack-response"].includes(actionMapResult.ruleQueueState)) failures.push(`wire rule queue state did not reflect server queue context: ${actionMapResult.ruleQueueState}`);
  if (actionMapResult.ruleLaneCount !== 4) failures.push(`wire rule queue lane count mismatch: ${actionMapResult.ruleLaneCount}`);
  if (!actionMapResult.ruleFlowText.includes("规则队列地图")) failures.push("wire rule queue flow header missing");
  if (actionMapResult.ruleFocusLane !== "task") failures.push(`wire rule focus lane unexpected: ${actionMapResult.ruleFocusLane}`);
  if (!actionMapResult.ruleFocusDetailId?.includes("rule:task:fixture-task-1")) failures.push("wire rule focus did not expose active task detail id");
  if (!actionMapResult.ruleFocusText.includes("当前规则焦点")) failures.push("wire rule focus heading missing");
  if (!actionMapResult.ruleFocusText.includes("阻塞普通行动")) failures.push("wire rule focus reason missing");
  if (actionMapResult.ruleFocusRefCount < 1) failures.push("wire rule focus object refs missing");
  if (!actionMapResult.ruleFlowText.includes("结算链")) failures.push("wire rule queue stack lane missing");
  if (!actionMapResult.ruleFlowText.includes("规则任务")) failures.push("wire rule queue task lane missing");
  if (!actionMapResult.ruleFlowText.includes("触发队列")) failures.push("wire rule queue trigger lane missing");
  if (!actionMapResult.ruleFlowText.includes("近期事件")) failures.push("wire rule queue resolution lane missing");
  if (!actionMapResult.ruleFlowText.includes("下一步")) failures.push("wire rule queue next step missing");
  if (actionMapResult.ruleSequenceCount < 1) failures.push("wire rule queue sequence items missing");
  for (const sectionKey of ["stack", "task", "trigger", "resolution"]) {
    if (!actionMapResult.ruleSectionKeys.includes(sectionKey)) failures.push(`wire rule queue section missing: ${sectionKey}`);
  }
  if (!actionMapResult.ruleItemKeys.some((key) => key?.startsWith("stack:"))) failures.push("wire rule queue stack items missing");
  if (!actionMapResult.ruleItemKeys.some((key) => key?.startsWith("task:"))) failures.push("wire rule queue task items missing");
  if (!actionMapResult.ruleItemKeys.some((key) => key?.startsWith("trigger:"))) failures.push("wire rule queue trigger items missing");
  if (!actionMapResult.ruleItemKeys.some((key) => key?.startsWith("battlefield-resolution:"))) failures.push("wire rule queue battlefield resolution items missing");
  if (ruleInspectorResult.hidden) failures.push("wire rule inspector did not open");
  if (ruleInspectorResult.toggleExpanded !== "true") failures.push("wire rule inspector toggle aria state missing");
  if (!ruleInspectorResult.text.includes("规则检查")) failures.push("wire rule inspector header missing");
  if (!ruleInspectorResult.text.includes("活动")) failures.push("wire rule inspector active lane summary missing");
  if (!ruleInspectorResult.text.includes("下一步")) failures.push("wire rule inspector next step missing");
  if (ruleInspectorResult.laneCount !== 4) failures.push(`wire rule inspector lane count mismatch: ${ruleInspectorResult.laneCount}`);
  if (ruleInspectorResult.sequenceCount < 1) failures.push("wire rule inspector sequence items missing");
  if (actionMapResult.candidatePlanCount < 1) failures.push("action map candidate plan cards missing");
  if (actionMapResult.candidatePlanEnabled !== "true") failures.push("PLAY_CARD candidate plan did not preserve enabled state");
  if (!actionMapResult.candidatePlanText.includes("命令字段 5")) failures.push("PLAY_CARD candidate plan command field count missing");
  if (!actionMapResult.candidatePlanText.includes("缺口 0")) failures.push("PLAY_CARD candidate plan gap summary missing");
  if (!actionMapResult.candidatePlanNext.includes("下一步")) failures.push("PLAY_CARD candidate plan next-step missing");
  if (actionMapResult.routeState !== "ready") failures.push(`action route strip source-focus state unexpected: ${actionMapResult.routeState}`);
  if (!actionMapResult.routeText.includes("打出手牌")) failures.push("action route strip source-focus candidate missing");
  if (!actionMapResult.routeText.includes("可送服务端校验")) failures.push("action route strip source-focus ready copy missing");
  if (actionMapResult.candidateStepRefCount < 1) failures.push("action map candidate step object refs missing");
  if (!actionMapResult.candidateStepRefText.includes("手牌法术")) failures.push("action map candidate source object ref missing");
  if (actionCandidateStepResult.selected !== "true") failures.push("action candidate step ref did not focus source object");
  if (!actionCandidateStepResult.selectedContext) failures.push("action candidate step ref did not expose selected object context");
  if (actionCandidateStepResult.detailLayerOpen) failures.push("action candidate step ref opened detail");
  if (!actionMapResult.focusText.includes("服务端状态")) failures.push("action map focus did not refresh focused action summary");
  if (actionMapResult.detailLayerOpen) failures.push("action map object chip opened detail");
  if (actionFocusChoiceResult.sourceSelected !== "true") failures.push("action focus choice did not preserve source focus");
  if (actionFocusChoiceResult.targetState !== "chosen") failures.push("action focus choice did not mark target chosen");
  if (actionFocusChoiceResult.candidateDraftActive !== "true") failures.push("action focus choice did not activate candidate plan draft");
  if (actionFocusChoiceResult.routeState !== "ready") failures.push(`action focus choice route state unexpected: ${actionFocusChoiceResult.routeState}`);
  if (!actionFocusChoiceResult.routeText.includes("可送服务端校验")) failures.push("action focus choice route ready copy missing");
  if (!actionFocusChoiceResult.routeText.includes("服务端字段")) failures.push("action focus choice route server field safe label missing");
  if (!actionFocusChoiceResult.routeFieldStates.includes("covered")) failures.push("action focus choice route covered field missing");
  if (!actionFocusChoiceResult.routeFieldStates.includes("server")) failures.push("action focus choice route server field missing");
  if (actionFocusChoiceResult.targetRouteStepState !== "selected") failures.push("action focus choice route target step missing selected state");
  if (actionFocusChoiceResult.sourceStepProgress !== "selected") failures.push("action focus choice did not mark source step selected");
  if (actionFocusChoiceResult.targetStepProgress !== "selected") failures.push("action focus choice did not mark target step selected");
  if (!actionFocusChoiceResult.targetStepProgressText.includes("已选 1")) failures.push("action focus choice target step progress text missing");
  if (!actionFocusChoiceResult.draftText.includes("目标 1")) failures.push("action focus choice did not update target draft");
  if (actionFocusChoiceResult.previewText.includes("目标：无")) failures.push("action focus choice command preview missed target");
  if (actionFocusChoiceResult.detailLayerOpen) failures.push("action focus choice opened detail");
  if (routeInspectorResult.hidden) failures.push("route inspector did not open");
  if (routeInspectorResult.toggleExpanded !== "true") failures.push("route inspector toggle aria state missing");
  if (!routeInspectorResult.text.includes("路线检查")) failures.push("route inspector header missing");
  if (!routeInspectorResult.text.includes("字段覆盖")) failures.push("route inspector field section missing");
  if (!routeInspectorResult.text.includes("服务端字段")) failures.push("route inspector server field safe label missing");
  if (!routeInspectorResult.stepStates.includes("selected")) failures.push("route inspector selected step missing");
  if (!routeInspectorResult.fieldStates.includes("covered")) failures.push("route inspector covered field missing");
  if (!routeInspectorResult.fieldStates.includes("server")) failures.push("route inspector server field missing");
  if (blockedActionMapResult.selected !== "true") failures.push("blocked action map object chip did not focus table object");
  if (blockedActionMapResult.chipSelected !== "true") failures.push("blocked action map object chip did not show selected state");
  if (blockedActionMapResult.chipState !== "blocked") failures.push(`blocked action map chip state unexpected: ${blockedActionMapResult.chipState}`);
  if (blockedActionMapResult.focusBridgeState !== "blocked") failures.push(`blocked action map focus bridge state unexpected: ${blockedActionMapResult.focusBridgeState}`);
  if (!blockedActionMapResult.focusBridgeText.includes("ACTIVATE_ABILITY")) failures.push("blocked action map focus bridge command type missing");
  if (!blockedActionMapResult.focusBridgeText.includes("暂不可提交")) failures.push("blocked action map focus bridge blocked state missing");
  if (blockedActionMapResult.detailLayerOpen) failures.push("blocked action map object chip opened detail");
  if (runeActionMapResult.selected !== "true") failures.push("rune action map object chip did not focus table object");
  if (runeActionMapResult.chipSelected !== "true") failures.push("rune action map object chip did not show selected state");
  if (!runeActionMapResult.hasSelectedObjectContext) failures.push("rune action map did not render selected object context");
  if (!runeActionMapResult.detailContextText.includes("TAP_RUNE")) failures.push("rune object context did not expose server tap command template");
  if (!runeActionMapResult.detailContextText.includes("服务端索引")) failures.push("rune object context did not use server object candidate index");
  if (!runeActionMapResult.detailContextText.includes("来源:sourceObjectId*")) failures.push("rune object context did not expose required source binding");
  if (!runeActionMapResult.actionButtonText.includes("横置符文样例")) failures.push("rune action panel template button missing");
  if (runeActionMapResult.actionButtonText.includes("需选择")) failures.push("rune action panel did not build direct command from server template");
  if (!runeActionMapResult.focusText.includes("服务端状态")) failures.push("rune action map focus did not refresh focused action summary");
  if (candidateRefResult.hasCandidateRefs < 1) failures.push("candidate object refs missing");
  if (candidateRefResult.selected !== "true") failures.push("candidate object ref did not focus table object");
  if (!candidateRefResult.selectedRef) failures.push("candidate object ref did not show selected state");
  if (!candidateRefResult.contextText.includes("右战场 / 对方单位")) failures.push("candidate object ref did not refresh object context");
  if (!candidateRefResult.hasSelectedObjectContext) failures.push("timeline detail did not render selected object context");
  if (!candidateRefResult.detailContextText.includes("右战场 / 对方单位")) failures.push("timeline selected object context did not use server zone");
  if (!candidateRefResult.detailContextText.includes("服务端命令")) failures.push("timeline selected object context command section missing");
  if (!candidateRefResult.detailContextText.includes("服务端索引")) failures.push("timeline selected object context did not use server object candidate index");
  if (!candidateRefResult.detailContextText.includes("PLAY_CARD")) failures.push("timeline selected object context command type missing");
  if (!candidateRefResult.detailContextText.includes("服务端字段")) failures.push("timeline selected object context command metadata summary missing");
  if (candidateRefResult.detailContextText.includes("服务端:cardNo*")) failures.push("timeline selected object context leaked raw metadata command field");
  if (!candidateRefResult.detailContextText.includes("近期事件")) failures.push("timeline selected object context event section missing");
  if (candidateRefResult.detailLayerOpen) failures.push("candidate object ref opened detail");

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

  await clickWireDetail(cdp, "rule:stack:fixture-stack-1");
  await delay(150);
  const ruleDetailResult = await evaluateJson(cdp, `(() => {
    const panel = document.querySelector(".wire-timeline-detail");
    const trigger = document.querySelector('[data-wire-detail-id="rule:stack:fixture-stack-1"]');
    const selectedRow = document.querySelector(".wire-rule-item.is-detail-selected");
    const sourceObject = document.querySelector('[data-object-id="p1-hand-spell"]');
    const targetObject = document.querySelector('[data-object-id="p2-right-1"]');
    return {
      text: panel?.textContent ?? "",
      actionCandidateCount: Number(panel?.getAttribute("data-wire-timeline-action-candidate-count") ?? "-1"),
      commandBridgeCount: Number(panel?.getAttribute("data-wire-timeline-command-bridge-count") ?? "-1"),
      detailId: panel?.getAttribute("data-wire-timeline-detail-id") ?? "",
      detailSource: panel?.getAttribute("data-wire-timeline-source") ?? "",
      hiddenRefCount: Number(panel?.getAttribute("data-wire-timeline-hidden-ref-count") ?? "-1"),
      missingRefCount: Number(panel?.getAttribute("data-wire-timeline-missing-ref-count") ?? "-1"),
      panelState: panel?.getAttribute("data-wire-timeline-detail-state") ?? null,
      visibleRefCount: Number(panel?.getAttribute("data-wire-timeline-visible-ref-count") ?? "-1"),
      bodyId: panel?.querySelector("#wire-timeline-detail-body")?.id ?? "",
      triggerAriaPressed: trigger?.getAttribute("aria-pressed") ?? null,
      triggerControls: trigger?.getAttribute("aria-controls") ?? "",
      triggerLabel: trigger?.getAttribute("aria-label") ?? "",
      triggerSelected: trigger?.getAttribute("data-detail-selected") ?? null,
      triggerSource: trigger?.getAttribute("data-wire-detail-source") ?? null,
      selectedRow: Boolean(selectedRow),
      hasSourceRef: Boolean(panel?.querySelector('[data-rule-object-ref="p1-hand-spell"]')),
      hasTargetRef: Boolean(panel?.querySelector('[data-rule-object-ref="p2-right-1"]')),
      projectionStates: Array.from(panel?.querySelectorAll(".wire-timeline-projection-list li") ?? [])
        .map((item) => item.getAttribute("data-projection-state")),
      projectionText: panel?.querySelector(".wire-timeline-projection-list")?.textContent ?? "",
      navigationActionStates: Array.from(panel?.querySelectorAll(".wire-timeline-navigation-list li") ?? [])
        .map((item) => item.getAttribute("data-timeline-navigation-action-state")),
      navigationButtonCount: panel?.querySelectorAll(".wire-timeline-navigation-button").length ?? 0,
      navigationCount: panel?.querySelectorAll(".wire-timeline-navigation-list li").length ?? 0,
      navigationFocusStates: Array.from(panel?.querySelectorAll(".wire-timeline-navigation-list li") ?? [])
        .map((item) => item.getAttribute("data-timeline-navigation-focus-state")),
      navigationObjectIds: Array.from(panel?.querySelectorAll(".wire-timeline-navigation-list li") ?? [])
        .map((item) => item.getAttribute("data-timeline-navigation-object-id") ?? ""),
      navigationText: panel?.querySelector(".wire-timeline-navigation-list")?.textContent ?? "",
      commandBridgeEnabledStates: Array.from(panel?.querySelectorAll(".wire-timeline-command-bridge li") ?? [])
        .map((item) => item.getAttribute("data-timeline-command-bridge-enabled")),
      commandBridgeDraftActiveStates: Array.from(panel?.querySelectorAll(".wire-timeline-command-bridge li") ?? [])
        .map((item) => item.getAttribute("data-timeline-command-bridge-draft-active")),
      commandBridgeFieldStates: Array.from(panel?.querySelectorAll("[data-timeline-command-field-state]") ?? [])
        .map((item) => item.getAttribute("data-timeline-command-field-state")),
      commandBridgeGrammarStates: Array.from(panel?.querySelectorAll("[data-timeline-command-grammar-state]") ?? [])
        .map((item) => item.getAttribute("data-timeline-command-grammar-state")),
      commandBridgeGrammarStepStates: Array.from(panel?.querySelectorAll("[data-timeline-command-grammar-step-state]") ?? [])
        .map((item) => item.getAttribute("data-timeline-command-grammar-step-state")),
      commandBridgeGateStates: Array.from(panel?.querySelectorAll("[data-timeline-command-gate-state]") ?? [])
        .map((item) => item.getAttribute("data-timeline-command-gate-state")),
      commandBridgeOpenDetailButtonCount: panel?.querySelectorAll("[data-timeline-command-open-detail-object-id]").length ?? 0,
      commandBridgeOpenDetailObjectIds: Array.from(panel?.querySelectorAll("[data-timeline-command-open-detail-object-id]") ?? [])
        .map((item) => item.getAttribute("data-timeline-command-open-detail-object-id") ?? ""),
      commandBridgeNextButtonCount: panel?.querySelectorAll("[data-timeline-command-bridge-next-object-id]").length ?? 0,
      commandBridgeNextObjectIds: Array.from(panel?.querySelectorAll("[data-timeline-command-bridge-next-object-id]") ?? [])
        .map((item) => item.getAttribute("data-timeline-command-bridge-next-object-id") ?? ""),
      commandBridgeRouteStates: Array.from(panel?.querySelectorAll(".wire-timeline-command-bridge li") ?? [])
        .map((item) => item.getAttribute("data-timeline-command-bridge-route-state")),
      commandBridgeRowCount: panel?.querySelectorAll(".wire-timeline-command-bridge li").length ?? 0,
      commandBridgeText: panel?.querySelector(".wire-timeline-command-bridge")?.textContent ?? "",
      statusText: panel?.querySelector(".wire-timeline-detail-status-grid")?.textContent ?? "",
      actionHintCount: panel?.querySelectorAll(".wire-timeline-action-hint-list li").length ?? 0,
      actionHintButtonCount: panel?.querySelectorAll(".wire-timeline-action-hint-button").length ?? 0,
      actionHintText: panel?.querySelector(".wire-timeline-action-hint-list")?.textContent ?? "",
      sourceState: sourceObject?.getAttribute("data-timeline-state") ?? null,
      targetState: targetObject?.getAttribute("data-timeline-state") ?? null,
      sourcePromptState: sourceObject?.getAttribute("data-prompt-state") ?? null,
      targetPromptState: targetObject?.getAttribute("data-prompt-state") ?? null,
      detailLayerOpen: Boolean(document.querySelector(".detail-layer"))
    };
  })()`);

  const commandBridgeDetailObjectId = await clickTimelineCommandBridgeDetail(cdp, "p1-hand-spell");
  await delay(150);
  const commandBridgeDetailResult = await evaluateJson(cdp, `(() => {
    const detail = document.querySelector(".detail-layer");
    const actions = detail?.querySelector("[data-card-detail-actions-state]");
    return {
      actionCount: Number(actions?.querySelector("[data-card-detail-action-count]")?.getAttribute("data-card-detail-action-count") ?? "0"),
      actionText: actions?.textContent ?? "",
      activeText: document.activeElement?.textContent ?? "",
      composerCount: detail?.querySelectorAll(".candidate-composer").length ?? 0,
      connectionState: detail?.getAttribute("data-card-detail-connection-state") ?? null,
      detailTitle: detail?.querySelector("#card-detail-title")?.textContent ?? "",
      objectId: ${JSON.stringify(commandBridgeDetailObjectId)},
      open: Boolean(detail)
    };
  })()`);
  await pressEscape(cdp);
  await delay(120);
  const commandBridgeDetailClosed = await evaluateJson(cdp, `(() => !document.querySelector(".detail-layer"))()`);

  await clickButtonByText(cdp, "展开事件检查");
  await delay(150);
  const timelineInspectorResult = await evaluateJson(cdp, `(() => {
    const inspector = document.querySelector(".wire-timeline-inspector");
    return {
      candidateCount: inspector?.querySelectorAll("[data-timeline-inspector-candidate]").length ?? 0,
      hidden: inspector?.hasAttribute("hidden") ?? true,
      projectionCount: inspector?.querySelectorAll("[data-timeline-inspector-projection]").length ?? 0,
      text: inspector?.textContent ?? "",
      toggleExpanded: document.querySelector("[data-timeline-inspector-toggle]")?.getAttribute("aria-expanded") ?? null
    };
  })()`);

  const commandBridgeObjectId = await clickTimelineCommandBridgeNext(cdp);
  await delay(150);
  const commandBridgeFocusResult = await evaluateJson(cdp, `(() => {
    const objectId = ${JSON.stringify(commandBridgeObjectId)};
    const sourceObject = document.querySelector('[data-object-id="p1-hand-spell"]');
    const targetObject = document.querySelector(\`[data-object-id="\${objectId}"]\`);
    const candidatePlan = document.querySelector('[data-candidate-plan-action="PLAY_CARD"]');
    const panel = document.querySelector(".wire-timeline-detail");
    const route = document.querySelector("[data-action-route-state]");
    const sourceStep = candidatePlan?.querySelector('[data-step-role="source"]');
    const targetStep = candidatePlan?.querySelector('[data-step-role="target"]');
    const targetRouteStep = route?.querySelector('[data-route-step-role="target"]');
    return {
      candidateDraftActive: candidatePlan?.getAttribute("data-candidate-plan-draft-active") ?? null,
      detailLayerOpen: Boolean(document.querySelector(".detail-layer")),
      draftText: document.querySelector(".wire-selection-draft")?.textContent ?? "",
      objectId,
      panelState: panel?.getAttribute("data-wire-timeline-detail-state") ?? null,
      previewText: document.querySelector(".candidate-command-preview")?.textContent ?? "",
      routeState: route?.getAttribute("data-action-route-state") ?? null,
      routeText: route?.textContent ?? "",
      commandBridgeDraftActiveStates: Array.from(panel?.querySelectorAll(".wire-timeline-command-bridge li") ?? [])
        .map((item) => item.getAttribute("data-timeline-command-bridge-draft-active")),
      commandBridgeFieldStates: Array.from(panel?.querySelectorAll("[data-timeline-command-field-state]") ?? [])
        .map((item) => item.getAttribute("data-timeline-command-field-state")),
      commandBridgeGrammarStates: Array.from(panel?.querySelectorAll("[data-timeline-command-grammar-state]") ?? [])
        .map((item) => item.getAttribute("data-timeline-command-grammar-state")),
      commandBridgeGrammarStepStates: Array.from(panel?.querySelectorAll("[data-timeline-command-grammar-step-state]") ?? [])
        .map((item) => item.getAttribute("data-timeline-command-grammar-step-state")),
      commandBridgeGateStates: Array.from(panel?.querySelectorAll("[data-timeline-command-gate-state]") ?? [])
        .map((item) => item.getAttribute("data-timeline-command-gate-state")),
      commandBridgeNextButtonCount: panel?.querySelectorAll("[data-timeline-command-bridge-next-object-id]").length ?? 0,
      commandBridgeRouteStates: Array.from(panel?.querySelectorAll(".wire-timeline-command-bridge li") ?? [])
        .map((item) => item.getAttribute("data-timeline-command-bridge-route-state")),
      commandBridgeText: panel?.querySelector(".wire-timeline-command-bridge")?.textContent ?? "",
      sourceSelected: sourceObject?.getAttribute("data-selected") ?? null,
      sourceStepProgress: sourceStep?.getAttribute("data-step-progress") ?? null,
      targetRouteStepState: targetRouteStep?.getAttribute("data-route-step-state") ?? null,
      targetSelected: targetObject?.getAttribute("data-selected") ?? null,
      targetState: targetObject?.getAttribute("data-prompt-state") ?? null,
      targetStepProgress: targetStep?.getAttribute("data-step-progress") ?? null
    };
  })()`);

  const commandBridgeDraftDetailObjectId = await clickTimelineCommandBridgeDetail(cdp, "p1-hand-spell");
  await delay(150);
  const commandBridgeDraftDetailResult = await evaluateJson(cdp, `(() => {
    const detail = document.querySelector(".detail-layer");
    const preview = detail?.querySelector(".candidate-command-preview");
    return {
      composerCount: detail?.querySelectorAll(".candidate-composer").length ?? 0,
      objectId: ${JSON.stringify(commandBridgeDraftDetailObjectId)},
      open: Boolean(detail),
      previewText: preview?.textContent ?? ""
    };
  })()`);
  await pressEscape(cdp);
  await delay(120);
  const commandBridgeDraftDetailClosed = await evaluateJson(cdp, `(() => !document.querySelector(".detail-layer"))()`);

  const actionHintObjectId = await clickTimelineActionHint(cdp);
  await delay(150);
  const actionHintFocusResult = await evaluateJson(cdp, `(() => {
    const objectId = ${JSON.stringify(actionHintObjectId)};
    const tableObject = document.querySelector(\`[data-object-id="\${objectId}"]\`);
    const selectedObjectContext = document.querySelector(\`[data-wire-selected-object-context="\${objectId}"]\`);
    const panel = document.querySelector(".wire-timeline-detail");
    return {
      detailLayerOpen: Boolean(document.querySelector(".detail-layer")),
      objectId,
      panelState: panel?.getAttribute("data-wire-timeline-detail-state") ?? null,
      selected: tableObject?.getAttribute("data-selected") ?? null,
      selectedContext: Boolean(selectedObjectContext)
    };
  })()`);

  await clickWireDetail(cdp, "event:STACK_ITEM_ADDED:0");
  await delay(150);
  const eventDetailResult = await evaluateJson(cdp, `(() => {
    const panel = document.querySelector(".wire-timeline-detail");
    const trigger = document.querySelector('[data-wire-detail-id="event:STACK_ITEM_ADDED:0"]');
    const log = document.querySelector(".event-log");
    const selectedRow = document.querySelector(".log-row.is-detail-selected");
    const sourceObject = document.querySelector('[data-object-id="p1-hand-spell"]');
    const targetObject = document.querySelector('[data-object-id="p2-right-1"]');
    return {
      text: panel?.textContent ?? "",
      actionCandidateCount: Number(panel?.getAttribute("data-wire-timeline-action-candidate-count") ?? "-1"),
      commandBridgeCount: Number(panel?.getAttribute("data-wire-timeline-command-bridge-count") ?? "-1"),
      detailId: panel?.getAttribute("data-wire-timeline-detail-id") ?? "",
      detailSource: panel?.getAttribute("data-wire-timeline-source") ?? "",
      hiddenRefCount: Number(panel?.getAttribute("data-wire-timeline-hidden-ref-count") ?? "-1"),
      missingRefCount: Number(panel?.getAttribute("data-wire-timeline-missing-ref-count") ?? "-1"),
      panelState: panel?.getAttribute("data-wire-timeline-detail-state") ?? null,
      visibleRefCount: Number(panel?.getAttribute("data-wire-timeline-visible-ref-count") ?? "-1"),
      triggerAriaPressed: trigger?.getAttribute("aria-pressed") ?? null,
      triggerControls: trigger?.getAttribute("aria-controls") ?? "",
      triggerLabel: trigger?.getAttribute("aria-label") ?? "",
      triggerSelected: trigger?.getAttribute("data-detail-selected") ?? null,
      triggerSource: trigger?.getAttribute("data-wire-detail-source") ?? null,
      logErrorCount: Number(log?.getAttribute("data-event-log-error-count") ?? "-1"),
      logHiddenCount: Number(log?.getAttribute("data-event-log-hidden-count") ?? "-1"),
      logState: log?.getAttribute("data-event-log-state") ?? null,
      logVisibleCount: Number(log?.getAttribute("data-event-log-visible-count") ?? "0"),
      selectedRowKind: selectedRow?.getAttribute("data-event-log-row-kind") ?? null,
      selectedRowRefCount: Number(selectedRow?.getAttribute("data-event-log-row-ref-count") ?? "0"),
      selectedRow: Boolean(selectedRow),
      hasSourceRef: Boolean(panel?.querySelector('[data-event-object-ref="p1-hand-spell"]')),
      hasTargetRef: Boolean(panel?.querySelector('[data-event-object-ref="p2-right-1"]')),
      projectionStates: Array.from(panel?.querySelectorAll(".wire-timeline-projection-list li") ?? [])
        .map((item) => item.getAttribute("data-projection-state")),
      projectionText: panel?.querySelector(".wire-timeline-projection-list")?.textContent ?? "",
      navigationActionStates: Array.from(panel?.querySelectorAll(".wire-timeline-navigation-list li") ?? [])
        .map((item) => item.getAttribute("data-timeline-navigation-action-state")),
      navigationButtonCount: panel?.querySelectorAll(".wire-timeline-navigation-button").length ?? 0,
      navigationCount: panel?.querySelectorAll(".wire-timeline-navigation-list li").length ?? 0,
      navigationFocusStates: Array.from(panel?.querySelectorAll(".wire-timeline-navigation-list li") ?? [])
        .map((item) => item.getAttribute("data-timeline-navigation-focus-state")),
      navigationObjectIds: Array.from(panel?.querySelectorAll(".wire-timeline-navigation-list li") ?? [])
        .map((item) => item.getAttribute("data-timeline-navigation-object-id") ?? ""),
      navigationText: panel?.querySelector(".wire-timeline-navigation-list")?.textContent ?? "",
      commandBridgeEnabledStates: Array.from(panel?.querySelectorAll(".wire-timeline-command-bridge li") ?? [])
        .map((item) => item.getAttribute("data-timeline-command-bridge-enabled")),
      commandBridgeDraftActiveStates: Array.from(panel?.querySelectorAll(".wire-timeline-command-bridge li") ?? [])
        .map((item) => item.getAttribute("data-timeline-command-bridge-draft-active")),
      commandBridgeFieldStates: Array.from(panel?.querySelectorAll("[data-timeline-command-field-state]") ?? [])
        .map((item) => item.getAttribute("data-timeline-command-field-state")),
      commandBridgeGrammarStates: Array.from(panel?.querySelectorAll("[data-timeline-command-grammar-state]") ?? [])
        .map((item) => item.getAttribute("data-timeline-command-grammar-state")),
      commandBridgeGrammarStepStates: Array.from(panel?.querySelectorAll("[data-timeline-command-grammar-step-state]") ?? [])
        .map((item) => item.getAttribute("data-timeline-command-grammar-step-state")),
      commandBridgeGateStates: Array.from(panel?.querySelectorAll("[data-timeline-command-gate-state]") ?? [])
        .map((item) => item.getAttribute("data-timeline-command-gate-state")),
      commandBridgeNextButtonCount: panel?.querySelectorAll("[data-timeline-command-bridge-next-object-id]").length ?? 0,
      commandBridgeNextObjectIds: Array.from(panel?.querySelectorAll("[data-timeline-command-bridge-next-object-id]") ?? [])
        .map((item) => item.getAttribute("data-timeline-command-bridge-next-object-id") ?? ""),
      commandBridgeRouteStates: Array.from(panel?.querySelectorAll(".wire-timeline-command-bridge li") ?? [])
        .map((item) => item.getAttribute("data-timeline-command-bridge-route-state")),
      commandBridgeRowCount: panel?.querySelectorAll(".wire-timeline-command-bridge li").length ?? 0,
      commandBridgeText: panel?.querySelector(".wire-timeline-command-bridge")?.textContent ?? "",
      statusText: panel?.querySelector(".wire-timeline-detail-status-grid")?.textContent ?? "",
      actionHintCount: panel?.querySelectorAll(".wire-timeline-action-hint-list li").length ?? 0,
      actionHintText: panel?.querySelector(".wire-timeline-action-hint-list")?.textContent ?? "",
      sourceState: sourceObject?.getAttribute("data-timeline-state") ?? null,
      targetState: targetObject?.getAttribute("data-timeline-state") ?? null,
      sourcePromptState: sourceObject?.getAttribute("data-prompt-state") ?? null,
      targetPromptState: targetObject?.getAttribute("data-prompt-state") ?? null,
      detailLayerOpen: Boolean(document.querySelector(".detail-layer"))
    };
  })()`);

  await clickDetailClear(cdp);
  await delay(100);
  const detailClearResult = await evaluateJson(cdp, `(() => {
    const panel = document.querySelector(".wire-timeline-detail");
    return {
      activeDetailId: document.activeElement?.getAttribute("data-wire-detail-id") ?? null,
      clearButton: Boolean(document.querySelector(".wire-detail-clear")),
      panelState: panel?.getAttribute("data-wire-timeline-detail-state") ?? null,
      selectedDetailCount: document.querySelectorAll('[data-detail-selected="true"]').length,
      selectedRowCount: document.querySelectorAll(".is-detail-selected").length,
      text: panel?.textContent ?? ""
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
  if (!ruleDetailResult.text.includes("结算链项目")) failures.push("rule detail title missing");
  if (ruleDetailResult.panelState !== "rule") failures.push(`rule detail panel state unexpected: ${ruleDetailResult.panelState}`);
  if (ruleDetailResult.bodyId !== "wire-timeline-detail-body") failures.push("rule detail body id missing");
  if (ruleDetailResult.triggerAriaPressed !== "true") failures.push("rule detail trigger aria-pressed missing");
  if (ruleDetailResult.triggerControls !== "wire-timeline-detail-body") failures.push("rule detail trigger aria-controls missing");
  if (!ruleDetailResult.triggerLabel.includes("结算链项目")) failures.push("rule detail trigger accessible label missing");
  if (ruleDetailResult.triggerSelected !== "true") failures.push("rule detail trigger selected state missing");
  if (ruleDetailResult.triggerSource !== "rule") failures.push("rule detail trigger source missing");
  if (ruleDetailResult.detailId !== "rule:stack:fixture-stack-1") failures.push(`rule detail id missing: ${ruleDetailResult.detailId}`);
  if (ruleDetailResult.detailSource !== "rule") failures.push("rule detail source data attr missing");
  if (ruleDetailResult.visibleRefCount < 2) failures.push(`rule detail visible ref count too low: ${ruleDetailResult.visibleRefCount}`);
  if (ruleDetailResult.actionCandidateCount < 1) failures.push("rule detail action candidate count missing");
  if (ruleDetailResult.commandBridgeCount < 1) failures.push("rule detail command bridge count missing");
  if (!ruleDetailResult.text.includes("来源")) failures.push("rule detail source line missing");
  if (!ruleDetailResult.hasSourceRef) failures.push("rule detail source ref missing");
  if (!ruleDetailResult.hasTargetRef) failures.push("rule detail target ref missing");
  if (!ruleDetailResult.statusText.includes("桌面投影")) failures.push("rule detail projection status missing");
  if (!ruleDetailResult.statusText.includes("当前焦点")) failures.push("rule detail focus status missing");
  if (!ruleDetailResult.statusText.includes("关联候选")) failures.push("rule detail candidate status missing");
  if (!ruleDetailResult.projectionStates.includes("visible")) failures.push("rule detail did not expose visible projection rows");
  if (!ruleDetailResult.projectionText.includes("来源")) failures.push("rule detail projection source role missing");
  if (!ruleDetailResult.projectionText.includes("目标")) failures.push("rule detail projection target role missing");
  if (ruleDetailResult.navigationCount < 2) failures.push("rule detail navigation rows missing");
  if (ruleDetailResult.navigationButtonCount < 2) failures.push("rule detail navigation focus buttons missing");
  if (!ruleDetailResult.navigationObjectIds.includes("p1-hand-spell")) failures.push("rule detail navigation source object missing");
  if (!ruleDetailResult.navigationObjectIds.includes("p2-right-1")) failures.push("rule detail navigation target object missing");
  if (!ruleDetailResult.navigationFocusStates.includes("focusable")) failures.push("rule detail navigation focusable state missing");
  if (!ruleDetailResult.navigationActionStates.includes("available")) failures.push("rule detail navigation available action state missing");
  if (!ruleDetailResult.navigationText.includes("可聚焦")) failures.push("rule detail navigation focus label missing");
  if (!ruleDetailResult.navigationText.includes("可用")) failures.push("rule detail navigation action label missing");
  if (ruleDetailResult.commandBridgeRowCount < 1) failures.push("rule detail command bridge rows missing");
  if (ruleDetailResult.commandBridgeNextButtonCount < 1) failures.push("rule detail command bridge next object buttons missing");
  if (!ruleDetailResult.commandBridgeNextObjectIds.includes("p2-right-1")) failures.push("rule detail command bridge target object missing");
  if (!ruleDetailResult.commandBridgeEnabledStates.includes("true")) failures.push("rule detail command bridge enabled state missing");
  if (!ruleDetailResult.commandBridgeDraftActiveStates.includes("true")) failures.push("rule detail command bridge draft state missing");
  if (!ruleDetailResult.commandBridgeRouteStates.includes("ready")) failures.push("rule detail command bridge route state missing");
  if (!ruleDetailResult.commandBridgeText.includes("候选路径")) failures.push("rule detail command bridge title missing");
  if (!ruleDetailResult.commandBridgeText.includes("PLAY_CARD")) failures.push("rule detail command bridge command type missing");
  if (!ruleDetailResult.commandBridgeText.includes("可选目标")) failures.push("rule detail command bridge next step missing");
  if (!ruleDetailResult.commandBridgeText.includes("已选 来源")) failures.push("rule detail command bridge source draft label missing");
  if (!ruleDetailResult.commandBridgeFieldStates.includes("covered")) failures.push("rule detail command bridge covered field state missing");
  if (!ruleDetailResult.commandBridgeFieldStates.includes("server")) failures.push("rule detail command bridge server field state missing");
  if (!ruleDetailResult.commandBridgeText.includes("服务端注入")) failures.push("rule detail command bridge server field label missing");
  if (!ruleDetailResult.commandBridgeGrammarStates.includes("ready")) failures.push("rule detail command bridge grammar ready state missing");
  if (!ruleDetailResult.commandBridgeGrammarStepStates.includes("locked")) failures.push("rule detail command bridge grammar source lock missing");
  if (!ruleDetailResult.commandBridgeGrammarStepStates.includes("ready")) failures.push("rule detail command bridge grammar submit ready missing");
  if (!ruleDetailResult.commandBridgeGateStates.includes("ready")) failures.push("rule detail command bridge gate ready state missing");
  if (!ruleDetailResult.commandBridgeText.includes("提交门禁")) failures.push("rule detail command bridge gate label missing");
  if (ruleDetailResult.commandBridgeOpenDetailButtonCount < 1) failures.push("rule detail command bridge detail buttons missing");
  if (!ruleDetailResult.commandBridgeOpenDetailObjectIds.includes("p1-hand-spell")) failures.push("rule detail command bridge source detail button missing");
  if (commandBridgeDetailResult.objectId !== "p1-hand-spell") failures.push(`command bridge detail opened unexpected object: ${commandBridgeDetailResult.objectId}`);
  if (!commandBridgeDetailResult.open) failures.push("command bridge detail button did not open card detail layer");
  if (!commandBridgeDetailResult.activeText.includes("关闭")) failures.push("command bridge detail drawer did not focus close button");
  if (!commandBridgeDetailResult.actionText.includes("服务端可提交操作")) failures.push("command bridge detail drawer action section missing");
  if (commandBridgeDetailResult.connectionState !== "ready") failures.push(`command bridge detail drawer unexpected connection state: ${commandBridgeDetailResult.connectionState}`);
  if (commandBridgeDetailResult.actionCount < 1) failures.push("command bridge detail drawer action entries missing");
  if (commandBridgeDetailResult.composerCount < 1) failures.push("command bridge detail drawer composer missing");
  if (!commandBridgeDetailClosed) failures.push("command bridge detail drawer did not close before continuing");
  if (ruleDetailResult.actionHintCount < 1) failures.push("rule detail candidate hint rows missing");
  if (ruleDetailResult.actionHintButtonCount < 1) failures.push("rule detail candidate hint buttons missing");
  if (!ruleDetailResult.actionHintText.includes("PLAY_CARD")) failures.push("rule detail candidate hint command type missing");
  if (!ruleDetailResult.actionHintText.includes("可用")) failures.push("rule detail candidate hint state missing");
  if (!ruleDetailResult.actionHintText.includes("角色")) failures.push("rule detail candidate hint role labels missing");
  if (!ruleDetailResult.actionHintText.includes("必填")) failures.push("rule detail candidate hint required fields missing");
  if (timelineInspectorResult.hidden) failures.push("timeline inspector did not open");
  if (timelineInspectorResult.toggleExpanded !== "true") failures.push("timeline inspector toggle aria state missing");
  if (!timelineInspectorResult.text.includes("事件检查")) failures.push("timeline inspector header missing");
  if (!timelineInspectorResult.text.includes("对象投影")) failures.push("timeline inspector projection section missing");
  if (!timelineInspectorResult.text.includes("关联候选")) failures.push("timeline inspector candidate section missing");
  if (!timelineInspectorResult.text.includes("隐藏")) failures.push("timeline inspector hidden boundary missing");
  if (timelineInspectorResult.projectionCount < 4) failures.push("timeline inspector projection states missing");
  if (timelineInspectorResult.candidateCount < 1) failures.push("timeline inspector candidate rows missing");
  if (commandBridgeFocusResult.panelState !== "rule") failures.push(`command bridge focus changed detail panel state: ${commandBridgeFocusResult.panelState}`);
  if (commandBridgeFocusResult.objectId !== "p2-right-1") failures.push(`command bridge focused unexpected object: ${commandBridgeFocusResult.objectId}`);
  if (commandBridgeFocusResult.sourceSelected !== "true") failures.push("command bridge did not keep source object focused");
  if (commandBridgeFocusResult.targetSelected === "true") failures.push("command bridge next object should update draft, not replace focus");
  if (commandBridgeFocusResult.targetState !== "chosen") failures.push(`command bridge next object did not enter draft: ${commandBridgeFocusResult.targetState}`);
  if (commandBridgeFocusResult.candidateDraftActive !== "true") failures.push("command bridge did not activate PLAY_CARD draft");
  if (commandBridgeFocusResult.sourceStepProgress !== "selected") failures.push("command bridge source step not selected in candidate plan");
  if (commandBridgeFocusResult.targetStepProgress !== "selected") failures.push("command bridge target step not selected in candidate plan");
  if (commandBridgeFocusResult.targetRouteStepState !== "selected") failures.push("command bridge target route step not selected");
  if (commandBridgeFocusResult.routeState !== "ready") failures.push(`command bridge route state unexpected: ${commandBridgeFocusResult.routeState}`);
  if (!commandBridgeFocusResult.draftText.includes("目标 1")) failures.push("command bridge draft target count missing");
  if (!commandBridgeFocusResult.routeText.includes("PLAY_CARD")) failures.push("command bridge route command type missing");
  if (!commandBridgeFocusResult.commandBridgeDraftActiveStates.includes("true")) failures.push("command bridge detail draft state missing after target selection");
  if (!commandBridgeFocusResult.commandBridgeRouteStates.includes("ready")) failures.push("command bridge detail route state missing after target selection");
  if (commandBridgeFocusResult.commandBridgeNextButtonCount < 1) failures.push("command bridge detail should still offer optional next choices after target selection");
  if (!commandBridgeFocusResult.commandBridgeText.includes("已选 来源 / 目标")) failures.push("command bridge detail selected roles missing after target selection");
  if (!commandBridgeFocusResult.commandBridgeText.includes("可选位置")) failures.push("command bridge detail optional next step missing after target selection");
  if (!commandBridgeFocusResult.commandBridgeFieldStates.includes("covered")) failures.push("command bridge detail covered field state missing after target selection");
  if (!commandBridgeFocusResult.commandBridgeFieldStates.includes("server")) failures.push("command bridge detail server field state missing after target selection");
  if (!commandBridgeFocusResult.commandBridgeText.includes("2 覆盖 / 0 缺少")) failures.push("command bridge detail coverage summary missing after target selection");
  if (!commandBridgeFocusResult.commandBridgeGrammarStates.includes("ready")) failures.push("command bridge detail grammar ready state missing after target selection");
  if (!commandBridgeFocusResult.commandBridgeGrammarStepStates.includes("selected")) failures.push("command bridge detail grammar selected target missing after target selection");
  if (!commandBridgeFocusResult.commandBridgeGrammarStepStates.includes("ready")) failures.push("command bridge detail grammar submit ready missing after target selection");
  if (!commandBridgeFocusResult.commandBridgeGateStates.includes("ready")) failures.push("command bridge detail gate ready state missing after target selection");
  if (!commandBridgeFocusResult.commandBridgeText.includes("提交门禁")) failures.push("command bridge detail gate label missing after target selection");
  if (commandBridgeFocusResult.detailLayerOpen) failures.push("command bridge button opened card detail layer");
  if (commandBridgeDraftDetailResult.objectId !== "p1-hand-spell") failures.push(`command bridge draft detail opened unexpected object: ${commandBridgeDraftDetailResult.objectId}`);
  if (!commandBridgeDraftDetailResult.open) failures.push("command bridge draft detail did not open card detail layer");
  if (commandBridgeDraftDetailResult.composerCount < 1) failures.push("command bridge draft detail composer missing");
  if (!commandBridgeDraftDetailResult.previewText.includes("目标：")) failures.push("command bridge draft detail preview target row missing");
  if (commandBridgeDraftDetailResult.previewText.includes("目标：无")) failures.push("command bridge draft detail did not inherit selected target");
  if (!commandBridgeDraftDetailClosed) failures.push("command bridge draft detail drawer did not close before continuing");
  if (ruleDetailResult.sourceState !== "rule") failures.push("rule detail did not project source to table");
  if (ruleDetailResult.targetState !== "rule") failures.push("rule detail did not project target to table");
  if (!ruleDetailResult.selectedRow) failures.push("rule detail selected row missing");
  if (ruleDetailResult.detailLayerOpen) failures.push("rule detail opened card detail layer");
  if (actionHintFocusResult.panelState !== "rule") failures.push(`action hint focus changed detail panel state: ${actionHintFocusResult.panelState}`);
  if (actionHintFocusResult.selected !== "true") failures.push("action hint button did not focus table object");
  if (!actionHintFocusResult.selectedContext) failures.push("action hint button did not expose selected object context");
  if (actionHintFocusResult.detailLayerOpen) failures.push("action hint button opened card detail layer");
  if (!eventDetailResult.text.includes("加入结算链")) failures.push("event detail title missing");
  if (eventDetailResult.panelState !== "event") failures.push(`event detail panel state unexpected: ${eventDetailResult.panelState}`);
  if (eventDetailResult.triggerAriaPressed !== "true") failures.push("event detail trigger aria-pressed missing");
  if (eventDetailResult.triggerControls !== "wire-timeline-detail-body") failures.push("event detail trigger aria-controls missing");
  if (!eventDetailResult.triggerLabel.includes("加入结算链")) failures.push("event detail trigger accessible label missing");
  if (eventDetailResult.triggerSelected !== "true") failures.push("event detail trigger selected state missing");
  if (eventDetailResult.triggerSource !== "event") failures.push("event detail trigger source missing");
  if (eventDetailResult.detailId !== "event:STACK_ITEM_ADDED:0") failures.push(`event detail id missing: ${eventDetailResult.detailId}`);
  if (eventDetailResult.detailSource !== "event") failures.push("event detail source data attr missing");
  if (eventDetailResult.visibleRefCount < 2) failures.push(`event detail visible ref count too low: ${eventDetailResult.visibleRefCount}`);
  if (eventDetailResult.actionCandidateCount < 1) failures.push("event detail action candidate count missing");
  if (eventDetailResult.commandBridgeCount < 1) failures.push("event detail command bridge count missing");
  if (eventDetailResult.logState !== "events") failures.push(`event log plan state unexpected: ${eventDetailResult.logState}`);
  if (eventDetailResult.logVisibleCount < 1) failures.push("event log plan visible count missing");
  if (eventDetailResult.logHiddenCount !== 0) failures.push(`event log hidden count unexpected: ${eventDetailResult.logHiddenCount}`);
  if (eventDetailResult.logErrorCount !== 0) failures.push(`event log error count unexpected: ${eventDetailResult.logErrorCount}`);
  if (eventDetailResult.selectedRowKind !== "STACK_ITEM_ADDED") failures.push(`event log selected row kind unexpected: ${eventDetailResult.selectedRowKind}`);
  if (eventDetailResult.selectedRowRefCount < 1) failures.push("event log selected row ref count missing");
  if (!eventDetailResult.text.includes("服务端摘要")) failures.push("event detail did not use server object refs");
  if (!eventDetailResult.hasSourceRef) failures.push("event detail source ref missing");
  if (!eventDetailResult.hasTargetRef) failures.push("event detail target ref missing");
  if (!eventDetailResult.statusText.includes("日志事件")) failures.push("event detail did not label event source");
  if (!eventDetailResult.statusText.includes("关联候选")) failures.push("event detail candidate status missing");
  if (!eventDetailResult.projectionStates.includes("visible")) failures.push("event detail did not expose visible projection rows");
  if (!eventDetailResult.projectionText.includes("来源")) failures.push("event detail projection source role missing");
  if (eventDetailResult.navigationCount < 2) failures.push("event detail navigation rows missing");
  if (eventDetailResult.navigationButtonCount < 2) failures.push("event detail navigation focus buttons missing");
  if (!eventDetailResult.navigationObjectIds.includes("p1-hand-spell")) failures.push("event detail navigation source object missing");
  if (!eventDetailResult.navigationObjectIds.includes("p2-right-1")) failures.push("event detail navigation target object missing");
  if (!eventDetailResult.navigationFocusStates.includes("focusable")) failures.push("event detail navigation focusable state missing");
  if (!eventDetailResult.navigationActionStates.includes("available")) failures.push("event detail navigation available action state missing");
  if (!eventDetailResult.navigationText.includes("可聚焦")) failures.push("event detail navigation focus label missing");
  if (!eventDetailResult.navigationText.includes("可用")) failures.push("event detail navigation action label missing");
  if (eventDetailResult.commandBridgeRowCount < 1) failures.push("event detail command bridge rows missing");
  if (eventDetailResult.commandBridgeNextButtonCount < 1) failures.push("event detail command bridge next object buttons missing");
  if (!eventDetailResult.commandBridgeNextObjectIds.includes("p2-right-1")) failures.push("event detail command bridge target object missing");
  if (!eventDetailResult.commandBridgeEnabledStates.includes("true")) failures.push("event detail command bridge enabled state missing");
  if (!eventDetailResult.commandBridgeText.includes("候选路径")) failures.push("event detail command bridge title missing");
  if (!eventDetailResult.commandBridgeText.includes("PLAY_CARD")) failures.push("event detail command bridge command type missing");
  if (!eventDetailResult.commandBridgeText.includes("可选目标")) failures.push("event detail command bridge next step missing");
  if (!eventDetailResult.commandBridgeFieldStates.includes("covered")) failures.push("event detail command bridge covered field state missing");
  if (!eventDetailResult.commandBridgeFieldStates.includes("server")) failures.push("event detail command bridge server field state missing");
  if (!eventDetailResult.commandBridgeText.includes("服务端注入")) failures.push("event detail command bridge server field label missing");
  if (!eventDetailResult.commandBridgeGrammarStates.includes("ready")) failures.push("event detail command bridge grammar ready state missing");
  if (!eventDetailResult.commandBridgeGrammarStepStates.includes("locked")) failures.push("event detail command bridge grammar source lock missing");
  if (!eventDetailResult.commandBridgeGrammarStepStates.includes("ready")) failures.push("event detail command bridge grammar submit ready missing");
  if (!eventDetailResult.commandBridgeGateStates.includes("ready")) failures.push("event detail command bridge gate ready state missing");
  if (!eventDetailResult.commandBridgeText.includes("提交门禁")) failures.push("event detail command bridge gate label missing");
  if (eventDetailResult.actionHintCount < 1) failures.push("event detail candidate hint rows missing");
  if (!eventDetailResult.actionHintText.includes("PLAY_CARD")) failures.push("event detail candidate hint command type missing");
  if (!eventDetailResult.actionHintText.includes("必填")) failures.push("event detail candidate hint required fields missing");
  if (eventDetailResult.sourceState !== "event") failures.push("event detail did not project source to table");
  if (eventDetailResult.targetState !== "event") failures.push("event detail did not project target to table");
  if (!eventDetailResult.selectedRow) failures.push("event detail selected row missing");
  if (eventDetailResult.detailLayerOpen) failures.push("event detail opened card detail layer");
  if (detailClearResult.clearButton) failures.push("detail clear button remained after clearing");
  if (detailClearResult.panelState !== "object") failures.push(`detail clear did not return to selected object context: ${detailClearResult.panelState}`);
  if (detailClearResult.activeDetailId !== "event:STACK_ITEM_ADDED:0") failures.push("detail clear did not restore focus to source detail trigger");
  if (detailClearResult.selectedDetailCount !== 0) failures.push("detail clear left selected detail trigger");
  if (detailClearResult.selectedRowCount !== 0) failures.push("detail clear left selected detail row");
  if (!detailClearResult.text.includes("焦点对象")) failures.push("detail clear did not keep selected object context");

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

async function focusObject(cdp, objectId) {
  const result = await cdp.send("Runtime.evaluate", {
    expression: `(() => {
      const element = document.querySelector(${JSON.stringify(`[data-object-id="${objectId}"]`)});
      if (!element) return false;
      element.focus();
      return document.activeElement === element;
    })()`,
    returnByValue: true
  });
  if (!result.result?.value) {
    throw new Error(`Object could not receive focus: ${objectId}`);
  }
}

async function hoverObject(cdp, objectId) {
  const result = await cdp.send("Runtime.evaluate", {
    expression: `(() => {
      const element = document.querySelector(${JSON.stringify(`[data-object-id="${objectId}"]`)});
      if (!element) return false;
      element.dispatchEvent(new MouseEvent("mouseover", { bubbles: true, relatedTarget: null }));
      element.dispatchEvent(new MouseEvent("mouseenter", { bubbles: false, relatedTarget: null }));
      return true;
    })()`,
    returnByValue: true
  });
  if (result.result?.value !== true) {
    throw new Error(`Missing hoverable object ${objectId}`);
  }
}

async function unhoverObject(cdp, objectId) {
  const result = await cdp.send("Runtime.evaluate", {
    expression: `(() => {
      const element = document.querySelector(${JSON.stringify(`[data-object-id="${objectId}"]`)});
      if (!element) return false;
      element.dispatchEvent(new MouseEvent("mouseout", { bubbles: true, relatedTarget: document.body }));
      element.dispatchEvent(new MouseEvent("mouseleave", { bubbles: false, relatedTarget: document.body }));
      return true;
    })()`,
    returnByValue: true
  });
  if (result.result?.value !== true) {
    throw new Error(`Missing unhoverable object ${objectId}`);
  }
}

async function readWireCardPreview(cdp) {
  return evaluateJson(cdp, `(() => {
    const preview = document.querySelector(".wire-card-preview");
    return {
      delayMs: Number(preview?.getAttribute("data-wire-card-preview-delay-ms") ?? "0"),
      exists: Boolean(preview),
      kind: preview?.getAttribute("data-wire-card-preview-kind") ?? null,
      objectId: preview?.getAttribute("data-wire-card-preview-object-id") ?? null,
      orientation: preview?.getAttribute("data-wire-card-preview-orientation") ?? null,
      state: preview?.getAttribute("data-wire-card-preview-state") ?? null
    };
  })()`);
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

async function clickActionMapObject(cdp, objectId) {
  const result = await cdp.send("Runtime.evaluate", {
    expression: `(() => {
      const element = document.querySelector(${JSON.stringify(`[data-action-object-id="${objectId}"]`)});
      if (!element) return false;
      element.click();
      return true;
    })()`,
    returnByValue: true
  });
  if (!result.result?.value) {
    throw new Error(`Action map object chip not found: ${objectId}`);
  }
}

async function clickActionFocusChoiceObject(cdp, objectId) {
  const result = await cdp.send("Runtime.evaluate", {
    expression: `(() => {
      const element = document.querySelector(${JSON.stringify(`[data-action-focus-choice-object-id="${objectId}"]`)});
      if (!element) return false;
      element.click();
      return true;
    })()`,
    returnByValue: true
  });
  if (!result.result?.value) {
    throw new Error(`Action focus choice object not found: ${objectId}`);
  }
}

async function clickActionCandidateStepObject(cdp, objectId) {
  const result = await cdp.send("Runtime.evaluate", {
    expression: `(() => {
      const element = document.querySelector(${JSON.stringify(`[data-action-candidate-step-object-id="${objectId}"]`)});
      if (!element) return false;
      element.click();
      return true;
    })()`,
    returnByValue: true
  });
  if (!result.result?.value) {
    throw new Error(`Action candidate step object not found: ${objectId}`);
  }
}

async function clickCandidateObjectRef(cdp, objectId) {
  const result = await cdp.send("Runtime.evaluate", {
    expression: `(() => {
      const element = document.querySelector(${JSON.stringify(`[data-candidate-object-ref="${objectId}"]`)});
      if (!element) return false;
      element.click();
      return true;
    })()`,
    returnByValue: true
  });
  if (!result.result?.value) {
    throw new Error(`Candidate object ref not found: ${objectId}`);
  }
}

async function clickButtonByText(cdp, text) {
  const result = await cdp.send("Runtime.evaluate", {
    expression: `(() => {
      const element = Array.from(document.querySelectorAll("button")).find((button) => button.textContent?.includes(${JSON.stringify(text)}));
      if (!element) return false;
      element.click();
      return true;
    })()`,
    returnByValue: true
  });
  if (!result.result?.value) {
    throw new Error(`Button not found: ${text}`);
  }
}

async function pressEscape(cdp) {
  await cdp.send("Input.dispatchKeyEvent", {
    type: "keyDown",
    key: "Escape",
    code: "Escape",
    windowsVirtualKeyCode: 27,
    nativeVirtualKeyCode: 27
  });
  await cdp.send("Input.dispatchKeyEvent", {
    type: "keyUp",
    key: "Escape",
    code: "Escape",
    windowsVirtualKeyCode: 27,
    nativeVirtualKeyCode: 27
  });
}

async function clickWireDetail(cdp, detailId) {
  const result = await cdp.send("Runtime.evaluate", {
    expression: `(() => {
      const element = document.querySelector(${JSON.stringify(`[data-wire-detail-id="${detailId}"]`)});
      if (!element) return false;
      element.click();
      return true;
    })()`,
    returnByValue: true
  });
  if (!result.result?.value) {
    throw new Error(`Wire detail trigger not found: ${detailId}`);
  }
}

async function clickTimelineActionHint(cdp) {
  const result = await cdp.send("Runtime.evaluate", {
    expression: `(() => {
      const element = document.querySelector(".wire-timeline-action-hint-button");
      if (!element) return "";
      element.click();
      return element.getAttribute("data-action-hint-object-id") ?? "";
    })()`,
    returnByValue: true
  });
  const objectId = String(result.result?.value ?? "");
  if (!objectId) {
    throw new Error("Wire timeline action hint button not found");
  }
  return objectId;
}

async function clickTimelineCommandBridgeNext(cdp) {
  const result = await cdp.send("Runtime.evaluate", {
    expression: `(() => {
      const element = document.querySelector("[data-timeline-command-bridge-next-object-id]");
      if (!element) return "";
      const objectId = element.getAttribute("data-timeline-command-bridge-next-object-id") ?? "";
      element.click();
      return objectId;
    })()`,
    returnByValue: true
  });
  const objectId = String(result.result?.value ?? "");
  if (!objectId) {
    throw new Error("Wire timeline command bridge next object button not found");
  }
  return objectId;
}

async function clickTimelineCommandBridgeDetail(cdp, objectId) {
  const result = await cdp.send("Runtime.evaluate", {
    expression: `(() => {
      const element = document.querySelector(${JSON.stringify(`[data-timeline-command-open-detail-object-id="${objectId}"]`)});
      if (!element) return "";
      element.click();
      return element.getAttribute("data-timeline-command-open-detail-object-id") ?? "";
    })()`,
    returnByValue: true
  });
  const clickedObjectId = String(result.result?.value ?? "");
  if (!clickedObjectId) {
    throw new Error(`Wire timeline command bridge detail button not found: ${objectId}`);
  }
  return clickedObjectId;
}

async function clickDetailClear(cdp) {
  const result = await cdp.send("Runtime.evaluate", {
    expression: `(() => {
      const element = document.querySelector(".wire-detail-clear");
      if (!element) return false;
      element.click();
      return true;
    })()`,
    returnByValue: true
  });
  if (!result.result?.value) {
    throw new Error("Wire timeline detail clear button not found");
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

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
const acceptedCommandFollowupStates = ["accepted-events", "accepted-silent", "accepted-snapshot"];
const validCommandFollowupStates = ["accepted-awaiting", ...acceptedCommandFollowupStates, "empty", "failed", "pending", "unknown-tick"];

const routes = [
  { path: "/", texts: ["符文战场", "进入大厅"] },
  { path: "/lobby", texts: ["创建或加入", "玩家名称", "房间码"] },
  { path: "/decks", texts: ["本地测试卡组", "等待服务端验证"] },
  { path: "/cards", texts: ["卡牌图鉴", "官方卡牌视图"] },
  { path: "/rooms/stage3-smoke", texts: ["房间", "连接/重连并入座", "选择卡组"] },
  {
    path: "/matches/stage3-smoke",
    texts: ["符文战场对战线框", "等待开局", "窗口总览", "指挥中心", "优先权轨道", "合法操作地图", "候选覆盖审计", "提交审阅", "提交反馈", "候选步骤", "交互语法", "焦点 / 候选 / 规则队列", "规则队列地图", "响应责任时间线", "服务端行动提示", "结算链 / 规则事件", "日志"],
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
  await waitForText(cdp, ["符文战场对战线框", "指挥中心", "合法操作地图", "候选覆盖审计", "提交审阅", "提交反馈", "响应责任时间线", "责任来源：服务端", "焦点 / 候选 / 规则队列"]);
  await runAccessibilitySmoke(cdp, "/matches/local?fixture=layout");
  await runWireLayoutGeometrySmoke(cdp);
  console.log("Chrome smoke OK: wire layout geometry");
  await runWireClickSelectionSmoke(cdp);
  console.log("Chrome smoke OK: wire click selection");
  await navigateAndWait(cdp, `${frontendUrl}/matches/local?fixture=layout`);
  await waitForText(cdp, ["符文战场对战线框", "事件详情", "行动提示"]);
  await runWireTimelineCommandSubmitSmoke(cdp);
  console.log("Chrome smoke OK: wire timeline command submit");
  await navigateAndWait(cdp, `${frontendUrl}/matches/local?fixture=layout&fixtureSubmission=snapshot`);
  await waitForText(cdp, ["符文战场对战线框", "服务端已接受", "快照"]);
  await runAccessibilitySmoke(cdp, "/matches/local?fixture=layout&fixtureSubmission=snapshot");
  await runWireSnapshotSubmissionSmoke(cdp);
  console.log("Chrome smoke OK: wire snapshot submission");
  await navigateAndWait(cdp, `${frontendUrl}/matches/local?fixture=layout&fixtureSubmission=silent`);
  await waitForText(cdp, ["符文战场对战线框", "服务端已接受", "静默接受"]);
  await runAccessibilitySmoke(cdp, "/matches/local?fixture=layout&fixtureSubmission=silent");
  await runWireSilentSubmissionSmoke(cdp);
  console.log("Chrome smoke OK: wire silent submission");
  await navigateAndWait(cdp, `${frontendUrl}/matches/local?fixture=layout&fixtureSubmission=rejected`);
  await waitForText(cdp, ["符文战场对战线框", "服务端拒绝", "提交反馈"]);
  await runAccessibilitySmoke(cdp, "/matches/local?fixture=layout&fixtureSubmission=rejected");
  await runWireRejectedSubmissionSmoke(cdp);
  console.log("Chrome smoke OK: wire rejected submission");
  await navigateAndWait(cdp, `${frontendUrl}/matches/local?fixture=layout&fixtureSubmission=timeline`);
  await waitForText(cdp, ["符文战场对战线框", "规则与事件详情", "服务端已接受", "后续事件"]);
  await runAccessibilitySmoke(cdp, "/matches/local?fixture=layout&fixtureSubmission=timeline");
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
    const validFollowupStates = ${JSON.stringify(validCommandFollowupStates)};
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
    const tableConsistency = document.querySelector("[data-wire-table-consistency-state]");
    const tableConsistencyState = tableConsistency?.getAttribute("data-wire-table-consistency-state") ?? "missing";
    if (tableConsistencyState !== "consistent") {
      failures.push(\`wire table consistency should use shared layout plans, got \${tableConsistencyState}\`);
    }
    const tableConsistencyRows = new Map(Array.from(document.querySelectorAll("[data-wire-table-consistency-row]")).map((row) => [
      row.getAttribute("data-wire-table-consistency-row") ?? "",
      {
        kind: row.getAttribute("data-wire-table-consistency-kind") ?? "",
        state: row.getAttribute("data-wire-table-consistency-state") ?? ""
      }
    ]));
    const expectedConsistencyRows = new Map([
      ["base", "base"],
      ["hand", "hand"],
      ["battlefieldUnit", "battlefield-unit"],
      ["standby", "standby"]
    ]);
    for (const [rowKey, expectedKind] of expectedConsistencyRows.entries()) {
      const row = tableConsistencyRows.get(rowKey);
      if (!row) {
        failures.push(\`wire table consistency row \${rowKey} is missing\`);
        continue;
      }
      if (row.state !== "consistent" || row.kind !== expectedKind) {
        failures.push(\`wire table consistency row \${rowKey} drifted: \${JSON.stringify(row)}\`);
      }
    }
    const tableCapacityRows = new Map(Array.from(document.querySelectorAll("[data-wire-table-capacity-row]")).map((row) => [
      row.getAttribute("data-wire-table-capacity-row") ?? "",
      {
        count: Number(row.getAttribute("data-wire-table-capacity-count") ?? "-1"),
        kind: row.getAttribute("data-wire-table-capacity-kind") ?? "",
        overflow: row.getAttribute("data-wire-table-capacity-overflow") ?? "",
        overflowCount: Number(row.getAttribute("data-wire-table-capacity-overflow-count") ?? "-1"),
        slots: Number(row.getAttribute("data-wire-table-capacity-slots") ?? "-1"),
        state: row.getAttribute("data-wire-table-capacity-state") ?? "",
        visibleSlots: Number(row.getAttribute("data-wire-table-capacity-visible-slots") ?? "-1")
      }
    ]));
    const expectedCapacityRows = new Map([
      ["opponent:base", "base"],
      ["opponent:hand", "hand"],
      ["self:base", "base"],
      ["self:hand", "hand"],
      ["battlefield:0:opponent", "battlefield-unit"],
      ["battlefield:0:self", "battlefield-unit"],
      ["battlefield:0:standby", "standby"],
      ["battlefield:1:opponent", "battlefield-unit"],
      ["battlefield:1:self", "battlefield-unit"],
      ["battlefield:1:standby", "standby"]
    ]);
    for (const [rowKey, expectedKind] of expectedCapacityRows.entries()) {
      const row = tableCapacityRows.get(rowKey);
      if (!row) {
        failures.push(\`wire table capacity row \${rowKey} is missing\`);
        continue;
      }
      if (row.kind !== expectedKind) {
        failures.push(\`wire table capacity row \${rowKey} has wrong kind: \${row.kind}\`);
      }
      if (!["empty", "stable", "scroll"].includes(row.state)) {
        failures.push(\`wire table capacity row \${rowKey} has wrong state: \${row.state}\`);
      }
      if (!["none", "scroll"].includes(row.overflow)) {
        failures.push(\`wire table capacity row \${rowKey} has wrong overflow: \${row.overflow}\`);
      }
      if (row.count < 0 || row.slots < row.count || row.visibleSlots > row.slots || row.overflowCount !== Math.max(0, row.slots - row.visibleSlots)) {
        failures.push(\`wire table capacity row \${rowKey} has invalid counts: \${JSON.stringify(row)}\`);
      }
    }
    const selectedLayout = document.querySelector("[data-wire-table-selected-layout-state]");
    const selectedLayoutState = selectedLayout?.getAttribute("data-wire-table-selected-layout-state") ?? "missing";
    if (selectedLayoutState !== "empty") {
      failures.push(\`wire table selected layout should start empty, got \${selectedLayoutState}\`);
    }
    if ((selectedLayout?.getAttribute("data-wire-table-selected-layout-kind") ?? "") !== "none") {
      failures.push("wire table selected layout empty state should use none kind");
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

    const commandReview = document.querySelector("[data-command-review-state]");
    const commandReviewState = commandReview?.getAttribute("data-command-review-state") ?? "missing";
    if (!["blocked", "drafting", "empty", "ready"].includes(commandReviewState)) {
      failures.push(\`wire command review state is unsupported: \${commandReviewState}\`);
    }
    const commandReviewFieldStates = Array.from(commandReview?.querySelectorAll("[data-command-review-field-state]") ?? [])
      .map((field) => field.getAttribute("data-command-review-field-state") ?? "missing");
    for (const fieldState of commandReviewFieldStates) {
      if (!["covered", "missing", "optional", "server"].includes(fieldState)) {
        failures.push(\`wire command review field state is unsupported: \${fieldState}\`);
      }
    }

    const commandSubmission = document.querySelector("[data-command-submission-state]");
    const commandSubmissionState = commandSubmission?.getAttribute("data-command-submission-state") ?? "missing";
    if (!["empty", "failed", "sent", "submitting"].includes(commandSubmissionState)) {
      failures.push(\`wire command submission state is unsupported: \${commandSubmissionState}\`);
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
    for (const rowKey of ["candidates", "commandTemplates", "composerSupport", "objectContexts", "contract", "submissionGate"]) {
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

    const commandCenter = document.querySelector("[data-wire-command-center-state]");
    const commandCenterState = commandCenter?.getAttribute("data-wire-command-center-state") ?? "missing";
    const commandCenterStepRole = commandCenter?.getAttribute("data-wire-command-center-step-role") ?? "missing";
    if (!["blocked", "no-focus", "observe", "ready", "selecting"].includes(commandCenterState)) {
      failures.push(\`wire command center state is unsupported: \${commandCenterState}\`);
    }
    if (!["destination", "mode", "optionalCost", "source", "submit", "sync", "target", "wait", "window"].includes(commandCenterStepRole)) {
      failures.push(\`wire command center step role is unsupported: \${commandCenterStepRole}\`);
    }
    const commandCenterRows = new Map(Array.from(document.querySelectorAll("[data-wire-command-center-row]")).map((row) => [
      row.getAttribute("data-wire-command-center-row") ?? "",
      row.getAttribute("data-wire-command-center-row-state") ?? ""
    ]));
    for (const rowKey of ["window", "focus", "candidate", "command", "submit", "feedback"]) {
      if (!commandCenterRows.get(rowKey)) {
        failures.push(\`wire command center row \${rowKey} is missing\`);
      }
    }
    const commandCenterFollowup = commandCenter?.querySelector("[data-command-followup-state]");
    const commandCenterFollowupState = commandCenterFollowup?.getAttribute("data-command-followup-state") ?? "missing";
    const commandCenterFollowupServerState = commandCenterFollowup?.getAttribute("data-command-followup-server-state") ?? "missing";
    const commandCenterFollowupBridge = commandCenterFollowup?.querySelector("[data-command-followup-bridge-state]");
    const commandCenterFollowupBridgeState = commandCenterFollowupBridge?.getAttribute("data-command-followup-bridge-state") ?? "missing";
    const commandCenterFollowupLayout = commandCenterFollowup?.querySelector("[data-command-followup-layout-state]");
    const commandCenterFollowupLayoutState = commandCenterFollowupLayout?.getAttribute("data-command-followup-layout-state") ?? "missing";
    if (!validFollowupStates.includes(commandCenterFollowupState)) {
      failures.push(\`wire command center followup state is unsupported: \${commandCenterFollowupState}\`);
    }
    if (commandCenterFollowupServerState === "missing" || commandCenterFollowupServerState.length === 0) {
      failures.push("wire command center followup server state is missing");
    }
    if (!["empty", "failed", "ready", "unknown", "waiting"].includes(commandCenterFollowupBridgeState)) {
      failures.push(\`wire command center followup bridge state is unsupported: \${commandCenterFollowupBridgeState}\`);
    }
    if (!["empty", "hidden-only", "linked", "unknown"].includes(commandCenterFollowupLayoutState)) {
      failures.push(\`wire command center followup layout projection state is unsupported: \${commandCenterFollowupLayoutState}\`);
    }
    for (const rowKey of ["serverState", "tick", "events", "snapshot", "prompt"]) {
      if (!commandCenterFollowupBridge?.querySelector(\`[data-command-followup-bridge-row="\${rowKey}"]\`)) {
        failures.push(\`wire command center followup bridge row \${rowKey} is missing\`);
      }
    }

    const responsibilitySource = document.querySelector("[data-wire-window-responsibility-source]")?.getAttribute("data-wire-window-responsibility-source") ?? "missing";
    if (responsibilitySource !== "server") {
      failures.push(\`wire turn window did not use server responsibility metadata: \${responsibilitySource}\`);
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

    const overview = document.querySelector("[data-wire-match-overview-state]");
    const overviewState = overview?.getAttribute("data-wire-match-overview-state") ?? "missing";
    if (!["blocked", "disconnected", "ready", "review", "resolving", "waiting"].includes(overviewState)) {
      failures.push(\`wire match overview state is unsupported: \${overviewState}\`);
    }
    const overviewRows = new Map(Array.from(overview?.querySelectorAll("[data-wire-match-overview-row]") ?? []).map((row) => [
      row.getAttribute("data-wire-match-overview-row") ?? "",
      row.getAttribute("data-wire-match-overview-row-state") ?? ""
    ]));
    for (const rowKey of ["window", "candidates", "rules", "focus", "timeline"]) {
      if (!overviewRows.get(rowKey)) {
        failures.push(\`wire match overview row \${rowKey} is missing\`);
      }
    }
    if ((overview?.querySelectorAll("[data-wire-match-overview-metric]").length ?? 0) < 4) {
      failures.push("wire match overview metric strip missing");
    }

    const expectedSidePanelSlots = [
      "overview",
      "turnWindow",
      "commandCenter",
      "serverFlow",
      "responseCoach",
      "tableAuthority",
      "informationBoundary",
      "promptAuthority",
      "actionMap",
      "interaction",
      "ruleQueue",
      "timelineDetail",
      "actionPrompt",
      "log"
    ];
    const sidePanelSlots = Array.from(document.querySelectorAll("[data-wire-side-panel-slot]"))
      .map((slot) => slot.getAttribute("data-wire-side-panel-slot") ?? "");
    if (sidePanelSlots.join("|") !== expectedSidePanelSlots.join("|")) {
      failures.push(\`wire side panel slot order drifted: \${sidePanelSlots.join(" -> ")}\`);
    }
    const sidePanelDirectoryLinks = Array.from(document.querySelectorAll("[data-wire-side-panel-directory-link]")).map((link) => ({
      href: link.getAttribute("href") ?? "",
      label: link.textContent?.trim() ?? "",
      slot: link.getAttribute("data-wire-side-panel-directory-link") ?? ""
    }));
    const sidePanelDirectoryLinkSlots = sidePanelDirectoryLinks.map((link) => link.slot);
    if (sidePanelDirectoryLinkSlots.join("|") !== expectedSidePanelSlots.join("|")) {
      failures.push(\`wire side panel directory order drifted: \${sidePanelDirectoryLinkSlots.join(" -> ")}\`);
    }
    for (const link of sidePanelDirectoryLinks) {
      const expectedAnchorId = \`wire-side-panel-\${link.slot}\`;
      if (link.href !== \`#\${expectedAnchorId}\`) {
        failures.push(\`wire side panel directory link \${link.slot} has href \${link.href}\`);
      }
      if (!document.getElementById(expectedAnchorId)) {
        failures.push(\`wire side panel directory target missing: \${expectedAnchorId}\`);
      }
      if (link.label.length === 0) {
        failures.push(\`wire side panel directory link label missing: \${link.slot}\`);
      }
    }

    return {
      failures,
      fixedPileCount: document.querySelectorAll(".wire-fixed-pile").length,
      flowCount: document.querySelectorAll(".wire-card-flow").length,
      commandReviewState,
      commandSubmissionState,
      commandCenterState,
      commandCenterFollowupState,
      commandCenterFollowupServerState,
      commandCenterFollowupBridgeState,
      commandCenterFollowupLayoutState,
      informationBoundaryState,
      promptAuthorityState,
      quickActionCount: quickActions.size,
      responsibilitySource,
      responseCoachState,
      ruleAuthorityState,
      sidePanelDirectoryCount: sidePanelDirectoryLinks.length,
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
  if ((result.sidePanelDirectoryCount ?? 0) < 13) {
    throw new Error("Wire layout geometry smoke did not find side panel directory links");
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
  if (!["blocked", "drafting", "empty", "ready"].includes(result.commandReviewState)) {
    throw new Error(`Wire layout geometry smoke did not find command review: ${result.commandReviewState}`);
  }
  if (!["empty", "failed", "sent", "submitting"].includes(result.commandSubmissionState)) {
    throw new Error(`Wire layout geometry smoke did not find command submission feedback: ${result.commandSubmissionState}`);
  }
  if (result.responsibilitySource !== "server") {
    throw new Error(`Wire layout geometry smoke did not find server responsibility metadata: ${result.responsibilitySource}`);
  }
  if (!["blocked", "opponent", "ready", "resolving", "selecting", "waiting"].includes(result.responseCoachState)) {
    throw new Error(`Wire layout geometry smoke did not find response coach: ${result.responseCoachState}`);
  }
  if (!["blocked", "no-focus", "observe", "ready", "selecting"].includes(result.commandCenterState)) {
    throw new Error(`Wire layout geometry smoke did not find command center: ${result.commandCenterState}`);
  }
  if (!validCommandFollowupStates.includes(result.commandCenterFollowupState)) {
    throw new Error(`Wire layout geometry smoke did not find command center followup: ${result.commandCenterFollowupState}`);
  }
  if (!result.commandCenterFollowupServerState || result.commandCenterFollowupServerState === "missing") {
    throw new Error("Wire layout geometry smoke did not find command center followup server state");
  }
  if (!["empty", "hidden-only", "linked", "unknown"].includes(result.commandCenterFollowupLayoutState)) {
    throw new Error(`Wire layout geometry smoke did not find command center followup layout projection: ${result.commandCenterFollowupLayoutState}`);
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
    const commandCenter = document.querySelector(".wire-command-center");
    const commandCenterFollowup = commandCenter?.querySelector("[data-command-followup-state]");
    const commandCenterFollowupLayout = commandCenterFollowup?.querySelector("[data-command-followup-layout-state]");
    const sourceObject = document.querySelector('[data-object-id="p1-hand-spell"]');
    return {
      state: summary?.getAttribute("data-wire-focused-action-state") ?? null,
      text: summary?.textContent ?? "",
      sourcePromptNext: sourceObject?.getAttribute("data-prompt-next") ?? null,
      sourcePromptRoles: sourceObject?.getAttribute("data-prompt-role-labels") ?? null,
      sourcePromptSummary: sourceObject?.getAttribute("data-prompt-summary") ?? null,
      contextAuthority: document.querySelector(".wire-object-context")?.getAttribute("data-wire-object-context-authority") ?? null,
      contextSource: document.querySelector(".wire-object-context")?.getAttribute("data-wire-object-context-source") ?? null,
      contextText: document.querySelector(".wire-object-context")?.textContent ?? "",
      contextSectionCount: document.querySelectorAll(".wire-object-context [data-wire-object-section]").length,
      contextSections: Array.from(document.querySelectorAll(".wire-object-context [data-wire-object-section]"))
        .map((node) => [
          node.getAttribute("data-wire-object-section") ?? "",
          node.getAttribute("data-wire-object-section-state") ?? "",
          node.getAttribute("data-wire-object-section-count") ?? ""
        ].join(":")),
      objectCommandComposerStates: Array.from(document.querySelectorAll(".wire-object-context [data-wire-object-command-composer-state]"))
        .map((node) => node.getAttribute("data-wire-object-command-composer-state")),
      grammarComposerState: document.querySelector(".wire-focused-grammar")?.getAttribute("data-wire-focused-grammar-composer-state") ?? null,
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
      composerCanSubmitStates: Array.from(document.querySelectorAll(".wire-focused-actions .candidate-composer"))
        .map((node) => node.getAttribute("data-candidate-composer-can-submit")),
      composerGateStates: Array.from(document.querySelectorAll(".wire-focused-actions .candidate-composer"))
        .map((node) => node.getAttribute("data-candidate-composer-gate-state")),
      composerCheckStates: Array.from(document.querySelectorAll(".wire-focused-actions [data-candidate-composer-check-state]"))
        .map((node) => node.getAttribute("data-candidate-composer-check-state")),
      composerGateText: document.querySelector(".wire-focused-actions .candidate-composer")?.textContent ?? "",
      commandCenterActionCount: Number(document.querySelector("[data-wire-command-center-action-count]")?.getAttribute("data-wire-command-center-action-count") ?? "0"),
      commandCenterRows: Array.from(document.querySelectorAll("[data-wire-command-center-row]")).map((node) =>
        (node.getAttribute("data-wire-command-center-row") ?? "") + ":" + (node.getAttribute("data-wire-command-center-row-state") ?? "")),
      commandCenterState: document.querySelector("[data-wire-command-center-state]")?.getAttribute("data-wire-command-center-state") ?? null,
      commandCenterStepRole: document.querySelector("[data-wire-command-center-step-role]")?.getAttribute("data-wire-command-center-step-role") ?? null,
      commandCenterFollowupState: commandCenterFollowup?.getAttribute("data-command-followup-state") ?? null,
      commandCenterFollowupServerState: commandCenterFollowup?.getAttribute("data-command-followup-server-state") ?? null,
      commandCenterFollowupBridgeState: commandCenterFollowup?.querySelector("[data-command-followup-bridge-state]")?.getAttribute("data-command-followup-bridge-state") ?? null,
      commandCenterFollowupBridgeRows: Array.from(commandCenterFollowup?.querySelectorAll("[data-command-followup-bridge-row]") ?? [])
        .map((node) => (node.getAttribute("data-command-followup-bridge-row") ?? "") + ":" + (node.getAttribute("data-command-followup-bridge-row-state") ?? "")),
      commandCenterFollowupLayoutHiddenCount: Number(commandCenterFollowupLayout?.getAttribute("data-command-followup-layout-hidden-count") ?? "0"),
      commandCenterFollowupLayoutLocatedCount: Number(commandCenterFollowupLayout?.getAttribute("data-command-followup-layout-located-count") ?? "0"),
      commandCenterFollowupLayoutState: commandCenterFollowupLayout?.getAttribute("data-command-followup-layout-state") ?? null,
      commandCenterFollowupLayoutText: commandCenterFollowupLayout?.textContent ?? "",
      commandCenterFollowupLayoutTotalCount: Number(commandCenterFollowupLayout?.getAttribute("data-command-followup-layout-total-count") ?? "0"),
      commandCenterFollowupMetricCount: commandCenterFollowup?.querySelectorAll("[data-command-followup-metric]").length ?? 0,
      commandCenterFollowupText: commandCenterFollowup?.textContent ?? "",
      commandCenterText: commandCenter?.textContent ?? "",
      selectedLayoutCapacityRow: document.querySelector("[data-wire-table-selected-layout-state]")?.getAttribute("data-wire-table-selected-layout-capacity-row") ?? null,
      selectedLayoutKind: document.querySelector("[data-wire-table-selected-layout-state]")?.getAttribute("data-wire-table-selected-layout-kind") ?? null,
      selectedLayoutObject: document.querySelector("[data-wire-table-selected-layout-state]")?.getAttribute("data-wire-table-selected-layout-object") ?? null,
      selectedLayoutSource: document.querySelector("[data-wire-table-selected-layout-state]")?.getAttribute("data-wire-table-selected-layout-source") ?? null,
      selectedLayoutState: document.querySelector("[data-wire-table-selected-layout-state]")?.getAttribute("data-wire-table-selected-layout-state") ?? null,
      selectedLayoutText: document.querySelector("[data-wire-table-selected-layout-state]")?.textContent ?? "",
      selectedLayoutZone: document.querySelector("[data-wire-table-selected-layout-state]")?.getAttribute("data-wire-table-selected-layout-zone") ?? null,
      traySelectionRows: Array.from(document.querySelectorAll(".wire-object-command-tray-selection li")).map((node) => ({
        choice: node.getAttribute("data-wire-object-command-tray-selection-choice") ?? "",
        objectIds: node.getAttribute("data-wire-object-command-tray-selection-object-ids") ?? "",
        role: node.getAttribute("data-wire-object-command-tray-selection") ?? "",
        text: node.textContent ?? ""
      })),
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
    const actionRoutes = detail?.querySelector(".detail-action-routes");
    const checkMap = detail?.querySelector("[data-card-detail-check-map]");
    return {
      actionCount: Number(actions?.querySelector("[data-card-detail-action-count]")?.getAttribute("data-card-detail-action-count") ?? "0"),
      actionModes: Array.from(actions?.querySelectorAll("[data-card-detail-action-mode]") ?? []).map((node) => node.getAttribute("data-card-detail-action-mode")),
      actionRouteCount: Number(actionRoutes?.getAttribute("data-card-detail-route-count") ?? "0"),
      actionRouteEntryKeys: Array.from(actionRoutes?.querySelectorAll("[data-card-detail-action-route-entry]") ?? [])
        .map((node) => node.getAttribute("data-card-detail-action-route-entry") ?? ""),
      actionRouteKeys: Array.from(actionRoutes?.querySelectorAll("[data-card-detail-action-route]") ?? [])
        .map((node) => node.getAttribute("data-card-detail-action-route") ?? ""),
      actionRouteReviewButtonCount: actionRoutes?.querySelectorAll("[data-card-detail-action-route-review]").length ?? 0,
      actionRouteStates: Array.from(actionRoutes?.querySelectorAll("[data-card-detail-action-route-state]") ?? [])
        .map((node) => node.getAttribute("data-card-detail-action-route-state")),
      actionRouteText: actionRoutes?.textContent ?? "",
      actionSource: actions?.getAttribute("data-card-detail-actions-source") ?? "",
      actionState: actions?.getAttribute("data-card-detail-actions-state") ?? null,
      actionSummaryKeys: Array.from(actions?.querySelectorAll("[data-card-detail-action-summary]") ?? []).map((node) => node.getAttribute("data-card-detail-action-summary")),
      actionText: actions?.textContent ?? "",
      activeText: document.activeElement?.textContent ?? "",
      checkMapCount: Number(checkMap?.getAttribute("data-card-detail-check-map-count") ?? "0"),
      checkMapMode: checkMap?.getAttribute("data-card-detail-check-map") ?? null,
      checkRows: Array.from(checkMap?.querySelectorAll("[data-card-detail-check-row]") ?? [])
        .map((node) => [
          node.getAttribute("data-card-detail-check-row") ?? "",
          node.getAttribute("data-card-detail-check-row-state") ?? "",
          node.getAttribute("data-card-detail-check-row-count") ?? ""
        ].join(":")),
      groups: Array.from(inspector?.querySelectorAll("[data-card-detail-inspector-group]") ?? []).map((node) => node.getAttribute("data-card-detail-inspector-group")),
      inspectorAuthority: inspector?.getAttribute("data-card-detail-inspector-authority") ?? null,
      inspectorOpen: Boolean(inspector),
      inspectorSource: inspector?.getAttribute("data-card-detail-inspector-source") ?? null,
      inspectorText: inspector?.textContent ?? "",
      labelledBy: detail?.getAttribute("aria-labelledby") ?? "",
      state: detail?.getAttribute("data-detail-dialog-state") ?? null,
      summaryKeys: Array.from(inspector?.querySelectorAll("[data-card-detail-inspector-summary]") ?? []).map((node) => node.getAttribute("data-card-detail-inspector-summary")),
      text: detail?.textContent ?? "",
      open: Boolean(detail)
    };
  })()`);
  const clickedDetailRouteReview = await evaluateJson(cdp, `(() => {
    const element = document.querySelector("[data-card-detail-action-route-review]");
    if (!(element instanceof HTMLButtonElement) || element.disabled) return false;
    element.click();
    return true;
  })()`);
  await delay(100);
  const detailReviewResult = await evaluateJson(cdp, `(() => {
    const detail = document.querySelector(".detail-layer");
    const review = detail?.querySelector("[data-card-detail-action-review-state]");
    const submit = review?.querySelector("[data-card-detail-action-review-submit-state]");
    return {
      command: review?.getAttribute("data-card-detail-action-review-command") ?? "",
      clicked: ${JSON.stringify(Boolean(clickedDetailRouteReview))},
      entry: review?.getAttribute("data-card-detail-action-review-entry") ?? "",
      open: Boolean(review),
      routeState: review?.getAttribute("data-card-detail-action-review-route-state") ?? "",
      rows: Array.from(review?.querySelectorAll("[data-card-detail-action-review-row]") ?? [])
        .map((node) => node.getAttribute("data-card-detail-action-review-row") ?? ""),
      source: review?.getAttribute("data-card-detail-action-review-source") ?? "",
      state: review?.getAttribute("data-card-detail-action-review-state") ?? "",
      submitState: submit?.getAttribute("data-card-detail-action-review-submit-state") ?? "",
      text: review?.textContent ?? ""
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
      grammarComposerState: document.querySelector(".wire-focused-grammar")?.getAttribute("data-wire-focused-grammar-composer-state") ?? null,
      grammarState: document.querySelector(".wire-focused-grammar")?.getAttribute("data-wire-focused-grammar-state") ?? null,
      grammarText: document.querySelector(".wire-focused-grammar")?.textContent ?? "",
      commandCenterState: document.querySelector("[data-wire-command-center-state]")?.getAttribute("data-wire-command-center-state") ?? null,
      previewText: document.querySelector(".candidate-command-preview")?.textContent ?? "",
      selectionRows: Array.from(document.querySelectorAll(".wire-selection-row-list li")).map((node) => ({
        choice: node.getAttribute("data-wire-selection-row-choice") ?? "",
        objectIds: node.getAttribute("data-wire-selection-row-object-ids") ?? "",
        role: node.getAttribute("data-wire-selection-row") ?? "",
        text: node.textContent ?? ""
      })),
      traySelectionRows: Array.from(document.querySelectorAll(".wire-object-command-tray-selection li")).map((node) => ({
        choice: node.getAttribute("data-wire-object-command-tray-selection-choice") ?? "",
        objectIds: node.getAttribute("data-wire-object-command-tray-selection-object-ids") ?? "",
        role: node.getAttribute("data-wire-object-command-tray-selection") ?? "",
        text: node.textContent ?? ""
      })),
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
    const actionPromptInspection = document.querySelector("[data-action-prompt-inspection]");
    const priorityRail = document.querySelector(".wire-priority-rail");
    const ruleQueue = document.querySelector(".wire-rule-queue");
    const ruleFocus = document.querySelector(".wire-rule-focus");
    const ruleFlow = document.querySelector(".wire-rule-flow");
    const focusBridge = document.querySelector(".wire-action-focus-bridge");
    const route = document.querySelector("[data-action-route-state]");
    const commandReview = document.querySelector("[data-command-review-state]");
    const commandSubmission = document.querySelector("[data-command-submission-state]");
    const commandSubmissionLayout = commandSubmission?.querySelector("[data-command-followup-layout-state]");
    const actionButtons = document.querySelector(".wire-action-panel .action-buttons");
    const layoutProjection = document.querySelector("[data-action-layout-projection-state]");
    return {
      selected: tableObject?.getAttribute("data-selected") ?? null,
      actionMapText: document.querySelector(".wire-action-map")?.textContent ?? "",
      actionLayoutProjectionLocatedCount: Number(layoutProjection?.getAttribute("data-action-layout-projection-located-count") ?? "0"),
      actionLayoutProjectionReadyCount: Number(layoutProjection?.getAttribute("data-action-layout-projection-ready-count") ?? "0"),
      actionLayoutProjectionRows: Array.from(layoutProjection?.querySelectorAll("[data-action-layout-projection-row]") ?? []).map((node) => ({
        capacityRow: node.getAttribute("data-action-layout-projection-capacity-row") ?? "",
        kind: node.getAttribute("data-action-layout-projection-kind") ?? "",
        objectId: node.getAttribute("data-action-layout-projection-object") ?? "",
        role: node.getAttribute("data-action-layout-projection-role") ?? "",
        selected: node.getAttribute("data-action-layout-projection-selected") ?? "",
        source: node.getAttribute("data-action-layout-projection-source") ?? "",
        state: node.getAttribute("data-action-layout-projection-state") ?? "",
        zone: node.getAttribute("data-action-layout-projection-zone") ?? ""
      })),
      actionLayoutProjectionState: layoutProjection?.getAttribute("data-action-layout-projection-state") ?? null,
      actionLayoutProjectionText: layoutProjection?.textContent ?? "",
      actionLayoutProjectionTotalCount: Number(layoutProjection?.getAttribute("data-action-layout-projection-total-count") ?? "0"),
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
      commandReviewFieldStates: Array.from(commandReview?.querySelectorAll("[data-command-review-field-state]") ?? [])
        .map((node) => node.getAttribute("data-command-review-field-state")),
      commandReviewState: commandReview?.getAttribute("data-command-review-state") ?? null,
      commandReviewSubmitDisabled: commandReview?.querySelector(".wire-command-review-submit")?.hasAttribute("disabled") ?? null,
      commandReviewSubmitState: commandReview?.querySelector(".wire-command-review-submit")?.getAttribute("data-command-review-submit-state") ?? null,
      commandReviewText: commandReview?.textContent ?? "",
      commandSubmissionOpenLayerDisabled: commandSubmission?.querySelector(".wire-command-submission-open-layer")?.hasAttribute("disabled") ?? null,
      commandSubmissionOpenLayerState: commandSubmission?.querySelector(".wire-command-submission-open-layer")?.getAttribute("data-command-submission-open-layer-state") ?? null,
      commandSubmissionLayoutState: commandSubmissionLayout?.getAttribute("data-command-followup-layout-state") ?? null,
      commandSubmissionLayoutText: commandSubmissionLayout?.textContent ?? "",
      commandSubmissionLayoutTotalCount: Number(commandSubmissionLayout?.getAttribute("data-command-followup-layout-total-count") ?? "0"),
      commandSubmissionState: commandSubmission?.getAttribute("data-command-submission-state") ?? null,
      commandSubmissionText: commandSubmission?.textContent ?? "",
      detailLayerOpen: Boolean(document.querySelector(".detail-layer")),
      focusBridgeState: focusBridge?.getAttribute("data-action-focus-state") ?? null,
      focusBridgeText: focusBridge?.textContent ?? "",
      focusText: document.querySelector(".wire-focused-action-summary")?.textContent ?? "",
      windowState: windowPlan?.getAttribute("data-wire-window-state") ?? null,
      windowText: windowPlan?.textContent ?? "",
      promptInspectionGroups: Array.from(document.querySelectorAll("[data-wire-prompt-inspection-group]")).map((node) => node.getAttribute("data-wire-prompt-inspection-group")),
      promptInspectionSummaryKeys: Array.from(document.querySelectorAll("[data-wire-prompt-inspection-summary]")).map((node) => node.getAttribute("data-wire-prompt-inspection-summary")),
      promptInspectionText: promptInspection?.textContent ?? "",
      actionPromptInspectionGroups: Array.from(actionPromptInspection?.querySelectorAll("[data-action-prompt-inspection-group]") ?? []).map((node) => node.getAttribute("data-action-prompt-inspection-group")),
      actionPromptInspectionSummaryKeys: Array.from(actionPromptInspection?.querySelectorAll("[data-action-prompt-inspection-summary]") ?? []).map((node) => node.getAttribute("data-action-prompt-inspection-summary")),
      actionPromptInspectionText: actionPromptInspection?.textContent ?? "",
      evidenceKeys: Array.from(document.querySelectorAll("[data-window-evidence-key]")).map((node) => node.getAttribute("data-window-evidence-key")),
      evidenceStackState: document.querySelector('[data-window-evidence-key="stack"]')?.getAttribute("data-window-evidence-state") ?? null,
      evidenceTaskState: document.querySelector('[data-window-evidence-key="tasks"]')?.getAttribute("data-window-evidence-state") ?? null,
      evidenceText: evidence?.textContent ?? "",
      priorityMode: windowPlan?.getAttribute("data-wire-priority-mode") ?? null,
      priorityRailText: priorityRail?.textContent ?? "",
      priorityActiveStep: document.querySelector('[data-priority-step-state="active"]')?.getAttribute("data-priority-step") ?? null,
      ruleFlowText: ruleFlow?.textContent ?? "",
      serverFlowActionCandidates: Array.from(document.querySelectorAll("[data-server-flow-action-candidates]"))
        .map((node) => node.getAttribute("data-server-flow-action-candidates") ?? ""),
      ruleFocusDetailId: ruleFocus?.getAttribute("data-rule-focus-detail-id") ?? null,
      ruleFocusLane: ruleFocus?.getAttribute("data-rule-focus-lane") ?? null,
      ruleFocusActionCount: document.querySelectorAll("[data-rule-focus-action-object-id]").length,
      ruleFocusActionAuthorities: Array.from(document.querySelectorAll("[data-rule-focus-action-authority]"))
        .map((node) => node.getAttribute("data-rule-focus-action-authority")),
      ruleFocusActionObjectIds: Array.from(document.querySelectorAll("[data-rule-focus-action-object-id]"))
        .map((node) => node.getAttribute("data-rule-focus-action-object-id")),
      ruleFocusActionSelectedStates: Array.from(document.querySelectorAll("[data-rule-focus-action-selected]"))
        .map((node) => node.getAttribute("data-rule-focus-action-selected")),
      ruleFocusActionStates: Array.from(document.querySelectorAll("[data-rule-focus-action-state]"))
        .map((node) => node.getAttribute("data-rule-focus-action-state")),
      ruleFocusActionSteps: Array.from(document.querySelectorAll("[data-rule-focus-action-steps]"))
        .map((node) => node.getAttribute("data-rule-focus-action-steps")),
      ruleFocusActionText: document.querySelector(".wire-rule-focus-action-bridge")?.textContent ?? "",
      ruleFocusRefCount: ruleFocus?.querySelectorAll("[data-rule-object-ref]").length ?? 0,
      ruleFocusText: ruleFocus?.textContent ?? "",
      ruleLaneCount: document.querySelectorAll("[data-rule-lane]").length,
      ruleLaneDetailIds: Array.from(document.querySelectorAll("[data-rule-lane-detail-id]"))
        .map((node) => node.getAttribute("data-rule-lane-detail-id") ?? ""),
      ruleCoverageDetailIds: Array.from(document.querySelectorAll("[data-rule-coverage-detail-id]"))
        .map((node) => node.getAttribute("data-rule-coverage-detail-id") ?? ""),
      ruleSectionKeys: Array.from(document.querySelectorAll("[data-rule-section-key]")).map((node) => node.getAttribute("data-rule-section-key")),
      ruleItemKeys: Array.from(document.querySelectorAll("[data-rule-item-key]")).map((node) => node.getAttribute("data-rule-item-key")),
      ruleStackItemText: document.querySelector('[data-rule-item-key^="stack:"]')?.textContent ?? "",
      routeCheckStates: Array.from(route?.querySelectorAll("[data-route-check-state]") ?? [])
        .map((node) => node.getAttribute("data-route-check-state")),
      routeState: route?.getAttribute("data-action-route-state") ?? null,
      routeText: route?.textContent ?? "",
      ruleQueueState: ruleQueue?.getAttribute("data-wire-rule-queue-state") ?? null,
      ruleSequenceCount: document.querySelectorAll("[data-rule-sequence-lane]").length,
      ruleSequenceDetailIds: Array.from(document.querySelectorAll("[data-rule-sequence-detail-id]"))
        .map((node) => node.getAttribute("data-rule-sequence-detail-id") ?? ""),
      ruleResponsibilityDetailIds: Array.from(document.querySelectorAll("[data-rule-responsibility-detail-id]"))
        .map((node) => node.getAttribute("data-rule-responsibility-detail-id") ?? ""),
      ruleSequenceRefCount: document.querySelectorAll(".wire-rule-sequence [data-rule-object-ref]").length
    };
  })()`);

  await clickButtonByText(cdp, "展开规则检查");
  await delay(150);
  const ruleInspectorResult = await evaluateJson(cdp, `(() => {
    const inspector = document.querySelector(".wire-rule-inspector");
    return {
      hidden: inspector?.hasAttribute("hidden") ?? true,
      laneCount: inspector?.querySelectorAll("[data-rule-inspector-lane]").length ?? 0,
      laneDetailIds: Array.from(inspector?.querySelectorAll("[data-rule-inspector-lane-detail-id]") ?? [])
        .map((node) => node.getAttribute("data-rule-inspector-lane-detail-id") ?? ""),
      sequenceDetailIds: Array.from(inspector?.querySelectorAll("[data-rule-inspector-sequence-detail-id]") ?? [])
        .map((node) => node.getAttribute("data-rule-inspector-sequence-detail-id") ?? ""),
      sequenceRefCount: inspector?.querySelectorAll(".wire-rule-inspector-sequence [data-rule-object-ref]").length ?? 0,
      sequenceCount: inspector?.querySelectorAll("[data-rule-inspector-sequence-lane]").length ?? 0,
      text: inspector?.textContent ?? "",
      toggleExpanded: document.querySelector("[data-rule-inspector-toggle]")?.getAttribute("aria-expanded") ?? null
    };
  })()`);

  const ruleSequenceDetailTarget = await firstScopedWireDetailId(cdp, ".wire-rule-sequence");
  const ruleSequenceDetailClickId = await clickScopedWireDetail(cdp, ".wire-rule-sequence", ruleSequenceDetailTarget);
  await delay(150);
  const ruleSequenceDetailResult = await timelineDetailSummary(cdp);
  const ruleLaneDetailTarget = await firstScopedWireDetailId(cdp, ".wire-rule-lanes");
  const ruleLaneDetailClickId = await clickScopedWireDetail(cdp, ".wire-rule-lanes", ruleLaneDetailTarget);
  await delay(150);
  const ruleLaneDetailResult = await timelineDetailSummary(cdp);
  const ruleCoverageDetailTarget = await firstScopedWireDetailId(cdp, ".wire-rule-coverage");
  const ruleCoverageDetailClickId = await clickScopedWireDetail(cdp, ".wire-rule-coverage", ruleCoverageDetailTarget);
  await delay(150);
  const ruleCoverageDetailResult = await timelineDetailSummary(cdp);
  const ruleResponsibilityDetailTarget = await firstScopedWireDetailId(cdp, ".wire-rule-responsibility");
  const ruleResponsibilityDetailClickId = await clickScopedWireDetail(cdp, ".wire-rule-responsibility", ruleResponsibilityDetailTarget);
  await delay(150);
  const ruleResponsibilityDetailResult = await timelineDetailSummary(cdp);
  const ruleInspectorLaneDetailTarget = await firstScopedWireDetailId(cdp, ".wire-rule-inspector-lanes");
  const ruleInspectorLaneDetailClickId = await clickScopedWireDetail(cdp, ".wire-rule-inspector-lanes", ruleInspectorLaneDetailTarget);
  await delay(150);
  const ruleInspectorLaneDetailResult = await timelineDetailSummary(cdp);
  const ruleInspectorSequenceDetailTarget = await firstScopedWireDetailId(cdp, ".wire-rule-inspector-sequence");
  const ruleInspectorSequenceDetailClickId = await clickScopedWireDetail(cdp, ".wire-rule-inspector-sequence", ruleInspectorSequenceDetailTarget);
  await delay(150);
  const ruleInspectorSequenceDetailResult = await timelineDetailSummary(cdp);

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
    const commandReview = document.querySelector("[data-command-review-state]");
    const objectRouteReview = document.querySelector(".wire-focused-actions .wire-object-route-review");
    const trayRouteReview = document.querySelector(".wire-object-command-tray .wire-object-route-review");
    const sourceStep = candidatePlan?.querySelector('[data-step-role="source"]');
    const targetStep = candidatePlan?.querySelector('[data-step-role="target"]');
    const targetRouteStep = route?.querySelector('[data-route-step-role="target"]');
    return {
      candidateDraftActive: candidatePlan?.getAttribute("data-candidate-plan-draft-active") ?? null,
      detailLayerOpen: Boolean(document.querySelector(".detail-layer")),
      draftText: document.querySelector(".wire-selection-draft")?.textContent ?? "",
      previewText: document.querySelector(".candidate-command-preview")?.textContent ?? "",
      objectRouteCheckStates: Array.from(objectRouteReview?.querySelectorAll("[data-wire-object-route-check-state]") ?? [])
        .map((node) => node.getAttribute("data-wire-object-route-check-state")),
      objectRouteFieldStates: Array.from(objectRouteReview?.querySelectorAll("[data-wire-object-route-field-state]") ?? [])
        .map((node) => node.getAttribute("data-wire-object-route-field-state")),
      objectRouteReviewCount: document.querySelectorAll(".wire-object-route-review").length,
      objectRouteReviewState: objectRouteReview?.getAttribute("data-wire-object-route-review-state") ?? null,
      objectRouteState: objectRouteReview?.getAttribute("data-wire-object-route-state") ?? null,
      objectRouteStepStates: Array.from(objectRouteReview?.querySelectorAll("[data-wire-object-route-step-state]") ?? [])
        .map((node) => node.getAttribute("data-wire-object-route-step-state")),
      objectRouteSubmitDisabled: objectRouteReview?.querySelector(".wire-object-route-review-submit")?.hasAttribute("disabled") ?? null,
      objectRouteSubmitState: objectRouteReview?.querySelector(".wire-object-route-review-submit")?.getAttribute("data-wire-object-route-review-submit-state") ?? null,
      objectRouteText: objectRouteReview?.textContent ?? "",
      commandReviewFieldStates: Array.from(commandReview?.querySelectorAll("[data-command-review-field-state]") ?? [])
        .map((node) => node.getAttribute("data-command-review-field-state")),
      commandReviewState: commandReview?.getAttribute("data-command-review-state") ?? null,
      commandReviewSubmitDisabled: commandReview?.querySelector(".wire-command-review-submit")?.hasAttribute("disabled") ?? null,
      commandReviewSubmitState: commandReview?.querySelector(".wire-command-review-submit")?.getAttribute("data-command-review-submit-state") ?? null,
      commandReviewText: commandReview?.textContent ?? "",
      trayRouteReviewState: trayRouteReview?.getAttribute("data-wire-object-route-review-state") ?? null,
      trayRouteState: trayRouteReview?.getAttribute("data-wire-object-route-state") ?? null,
      trayRouteText: trayRouteReview?.textContent ?? "",
      routeState: route?.getAttribute("data-action-route-state") ?? null,
      routeText: route?.textContent ?? "",
      routeFieldStates: Array.from(route?.querySelectorAll("[data-route-field-state]") ?? [])
        .map((node) => node.getAttribute("data-route-field-state")),
      routeCheckStates: Array.from(route?.querySelectorAll("[data-route-check-state]") ?? [])
        .map((node) => node.getAttribute("data-route-check-state")),
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
      checkStates: Array.from(inspector?.querySelectorAll("[data-route-inspector-check-state]") ?? [])
        .map((node) => node.getAttribute("data-route-inspector-check-state")),
      hidden: inspector?.hasAttribute("hidden") ?? true,
      stepStates: Array.from(inspector?.querySelectorAll("[data-route-inspector-step-state]") ?? [])
        .map((node) => node.getAttribute("data-route-inspector-step-state")),
      text: inspector?.textContent ?? "",
      toggleExpanded: document.querySelector("[data-action-route-inspector-toggle]")?.getAttribute("aria-expanded") ?? null
    };
  })()`);

  await clickButtonByText(cdp, "打开提交检查层");
  await delay(150);
  const commandReviewLayerResult = await evaluateJson(cdp, `(() => {
    const layer = document.querySelector(".wire-command-review-layer");
    return {
      activeText: document.activeElement?.textContent ?? "",
      canSubmit: layer?.getAttribute("data-command-review-layer-can-submit") ?? "",
      commandType: layer?.getAttribute("data-command-review-layer-command-type") ?? "",
      fieldStates: Array.from(layer?.querySelectorAll("[data-command-review-layer-field-state]") ?? [])
        .map((node) => node.getAttribute("data-command-review-layer-field-state")),
      checkStates: Array.from(layer?.querySelectorAll("[data-command-review-layer-check-state]") ?? [])
        .map((node) => node.getAttribute("data-command-review-layer-check-state")),
      modal: layer?.getAttribute("aria-modal") ?? "",
      open: Boolean(layer),
      reviewState: layer?.getAttribute("data-command-review-layer-review-state") ?? "",
      role: layer?.getAttribute("role") ?? "",
      state: layer?.getAttribute("data-command-review-layer-state") ?? "",
      submitState: layer?.querySelector(".wire-command-review-layer-submit")?.getAttribute("data-command-review-layer-submit-state") ?? "",
      text: layer?.textContent ?? "",
      title: layer?.querySelector("#wire-command-review-layer-title")?.textContent ?? ""
    };
  })()`);
  await pressEscape(cdp);
  await delay(120);
  const commandReviewLayerClosed = await evaluateJson(cdp, `(() => !document.querySelector(".wire-command-review-layer"))()`);

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
    const objectContext = document.querySelector(".wire-object-context");
    const selectedProjection = document.querySelector('[data-rule-selected-object="p2-right-1"]');
    return {
      selected: tableObject?.getAttribute("data-selected") ?? null,
      selectedRef: Boolean(selectedRef),
      contextText: objectContext?.textContent ?? "",
      detailContextText: selectedObjectContext?.textContent ?? "",
      detailLayerOpen: Boolean(document.querySelector(".detail-layer")),
      hasCandidateRefs: document.querySelectorAll("[data-candidate-object-ref]").length,
      hasSelectedObjectContext: Boolean(selectedObjectContext),
      objectSyntaxMissingRequiredCount: Number(objectContext?.querySelector("[data-wire-object-syntax-missing-required-count]")?.getAttribute("data-wire-object-syntax-missing-required-count") ?? 0),
      objectSyntaxRoles: Array.from(objectContext?.querySelectorAll("[data-wire-object-syntax-role]") ?? [])
        .map((node) => node.getAttribute("data-wire-object-syntax-role")),
      objectSyntaxSources: Array.from(objectContext?.querySelectorAll("[data-wire-object-syntax-source]") ?? [])
        .map((node) => node.getAttribute("data-wire-object-syntax-source")),
      objectSyntaxStates: Array.from(objectContext?.querySelectorAll("[data-wire-object-syntax-state]") ?? [])
        .map((node) => node.getAttribute("data-wire-object-syntax-state")),
      objectSyntaxSummary: objectContext?.querySelector("[data-wire-object-syntax-summary]")?.textContent ?? "",
      objectSyntaxUsableCount: Number(objectContext?.querySelector("[data-wire-object-syntax-usable-count]")?.getAttribute("data-wire-object-syntax-usable-count") ?? 0),
      objectEventDetailIds: Array.from(objectContext?.querySelectorAll("[data-wire-object-event-detail]") ?? [])
        .map((node) => node.getAttribute("data-wire-object-event-detail") ?? ""),
      projectionRelationCount: Number(selectedProjection?.getAttribute("data-rule-selected-object-relation-count") ?? 0),
      projectionRelationActions: Array.from(selectedProjection?.querySelectorAll("[data-rule-selected-object-relation-actions]") ?? [])
        .map((node) => node.getAttribute("data-rule-selected-object-relation-actions") ?? ""),
      projectionSources: Array.from(selectedProjection?.querySelectorAll("[data-rule-selected-object-relation-source]") ?? [])
        .map((node) => node.getAttribute("data-rule-selected-object-relation-source")),
      projectionState: selectedProjection?.getAttribute("data-rule-selected-object-state") ?? null,
      syntaxMissingRequiredCount: Number(selectedProjection?.querySelector("[data-rule-selected-object-syntax-missing-required-count]")?.getAttribute("data-rule-selected-object-syntax-missing-required-count") ?? 0),
      syntaxRoles: Array.from(selectedProjection?.querySelectorAll("[data-rule-selected-object-syntax-role]") ?? [])
        .map((node) => node.getAttribute("data-rule-selected-object-syntax-role")),
      syntaxSources: Array.from(selectedProjection?.querySelectorAll("[data-rule-selected-object-syntax-source]") ?? [])
        .map((node) => node.getAttribute("data-rule-selected-object-syntax-source")),
      syntaxStates: Array.from(selectedProjection?.querySelectorAll("[data-rule-selected-object-syntax-state]") ?? [])
        .map((node) => node.getAttribute("data-rule-selected-object-syntax-state")),
      syntaxSummary: selectedProjection?.querySelector("[data-rule-selected-object-syntax-summary]")?.textContent ?? "",
      syntaxUsableCount: Number(selectedProjection?.querySelector("[data-rule-selected-object-syntax-usable-count]")?.getAttribute("data-rule-selected-object-syntax-usable-count") ?? 0),
      projectionText: selectedProjection?.textContent ?? ""
    };
  })()`);
  const objectEventDetailTriggerResult = await evaluateJson(cdp, `(() => {
    const row = document.querySelector('.wire-object-context [data-wire-object-event-detail^="object-event:"]');
    const trigger = row?.querySelector('[data-wire-detail-id^="object-event:"]');
    trigger?.click();
    return {
      clicked: Boolean(trigger),
      detailId: trigger?.getAttribute("data-wire-detail-id") ?? "",
      rowDetailId: row?.getAttribute("data-wire-object-event-detail") ?? ""
    };
  })()`);
  await delay(150);
  const objectEventDetailPanelResult = await evaluateJson(cdp, `(() => {
    const panel = document.querySelector(".wire-timeline-detail");
    return {
      detailId: panel?.getAttribute("data-wire-timeline-detail-id") ?? "",
      source: panel?.getAttribute("data-wire-timeline-source") ?? "",
      text: panel?.textContent ?? ""
    };
  })()`);

  await clickSelectedProjectionDetail(cdp, "rule:stack:fixture-stack-1");
  await delay(150);
  const projectionDetailResult = await evaluateJson(cdp, `(() => {
    const panel = document.querySelector(".wire-timeline-detail");
    const selectedProjection = document.querySelector('[data-rule-selected-object="p2-right-1"]');
    const trigger = selectedProjection?.querySelector('[data-wire-detail-id="rule:stack:fixture-stack-1"]');
    return {
      detailId: panel?.getAttribute("data-wire-timeline-detail-id") ?? "",
      detailSource: panel?.getAttribute("data-wire-timeline-source") ?? "",
      projectionTriggerPressed: trigger?.getAttribute("aria-pressed") ?? null,
      projectionTriggerSelected: trigger?.getAttribute("data-detail-selected") ?? null,
      text: panel?.textContent ?? "",
      triggerLabel: trigger?.getAttribute("aria-label") ?? ""
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
  if (focusResult.sourcePromptNext !== "已选来源") failures.push(`source object prompt next hint unexpected: ${focusResult.sourcePromptNext}`);
  if (focusResult.sourcePromptRoles !== "来源") failures.push(`source object prompt roles unexpected: ${focusResult.sourcePromptRoles}`);
  if (!String(focusResult.sourcePromptSummary ?? "").includes("打出手牌样例")) failures.push("source object prompt summary missing server action label");
  if (!focusResult.traySelectionRows.some((row) => row.role === "source" && row.choice === "p1-hand-spell" && row.text.includes("手牌法术"))) {
    failures.push(`object command tray did not expose source selection row: ${JSON.stringify(focusResult.traySelectionRows)}`);
  }
  if (focusResult.readinessState !== "ready") failures.push(`focused readiness state unexpected: ${focusResult.readinessState}`);
  if (focusResult.readinessCanSubmit !== "true") failures.push("focused readiness did not allow submit");
  if (focusResult.readinessCommand !== "PLAY_CARD") failures.push(`focused readiness command unexpected: ${focusResult.readinessCommand}`);
  if (focusResult.readinessEnabledCount !== "1") failures.push(`focused readiness enabled count unexpected: ${focusResult.readinessEnabledCount}`);
  if (focusResult.readinessMissingRequiredCount !== "0") failures.push(`focused readiness missing count unexpected: ${focusResult.readinessMissingRequiredCount}`);
  if (!focusResult.readinessText.includes("行动状态")) failures.push("focused readiness text missing heading");
  if (focusResult.commandCenterState !== "ready") failures.push(`command center did not follow focused source: ${focusResult.commandCenterState}`);
  if (!focusResult.commandCenterRows.some((row) => row.startsWith("window:"))) failures.push(`command center window row missing: ${focusResult.commandCenterRows.join(",")}`);
  if (!focusResult.commandCenterRows.includes("focus:server")) failures.push(`command center focus row missing server state: ${focusResult.commandCenterRows.join(",")}`);
  if (!focusResult.commandCenterRows.includes("candidate:ready")) failures.push(`command center candidate row missing ready state: ${focusResult.commandCenterRows.join(",")}`);
  if (focusResult.commandCenterActionCount < 1) failures.push("command center focused action entry missing");
  if (!focusResult.commandCenterText.includes("PLAY_CARD")) failures.push("command center did not expose command type");
  if (focusResult.commandCenterFollowupState !== "empty") failures.push(`command center followup state unexpected before submit: ${focusResult.commandCenterFollowupState}`);
  if (focusResult.commandCenterFollowupServerState !== "none") failures.push(`command center followup server state unexpected before submit: ${focusResult.commandCenterFollowupServerState}`);
  if (focusResult.commandCenterFollowupBridgeState !== "empty") failures.push(`command center followup bridge state unexpected before submit: ${focusResult.commandCenterFollowupBridgeState}`);
  for (const rowKey of ["serverState", "tick", "events", "snapshot", "prompt"]) {
    if (!focusResult.commandCenterFollowupBridgeRows.some((row) => row.startsWith(`${rowKey}:`))) {
      failures.push(`command center followup bridge row missing before submit: ${rowKey}`);
    }
  }
  if (focusResult.commandCenterFollowupMetricCount < 4) failures.push("command center followup metric strip missing");
  if (!focusResult.commandCenterFollowupText.includes("后续事件")) failures.push("command center followup heading missing");
  if (focusResult.commandCenterFollowupLayoutState !== "empty") failures.push(`command center followup layout state unexpected before submit: ${focusResult.commandCenterFollowupLayoutState}`);
  if (focusResult.commandCenterFollowupLayoutTotalCount !== 0) failures.push(`command center followup layout should not project refs before submit: ${focusResult.commandCenterFollowupLayoutTotalCount}`);
  if (!focusResult.commandCenterFollowupLayoutText.includes("回执桌面投影")) failures.push("command center followup layout heading missing");
  if (!focusResult.commandCenterFollowupText.includes("等待提交")) failures.push("command center followup bridge headline missing");
  if (!focusResult.commandCenterFollowupText.includes("尚未提交")) failures.push("command center followup empty summary missing");
  if (!focusResult.text.includes("服务端状态")) failures.push("focused action summary status missing");
  if (!focusResult.text.includes("可提交")) failures.push("focused action summary enabled count missing");
  if (!focusResult.contextText.includes("位置")) failures.push("object context position missing");
  if (!focusResult.contextText.includes("我方手牌")) failures.push("object context did not locate hand source");
  if (!focusResult.contextText.includes("下一步")) failures.push("object context next-step plan missing");
  if (!focusResult.contextText.includes("服务端命令")) failures.push("object context command plan missing");
  if (!focusResult.contextText.includes("组合：服务端声明")) failures.push("object context composer authority missing");
  if (focusResult.selectedLayoutState !== "located") failures.push(`selected layout state unexpected: ${focusResult.selectedLayoutState}`);
  if (focusResult.selectedLayoutObject !== "p1-hand-spell") failures.push(`selected layout object mismatch: ${focusResult.selectedLayoutObject}`);
  if (focusResult.selectedLayoutKind !== "hand") failures.push(`selected layout kind mismatch: ${focusResult.selectedLayoutKind}`);
  if (focusResult.selectedLayoutCapacityRow !== "self:hand") failures.push(`selected layout capacity row mismatch: ${focusResult.selectedLayoutCapacityRow}`);
  if (focusResult.selectedLayoutZone !== "self:hand") failures.push(`selected layout zone mismatch: ${focusResult.selectedLayoutZone}`);
  if (focusResult.selectedLayoutSource !== "player-hand-flow") failures.push(`selected layout source mismatch: ${focusResult.selectedLayoutSource}`);
  if (!focusResult.selectedLayoutText.includes("我方手牌")) failures.push("selected layout text did not locate hand zone");
  if (focusResult.contextAuthority !== "server") failures.push(`object context authority unexpected: ${focusResult.contextAuthority}`);
  if (focusResult.contextSource !== "服务端对象上下文") failures.push(`object context source unexpected: ${focusResult.contextSource}`);
  if (focusResult.contextSectionCount < 8) failures.push(`object context section map incomplete: ${focusResult.contextSectionCount}`);
  if (!focusResult.contextSections.some((row) => row.startsWith("identity:ready:"))) failures.push(`object context identity section missing: ${focusResult.contextSections.join(",")}`);
  if (!focusResult.contextSections.some((row) => row.startsWith("authority:server:"))) failures.push(`object context authority section missing server state: ${focusResult.contextSections.join(",")}`);
  if (!focusResult.contextSections.some((row) => row.startsWith("syntax:warning:") || row.startsWith("syntax:ready:"))) failures.push(`object context syntax section missing: ${focusResult.contextSections.join(",")}`);
  if (!focusResult.contextSections.some((row) => row.startsWith("commands:ready:"))) failures.push(`object context command section missing ready state: ${focusResult.contextSections.join(",")}`);
  if (!focusResult.contextSections.some((row) => row.startsWith("events:"))) failures.push(`object context event section missing state row: ${focusResult.contextSections.join(",")}`);
  if (!focusResult.contextSections.some((row) => row.startsWith("contract:server:"))) failures.push(`object context contract section missing server state: ${focusResult.contextSections.join(",")}`);
  if (!focusResult.contextText.includes("权威：服务端对象上下文")) failures.push("object context authority label missing");
  if (!focusResult.contextText.includes("服务端对象上下文")) failures.push("object context did not use server object candidate index");
  if (!focusResult.objectCommandComposerStates.includes("server")) failures.push("object context composer state missing");
  if (!focusResult.contextText.includes("近期事件")) failures.push("object context event section missing");
  if (focusResult.contextText.includes("serverPaymentState")) failures.push("object context leaked hidden server state");
  if (!detailContextResult.open) failures.push("card detail did not open");
  if (detailContextResult.state !== "open") failures.push("card detail dialog state missing");
  if (detailContextResult.labelledBy !== "card-detail-title") failures.push("card detail dialog label binding missing");
  if (!detailContextResult.activeText.includes("关闭")) failures.push("card detail close button did not receive focus");
  if (!detailContextResult.text.includes("规则上下文")) failures.push("card detail context section missing");
  if (!detailContextResult.text.includes("我方手牌")) failures.push("card detail did not reuse object context location");
  if (!detailContextResult.text.includes("服务端命令")) failures.push("card detail command context missing");
  if (!detailContextResult.text.includes("服务端对象上下文")) failures.push("card detail did not use server object candidate index");
  if (!detailContextResult.text.includes("PLAY_CARD")) failures.push("card detail command type missing");
  if (!detailContextResult.text.includes("来源:sourceObjectId*")) failures.push("card detail command field missing");
  if (!detailContextResult.text.includes("服务端字段")) failures.push("card detail command metadata summary missing");
  if (detailContextResult.text.includes("服务端:cardNo*")) failures.push("card detail leaked raw metadata command field");
  if (detailContextResult.checkMapMode !== "visible") failures.push(`card detail check map mode unexpected: ${detailContextResult.checkMapMode}`);
  if (detailContextResult.checkMapCount < 7) failures.push("card detail check map rows missing");
  if (!detailContextResult.checkRows.some((row) => row.startsWith("identity:server:"))) failures.push("card detail check map identity row missing");
  if (!detailContextResult.checkRows.some((row) => row.startsWith("candidates:server:"))) failures.push("card detail check map server candidate row missing");
  if (!detailContextResult.checkRows.some((row) => row.startsWith("selection:server:") || row.startsWith("selection:warning:"))) failures.push("card detail check map selection row missing");
  if (!detailContextResult.checkRows.some((row) => row.startsWith("rules:ready:"))) failures.push("card detail check map rules row missing");
  if (detailContextResult.actionState !== "ready") failures.push(`card detail action state unexpected: ${detailContextResult.actionState}`);
  if (detailContextResult.actionSource !== "p1-hand-spell") failures.push("card detail action source binding missing");
  if (detailContextResult.actionCount < 1) failures.push("card detail action entries missing");
  if (!detailContextResult.actionSummaryKeys.includes("candidate")) failures.push("card detail action candidate summary missing");
  if (!detailContextResult.actionSummaryKeys.includes("route")) failures.push("card detail action route summary missing");
  if (!detailContextResult.actionSummaryKeys.includes("field")) failures.push("card detail action field summary missing");
  if (!detailContextResult.actionModes.includes("composer")) failures.push("card detail composer action entry missing");
  if (detailContextResult.actionRouteCount < 1) failures.push("card detail action route rows missing");
  if (detailContextResult.actionRouteEntryKeys.some((entryKey) => !entryKey)) failures.push("card detail action route entry key missing");
  if (detailContextResult.actionRouteReviewButtonCount < 1) failures.push("card detail action route review controls missing");
  if (!detailContextResult.actionRouteStates.includes("composer")) failures.push("card detail action route composer state missing");
  if (!detailContextResult.actionRouteText.includes("候选入口路线")) failures.push("card detail action route section title missing");
  if (!detailContextResult.actionRouteText.includes("打开组合入口")) failures.push("card detail action route next step missing");
  if (!detailContextResult.actionText.includes("服务端可提交操作")) failures.push("card detail action section text missing");
  if (!detailContextResult.actionText.includes("组合")) failures.push("card detail action route summary text missing");
  if (!detailContextResult.actionText.includes("字段")) failures.push("card detail action field summary text missing");
  if (!detailContextResult.actionText.includes("提交服务端候选")) failures.push("card detail composer submit control missing");
  if (!detailReviewResult.clicked) failures.push("card detail route review control could not be clicked");
  if (!detailReviewResult.open) failures.push("card detail route review did not open");
  if (detailReviewResult.state !== "open") failures.push(`card detail route review state unexpected: ${detailReviewResult.state}`);
  if (detailReviewResult.routeState !== "composer") failures.push(`card detail route review route state unexpected: ${detailReviewResult.routeState}`);
  if (detailReviewResult.command !== "PLAY_CARD") failures.push(`card detail route review command unexpected: ${detailReviewResult.command}`);
  if (detailReviewResult.source !== "p1-hand-spell") failures.push(`card detail route review source unexpected: ${detailReviewResult.source}`);
  if (detailReviewResult.submitState !== "blocked") failures.push(`card detail route review submit state unexpected: ${detailReviewResult.submitState}`);
  if (!detailReviewResult.rows.includes("field")) failures.push("card detail route review field audit row missing");
  if (!detailReviewResult.rows.includes("next-step")) failures.push("card detail route review next-step audit row missing");
  if (!detailReviewResult.text.includes("只展示服务端候选")) failures.push("card detail route review authority copy missing");
  if (!detailReviewResult.text.includes("打开组合入口")) failures.push("card detail route review next step missing");
  if (!detailContextResult.inspectorOpen) failures.push("card detail inspector missing");
  if (detailContextResult.inspectorAuthority !== "server-inspection") failures.push(`card detail inspector authority unexpected: ${detailContextResult.inspectorAuthority}`);
  if (detailContextResult.inspectorSource !== "服务端检查摘要") failures.push(`card detail inspector source unexpected: ${detailContextResult.inspectorSource}`);
  if (!detailContextResult.inspectorText.includes("卡牌检查")) failures.push("card detail inspector header missing");
  if (!detailContextResult.inspectorText.includes("服务端只公开")) failures.push("card detail inspector boundary missing");
  if (!detailContextResult.summaryKeys.includes("authority")) failures.push("card detail inspector authority summary missing");
  if (!detailContextResult.summaryKeys.includes("zone")) failures.push("card detail inspector zone summary missing");
  if (!detailContextResult.summaryKeys.includes("candidate")) failures.push("card detail inspector candidate summary missing");
  if (!detailContextResult.groups.includes("identity")) failures.push("card detail inspector identity group missing");
  if (!detailContextResult.groups.includes("candidate")) failures.push("card detail inspector candidate group missing");
  if (!detailContextResult.groups.includes("selection-steps")) failures.push("card detail inspector selection steps group missing");
  if (!detailContextResult.groups.includes("events")) failures.push("card detail inspector event group missing");
  if (!detailContextResult.inspectorText.includes("服务端对象上下文")) failures.push("card detail inspector server inspection source missing");
  if (!detailContextResult.inspectorText.includes("前端不重算")) failures.push("card detail inspector safe boundary missing");
  if (!detailContextResult.inspectorText.includes("结算链")) failures.push("card detail inspector stack boundary missing");
  if (detailEscapeResult.open) failures.push("card detail did not close on Escape");
  if (detailEscapeResult.activeObjectId !== "p1-hand-spell") failures.push("card detail did not restore focus to source card");
  if (!focusResult.nextStep.includes("下一步")) failures.push("focused action next step missing");
  if (focusResult.candidatePlanCount < 1) failures.push("focused action candidate plan missing");
  if (focusResult.focusedPathCount < 1) failures.push("focused interaction candidate path missing");
  if (focusResult.composerCount < 1) failures.push("focused interaction composer entry missing");
  if (!focusResult.composerCanSubmitStates.includes("true")) failures.push("focused interaction composer submit state missing");
  if (!focusResult.composerGateStates.includes("可提交")) failures.push("focused interaction composer gate state missing");
  if (!focusResult.composerCheckStates.includes("ready")) failures.push("focused interaction composer check ready state missing");
  if (!focusResult.composerGateText.includes("提交门禁")) failures.push("focused interaction composer gate text missing");
  if (!focusResult.composerGateText.includes("提交检查")) failures.push("focused interaction composer check text missing");
  if (focusResult.focusedActionButtonCount < 1) failures.push("focused interaction action controls missing");
  if (focusResult.grammarComposerState !== "server") failures.push(`focused interaction grammar composer state unexpected: ${focusResult.grammarComposerState}`);
  if (focusResult.grammarState !== "ready") failures.push(`focused interaction grammar state unexpected: ${focusResult.grammarState}`);
  if (!focusResult.grammarText.includes("交互语法")) failures.push("focused interaction grammar header missing");
  if (!focusResult.grammarText.includes("组合：服务端声明")) failures.push("focused interaction grammar composer label missing");
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
  if (targetResult.commandCenterState !== "ready") failures.push(`command center state unexpected after target click: ${targetResult.commandCenterState}`);
  if (targetResult.detailLayerOpen) failures.push("target click opened detail");
  if (!targetResult.draftText.includes("目标 1")) failures.push("draft target count missing");
  if (!targetResult.selectionRows.some((row) => row.role === "target" && row.choice === "p2-left-1" && row.objectIds.includes("p2-left-1"))) {
    failures.push(`selection guide did not expose target selection row: ${JSON.stringify(targetResult.selectionRows)}`);
  }
  if (!targetResult.traySelectionRows.some((row) => row.role === "target" && row.choice === "p2-left-1" && row.objectIds.includes("p2-left-1"))) {
    failures.push(`object command tray did not expose target selection row: ${JSON.stringify(targetResult.traySelectionRows)}`);
  }
  if (targetResult.grammarComposerState !== "server") failures.push(`target interaction grammar composer state unexpected: ${targetResult.grammarComposerState}`);
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
  if (actionMapResult.commandReviewState !== "ready") failures.push(`action command review source-focus state unexpected: ${actionMapResult.commandReviewState}`);
  if (!actionMapResult.commandReviewText.includes("提交审阅")) failures.push("action command review heading missing");
  if (!actionMapResult.commandReviewText.includes("提交当前路线")) failures.push("action command review submit button missing");
  if (actionMapResult.commandSubmissionState !== "empty") failures.push(`action command submission initial state unexpected: ${actionMapResult.commandSubmissionState}`);
  if (!actionMapResult.commandSubmissionText.includes("提交反馈")) failures.push("action command submission feedback heading missing");
  if (!actionMapResult.commandSubmissionText.includes("尚未提交")) failures.push("action command submission feedback empty state missing");
  if (!actionMapResult.commandSubmissionText.includes("打开回执检查层")) failures.push("action command submission receipt layer entry missing");
  if (actionMapResult.commandSubmissionOpenLayerState !== "empty") failures.push(`action command submission receipt layer initial state unexpected: ${actionMapResult.commandSubmissionOpenLayerState}`);
  if (actionMapResult.commandSubmissionOpenLayerDisabled !== true) failures.push("action command submission receipt layer entry should be disabled before submit");
  if (actionMapResult.commandSubmissionLayoutState !== "empty") failures.push(`action command submission layout state unexpected before submit: ${actionMapResult.commandSubmissionLayoutState}`);
  if (actionMapResult.commandSubmissionLayoutTotalCount !== 0) failures.push(`action command submission layout should not project refs before submit: ${actionMapResult.commandSubmissionLayoutTotalCount}`);
  if (!actionMapResult.commandSubmissionLayoutText.includes("回执桌面投影")) failures.push("action command submission layout projection heading missing");
  if (!actionMapResult.commandReviewText.includes("打出手牌")) failures.push("action command review candidate missing");
  if (!actionMapResult.commandReviewText.includes("PLAY_CARD")) failures.push("action command review command type missing");
  if (!actionMapResult.commandReviewText.includes("下一步")) failures.push("action command review next step missing");
  if (!actionMapResult.commandReviewText.includes("服务端字段")) failures.push("action command review server field safe label missing");
  if (!actionMapResult.commandReviewFieldStates.includes("covered")) failures.push("action command review covered field missing");
  if (!actionMapResult.commandReviewFieldStates.includes("server")) failures.push("action command review server field missing");
  if (actionMapResult.commandReviewSubmitState !== "ready") failures.push(`action command review submit state unexpected: ${actionMapResult.commandReviewSubmitState}`);
  if (actionMapResult.commandReviewSubmitDisabled !== false) failures.push("action command review submit button should be enabled for ready route");
  if (actionMapResult.commandReviewText.includes("serverPaymentState")) failures.push("action command review leaked hidden server state");
  if (actionMapResult.focusBridgeState !== "enabled") failures.push(`action map focus bridge state unexpected: ${actionMapResult.focusBridgeState}`);
  if (!actionMapResult.focusBridgeText.includes("角色 来源")) failures.push("action map focus bridge role summary missing");
  if (!actionMapResult.focusBridgeText.includes("可选目标")) failures.push("action map focus bridge next step missing");
  if (!actionMapResult.focusBridgeText.includes("PLAY_CARD")) failures.push("action map focus bridge command type missing");
  if (!actionMapResult.focusBridgeText.includes("对方单位")) failures.push("action map focus bridge next object ref missing");
  if (actionMapResult.actionLayoutProjectionState !== "ready") failures.push(`action layout projection state unexpected: ${actionMapResult.actionLayoutProjectionState}`);
  if (actionMapResult.actionLayoutProjectionLocatedCount < 2) failures.push(`action layout projection located count too low: ${actionMapResult.actionLayoutProjectionLocatedCount}`);
  if (actionMapResult.actionLayoutProjectionReadyCount < 2) failures.push(`action layout projection ready count too low: ${actionMapResult.actionLayoutProjectionReadyCount}`);
  if (actionMapResult.actionLayoutProjectionTotalCount < 2) failures.push(`action layout projection total count too low: ${actionMapResult.actionLayoutProjectionTotalCount}`);
  if (!actionMapResult.actionLayoutProjectionText.includes("桌面区域投影")) failures.push("action layout projection heading missing");
  if (!actionMapResult.actionLayoutProjectionRows.some((row) => row.objectId === "p1-hand-spell" && row.zone === "self:hand" && row.state === "ready")) {
    failures.push(`action layout projection did not map playable hand source: ${JSON.stringify(actionMapResult.actionLayoutProjectionRows)}`);
  }
  if (!actionMapResult.actionLayoutProjectionRows.some((row) => row.objectId === "p2-right-1" && row.zone === "battlefield:1:opponent" && row.kind === "battlefield-unit")) {
    failures.push(`action layout projection did not map target unit: ${JSON.stringify(actionMapResult.actionLayoutProjectionRows)}`);
  }
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
  if (!actionMapResult.actionPromptInspectionText.includes("提示检查")) failures.push("action panel prompt inspection header missing");
  if (!actionMapResult.actionPromptInspectionText.includes("服务端提示检查")) failures.push("action panel prompt inspection source missing");
  if (!actionMapResult.actionPromptInspectionText.includes("服务端只公开")) failures.push("action panel prompt inspection boundary missing");
  if (!actionMapResult.actionPromptInspectionSummaryKeys.includes("candidate")) failures.push("action panel prompt inspection candidate summary missing");
  if (!actionMapResult.actionPromptInspectionGroups.includes("safe-boundary")) failures.push("action panel prompt inspection safe-boundary group missing");
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
  if (!actionMapResult.serverFlowActionCandidates.some((candidate) => candidate.includes("PLAY_CARD"))) {
    failures.push(`server flow action candidates missing command name: ${actionMapResult.serverFlowActionCandidates.join(",")}`);
  }
  if (actionMapResult.ruleFocusLane !== "task") failures.push(`wire rule focus lane unexpected: ${actionMapResult.ruleFocusLane}`);
  if (!actionMapResult.ruleFocusDetailId?.includes("rule:task:fixture-task-1")) failures.push("wire rule focus did not expose active task detail id");
  if (!actionMapResult.ruleFocusText.includes("当前规则焦点")) failures.push("wire rule focus heading missing");
  if (!actionMapResult.ruleFocusText.includes("阻塞普通行动")) failures.push("wire rule focus reason missing");
  if (actionMapResult.ruleFocusRefCount < 1) failures.push("wire rule focus object refs missing");
  if (actionMapResult.ruleFocusActionCount < 1) failures.push("wire rule focus action bridge missing");
  if (!actionMapResult.ruleFocusActionStates.some((state) => ["blocked", "ready", "referenced"].includes(state))) {
    failures.push(`wire rule focus action bridge state missing: ${actionMapResult.ruleFocusActionStates.join(",")}`);
  }
  if (!actionMapResult.ruleFocusActionText.includes("候选")) failures.push("wire rule focus action bridge candidate summary missing");
  if (!actionMapResult.ruleFocusActionAuthorities.some((authority) => authority?.includes("服务端"))) {
    failures.push(`wire rule focus action bridge server authority missing: ${actionMapResult.ruleFocusActionAuthorities.join(",")}`);
  }
  if (!actionMapResult.ruleFocusActionSteps.some((steps) => typeof steps === "string" && steps.length > 0)) {
    failures.push("wire rule focus action bridge step summary missing");
  }
  if (!actionMapResult.ruleFlowText.includes("结算链")) failures.push("wire rule queue stack lane missing");
  if (!actionMapResult.ruleFlowText.includes("规则任务")) failures.push("wire rule queue task lane missing");
  if (!actionMapResult.ruleFlowText.includes("触发队列")) failures.push("wire rule queue trigger lane missing");
  if (!actionMapResult.ruleFlowText.includes("近期事件")) failures.push("wire rule queue resolution lane missing");
  if (!actionMapResult.ruleFlowText.includes("下一步")) failures.push("wire rule queue next step missing");
  if (!actionMapResult.ruleLaneDetailIds.some(Boolean)) {
    failures.push(`wire rule queue lane detail id missing: ${actionMapResult.ruleLaneDetailIds.join(",")}`);
  }
  if (!actionMapResult.ruleCoverageDetailIds.some(Boolean)) {
    failures.push(`wire rule coverage detail id missing: ${actionMapResult.ruleCoverageDetailIds.join(",")}`);
  }
  if (actionMapResult.ruleSequenceCount < 1) failures.push("wire rule queue sequence items missing");
  if (!actionMapResult.ruleSequenceDetailIds.some(Boolean)) {
    failures.push(`wire rule queue sequence detail id missing: ${actionMapResult.ruleSequenceDetailIds.join(",")}`);
  }
  if (!actionMapResult.ruleResponsibilityDetailIds.some(Boolean)) {
    failures.push(`wire rule queue responsibility detail id missing: ${actionMapResult.ruleResponsibilityDetailIds.join(",")}`);
  }
  if (actionMapResult.ruleSequenceRefCount < 1) failures.push("wire rule queue sequence object refs missing");
  for (const sectionKey of ["stack", "task", "trigger", "resolution"]) {
    if (!actionMapResult.ruleSectionKeys.includes(sectionKey)) failures.push(`wire rule queue section missing: ${sectionKey}`);
  }
  if (!actionMapResult.ruleItemKeys.some((key) => key?.startsWith("stack:"))) failures.push("wire rule queue stack items missing");
  if (!actionMapResult.ruleStackItemText.includes("顺序")) failures.push("wire rule queue stack order line missing");
  if (!actionMapResult.ruleStackItemText.includes("响应")) failures.push("wire rule queue stack response line missing");
  if (!actionMapResult.ruleStackItemText.includes("响应窗口由服务端 prompt 裁定")) failures.push("wire rule queue stack response authority copy missing");
  if (!actionMapResult.ruleItemKeys.some((key) => key?.startsWith("task:"))) failures.push("wire rule queue task items missing");
  if (!actionMapResult.ruleItemKeys.some((key) => key?.startsWith("trigger:"))) failures.push("wire rule queue trigger items missing");
  if (!actionMapResult.ruleItemKeys.some((key) => key?.startsWith("battlefield-resolution:"))) failures.push("wire rule queue battlefield resolution items missing");
  if (ruleInspectorResult.hidden) failures.push("wire rule inspector did not open");
  if (ruleInspectorResult.toggleExpanded !== "true") failures.push("wire rule inspector toggle aria state missing");
  if (!ruleInspectorResult.text.includes("规则检查")) failures.push("wire rule inspector header missing");
  if (!ruleInspectorResult.text.includes("活动")) failures.push("wire rule inspector active lane summary missing");
  if (!ruleInspectorResult.text.includes("下一步")) failures.push("wire rule inspector next step missing");
  if (ruleInspectorResult.laneCount !== 4) failures.push(`wire rule inspector lane count mismatch: ${ruleInspectorResult.laneCount}`);
  if (!ruleInspectorResult.laneDetailIds.some(Boolean)) {
    failures.push(`wire rule inspector lane detail id missing: ${ruleInspectorResult.laneDetailIds.join(",")}`);
  }
  if (ruleInspectorResult.sequenceCount < 1) failures.push("wire rule inspector sequence items missing");
  if (!ruleInspectorResult.sequenceDetailIds.some(Boolean)) {
    failures.push(`wire rule inspector sequence detail id missing: ${ruleInspectorResult.sequenceDetailIds.join(",")}`);
  }
  if (ruleInspectorResult.sequenceRefCount < 1) failures.push("wire rule inspector sequence object refs missing");
  if (ruleSequenceDetailClickId !== ruleSequenceDetailTarget) failures.push(`rule sequence detail click unexpected: ${ruleSequenceDetailClickId}`);
  if (ruleSequenceDetailResult.detailId !== ruleSequenceDetailTarget) failures.push(`rule sequence detail panel unexpected: ${ruleSequenceDetailResult.detailId}`);
  if (ruleSequenceDetailResult.panelState !== "rule") failures.push(`rule sequence detail panel state unexpected: ${ruleSequenceDetailResult.panelState}`);
  if (!ruleSequenceDetailResult.text.includes("服务端")) failures.push("rule sequence detail authority text missing");
  if (ruleLaneDetailClickId !== ruleLaneDetailTarget) failures.push(`rule lane detail click unexpected: ${ruleLaneDetailClickId}`);
  if (ruleLaneDetailResult.detailId !== ruleLaneDetailTarget) failures.push(`rule lane detail panel unexpected: ${ruleLaneDetailResult.detailId}`);
  if (ruleLaneDetailResult.panelState !== "rule") failures.push(`rule lane detail panel state unexpected: ${ruleLaneDetailResult.panelState}`);
  if (!ruleLaneDetailResult.text.includes("服务端")) failures.push("rule lane detail authority text missing");
  if (ruleCoverageDetailClickId !== ruleCoverageDetailTarget) failures.push(`rule coverage detail click unexpected: ${ruleCoverageDetailClickId}`);
  if (ruleCoverageDetailResult.detailId !== ruleCoverageDetailTarget) failures.push(`rule coverage detail panel unexpected: ${ruleCoverageDetailResult.detailId}`);
  if (ruleCoverageDetailResult.panelState !== "rule") failures.push(`rule coverage detail panel state unexpected: ${ruleCoverageDetailResult.panelState}`);
  if (!ruleCoverageDetailResult.text.includes("服务端")) failures.push("rule coverage detail authority text missing");
  if (ruleResponsibilityDetailClickId !== ruleResponsibilityDetailTarget) failures.push(`rule responsibility detail click unexpected: ${ruleResponsibilityDetailClickId}`);
  if (ruleResponsibilityDetailResult.detailId !== ruleResponsibilityDetailTarget) failures.push(`rule responsibility detail panel unexpected: ${ruleResponsibilityDetailResult.detailId}`);
  if (ruleResponsibilityDetailResult.panelState !== "rule") failures.push(`rule responsibility detail panel state unexpected: ${ruleResponsibilityDetailResult.panelState}`);
  if (!ruleResponsibilityDetailResult.text.includes("服务端")) failures.push("rule responsibility detail authority text missing");
  if (ruleInspectorLaneDetailClickId !== ruleInspectorLaneDetailTarget) failures.push(`rule inspector lane detail click unexpected: ${ruleInspectorLaneDetailClickId}`);
  if (ruleInspectorLaneDetailResult.detailId !== ruleInspectorLaneDetailTarget) failures.push(`rule inspector lane detail panel unexpected: ${ruleInspectorLaneDetailResult.detailId}`);
  if (ruleInspectorLaneDetailResult.panelState !== "rule") failures.push(`rule inspector lane detail panel state unexpected: ${ruleInspectorLaneDetailResult.panelState}`);
  if (!ruleInspectorLaneDetailResult.text.includes("服务端")) failures.push("rule inspector lane detail authority text missing");
  if (ruleInspectorSequenceDetailClickId !== ruleInspectorSequenceDetailTarget) failures.push(`rule inspector sequence detail click unexpected: ${ruleInspectorSequenceDetailClickId}`);
  if (ruleInspectorSequenceDetailResult.detailId !== ruleInspectorSequenceDetailTarget) failures.push(`rule inspector sequence detail panel unexpected: ${ruleInspectorSequenceDetailResult.detailId}`);
  if (ruleInspectorSequenceDetailResult.panelState !== "rule") failures.push(`rule inspector sequence detail panel state unexpected: ${ruleInspectorSequenceDetailResult.panelState}`);
  if (!ruleInspectorSequenceDetailResult.text.includes("服务端")) failures.push("rule inspector sequence detail authority text missing");
  if (actionMapResult.candidatePlanCount < 1) failures.push("action map candidate plan cards missing");
  if (actionMapResult.candidatePlanEnabled !== "true") failures.push("PLAY_CARD candidate plan did not preserve enabled state");
  if (!actionMapResult.candidatePlanText.includes("命令字段 5")) failures.push("PLAY_CARD candidate plan command field count missing");
  if (!actionMapResult.candidatePlanText.includes("缺口 0")) failures.push("PLAY_CARD candidate plan gap summary missing");
  if (!actionMapResult.candidatePlanNext.includes("下一步")) failures.push("PLAY_CARD candidate plan next-step missing");
  if (actionMapResult.routeState !== "ready") failures.push(`action route strip source-focus state unexpected: ${actionMapResult.routeState}`);
  if (!actionMapResult.routeText.includes("打出手牌")) failures.push("action route strip source-focus candidate missing");
  if (!actionMapResult.routeText.includes("可送服务端校验")) failures.push("action route strip source-focus ready copy missing");
  if (!actionMapResult.routeText.includes("提交审计")) failures.push("action route submit audit summary missing");
  if (!actionMapResult.routeText.includes("服务端注入")) failures.push("action route submit audit server injected row missing");
  if (!actionMapResult.routeCheckStates.includes("ready")) failures.push("action route submit audit ready state missing");
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
  if (!actionFocusChoiceResult.routeText.includes("提交审计")) failures.push("action focus choice submit audit summary missing");
  if (!actionFocusChoiceResult.routeFieldStates.includes("covered")) failures.push("action focus choice route covered field missing");
  if (!actionFocusChoiceResult.routeFieldStates.includes("server")) failures.push("action focus choice route server field missing");
  if (!actionFocusChoiceResult.routeCheckStates.includes("ready")) failures.push("action focus choice route audit ready state missing");
  if (actionFocusChoiceResult.commandReviewState !== "ready") failures.push(`action focus choice command review state unexpected: ${actionFocusChoiceResult.commandReviewState}`);
  if (!actionFocusChoiceResult.commandReviewText.includes("提交审阅")) failures.push("action focus choice command review heading missing");
  if (!actionFocusChoiceResult.commandReviewText.includes("提交当前路线")) failures.push("action focus choice command review submit button missing");
  if (!actionFocusChoiceResult.commandReviewText.includes("打出手牌")) failures.push("action focus choice command review candidate missing");
  if (!actionFocusChoiceResult.commandReviewFieldStates.includes("covered")) failures.push("action focus choice command review covered field missing");
  if (!actionFocusChoiceResult.commandReviewFieldStates.includes("server")) failures.push("action focus choice command review server field missing");
  if (actionFocusChoiceResult.commandReviewSubmitState !== "ready") failures.push(`action focus choice command review submit state unexpected: ${actionFocusChoiceResult.commandReviewSubmitState}`);
  if (actionFocusChoiceResult.commandReviewSubmitDisabled !== false) failures.push("action focus choice command review submit button should be enabled for ready route");
  if (actionFocusChoiceResult.objectRouteReviewCount < 2) failures.push(`action focus choice object route review count too low: ${actionFocusChoiceResult.objectRouteReviewCount}`);
  if (actionFocusChoiceResult.objectRouteReviewState !== "ready") failures.push(`action focus choice focused object review state unexpected: ${actionFocusChoiceResult.objectRouteReviewState}`);
  if (actionFocusChoiceResult.objectRouteState !== "ready") failures.push(`action focus choice focused object route state unexpected: ${actionFocusChoiceResult.objectRouteState}`);
  if (!actionFocusChoiceResult.objectRouteText.includes("打出手牌")) failures.push("action focus choice focused object route candidate missing");
  if (!actionFocusChoiceResult.objectRouteText.includes("提交当前路线")) failures.push("action focus choice focused object route submit copy missing");
  if (!actionFocusChoiceResult.objectRouteText.includes("可送服务端")) failures.push("action focus choice focused object route ready copy missing");
  if (actionFocusChoiceResult.objectRouteSubmitState !== "ready") failures.push(`action focus choice focused object submit state unexpected: ${actionFocusChoiceResult.objectRouteSubmitState}`);
  if (actionFocusChoiceResult.objectRouteSubmitDisabled !== false) failures.push("action focus choice focused object submit button should be enabled for ready route");
  if (!actionFocusChoiceResult.objectRouteStepStates.includes("selected")) failures.push("action focus choice focused object selected step missing");
  if (!actionFocusChoiceResult.objectRouteFieldStates.includes("covered")) failures.push("action focus choice focused object covered field missing");
  if (!actionFocusChoiceResult.objectRouteFieldStates.includes("server")) failures.push("action focus choice focused object server field missing");
  if (!actionFocusChoiceResult.objectRouteCheckStates.includes("ready")) failures.push("action focus choice focused object ready check missing");
  if (actionFocusChoiceResult.trayRouteReviewState !== "ready") failures.push(`action focus choice tray object review state unexpected: ${actionFocusChoiceResult.trayRouteReviewState}`);
  if (actionFocusChoiceResult.trayRouteState !== "ready") failures.push(`action focus choice tray object route state unexpected: ${actionFocusChoiceResult.trayRouteState}`);
  if (!actionFocusChoiceResult.trayRouteText.includes("打出手牌")) failures.push("action focus choice tray object route candidate missing");
  if (!actionFocusChoiceResult.trayRouteText.includes("提交当前路线")) failures.push("action focus choice tray object route submit copy missing");
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
  if (!routeInspectorResult.text.includes("提交审计")) failures.push("route inspector audit section missing");
  if (!routeInspectorResult.text.includes("字段覆盖")) failures.push("route inspector field section missing");
  if (!routeInspectorResult.text.includes("服务端字段")) failures.push("route inspector server field safe label missing");
  if (!routeInspectorResult.checkStates.includes("ready")) failures.push("route inspector audit ready check missing");
  if (!routeInspectorResult.stepStates.includes("selected")) failures.push("route inspector selected step missing");
  if (!routeInspectorResult.fieldStates.includes("covered")) failures.push("route inspector covered field missing");
  if (!routeInspectorResult.fieldStates.includes("server")) failures.push("route inspector server field missing");
  if (!commandReviewLayerResult.open) failures.push("command review layer did not open");
  if (commandReviewLayerResult.role !== "dialog") failures.push(`command review layer role unexpected: ${commandReviewLayerResult.role}`);
  if (commandReviewLayerResult.modal !== "true") failures.push("command review layer modal state missing");
  if (commandReviewLayerResult.state !== "open") failures.push(`command review layer state unexpected: ${commandReviewLayerResult.state}`);
  if (commandReviewLayerResult.reviewState !== "ready") failures.push(`command review layer review state unexpected: ${commandReviewLayerResult.reviewState}`);
  if (commandReviewLayerResult.canSubmit !== "true") failures.push(`command review layer submit gate unexpected: ${commandReviewLayerResult.canSubmit}`);
  if (commandReviewLayerResult.commandType !== "PLAY_CARD") failures.push(`command review layer command type unexpected: ${commandReviewLayerResult.commandType}`);
  if (commandReviewLayerResult.submitState !== "ready") failures.push(`command review layer submit state unexpected: ${commandReviewLayerResult.submitState}`);
  if (!commandReviewLayerResult.activeText.includes("关闭")) failures.push("command review layer close button did not receive focus");
  if (!commandReviewLayerResult.title.includes("打出手牌")) failures.push("command review layer title missing candidate label");
  if (!commandReviewLayerResult.text.includes("提交检查层")) failures.push("command review layer heading missing");
  if (!commandReviewLayerResult.text.includes("服务端字段覆盖")) failures.push("command review layer field coverage section missing");
  if (!commandReviewLayerResult.text.includes("提交审计")) failures.push("command review layer audit section missing");
  if (!commandReviewLayerResult.text.includes("最终仍由服务端规则校验")) failures.push("command review layer authority copy missing");
  if (!commandReviewLayerResult.fieldStates.includes("covered")) failures.push("command review layer covered field missing");
  if (!commandReviewLayerResult.fieldStates.includes("server")) failures.push("command review layer server field missing");
  if (!commandReviewLayerResult.checkStates.includes("ready")) failures.push("command review layer ready check missing");
  if (commandReviewLayerResult.text.includes("serverPaymentState")) failures.push("command review layer leaked hidden server state");
  if (!commandReviewLayerClosed) failures.push("command review layer did not close on Escape");
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
  if (!runeActionMapResult.detailContextText.includes("服务端对象上下文")) failures.push("rune object context did not use server object candidate index");
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
  if (!candidateRefResult.detailContextText.includes("服务端对象上下文")) failures.push("timeline selected object context did not use server object candidate index");
  if (!candidateRefResult.detailContextText.includes("PLAY_CARD")) failures.push("timeline selected object context command type missing");
  if (!candidateRefResult.detailContextText.includes("服务端字段")) failures.push("timeline selected object context command metadata summary missing");
  if (candidateRefResult.detailContextText.includes("服务端:cardNo*")) failures.push("timeline selected object context leaked raw metadata command field");
  if (!candidateRefResult.detailContextText.includes("近期事件")) failures.push("timeline selected object context event section missing");
  if (!candidateRefResult.objectEventDetailIds.some((id) => id.startsWith("object-event:"))) {
    failures.push(`focused object event detail id missing: ${candidateRefResult.objectEventDetailIds.join(",")}`);
  }
  if (!objectEventDetailTriggerResult.clicked) failures.push("focused object event detail trigger missing");
  if (objectEventDetailTriggerResult.detailId !== objectEventDetailTriggerResult.rowDetailId) {
    failures.push(`focused object event detail trigger mismatch: ${JSON.stringify(objectEventDetailTriggerResult)}`);
  }
  if (objectEventDetailPanelResult.detailId !== objectEventDetailTriggerResult.detailId) {
    failures.push(`focused object event detail did not select timeline detail: ${JSON.stringify(objectEventDetailPanelResult)}`);
  }
  if (objectEventDetailPanelResult.source !== "event") failures.push(`focused object event detail source unexpected: ${objectEventDetailPanelResult.source}`);
  if (!objectEventDetailPanelResult.text.includes("对象来源")) failures.push("focused object event detail did not expose object ref source");
  if (!candidateRefResult.objectSyntaxSources.includes("object-context")) failures.push("focused object syntax object-context source missing");
  if (!candidateRefResult.objectSyntaxStates.includes("usable-optional")) failures.push("focused object syntax usable optional state missing");
  if (!candidateRefResult.objectSyntaxStates.includes("missing-required")) failures.push("focused object syntax missing required state missing");
  if (!candidateRefResult.objectSyntaxRoles.includes("target")) failures.push("focused object syntax target role missing");
  if (!candidateRefResult.objectSyntaxSummary.includes("可作为 目标")) failures.push("focused object syntax usable role summary missing");
  if (!candidateRefResult.objectSyntaxSummary.includes("还需 来源")) failures.push("focused object syntax missing role summary missing");
  if (candidateRefResult.objectSyntaxUsableCount < 1) failures.push(`focused object syntax usable count too low: ${candidateRefResult.objectSyntaxUsableCount}`);
  if (candidateRefResult.objectSyntaxMissingRequiredCount < 1) failures.push(`focused object syntax missing required count too low: ${candidateRefResult.objectSyntaxMissingRequiredCount}`);
  if (candidateRefResult.projectionState !== "linked") failures.push(`selected object rule projection state unexpected: ${candidateRefResult.projectionState}`);
  if (candidateRefResult.projectionRelationCount < 3) failures.push(`selected object rule projection relation count too low: ${candidateRefResult.projectionRelationCount}`);
  if (!candidateRefResult.projectionSources.includes("server-flow")) failures.push("selected object projection server-flow source missing");
  if (!candidateRefResult.projectionSources.includes("object-context")) failures.push("selected object projection object-context source missing");
  if (!candidateRefResult.projectionSources.includes("responsibility")) failures.push("selected object projection responsibility source missing");
  if (!candidateRefResult.projectionText.includes("选中对象投影")) failures.push("selected object projection heading missing");
  if (!candidateRefResult.projectionText.includes("候选目标")) failures.push("selected object projection server role missing");
  if (!candidateRefResult.projectionRelationActions.some((actions) => actions.includes("PLAY_CARD"))) {
    failures.push(`selected object projection relation actions missing command name: ${candidateRefResult.projectionRelationActions.join(",")}`);
  }
  if (!candidateRefResult.syntaxSources.includes("server-flow")) failures.push("selected object syntax server-flow source missing");
  if (!candidateRefResult.syntaxSources.includes("object-context")) failures.push("selected object syntax object-context source missing");
  if (!candidateRefResult.syntaxStates.includes("usable-optional")) failures.push("selected object syntax usable optional state missing");
  if (!candidateRefResult.syntaxStates.includes("missing-required")) failures.push("selected object syntax missing required state missing");
  if (!candidateRefResult.syntaxRoles.includes("target")) failures.push("selected object syntax target role missing");
  if (!candidateRefResult.syntaxSummary.includes("可作为 目标")) failures.push("selected object syntax usable role summary missing");
  if (!candidateRefResult.syntaxSummary.includes("还需 来源")) failures.push("selected object syntax missing role summary missing");
  if (candidateRefResult.syntaxUsableCount < 1) failures.push(`selected object syntax usable count too low: ${candidateRefResult.syntaxUsableCount}`);
  if (candidateRefResult.syntaxMissingRequiredCount < 1) failures.push(`selected object syntax missing required count too low: ${candidateRefResult.syntaxMissingRequiredCount}`);
  if (projectionDetailResult.detailId !== "rule:stack:fixture-stack-1") failures.push(`selected object projection detail id unexpected: ${projectionDetailResult.detailId}`);
  if (projectionDetailResult.detailSource !== "rule") failures.push(`selected object projection detail source unexpected: ${projectionDetailResult.detailSource}`);
  if (projectionDetailResult.projectionTriggerPressed !== "true") failures.push("selected object projection detail trigger did not reflect selected state");
  if (projectionDetailResult.projectionTriggerSelected !== "true") failures.push("selected object projection detail trigger selected attr missing");
  if (!projectionDetailResult.triggerLabel.includes("结算链项目")) failures.push("selected object projection detail accessible label missing");
  if (!projectionDetailResult.text.includes("结算链项目")) failures.push("selected object projection did not open rule detail panel");
  if (candidateRefResult.detailLayerOpen) failures.push("candidate object ref opened detail");

  if (failures.length > 0) {
    throw new Error(`Wire click selection smoke failed:\n${failures.join("\n")}`);
  }
}

async function runWireTimelineCommandSubmitSmoke(cdp) {
  await clickWireDetail(cdp, "rule:stack:fixture-stack-1");
  await delay(150);
  const initial = await timelineCommandSubmitSummary(cdp);
  if (initial.commandSubmissionState !== "empty") {
    throw new Error(`Timeline submit smoke expected empty feedback before submit, got ${initial.commandSubmissionState}`);
  }
  if (initial.detailId !== "rule:stack:fixture-stack-1") {
    throw new Error(`Timeline submit smoke did not open stack detail: ${initial.detailId}`);
  }
  if (!initial.submitTypes.includes("PLAY_CARD")) {
    throw new Error(`Timeline submit smoke did not expose PLAY_CARD submit plan: ${initial.submitTypes.join(",")}`);
  }
  if (initial.submitStates.includes("ready")) {
    throw new Error(`Timeline submit smoke should not start ready before choosing target: ${initial.submitStates.join(",")}`);
  }
  if (!initial.submitEnabledStates.includes("false")) {
    throw new Error(`Timeline submit smoke expected disabled submit before choosing target: ${initial.submitEnabledStates.join(",")}`);
  }
  if (initial.fieldStates.includes("covered")) {
    throw new Error(`Timeline submit smoke should not start with all fields covered: ${initial.fieldStates.join(",")}`);
  }

  const chosenSource = await clickTimelineCommandFieldChoose(cdp, "p1-hand-spell");
  if (chosenSource !== "p1-hand-spell") {
    throw new Error(`Timeline submit smoke chose wrong source: ${chosenSource}`);
  }
  await delay(150);

  const chosenTarget = await clickTimelineCommandFieldChoose(cdp, "p2-right-1");
  if (chosenTarget !== "p2-right-1") {
    throw new Error(`Timeline submit smoke chose wrong target: ${chosenTarget}`);
  }
  await delay(150);
  const ready = await timelineCommandSubmitSummary(cdp);
  if (ready.routeSummaryState !== "ready") {
    throw new Error(`Timeline submit smoke route summary did not become ready: ${ready.routeSummaryState}`);
  }
  if (!ready.submitStates.includes("ready")) {
    throw new Error(`Timeline submit smoke submit plan did not become ready: ${ready.submitStates.join(",")}`);
  }
  if (!ready.submitCanSubmitStates.includes("true") || !ready.submitCommandReadyStates.includes("true")) {
    throw new Error(`Timeline submit smoke submit gate not ready: canSubmit=${ready.submitCanSubmitStates.join(",")} commandReady=${ready.submitCommandReadyStates.join(",")}`);
  }
  if (!ready.submitEnabledStates.includes("true")) {
    throw new Error(`Timeline submit smoke submit button was not enabled: ${ready.submitEnabledStates.join(",")}`);
  }
  if (!ready.fieldStates.includes("covered") || !ready.fieldStates.includes("server")) {
    throw new Error(`Timeline submit smoke expected covered and server fields before submit: ${ready.fieldStates.join(",")}`);
  }
  if (!ready.selectedObjectIds.includes("p1-hand-spell") || !ready.selectedObjectIds.includes("p2-right-1")) {
    throw new Error(`Timeline submit smoke selected object ids incomplete: ${ready.selectedObjectIds.join(",")}`);
  }

  const submittedType = await clickTimelineCommandSubmit(cdp);
  if (submittedType !== "PLAY_CARD") {
    throw new Error(`Timeline submit smoke clicked wrong submit command: ${submittedType}`);
  }

  const followup = await waitForTimelineCommandFollowup(cdp);
  const failures = [];
  if (followup.commandSubmissionState !== "sent") failures.push(`feedback state ${followup.commandSubmissionState}`);
  if (followup.commandSubmissionCommand !== "PLAY_CARD") failures.push(`feedback command ${followup.commandSubmissionCommand}`);
  if (followup.timelineFollowupState !== "accepted-events") failures.push(`timeline followup state ${followup.timelineFollowupState}`);
  if (followup.timelineFollowupServerState !== "events") failures.push(`timeline server state ${followup.timelineFollowupServerState}`);
  if (followup.timelineFollowupBridgeState !== "ready") failures.push(`timeline bridge state ${followup.timelineFollowupBridgeState}`);
  if (followup.timelineFollowupLayoutState !== "linked") failures.push(`timeline layout projection state ${followup.timelineFollowupLayoutState}`);
  if (followup.timelineFollowupSourceSurface !== "timeline-detail") failures.push(`source surface ${followup.timelineFollowupSourceSurface}`);
  if (followup.timelineFollowupSourceDetail !== "rule:stack:fixture-stack-1") failures.push(`source detail ${followup.timelineFollowupSourceDetail}`);
  if (!followup.timelineFollowupServerKindActions.includes("STACK_ITEM_ADDED")) failures.push("missing STACK_ITEM_ADDED server kind");
  if (!followup.timelineFollowupServerKindActions.includes("BATTLEFIELD_CONTROL_RESOLVED")) failures.push("missing BATTLEFIELD_CONTROL_RESOLVED server kind");
  if (!followup.timelineFollowupText.includes("后续事件")) failures.push("missing followup heading");
  if (!followup.commandSubmissionText.includes("服务端已接受")) failures.push("missing accepted feedback copy");

  const layer = await openCommandSubmissionLayer(cdp);
  if (layer.state !== "open") failures.push(`submission layer state ${layer.state}`);
  if (layer.cmdType !== "PLAY_CARD") failures.push(`submission layer command ${layer.cmdType}`);
  if (layer.followupState !== "accepted-events") failures.push(`submission layer followup state ${layer.followupState}`);
  if (layer.serverState !== "events") failures.push(`submission layer server state ${layer.serverState}`);
  if (layer.sourceSurface !== "timeline-detail") failures.push(`submission layer source surface ${layer.sourceSurface}`);
  if (layer.sourceDetail !== "rule:stack:fixture-stack-1") failures.push(`submission layer source detail ${layer.sourceDetail}`);
  if (layer.eventCount < 2) failures.push(`submission layer event count ${layer.eventCount}`);
  if (!layer.eventKinds.includes("BATTLEFIELD_CONTROL_RESOLVED")) failures.push("submission layer missing battlefield event button");
  if (!layer.layoutObjects.includes("p1-hand-spell") || !layer.layoutObjects.includes("p2-right-1")) {
    failures.push(`submission layer layout projection objects incomplete: ${layer.layoutObjects.join(",")}`);
  }

  const layerEventKind = await clickCommandSubmissionLayerFollowupEvent(cdp, "BATTLEFIELD_CONTROL_RESOLVED", 1);
  if (layerEventKind !== "BATTLEFIELD_CONTROL_RESOLVED") failures.push(`submission layer event click kind ${layerEventKind}`);
  await delay(150);
  const eventDetail = await timelineDetailSummary(cdp);
  if (eventDetail.detailId !== "event:BATTLEFIELD_CONTROL_RESOLVED:1") {
    failures.push(`submission layer event did not open timeline detail: ${eventDetail.detailId}`);
  }
  if (eventDetail.panelState !== "event") failures.push(`submission layer event panel state ${eventDetail.panelState}`);

  const layerObjectId = await clickCommandSubmissionLayerLayoutObject(cdp, "p2-right-1");
  if (layerObjectId !== "p2-right-1") failures.push(`submission layer object click ${layerObjectId}`);
  await delay(150);
  const layerObjectSelection = await evaluateJson(cdp, `(() => {
    const tableObject = document.querySelector('[data-object-id="p2-right-1"]');
    const selectedContext = document.querySelector('[data-wire-selected-object-context="p2-right-1"]');
    return {
      detailLayerOpen: Boolean(document.querySelector(".detail-layer")),
      hasSelectedContext: Boolean(selectedContext),
      selected: tableObject?.getAttribute("data-selected") ?? "",
      selectedContextText: selectedContext?.textContent ?? ""
    };
  })()`);
  if (layerObjectSelection.selected !== "true") failures.push(`submission layer object did not select table object: ${layerObjectSelection.selected}`);
  if (!layerObjectSelection.hasSelectedContext) failures.push("submission layer object did not expose selected object context");
  if (!layerObjectSelection.selectedContextText.includes("对方单位")) failures.push("submission layer selected context missing unit zone");
  if (layerObjectSelection.detailLayerOpen) failures.push("submission layer object projection opened card detail unexpectedly");

  if (failures.length > 0) {
    throw new Error(`Timeline submit smoke failed:\n${failures.join("\n")}\n${JSON.stringify(followup, null, 2)}`);
  }
}

async function runWireRejectedSubmissionSmoke(cdp) {
  const result = await evaluateJson(cdp, `(() => {
    const feedback = document.querySelector(".wire-command-submission-feedback");
    const followup = feedback?.querySelector("[data-command-followup-state]");
    const bridge = followup?.querySelector("[data-command-followup-bridge-state]");
    const layoutProjection = followup?.querySelector("[data-command-followup-layout-state]");
    return {
      command: feedback?.querySelector('[data-command-submission-metric="command"] strong')?.textContent?.trim() ?? "",
      error: feedback?.querySelector('[data-command-submission-metric="error"] strong')?.textContent?.trim() ?? "",
      eventButtonCount: followup?.querySelectorAll("[data-command-followup-event-action]").length ?? 0,
      followupEventCount: Number(followup?.getAttribute("data-command-followup-event-count") ?? "-1"),
      followupHiddenCount: Number(followup?.getAttribute("data-command-followup-hidden-count") ?? "-1"),
      followupServerState: followup?.getAttribute("data-command-followup-server-state") ?? "",
      followupState: followup?.getAttribute("data-command-followup-state") ?? "",
      layoutProjectionState: layoutProjection?.getAttribute("data-command-followup-layout-state") ?? "",
      receipt: feedback?.querySelector('[data-command-submission-metric="receipt"] strong')?.textContent?.trim() ?? "",
      state: feedback?.getAttribute("data-command-submission-state") ?? "",
      text: feedback?.textContent ?? "",
      bridgeState: bridge?.getAttribute("data-command-followup-bridge-state") ?? ""
    };
  })()`);
  const failures = [];
  if (result.state !== "failed") failures.push(`feedback state ${result.state}`);
  if (result.command !== "PLAY_CARD") failures.push(`command ${result.command}`);
  if (result.receipt !== "REJECTED") failures.push(`receipt ${result.receipt}`);
  if (result.error !== "RULE_REJECTED") failures.push(`error ${result.error}`);
  if (result.followupState !== "failed") failures.push(`followup state ${result.followupState}`);
  if (result.followupServerState !== "rejected") failures.push(`followup server state ${result.followupServerState}`);
  if (result.bridgeState !== "failed") failures.push(`bridge state ${result.bridgeState}`);
  if (result.layoutProjectionState !== "empty") failures.push(`layout projection state ${result.layoutProjectionState}`);
  if (result.followupEventCount !== 0) failures.push(`event count ${result.followupEventCount}`);
  if (result.followupHiddenCount !== 0) failures.push(`hidden event count ${result.followupHiddenCount}`);
  if (result.eventButtonCount !== 0) failures.push(`event button count ${result.eventButtonCount}`);
  if (!result.text.includes("命令被服务端规则拒绝")) failures.push("rejected summary missing");

  const layer = await openCommandSubmissionLayer(cdp);
  if (layer.state !== "open") failures.push(`layer state ${layer.state}`);
  if (layer.cmdType !== "PLAY_CARD") failures.push(`layer command ${layer.cmdType}`);
  if (layer.receiptState !== "REJECTED") failures.push(`layer receipt ${layer.receiptState}`);
  if (layer.followupState !== "failed") failures.push(`layer followup state ${layer.followupState}`);
  if (layer.serverState !== "rejected") failures.push(`layer server state ${layer.serverState}`);
  if (layer.eventCount !== 0) failures.push(`layer event count ${layer.eventCount}`);
  if (layer.hiddenCount !== 0) failures.push(`layer hidden count ${layer.hiddenCount}`);
  if (layer.eventKinds.length !== 0) failures.push(`layer event kinds ${layer.eventKinds.join(",")}`);
  if (layer.layoutObjects.length !== 0) failures.push(`layer layout objects ${layer.layoutObjects.join(",")}`);
  if (layer.sourceSurface !== "timeline-detail") failures.push(`layer source surface ${layer.sourceSurface}`);
  if (!layer.text.includes("命令被服务端规则拒绝")) failures.push("layer rejected summary missing");

  if (failures.length > 0) {
    throw new Error(`Rejected submission smoke failed:\n${failures.join("\n")}\n${JSON.stringify({ result, layer }, null, 2)}`);
  }
}

async function runWireSnapshotSubmissionSmoke(cdp) {
  const result = await evaluateJson(cdp, `(() => {
    const feedback = document.querySelector(".wire-command-submission-feedback");
    const followup = feedback?.querySelector("[data-command-followup-state]");
    const bridge = followup?.querySelector("[data-command-followup-bridge-state]");
    const layoutProjection = followup?.querySelector("[data-command-followup-layout-state]");
    return {
      command: feedback?.querySelector('[data-command-submission-metric="command"] strong')?.textContent?.trim() ?? "",
      eventButtonCount: followup?.querySelectorAll("[data-command-followup-event-action]").length ?? 0,
      followupEventCount: Number(followup?.getAttribute("data-command-followup-event-count") ?? "-1"),
      followupHiddenCount: Number(followup?.getAttribute("data-command-followup-hidden-count") ?? "-1"),
      followupServerState: followup?.getAttribute("data-command-followup-server-state") ?? "",
      followupState: followup?.getAttribute("data-command-followup-state") ?? "",
      layoutProjectionState: layoutProjection?.getAttribute("data-command-followup-layout-state") ?? "",
      receipt: feedback?.querySelector('[data-command-submission-metric="receipt"] strong')?.textContent?.trim() ?? "",
      state: feedback?.getAttribute("data-command-submission-state") ?? "",
      text: feedback?.textContent ?? "",
      bridgeState: bridge?.getAttribute("data-command-followup-bridge-state") ?? ""
    };
  })()`);
  const failures = [];
  if (result.state !== "sent") failures.push(`feedback state ${result.state}`);
  if (result.command !== "PLAY_CARD") failures.push(`command ${result.command}`);
  if (result.receipt !== "ACCEPTED") failures.push(`receipt ${result.receipt}`);
  if (result.followupState !== "accepted-snapshot") failures.push(`followup state ${result.followupState}`);
  if (result.followupServerState !== "snapshot-prompt") failures.push(`followup server state ${result.followupServerState}`);
  if (result.bridgeState !== "ready") failures.push(`bridge state ${result.bridgeState}`);
  if (result.layoutProjectionState !== "empty") failures.push(`layout projection state ${result.layoutProjectionState}`);
  if (result.followupEventCount !== 0) failures.push(`event count ${result.followupEventCount}`);
  if (result.followupHiddenCount !== 0) failures.push(`hidden event count ${result.followupHiddenCount}`);
  if (result.eventButtonCount !== 0) failures.push(`event button count ${result.eventButtonCount}`);
  if (!result.text.includes("快照/提示已同步")) failures.push("snapshot bridge headline missing");
  if (!result.text.includes("无公开事件")) failures.push("snapshot summary missing");

  const layer = await openCommandSubmissionLayer(cdp);
  if (layer.state !== "open") failures.push(`layer state ${layer.state}`);
  if (layer.cmdType !== "PLAY_CARD") failures.push(`layer command ${layer.cmdType}`);
  if (layer.receiptState !== "ACCEPTED") failures.push(`layer receipt ${layer.receiptState}`);
  if (layer.followupState !== "accepted-snapshot") failures.push(`layer followup state ${layer.followupState}`);
  if (layer.serverState !== "snapshot-prompt") failures.push(`layer server state ${layer.serverState}`);
  if (layer.eventCount !== 0) failures.push(`layer event count ${layer.eventCount}`);
  if (layer.hiddenCount !== 0) failures.push(`layer hidden count ${layer.hiddenCount}`);
  if (layer.eventKinds.length !== 0) failures.push(`layer event kinds ${layer.eventKinds.join(",")}`);
  if (layer.layoutObjects.length !== 0) failures.push(`layer layout objects ${layer.layoutObjects.join(",")}`);
  if (layer.sourceSurface !== "timeline-detail") failures.push(`layer source surface ${layer.sourceSurface}`);
  if (!layer.text.includes("快照/提示已同步")) failures.push("layer snapshot bridge headline missing");
  if (!layer.text.includes("无公开事件")) failures.push("layer snapshot summary missing");

  if (failures.length > 0) {
    throw new Error(`Snapshot submission smoke failed:\n${failures.join("\n")}\n${JSON.stringify({ result, layer }, null, 2)}`);
  }
}

async function runWireSilentSubmissionSmoke(cdp) {
  const result = await evaluateJson(cdp, `(() => {
    const feedback = document.querySelector(".wire-command-submission-feedback");
    const followup = feedback?.querySelector("[data-command-followup-state]");
    const bridge = followup?.querySelector("[data-command-followup-bridge-state]");
    const layoutProjection = followup?.querySelector("[data-command-followup-layout-state]");
    return {
      command: feedback?.querySelector('[data-command-submission-metric="command"] strong')?.textContent?.trim() ?? "",
      eventButtonCount: followup?.querySelectorAll("[data-command-followup-event-action]").length ?? 0,
      followupEventCount: Number(followup?.getAttribute("data-command-followup-event-count") ?? "-1"),
      followupHiddenCount: Number(followup?.getAttribute("data-command-followup-hidden-count") ?? "-1"),
      followupServerState: followup?.getAttribute("data-command-followup-server-state") ?? "",
      followupState: followup?.getAttribute("data-command-followup-state") ?? "",
      layoutProjectionState: layoutProjection?.getAttribute("data-command-followup-layout-state") ?? "",
      receipt: feedback?.querySelector('[data-command-submission-metric="receipt"] strong')?.textContent?.trim() ?? "",
      state: feedback?.getAttribute("data-command-submission-state") ?? "",
      text: feedback?.textContent ?? "",
      bridgeState: bridge?.getAttribute("data-command-followup-bridge-state") ?? ""
    };
  })()`);
  const failures = [];
  if (result.state !== "sent") failures.push(`feedback state ${result.state}`);
  if (result.command !== "PLAY_CARD") failures.push(`command ${result.command}`);
  if (result.receipt !== "ACCEPTED") failures.push(`receipt ${result.receipt}`);
  if (result.followupState !== "accepted-silent") failures.push(`followup state ${result.followupState}`);
  if (result.followupServerState !== "silent") failures.push(`followup server state ${result.followupServerState}`);
  if (result.bridgeState !== "ready") failures.push(`bridge state ${result.bridgeState}`);
  if (result.layoutProjectionState !== "empty") failures.push(`layout projection state ${result.layoutProjectionState}`);
  if (result.followupEventCount !== 0) failures.push(`event count ${result.followupEventCount}`);
  if (result.followupHiddenCount !== 0) failures.push(`hidden event count ${result.followupHiddenCount}`);
  if (result.eventButtonCount !== 0) failures.push(`event button count ${result.eventButtonCount}`);
  if (!result.text.includes("静默接受")) failures.push("silent bridge headline missing");
  if (!result.text.includes("未生成公开事件或广播视图")) failures.push("silent summary missing");

  const layer = await openCommandSubmissionLayer(cdp);
  if (layer.state !== "open") failures.push(`layer state ${layer.state}`);
  if (layer.cmdType !== "PLAY_CARD") failures.push(`layer command ${layer.cmdType}`);
  if (layer.receiptState !== "ACCEPTED") failures.push(`layer receipt ${layer.receiptState}`);
  if (layer.followupState !== "accepted-silent") failures.push(`layer followup state ${layer.followupState}`);
  if (layer.serverState !== "silent") failures.push(`layer server state ${layer.serverState}`);
  if (layer.eventCount !== 0) failures.push(`layer event count ${layer.eventCount}`);
  if (layer.hiddenCount !== 0) failures.push(`layer hidden count ${layer.hiddenCount}`);
  if (layer.eventKinds.length !== 0) failures.push(`layer event kinds ${layer.eventKinds.join(",")}`);
  if (layer.layoutObjects.length !== 0) failures.push(`layer layout objects ${layer.layoutObjects.join(",")}`);
  if (layer.sourceSurface !== "timeline-detail") failures.push(`layer source surface ${layer.sourceSurface}`);
  if (!layer.text.includes("静默接受")) failures.push("layer silent bridge headline missing");
  if (!layer.text.includes("未生成公开事件或广播视图")) failures.push("layer silent summary missing");

  if (failures.length > 0) {
    throw new Error(`Silent submission smoke failed:\n${failures.join("\n")}\n${JSON.stringify({ result, layer }, null, 2)}`);
  }
}

async function runWireRuleObjectRefSmoke(cdp) {
  const initial = await evaluateJson(cdp, `(() => ({
    battlefieldRefs: document.querySelectorAll('[data-rule-object-ref="fixture-left-battlefield"]').length,
    eventRefs: document.querySelectorAll('[data-event-object-ref="p1-hand-spell"]').length,
    eventRefInspectableStates: Array.from(document.querySelectorAll('[data-event-object-ref="p1-hand-spell"]'))
      .map((item) => item.getAttribute("data-object-ref-inspectable")),
    eventRefVisibilityStates: Array.from(document.querySelectorAll('[data-event-object-ref="p1-hand-spell"]'))
      .map((item) => item.getAttribute("data-object-ref-visibility")),
    eventRefZoneLabels: Array.from(document.querySelectorAll('[data-event-object-ref="p1-hand-spell"]'))
      .map((item) => item.getAttribute("data-object-ref-zone-label") ?? ""),
    unitRefs: document.querySelectorAll('[data-rule-object-ref="p2-right-1"]').length,
    hiddenRefs: document.querySelectorAll('[data-rule-object-ref="HIDDEN"]').length,
    responsibilitySubmitReadyStates: Array.from(document.querySelectorAll("[data-rule-responsibility-submit-ready]"))
      .map((item) => item.getAttribute("data-rule-responsibility-submit-ready") ?? ""),
    responsibilitySubmitStates: Array.from(document.querySelectorAll("[data-rule-responsibility-submit-state]"))
      .map((item) => item.getAttribute("data-rule-responsibility-submit-state") ?? ""),
    responsibilitySubmitText: document.querySelector(".wire-rule-responsibility")?.textContent ?? ""
  }))()`);

  await clickButtonByText(cdp, "打开责任检查层");
  await delay(150);
  const responsibilityLayerResult = await evaluateJson(cdp, `(() => {
    const layer = document.querySelector(".wire-rule-responsibility-layer");
    return {
      activeCount: layer?.getAttribute("data-rule-responsibility-layer-active-count") ?? "",
      activeText: document.activeElement?.textContent ?? "",
      hiddenBoundary: layer?.querySelector("[data-rule-responsibility-layer-hidden-boundary]")?.getAttribute("data-rule-responsibility-layer-hidden-boundary") ?? "",
      hiddenRefCount: layer?.querySelectorAll('[data-rule-object-ref="HIDDEN"]').length ?? 0,
      itemCount: layer?.getAttribute("data-rule-responsibility-layer-item-count") ?? "",
      itemStates: Array.from(layer?.querySelectorAll("[data-rule-responsibility-layer-state]") ?? [])
        .map((item) => item.getAttribute("data-rule-responsibility-layer-state") ?? ""),
      modal: layer?.getAttribute("aria-modal") ?? "",
      open: Boolean(layer),
      readyCount: layer?.getAttribute("data-rule-responsibility-layer-ready-count") ?? "",
      role: layer?.getAttribute("role") ?? "",
      sourceRefCount: layer?.querySelectorAll('[data-rule-object-ref="p1-hand-spell"]').length ?? 0,
      state: layer?.getAttribute("data-rule-responsibility-layer-state") ?? "",
      submitReadyStates: Array.from(layer?.querySelectorAll("[data-rule-responsibility-layer-submit-ready]") ?? [])
        .map((item) => item.getAttribute("data-rule-responsibility-layer-submit-ready") ?? ""),
      submitStates: Array.from(layer?.querySelectorAll("[data-rule-responsibility-layer-submit-state]") ?? [])
        .map((item) => item.getAttribute("data-rule-responsibility-layer-submit-state") ?? ""),
      targetRefCount: layer?.querySelectorAll('[data-rule-object-ref="p2-right-1"]').length ?? 0,
      text: layer?.textContent ?? "",
      title: layer?.querySelector("#wire-rule-responsibility-layer-title")?.textContent ?? ""
    };
  })()`);
  const responsibilityLayerObjectClicked = await evaluateJson(cdp, `(() => {
    const ref = document.querySelector('.wire-rule-responsibility-layer [data-rule-object-ref="p2-right-1"]');
    ref?.click();
    return Boolean(ref);
  })()`);
  await delay(150);
  const responsibilityLayerObjectResult = await evaluateJson(cdp, `(() => {
    const tableObject = document.querySelector('[data-object-id="p2-right-1"]');
    const selectedRef = document.querySelector('.wire-rule-responsibility-layer [data-rule-object-ref="p2-right-1"][data-selected="true"]');
    return {
      clicked: ${JSON.stringify(responsibilityLayerObjectClicked)},
      detailLayerOpen: Boolean(document.querySelector(".detail-layer")),
      selected: tableObject?.getAttribute("data-selected") ?? null,
      selectedRef: Boolean(selectedRef)
    };
  })()`);
  await pressEscape(cdp);
  await delay(120);
  const responsibilityLayerClosed = await evaluateJson(cdp, `(() => !document.querySelector(".wire-rule-responsibility-layer"))()`);

  await clickButtonByText(cdp, "打开流程检查层");
  await delay(150);
  const serverFlowLayerResult = await evaluateJson(cdp, `(() => {
    const layer = document.querySelector(".wire-server-flow-layer");
    return {
      activeText: document.activeElement?.textContent ?? "",
      actionCandidates: Array.from(layer?.querySelectorAll("[data-wire-server-flow-layer-action-candidates]") ?? [])
        .map((item) => item.getAttribute("data-wire-server-flow-layer-action-candidates") ?? ""),
      actionInspectableStates: Array.from(layer?.querySelectorAll("[data-wire-server-flow-layer-action-inspectable]") ?? [])
        .map((item) => item.getAttribute("data-wire-server-flow-layer-action-inspectable") ?? ""),
      actionObjectIds: Array.from(layer?.querySelectorAll("[data-wire-server-flow-layer-action-object-id]") ?? [])
        .map((item) => item.getAttribute("data-wire-server-flow-layer-action-object-id") ?? ""),
      actionStates: Array.from(layer?.querySelectorAll("[data-wire-server-flow-layer-action-state]") ?? [])
        .map((item) => item.getAttribute("data-wire-server-flow-layer-action-state") ?? ""),
      authority: layer?.querySelector("[data-wire-server-flow-layer-authority]")?.getAttribute("data-wire-server-flow-layer-authority") ?? "",
      flowState: layer?.getAttribute("data-wire-server-flow-layer-flow-state") ?? "",
      laneCount: layer?.getAttribute("data-wire-server-flow-layer-lane-count") ?? "",
      modal: layer?.getAttribute("aria-modal") ?? "",
      open: Boolean(layer),
      relatedCount: layer?.getAttribute("data-wire-server-flow-layer-related-count") ?? "",
      role: layer?.getAttribute("role") ?? "",
      sourceRefCount: layer?.querySelectorAll('[data-rule-object-ref="p1-hand-spell"]').length ?? 0,
      state: layer?.getAttribute("data-wire-server-flow-layer-state") ?? "",
      stepCount: layer?.getAttribute("data-wire-server-flow-layer-step-count") ?? "",
      stepDetailStates: Array.from(layer?.querySelectorAll("[data-wire-server-flow-layer-step-detail]") ?? [])
        .map((item) => item.getAttribute("data-wire-server-flow-layer-step-detail") ?? ""),
      stepRoles: Array.from(layer?.querySelectorAll("[data-wire-server-flow-layer-step-role]") ?? [])
        .map((item) => item.getAttribute("data-wire-server-flow-layer-step-role") ?? ""),
      stepStates: Array.from(layer?.querySelectorAll("[data-wire-server-flow-layer-step-state]") ?? [])
        .map((item) => item.getAttribute("data-wire-server-flow-layer-step-state") ?? ""),
      targetRefCount: layer?.querySelectorAll('[data-rule-object-ref="p2-right-1"]').length ?? 0,
      text: layer?.textContent ?? "",
      title: layer?.querySelector("#wire-server-flow-layer-title")?.textContent ?? ""
    };
  })()`);
  const serverFlowLayerObjectClicked = await evaluateJson(cdp, `(() => {
    const ref = document.querySelector('.wire-server-flow-layer [data-rule-object-ref="p2-right-1"]');
    ref?.click();
    return Boolean(ref);
  })()`);
  await delay(150);
  const serverFlowLayerObjectResult = await evaluateJson(cdp, `(() => {
    const tableObject = document.querySelector('[data-object-id="p2-right-1"]');
    const selectedRef = document.querySelector('.wire-server-flow-layer [data-rule-object-ref="p2-right-1"][data-selected="true"]');
    return {
      clicked: ${JSON.stringify(serverFlowLayerObjectClicked)},
      detailLayerOpen: Boolean(document.querySelector(".detail-layer")),
      selected: tableObject?.getAttribute("data-selected") ?? null,
      selectedRef: Boolean(selectedRef)
    };
  })()`);
  await pressEscape(cdp);
  await delay(120);
  const serverFlowLayerClosed = await evaluateJson(cdp, `(() => !document.querySelector(".wire-server-flow-layer"))()`);

  await clickRuleObjectRef(cdp, "fixture-left-battlefield");
  await delay(150);
  const battlefieldResult = await evaluateJson(cdp, `(() => {
    const tableObject = document.querySelector('[data-object-id="fixture-left-battlefield"]');
    const selectedRef = document.querySelector('[data-rule-object-ref="fixture-left-battlefield"][data-selected="true"]');
    const selectedObjectContext = document.querySelector('[data-wire-selected-object-context="fixture-left-battlefield"]');
    const selectedLayout = document.querySelector("[data-wire-table-selected-layout-state]");
    return {
      contextAuthority: selectedObjectContext?.querySelector(".wire-object-context")?.getAttribute("data-wire-object-context-authority") ?? null,
      contextSource: selectedObjectContext?.querySelector(".wire-object-context")?.getAttribute("data-wire-object-context-source") ?? null,
      contextText: selectedObjectContext?.textContent ?? "",
      selected: tableObject?.getAttribute("data-selected") ?? null,
      selectedRef: Boolean(selectedRef),
      selectedLayoutKind: selectedLayout?.getAttribute("data-wire-table-selected-layout-kind") ?? null,
      selectedLayoutState: selectedLayout?.getAttribute("data-wire-table-selected-layout-state") ?? null,
      selectedLayoutZone: selectedLayout?.getAttribute("data-wire-table-selected-layout-zone") ?? null,
      hasSelectedObjectContext: Boolean(selectedObjectContext),
      detailLayerOpen: Boolean(document.querySelector(".detail-layer"))
    };
  })()`);

  await clickRuleObjectRef(cdp, "p2-right-1");
  await delay(150);
  const unitResult = await evaluateJson(cdp, `(() => {
    const tableObject = document.querySelector('[data-object-id="p2-right-1"]');
    const selectedRef = document.querySelector('[data-rule-object-ref="p2-right-1"][data-selected="true"]');
    const selectedLayout = document.querySelector("[data-wire-table-selected-layout-state]");
    return {
      selected: tableObject?.getAttribute("data-selected") ?? null,
      selectedRef: Boolean(selectedRef),
      selectedLayoutCapacityRow: selectedLayout?.getAttribute("data-wire-table-selected-layout-capacity-row") ?? null,
      selectedLayoutKind: selectedLayout?.getAttribute("data-wire-table-selected-layout-kind") ?? null,
      selectedLayoutState: selectedLayout?.getAttribute("data-wire-table-selected-layout-state") ?? null,
      selectedLayoutZone: selectedLayout?.getAttribute("data-wire-table-selected-layout-zone") ?? null,
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
      selectedRefInspectable: selectedRef?.getAttribute("data-object-ref-inspectable") ?? null,
      selectedRefVisibility: selectedRef?.getAttribute("data-object-ref-visibility") ?? null,
      selectedRefZoneLabel: selectedRef?.getAttribute("data-object-ref-zone-label") ?? null,
      detailLayerOpen: Boolean(document.querySelector(".detail-layer"))
    };
  })()`);

  await clickWireDetail(cdp, "rule:stack:fixture-stack-1");
  await delay(150);
  const ruleDetailResult = await evaluateJson(cdp, `(() => {
    const panel = document.querySelector(".wire-timeline-detail");
    const routeSummary = panel?.querySelector(".wire-timeline-route-summary");
    const timelineFollowup = panel?.querySelector(".wire-timeline-command-followup");
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
      openLayerTriggerControls: panel?.querySelector("[data-wire-timeline-layer-open-trigger]")?.getAttribute("aria-controls") ?? "",
      openLayerTriggerCount: panel?.querySelectorAll("[data-wire-timeline-layer-open-trigger]").length ?? 0,
      openLayerTriggerText: panel?.querySelector("[data-wire-timeline-layer-open-trigger]")?.textContent ?? "",
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
      routeSummaryLabel: routeSummary?.getAttribute("aria-label") ?? "",
      routeSummaryState: routeSummary?.getAttribute("data-timeline-route-summary-state") ?? null,
      routeSummaryCountKeys: Array.from(routeSummary?.querySelectorAll("[data-timeline-route-count]") ?? [])
        .map((item) => item.getAttribute("data-timeline-route-count") ?? ""),
      routeSummaryCountStates: Array.from(routeSummary?.querySelectorAll("[data-timeline-route-count-state]") ?? [])
        .map((item) => item.getAttribute("data-timeline-route-count-state")),
      routeSummaryText: routeSummary?.textContent ?? "",
      timelineFollowupBridgeState: timelineFollowup?.querySelector(".wire-command-followup-bridge")?.getAttribute("data-command-followup-bridge-state") ?? "",
      timelineFollowupLayoutState: timelineFollowup?.querySelector("[data-command-followup-layout-state]")?.getAttribute("data-command-followup-layout-state") ?? "",
      timelineFollowupServerState: timelineFollowup?.getAttribute("data-command-followup-server-state") ?? "",
      timelineFollowupServerKindActions: Array.from(timelineFollowup?.querySelectorAll("[data-command-followup-server-event-kind-action]") ?? [])
        .map((item) => item.getAttribute("data-command-followup-server-event-kind-action") ?? ""),
      timelineFollowupServerKindOrders: Array.from(timelineFollowup?.querySelectorAll("[data-command-followup-server-event-order]") ?? [])
        .map((item) => item.getAttribute("data-command-followup-server-event-order") ?? ""),
      timelineFollowupServerKindSources: Array.from(timelineFollowup?.querySelectorAll("[data-command-followup-server-event-kind-source]") ?? [])
        .map((item) => item.getAttribute("data-command-followup-server-event-kind-source") ?? ""),
      timelineFollowupServerKindStates: Array.from(timelineFollowup?.querySelectorAll("[data-command-followup-server-event-kind-state]") ?? [])
        .map((item) => item.getAttribute("data-command-followup-server-event-kind-state") ?? ""),
      timelineFollowupSourceSurface: timelineFollowup?.querySelector("[data-command-followup-source-surface]")?.getAttribute("data-command-followup-source-surface") ?? "",
      timelineFollowupState: timelineFollowup?.getAttribute("data-command-followup-state") ?? "",
      timelineFollowupText: timelineFollowup?.textContent ?? "",
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
      commandBridgeDetailRoles: Array.from(panel?.querySelectorAll("[data-timeline-command-bridge-detail-role]") ?? [])
        .map((item) => item.getAttribute("data-timeline-command-bridge-detail-role") ?? ""),
      commandBridgeFieldStates: Array.from(panel?.querySelectorAll("[data-timeline-command-field-state]") ?? [])
        .map((item) => item.getAttribute("data-timeline-command-field-state")),
      commandBridgeFieldDetailCounts: Array.from(panel?.querySelectorAll("[data-timeline-command-field-detail-count]") ?? [])
        .map((item) => item.getAttribute("data-timeline-command-field-detail-count")),
      commandBridgeFieldSelectedCounts: Array.from(panel?.querySelectorAll("[data-timeline-command-field-selected-count]") ?? [])
        .map((item) => item.getAttribute("data-timeline-command-field-selected-count")),
      commandBridgeFieldCandidateChoiceCounts: Array.from(panel?.querySelectorAll("[data-timeline-command-field-candidate-choice-count]") ?? [])
        .map((item) => item.getAttribute("data-timeline-command-field-candidate-choice-count")),
      commandBridgeFieldDetailObjectIds: Array.from(panel?.querySelectorAll("[data-timeline-command-field-detail-object-id]") ?? [])
        .map((item) => item.getAttribute("data-timeline-command-field-detail-object-id") ?? ""),
      commandBridgeFieldSelectedObjectIds: Array.from(panel?.querySelectorAll("[data-timeline-command-field-selected-object-id]") ?? [])
        .map((item) => item.getAttribute("data-timeline-command-field-selected-object-id") ?? ""),
      commandBridgeFieldChooseObjectIds: Array.from(panel?.querySelectorAll("[data-timeline-command-field-choose-object-id]") ?? [])
        .map((item) => item.getAttribute("data-timeline-command-field-choose-object-id") ?? ""),
      commandBridgeFieldChooseEnabledStates: Array.from(panel?.querySelectorAll("[data-timeline-command-field-choose-object-id]") ?? [])
        .map((item) => item.getAttribute("data-timeline-command-field-choose-enabled") ?? ""),
      commandBridgeGrammarStates: Array.from(panel?.querySelectorAll("[data-timeline-command-grammar-state]") ?? [])
        .map((item) => item.getAttribute("data-timeline-command-grammar-state")),
      commandBridgeGrammarStepStates: Array.from(panel?.querySelectorAll("[data-timeline-command-grammar-step-state]") ?? [])
        .map((item) => item.getAttribute("data-timeline-command-grammar-step-state")),
      commandBridgeGateStates: Array.from(panel?.querySelectorAll("[data-timeline-command-gate-state]") ?? [])
        .map((item) => item.getAttribute("data-timeline-command-gate-state")),
      commandBridgeSubmitCanSubmit: Array.from(panel?.querySelectorAll("[data-timeline-command-submit-can-submit]") ?? [])
        .map((item) => item.getAttribute("data-timeline-command-submit-can-submit")),
      commandBridgeSubmitCommandReady: Array.from(panel?.querySelectorAll("[data-timeline-command-submit-command-ready]") ?? [])
        .map((item) => item.getAttribute("data-timeline-command-submit-command-ready")),
      commandBridgeSubmitButtonCount: panel?.querySelectorAll("[data-timeline-command-submit]").length ?? 0,
      commandBridgeSubmitButtonEnabledStates: Array.from(panel?.querySelectorAll("[data-timeline-command-submit]") ?? [])
        .map((item) => item.getAttribute("data-timeline-command-submit-enabled")),
      commandBridgeSubmitFieldStates: Array.from(panel?.querySelectorAll("[data-timeline-command-submit-field-state]") ?? [])
        .map((item) => item.getAttribute("data-timeline-command-submit-field-state")),
      commandBridgeSubmitStates: Array.from(panel?.querySelectorAll("[data-timeline-command-submit-state]") ?? [])
        .map((item) => item.getAttribute("data-timeline-command-submit-state")),
      commandBridgeSubmitText: Array.from(panel?.querySelectorAll(".wire-timeline-command-submit-plan") ?? [])
        .map((item) => item.textContent ?? "").join(" / "),
      commandBridgeSubmitTypes: Array.from(panel?.querySelectorAll("[data-timeline-command-submit-type]") ?? [])
        .map((item) => item.getAttribute("data-timeline-command-submit-type") ?? ""),
      commandBridgeOpenDetailButtonCount: panel?.querySelectorAll("[data-timeline-command-open-detail-object-id]").length ?? 0,
      commandBridgeOpenDetailObjectIds: Array.from(panel?.querySelectorAll("[data-timeline-command-open-detail-object-id]") ?? [])
        .map((item) => item.getAttribute("data-timeline-command-open-detail-object-id") ?? ""),
      commandBridgeNextButtonCount: panel?.querySelectorAll("[data-timeline-command-bridge-next-object-id]").length ?? 0,
      commandBridgeNextObjectIds: Array.from(panel?.querySelectorAll("[data-timeline-command-bridge-next-object-id]") ?? [])
        .map((item) => item.getAttribute("data-timeline-command-bridge-next-object-id") ?? ""),
      commandBridgeRouteStates: Array.from(panel?.querySelectorAll(".wire-timeline-command-bridge li") ?? [])
        .map((item) => item.getAttribute("data-timeline-command-bridge-route-state")),
      commandBridgeRowCount: panel?.querySelectorAll(".wire-timeline-command-bridge li").length ?? 0,
      commandBridgeServerRoles: Array.from(panel?.querySelectorAll("[data-timeline-command-bridge-server-role]") ?? [])
        .map((item) => item.getAttribute("data-timeline-command-bridge-server-role") ?? ""),
      commandBridgeText: panel?.querySelector(".wire-timeline-command-bridge")?.textContent ?? "",
      evidenceKeys: Array.from(panel?.querySelectorAll("[data-timeline-evidence]") ?? [])
        .map((item) => item.getAttribute("data-timeline-evidence") ?? ""),
      evidenceStates: Array.from(panel?.querySelectorAll("[data-timeline-evidence-state]") ?? [])
        .map((item) => item.getAttribute("data-timeline-evidence-state") ?? ""),
      evidenceText: panel?.querySelector(".wire-timeline-evidence-list")?.textContent ?? "",
      nextStepButtonCount: panel?.querySelectorAll("[data-timeline-next-step-object-id]").length ?? 0,
      nextStepCheckKeys: Array.from(panel?.querySelectorAll("[data-timeline-next-step-check]") ?? [])
        .map((item) => item.getAttribute("data-timeline-next-step-check") ?? ""),
      nextStepCheckStates: Array.from(panel?.querySelectorAll("[data-timeline-next-step-check-state]") ?? [])
        .map((item) => item.getAttribute("data-timeline-next-step-check-state") ?? ""),
      nextStepGrammarRoles: Array.from(panel?.querySelectorAll("[data-timeline-next-step-grammar-role]") ?? [])
        .map((item) => item.getAttribute("data-timeline-next-step-grammar-role") ?? ""),
      nextStepGrammarStates: Array.from(panel?.querySelectorAll("[data-timeline-next-step-grammar-state]") ?? [])
        .map((item) => item.getAttribute("data-timeline-next-step-grammar-state") ?? ""),
      nextStepObjectIds: Array.from(panel?.querySelectorAll("[data-timeline-next-step-object-id]") ?? [])
        .map((item) => item.getAttribute("data-timeline-next-step-object-id") ?? ""),
      nextStepState: panel?.querySelector("[data-timeline-next-step-state]")?.getAttribute("data-timeline-next-step-state") ?? null,
      nextStepText: panel?.querySelector(".wire-timeline-next-step")?.textContent ?? "",
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

  await clickButtonByText(cdp, "打开检查层");
  await delay(150);
  const timelineLayerResult = await evaluateJson(cdp, `(() => {
    const layer = document.querySelector(".wire-timeline-detail-layer");
    const panel = layer?.querySelector(".wire-timeline-detail");
    const timelineFollowup = panel?.querySelector(".wire-timeline-command-followup");
    return {
      activeText: document.activeElement?.textContent ?? "",
      bodyId: layer?.querySelector("#wire-timeline-detail-layer-body")?.id ?? "",
      cardDetailOpen: Boolean(document.querySelector(".detail-layer")),
      detailId: layer?.getAttribute("data-wire-timeline-detail-layer-detail-id") ?? "",
      modal: layer?.getAttribute("aria-modal") ?? "",
      open: Boolean(layer),
      panelDetailId: panel?.getAttribute("data-wire-timeline-detail-id") ?? "",
      panelState: panel?.getAttribute("data-wire-timeline-detail-state") ?? "",
      role: layer?.getAttribute("role") ?? "",
      routeSummaryLabel: panel?.querySelector(".wire-timeline-route-summary")?.getAttribute("aria-label") ?? "",
      routeSummaryState: panel?.querySelector(".wire-timeline-route-summary")?.getAttribute("data-timeline-route-summary-state") ?? "",
      source: layer?.getAttribute("data-wire-timeline-detail-layer-source") ?? "",
      state: layer?.getAttribute("data-wire-timeline-detail-layer-state") ?? "",
      timelineFollowupBridgeState: timelineFollowup?.querySelector(".wire-command-followup-bridge")?.getAttribute("data-command-followup-bridge-state") ?? "",
      timelineFollowupState: timelineFollowup?.getAttribute("data-command-followup-state") ?? "",
      timelineFollowupText: timelineFollowup?.textContent ?? "",
      text: layer?.textContent ?? "",
      title: layer?.querySelector("#wire-timeline-detail-layer-title")?.textContent ?? ""
    };
  })()`);
  await pressEscape(cdp);
  await delay(120);
  const timelineLayerClosed = await evaluateJson(cdp, `(() => !document.querySelector(".wire-timeline-detail-layer"))()`);

  const commandBridgeDetailObjectId = await clickTimelineCommandBridgeDetail(cdp, "p1-hand-spell");
  await delay(150);
  const commandBridgeDetailResult = await evaluateJson(cdp, `(() => {
    const detail = document.querySelector(".detail-layer");
    const actions = detail?.querySelector("[data-card-detail-actions-state]");
    const actionRoutes = detail?.querySelector(".detail-action-routes");
    return {
      actionCount: Number(actions?.querySelector("[data-card-detail-action-count]")?.getAttribute("data-card-detail-action-count") ?? "0"),
      actionRouteCount: Number(actionRoutes?.getAttribute("data-card-detail-route-count") ?? "0"),
      actionRouteStates: Array.from(actionRoutes?.querySelectorAll("[data-card-detail-action-route-state]") ?? [])
        .map((node) => node.getAttribute("data-card-detail-action-route-state")),
      actionRouteText: actionRoutes?.textContent ?? "",
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

  const commandBridgeObjectId = await clickTimelineCommandFieldChoose(cdp, "p2-right-1");
  await delay(150);
  const commandBridgeFocusResult = await evaluateJson(cdp, `(() => {
    const objectId = ${JSON.stringify(commandBridgeObjectId)};
    const sourceObject = document.querySelector('[data-object-id="p1-hand-spell"]');
    const targetObject = document.querySelector(\`[data-object-id="\${objectId}"]\`);
    const candidatePlan = document.querySelector('[data-candidate-plan-action="PLAY_CARD"]');
    const panel = document.querySelector(".wire-timeline-detail");
    const routeSummary = panel?.querySelector(".wire-timeline-route-summary");
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
      routeSummaryState: routeSummary?.getAttribute("data-timeline-route-summary-state") ?? null,
      routeSummaryCountStates: Array.from(routeSummary?.querySelectorAll("[data-timeline-route-count-state]") ?? [])
        .map((item) => item.getAttribute("data-timeline-route-count-state")),
      routeSummaryText: routeSummary?.textContent ?? "",
      commandBridgeDraftActiveStates: Array.from(panel?.querySelectorAll(".wire-timeline-command-bridge li") ?? [])
        .map((item) => item.getAttribute("data-timeline-command-bridge-draft-active")),
      commandBridgeDetailRoles: Array.from(panel?.querySelectorAll("[data-timeline-command-bridge-detail-role]") ?? [])
        .map((item) => item.getAttribute("data-timeline-command-bridge-detail-role") ?? ""),
      commandBridgeFieldStates: Array.from(panel?.querySelectorAll("[data-timeline-command-field-state]") ?? [])
        .map((item) => item.getAttribute("data-timeline-command-field-state")),
      commandBridgeFieldDetailCounts: Array.from(panel?.querySelectorAll("[data-timeline-command-field-detail-count]") ?? [])
        .map((item) => item.getAttribute("data-timeline-command-field-detail-count")),
      commandBridgeFieldSelectedCounts: Array.from(panel?.querySelectorAll("[data-timeline-command-field-selected-count]") ?? [])
        .map((item) => item.getAttribute("data-timeline-command-field-selected-count")),
      commandBridgeFieldCandidateChoiceCounts: Array.from(panel?.querySelectorAll("[data-timeline-command-field-candidate-choice-count]") ?? [])
        .map((item) => item.getAttribute("data-timeline-command-field-candidate-choice-count")),
      commandBridgeFieldDetailObjectIds: Array.from(panel?.querySelectorAll("[data-timeline-command-field-detail-object-id]") ?? [])
        .map((item) => item.getAttribute("data-timeline-command-field-detail-object-id") ?? ""),
      commandBridgeFieldSelectedObjectIds: Array.from(panel?.querySelectorAll("[data-timeline-command-field-selected-object-id]") ?? [])
        .map((item) => item.getAttribute("data-timeline-command-field-selected-object-id") ?? ""),
      commandBridgeFieldChooseObjectIds: Array.from(panel?.querySelectorAll("[data-timeline-command-field-choose-object-id]") ?? [])
        .map((item) => item.getAttribute("data-timeline-command-field-choose-object-id") ?? ""),
      commandBridgeFieldChooseEnabledStates: Array.from(panel?.querySelectorAll("[data-timeline-command-field-choose-object-id]") ?? [])
        .map((item) => item.getAttribute("data-timeline-command-field-choose-enabled") ?? ""),
      commandBridgeGrammarStates: Array.from(panel?.querySelectorAll("[data-timeline-command-grammar-state]") ?? [])
        .map((item) => item.getAttribute("data-timeline-command-grammar-state")),
      commandBridgeGrammarStepStates: Array.from(panel?.querySelectorAll("[data-timeline-command-grammar-step-state]") ?? [])
        .map((item) => item.getAttribute("data-timeline-command-grammar-step-state")),
      commandBridgeGateStates: Array.from(panel?.querySelectorAll("[data-timeline-command-gate-state]") ?? [])
        .map((item) => item.getAttribute("data-timeline-command-gate-state")),
      commandBridgeSubmitCanSubmit: Array.from(panel?.querySelectorAll("[data-timeline-command-submit-can-submit]") ?? [])
        .map((item) => item.getAttribute("data-timeline-command-submit-can-submit")),
      commandBridgeSubmitCommandReady: Array.from(panel?.querySelectorAll("[data-timeline-command-submit-command-ready]") ?? [])
        .map((item) => item.getAttribute("data-timeline-command-submit-command-ready")),
      commandBridgeSubmitButtonCount: panel?.querySelectorAll("[data-timeline-command-submit]").length ?? 0,
      commandBridgeSubmitButtonEnabledStates: Array.from(panel?.querySelectorAll("[data-timeline-command-submit]") ?? [])
        .map((item) => item.getAttribute("data-timeline-command-submit-enabled")),
      commandBridgeSubmitFieldStates: Array.from(panel?.querySelectorAll("[data-timeline-command-submit-field-state]") ?? [])
        .map((item) => item.getAttribute("data-timeline-command-submit-field-state")),
      commandBridgeSubmitStates: Array.from(panel?.querySelectorAll("[data-timeline-command-submit-state]") ?? [])
        .map((item) => item.getAttribute("data-timeline-command-submit-state")),
      commandBridgeSubmitText: Array.from(panel?.querySelectorAll(".wire-timeline-command-submit-plan") ?? [])
        .map((item) => item.textContent ?? "").join(" / "),
      commandBridgeSubmitTypes: Array.from(panel?.querySelectorAll("[data-timeline-command-submit-type]") ?? [])
        .map((item) => item.getAttribute("data-timeline-command-submit-type") ?? ""),
      commandBridgeNextButtonCount: panel?.querySelectorAll("[data-timeline-command-bridge-next-object-id]").length ?? 0,
      commandBridgeRouteStates: Array.from(panel?.querySelectorAll(".wire-timeline-command-bridge li") ?? [])
        .map((item) => item.getAttribute("data-timeline-command-bridge-route-state")),
      commandBridgeText: panel?.querySelector(".wire-timeline-command-bridge")?.textContent ?? "",
      evidenceKeys: Array.from(panel?.querySelectorAll("[data-timeline-evidence]") ?? [])
        .map((item) => item.getAttribute("data-timeline-evidence") ?? ""),
      evidenceStates: Array.from(panel?.querySelectorAll("[data-timeline-evidence-state]") ?? [])
        .map((item) => item.getAttribute("data-timeline-evidence-state") ?? ""),
      evidenceText: panel?.querySelector(".wire-timeline-evidence-list")?.textContent ?? "",
      nextStepButtonCount: panel?.querySelectorAll("[data-timeline-next-step-object-id]").length ?? 0,
      nextStepCheckKeys: Array.from(panel?.querySelectorAll("[data-timeline-next-step-check]") ?? [])
        .map((item) => item.getAttribute("data-timeline-next-step-check") ?? ""),
      nextStepCheckStates: Array.from(panel?.querySelectorAll("[data-timeline-next-step-check-state]") ?? [])
        .map((item) => item.getAttribute("data-timeline-next-step-check-state") ?? ""),
      nextStepGrammarRoles: Array.from(panel?.querySelectorAll("[data-timeline-next-step-grammar-role]") ?? [])
        .map((item) => item.getAttribute("data-timeline-next-step-grammar-role") ?? ""),
      nextStepGrammarStates: Array.from(panel?.querySelectorAll("[data-timeline-next-step-grammar-state]") ?? [])
        .map((item) => item.getAttribute("data-timeline-next-step-grammar-state") ?? ""),
      nextStepObjectIds: Array.from(panel?.querySelectorAll("[data-timeline-next-step-object-id]") ?? [])
        .map((item) => item.getAttribute("data-timeline-next-step-object-id") ?? ""),
      nextStepState: panel?.querySelector("[data-timeline-next-step-state]")?.getAttribute("data-timeline-next-step-state") ?? null,
      nextStepText: panel?.querySelector(".wire-timeline-next-step")?.textContent ?? "",
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
    const routeSummary = panel?.querySelector(".wire-timeline-route-summary");
    const timelineFollowup = panel?.querySelector(".wire-timeline-command-followup");
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
      routeSummaryLabel: routeSummary?.getAttribute("aria-label") ?? "",
      routeSummaryState: routeSummary?.getAttribute("data-timeline-route-summary-state") ?? null,
      routeSummaryCountKeys: Array.from(routeSummary?.querySelectorAll("[data-timeline-route-count]") ?? [])
        .map((item) => item.getAttribute("data-timeline-route-count") ?? ""),
      routeSummaryCountStates: Array.from(routeSummary?.querySelectorAll("[data-timeline-route-count-state]") ?? [])
        .map((item) => item.getAttribute("data-timeline-route-count-state")),
      routeSummaryText: routeSummary?.textContent ?? "",
      timelineFollowupBridgeState: timelineFollowup?.querySelector(".wire-command-followup-bridge")?.getAttribute("data-command-followup-bridge-state") ?? "",
      timelineFollowupLayoutState: timelineFollowup?.querySelector("[data-command-followup-layout-state]")?.getAttribute("data-command-followup-layout-state") ?? "",
      timelineFollowupServerState: timelineFollowup?.getAttribute("data-command-followup-server-state") ?? "",
      timelineFollowupServerKindActions: Array.from(timelineFollowup?.querySelectorAll("[data-command-followup-server-event-kind-action]") ?? [])
        .map((item) => item.getAttribute("data-command-followup-server-event-kind-action") ?? ""),
      timelineFollowupServerKindOrders: Array.from(timelineFollowup?.querySelectorAll("[data-command-followup-server-event-order]") ?? [])
        .map((item) => item.getAttribute("data-command-followup-server-event-order") ?? ""),
      timelineFollowupServerKindSources: Array.from(timelineFollowup?.querySelectorAll("[data-command-followup-server-event-kind-source]") ?? [])
        .map((item) => item.getAttribute("data-command-followup-server-event-kind-source") ?? ""),
      timelineFollowupServerKindStates: Array.from(timelineFollowup?.querySelectorAll("[data-command-followup-server-event-kind-state]") ?? [])
        .map((item) => item.getAttribute("data-command-followup-server-event-kind-state") ?? ""),
      timelineFollowupSourceSurface: timelineFollowup?.querySelector("[data-command-followup-source-surface]")?.getAttribute("data-command-followup-source-surface") ?? "",
      timelineFollowupState: timelineFollowup?.getAttribute("data-command-followup-state") ?? "",
      timelineFollowupText: timelineFollowup?.textContent ?? "",
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
      commandBridgeDetailRoles: Array.from(panel?.querySelectorAll("[data-timeline-command-bridge-detail-role]") ?? [])
        .map((item) => item.getAttribute("data-timeline-command-bridge-detail-role") ?? ""),
      commandBridgeFieldStates: Array.from(panel?.querySelectorAll("[data-timeline-command-field-state]") ?? [])
        .map((item) => item.getAttribute("data-timeline-command-field-state")),
      commandBridgeFieldDetailCounts: Array.from(panel?.querySelectorAll("[data-timeline-command-field-detail-count]") ?? [])
        .map((item) => item.getAttribute("data-timeline-command-field-detail-count")),
      commandBridgeFieldSelectedCounts: Array.from(panel?.querySelectorAll("[data-timeline-command-field-selected-count]") ?? [])
        .map((item) => item.getAttribute("data-timeline-command-field-selected-count")),
      commandBridgeFieldCandidateChoiceCounts: Array.from(panel?.querySelectorAll("[data-timeline-command-field-candidate-choice-count]") ?? [])
        .map((item) => item.getAttribute("data-timeline-command-field-candidate-choice-count")),
      commandBridgeFieldDetailObjectIds: Array.from(panel?.querySelectorAll("[data-timeline-command-field-detail-object-id]") ?? [])
        .map((item) => item.getAttribute("data-timeline-command-field-detail-object-id") ?? ""),
      commandBridgeFieldSelectedObjectIds: Array.from(panel?.querySelectorAll("[data-timeline-command-field-selected-object-id]") ?? [])
        .map((item) => item.getAttribute("data-timeline-command-field-selected-object-id") ?? ""),
      commandBridgeFieldChooseObjectIds: Array.from(panel?.querySelectorAll("[data-timeline-command-field-choose-object-id]") ?? [])
        .map((item) => item.getAttribute("data-timeline-command-field-choose-object-id") ?? ""),
      commandBridgeFieldChooseEnabledStates: Array.from(panel?.querySelectorAll("[data-timeline-command-field-choose-object-id]") ?? [])
        .map((item) => item.getAttribute("data-timeline-command-field-choose-enabled") ?? ""),
      commandBridgeGrammarStates: Array.from(panel?.querySelectorAll("[data-timeline-command-grammar-state]") ?? [])
        .map((item) => item.getAttribute("data-timeline-command-grammar-state")),
      commandBridgeGrammarStepStates: Array.from(panel?.querySelectorAll("[data-timeline-command-grammar-step-state]") ?? [])
        .map((item) => item.getAttribute("data-timeline-command-grammar-step-state")),
      commandBridgeGateStates: Array.from(panel?.querySelectorAll("[data-timeline-command-gate-state]") ?? [])
        .map((item) => item.getAttribute("data-timeline-command-gate-state")),
      commandBridgeSubmitCanSubmit: Array.from(panel?.querySelectorAll("[data-timeline-command-submit-can-submit]") ?? [])
        .map((item) => item.getAttribute("data-timeline-command-submit-can-submit")),
      commandBridgeSubmitCommandReady: Array.from(panel?.querySelectorAll("[data-timeline-command-submit-command-ready]") ?? [])
        .map((item) => item.getAttribute("data-timeline-command-submit-command-ready")),
      commandBridgeSubmitButtonCount: panel?.querySelectorAll("[data-timeline-command-submit]").length ?? 0,
      commandBridgeSubmitButtonEnabledStates: Array.from(panel?.querySelectorAll("[data-timeline-command-submit]") ?? [])
        .map((item) => item.getAttribute("data-timeline-command-submit-enabled")),
      commandBridgeSubmitFieldStates: Array.from(panel?.querySelectorAll("[data-timeline-command-submit-field-state]") ?? [])
        .map((item) => item.getAttribute("data-timeline-command-submit-field-state")),
      commandBridgeSubmitStates: Array.from(panel?.querySelectorAll("[data-timeline-command-submit-state]") ?? [])
        .map((item) => item.getAttribute("data-timeline-command-submit-state")),
      commandBridgeSubmitText: Array.from(panel?.querySelectorAll(".wire-timeline-command-submit-plan") ?? [])
        .map((item) => item.textContent ?? "").join(" / "),
      commandBridgeSubmitTypes: Array.from(panel?.querySelectorAll("[data-timeline-command-submit-type]") ?? [])
        .map((item) => item.getAttribute("data-timeline-command-submit-type") ?? ""),
      commandBridgeNextButtonCount: panel?.querySelectorAll("[data-timeline-command-bridge-next-object-id]").length ?? 0,
      commandBridgeNextObjectIds: Array.from(panel?.querySelectorAll("[data-timeline-command-bridge-next-object-id]") ?? [])
        .map((item) => item.getAttribute("data-timeline-command-bridge-next-object-id") ?? ""),
      commandBridgeRouteStates: Array.from(panel?.querySelectorAll(".wire-timeline-command-bridge li") ?? [])
        .map((item) => item.getAttribute("data-timeline-command-bridge-route-state")),
      commandBridgeRowCount: panel?.querySelectorAll(".wire-timeline-command-bridge li").length ?? 0,
      commandBridgeServerRoles: Array.from(panel?.querySelectorAll("[data-timeline-command-bridge-server-role]") ?? [])
        .map((item) => item.getAttribute("data-timeline-command-bridge-server-role") ?? ""),
      commandBridgeText: panel?.querySelector(".wire-timeline-command-bridge")?.textContent ?? "",
      evidenceKeys: Array.from(panel?.querySelectorAll("[data-timeline-evidence]") ?? [])
        .map((item) => item.getAttribute("data-timeline-evidence") ?? ""),
      evidenceStates: Array.from(panel?.querySelectorAll("[data-timeline-evidence-state]") ?? [])
        .map((item) => item.getAttribute("data-timeline-evidence-state") ?? ""),
      evidenceText: panel?.querySelector(".wire-timeline-evidence-list")?.textContent ?? "",
      nextStepButtonCount: panel?.querySelectorAll("[data-timeline-next-step-object-id]").length ?? 0,
      nextStepCheckKeys: Array.from(panel?.querySelectorAll("[data-timeline-next-step-check]") ?? [])
        .map((item) => item.getAttribute("data-timeline-next-step-check") ?? ""),
      nextStepCheckStates: Array.from(panel?.querySelectorAll("[data-timeline-next-step-check-state]") ?? [])
        .map((item) => item.getAttribute("data-timeline-next-step-check-state") ?? ""),
      nextStepGrammarRoles: Array.from(panel?.querySelectorAll("[data-timeline-next-step-grammar-role]") ?? [])
        .map((item) => item.getAttribute("data-timeline-next-step-grammar-role") ?? ""),
      nextStepGrammarStates: Array.from(panel?.querySelectorAll("[data-timeline-next-step-grammar-state]") ?? [])
        .map((item) => item.getAttribute("data-timeline-next-step-grammar-state") ?? ""),
      nextStepObjectIds: Array.from(panel?.querySelectorAll("[data-timeline-next-step-object-id]") ?? [])
        .map((item) => item.getAttribute("data-timeline-next-step-object-id") ?? ""),
      nextStepState: panel?.querySelector("[data-timeline-next-step-state]")?.getAttribute("data-timeline-next-step-state") ?? null,
      nextStepText: panel?.querySelector(".wire-timeline-next-step")?.textContent ?? "",
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

  const serverKindJumpKind = await clickTimelineFollowupServerKind(cdp, "BATTLEFIELD_CONTROL_RESOLVED", 1);
  await delay(150);
  const serverKindJumpResult = await evaluateJson(cdp, `(() => {
    const panel = document.querySelector(".wire-timeline-detail");
    const selectedRow = document.querySelector(".log-row.is-detail-selected");
    return {
      detailId: panel?.getAttribute("data-wire-timeline-detail-id") ?? "",
      panelState: panel?.getAttribute("data-wire-timeline-detail-state") ?? "",
      selectedRowKind: selectedRow?.getAttribute("data-event-log-row-kind") ?? "",
      text: panel?.textContent ?? ""
    };
  })()`);

  const serverKindRestoreKind = await clickTimelineFollowupServerKind(cdp, "STACK_ITEM_ADDED", 0);
  await delay(150);
  const serverKindRestoreResult = await evaluateJson(cdp, `(() => {
    const panel = document.querySelector(".wire-timeline-detail");
    const selectedRow = document.querySelector(".log-row.is-detail-selected");
    return {
      detailId: panel?.getAttribute("data-wire-timeline-detail-id") ?? "",
      panelState: panel?.getAttribute("data-wire-timeline-detail-state") ?? "",
      selectedRowKind: selectedRow?.getAttribute("data-event-log-row-kind") ?? "",
      text: panel?.textContent ?? ""
    };
  })()`);

  const followupEventClickKind = await clickTimelineFollowupEvent(cdp, "BATTLEFIELD_CONTROL_RESOLVED", 1);
  await delay(150);
  const followupEventClickResult = await evaluateJson(cdp, `(() => {
    const panel = document.querySelector(".wire-timeline-detail");
    const selectedRow = document.querySelector(".log-row.is-detail-selected");
    return {
      detailId: panel?.getAttribute("data-wire-timeline-detail-id") ?? "",
      panelState: panel?.getAttribute("data-wire-timeline-detail-state") ?? "",
      selectedRowKind: selectedRow?.getAttribute("data-event-log-row-kind") ?? "",
      text: panel?.textContent ?? ""
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
  if (!initial.eventRefVisibilityStates.includes("visible")) failures.push("event object ref visible state missing");
  if (!initial.eventRefInspectableStates.includes("true")) failures.push("event object ref inspectable state missing");
  if (!initial.eventRefZoneLabels.some((label) => label.length > 0)) failures.push("event object ref zone label missing");
  if (initial.unitRefs < 1) failures.push("unit rule object ref missing");
  if (initial.hiddenRefs < 1) failures.push("hidden rule object ref missing");
  if (initial.responsibilitySubmitStates.length < 1) failures.push("rule responsibility submit state missing");
  if (!initial.responsibilitySubmitStates.includes("ready")) failures.push("rule responsibility ready submit state missing");
  if (!initial.responsibilitySubmitReadyStates.includes("true")) failures.push("rule responsibility ready data attr missing");
  if (!initial.responsibilitySubmitText.includes("候选")) failures.push("rule responsibility submit candidate text missing");
  if (!responsibilityLayerResult.open) failures.push("rule responsibility layer did not open");
  if (responsibilityLayerResult.state !== "open") failures.push(`rule responsibility layer state unexpected: ${responsibilityLayerResult.state}`);
  if (responsibilityLayerResult.role !== "dialog") failures.push("rule responsibility layer role missing");
  if (responsibilityLayerResult.modal !== "true") failures.push("rule responsibility layer aria-modal missing");
  if (!responsibilityLayerResult.activeText.includes("关闭检查层")) failures.push("rule responsibility layer close button did not receive focus");
  if (Number(responsibilityLayerResult.itemCount) < 1) failures.push("rule responsibility layer item count missing");
  if (Number(responsibilityLayerResult.readyCount) < 1) failures.push("rule responsibility layer ready count missing");
  if (!responsibilityLayerResult.submitStates.includes("ready")) failures.push("rule responsibility layer ready submit state missing");
  if (!responsibilityLayerResult.submitReadyStates.includes("true")) failures.push("rule responsibility layer ready submit attr missing");
  if (!responsibilityLayerResult.itemStates.some((state) => ["blocked", "respond", "watch"].includes(state))) {
    failures.push(`rule responsibility layer active item state missing: ${responsibilityLayerResult.itemStates.join(",")}`);
  }
  if (responsibilityLayerResult.sourceRefCount < 1) failures.push("rule responsibility layer source ref missing");
  if (responsibilityLayerResult.targetRefCount < 1) failures.push("rule responsibility layer target ref missing");
  if (responsibilityLayerResult.hiddenRefCount < 1) failures.push("rule responsibility layer hidden boundary ref missing");
  if (responsibilityLayerResult.hiddenBoundary !== "true") failures.push("rule responsibility layer hidden boundary attr missing");
  if (!responsibilityLayerResult.text.includes("响应责任和候选入口来自服务端")) failures.push("rule responsibility layer authority text missing");
  if (responsibilityLayerResult.text.includes("serverPaymentState")) failures.push("rule responsibility layer leaked hidden server state");
  if (!responsibilityLayerObjectResult.clicked) failures.push("rule responsibility layer object ref not clickable");
  if (responsibilityLayerObjectResult.selected !== "true") failures.push("rule responsibility layer object ref did not focus table object");
  if (!responsibilityLayerObjectResult.selectedRef) failures.push("rule responsibility layer object ref did not show selected state");
  if (responsibilityLayerObjectResult.detailLayerOpen) failures.push("rule responsibility layer object ref opened detail");
  if (!responsibilityLayerClosed) failures.push("rule responsibility layer did not close on Escape");
  if (!serverFlowLayerResult.open) failures.push("server flow layer did not open");
  if (serverFlowLayerResult.state !== "open") failures.push(`server flow layer state unexpected: ${serverFlowLayerResult.state}`);
  if (serverFlowLayerResult.role !== "dialog") failures.push("server flow layer role missing");
  if (serverFlowLayerResult.modal !== "true") failures.push("server flow layer aria-modal missing");
  if (!serverFlowLayerResult.activeText.includes("关闭检查层")) failures.push("server flow layer close button did not receive focus");
  if (!["blocked", "history", "ready", "respond", "selecting", "waiting"].includes(serverFlowLayerResult.flowState)) {
    failures.push(`server flow layer flow state unsupported: ${serverFlowLayerResult.flowState}`);
  }
  if (Number(serverFlowLayerResult.stepCount) < 1) failures.push("server flow layer step count missing");
  if (Number(serverFlowLayerResult.laneCount) < 1) failures.push("server flow layer lane count missing");
  if (Number(serverFlowLayerResult.relatedCount) < 1) failures.push("server flow layer related count missing");
  if (!serverFlowLayerResult.stepDetailStates.includes("available")) failures.push("server flow layer step detail state missing");
  if (!serverFlowLayerResult.stepRoles.includes("candidate")) failures.push(`server flow layer candidate step role missing: ${serverFlowLayerResult.stepRoles.join(",")}`);
  if (!serverFlowLayerResult.stepStates.some((state) => ["blocked", "ready", "respond", "selecting", "server", "watch"].includes(state))) {
    failures.push(`server flow layer active step state missing: ${serverFlowLayerResult.stepStates.join(",")}`);
  }
  if (serverFlowLayerResult.sourceRefCount < 1) failures.push("server flow layer source ref missing");
  if (serverFlowLayerResult.targetRefCount < 1) failures.push("server flow layer target ref missing");
  if (!serverFlowLayerResult.actionObjectIds.includes("p2-right-1")) failures.push("server flow layer action object missing");
  if (!serverFlowLayerResult.actionCandidates.some((candidate) => candidate.includes("PLAY_CARD"))) {
    failures.push(`server flow layer action candidates missing command name: ${serverFlowLayerResult.actionCandidates.join(",")}`);
  }
  if (!serverFlowLayerResult.actionStates.includes("ready")) failures.push("server flow layer ready action state missing");
  if (!serverFlowLayerResult.actionInspectableStates.includes("true")) failures.push("server flow layer inspectable action missing");
  if (serverFlowLayerResult.authority !== "server") failures.push("server flow layer server authority attr missing");
  if (!serverFlowLayerResult.text.includes("流程、责任、候选和对象关联来自服务端")) failures.push("server flow layer authority text missing");
  if (serverFlowLayerResult.text.includes("serverPaymentState")) failures.push("server flow layer leaked hidden server state");
  if (!serverFlowLayerObjectResult.clicked) failures.push("server flow layer object ref not clickable");
  if (serverFlowLayerObjectResult.selected !== "true") failures.push("server flow layer object ref did not focus table object");
  if (!serverFlowLayerObjectResult.selectedRef) failures.push("server flow layer object ref did not show selected state");
  if (serverFlowLayerObjectResult.detailLayerOpen) failures.push("server flow layer object ref opened detail");
  if (!serverFlowLayerClosed) failures.push("server flow layer did not close on Escape");
  if (battlefieldResult.selected !== "true") failures.push("battlefield ref did not focus battlefield card");
  if (!battlefieldResult.selectedRef) failures.push("battlefield ref did not show selected state");
  if (!battlefieldResult.hasSelectedObjectContext) failures.push("battlefield ref did not expose selected object context");
  if (battlefieldResult.contextAuthority !== "server") failures.push(`battlefield ref context authority unexpected: ${battlefieldResult.contextAuthority}`);
  if (battlefieldResult.contextSource !== "服务端关联对象") failures.push(`battlefield ref context source unexpected: ${battlefieldResult.contextSource}`);
  if (!battlefieldResult.contextText.includes("服务端关联对象")) failures.push("battlefield ref context server relation section missing");
  if (!battlefieldResult.contextText.includes("相关战场")) failures.push("battlefield ref context relation role missing");
  if (battlefieldResult.selectedLayoutState !== "located") failures.push(`battlefield ref selected layout state unexpected: ${battlefieldResult.selectedLayoutState}`);
  if (battlefieldResult.selectedLayoutKind !== "site") failures.push(`battlefield ref selected layout kind unexpected: ${battlefieldResult.selectedLayoutKind}`);
  if (battlefieldResult.selectedLayoutZone !== "battlefield:0:site") failures.push(`battlefield ref selected layout zone unexpected: ${battlefieldResult.selectedLayoutZone}`);
  if (battlefieldResult.detailLayerOpen) failures.push("battlefield ref opened detail layer");
  if (unitResult.selected !== "true") failures.push("unit ref did not focus unit card");
  if (!unitResult.selectedRef) failures.push("unit ref did not show selected state");
  if (unitResult.selectedLayoutState !== "located") failures.push(`unit ref selected layout state unexpected: ${unitResult.selectedLayoutState}`);
  if (unitResult.selectedLayoutKind !== "battlefield-unit") failures.push(`unit ref selected layout kind unexpected: ${unitResult.selectedLayoutKind}`);
  if (unitResult.selectedLayoutCapacityRow !== "battlefield:1:opponent") failures.push(`unit ref selected layout capacity row unexpected: ${unitResult.selectedLayoutCapacityRow}`);
  if (unitResult.selectedLayoutZone !== "battlefield:1:opponent") failures.push(`unit ref selected layout zone unexpected: ${unitResult.selectedLayoutZone}`);
  if (unitResult.detailLayerOpen) failures.push("unit ref opened detail layer");
  if (eventResult.selected !== "true") failures.push("event ref did not focus source card");
  if (!eventResult.selectedRef) failures.push("event ref did not show selected state");
  if (eventResult.selectedRefVisibility !== "visible") failures.push(`event ref visibility unexpected: ${eventResult.selectedRefVisibility}`);
  if (eventResult.selectedRefInspectable !== "true") failures.push(`event ref inspectable state unexpected: ${eventResult.selectedRefInspectable}`);
  if (!eventResult.selectedRefZoneLabel) failures.push("selected event object ref zone label missing");
  if (eventResult.detailLayerOpen) failures.push("event ref opened detail layer");
  if (!ruleDetailResult.text.includes("结算链项目")) failures.push("rule detail title missing");
  if (ruleDetailResult.panelState !== "rule") failures.push(`rule detail panel state unexpected: ${ruleDetailResult.panelState}`);
  if (ruleDetailResult.bodyId !== "wire-timeline-detail-body") failures.push("rule detail body id missing");
  if (ruleDetailResult.openLayerTriggerCount !== 1) failures.push(`rule detail open layer trigger count unexpected: ${ruleDetailResult.openLayerTriggerCount}`);
  if (ruleDetailResult.openLayerTriggerControls !== "wire-timeline-detail-layer") failures.push("rule detail open layer trigger aria-controls missing");
  if (!ruleDetailResult.openLayerTriggerText.includes("打开检查层")) failures.push("rule detail open layer trigger text missing");
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
  if (ruleDetailResult.routeSummaryLabel !== "候选提交路线摘要") failures.push("rule detail route summary label missing");
  if (!["ready", "selecting", "inactive"].includes(ruleDetailResult.routeSummaryState)) failures.push(`rule detail route summary state unexpected: ${ruleDetailResult.routeSummaryState}`);
  if (!ruleDetailResult.routeSummaryCountKeys.includes("ready")) failures.push("rule detail route summary ready count missing");
  if (!ruleDetailResult.routeSummaryCountKeys.includes("draft")) failures.push("rule detail route summary draft count missing");
  if (!ruleDetailResult.routeSummaryText.includes("路径")) failures.push("rule detail route summary total path text missing");
  if (!ruleDetailResult.routeSummaryText.includes("PLAY_CARD")) failures.push("rule detail route summary command type missing");
  if (!validCommandFollowupStates.includes(ruleDetailResult.timelineFollowupState)) failures.push(`rule detail timeline followup state unexpected: ${ruleDetailResult.timelineFollowupState}`);
  if (!["empty", "failed", "ready", "unknown", "waiting"].includes(ruleDetailResult.timelineFollowupBridgeState)) failures.push(`rule detail timeline followup bridge state unexpected: ${ruleDetailResult.timelineFollowupBridgeState}`);
  if (!["empty", "hidden-only", "linked", "unknown"].includes(ruleDetailResult.timelineFollowupLayoutState)) failures.push(`rule detail timeline followup layout state unexpected: ${ruleDetailResult.timelineFollowupLayoutState}`);
  if (!ruleDetailResult.timelineFollowupText.includes("后续事件")) failures.push("rule detail timeline followup title missing");
  if (!ruleDetailResult.timelineFollowupText.includes("服务端")) failures.push("rule detail timeline followup server wording missing");
  if (ruleDetailResult.timelineFollowupSourceSurface !== "timeline-detail") failures.push(`rule detail followup source surface unexpected: ${ruleDetailResult.timelineFollowupSourceSurface}`);
  if (!ruleDetailResult.timelineFollowupServerKindActions.includes("STACK_ITEM_ADDED")) failures.push("rule detail followup stack event kind action missing");
  if (!ruleDetailResult.timelineFollowupServerKindActions.includes("BATTLEFIELD_CONTROL_RESOLVED")) failures.push("rule detail followup battlefield event kind action missing");
  if (!ruleDetailResult.timelineFollowupServerKindSources.includes("event-ref")) failures.push("rule detail followup event-ref source missing");
  if (!ruleDetailResult.timelineFollowupServerKindOrders.includes("0")) failures.push("rule detail followup stack event order missing");
  if (!ruleDetailResult.timelineFollowupServerKindOrders.includes("1")) failures.push("rule detail followup battlefield event order missing");
  if (!ruleDetailResult.timelineFollowupServerKindStates.includes("linked")) failures.push("rule detail followup linked event kind state missing");
  if (!ruleDetailResult.text.includes("来源")) failures.push("rule detail source line missing");
  if (!ruleDetailResult.hasSourceRef) failures.push("rule detail source ref missing");
  if (!ruleDetailResult.hasTargetRef) failures.push("rule detail target ref missing");
  if (!ruleDetailResult.statusText.includes("桌面投影")) failures.push("rule detail projection status missing");
  if (!ruleDetailResult.statusText.includes("当前焦点")) failures.push("rule detail focus status missing");
  if (!ruleDetailResult.statusText.includes("关联候选")) failures.push("rule detail candidate status missing");
  if (!ruleDetailResult.evidenceKeys.includes("source")) failures.push("rule detail evidence source row missing");
  if (!ruleDetailResult.evidenceKeys.includes("projection")) failures.push("rule detail evidence projection row missing");
  if (!ruleDetailResult.evidenceKeys.includes("candidate")) failures.push("rule detail evidence candidate row missing");
  if (!ruleDetailResult.evidenceKeys.includes("path")) failures.push("rule detail evidence path row missing");
  if (!ruleDetailResult.evidenceKeys.includes("boundary")) failures.push("rule detail evidence boundary row missing");
  if (!ruleDetailResult.evidenceStates.includes("ready")) failures.push("rule detail evidence ready state missing");
  if (!ruleDetailResult.evidenceText.includes("服务端规则")) failures.push("rule detail evidence source authority missing");
  if (!ruleDetailResult.evidenceText.includes("路径")) failures.push("rule detail evidence path label missing");
  if (ruleDetailResult.nextStepState !== "ready") failures.push(`rule detail next step state unexpected: ${ruleDetailResult.nextStepState}`);
  if (!ruleDetailResult.nextStepText.includes("可送服务端校验")) failures.push("rule detail next step ready headline missing");
  if (!ruleDetailResult.nextStepText.includes("PLAY_CARD")) failures.push("rule detail next step command type missing");
  if (ruleDetailResult.nextStepButtonCount < 1) failures.push("rule detail next step object buttons missing");
  if (!ruleDetailResult.nextStepObjectIds.includes("p2-right-1")) failures.push("rule detail next step target object missing");
  if (!ruleDetailResult.nextStepCheckKeys.includes("server-candidate")) failures.push("rule detail next step server candidate check missing");
  if (!ruleDetailResult.nextStepCheckKeys.includes("connection")) failures.push("rule detail next step connection check missing");
  if (!ruleDetailResult.nextStepCheckKeys.includes("required-fields")) failures.push("rule detail next step required fields check missing");
  if (!ruleDetailResult.nextStepCheckKeys.includes("submit-step")) failures.push("rule detail next step submit check missing");
  if (!ruleDetailResult.nextStepCheckStates.includes("ready")) failures.push("rule detail next step ready check state missing");
  if (!ruleDetailResult.nextStepGrammarRoles.includes("source")) failures.push("rule detail next step source grammar missing");
  if (!ruleDetailResult.nextStepGrammarRoles.includes("target")) failures.push("rule detail next step target grammar missing");
  if (!ruleDetailResult.nextStepGrammarRoles.includes("submit")) failures.push("rule detail next step submit grammar missing");
  if (!ruleDetailResult.nextStepGrammarStates.includes("ready")) failures.push("rule detail next step ready grammar state missing");
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
  if (!ruleDetailResult.commandBridgeDetailRoles.includes("来源")) failures.push("rule detail command bridge detail role missing");
  if (!ruleDetailResult.commandBridgeServerRoles.includes("来源")) failures.push("rule detail command bridge server role missing");
  if (!ruleDetailResult.commandBridgeText.includes("详情来源 / 候选来源")) failures.push("rule detail command bridge detail link missing");
  if (!ruleDetailResult.commandBridgeText.includes("已选 来源")) failures.push("rule detail command bridge source draft label missing");
  if (!ruleDetailResult.commandBridgeFieldStates.includes("covered")) failures.push("rule detail command bridge covered field state missing");
  if (!ruleDetailResult.commandBridgeFieldStates.includes("server")) failures.push("rule detail command bridge server field state missing");
  if (!ruleDetailResult.commandBridgeFieldDetailCounts.includes("1")) failures.push("rule detail command bridge detail field coverage missing");
  if (!ruleDetailResult.commandBridgeFieldSelectedCounts.includes("1")) failures.push("rule detail command bridge selected field coverage missing");
  if (!ruleDetailResult.commandBridgeFieldCandidateChoiceCounts.includes("1")) failures.push("rule detail command bridge candidate choice count missing");
  if (!ruleDetailResult.commandBridgeFieldDetailObjectIds.includes("p1-hand-spell")) failures.push("rule detail command bridge source field detail object missing");
  if (!ruleDetailResult.commandBridgeFieldDetailObjectIds.includes("p2-right-1")) failures.push("rule detail command bridge target field detail object missing");
  if (!ruleDetailResult.commandBridgeFieldSelectedObjectIds.includes("p1-hand-spell")) failures.push("rule detail command bridge source field selected object missing");
  if (!ruleDetailResult.commandBridgeFieldChooseObjectIds.includes("p2-right-1")) failures.push("rule detail command bridge target field choose object missing");
  if (!ruleDetailResult.commandBridgeFieldChooseEnabledStates.includes("true")) failures.push("rule detail command bridge field choose enabled state missing");
  if (!ruleDetailResult.commandBridgeFieldChooseEnabledStates.includes("false")) failures.push("rule detail command bridge selected field choose disabled state missing");
  if (!ruleDetailResult.commandBridgeText.includes("服务端注入")) failures.push("rule detail command bridge server field label missing");
  if (!ruleDetailResult.commandBridgeText.includes("草稿已选来源")) failures.push("rule detail command bridge source coverage label missing");
  if (!ruleDetailResult.commandBridgeText.includes("详情引用可作为目标")) failures.push("rule detail command bridge target coverage label missing");
  if (!ruleDetailResult.commandBridgeText.includes("详情 ") || !ruleDetailResult.commandBridgeText.includes("草稿 ")) failures.push("rule detail command bridge field coverage summary missing");
  if (!ruleDetailResult.commandBridgeGrammarStates.includes("ready")) failures.push("rule detail command bridge grammar ready state missing");
  if (!ruleDetailResult.commandBridgeGrammarStepStates.includes("locked")) failures.push("rule detail command bridge grammar source lock missing");
  if (!ruleDetailResult.commandBridgeGrammarStepStates.includes("ready")) failures.push("rule detail command bridge grammar submit ready missing");
  if (!ruleDetailResult.commandBridgeGateStates.includes("ready")) failures.push("rule detail command bridge gate ready state missing");
  if (!ruleDetailResult.commandBridgeText.includes("提交门禁")) failures.push("rule detail command bridge gate label missing");
  if (!ruleDetailResult.commandBridgeSubmitStates.includes("ready")) failures.push("rule detail command submit plan ready state missing");
  if (!ruleDetailResult.commandBridgeSubmitCanSubmit.includes("true")) failures.push("rule detail command submit plan can-submit missing");
  if (!ruleDetailResult.commandBridgeSubmitCommandReady.includes("true")) failures.push("rule detail command submit command-ready missing");
  if (ruleDetailResult.commandBridgeSubmitButtonCount < 1) failures.push("rule detail command submit button missing");
  if (!ruleDetailResult.commandBridgeSubmitButtonEnabledStates.includes("true")) failures.push("rule detail command submit button enabled state missing");
  if (!ruleDetailResult.commandBridgeSubmitTypes.includes("PLAY_CARD")) failures.push("rule detail command submit plan type missing");
  if (!ruleDetailResult.commandBridgeSubmitFieldStates.includes("covered")) failures.push("rule detail command submit plan covered field missing");
  if (!ruleDetailResult.commandBridgeSubmitFieldStates.includes("server")) failures.push("rule detail command submit plan server field missing");
  if (!ruleDetailResult.commandBridgeSubmitText.includes("命令预览")) failures.push("rule detail command submit plan label missing");
  if (!ruleDetailResult.commandBridgeSubmitText.includes("服务端规则校验")) failures.push("rule detail command submit plan authority note missing");
  if (ruleDetailResult.commandBridgeOpenDetailButtonCount < 1) failures.push("rule detail command bridge detail buttons missing");
  if (!ruleDetailResult.commandBridgeOpenDetailObjectIds.includes("p1-hand-spell")) failures.push("rule detail command bridge source detail button missing");
  if (commandBridgeDetailResult.objectId !== "p1-hand-spell") failures.push(`command bridge detail opened unexpected object: ${commandBridgeDetailResult.objectId}`);
  if (!commandBridgeDetailResult.open) failures.push("command bridge detail button did not open card detail layer");
  if (!commandBridgeDetailResult.activeText.includes("关闭")) failures.push("command bridge detail drawer did not focus close button");
  if (!commandBridgeDetailResult.actionText.includes("服务端可提交操作")) failures.push("command bridge detail drawer action section missing");
  if (commandBridgeDetailResult.actionRouteCount < 1) failures.push("command bridge detail drawer route rows missing");
  if (!commandBridgeDetailResult.actionRouteStates.includes("composer")) failures.push("command bridge detail drawer route composer state missing");
  if (!commandBridgeDetailResult.actionRouteText.includes("候选入口路线")) failures.push("command bridge detail drawer route section title missing");
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
  if (!timelineLayerResult.open) failures.push("timeline detail layer did not open");
  if (timelineLayerResult.role !== "dialog") failures.push(`timeline detail layer role unexpected: ${timelineLayerResult.role}`);
  if (timelineLayerResult.modal !== "true") failures.push("timeline detail layer aria-modal missing");
  if (timelineLayerResult.state !== "open") failures.push(`timeline detail layer state unexpected: ${timelineLayerResult.state}`);
  if (timelineLayerResult.source !== "rule") failures.push(`timeline detail layer source unexpected: ${timelineLayerResult.source}`);
  if (timelineLayerResult.detailId !== "rule:stack:fixture-stack-1") failures.push(`timeline detail layer detail id missing: ${timelineLayerResult.detailId}`);
  if (timelineLayerResult.panelDetailId !== "rule:stack:fixture-stack-1") failures.push("timeline detail layer panel did not reuse selected detail");
  if (timelineLayerResult.panelState !== "rule") failures.push(`timeline detail layer panel state unexpected: ${timelineLayerResult.panelState}`);
  if (timelineLayerResult.bodyId !== "wire-timeline-detail-layer-body") failures.push("timeline detail layer body id missing");
  if (!timelineLayerResult.title.includes("结算链项目")) failures.push("timeline detail layer title missing");
  if (!timelineLayerResult.text.includes("规则事件检查层")) failures.push("timeline detail layer heading missing");
  if (timelineLayerResult.routeSummaryLabel !== "候选提交路线摘要") failures.push("timeline detail layer route summary label missing");
  if (!["ready", "selecting", "inactive"].includes(timelineLayerResult.routeSummaryState)) failures.push(`timeline detail layer route summary state unexpected: ${timelineLayerResult.routeSummaryState}`);
  if (!validCommandFollowupStates.includes(timelineLayerResult.timelineFollowupState)) failures.push(`timeline detail layer followup state unexpected: ${timelineLayerResult.timelineFollowupState}`);
  if (!["empty", "failed", "ready", "unknown", "waiting"].includes(timelineLayerResult.timelineFollowupBridgeState)) failures.push(`timeline detail layer followup bridge state unexpected: ${timelineLayerResult.timelineFollowupBridgeState}`);
  if (!timelineLayerResult.timelineFollowupText.includes("后续事件")) failures.push("timeline detail layer followup title missing");
  if (!timelineLayerResult.text.includes("服务端规则")) failures.push("timeline detail layer server rule source missing");
  if (!timelineLayerResult.activeText.includes("关闭检查层")) failures.push("timeline detail layer close button was not focused");
  if (timelineLayerResult.cardDetailOpen) failures.push("timeline detail layer opened card detail layer");
  if (!timelineLayerClosed) failures.push("timeline detail layer did not close on Escape");
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
  if (commandBridgeFocusResult.routeSummaryState !== "ready") failures.push(`command bridge route summary state unexpected after target selection: ${commandBridgeFocusResult.routeSummaryState}`);
  if (!commandBridgeFocusResult.routeSummaryCountStates.includes("ready")) failures.push("command bridge route summary ready count state missing after target selection");
  if (!commandBridgeFocusResult.routeSummaryText.includes("存在可提交路线")) failures.push("command bridge route summary ready headline missing after target selection");
  if (!commandBridgeFocusResult.routeSummaryText.includes("PLAY_CARD")) failures.push("command bridge route summary command text missing after target selection");
  if (!commandBridgeFocusResult.draftText.includes("目标 1")) failures.push("command bridge draft target count missing");
  if (!commandBridgeFocusResult.routeText.includes("PLAY_CARD")) failures.push("command bridge route command type missing");
  if (!commandBridgeFocusResult.commandBridgeDraftActiveStates.includes("true")) failures.push("command bridge detail draft state missing after target selection");
  if (!commandBridgeFocusResult.commandBridgeRouteStates.includes("ready")) failures.push("command bridge detail route state missing after target selection");
  if (commandBridgeFocusResult.commandBridgeNextButtonCount < 1) failures.push("command bridge detail should still offer optional next choices after target selection");
  if (!commandBridgeFocusResult.commandBridgeText.includes("已选 来源 / 目标")) failures.push("command bridge detail selected roles missing after target selection");
  if (!commandBridgeFocusResult.commandBridgeText.includes("可选位置")) failures.push("command bridge detail optional next step missing after target selection");
  if (!commandBridgeFocusResult.commandBridgeFieldStates.includes("covered")) failures.push("command bridge detail covered field state missing after target selection");
  if (!commandBridgeFocusResult.commandBridgeFieldStates.includes("server")) failures.push("command bridge detail server field state missing after target selection");
  if (!commandBridgeFocusResult.commandBridgeFieldDetailCounts.includes("1")) failures.push("command bridge detail field detail coverage missing after target selection");
  if (!commandBridgeFocusResult.commandBridgeFieldSelectedCounts.includes("1")) failures.push("command bridge detail selected field coverage missing after target selection");
  if (!commandBridgeFocusResult.commandBridgeFieldCandidateChoiceCounts.includes("1")) failures.push("command bridge detail candidate choice count missing after target selection");
  if (!commandBridgeFocusResult.commandBridgeFieldDetailObjectIds.includes("p1-hand-spell")) failures.push("command bridge detail source field detail object missing after target selection");
  if (!commandBridgeFocusResult.commandBridgeFieldDetailObjectIds.includes("p2-right-1")) failures.push("command bridge detail target field detail object missing after target selection");
  if (!commandBridgeFocusResult.commandBridgeFieldSelectedObjectIds.includes("p1-hand-spell")) failures.push("command bridge detail source field selected object missing after target selection");
  if (!commandBridgeFocusResult.commandBridgeFieldSelectedObjectIds.includes("p2-right-1")) failures.push("command bridge detail target field selected object missing after target selection");
  if (!commandBridgeFocusResult.commandBridgeFieldChooseObjectIds.includes("p2-right-1")) failures.push("command bridge detail target field choose object missing after target selection");
  if (!commandBridgeFocusResult.commandBridgeFieldChooseEnabledStates.includes("false")) failures.push("command bridge detail chosen field disabled state missing after target selection");
  if (!commandBridgeFocusResult.commandBridgeText.includes("2 覆盖 / 0 缺少")) failures.push("command bridge detail coverage summary missing after target selection");
  if (!commandBridgeFocusResult.commandBridgeText.includes("草稿已选目标")) failures.push("command bridge detail target coverage label missing after target selection");
  if (!commandBridgeFocusResult.commandBridgeText.includes("详情 ") || !commandBridgeFocusResult.commandBridgeText.includes("草稿 ")) failures.push("command bridge detail field coverage summary missing after target selection");
  if (!commandBridgeFocusResult.commandBridgeGrammarStates.includes("ready")) failures.push("command bridge detail grammar ready state missing after target selection");
  if (!commandBridgeFocusResult.commandBridgeGrammarStepStates.includes("selected")) failures.push("command bridge detail grammar selected target missing after target selection");
  if (!commandBridgeFocusResult.commandBridgeGrammarStepStates.includes("ready")) failures.push("command bridge detail grammar submit ready missing after target selection");
  if (!commandBridgeFocusResult.commandBridgeGateStates.includes("ready")) failures.push("command bridge detail gate ready state missing after target selection");
  if (!commandBridgeFocusResult.commandBridgeText.includes("提交门禁")) failures.push("command bridge detail gate label missing after target selection");
  if (!commandBridgeFocusResult.commandBridgeSubmitStates.includes("ready")) failures.push("command bridge detail submit plan ready missing after target selection");
  if (!commandBridgeFocusResult.commandBridgeSubmitCanSubmit.includes("true")) failures.push("command bridge detail submit plan can-submit missing after target selection");
  if (!commandBridgeFocusResult.commandBridgeSubmitCommandReady.includes("true")) failures.push("command bridge detail submit command-ready missing after target selection");
  if (!commandBridgeFocusResult.commandBridgeSubmitButtonEnabledStates.includes("true")) failures.push("command bridge detail submit button enabled state missing after target selection");
  if (!commandBridgeFocusResult.commandBridgeSubmitText.includes("提交 PLAY_CARD")) failures.push("command bridge detail submit plan label missing after target selection");
  if (!commandBridgeFocusResult.evidenceKeys.includes("path")) failures.push("command bridge detail evidence path missing after target selection");
  if (!commandBridgeFocusResult.evidenceStates.includes("ready")) failures.push("command bridge detail evidence ready state missing after target selection");
  if (!commandBridgeFocusResult.evidenceText.includes("可送")) failures.push("command bridge detail evidence route summary missing after target selection");
  if (commandBridgeFocusResult.nextStepState !== "ready") failures.push(`command bridge detail next step state unexpected after target selection: ${commandBridgeFocusResult.nextStepState}`);
  if (!commandBridgeFocusResult.nextStepText.includes("可送服务端校验")) failures.push("command bridge detail next step headline missing after target selection");
  if (!commandBridgeFocusResult.nextStepCheckKeys.includes("submit-step")) failures.push("command bridge detail next step submit check missing after target selection");
  if (!commandBridgeFocusResult.nextStepCheckStates.includes("ready")) failures.push("command bridge detail next step ready checks missing after target selection");
  if (!commandBridgeFocusResult.nextStepGrammarRoles.includes("target")) failures.push("command bridge detail next step target grammar missing after target selection");
  if (!commandBridgeFocusResult.nextStepGrammarStates.includes("selected")) failures.push("command bridge detail next step selected grammar missing after target selection");
  if (!commandBridgeFocusResult.nextStepGrammarStates.includes("ready")) failures.push("command bridge detail next step ready grammar missing after target selection");
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
  if (eventDetailResult.routeSummaryLabel !== "候选提交路线摘要") failures.push("event detail route summary label missing");
  if (!["ready", "selecting", "inactive"].includes(eventDetailResult.routeSummaryState)) failures.push(`event detail route summary state unexpected: ${eventDetailResult.routeSummaryState}`);
  if (!eventDetailResult.routeSummaryCountKeys.includes("ready")) failures.push("event detail route summary ready count missing");
  if (!eventDetailResult.routeSummaryCountKeys.includes("draft")) failures.push("event detail route summary draft count missing");
  if (!eventDetailResult.routeSummaryText.includes("路径")) failures.push("event detail route summary total path text missing");
  if (!eventDetailResult.routeSummaryText.includes("PLAY_CARD")) failures.push("event detail route summary command type missing");
  if (!validCommandFollowupStates.includes(eventDetailResult.timelineFollowupState)) failures.push(`event detail timeline followup state unexpected: ${eventDetailResult.timelineFollowupState}`);
  if (!["empty", "failed", "ready", "unknown", "waiting"].includes(eventDetailResult.timelineFollowupBridgeState)) failures.push(`event detail timeline followup bridge state unexpected: ${eventDetailResult.timelineFollowupBridgeState}`);
  if (!["empty", "hidden-only", "linked", "unknown"].includes(eventDetailResult.timelineFollowupLayoutState)) failures.push(`event detail timeline followup layout state unexpected: ${eventDetailResult.timelineFollowupLayoutState}`);
  if (!eventDetailResult.timelineFollowupText.includes("后续事件")) failures.push("event detail timeline followup title missing");
  if (!eventDetailResult.timelineFollowupText.includes("服务端")) failures.push("event detail timeline followup server wording missing");
  if (eventDetailResult.timelineFollowupSourceSurface !== "timeline-detail") failures.push(`event detail followup source surface unexpected: ${eventDetailResult.timelineFollowupSourceSurface}`);
  if (!eventDetailResult.timelineFollowupServerKindActions.includes("STACK_ITEM_ADDED")) failures.push("event detail followup stack event kind action missing");
  if (!eventDetailResult.timelineFollowupServerKindActions.includes("BATTLEFIELD_CONTROL_RESOLVED")) failures.push("event detail followup battlefield event kind action missing");
  if (!eventDetailResult.timelineFollowupServerKindSources.includes("event-ref")) failures.push("event detail followup event-ref source missing");
  if (!eventDetailResult.evidenceKeys.includes("source")) failures.push("event detail evidence source row missing");
  if (!eventDetailResult.evidenceKeys.includes("path")) failures.push("event detail evidence path row missing");
  if (!eventDetailResult.evidenceText.includes("服务端日志")) failures.push("event detail evidence source authority missing");
  if (eventDetailResult.nextStepState !== "ready") failures.push(`event detail next step state unexpected: ${eventDetailResult.nextStepState}`);
  if (!eventDetailResult.nextStepText.includes("可送服务端校验")) failures.push("event detail next step ready headline missing");
  if (!eventDetailResult.nextStepCheckKeys.includes("server-candidate")) failures.push("event detail next step server candidate check missing");
  if (!eventDetailResult.nextStepCheckKeys.includes("submit-step")) failures.push("event detail next step submit check missing");
  if (!eventDetailResult.nextStepGrammarRoles.includes("source")) failures.push("event detail next step source grammar missing");
  if (!eventDetailResult.nextStepGrammarRoles.includes("submit")) failures.push("event detail next step submit grammar missing");
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
  if (!eventDetailResult.commandBridgeDetailRoles.includes("来源")) failures.push("event detail command bridge detail role missing");
  if (!eventDetailResult.commandBridgeServerRoles.includes("来源")) failures.push("event detail command bridge server role missing");
  if (!eventDetailResult.commandBridgeText.includes("详情来源 / 候选来源")) failures.push("event detail command bridge detail link missing");
  if (!eventDetailResult.commandBridgeFieldStates.includes("covered")) failures.push("event detail command bridge covered field state missing");
  if (!eventDetailResult.commandBridgeFieldStates.includes("server")) failures.push("event detail command bridge server field state missing");
  if (!eventDetailResult.commandBridgeFieldDetailCounts.includes("1")) failures.push("event detail command bridge detail field coverage missing");
  if (!eventDetailResult.commandBridgeFieldSelectedCounts.includes("1")) failures.push("event detail command bridge selected field coverage missing");
  if (!eventDetailResult.commandBridgeFieldCandidateChoiceCounts.includes("1")) failures.push("event detail command bridge candidate choice count missing");
  if (!eventDetailResult.commandBridgeFieldDetailObjectIds.includes("p1-hand-spell")) failures.push("event detail command bridge source field detail object missing");
  if (!eventDetailResult.commandBridgeFieldDetailObjectIds.includes("p2-right-1")) failures.push("event detail command bridge target field detail object missing");
  if (!eventDetailResult.commandBridgeFieldSelectedObjectIds.includes("p1-hand-spell")) failures.push("event detail command bridge source field selected object missing");
  if (!eventDetailResult.commandBridgeFieldChooseObjectIds.includes("p2-right-1")) failures.push("event detail command bridge target field choose object missing");
  if (!eventDetailResult.commandBridgeFieldChooseEnabledStates.includes("true")) failures.push("event detail command bridge field choose enabled state missing");
  if (!eventDetailResult.commandBridgeText.includes("服务端注入")) failures.push("event detail command bridge server field label missing");
  if (!eventDetailResult.commandBridgeText.includes("草稿已选来源")) failures.push("event detail command bridge source coverage label missing");
  if (!eventDetailResult.commandBridgeText.includes("详情引用可作为目标")) failures.push("event detail command bridge target coverage label missing");
  if (!eventDetailResult.commandBridgeGrammarStates.includes("ready")) failures.push("event detail command bridge grammar ready state missing");
  if (!eventDetailResult.commandBridgeGrammarStepStates.includes("locked")) failures.push("event detail command bridge grammar source lock missing");
  if (!eventDetailResult.commandBridgeGrammarStepStates.includes("ready")) failures.push("event detail command bridge grammar submit ready missing");
  if (!eventDetailResult.commandBridgeGateStates.includes("ready")) failures.push("event detail command bridge gate ready state missing");
  if (!eventDetailResult.commandBridgeText.includes("提交门禁")) failures.push("event detail command bridge gate label missing");
  if (!eventDetailResult.commandBridgeSubmitStates.includes("ready")) failures.push("event detail command submit plan ready state missing");
  if (!eventDetailResult.commandBridgeSubmitCanSubmit.includes("true")) failures.push("event detail command submit plan can-submit missing");
  if (!eventDetailResult.commandBridgeSubmitCommandReady.includes("true")) failures.push("event detail command submit command-ready missing");
  if (eventDetailResult.commandBridgeSubmitButtonCount < 1) failures.push("event detail command submit button missing");
  if (!eventDetailResult.commandBridgeSubmitButtonEnabledStates.includes("true")) failures.push("event detail command submit button enabled state missing");
  if (!eventDetailResult.commandBridgeSubmitText.includes("命令预览")) failures.push("event detail command submit plan label missing");
  if (eventDetailResult.actionHintCount < 1) failures.push("event detail candidate hint rows missing");
  if (!eventDetailResult.actionHintText.includes("PLAY_CARD")) failures.push("event detail candidate hint command type missing");
  if (!eventDetailResult.actionHintText.includes("必填")) failures.push("event detail candidate hint required fields missing");
  if (eventDetailResult.sourceState !== "event") failures.push("event detail did not project source to table");
  if (eventDetailResult.targetState !== "event") failures.push("event detail did not project target to table");
  if (!eventDetailResult.selectedRow) failures.push("event detail selected row missing");
  if (eventDetailResult.detailLayerOpen) failures.push("event detail opened card detail layer");
  if (serverKindJumpKind !== "BATTLEFIELD_CONTROL_RESOLVED") failures.push(`receipt event kind jump clicked unexpected kind: ${serverKindJumpKind}`);
  if (serverKindJumpResult.detailId !== "event:BATTLEFIELD_CONTROL_RESOLVED:1") failures.push(`receipt event kind jump detail unexpected: ${serverKindJumpResult.detailId}`);
  if (serverKindJumpResult.panelState !== "event") failures.push(`receipt event kind jump panel state unexpected: ${serverKindJumpResult.panelState}`);
  if (serverKindJumpResult.selectedRowKind !== "BATTLEFIELD_CONTROL_RESOLVED") failures.push(`receipt event kind jump selected row unexpected: ${serverKindJumpResult.selectedRowKind}`);
  if (!serverKindJumpResult.text.includes("战场控制结算")) failures.push("receipt event kind jump detail title missing");
  if (serverKindRestoreKind !== "STACK_ITEM_ADDED") failures.push(`receipt event kind restore clicked unexpected kind: ${serverKindRestoreKind}`);
  if (serverKindRestoreResult.detailId !== "event:STACK_ITEM_ADDED:0") failures.push(`receipt event kind restore detail unexpected: ${serverKindRestoreResult.detailId}`);
  if (serverKindRestoreResult.panelState !== "event") failures.push(`receipt event kind restore panel state unexpected: ${serverKindRestoreResult.panelState}`);
  if (serverKindRestoreResult.selectedRowKind !== "STACK_ITEM_ADDED") failures.push(`receipt event kind restore selected row unexpected: ${serverKindRestoreResult.selectedRowKind}`);
  if (followupEventClickKind !== "BATTLEFIELD_CONTROL_RESOLVED") failures.push(`followup event click unexpected kind: ${followupEventClickKind}`);
  if (followupEventClickResult.detailId !== "event:BATTLEFIELD_CONTROL_RESOLVED:1") failures.push(`followup event click detail unexpected: ${followupEventClickResult.detailId}`);
  if (followupEventClickResult.panelState !== "event") failures.push(`followup event click panel state unexpected: ${followupEventClickResult.panelState}`);
  if (followupEventClickResult.selectedRowKind !== "BATTLEFIELD_CONTROL_RESOLVED") failures.push(`followup event click selected row unexpected: ${followupEventClickResult.selectedRowKind}`);
  if (!followupEventClickResult.text.includes("战场控制结算")) failures.push("followup event click detail title missing");
  if (detailClearResult.clearButton) failures.push("detail clear button remained after clearing");
  if (detailClearResult.panelState !== "object") failures.push(`detail clear did not return to selected object context: ${detailClearResult.panelState}`);
  if (detailClearResult.activeDetailId !== "event:BATTLEFIELD_CONTROL_RESOLVED:1") failures.push("detail clear did not restore focus to source detail trigger");
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

async function clickScopedWireDetail(cdp, scopeSelector, detailId) {
  const result = await cdp.send("Runtime.evaluate", {
    expression: `(() => {
      const scope = document.querySelector(${JSON.stringify(scopeSelector)});
      const element = scope?.querySelector(${JSON.stringify(`[data-wire-detail-id="${detailId}"]`)});
      if (!(element instanceof HTMLButtonElement) || element.disabled) return "";
      element.click();
      return element.getAttribute("data-wire-detail-id") ?? "";
    })()`,
    returnByValue: true
  });
  const clickedDetailId = String(result.result?.value ?? "");
  if (!clickedDetailId) {
    throw new Error(`Scoped wire detail trigger not found: ${scopeSelector} -> ${detailId}`);
  }
  return clickedDetailId;
}

async function firstScopedWireDetailId(cdp, scopeSelector) {
  const result = await cdp.send("Runtime.evaluate", {
    expression: `(() => {
      const scope = document.querySelector(${JSON.stringify(scopeSelector)});
      const element = scope?.querySelector("[data-wire-detail-id]");
      return element?.getAttribute("data-wire-detail-id") ?? "";
    })()`,
    returnByValue: true
  });
  const detailId = String(result.result?.value ?? "");
  if (!detailId) {
    throw new Error(`Scoped wire detail target not found: ${scopeSelector}`);
  }
  return detailId;
}

async function timelineDetailSummary(cdp) {
  return evaluateJson(cdp, `(() => {
    const panel = document.querySelector(".wire-timeline-detail");
    const trigger = document.querySelector('[data-detail-selected="true"][data-wire-detail-id]');
    return {
      detailId: panel?.getAttribute("data-wire-timeline-detail-id") ?? "",
      panelState: panel?.getAttribute("data-wire-timeline-detail-state") ?? "",
      text: panel?.textContent ?? "",
      triggerId: trigger?.getAttribute("data-wire-detail-id") ?? ""
    };
  })()`);
}

async function clickSelectedProjectionDetail(cdp, detailId) {
  const result = await cdp.send("Runtime.evaluate", {
    expression: `(() => {
      const projection = document.querySelector("[data-rule-selected-object-state='linked']");
      const element = projection?.querySelector(${JSON.stringify(`[data-wire-detail-id="${detailId}"]`)});
      if (!element) {
        return {
          clicked: false,
          relations: Array.from(projection?.querySelectorAll("[data-rule-selected-object-relation]") ?? []).map((node) => ({
            detail: node.getAttribute("data-rule-selected-object-relation-detail"),
            source: node.getAttribute("data-rule-selected-object-relation-source"),
            text: node.textContent
          }))
        };
      }
      element.click();
      return { clicked: true, relations: [] };
    })()`,
    returnByValue: true
  });
  const value = result.result?.value;
  if (!value?.clicked) {
    throw new Error(`Selected object projection detail trigger not found: ${detailId}; relations=${JSON.stringify(value?.relations ?? [])}`);
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

async function clickTimelineCommandFieldChoose(cdp, objectId) {
  const result = await cdp.send("Runtime.evaluate", {
    expression: `(() => {
      const element = document.querySelector(${JSON.stringify(`[data-timeline-command-field-choose-object-id="${objectId}"]`)});
      if (!(element instanceof HTMLButtonElement) || element.disabled) return "";
      element.click();
      return element.getAttribute("data-timeline-command-field-choose-object-id") ?? "";
    })()`,
    returnByValue: true
  });
  const clickedObjectId = String(result.result?.value ?? "");
  if (!clickedObjectId) {
    throw new Error(`Wire timeline command field choose button not found: ${objectId}`);
  }
  return clickedObjectId;
}

async function timelineCommandSubmitSummary(cdp) {
  return evaluateJson(cdp, `(() => {
    const panel = document.querySelector(".wire-timeline-detail");
    const routeSummary = panel?.querySelector(".wire-timeline-route-summary");
    const feedback = document.querySelector("[data-command-submission-state]");
    return {
      commandSubmissionState: feedback?.getAttribute("data-command-submission-state") ?? "",
      detailId: panel?.getAttribute("data-wire-timeline-detail-id") ?? "",
      fieldStates: Array.from(panel?.querySelectorAll("[data-timeline-command-field-state]") ?? [])
        .map((item) => item.getAttribute("data-timeline-command-field-state") ?? ""),
      routeSummaryState: routeSummary?.getAttribute("data-timeline-route-summary-state") ?? "",
      selectedObjectIds: Array.from(panel?.querySelectorAll("[data-timeline-command-field-selected-object-id]") ?? [])
        .map((item) => item.getAttribute("data-timeline-command-field-selected-object-id") ?? ""),
      submitCanSubmitStates: Array.from(panel?.querySelectorAll("[data-timeline-command-submit-can-submit]") ?? [])
        .map((item) => item.getAttribute("data-timeline-command-submit-can-submit") ?? ""),
      submitCommandReadyStates: Array.from(panel?.querySelectorAll("[data-timeline-command-submit-command-ready]") ?? [])
        .map((item) => item.getAttribute("data-timeline-command-submit-command-ready") ?? ""),
      submitEnabledStates: Array.from(panel?.querySelectorAll("[data-timeline-command-submit]") ?? [])
        .map((item) => item.getAttribute("data-timeline-command-submit-enabled") ?? ""),
      submitStates: Array.from(panel?.querySelectorAll("[data-timeline-command-submit-state]") ?? [])
        .map((item) => item.getAttribute("data-timeline-command-submit-state") ?? ""),
      submitText: Array.from(panel?.querySelectorAll(".wire-timeline-command-submit-plan") ?? [])
        .map((item) => item.textContent ?? "").join(" / "),
      submitTypes: Array.from(panel?.querySelectorAll("[data-timeline-command-submit-type]") ?? [])
        .map((item) => item.getAttribute("data-timeline-command-submit-type") ?? "")
    };
  })()`);
}

async function clickTimelineCommandSubmit(cdp) {
  const result = await cdp.send("Runtime.evaluate", {
    expression: `(() => {
      const element = document.querySelector('[data-timeline-command-submit-enabled="true"]');
      if (!(element instanceof HTMLButtonElement) || element.disabled) return "";
      const plan = element.closest("[data-timeline-command-submit-type]");
      const commandType = plan?.getAttribute("data-timeline-command-submit-type") ?? "";
      element.click();
      return commandType;
    })()`,
    returnByValue: true
  });
  const commandType = String(result.result?.value ?? "");
  if (!commandType) {
    throw new Error("Wire timeline command submit button not found");
  }
  return commandType;
}

async function waitForTimelineCommandFollowup(cdp) {
  const deadline = Date.now() + 10_000;
  let last = {};
  while (Date.now() < deadline) {
    last = await evaluateJson(cdp, `(() => {
      const panel = document.querySelector(".wire-timeline-detail");
      const feedback = document.querySelector("[data-command-submission-state]");
      const followup = panel?.querySelector(".wire-timeline-command-followup");
      return {
        commandSubmissionCommand: feedback?.querySelector('[data-command-submission-metric="command"] strong')?.textContent?.trim() ?? "",
        commandSubmissionState: feedback?.getAttribute("data-command-submission-state") ?? "",
        commandSubmissionText: feedback?.textContent ?? "",
        timelineFollowupBridgeState: followup?.querySelector(".wire-command-followup-bridge")?.getAttribute("data-command-followup-bridge-state") ?? "",
        timelineFollowupLayoutState: followup?.querySelector("[data-command-followup-layout-state]")?.getAttribute("data-command-followup-layout-state") ?? "",
        timelineFollowupServerKindActions: Array.from(followup?.querySelectorAll("[data-command-followup-server-event-kind-action]") ?? [])
          .map((item) => item.getAttribute("data-command-followup-server-event-kind-action") ?? ""),
        timelineFollowupServerState: followup?.getAttribute("data-command-followup-server-state") ?? "",
        timelineFollowupSourceDetail: followup?.querySelector("[data-command-followup-source-detail]")?.getAttribute("data-command-followup-source-detail") ?? "",
        timelineFollowupSourceObject: followup?.querySelector("[data-command-followup-source-object]")?.getAttribute("data-command-followup-source-object") ?? "",
        timelineFollowupSourceSurface: followup?.querySelector("[data-command-followup-source-surface]")?.getAttribute("data-command-followup-source-surface") ?? "",
        timelineFollowupState: followup?.getAttribute("data-command-followup-state") ?? "",
        timelineFollowupText: followup?.textContent ?? ""
      };
    })()`);
    if (
      last.commandSubmissionState === "sent"
      && acceptedCommandFollowupStates.includes(last.timelineFollowupState)
    ) {
      return last;
    }
    await delay(150);
  }

  throw new Error(`Timed out waiting for timeline command followup: ${JSON.stringify(last, null, 2)}`);
}

async function openCommandSubmissionLayer(cdp) {
  const opened = await cdp.send("Runtime.evaluate", {
    expression: `(() => {
      const element = document.querySelector(".wire-command-submission-feedback .wire-command-submission-open-layer:not([disabled])");
      if (!(element instanceof HTMLButtonElement)) return false;
      element.click();
      return true;
    })()`,
    returnByValue: true
  });
  if (!opened.result?.value) {
    throw new Error("Command submission layer open button not found");
  }
  await delay(150);
  return commandSubmissionLayerSummary(cdp);
}

async function commandSubmissionLayerSummary(cdp) {
  return evaluateJson(cdp, `(() => {
    const layer = document.querySelector(".wire-command-submission-layer");
    return {
      cmdType: layer?.getAttribute("data-command-submission-layer-cmd-type") ?? "",
      eventCount: Number(layer?.getAttribute("data-command-submission-layer-event-count") ?? "0"),
      eventKinds: Array.from(layer?.querySelectorAll("[data-command-followup-event-action]") ?? [])
        .map((item) => item.getAttribute("data-command-followup-event-action") ?? ""),
      followupState: layer?.getAttribute("data-command-submission-layer-followup-state") ?? "",
      hiddenCount: Number(layer?.getAttribute("data-command-submission-layer-hidden-count") ?? "0"),
      layoutObjects: Array.from(layer?.querySelectorAll("[data-command-followup-layout-object]") ?? [])
        .map((item) => item.getAttribute("data-command-followup-layout-object") ?? ""),
      open: Boolean(layer),
      receiptState: layer?.getAttribute("data-command-submission-layer-receipt-state") ?? "",
      serverState: layer?.getAttribute("data-command-submission-layer-server-state") ?? "",
      sourceDetail: layer?.getAttribute("data-command-submission-layer-source-detail") ?? "",
      sourceObject: layer?.getAttribute("data-command-submission-layer-source-object") ?? "",
      sourceSurface: layer?.getAttribute("data-command-submission-layer-source-surface") ?? "",
      state: layer?.getAttribute("data-command-submission-layer-state") ?? "",
      text: layer?.textContent ?? ""
    };
  })()`);
}

async function clickCommandSubmissionLayerFollowupEvent(cdp, kind, order) {
  const result = await cdp.send("Runtime.evaluate", {
    expression: `(() => {
      const selector = ${JSON.stringify(
        `.wire-command-submission-layer [data-command-followup-event-action="${kind}"]${order == null ? "" : `[data-command-followup-event-order-action="${order}"]`}`
      )};
      const element = document.querySelector(selector);
      if (!(element instanceof HTMLButtonElement) || element.disabled) return "";
      element.click();
      return element.getAttribute("data-command-followup-event-action") ?? "";
    })()`,
    returnByValue: true
  });
  const clickedKind = String(result.result?.value ?? "");
  if (!clickedKind) {
    throw new Error(`Command submission layer followup event button not found: ${kind}`);
  }
  return clickedKind;
}

async function clickCommandSubmissionLayerLayoutObject(cdp, objectId) {
  const result = await cdp.send("Runtime.evaluate", {
    expression: `(() => {
      const row = document.querySelector(${JSON.stringify(`.wire-command-submission-layer [data-command-followup-layout-object="${objectId}"]`)});
      const element = row?.querySelector("button");
      if (!(element instanceof HTMLButtonElement) || element.disabled) return "";
      element.click();
      return row?.getAttribute("data-command-followup-layout-object") ?? "";
    })()`,
    returnByValue: true
  });
  const clickedObjectId = String(result.result?.value ?? "");
  if (!clickedObjectId) {
    throw new Error(`Command submission layer layout object not found: ${objectId}`);
  }
  return clickedObjectId;
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

async function clickTimelineFollowupServerKind(cdp, kind, order) {
  const result = await cdp.send("Runtime.evaluate", {
    expression: `(() => {
      const selector = ${JSON.stringify(
        `.wire-timeline-command-followup [data-command-followup-server-event-kind-action="${kind}"]${order == null ? "" : `[data-command-followup-server-event-order-action="${order}"]`}`
      )};
      const element = document.querySelector(selector);
      if (!(element instanceof HTMLButtonElement) || element.disabled) return "";
      element.click();
      return element.getAttribute("data-command-followup-server-event-kind-action") ?? "";
    })()`,
    returnByValue: true
  });
  const clickedKind = String(result.result?.value ?? "");
  if (!clickedKind) {
    throw new Error(`Wire timeline followup event kind button not found: ${kind}`);
  }
  return clickedKind;
}

async function clickTimelineFollowupEvent(cdp, kind, order) {
  const result = await cdp.send("Runtime.evaluate", {
    expression: `(() => {
      const selector = ${JSON.stringify(
        `.wire-timeline-command-followup [data-command-followup-event-action="${kind}"]${order == null ? "" : `[data-command-followup-event-order-action="${order}"]`}`
      )};
      const element = document.querySelector(selector);
      if (!(element instanceof HTMLButtonElement) || element.disabled) return "";
      element.click();
      return element.getAttribute("data-command-followup-event-action") ?? "";
    })()`,
    returnByValue: true
  });
  const clickedKind = String(result.result?.value ?? "");
  if (!clickedKind) {
    throw new Error(`Wire timeline followup event button not found: ${kind}`);
  }
  return clickedKind;
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
  if (result.exceptionDetails) {
    const description = result.exceptionDetails.exception?.description
      ?? result.exceptionDetails.text
      ?? "unknown Runtime.evaluate error";
    throw new Error(`Chrome Runtime.evaluate failed: ${description}`);
  }
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

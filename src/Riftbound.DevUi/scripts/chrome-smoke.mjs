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
  const focusResult = await evaluateJson(cdp, `(() => {
    const summary = document.querySelector(".wire-focused-action-summary");
    return {
      state: summary?.getAttribute("data-wire-focused-action-state") ?? null,
      text: summary?.textContent ?? "",
      contextText: document.querySelector(".wire-object-context")?.textContent ?? "",
      grammarState: document.querySelector(".wire-focused-grammar")?.getAttribute("data-wire-focused-grammar-state") ?? null,
      grammarText: document.querySelector(".wire-focused-grammar")?.textContent ?? "",
      grammarRoles: Array.from(document.querySelectorAll("[data-wire-grammar-role]")).map((node) => node.getAttribute("data-wire-grammar-role")),
      nextStep: document.querySelector("[data-wire-focused-next-step]")?.textContent ?? "",
      candidatePlanCount: document.querySelectorAll(".wire-focused-candidate-plan li").length,
      detailLayerOpen: Boolean(document.querySelector(".detail-layer"))
    };
  })()`);
  await focusObject(cdp, "p1-hand-spell");
  await clickButtonByText(cdp, "查看详情");
  await delay(150);
  const detailContextResult = await evaluateJson(cdp, `(() => {
    const detail = document.querySelector(".detail-layer");
    return {
      activeText: document.activeElement?.textContent ?? "",
      labelledBy: detail?.getAttribute("aria-labelledby") ?? "",
      state: detail?.getAttribute("data-detail-dialog-state") ?? null,
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
    const priorityRail = document.querySelector(".wire-priority-rail");
    const ruleQueue = document.querySelector(".wire-rule-queue");
    const ruleFlow = document.querySelector(".wire-rule-flow");
    return {
      selected: tableObject?.getAttribute("data-selected") ?? null,
      actionMapText: document.querySelector(".wire-action-map")?.textContent ?? "",
      candidatePlanCount: document.querySelectorAll(".wire-action-candidate-plan-card").length,
      candidatePlanEnabled: candidatePlan?.getAttribute("data-candidate-plan-enabled") ?? null,
      candidatePlanNext: candidatePlan?.querySelector("[data-candidate-plan-next-step]")?.textContent ?? "",
      candidatePlanText: candidatePlan?.textContent ?? "",
      chipSelected: actionChip?.getAttribute("data-selected") ?? null,
      detailLayerOpen: Boolean(document.querySelector(".detail-layer")),
      focusText: document.querySelector(".wire-focused-action-summary")?.textContent ?? "",
      windowState: windowPlan?.getAttribute("data-wire-window-state") ?? null,
      windowText: windowPlan?.textContent ?? "",
      priorityMode: windowPlan?.getAttribute("data-wire-priority-mode") ?? null,
      priorityRailText: priorityRail?.textContent ?? "",
      priorityActiveStep: document.querySelector('[data-priority-step-state="active"]')?.getAttribute("data-priority-step") ?? null,
      ruleFlowText: ruleFlow?.textContent ?? "",
      ruleLaneCount: document.querySelectorAll("[data-rule-lane]").length,
      ruleQueueState: ruleQueue?.getAttribute("data-wire-rule-queue-state") ?? null,
      ruleSequenceCount: document.querySelectorAll("[data-rule-sequence-lane]").length
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
  if (focusResult.state !== "server-candidate") failures.push("focused action summary did not use server candidate state");
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
  if (!detailContextResult.text.includes("服务端:cardNo*")) failures.push("card detail command metadata field missing");
  if (detailEscapeResult.open) failures.push("card detail did not close on Escape");
  if (detailEscapeResult.activeObjectId !== "p1-hand-spell") failures.push("card detail did not restore focus to source card");
  if (!focusResult.nextStep.includes("下一步")) failures.push("focused action next step missing");
  if (focusResult.candidatePlanCount < 1) failures.push("focused action candidate plan missing");
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
  if (!actionMapResult.actionMapText.includes("命令字段")) failures.push("action map command field list missing");
  if (!actionMapResult.actionMapText.includes("候选步骤")) failures.push("action map candidate step plan missing");
  if (!actionMapResult.actionMapText.includes("下一步")) failures.push("action map candidate next-step text missing");
  if (!actionMapResult.actionMapText.includes("PLAY_CARD")) failures.push("action map command type missing");
  if (!actionMapResult.actionMapText.includes("服务端:cardNo*")) failures.push("action map metadata command field missing");
  if (!["resolving", "you-action"].includes(actionMapResult.windowState)) failures.push(`wire window plan did not show a server-derived active state: ${actionMapResult.windowState}`);
  if (!actionMapResult.windowText.includes("窗口总览")) failures.push("wire window plan header missing");
  if (!actionMapResult.windowText.includes("下一步")) failures.push("wire window plan next step missing");
  if (!actionMapResult.windowText.includes("可提交")) failures.push("wire window plan candidate metric missing");
  if (!actionMapResult.windowText.includes("结算链")) failures.push("wire window plan stack metric missing");
  if (!actionMapResult.windowText.includes("任务")) failures.push("wire window plan task metric missing");
  if (!["battle", "battlefield-task", "task"].includes(actionMapResult.priorityMode)) failures.push(`wire priority rail mode did not reflect server task context: ${actionMapResult.priorityMode}`);
  if (actionMapResult.priorityActiveStep !== "focus" && actionMapResult.priorityActiveStep !== "tasks") failures.push(`wire priority rail active step missing task/focus state: ${actionMapResult.priorityActiveStep}`);
  if (!actionMapResult.priorityRailText.includes("优先权轨道")) failures.push("wire priority rail header missing");
  if (!actionMapResult.priorityRailText.includes("响应/焦点")) failures.push("wire priority rail focus step missing");
  if (!actionMapResult.priorityRailText.includes("规则任务")) failures.push("wire priority rail task step missing");
  if (!actionMapResult.priorityRailText.includes("操作入口")) failures.push("wire priority rail entry step missing");
  if (!["task-blocked", "task-open", "stack-response"].includes(actionMapResult.ruleQueueState)) failures.push(`wire rule queue state did not reflect server queue context: ${actionMapResult.ruleQueueState}`);
  if (actionMapResult.ruleLaneCount !== 4) failures.push(`wire rule queue lane count mismatch: ${actionMapResult.ruleLaneCount}`);
  if (!actionMapResult.ruleFlowText.includes("规则队列地图")) failures.push("wire rule queue flow header missing");
  if (!actionMapResult.ruleFlowText.includes("结算链")) failures.push("wire rule queue stack lane missing");
  if (!actionMapResult.ruleFlowText.includes("规则任务")) failures.push("wire rule queue task lane missing");
  if (!actionMapResult.ruleFlowText.includes("触发队列")) failures.push("wire rule queue trigger lane missing");
  if (!actionMapResult.ruleFlowText.includes("近期事件")) failures.push("wire rule queue resolution lane missing");
  if (!actionMapResult.ruleFlowText.includes("下一步")) failures.push("wire rule queue next step missing");
  if (actionMapResult.ruleSequenceCount < 1) failures.push("wire rule queue sequence items missing");
  if (actionMapResult.candidatePlanCount < 1) failures.push("action map candidate plan cards missing");
  if (actionMapResult.candidatePlanEnabled !== "true") failures.push("PLAY_CARD candidate plan did not preserve enabled state");
  if (!actionMapResult.candidatePlanText.includes("命令字段 5")) failures.push("PLAY_CARD candidate plan command field count missing");
  if (!actionMapResult.candidatePlanText.includes("缺口 0")) failures.push("PLAY_CARD candidate plan gap summary missing");
  if (!actionMapResult.candidatePlanNext.includes("下一步")) failures.push("PLAY_CARD candidate plan next-step missing");
  if (!actionMapResult.focusText.includes("服务端状态")) failures.push("action map focus did not refresh focused action summary");
  if (actionMapResult.detailLayerOpen) failures.push("action map object chip opened detail");
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
  if (!candidateRefResult.detailContextText.includes("服务端:cardNo*")) failures.push("timeline selected object context command metadata field missing");
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
      panelState: panel?.getAttribute("data-wire-timeline-detail-state") ?? null,
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

  await clickWireDetail(cdp, "event:STACK_ITEM_ADDED:0");
  await delay(150);
  const eventDetailResult = await evaluateJson(cdp, `(() => {
    const panel = document.querySelector(".wire-timeline-detail");
    const trigger = document.querySelector('[data-wire-detail-id="event:STACK_ITEM_ADDED:0"]');
    const selectedRow = document.querySelector(".log-row.is-detail-selected");
    const sourceObject = document.querySelector('[data-object-id="p1-hand-spell"]');
    const targetObject = document.querySelector('[data-object-id="p2-right-1"]');
    return {
      text: panel?.textContent ?? "",
      panelState: panel?.getAttribute("data-wire-timeline-detail-state") ?? null,
      triggerAriaPressed: trigger?.getAttribute("aria-pressed") ?? null,
      triggerControls: trigger?.getAttribute("aria-controls") ?? "",
      triggerLabel: trigger?.getAttribute("aria-label") ?? "",
      triggerSelected: trigger?.getAttribute("data-detail-selected") ?? null,
      triggerSource: trigger?.getAttribute("data-wire-detail-source") ?? null,
      selectedRow: Boolean(selectedRow),
      hasSourceRef: Boolean(panel?.querySelector('[data-event-object-ref="p1-hand-spell"]')),
      hasTargetRef: Boolean(panel?.querySelector('[data-event-object-ref="p2-right-1"]')),
      projectionStates: Array.from(panel?.querySelectorAll(".wire-timeline-projection-list li") ?? [])
        .map((item) => item.getAttribute("data-projection-state")),
      projectionText: panel?.querySelector(".wire-timeline-projection-list")?.textContent ?? "",
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
  if (!ruleDetailResult.text.includes("来源")) failures.push("rule detail source line missing");
  if (!ruleDetailResult.hasSourceRef) failures.push("rule detail source ref missing");
  if (!ruleDetailResult.hasTargetRef) failures.push("rule detail target ref missing");
  if (!ruleDetailResult.statusText.includes("桌面投影")) failures.push("rule detail projection status missing");
  if (!ruleDetailResult.statusText.includes("当前焦点")) failures.push("rule detail focus status missing");
  if (!ruleDetailResult.statusText.includes("关联候选")) failures.push("rule detail candidate status missing");
  if (!ruleDetailResult.projectionStates.includes("visible")) failures.push("rule detail did not expose visible projection rows");
  if (!ruleDetailResult.projectionText.includes("来源")) failures.push("rule detail projection source role missing");
  if (!ruleDetailResult.projectionText.includes("目标")) failures.push("rule detail projection target role missing");
  if (ruleDetailResult.actionHintCount < 1) failures.push("rule detail candidate hint rows missing");
  if (!ruleDetailResult.actionHintText.includes("PLAY_CARD")) failures.push("rule detail candidate hint command type missing");
  if (!ruleDetailResult.actionHintText.includes("可用")) failures.push("rule detail candidate hint state missing");
  if (ruleDetailResult.sourceState !== "rule") failures.push("rule detail did not project source to table");
  if (ruleDetailResult.targetState !== "rule") failures.push("rule detail did not project target to table");
  if (!ruleDetailResult.selectedRow) failures.push("rule detail selected row missing");
  if (ruleDetailResult.detailLayerOpen) failures.push("rule detail opened card detail layer");
  if (!eventDetailResult.text.includes("加入结算链")) failures.push("event detail title missing");
  if (eventDetailResult.panelState !== "event") failures.push(`event detail panel state unexpected: ${eventDetailResult.panelState}`);
  if (eventDetailResult.triggerAriaPressed !== "true") failures.push("event detail trigger aria-pressed missing");
  if (eventDetailResult.triggerControls !== "wire-timeline-detail-body") failures.push("event detail trigger aria-controls missing");
  if (!eventDetailResult.triggerLabel.includes("加入结算链")) failures.push("event detail trigger accessible label missing");
  if (eventDetailResult.triggerSelected !== "true") failures.push("event detail trigger selected state missing");
  if (eventDetailResult.triggerSource !== "event") failures.push("event detail trigger source missing");
  if (!eventDetailResult.text.includes("服务端摘要")) failures.push("event detail did not use server object refs");
  if (!eventDetailResult.hasSourceRef) failures.push("event detail source ref missing");
  if (!eventDetailResult.hasTargetRef) failures.push("event detail target ref missing");
  if (!eventDetailResult.statusText.includes("日志事件")) failures.push("event detail did not label event source");
  if (!eventDetailResult.statusText.includes("关联候选")) failures.push("event detail candidate status missing");
  if (!eventDetailResult.projectionStates.includes("visible")) failures.push("event detail did not expose visible projection rows");
  if (!eventDetailResult.projectionText.includes("来源")) failures.push("event detail projection source role missing");
  if (eventDetailResult.actionHintCount < 1) failures.push("event detail candidate hint rows missing");
  if (!eventDetailResult.actionHintText.includes("PLAY_CARD")) failures.push("event detail candidate hint command type missing");
  if (eventDetailResult.sourceState !== "event") failures.push("event detail did not project source to table");
  if (eventDetailResult.targetState !== "event") failures.push("event detail did not project target to table");
  if (!eventDetailResult.selectedRow) failures.push("event detail selected row missing");
  if (eventDetailResult.detailLayerOpen) failures.push("event detail opened card detail layer");
  if (detailClearResult.clearButton) failures.push("detail clear button remained after clearing");
  if (detailClearResult.panelState !== "object") failures.push(`detail clear did not return to selected object context: ${detailClearResult.panelState}`);
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

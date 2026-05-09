import { spawn } from "node:child_process";
import { existsSync } from "node:fs";
import { mkdtemp, rm } from "node:fs/promises";
import { tmpdir } from "node:os";
import path from "node:path";
import { fileURLToPath } from "node:url";

const scriptDir = path.dirname(fileURLToPath(import.meta.url));
const appRoot = path.resolve(scriptDir, "..");
const repoRoot = path.resolve(scriptDir, "../../..");
const frontendPort = Number(process.env.RIFTBOUND_PREFLIGHT_FRONTEND_PORT ?? 5174);
const serverUrl = process.env.RIFTBOUND_SERVER_URL ?? "http://127.0.0.1:5088";
const frontendUrl = `http://127.0.0.1:${frontendPort}`;
const roomId = process.env.RIFTBOUND_PREFLIGHT_ROOM_ID ?? "stage3-preflight";
const startApi = process.argv.includes("--start-api");
const clients = [
  { debugPort: 9340, name: "player-a", playerId: "preflight-alpha" },
  { debugPort: 9341, name: "player-b", playerId: "preflight-beta" }
];

const children = [];
const userDataDirs = [];
const browserErrors = [];

try {
  if (startApi) {
    await ensureApi();
  }

  const preview = spawnChild(viteBin(), ["preview", "--host", "127.0.0.1", "--port", String(frontendPort), "--strictPort"], {
    cwd: appRoot,
    name: "vite-preview"
  });
  children.push(preview);
  await waitForHttp(`${frontendUrl}/`, 30_000);

  for (const client of clients) {
    await runClientPreflight(client);
  }

  if (browserErrors.length > 0) {
    throw new Error(`Chrome preflight reported errors:\n${browserErrors.join("\n")}`);
  }

  console.log("Stage 3 preflight passed.");
} finally {
  for (const child of children.reverse()) {
    child.kill("SIGTERM");
  }

  await delay(300);
  for (const userDataDir of userDataDirs) {
    await rm(userDataDir, { force: true, recursive: true, maxRetries: 5, retryDelay: 150 });
  }
}

async function runClientPreflight(client) {
  const userDataDir = await mkdtemp(path.join(tmpdir(), `riftbound-${client.name}-`));
  userDataDirs.push(userDataDir);
  const chrome = spawnChild(chromePath(), [
    "--headless=new",
    "--disable-gpu",
    "--no-first-run",
    "--no-default-browser-check",
    `--remote-debugging-port=${client.debugPort}`,
    `--user-data-dir=${userDataDir}`,
    "about:blank"
  ], { name: `chrome-${client.name}` });
  children.push(chrome);
  await waitForHttp(`http://127.0.0.1:${client.debugPort}/json/version`, 15_000);

  const tab = await openChromeTab(client.debugPort, `${frontendUrl}/`);
  const cdp = await connectCdp(tab.webSocketDebuggerUrl, client.name);
  await cdp.send("Page.enable");
  await cdp.send("Runtime.enable");
  await cdp.send("Log.enable");

  await cdp.send("Page.navigate", { url: `${frontendUrl}/` });
  await waitForText(cdp, ["符文战场", "进入大厅"]);
  await setClientSettings(cdp, client.playerId);
  await cdp.send("Page.navigate", { url: `${frontendUrl}/rooms/${roomId}` });
  await waitForText(cdp, ["房间", "连接/重连并入座", "选择卡组"]);
  await clickButton(cdp, "连接/重连并入座");
  await waitForText(cdp, ["进入对战桌面"]);
  await cdp.send("Page.navigate", { url: `${frontendUrl}/matches/${roomId}` });
  await waitForText(cdp, ["对战状态", "正式桌面状态", "战场", "支付费用", "伤害分配", "触发排序", "服务端行动提示", "权威快照摘要"]);
  await expectAbsentText(cdp, [
    "mainDeck",
    "runeDeck",
    "handHidden",
    "reconnectToken",
    "serverPaymentState",
    "resourceLedgerBeforePayment",
    "damageLedger",
    "triggerQueue"
  ]);
  await cdp.close();
  console.log(`Stage 3 preflight OK: ${client.name}`);
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
    throw new Error("Google Chrome was not found. Set CHROME_PATH to run Stage 3 preflight.");
  }

  return found;
}

async function openChromeTab(debugPort, url) {
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

async function connectCdp(webSocketDebuggerUrl, clientName) {
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

  eventHandlers.push((message) => {
    if (message.method === "Runtime.exceptionThrown") {
      browserErrors.push(`${clientName} exception: ${message.params?.exceptionDetails?.text ?? "unknown"}`);
    }

    if (message.method === "Runtime.consoleAPICalled" && message.params?.type === "error") {
      browserErrors.push(`${clientName} console.error: ${consoleArgs(message.params.args)}`);
    }

    if (message.method === "Log.entryAdded" && message.params?.entry?.level === "error") {
      const text = String(message.params.entry.text ?? "");
      if (!text.includes(serverUrl) && !text.includes("Failed to load resource: the server responded with a status of 404")) {
        browserErrors.push(`${clientName} log.error: ${text}`);
      }
    }
  });

  return {
    close: () => ws.close(),
    send: (method, params = {}) => new Promise((resolve, reject) => {
      const id = nextId++;
      pending.set(id, { resolve, reject });
      ws.send(JSON.stringify({ id, method, params }));
    })
  };
}

async function setClientSettings(cdp, playerId) {
  await cdp.send("Runtime.evaluate", {
    expression: `
      localStorage.setItem("riftbound.serverUrl", ${JSON.stringify(serverUrl)});
      localStorage.setItem("riftbound.playerId", ${JSON.stringify(playerId)});
      localStorage.setItem("riftbound.logDensity", "compact");
      localStorage.setItem("riftbound.animationLevel", "off");
      true;
    `,
    returnByValue: true
  });
}

async function clickButton(cdp, text) {
  const result = await cdp.send("Runtime.evaluate", {
    expression: `
      (() => {
        const button = Array.from(document.querySelectorAll("button"))
          .find((item) => item.innerText.includes(${JSON.stringify(text)}));
        if (!button) return false;
        button.click();
        return true;
      })()
    `,
    returnByValue: true
  });
  if (!result.result?.value) {
    throw new Error(`Could not click button containing text: ${text}`);
  }
}

async function waitForText(cdp, texts) {
  const deadline = Date.now() + 12_000;
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

function delay(ms) {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

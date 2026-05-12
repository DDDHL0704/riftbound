import { spawn } from "node:child_process";
import { existsSync } from "node:fs";
import { mkdtemp, rm } from "node:fs/promises";
import { tmpdir } from "node:os";
import path from "node:path";
import { fileURLToPath } from "node:url";
import * as signalR from "@microsoft/signalr";

const scriptDir = path.dirname(fileURLToPath(import.meta.url));
const appRoot = path.resolve(scriptDir, "..");
const repoRoot = path.resolve(scriptDir, "../../..");
const frontendPort = Number(process.env.RIFTBOUND_E2E_FRONTEND_PORT ?? 5174);
const debugPortBase = Number(process.env.RIFTBOUND_E2E_CHROME_DEBUG_PORT ?? 9340);
const serverUrl = process.env.RIFTBOUND_SERVER_URL ?? "http://127.0.0.1:5088";
const frontendUrl = `http://127.0.0.1:${frontendPort}`;
const startApi = process.argv.includes("--start-api");

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

const formalDeck = {
  cmdType: "SUBMIT_DECK",
  legendCardNo: "UNL-181/219",
  championCardNo: "UNL-022/219",
  mainDeck: [
    "UNL-022/219",
    ...repeat([
      "UNL-002/219",
      "UNL-065/219",
      "UNL-076/219",
      "UNL-220/219",
      "SFD·007/221",
      "SFD·018/221",
      "SFD·065/221",
      "SFD·069/221",
      "OGN·010/298",
      "OGN·096/298",
      "OGN·103/298",
      "ARC-001/006",
      "SFD·013/221"
    ], 3)
  ],
  runeDeck: [
    "OGN·007/298",
    "OGN·007a/298",
    "OGN·007b/298",
    "OGN·089/298",
    "OGN·089a/298",
    "OGN·089b/298",
    "SFD·R01",
    "SFD·R01a",
    "SFD·R01b",
    "SFD·R03",
    "SFD·R03a",
    "SFD·R03b"
  ],
  battlefields: ["OGN·290/298", "OGN·275/298", "OGN·276/298"]
};

const children = [];
const tempDirs = [];
let roomDriver;

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

  roomDriver = await createFormalRoomDriver();
  const browserErrors = [];
  const p1Tab = await openPlayerChrome("P1", roomDriver.sessions.P1.reconnectToken, debugPortBase, browserErrors);
  const p2Tab = await openPlayerChrome("P2", roomDriver.sessions.P2.reconnectToken, debugPortBase + 1, browserErrors);

  await connectPlayerTab(p1Tab);
  await connectPlayerTab(p2Tab);
  await expectAbsentText(p1Tab.cdp, hiddenDebugTexts);
  await expectAbsentText(p2Tab.cdp, hiddenDebugTexts);

  await runFormal18(roomDriver, p1Tab, p2Tab);

  if (browserErrors.length > 0) {
    throw new Error(`Chrome reported errors:\n${browserErrors.join("\n")}`);
  }

  await p1Tab.cdp.close();
  await p2Tab.cdp.close();
  console.log(`Formal 18-step E2E passed: ${roomDriver.roomId}`);
} finally {
  if (roomDriver) {
    await roomDriver.close().catch(() => undefined);
  }

  for (const child of children.reverse()) {
    await stopChild(child);
  }

  for (const dir of tempDirs.reverse()) {
    await rm(dir, { force: true, maxRetries: 5, recursive: true, retryDelay: 150 })
      .catch((error) => console.warn(`Could not remove temp dir ${dir}: ${error.message}`));
  }
}

async function runFormal18(driver, p1Tab, p2Tab) {
  const { roomId, clients } = driver;
  const p1 = clients.P1;
  const p2 = clients.P2;

  logStep(1, "P1 creates room and joins through the server hub.");
  assertJoined(p1, "P1");
  logStep(2, "P2 joins the same room.");
  assertJoined(p2, "P2");
  logStep(3, "Both players submit legal official decks with a deterministic scoring battlefield candidate.");
  assertEvent(p1, "DECK_SUBMITTED", (event) => event.payload?.playerId === "P1");
  assertEvent(p1, "DECK_SUBMITTED", (event) => event.payload?.playerId === "P2");
  logStep(4, "Both players ready and the server starts official mulligan.");
  assertEvent(p1, "MATCH_STARTED");
  assertEqual(phase(p1), "MULLIGAN", "Expected official opening to enter mulligan.");
  logStep(5, "Both players confirm mulligan choices.");
  await submit(p1, { cmdType: "MULLIGAN", handObjectIds: [] }, "mulligan-p1");
  await submit(p2, { cmdType: "MULLIGAN", handObjectIds: [] }, "mulligan-p2");
  await waitFor(() => phase(p1) === "MAIN" && snapshot(p1).activePlayerId === "P1", "P1 first main phase");
  assertEvent(p1, "MULLIGAN_PHASE_COMPLETED");
  logStep(6, "First turn begins with server rune call and draw.");
  assertEvent(p1, "RUNES_CALLED", (event) => event.payload?.playerId === "P1");
  assertEvent(p1, "CARD_DRAWN", (event) => event.payload?.playerId === "P1");
  await waitForText(p1Tab.cdp, ["正式桌面状态", "服务端行动提示", "P1"]);

  logStep(7, "Active player taps server-provided runes for mana.");
  await tapRunesForMana(p1, 2);
  assertEvent(p1, "RUNE_TAPPED");
  assertEvent(p1, "MANA_GAINED");

  logStep(8, "Active player plays a unit from the server ActionPrompt.");
  const playSource = await playFirstUnit(p1);
  assertEvent(p1, "CARD_PLAYED");
  assertEvent(p1, "STACK_ITEM_ADDED");
  await waitForText(p1Tab.cdp, ["法术对决", "结算链"]);

  logStep(9, "Both players pass priority on the stack window.");
  await passPriorityUntilStackResolves(p1, p2);
  assertEvent(p1, "PRIORITY_PASSED", (event) => event.payload?.playerId === "P1");
  assertEvent(p1, "PRIORITY_PASSED", (event) => event.payload?.playerId === "P2");
  logStep(10, "The played unit resolves from stack into base.");
  assertEvent(p1, "STACK_ITEM_RESOLVED");
  assertEvent(p1, "UNIT_PLAYED_TO_BASE", (event) => event.payload?.sourceObjectId === playSource);

  logStep(11, "The unit moves to the opponent battlefield through a legal server destination.");
  await moveUnitToOpponentBattlefield(p1, "P2");
  assertEvent(p1, "UNIT_MOVED_TO_BATTLEFIELD", (event) => event.payload?.playerId === "P1");
  await waitForText(p1Tab.cdp, ["中央战场", "OGN·290/298"]);

  logStep(12, "Reconnect restores the active player's authoritative state before ending turn.");
  await reloadAndReconnect(p1Tab);
  await waitForText(p1Tab.cdp, ["正式桌面状态", "P1", "OGN·290/298"]);
  assertOpponentHandRedacted(snapshot(p1), "P2");

  logStep(13, "Active player ends turn.");
  await submit(p1, { cmdType: "END_TURN" }, "end-turn-p1");
  assertEvent(p1, "TURN_END_DECLARED");
  logStep(14, "Opponent turn begins.");
  await waitFor(() => phase(p1) === "MAIN" && snapshot(p1).activePlayerId === "P2", "P2 main phase");
  assertEvent(p1, "TURN_PLAYER_ADVANCED", (event) => event.payload?.turnPlayerId === "P2");
  logStep(15, "Server resolves the first-turn battlefield score in the same continuous game.");
  assertEvent(p1, "BATTLEFIELD_TRIGGER_RESOLVED", (event) => event.payload?.trigger === "BATTLEFIELD_FIRST_TURN_GAIN_SCORE");
  assertEvent(p1, "SCORE_GAINED", (event) => event.payload?.playerId === "P2" && event.payload?.score === 1);
  assertEqual(snapshot(p1).players.P2.score, 1, "Expected P2 score to be 1 after battlefield score.");

  logStep(16, "Opponent browser reconnects and displays the scored state.");
  await reloadAndReconnect(p2Tab);
  await waitForText(p2Tab.cdp, ["正式桌面状态", "P2", "1/8"]);
  await expectAbsentText(p2Tab.cdp, hiddenDebugTexts);

  logStep(17, "Opponent surrenders through the server command path.");
  await submit(p2, { cmdType: "SURRENDER" }, "surrender-p2");
  await waitFor(() => snapshot(p1).timing?.roomStatus === "FINISHED", "finished room status");
  assertEvent(p1, "MATCH_WON", (event) => event.payload?.winnerPlayerId === "P1" && event.payload?.reason === "SURRENDER");
  logStep(18, "Result page reflects the authoritative winner.");
  await waitForText(p1Tab.cdp, ["结算", "胜者", "P1"]);
  await waitForText(p2Tab.cdp, ["结算", "胜者", "P1"]);
}

async function createFormalRoomDriver() {
  for (let attempt = 0; attempt < 60; attempt++) {
    const roomId = `formal-18-${Date.now()}-${attempt}`;
    const clients = {
      P1: createSignalRClient("P1", roomId),
      P2: createSignalRClient("P2", roomId)
    };
    await Promise.all([clients.P1.connection.start(), clients.P2.connection.start()]);
    await invokeHub(clients.P1, "JoinRoom", roomId, "P1", null);
    await invokeHub(clients.P2, "JoinRoom", roomId, "P2", null);
    await invokeHub(clients.P1, "SubmitIntent", roomId, "P1", intentId("submit-deck-p1"), formalDeck);
    await invokeHub(clients.P2, "SubmitIntent", roomId, "P2", intentId("submit-deck-p2"), formalDeck);
    await invokeHub(clients.P1, "Ready", roomId, "P1", intentId("ready-p1"));
    await invokeHub(clients.P2, "Ready", roomId, "P2", intentId("ready-p2"));
    await waitFor(() => phase(clients.P1) === "MULLIGAN", `mulligan for ${roomId}`);

    const p1Snapshot = snapshot(clients.P1);
    const activePlayerId = p1Snapshot.activePlayerId;
    const p1Battlefield = playerBattlefieldCardNo(p1Snapshot, "P1");
    const p2Battlefield = playerBattlefieldCardNo(p1Snapshot, "P2");
    if (activePlayerId === "P1" && p1Battlefield !== "OGN·290/298" && p2Battlefield === "OGN·290/298") {
      console.log(`Formal room selected: ${roomId} active=P1 P1 battlefield=${p1Battlefield} P2 battlefield=${p2Battlefield}`);
      return {
        roomId,
        clients,
        sessions: {
          P1: clients.P1.state.joined,
          P2: clients.P2.state.joined
        },
        close: async () => {
          await Promise.all([clients.P1.connection.stop(), clients.P2.connection.stop()]);
        }
      };
    }

    await Promise.all([clients.P1.connection.stop(), clients.P2.connection.stop()]);
  }

  throw new Error("Could not find deterministic formal room with active P1 and P2 OGN·290/298 battlefield.");
}

function createSignalRClient(playerId, roomId) {
  const state = {
    playerId,
    roomId,
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

async function openPlayerChrome(playerId, reconnectToken, debugPort, browserErrors) {
  const userDataDir = await mkdtemp(path.join(tmpdir(), `riftbound-formal-${playerId.toLowerCase()}-`));
  tempDirs.push(userDataDir);
  const chrome = spawnChild(chromePath(), [
    "--headless=new",
    "--disable-gpu",
    "--no-first-run",
    "--no-default-browser-check",
    `--remote-debugging-port=${debugPort}`,
    `--user-data-dir=${userDataDir}`,
    "about:blank"
  ], { name: `chrome-${playerId}` });
  children.push(chrome);
  await waitForHttp(`http://127.0.0.1:${debugPort}/json/version`, 15_000);

  const tab = await openChromeTab(`${frontendUrl}/`, debugPort);
  const cdp = await connectCdp(tab.webSocketDebuggerUrl);
  await cdp.send("Page.enable");
  await cdp.send("Runtime.enable");
  await cdp.send("Log.enable");
  watchBrowserErrors(cdp, playerId, browserErrors);
  await waitForText(cdp, ["符文战场"]);
  await setLocalStorage(cdp, {
    "riftbound.serverUrl": serverUrl,
    "riftbound.playerId": playerId,
    "riftbound.animationLevel": "off",
    "riftbound.logDensity": "detailed",
    [`riftbound.session.${roomDriver.roomId}.${playerId}`]: JSON.stringify({
      playerId,
      seat: playerId,
      reconnectToken
    })
  });
  await cdp.send("Page.navigate", { url: `${frontendUrl}/matches/${roomDriver.roomId}` });
  await waitForText(cdp, ["对战状态", "连接/重连", roomDriver.roomId]);
  return { playerId, cdp };
}

async function connectPlayerTab(tab) {
  await clickButton(tab.cdp, "连接/重连");
  await waitForText(tab.cdp, ["正式桌面状态", "服务端行动提示", tab.playerId]);
}

async function reloadAndReconnect(tab) {
  await cdpNavigate(tab.cdp, `${frontendUrl}/matches/${roomDriver.roomId}`);
  await waitForText(tab.cdp, ["连接/重连", roomDriver.roomId]);
  await clickButton(tab.cdp, "连接/重连");
  await waitForText(tab.cdp, ["正式桌面状态", "服务端行动提示", tab.playerId]);
}

async function tapRunesForMana(client, targetMana) {
  for (let index = 0; index < 6; index++) {
    const currentMana = snapshot(client).players[client.playerId].runePool?.mana ?? 0;
    if (currentMana >= targetMana) {
      return;
    }

    const tap = enabledCandidate(client, "TAP_RUNE");
    if (!tap?.sources?.[0]) {
      throw new Error(`No TAP_RUNE candidate while ${client.playerId} has ${currentMana} mana.`);
    }

    await submit(client, { cmdType: "TAP_RUNE", sourceObjectId: tap.sources[0].id }, `tap-rune-${index}`);
  }

  throw new Error(`Could not reach ${targetMana} mana for ${client.playerId}.`);
}

async function playFirstUnit(client) {
  const play = enabledCandidate(client, "PLAY_CARD");
  if (!play) {
    throw new Error("No PLAY_CARD candidate for formal E2E.");
  }

  const source = (play.sources ?? [])
    .map((choice) => cardObject(snapshot(client), choice.id))
    .find((object) => object?.tags?.includes("CARD_TYPE:UNIT"));
  if (!source?.objectId || !source.cardNo) {
    throw new Error("No playable unit source in PLAY_CARD candidate.");
  }

  const requirement = sourceRequirement(play, source.objectId);
  await submit(client, {
    cmdType: "PLAY_CARD",
    sourceObjectId: source.objectId,
    cardNo: source.cardNo,
    targetObjectIds: [],
    destination: destinationId(requirement, "BASE"),
    optionalCosts: []
  }, "play-unit");
  return source.objectId;
}

async function passPriorityUntilStackResolves(p1, p2) {
  for (let index = 0; index < 8; index++) {
    if ((snapshot(p1).stack?.length ?? 0) === 0 && snapshot(p1).turnState === "NEUTRAL_OPEN") {
      return;
    }

    const priorityClient = [p1, p2].find((client) => enabledCandidate(client, "PASS_PRIORITY") || enabledCandidate(client, "PASS"));
    if (!priorityClient) {
      throw new Error("No priority pass candidate while stack is unresolved.");
    }

    const cmdType = enabledCandidate(priorityClient, "PASS_PRIORITY") ? "PASS_PRIORITY" : "PASS";
    await submit(priorityClient, { cmdType }, `pass-priority-${index}`);
  }

  throw new Error("Stack did not resolve after both players passed priority.");
}

async function moveUnitToOpponentBattlefield(client, opponentPlayerId) {
  const move = enabledCandidate(client, "MOVE_UNIT");
  const requirements = Array.isArray(move?.metadata?.sourceRequirements)
    ? move.metadata.sourceRequirements
    : [];
  const requirement = requirements.find((entry) => Array.isArray(entry.destinationChoices)
      && entry.destinationChoices.some((choice) => String(choice.id).includes(`${opponentPlayerId}-BATTLEFIELD`)))
    ?? requirements[0];
  const destination = requirement?.destinationChoices?.find((choice) => String(choice.id).includes(`${opponentPlayerId}-BATTLEFIELD`))
    ?? requirement?.destinationChoices?.[0];
  if (!requirement?.sourceObjectId || !destination?.id) {
    throw new Error("No MOVE_UNIT source requirement with a battlefield destination.");
  }

  await submit(client, {
    cmdType: "MOVE_UNIT",
    sourceObjectId: requirement.sourceObjectId,
    origin: requirement.origin,
    destination: destination.id,
    optionalCosts: requirement.requiredOptionalCosts ?? []
  }, "move-unit");
}

async function submit(client, command, label) {
  await invokeHub(
    client,
    "SubmitIntent",
    client.roomId,
    client.playerId,
    intentId(`${client.playerId}-${label}`),
    command);
}

async function invokeHub(client, method, ...args) {
  const errorStart = client.state.errors.length;
  await client.connection.invoke(method, ...args);
  await delay(120);
  if (client.state.errors.length > errorStart) {
    throw new Error(`${client.playerId} hub error: ${JSON.stringify(client.state.errors.slice(errorStart))}`);
  }
}

function assertJoined(client, playerId) {
  assertEqual(client.state.joined?.playerId, playerId, `${playerId} did not receive Joined.`);
}

function assertEvent(client, kind, predicate = () => true) {
  const event = client.state.events.find((candidate) => candidate.kind === kind && predicate(candidate));
  if (!event) {
    throw new Error(`Missing event ${kind}. Recent events: ${client.state.events.map((candidate) => candidate.kind).slice(-20).join(", ")}`);
  }
}

function assertOpponentHandRedacted(playerSnapshot, opponentPlayerId) {
  const zones = playerSnapshot.players[opponentPlayerId]?.zones ?? {};
  if (Array.isArray(zones.hand) && zones.hand.length > 0) {
    throw new Error(`Opponent hand leaked in snapshot for ${opponentPlayerId}.`);
  }

  if (typeof zones.handHidden !== "number") {
    throw new Error(`Opponent hand count was not redacted as handHidden for ${opponentPlayerId}.`);
  }
}

function enabledCandidate(client, action) {
  return (client.state.prompt?.candidates ?? [])
    .find((candidate) => candidate.action === action && candidate.enabled);
}

function sourceRequirement(candidate, sourceObjectId) {
  const requirements = Array.isArray(candidate.metadata?.sourceRequirements)
    ? candidate.metadata.sourceRequirements
    : [];
  return requirements.find((requirement) => requirement.sourceObjectId === sourceObjectId);
}

function destinationId(requirement, fallback) {
  return requirement?.destinationChoices?.find((choice) => choice.id === fallback)?.id
    ?? requirement?.destinationChoices?.[0]?.id
    ?? fallback;
}

function cardObject(playerSnapshot, objectId) {
  for (const player of Object.values(playerSnapshot.players)) {
    const object = player.objects?.[objectId];
    if (object) {
      return object;
    }
  }
  return undefined;
}

function playerBattlefieldCardNo(playerSnapshot, playerId) {
  const battlefieldObjectId = playerSnapshot.players[playerId]?.zones?.battlefields?.[0];
  return battlefieldObjectId ? cardObject(playerSnapshot, battlefieldObjectId)?.cardNo : undefined;
}

function snapshot(client) {
  if (!client.state.snapshot) {
    throw new Error(`No snapshot for ${client.playerId}.`);
  }
  return client.state.snapshot;
}

function phase(client) {
  return client.state.snapshot?.timing?.phase;
}

function logStep(step, text) {
  console.log(`Step ${step}/18 OK: ${text}`);
}

function repeat(values, count) {
  return values.flatMap((value) => Array.from({ length: count }, () => value));
}

function intentId(label) {
  return `${label}-${Date.now()}-${Math.random().toString(16).slice(2)}`;
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
    throw new Error("Google Chrome was not found. Set CHROME_PATH to run formal E2E.");
  }

  return found;
}

async function openChromeTab(url, debugPort) {
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

function watchBrowserErrors(cdp, label, browserErrors) {
  cdp.onEvent((message) => {
    if (message.method === "Runtime.exceptionThrown") {
      browserErrors.push(`${label} exception: ${message.params?.exceptionDetails?.text ?? "unknown"}`);
    }

    if (message.method === "Runtime.consoleAPICalled" && message.params?.type === "error") {
      const text = consoleArgs(message.params.args);
      browserErrors.push(`${label} console.error: ${text}`);
    }

    if (message.method === "Log.entryAdded" && message.params?.entry?.level === "error") {
      const text = String(message.params.entry.text ?? "");
      if (!isIgnorableResourceLog(text)) {
        browserErrors.push(`${label} log.error: ${text}`);
      }
    }
  });
}

async function setLocalStorage(cdp, values) {
  const entries = Object.entries(values);
  await cdp.send("Runtime.evaluate", {
    expression: `(() => {
      const entries = ${JSON.stringify(entries)};
      for (const [key, value] of entries) {
        localStorage.setItem(key, value);
      }
    })()`
  });
}

async function cdpNavigate(cdp, url) {
  await cdp.send("Page.navigate", { url });
}

async function clickButton(cdp, text) {
  const result = await cdp.send("Runtime.evaluate", {
    expression: `(() => {
      const button = Array.from(document.querySelectorAll("button"))
        .find((candidate) => candidate.innerText.includes(${JSON.stringify(text)}));
      if (!button) {
        return false;
      }
      button.click();
      return true;
    })()`,
    returnByValue: true
  });

  if (result.result?.value !== true) {
    const bodyText = await readBodyText(cdp);
    throw new Error(`Could not click button containing ${text}:\n${bodyText.slice(0, 1000)}`);
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

  throw new Error(`Missing expected text ${texts.join(", ")} in page body:\n${bodyText.slice(0, 1600)}`);
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

async function waitFor(fn, label, timeoutMs = 8_000) {
  const deadline = Date.now() + timeoutMs;
  while (Date.now() < deadline) {
    const value = fn();
    if (value) {
      return value;
    }
    await delay(50);
  }
  throw new Error(`Timed out waiting for ${label}.`);
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
  return text.includes("Failed to load resource: the server responded with a status of 404");
}

function delay(ms) {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

function assertEqual(actual, expected, message) {
  if (actual !== expected) {
    throw new Error(`${message} Expected ${expected}, got ${actual}.`);
  }
}

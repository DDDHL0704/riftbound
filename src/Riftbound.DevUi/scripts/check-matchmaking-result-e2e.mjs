import assert from "node:assert/strict";
import { createRequire } from "node:module";
import * as signalR from "@microsoft/signalr";

const require = createRequire(import.meta.url);
const WebSocketCtor = globalThis.WebSocket ?? require("ws");
if (!globalThis.WebSocket) {
  globalThis.WebSocket = WebSocketCtor;
}

const serverUrl = process.env.RIFTBOUND_SERVER_URL ?? "http://127.0.0.1:5088";
const hubUrl = `${serverUrl}/hubs/game`;
const runId = Date.now().toString(36);
const players = [
  { playerId: `b4alice${runId}`, key: `alice-b4-key-${runId}-1234567890` },
  { playerId: `b4bob${runId}`, key: `bob-b4-key-${runId}-1234567890` }
];

const clients = players.map(createClient);

try {
  await expectHttpOk(`${serverUrl}/health`, "API health");
  const decks = await loadPreconstructedDecks();
  assert.ok(decks.length >= 2, `expected at least 2 preconstructed decks, found ${decks.length}`);

  await Promise.all(clients.map((client) => client.connection.start()));
  await Promise.all(clients.map(async (client) => {
    const auth = await invokeHub(client, "Authenticate", client.playerId, client.key);
    assert.equal(auth.authenticated, true, `${client.playerId} did not authenticate`);
  }));

  const queued = await invokeHub(clients[0], "EnqueueMatchmaking", clients[0].playerId);
  assert.equal(queued.state, "QUEUED");

  const matchedReturn = await invokeHub(clients[1], "EnqueueMatchmaking", clients[1].playerId);
  assert.equal(matchedReturn.state, "MATCHED");
  assert.ok(matchedReturn.roomId, "second player did not receive a room id");

  const firstMatched = await waitFor(
    () => latestMatchmaking(clients[0], "MATCHED"),
    `${clients[0].playerId} matched`);
  const secondMatched = latestMatchmaking(clients[1], "MATCHED") ?? matchedReturn;
  assert.equal(firstMatched.roomId, secondMatched.roomId);
  assert.equal(firstMatched.roomId, matchedReturn.roomId);
  assert.notEqual(firstMatched.playerSession?.reconnectToken, secondMatched.playerSession?.reconnectToken);

  const roomId = matchedReturn.roomId;
  await reconnectMatchedClient(clients[0], roomId, firstMatched.playerSession);
  await reconnectMatchedClient(clients[1], roomId, secondMatched.playerSession);

  await submit(clients[0], toSubmitDeckCommand(decks[0]), "submit-deck-p1");
  await submit(clients[1], toSubmitDeckCommand(decks[1]), "submit-deck-p2");
  await invokeHub(clients[0], "Ready", roomId, clients[0].playerId, intentId("ready-p1"));
  await invokeHub(clients[1], "Ready", roomId, clients[1].playerId, intentId("ready-p2"));
  await waitFor(() => phase(clients[0]) === "MULLIGAN" && phase(clients[1]) === "MULLIGAN", "mulligan phase");

  await submit(clients[0], { cmdType: "MULLIGAN", handObjectIds: [] }, "mulligan-p1");
  await submit(clients[1], { cmdType: "MULLIGAN", handObjectIds: [] }, "mulligan-p2");
  await waitFor(() => phase(clients[0]) === "MAIN" && phase(clients[1]) === "MAIN", "main phase");
  assertOpponentHandRedacted(clients[0], clients[1].playerId);
  assertOpponentHandRedacted(clients[1], clients[0].playerId);

  await submit(clients[1], { cmdType: "SURRENDER" }, "surrender-p2");
  await waitFor(() => roomStatus(clients[0]) === "FINISHED", "finished room status");
  const winEvent = clients[0].state.events.find((event) => event.kind === "MATCH_WON");
  assert.ok(winEvent, "expected MATCH_WON event");
  assert.equal(winEvent.payload?.winnerPlayerId, clients[0].playerId);
  assert.equal(winEvent.payload?.reason, "SURRENDER");
  assertNoReconnectTokenLeak(clients[0]);
  assertNoReconnectTokenLeak(clients[1]);

  console.log(`Matchmaking result E2E passed: room=${roomId} winner=${winEvent.payload.winnerPlayerId}`);
} finally {
  await Promise.all(clients.map((client) => client.connection.stop().catch(() => undefined)));
}

function createClient(player) {
  const state = {
    events: [],
    errors: [],
    joined: undefined,
    matchmaking: [],
    prompt: undefined,
    snapshot: undefined
  };
  const connection = new signalR.HubConnectionBuilder()
    .withUrl(hubUrl, { WebSocket: WebSocketCtor })
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
  connection.on("Matchmaking", (message) => {
    state.matchmaking.push(message.payload);
  });
  connection.on("Error", (message) => {
    state.errors.push(message.payload);
  });

  return { ...player, roomId: undefined, connection, state };
}

async function loadPreconstructedDecks() {
  const response = await fetch(`${serverUrl}/decks/preconstructed`);
  if (!response.ok) {
    throw new Error(`Could not load preconstructed decks: ${response.status} ${response.statusText}`);
  }

  return await response.json();
}

function toSubmitDeckCommand(deck) {
  return {
    cmdType: "SUBMIT_DECK",
    legendCardNo: deck.legendCardNo,
    championCardNo: deck.championCardNo,
    mainDeck: deck.mainDeck,
    runeDeck: deck.runeDeck,
    battlefields: deck.battlefields
  };
}

async function reconnectMatchedClient(client, roomId, playerSession) {
  assert.ok(playerSession?.reconnectToken, `${client.playerId} missing reconnect token`);
  client.roomId = roomId;
  await invokeHub(client, "Reconnect", roomId, client.playerId, playerSession.reconnectToken);
  await waitFor(
    () => client.state.joined?.playerId === client.playerId && client.state.snapshot && client.state.prompt,
    `${client.playerId} reconnected`);
  assert.equal(client.state.joined.seat, playerSession.seat);
}

async function submit(client, command, label) {
  const receipt = await invokeHub(
    client,
    "SubmitIntent",
    client.roomId,
    client.playerId,
    intentId(`${client.playerId}-${label}`),
    command);
  assert.equal(receipt.accepted, true, `${client.playerId} ${label} was rejected: ${receipt.message}`);
}

async function invokeHub(client, method, ...args) {
  const errorStart = client.state.errors.length;
  const result = await client.connection.invoke(method, ...args);
  await delay(120);
  if (client.state.errors.length > errorStart) {
    throw new Error(`${client.playerId} hub error: ${JSON.stringify(client.state.errors.slice(errorStart))}`);
  }

  return result;
}

function latestMatchmaking(client, state) {
  return client.state.matchmaking.findLast((candidate) => candidate.state === state);
}

function phase(client) {
  return client.state.snapshot?.timing?.phase ?? client.state.snapshot?.phase;
}

function roomStatus(client) {
  return client.state.snapshot?.timing?.roomStatus ?? client.state.snapshot?.status;
}

function assertOpponentHandRedacted(client, opponentPlayerId) {
  const zones = client.state.snapshot?.players?.[opponentPlayerId]?.zones ?? {};
  assert.ok(!Array.isArray(zones.hand) || zones.hand.length === 0, `${client.playerId} can see ${opponentPlayerId} hand`);
  assert.equal(typeof zones.handHidden, "number", `${client.playerId} does not see ${opponentPlayerId} handHidden count`);
}

function assertNoReconnectTokenLeak(client) {
  const serialized = JSON.stringify(client.state.snapshot);
  assert.equal(serialized.includes("reconnectToken"), false, `${client.playerId} snapshot leaked reconnectToken`);
}

function intentId(label) {
  return `${label}-${Date.now()}-${Math.random().toString(16).slice(2)}`;
}

async function expectHttpOk(url, label) {
  const response = await fetch(url);
  if (!response.ok) {
    throw new Error(`${label} failed: ${response.status} ${response.statusText}`);
  }
}

async function waitFor(fn, label, timeoutMs = 12_000) {
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

function delay(ms) {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

import assert from "node:assert/strict";
import { HubConnectionBuilder, LogLevel } from "@microsoft/signalr";

const server = process.env.RIFTBOUND_SERVER_URL ?? "http://127.0.0.1:5088";
const run = Date.now().toString(36);
const connections = [];
async function connect(name) {
  const connection = new HubConnectionBuilder().withUrl(`${server}/hubs/game`)
    .configureLogging(LogLevel.Warning).build();
  for (const event of ["Joined", "Snapshot", "Prompt", "Events", "Error", "Matchmaking"])
    connection.on(event, () => {});
  connections.push(connection);
  await connection.start();
  assert.equal((await connection.invoke("Authenticate", name, `presence-${name}-test-key`)).authenticated, true);
  return connection;
}
async function rooms() {
  const response = await fetch(`${server}/matches`);
  assert.equal(response.status, 200);
  return response.json();
}
async function until(predicate, label) {
  const deadline = Date.now() + 5000;
  do {
    if (await predicate()) return;
    await new Promise(resolve => setTimeout(resolve, 50));
  } while (Date.now() < deadline);
  assert.fail(label);
}
try {
  const alice = `presence-a-${run}`;
  const bob = `presence-b-${run}`;
  const a = await connect(alice);
  const b = await connect(bob);
  const initial = await a.invoke("CreatePublicMatch", alice);
  assert.equal((await a.invoke("EnqueueMatchmaking", alice)).state, "QUEUED");
  await a.stop();
  // Directory visibility acknowledges that the server processed the disconnect.
  await until(async () => !(await rooms()).some(room => room.roomId === initial.match.roomId), "disconnect not processed");
  assert.equal((await b.invoke("EnqueueMatchmaking", bob)).state, "QUEUED", "offline player was paired");
  await b.invoke("CancelMatchmaking", bob);

  const host = await connect(alice);
  const created = await host.invoke("CreatePublicMatch", alice);
  const roomId = created.match.roomId;
  await until(async () => (await rooms()).some(room => room.roomId === roomId), "online room missing");
  await host.stop();
  await until(async () => !(await rooms()).some(room => room.roomId === roomId), "offline room remained listed");
  const reconnected = await connect(alice);
  assert.ok(!(await rooms()).some(room => room.roomId === roomId), "authentication alone restored old room presence");
  await reconnected.invoke("Reconnect", roomId, alice, created.playerSession.reconnectToken);
  await until(async () => (await rooms()).some(room => room.roomId === roomId), "rejoined room missing");
  const replacement = await reconnected.invoke("CreatePublicMatch", alice);
  const visible = await rooms();
  assert.ok(!visible.some(room => room.roomId === roomId));
  assert.ok(visible.some(room => room.roomId === replacement.match.roomId));
  console.log("Lobby presence E2E passed: disconnect, reconnect, and host room switch.");
} finally {
  await Promise.all(connections.map(connection => connection.stop()));
}

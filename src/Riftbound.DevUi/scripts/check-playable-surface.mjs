import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const mainSource = read("src/main.tsx");
const shellSource = read("src/components/layout/AppShell.tsx");
const lobbySource = read("src/pages/LobbyPage.tsx");
const roomSource = read("src/pages/RoomPage.tsx");
const matchSource = read("src/pages/MatchPage.tsx");
const playableMatchSource = readIfPresent("src/components/match/PlayableMatchSurface.tsx");
const cardDetailSource = read("src/components/cards/CardDetailDrawer.tsx");
const resultSource = read("src/pages/ResultPage.tsx");
const errors = [];

requireText(mainSource, 'import "./styles/game-client.css";', "main.tsx must import game-client.css");
const globalsIndex = mainSource.indexOf('import "./styles/globals.css";');
const gameClientIndex = mainSource.indexOf('import "./styles/game-client.css";');
if (gameClientIndex < globalsIndex || globalsIndex < 0) {
  errors.push("game-client.css must load after globals.css");
}

requireText(shellSource, "data-game-shell", "AppShell must expose the game shell marker");
requireText(shellSource, 'className="game-primary-nav"', "AppShell must expose a primary play navigation");
requireText(shellSource, 'className="game-secondary-nav"', "secondary tools must live outside the primary navigation");
for (const label of ["对战大厅", "卡牌图鉴", "我的卡组", "设置"]) {
  requireText(shellSource, `label="${label}"`, `primary navigation is missing ${label}`);
}

requireText(lobbySource, "data-play-lobby", "LobbyPage must expose the play-first lobby marker");
requireText(lobbySource, 'className="lobby-server-settings"', "server settings must be secondary in the lobby");
requireText(roomSource, "data-play-room", "RoomPage must expose the play-first room marker");
requireText(roomSource, "data-room-primary-actions", "room setup actions must be visible in the primary surface");
requireText(roomSource, 'className="room-diagnostics"', "room protocol diagnostics must be collapsible");

const diagnosticsIndex = roomSource.indexOf('className="room-diagnostics"');
for (const technicalSurface of ["<RoomWorkflowSurface", "<RoomSubmissionReceipt", 'className="room-log-panel"']) {
  const surfaceIndex = roomSource.indexOf(technicalSurface);
  if (surfaceIndex < diagnosticsIndex || diagnosticsIndex < 0) {
    errors.push(`${technicalSurface} must render inside the collapsed room diagnostics`);
  }
}

requireText(playableMatchSource, "data-playable-match-surface", "match must expose the playable surface marker");
requireText(playableMatchSource, "data-game-table", "playable match must expose the game table");
requireText(playableMatchSource, "data-game-action-dock", "playable match must expose the action dock");
requireText(playableMatchSource, "data-game-debug-drawer", "playable match must retain collapsed diagnostics");
requireText(playableMatchSource, 'className="game-debug-drawer"', "match diagnostics must use a closed details element");
requireText(matchSource, "<PlayableMatchSurface", "MatchPage must compose the new playable surface");
if (matchSource.includes("符文战场对战线框")) {
  errors.push("the playable match title must not describe itself as a wireframe");
}

requireText(cardDetailSource, "data-card-art-panel", "card detail must expose an official-art panel");
requireText(cardDetailSource, 'className="detail-diagnostics"', "card inspection evidence must be collapsible");
requireText(resultSource, "data-play-result", "ResultPage must expose the play result marker");
requireText(resultSource, 'className="result-diagnostics"', "result protocol evidence must be collapsible");

if (errors.length > 0) {
  console.error("Playable Web surface check failed:");
  for (const error of errors) {
    console.error(`- ${error}`);
  }
  process.exit(1);
}

console.log("Playable Web surface foundation is present.");

function read(relativePath) {
  return fs.readFileSync(path.join(root, relativePath), "utf8");
}

function readIfPresent(relativePath) {
  const absolutePath = path.join(root, relativePath);
  return fs.existsSync(absolutePath) ? fs.readFileSync(absolutePath, "utf8") : "";
}

function requireText(source, expected, message) {
  if (!source.includes(expected)) {
    errors.push(message);
  }
}

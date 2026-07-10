import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const mainSource = read("src/main.tsx");
const shellSource = read("src/components/layout/AppShell.tsx");
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

function requireText(source, expected, message) {
  if (!source.includes(expected)) {
    errors.push(message);
  }
}


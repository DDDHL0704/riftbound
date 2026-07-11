import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const mainSource = read("src/main.tsx");
const styles = readIfPresent("src/styles/arena-table.css");
const arenaTableSource = read("src/components/match/ArenaTable.tsx");
const surfaceSource = read("src/components/match/PlayableMatchSurface.tsx");
const matchSource = read("src/pages/MatchPage.tsx");
const errors = [];

requireText(mainSource, 'import "./styles/arena-table.css";', "arena CSS must load last");
const gameClientIndex = mainSource.indexOf('import "./styles/game-client.css";');
const arenaIndex = mainSource.indexOf('import "./styles/arena-table.css";');
if (gameClientIndex < 0 || arenaIndex <= gameClientIndex) errors.push("arena CSS must load after game-client.css");
requireText(styles, "--arena-table: #111615", "arena table token is missing");
requireText(styles, "--arena-legal: #55c89b", "legal-action token is missing");
requireText(styles, "top: 33%", "battlefield region must begin after the opponent public zones");
requireText(styles, "bottom: 33%", "battlefield region must end before the self public zones");
requireText(styles, "height: 15%", "both hand rails must remain compact");
requireText(styles, "--wire-card-w: 72px", "public battlefield units must remain readable at desktop size");
requireText(styles, "--wire-card-w: 82px", "desktop legend, champion, and base cards must remain readable");
requireText(styles, "--wire-fixed-pile-card-w: 76px", "desktop deck piles must remain readable");
requireText(styles, "--wire-rune-card-w: 48px", "desktop rune cards must remain readable");
requireText(styles, "grid-template-columns: repeat(6, var(--wire-card-w))", "base units must occupy six independent visual slots");
requireText(styles, ".card-image-cost, .card-image-power, .card-image-title", "official card thumbnails must hide duplicate overlay labels");
requireText(styles, "--arena-hand-clearance: 184px", "the resting fan must reserve mirrored space for piles");
requireText(styles, ".wire-object-command-tray.presentation-arena", "the selected-card tray must use compact arena styling");
requireText(styles, ":has(> .wire-object-command-tray.presentation-arena)", "selected-card actions must replace the generic prompt chooser");
requireText(styles, ".arena-prompt-layer.is-context:has(> .wire-object-command-tray.presentation-arena) {", "selected-card actions must stay above the hand instead of covering public targets");
requireText(styles, ".wire-object-command-tray.presentation-arena .candidate-composer-field", "arena command fields must have dedicated compact styling");
requireText(styles, '.wire-object-route-review[data-wire-object-route-review-presentation="arena"]', "direct-selection submission must share the arena theme");
requireText(styles, "color-scheme: dark", "arena form controls must use the dark table color scheme");
requireText(styles, ".candidate-composer-check input", "arena cost choices must override legacy full-width checkboxes");
requireText(styles, "width: 16px", "arena cost checkboxes must retain a stable compact size");
requireText(styles, "@media (max-width: 899px)", "mobile must have an independent layout");
requireText(styles, "--wire-battlefield-card-w: 84px", "mobile battlefield sites must fit beside the active lane");
requireText(styles, "--wire-card-w: 60px", "mobile public units must use an independent readable size");
requireText(styles, "scroll-snap-align: start", "mobile lanes must snap with their outer battlefield site visible");
requireText(styles, "scroll-margin-left: 86px", "the first mobile lane must preserve the left battlefield site");
requireText(styles, ".arena-battlefield-tabs", "mobile must expose a battlefield lane switcher");
requireText(styles, "scrollbar-width: none", "mobile battlefield scrolling must not show a permanent scrollbar");
requireText(styles, "grid-template-columns: minmax(0, 1fr) auto", "mobile object actions must stack the command form below the object summary");
requireText(styles, ".arena-side-drawer", "diagnostics must use a right drawer");
requireText(styles, ".arena-prompt-layer.is-modal .pay-cost-panel", "complex payment prompts must use the arena theme");
requireText(arenaTableSource, "tabIndex={0}", "the scrollable battlefield must be keyboard focusable");
requireText(arenaTableSource, "data-arena-battlefield-lane-control", "mobile lane controls must expose a stable interaction contract");
rejectText(styles, "font-size: clamp(", "font size must not scale with viewport width");
requireText(surfaceSource, "arena-side-drawer", "playable surface must expose the diagnostics drawer");
requireText(matchSource, "visibleArenaBackdrop", "backdrops must resolve from visible public cards only");
requireText(matchSource, "data-wire-banish-zone", "banish must remain visually separate from the base");

if (errors.length > 0) {
  console.error("Arena visual contract check failed:");
  for (const error of errors) console.error(`- ${error}`);
  process.exit(1);
}

console.log("Arena visual contract check passed.");

function read(relativePath) {
  return fs.readFileSync(path.join(root, relativePath), "utf8");
}

function readIfPresent(relativePath) {
  const absolutePath = path.join(root, relativePath);
  return fs.existsSync(absolutePath) ? fs.readFileSync(absolutePath, "utf8") : "";
}

function requireText(source, expected, message) {
  if (!source.includes(expected)) errors.push(message);
}

function rejectText(source, rejected, message) {
  if (source.includes(rejected)) errors.push(message);
}

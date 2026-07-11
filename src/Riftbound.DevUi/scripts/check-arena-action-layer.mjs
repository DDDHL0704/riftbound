import assert from "node:assert/strict";
import { existsSync, readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import ts from "typescript";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const srcRoot = resolve(scriptDir, "../src");
const planPath = resolve(srcRoot, "utils/arenaPromptPresentation.ts");
const layerPath = resolve(srcRoot, "components/match/ArenaActionLayer.tsx");
const actionPanelPath = resolve(srcRoot, "components/match/ActionPanel.tsx");
const objectTrayPath = resolve(srcRoot, "components/match/WireObjectCommandTray.tsx");
const routeReviewPath = resolve(srcRoot, "components/match/WireObjectRouteReview.tsx");
const matchPagePath = resolve(srcRoot, "pages/MatchPage.tsx");
const surfacePath = resolve(srcRoot, "components/match/PlayableMatchSurface.tsx");

assert(existsSync(planPath), "arena prompt presentation plan must exist");
assert(existsSync(layerPath), "arena action layer component must exist");

const { buildArenaPromptPresentation } = loadTsModule(planPath).exports;
assert.deepEqual(
  buildArenaPromptPresentation({ actionable: false, promptType: "MAIN_ACTION", selectedObjectId: "unit-1" }),
  { mode: "context", anchorObjectId: "unit-1" }
);
assert.deepEqual(
  buildArenaPromptPresentation({ actionable: false, promptType: "MAIN_ACTION" }),
  { mode: "hidden", anchorObjectId: undefined }
);
assert.deepEqual(
  buildArenaPromptPresentation({ actionable: true, promptType: "MAIN_ACTION", selectedObjectId: "unit-1" }),
  { mode: "context", anchorObjectId: "unit-1" }
);
for (const promptType of ["MULLIGAN", "ASSIGN_COMBAT_DAMAGE", "ORDER_TRIGGERS", "PAY_COST", "HAND_CHOICE"]) {
  assert.deepEqual(
    buildArenaPromptPresentation({ actionable: true, promptType, selectedObjectId: "unit-1" }),
    { mode: "modal", anchorObjectId: undefined },
    `${promptType} must use the dedicated modal presentation`
  );
}

const layerSource = readFileSync(layerPath, "utf8");
const actionPanelSource = readFileSync(actionPanelPath, "utf8");
const objectTraySource = readFileSync(objectTrayPath, "utf8");
const routeReviewSource = readFileSync(routeReviewPath, "utf8");
const matchPageSource = readFileSync(matchPagePath, "utf8");
const surfaceSource = readFileSync(surfacePath, "utf8");
assert(layerSource.includes("data-arena-action-mode"), "action layer must expose its presentation mode");
assert(layerSource.includes("data-object-id"), "context mode must anchor from the selected card DOM");
assert(layerSource.includes("--arena-action-translate-y"), "context mode must open above lower-half cards and below upper-half cards");
assert(layerSource.includes("const panelWidth = Math.min(600"), "desktop context actions must clamp their horizontal anchor inside the table");
assert(layerSource.includes("objectOnRight ? minimumX : maximumX"), "desktop context actions must dock opposite the selected battlefield card");
assert(actionPanelSource.includes('presentation?: "diagnostic" | "play" | "arena"'), "action panel must expose the compact arena presentation");
assert(actionPanelSource.includes("data-arena-action-choices"), "arena presentation must expose progressive action choices");
assert(actionPanelSource.includes("aria-expanded"), "arena action choices must announce their expanded state");
assert(objectTraySource.includes('presentation?: "diagnostic" | "arena"'), "object tray must expose a compact arena presentation");
assert(objectTraySource.includes("data-wire-object-command-tray-presentation"), "object tray must identify its presentation in the DOM");
assert(routeReviewSource.includes('presentation?: "diagnostic" | "arena"'), "direct-selection route review must retain a compact arena submission surface");
assert(objectTraySource.includes("presentation={presentation}"), "object tray must preserve route review in arena presentation");
assert(matchPageSource.includes('presentation="arena"'), "the match arena must use the compact action presentation");
assert.equal(matchPageSource.match(/presentation="arena"/g)?.length, 2, "the match arena must compact both the object tray and action panel");
assert(matchPageSource.includes('arenaPromptPresentation.mode === "modal"'), "the global action panel must stay hidden for direct-selection prompts");
assert(matchPageSource.includes('event.key !== "Escape"'), "Escape must clear the current client-side table selection draft");
assert(!surfaceSource.includes("game-action-dock"), "legacy action dock must not return");

console.log("Arena action-layer check passed.");

function loadTsModule(filename) {
  const source = readFileSync(filename, "utf8");
  const output = ts.transpileModule(source, {
    compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 }
  }).outputText;
  const module = { exports: {} };
  new Function("exports", "module", "require", output)(module.exports, module, () => ({}));
  return module;
}

import assert from "node:assert/strict";
import { existsSync, readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import ts from "typescript";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const srcRoot = resolve(scriptDir, "../src");
const planPath = resolve(srcRoot, "utils/arenaPromptPresentation.ts");
const placementPath = resolve(srcRoot, "utils/arenaActionPlacement.ts");
const layerPath = resolve(srcRoot, "components/match/ArenaActionLayer.tsx");
const actionPanelPath = resolve(srcRoot, "components/match/ActionPanel.tsx");
const objectTrayPath = resolve(srcRoot, "components/match/WireObjectCommandTray.tsx");
const routeReviewPath = resolve(srcRoot, "components/match/WireObjectRouteReview.tsx");
const cardPreviewPath = resolve(srcRoot, "components/match/WireCardPreview.tsx");
const matchPagePath = resolve(srcRoot, "pages/MatchPage.tsx");
const surfacePath = resolve(srcRoot, "components/match/PlayableMatchSurface.tsx");

assert(existsSync(planPath), "arena prompt presentation plan must exist");
assert(existsSync(placementPath), "arena action placement scorer must exist");
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
const { chooseArenaActionPlacement } = loadTsModule(placementPath).exports;
const actionPanelSource = readFileSync(actionPanelPath, "utf8");
const objectTraySource = readFileSync(objectTrayPath, "utf8");
const routeReviewSource = readFileSync(routeReviewPath, "utf8");
const cardPreviewSource = readFileSync(cardPreviewPath, "utf8");
const matchPageSource = readFileSync(matchPagePath, "utf8");
const surfaceSource = readFileSync(surfacePath, "utf8");
assert(layerSource.includes("data-arena-action-mode"), "action layer must expose its presentation mode");
assert(layerSource.includes("data-object-id"), "context mode must anchor from the selected card DOM");
assert(layerSource.includes("protectedObjectIds"), "context mode must receive server-derived legal objects to protect");
assert(layerSource.includes("chooseArenaActionPlacement"), "context mode must score placements instead of relying on source direction alone");
assert(layerSource.includes('document.addEventListener("scroll", scheduleSync, true)'), "context mode must track nested scrolling");
assert(layerSource.includes("ResizeObserver"), "context mode must track source, target, panel, and host size changes");
assert(layerSource.includes("data-arena-action-anchor-x"), "context mode must expose anchor movement for scroll regression checks");
assert(layerSource.includes("data-arena-action-protected-overlap"), "context mode must expose target overlap for browser regression checks");
assert(actionPanelSource.includes('presentation?: "diagnostic" | "play" | "arena"'), "action panel must expose the compact arena presentation");
assert(actionPanelSource.includes("data-arena-action-choices"), "arena presentation must expose progressive action choices");
assert(actionPanelSource.includes("aria-expanded"), "arena action choices must announce their expanded state");
assert(objectTraySource.includes('presentation?: "diagnostic" | "arena"'), "object tray must expose a compact arena presentation");
assert(objectTraySource.includes("data-wire-object-command-tray-presentation"), "object tray must identify its presentation in the DOM");
assert(routeReviewSource.includes('presentation?: "diagnostic" | "arena"'), "direct-selection route review must retain a compact arena submission surface");
assert(objectTraySource.includes("presentation={presentation}"), "object tray must preserve route review in arena presentation");
assert(objectTraySource.includes('const routeOnly = presentation === "arena"'), "arena tray must choose one submission path after a tabletop route exists");
assert(routeReviewSource.includes('presentation === "arena" ? "确认行动"'), "arena route and composer must use one confirmation label");
assert(cardPreviewSource.includes("clearPreviewCard"), "card preview hook must expose an immediate cancellation path");
assert(matchPageSource.includes("selectedObjectId ? undefined : previewCard"), "selected-card actions must suppress the fixed card preview");
assert(matchPageSource.includes('presentation="arena"'), "the match arena must use the compact action presentation");
assert.equal(matchPageSource.match(/presentation="arena"/g)?.length, 2, "the match arena must compact both the object tray and action panel");
assert(matchPageSource.includes('arenaPromptPresentation.mode === "modal"'), "the global action panel must stay hidden for direct-selection prompts");
assert(matchPageSource.includes('event.key !== "Escape"'), "Escape must clear the current client-side table selection draft");
assert(!surfaceSource.includes("game-action-dock"), "legacy action dock must not return");

const placement = chooseArenaActionPlacement({
  anchor: rect(760, 560, 80, 112),
  host: rect(0, 0, 1000, 700),
  panelHeight: 220,
  panelWidth: 420,
  protectedRects: [
    rect(20, 330, 150, 110),
    rect(210, 350, 80, 110),
    rect(420, 470, 80, 110),
    rect(700, 350, 80, 110)
  ]
});
assert.equal(placement.protectedOverlapArea, 0, "placement scorer must preserve a clear legal-target route when one exists");
assert(placement.left >= 12 && placement.top >= 12, "placement scorer must keep the panel inside the arena host");

console.log("Arena action-layer check passed.");

function rect(left, top, width, height) {
  return { bottom: top + height, height, left, right: left + width, top, width };
}

function loadTsModule(filename) {
  const source = readFileSync(filename, "utf8");
  const output = ts.transpileModule(source, {
    compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 }
  }).outputText;
  const module = { exports: {} };
  new Function("exports", "module", "require", output)(module.exports, module, () => ({}));
  return module;
}

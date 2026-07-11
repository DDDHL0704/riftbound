import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const layout = JSON.parse(fs.readFileSync(path.join(root, "src/components/match/wireTableLayoutData.json"), "utf8"));
const arena = layout.arena;

assert(arena, "wireTableLayoutData.json must define arena");
assert(arena.battlefieldMinHeightRatio >= 0.4, "balanced battlefield ratio must be at least 0.4");
assert(arena.battlefieldMinHeightRatio <= 0.44, "battlefield must leave enough room for base and resource zones");
assert(arena.handMaxViewportRatio <= 0.18, "hand ratio must be at most 0.18");
assert.deepEqual(
  arena.battlefieldSlots,
  ["leftSite", "leftLane", "rightLane", "rightSite"],
  "battlefield sites must anchor the two outer edges"
);
assert.equal(arena.desktopMinWidth, 1280, "desktop arena must cover the 1280px minimum");
assert.equal(arena.compactDesktopMinWidth, 900, "mobile layout must begin below 900px");
assert.equal(arena.self.runes, "bottomLeft", "local runes must occupy the lower-left viewpoint corner");
assert.equal(arena.self.home, "bottomCenter", "local public identity cards must occupy the lower center edge");
assert.equal(arena.self.mainDeck, "bottomRight", "local main deck must occupy the lower-right viewpoint corner");
assert.equal(arena.self.hand, "bottomFan", "local hand must use the bottom fan");
assert.equal(arena.opponent.runes, "topLeft", "opponent runes must mirror the local resource cluster");
assert.equal(arena.opponent.home, "topCenter", "opponent public identity cards must occupy the top center edge");
assert.equal(arena.opponent.mainDeck, "topRight", "opponent main deck must mirror the local pile cluster");
assert.equal(arena.opponent.hiddenHand, "topFan", "opponent hidden hand must use the top card-back fan");

console.log("Arena table contract check passed.");

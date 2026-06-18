import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { dirname, resolve } from "node:path";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const layoutPath = resolve(scriptDir, "../src/components/match/tabletopLayoutData.json");
const layout = JSON.parse(readFileSync(layoutPath, "utf8"));

const requiredZones = ["legend", "champion", "score", "piles", "base", "runeBank", "hand"];
const sides = ["self", "opponent"];
const errors = [];

if (layout.runeDeckSize !== 12) {
  errors.push(`runeDeckSize must be 12, got ${layout.runeDeckSize}`);
}

if (!layout.players || typeof layout.players !== "object") {
  errors.push("players object is required");
}

for (const side of sides) {
  const player = layout.players?.[side];
  if (!player || typeof player !== "object") {
    errors.push(`${side} player layout is required`);
    continue;
  }

  for (const zone of requiredZones) {
    const box = player[zone];
    if (!box) {
      errors.push(`${side}.${zone} is required`);
      continue;
    }

    validateBox(`${side}.${zone}`, box);
  }

  if (requiredZones.every((zone) => player[zone])) {
    validatePlayerShape(side, player);
    validatePlayerOverlaps(side, player);
  }
}

if (!Array.isArray(layout.battlefields) || layout.battlefields.length !== 2) {
  errors.push("battlefields must contain exactly 2 public battlefield zones");
} else {
  layout.battlefields.forEach((box, index) => validateBox(`battlefields[${index}]`, box));
  validateNoOverlap("battlefields[0]", layout.battlefields[0], "battlefields[1]", layout.battlefields[1], 0.01);
}

if (errors.length > 0) {
  console.error("Tabletop layout contract failed:");
  for (const error of errors) {
    console.error(`- ${error}`);
  }
  process.exit(1);
}

console.log("Tabletop layout contract check passed.");

function validateBox(name, box) {
  for (const key of ["id", "label"]) {
    if (typeof box[key] !== "string" || box[key].trim() === "") {
      errors.push(`${name}.${key} must be a non-empty string`);
    }
  }

  for (const key of ["x", "y", "width", "height"]) {
    if (!Number.isFinite(box[key])) {
      errors.push(`${name}.${key} must be a finite number`);
    }
  }

  if (Number.isFinite(box.width) && box.width <= 0) {
    errors.push(`${name}.width must be greater than 0`);
  }

  if (Number.isFinite(box.height) && box.height <= 0) {
    errors.push(`${name}.height must be greater than 0`);
  }

  if (Number.isFinite(box.x) && box.x < 0) {
    errors.push(`${name}.x must not be negative`);
  }

  if (Number.isFinite(box.y) && box.y < 0) {
    errors.push(`${name}.y must not be negative`);
  }

  if (Number.isFinite(box.x) && Number.isFinite(box.width) && box.x + box.width > 100) {
    errors.push(`${name} exceeds container width`);
  }

  if (Number.isFinite(box.y) && Number.isFinite(box.height) && box.y + box.height > 100) {
    errors.push(`${name} exceeds container height`);
  }
}

function validatePlayerShape(side, player) {
  if (side === "self") {
    validateBefore("self.legend", player.legend, "self.champion", player.champion);
    validateBefore("self.champion", player.champion, "self.piles", player.piles);
    validateBefore("self.piles", player.piles, "self.base", player.base);
    validateBelow("self.runeBank", player.runeBank, "self.piles", player.piles);
    validateBefore("self.runeBank", player.runeBank, "self.hand", player.hand);
    return;
  }

  validateBefore("opponent.base", player.base, "opponent.piles", player.piles);
  validateBefore("opponent.piles", player.piles, "opponent.legend", player.legend);
  validateBefore("opponent.legend", player.legend, "opponent.champion", player.champion);
  validateBefore("opponent.runeBank", player.runeBank, "opponent.hand", player.hand);
  validateBefore("opponent.hand", player.hand, "opponent.score", player.score);
}

function validatePlayerOverlaps(side, player) {
  validateNoOverlap(`${side}.legend`, player.legend, `${side}.champion`, player.champion, 0.02);
  validateNoOverlap(`${side}.base`, player.base, `${side}.runeBank`, player.runeBank, 0.02);
  validateNoOverlap(`${side}.runeBank`, player.runeBank, `${side}.hand`, player.hand, 0.02);
  validateNoOverlap(`${side}.score`, player.score, `${side}.hand`, player.hand, 0.02);
}

function validateBefore(leftName, left, rightName, right) {
  if (left.x + left.width > right.x) {
    errors.push(`${leftName} should be left of ${rightName}`);
  }
}

function validateBelow(lowerName, lower, upperName, upper) {
  if (lower.y < upper.y + upper.height) {
    errors.push(`${lowerName} should be below ${upperName}`);
  }
}

function validateNoOverlap(firstName, first, secondName, second, maxRatio) {
  const overlapWidth = Math.max(0, Math.min(first.x + first.width, second.x + second.width) - Math.max(first.x, second.x));
  const overlapHeight = Math.max(0, Math.min(first.y + first.height, second.y + second.height) - Math.max(first.y, second.y));
  const overlapArea = overlapWidth * overlapHeight;
  const smallestArea = Math.min(first.width * first.height, second.width * second.height);
  const ratio = smallestArea === 0 ? 0 : overlapArea / smallestArea;

  if (ratio > maxRatio) {
    errors.push(`${firstName} overlaps ${secondName} by ${(ratio * 100).toFixed(1)}%`);
  }
}

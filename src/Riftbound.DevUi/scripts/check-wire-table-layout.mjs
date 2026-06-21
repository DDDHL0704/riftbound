import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const layoutPath = resolve(scriptDir, "../src/components/match/wireTableLayoutData.json");
const layout = JSON.parse(readFileSync(layoutPath, "utf8"));
const errors = [];

const handSlots = ["hand", "runeDeck", "runeTrack"];
const homeSlots = ["base", "hero", "legend"];
const battlefieldSlots = ["center", "leftSite", "rightSite"];
const sidePanelSlots = [
  "overview",
  "turnWindow",
  "commandCenter",
  "serverFlow",
  "responseCoach",
  "tableAuthority",
  "informationBoundary",
  "promptAuthority",
  "actionMap",
  "interaction",
  "ruleQueue",
  "timelineDetail",
  "actionPrompt",
  "log"
];

validateRoot();
validateTokens();
validateTableRows();
validateHandRails();
validateHomes();
validateBattlefield();
validateSidePanel();

if (errors.length > 0) {
  console.error("Wire table layout contract failed:");
  for (const error of errors) {
    console.error(`- ${error}`);
  }
  process.exit(1);
}

console.log("Wire table layout contract check passed.");

function validateRoot() {
  if (layout.runeDeckSize !== 12) {
    errors.push(`runeDeckSize must be 12, got ${layout.runeDeckSize}`);
  }
}

function validateTokens() {
  const tokens = layout.tokens;
  if (!tokens || typeof tokens !== "object") {
    errors.push("tokens object is required");
    return;
  }

  for (const [key, value] of Object.entries(tokens)) {
    if (!Number.isFinite(value) || value <= 0) {
      errors.push(`tokens.${key} must be a positive number`);
    }
  }

  if (tokens.runeCardWidth >= tokens.cardWidth || tokens.runeCardHeight >= tokens.cardHeight) {
    errors.push("rune cards should remain smaller than normal cards");
  }

  if (tokens.battlefieldCardWidth <= tokens.battlefieldCardHeight) {
    errors.push("battlefield card slot must be horizontal");
  }

  if (tokens.tableMinWidth < 1200 || tokens.tableMinHeight < 800) {
    errors.push("table minimum size is too small for the current wire tabletop");
  }
}

function validateTableRows() {
  const table = layout.table;
  if (!table || typeof table !== "object") {
    errors.push("table object is required");
    return;
  }

  if (!Array.isArray(table.rows) || table.rows.length !== 5) {
    errors.push("table.rows must contain opponent hand, opponent home, battlefield, self home, self hand");
    return;
  }

  if (!Array.isArray(table.rowTemplates) || table.rowTemplates.length !== table.rows.length) {
    errors.push("table.rowTemplates must match table.rows length");
  }

  expectRow(0, "handRail", "opponent");
  expectRow(1, "playerHome", "opponent");
  expectRow(2, "battlefield");
  expectRow(3, "playerHome", "self");
  expectRow(4, "handRail", "self");
}

function validateHandRails() {
  const rails = layout.handRails;
  if (!rails || typeof rails !== "object") {
    errors.push("handRails object is required");
    return;
  }

  validateHandRail("self", ["runeDeck", "runeTrack", "hand"], ["cards", "piles"], ["library", "played"], false);
  validateHandRail("opponent", ["hand", "runeTrack", "runeDeck"], ["piles", "cards"], ["played", "library"], true);
}

function validateHomes() {
  const homes = layout.playerHomes;
  if (!homes || typeof homes !== "object") {
    errors.push("playerHomes object is required");
    return;
  }

  validateHome("self", ["legend", "hero", "base"], ["base", "banish"]);
  validateHome("opponent", ["base", "hero", "legend"], ["banish", "base"]);
}

function validateBattlefield() {
  const battlefield = layout.battlefield;
  if (!battlefield || typeof battlefield !== "object") {
    errors.push("battlefield object is required");
    return;
  }

  expectArray("battlefield.slots", battlefield.slots, ["leftSite", "center", "rightSite"]);
  expectAllowedSet("battlefield.slots", battlefield.slots, battlefieldSlots);
  expectLength("battlefield.columns", battlefield.columns, battlefield.slots?.length ?? 0);
  expectLength("battlefield.centerColumns", battlefield.centerColumns, 2);
  expectLength("battlefield.centerRows", battlefield.centerRows, 3);

  const zones = battlefield.unitZones;
  if (!Array.isArray(zones) || zones.length !== 4) {
    errors.push("battlefield.unitZones must contain four lane/player quadrants");
    return;
  }

  expectUnitZone(0, 0, "opponent");
  expectUnitZone(1, 1, "opponent");
  expectUnitZone(2, 0, "self");
  expectUnitZone(3, 1, "self");

  const standbyZones = battlefield.standbyZones;
  if (!Array.isArray(standbyZones) || standbyZones.length !== 2) {
    errors.push("battlefield.standbyZones must contain one standby rail per battlefield");
    return;
  }

  expectStandbyZone(0, 0);
  expectStandbyZone(1, 1);
}

function validateSidePanel() {
  const panel = layout.sidePanel;
  if (!panel || typeof panel !== "object") {
    errors.push("sidePanel object is required");
    return;
  }

  expectArray("sidePanel.slots", panel.slots, sidePanelSlots);
  expectAllowedSet("sidePanel.slots", panel.slots, sidePanelSlots);
}

function validateHandRail(side, expectedSlots, expectedBodySlots, expectedPileSlots, expectedRuneReverse) {
  const rail = layout.handRails?.[side];
  if (!rail) {
    errors.push(`handRails.${side} is required`);
    return;
  }

  expectArray(`handRails.${side}.slots`, rail.slots, expectedSlots);
  expectAllowedSet(`handRails.${side}.slots`, rail.slots, handSlots);
  expectLength(`handRails.${side}.columns`, rail.columns, rail.slots?.length ?? 0);
  expectArray(`handRails.${side}.handBodySlots`, rail.handBodySlots, expectedBodySlots);
  expectLength(`handRails.${side}.handBodyColumns`, rail.handBodyColumns, rail.handBodySlots?.length ?? 0);
  expectArray(`handRails.${side}.pileSlots`, rail.pileSlots, expectedPileSlots);
  if (rail.runeReverse !== expectedRuneReverse) {
    errors.push(`handRails.${side}.runeReverse should be ${expectedRuneReverse}`);
  }
}

function validateHome(side, expectedSlots, expectedBaseSlots) {
  const home = layout.playerHomes?.[side];
  if (!home) {
    errors.push(`playerHomes.${side} is required`);
    return;
  }

  expectArray(`playerHomes.${side}.slots`, home.slots, expectedSlots);
  expectAllowedSet(`playerHomes.${side}.slots`, home.slots, homeSlots);
  expectLength(`playerHomes.${side}.columns`, home.columns, home.slots?.length ?? 0);
  expectArray(`playerHomes.${side}.baseSlots`, home.baseSlots, expectedBaseSlots);
  expectLength(`playerHomes.${side}.baseColumns`, home.baseColumns, home.baseSlots?.length ?? 0);
}

function expectRow(index, kind, side) {
  const row = layout.table.rows[index];
  if (row?.kind !== kind) {
    errors.push(`table.rows[${index}].kind should be ${kind}`);
  }
  if (side && row?.side !== side) {
    errors.push(`table.rows[${index}].side should be ${side}`);
  }
}

function expectUnitZone(index, laneIndex, side) {
  const zone = layout.battlefield.unitZones[index];
  if (zone?.laneIndex !== laneIndex || zone?.side !== side) {
    errors.push(`battlefield.unitZones[${index}] should be lane ${laneIndex} / ${side}`);
  }
}

function expectStandbyZone(index, laneIndex) {
  const zone = layout.battlefield.standbyZones[index];
  if (zone?.laneIndex !== laneIndex) {
    errors.push(`battlefield.standbyZones[${index}] should be lane ${laneIndex}`);
  }
}

function expectArray(name, actual, expected) {
  if (!Array.isArray(actual)) {
    errors.push(`${name} must be an array`);
    return;
  }

  if (actual.length !== expected.length || actual.some((value, index) => value !== expected[index])) {
    errors.push(`${name} should be [${expected.join(", ")}], got [${actual.join(", ")}]`);
  }
}

function expectAllowedSet(name, actual, allowed) {
  if (!Array.isArray(actual)) {
    return;
  }

  for (const value of actual) {
    if (!allowed.includes(value)) {
      errors.push(`${name} contains unsupported value ${value}`);
    }
  }
}

function expectLength(name, actual, expectedLength) {
  if (!Array.isArray(actual)) {
    errors.push(`${name} must be an array`);
    return;
  }

  if (actual.length !== expectedLength) {
    errors.push(`${name} length should be ${expectedLength}, got ${actual.length}`);
  }
}

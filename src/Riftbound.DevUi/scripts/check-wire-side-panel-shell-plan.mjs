import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const matchPageSource = readFileSync(resolve(scriptDir, "../src/pages/MatchPage.tsx"), "utf8");
const styleSource = readFileSync(resolve(scriptDir, "../src/styles/globals.css"), "utf8");

const sidePanelMatch = matchPageSource.match(/<aside className="wire-side-panel"[\s\S]*?<\/aside>/);
assert.ok(sidePanelMatch, "MatchPage must render a wire-side-panel aside");
const sidePanelSource = sidePanelMatch[0];

assertInOrder(sidePanelSource, [
  "<WireSidePanelDirectory",
  "<WireSidePanelOperationPanel",
  'className="wire-side-panel-rail-stack"',
  'className="wire-side-panel-stack"'
]);
assert.match(sidePanelSource, /aria-label="行动与日志"/);
assert.match(sidePanelSource, /aria-label="右侧控制台摘要堆栈"/);
assert.match(sidePanelSource, /data-wire-side-panel-rail-stack/);
assert.match(sidePanelSource, /data-wire-side-panel-rail-visible-count=/);
assert.match(sidePanelSource, /data-wire-side-panel-visible-count=/);
assert.match(sidePanelSource, /data-wire-side-panel-pane=/);
assert.match(sidePanelSource, /data-wire-side-panel-pane-visible=/);
assert.match(sidePanelSource, /data-wire-side-panel-pane-region=/);

assertStyleBlock(".wire-side-panel", [
  /grid-template-rows:[\s\S]*auto[\s\S]*auto[\s\S]*minmax\(0, min-content\)[\s\S]*minmax\(0, 1fr\)/,
  /overflow: hidden/
]);
assertStyleBlock(".wire-side-panel-directory", [
  /display: grid/,
  /border: 1px solid #000/
]);
assertStyleBlock(".wire-side-panel-operation-sections", [
  /grid-template-columns: repeat\(4, minmax\(0, 1fr\)\)/,
  /max-height: clamp\(72px, 10vh, 112px\)/,
  /overflow: auto/
]);
assertStyleBlock(".wire-side-panel-rail-stack", [
  /min-height: 0/,
  /overflow: hidden/
]);
assertStyleBlock('.wire-side-panel-rail-entry[data-wire-side-panel-rail-body-mode="compact"] > .wire-side-panel-rail-body', [
  /max-height: 36px/,
  /overflow: hidden/
]);
assertStyleBlock('.wire-side-panel-rail-entry[data-wire-side-panel-rail-body-mode="full"] > .wire-side-panel-rail-body', [
  /max-height: clamp\(64px, 12vh, 116px\)/,
  /overflow: auto/
]);
assertStyleBlock(".wire-side-panel-stack", [
  /grid-template-rows: minmax\(0, 1fr\)/,
  /overflow: hidden/
]);
assertStyleBlock(".wire-panel", [
  /overflow: auto/
]);

console.log("Wire side panel shell check passed.");

function assertInOrder(source, snippets) {
  let offset = -1;
  for (const snippet of snippets) {
    const nextOffset = source.indexOf(snippet);
    assert.ok(nextOffset > offset, `${snippet} must appear after previous side-panel shell layer`);
    offset = nextOffset;
  }
}

function assertStyleBlock(selector, patterns) {
  for (const pattern of patterns) {
    assert.ok(styleBlocks(selector).some((block) => pattern.test(block)), `${selector} must satisfy ${pattern}`);
  }
}

function styleBlocks(selector) {
  const escaped = selector.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
  return Array.from(styleSource.matchAll(new RegExp(`${escaped}\\s*\\{[\\s\\S]*?\\n\\}`, "g")), (match) => match[0]);
}

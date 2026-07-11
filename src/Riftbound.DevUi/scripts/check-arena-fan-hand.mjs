import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const flowSource = read("src/components/match/wireCardFlow.tsx");
const cardSource = read("src/components/cards/CardFace.tsx");
const matchSource = read("src/pages/MatchPage.tsx");
const errors = [];

requireText(flowSource, 'presentation?: "rail" | "fan"', "card flow must expose fan presentation");
requireText(flowSource, '"--arena-fan-index"', "fan cards need stable index geometry");
requireText(flowSource, '"--arena-fan-count"', "fan cards need total-count geometry");
requireText(flowSource, '"--arena-fan-center"', "fan cards need a stable center pivot");
requireText(matchSource, 'presentation="fan"', "hand renderers must opt into fan presentation");
requireText(cardSource, "className?: string", "CardFace must accept presentation classes");
requireText(cardSource, "style?: CSSProperties", "CardFace must accept fan CSS variables");
requireText(cardSource, 'cardAccessibilityLabel("未公开卡牌"', "hidden fans must retain neutral labels");

if (errors.length > 0) {
  console.error("Arena fan-hand check failed:");
  for (const error of errors) console.error(`- ${error}`);
  process.exit(1);
}

console.log("Arena fan-hand check passed.");

function read(relativePath) {
  return fs.readFileSync(path.join(root, relativePath), "utf8");
}

function requireText(source, expected, message) {
  if (!source.includes(expected)) errors.push(message);
}

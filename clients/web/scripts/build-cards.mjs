// Slims the official zh-CN catalog into the fields the battle table renders.
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const source = path.resolve(here, "../../../data/official/card-catalog.zh-CN.json");
const target = path.resolve(here, "../public/cards.json");

const catalog = JSON.parse(fs.readFileSync(source, "utf8"));
const cards = {};
for (const card of [...catalog.cards, ...(Array.isArray(catalog.scopedAdditions) ? catalog.scopedAdditions : [])]) {
  if (!card?.cardNo) continue;
  cards[card.cardNo] = {
    name: card.cardName,
    sub: card.subTitle || undefined,
    category: card.cardCategoryName,
    colors: card.cardColorList ?? [],
    cost: card.energy ?? undefined,
    power: card.power ?? undefined,
    effect: card.cardEffect || undefined,
    tag: card.tag || undefined,
    image: card.frontImage || undefined,
  };
}
fs.writeFileSync(target, JSON.stringify(cards));
console.log(`cards.json: ${Object.keys(cards).length} cards`);

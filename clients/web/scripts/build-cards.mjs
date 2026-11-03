// Slims the official zh-CN catalog into the fields the battle table renders.
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const source = path.resolve(here, "../../../data/official/card-catalog.zh-CN.json");
const target = path.resolve(here, "../public/cards.json");

const catalog = JSON.parse(fs.readFileSync(source, "utf8"));

// Later sets (VEN, RAD, tournament reprints…) are not in the base catalog: the server admits them
// from upstream snapshots by (Id, CardNo) pairs listed in src/Riftbound.CardCatalog/*Sources.cs.
// Read the same pairs so the table and deck builder know exactly the cards the server plays.
function admittedAdditions() {
  const sourcesDir = path.resolve(here, "../../../src/Riftbound.CardCatalog");
  const pairs = new Map();
  for (const file of fs.readdirSync(sourcesDir).filter((f) => f.endsWith("Sources.cs")))
    for (const m of fs.readFileSync(path.join(sourcesDir, file), "utf8").matchAll(/Id:\s*(\d+)L,\s*CardNo:\s*"([^"]+)"/g)) pairs.set(m[2], Number(m[1]));
  const byId = new Map();
  const walk = (dir) => {
    for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
      const full = path.join(dir, entry.name);
      if (entry.isDirectory()) walk(full);
      else if (entry.name.endsWith(".json")) {
        let data;
        try {
          data = JSON.parse(fs.readFileSync(full, "utf8"));
        } catch {
          continue;
        }
        if (Array.isArray(data?.cards)) for (const c of data.cards) if (c?.id != null && c.cardNo && !byId.has(c.id)) byId.set(c.id, c);
      }
    }
  };
  walk(path.resolve(here, "../../../data/official/upstream"));
  const out = [];
  for (const [cardNo, id] of pairs) {
    const card = byId.get(id);
    if (card) out.push({ ...card, cardNo });
    else console.warn(`build-cards: no upstream record for ${cardNo} (id ${id})`);
  }
  return out;
}

const cards = {};
const base = [...catalog.cards, ...(Array.isArray(catalog.scopedAdditions) ? catalog.scopedAdditions : [])];
const known = new Set(base.map((c) => c?.cardNo));
for (const card of [...base, ...admittedAdditions().filter((c) => !known.has(c.cardNo))]) {
  // scopedAdditions entries only point at a record (no card data); never let one replace it.
  if (!card?.cardNo || !card.cardName) continue;
  cards[card.cardNo] = {
    name: card.cardName,
    sub: card.subTitle || undefined,
    // Newer records carry only the list; the server joins it the same way (OfficialCardCatalogLoader).
    category: card.cardCategoryName || (card.cardCategoryNameList ?? []).join(" / ") || undefined,
    colors: card.cardColorList ?? [],
    cost: card.energy ?? undefined,
    power: card.power ?? undefined,
    effect: card.cardEffect || undefined,
    tag: card.tag || undefined,
    image: card.frontImage || undefined,
    // Deck building: legend/champion pairing, the one-copy limit and the printing's sort order.
    hero: card.hero || undefined,
    limit: card.cardGroupLimit ?? undefined,
    sort: card.listSort ?? undefined,
  };
}
// public/ holds only this generated (git-ignored) file, so a fresh checkout has no such folder.
fs.mkdirSync(path.dirname(target), { recursive: true });
fs.writeFileSync(target, JSON.stringify(cards));
console.log(`cards.json: ${Object.keys(cards).length} cards`);

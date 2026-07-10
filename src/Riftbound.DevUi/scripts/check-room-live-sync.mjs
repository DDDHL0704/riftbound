import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const roomPage = readFileSync(resolve(scriptDir, "../src/pages/RoomPage.tsx"), "utf8");

assert.match(roomPage, /useEffect\(\(\) => \{/);
assert.match(roomPage, /roomStatus !== "IN_PROGRESS" && roomStatus !== "FINISHED"/);
assert.match(roomPage, /window\.setInterval\(/);
assert.match(roomPage, /controller\.requestSnapshot\(\)/);
assert.match(roomPage, /window\.clearInterval\(/);

console.log("Room live sync check passed.");

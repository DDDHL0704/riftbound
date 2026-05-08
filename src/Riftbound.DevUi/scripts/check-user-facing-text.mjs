import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const scriptDir = path.dirname(fileURLToPath(import.meta.url));
const devUiRoot = path.resolve(scriptDir, "..");
const repoRoot = path.resolve(devUiRoot, "../..");

const forbiddenPhrases = [
  "Timed out waiting for Joined.",
  "BehaviorSpec not found.",
  "room already has two players",
  "invalid reconnect token",
  "player is not in room",
  "clientIntentId is required",
  "scenarioId is required",
  "SeedScenario is only available in Development."
];

const scanRoots = [
  path.join(devUiRoot, "src"),
  path.join(repoRoot, "src/Riftbound.Api"),
  path.join(repoRoot, "src/Riftbound.Engine")
];

const matches = [];
for (const filePath of scanRoots.flatMap((root) => listSourceFiles(root))) {
  const source = fs.readFileSync(filePath, "utf8");
  for (const phrase of forbiddenPhrases) {
    const index = source.indexOf(phrase);
    if (index >= 0) {
      matches.push({ filePath, phrase, line: lineNumber(source, index) });
    }
  }
}

if (matches.length > 0) {
  console.error("User-facing text check found forbidden English fallback phrases:");
  for (const match of matches) {
    console.error(`- ${path.relative(repoRoot, match.filePath)}:${match.line} ${match.phrase}`);
  }

  process.exit(1);
}

console.log("User-facing fallback text check passed.");

function listSourceFiles(root) {
  if (!fs.existsSync(root)) {
    return [];
  }

  const entries = fs.readdirSync(root, { withFileTypes: true });
  const files = [];
  for (const entry of entries) {
    if (entry.name === "bin" || entry.name === "obj" || entry.name === "dist" || entry.name === "node_modules") {
      continue;
    }

    const fullPath = path.join(root, entry.name);
    if (entry.isDirectory()) {
      files.push(...listSourceFiles(fullPath));
    } else if (entry.isFile() && /\.(cs|ts|tsx)$/.test(fullPath)) {
      files.push(fullPath);
    }
  }

  return files;
}

function lineNumber(source, index) {
  return source.slice(0, index).split("\n").length;
}

import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const scriptDir = path.dirname(fileURLToPath(import.meta.url));
const devUiRoot = path.resolve(scriptDir, "..");
const repoRoot = path.resolve(devUiRoot, "../..");
const eventLogPath = path.join(devUiRoot, "src/components/match/EventLog.tsx");

const labels = extractEventLabels(fs.readFileSync(eventLogPath, "utf8"));
const eventKinds = extractBackendEventKinds(repoRoot);
const missing = [...eventKinds].filter((kind) => !labels.has(kind)).sort();

if (missing.length > 0) {
  console.error("EventLog is missing Chinese labels for backend event kinds:");
  for (const kind of missing) {
    console.error(`- ${kind}`);
  }
  process.exit(1);
}

console.log(`EventLog labels cover ${eventKinds.size} backend event kinds.`);

function extractEventLabels(source) {
  return new Set([...source.matchAll(/^\s*([A-Z0-9_]+):\s*"/gm)].map((match) => match[1]));
}

function extractBackendEventKinds(root) {
  const eventKinds = new Set();
  const backendRoots = ["src/Riftbound.Engine", "src/Riftbound.Api"].map((relativePath) => path.join(root, relativePath));
  for (const filePath of backendRoots.flatMap((backendRoot) => listSourceFiles(backendRoot))) {
    const source = fs.readFileSync(filePath, "utf8");
    for (const match of source.matchAll(/new\s+GameEvent\s*\(\s*"([A-Z][A-Z0-9_]+)"/g)) {
      eventKinds.add(match[1]);
    }
  }

  for (const relativePath of [
    "src/Riftbound.Engine/CoreRuleEngine.cs",
    "src/Riftbound.Engine/MatchSession.cs",
    "src/Riftbound.Api/Hubs/GameHub.cs"
  ]) {
    const filePath = path.join(root, relativePath);
    const source = fs.readFileSync(filePath, "utf8");
    for (const match of source.matchAll(/new\s*(?:GameEvent)?\s*\(\s*"([A-Z][A-Z0-9_]+)"/g)) {
      eventKinds.add(match[1]);
    }
  }

  return eventKinds;
}

function listSourceFiles(root) {
  if (!fs.existsSync(root)) {
    return [];
  }

  const entries = fs.readdirSync(root, { withFileTypes: true });
  const files = [];
  for (const entry of entries) {
    if (entry.name === "bin" || entry.name === "obj") {
      continue;
    }

    const fullPath = path.join(root, entry.name);
    if (entry.isDirectory()) {
      files.push(...listSourceFiles(fullPath));
    } else if (entry.isFile() && fullPath.endsWith(".cs")) {
      files.push(fullPath);
    }
  }

  return files;
}

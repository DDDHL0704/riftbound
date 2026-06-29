import assert from "node:assert/strict";
import { existsSync, readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const srcRoot = resolve(scriptDir, "../src");
const packageJson = JSON.parse(readFileSync(resolve(scriptDir, "../package.json"), "utf8"));

const routerSource = readFileSync(resolve(srcRoot, "app/router.ts"), "utf8");
const appSource = readFileSync(resolve(srcRoot, "app/App.tsx"), "utf8");
const shellSource = readFileSync(resolve(srcRoot, "components/layout/AppShell.tsx"), "utf8");
const apiClientSource = readFileSync(resolve(srcRoot, "services/apiClient.ts"), "utf8");
const protocolSource = readFileSync(resolve(srcRoot, "types/protocol.ts"), "utf8");
const resultPageSource = readFileSync(resolve(srcRoot, "pages/ResultPage.tsx"), "utf8");
const profilePagePath = resolve(srcRoot, "pages/PlayerProfilePage.tsx");

assert.ok(existsSync(profilePagePath), "PlayerProfilePage must exist.");
const profilePageSource = readFileSync(profilePagePath, "utf8");

for (const requiredType of [
  "PlayerProfileDto",
  "PlayerMatchParticipantDto",
  "PlayerMatchDto",
  "LeaderboardEntryDto"
]) {
  assert.ok(protocolSource.includes(`type ${requiredType}`), `Protocol types must expose ${requiredType}.`);
}

for (const requiredMethod of [
  "playerProfile(",
  "playerMatches(",
  "leaderboard("
]) {
  assert.ok(apiClientSource.includes(requiredMethod), `ApiClient must expose ${requiredMethod}.`);
}

assert.ok(routerSource.includes('name: "profile"; handle: string'), "Router must model the profile route.");
assert.ok(routerSource.includes('segments[0] === "players"'), "Router must parse /players/:handle.");
assert.ok(routerSource.includes('case "profile"'), "Router must build profile route paths.");
assert.ok(appSource.includes("PlayerProfilePage"), "App must render PlayerProfilePage.");
assert.ok(shellSource.includes('label="资料"'), "Main navigation must expose the profile page.");

for (const requiredAttribute of [
  "data-profile-surface",
  "data-profile-handle",
  "data-profile-total-matches",
  "data-profile-match-history",
  "data-profile-match-row",
  "data-profile-leaderboard",
  "data-profile-leaderboard-row"
]) {
  assert.ok(profilePageSource.includes(requiredAttribute), `PlayerProfilePage must expose ${requiredAttribute}.`);
}

for (const requiredCopy of [
  "玩家资料",
  "最近对局",
  "排行榜"
]) {
  assert.ok(profilePageSource.includes(requiredCopy), `PlayerProfilePage must render ${requiredCopy}.`);
}

for (const requiredAttribute of [
  "data-result-recorded-status",
  "data-result-recorded-room-id",
  "data-result-recorded-player-id"
]) {
  assert.ok(resultPageSource.includes(requiredAttribute), `ResultPage must expose ${requiredAttribute}.`);
}

assert.ok(resultPageSource.includes("本局已记录"), "ResultPage must render the recorded status.");
assert.ok(resultPageSource.includes("playerMatches("), "ResultPage must query public player history for recorded status.");
assert.ok(packageJson.scripts["check:player-profile-surface"], "Package scripts must expose the player profile surface check.");
assert.ok(packageJson.scripts.build.includes("check:player-profile-surface"), "Build must run the player profile surface check.");

console.log("Player profile surface check passed.");

import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import * as signalR from "@microsoft/signalr";

const scriptDir = path.dirname(fileURLToPath(import.meta.url));
const repoRoot = path.resolve(scriptDir, "../../..");
const catalogPath = path.join(repoRoot, "data/official/card-catalog.zh-CN.json");
const serverUrl = process.env.RIFTBOUND_SERVER_URL ?? "http://127.0.0.1:5088";
const outputPath = process.env.RIFTBOUND_LEGEND_SMOKE_OUT
  ?? `/tmp/riftbound-regular-legend-batch-smoke-${Date.now()}.json`;
const maxMatches = Number(process.env.RIFTBOUND_LEGEND_SMOKE_MAX_MATCHES ?? "40");
const startIndex = Number(process.env.RIFTBOUND_LEGEND_SMOKE_START_INDEX ?? "0");
const opponentOffsets = (process.env.RIFTBOUND_LEGEND_SMOKE_OPPONENT_OFFSETS ?? "1,-2,2,-1,3,-3,4,-4,5,-5")
  .split(",")
  .map((value) => Number(value.trim()))
  .filter((value) => Number.isFinite(value) && value !== 0);
const requireTargetWin = process.env.RIFTBOUND_LEGEND_SMOKE_REQUIRE_TARGET_WIN !== "0";
const maxAttemptsPerLegend = Number(process.env.RIFTBOUND_LEGEND_SMOKE_MAX_ATTEMPTS ?? String(opponentOffsets.length * 4));
const maxCommandsPerMatch = Number(process.env.RIFTBOUND_LEGEND_SMOKE_MAX_COMMANDS ?? "190");
const delayMs = Number(process.env.RIFTBOUND_LEGEND_SMOKE_DELAY_MS ?? "45");

const catalog = JSON.parse(fs.readFileSync(catalogPath, "utf8"));
const cards = catalog.cards ?? catalog;
const specs = await fetchJson(`${serverUrl}/catalog/behavior-specs`);
const specByNo = new Map(specs.map((spec) => [spec.cardNo, spec]));
const byNo = new Map(cards.map((card) => [card.cardNo, card]));

const legends = regularLegends();
const decks = legends.map((legend, index) => buildDeck(legend, index));
const invalid = decks
  .map((deck, index) => ({ legend: legends[index], deck, errors: validateDeck(deck) }))
  .filter((entry) => entry.errors.length > 0);

if (invalid.length > 0) {
  throw new Error(`Generated invalid decks: ${JSON.stringify(invalid.slice(0, 5), null, 2)}`);
}

const run = {
  startedAt: new Date().toISOString(),
  serverUrl,
  scope: {
    rawLegendCards: cards.filter((card) => card.cardCategoryName === "传奇").length,
    regularLegendNames: legends.length,
    startIndex,
    maxMatches,
    opponentOffsets,
    requireTargetWin,
    maxAttemptsPerLegend
  },
  deckGeneration: legends.map((legend, index) => ({
    index: index + 1,
    cardName: legend.cardName,
    hero: legend.hero,
    legendCardNo: legend.cardNo,
    championCardNo: decks[index].championCardNo,
    battlefields: decks[index].battlefields,
    mainDeckCount: decks[index].mainDeck.length,
    runeDeckCount: decks[index].runeDeck.length
  })),
  matches: [],
  blockers: []
};

console.log(`Regular legend decks generated: ${legends.length}; output=${outputPath}`);

const lastIndex = Math.min(legends.length, startIndex + maxMatches);
for (let index = startIndex; index < lastIndex; index++) {
  const target = legends[index];
  const playerIds = {
    target: `L${String(index + 1).padStart(2, "0")}T`,
    opponent: `L${String(index + 1).padStart(2, "0")}O`
  };

  console.log(`[${index + 1}/${legends.length}] ${target.cardName}/${target.hero} (${target.cardNo})`);

  const attempts = [];
  let accepted = false;
  for (let attempt = 1; attempt <= maxAttemptsPerLegend; attempt++) {
    const roomId = `regular-legend-${String(index + 1).padStart(2, "0")}-a${attempt}-${Date.now()}`;
    const combo = attempt - 1;
    const targetSeat = combo % 2 === 0 ? "P1" : "P2";
    const targetAggressive = Math.floor(combo / 2) % 2 === 0;
    const opponentOffset = opponentOffsets[Math.floor(combo / 4) % opponentOffsets.length];
    const opponentIndex = (index + opponentOffset + legends.length) % legends.length;
    const opponent = legends[opponentIndex];
    try {
      const result = await runMatch({
        roomId,
        targetIndex: index,
        opponentIndex,
        targetDeck: decks[index],
        opponentDeck: decks[opponentIndex],
        targetLegend: target,
        opponentLegend: opponent,
        playerIds,
        targetSeat,
        targetAggressive
      });

      const targetScore = result.final.scores[playerIds.target] ?? 0;
      const opponentScore = result.final.scores[playerIds.opponent] ?? 0;
      const summary = {
        attempt,
        roomId,
        targetSeat,
        targetAggressive,
        opponentOffset,
        opponentLegendCardNo: opponent.cardNo,
        opponentLegendName: opponent.cardName,
        winnerPlayerId: result.final.winnerPlayerId,
        scores: result.final.scores,
        commandCount: result.commandCount,
        battles: result.eventCounts.BATTLE_DECLARED ?? 0
      };
      attempts.push(summary);
      console.log(
        `  a${attempt}/${targetSeat}/off=${opponentOffset} vs ${opponent.cardName} `
        + `${targetAggressive ? "target-aggr" : "target-passive"} `
        + `-> winner=${result.final.winnerPlayerId} scores=${targetScore}:${opponentScore} `
        + `commands=${result.commandCount} battles=${result.eventCounts.BATTLE_DECLARED ?? 0}`);

      if (!requireTargetWin || result.final.winnerPlayerId === playerIds.target) {
        result.acceptedAttempt = attempt;
        result.nonWinningAttempts = attempts.slice(0, -1);
        run.matches.push(result);
        accepted = true;
        break;
      }
    } catch (error) {
      run.blockers.push({
        severity: "P0",
        roomId,
        legendCardNo: target.cardNo,
        legendName: target.cardName,
        issue: error instanceof Error ? error.message : String(error),
        partial: error?.partial,
        attempts
      });
      console.error(`  !! blocker ${target.cardName}: ${error instanceof Error ? error.stack : error}`);
      accepted = false;
      break;
    } finally {
      fs.writeFileSync(outputPath, `${JSON.stringify(run, null, 2)}\n`);
    }
  }

  if (!accepted && run.blockers.length === 0) {
    run.blockers.push({
      severity: "P1",
      legendCardNo: target.cardNo,
      legendName: target.cardName,
      issue: `Target regular legend did not win after ${maxAttemptsPerLegend} score-finish attempts.`,
      attempts
    });
  }

  if (!accepted) {
    break;
  }
}

run.completedAt = new Date().toISOString();
fs.writeFileSync(outputPath, `${JSON.stringify(run, null, 2)}\n`);
console.log(`Wrote ${outputPath}`);

if (run.blockers.length > 0) {
  process.exitCode = 1;
}

async function runMatch(context) {
  const targetClient = createClient(context.playerIds.target, context.roomId, context.targetAggressive);
  const opponentClient = createClient(context.playerIds.opponent, context.roomId, !context.targetAggressive);
  const p1 = context.targetSeat === "P1" ? targetClient : opponentClient;
  const p2 = context.targetSeat === "P1" ? opponentClient : targetClient;
  const clients = [p1, p2];
  const commandLog = [];

  try {
    await Promise.all(clients.map((client) => client.connection.start()));
    await invokeHub(p1, "JoinRoom", context.roomId, p1.playerId, null);
    await invokeHub(p2, "JoinRoom", context.roomId, p2.playerId, null);
    await waitFor(() => p1.state.joined && p2.state.joined, "both players joined");

    await submit(targetClient, context.targetDeck, "SUBMIT_DECK target", commandLog);
    await submit(opponentClient, context.opponentDeck, "SUBMIT_DECK opponent", commandLog);
    await invokeReady(p1, commandLog);
    await invokeReady(p2, commandLog);
    await waitFor(() => phase(p1) === "MULLIGAN" || phase(p2) === "MULLIGAN", "mulligan phase");

    let idleTicks = 0;
    while (!isFinished(p1) && commandLog.length < maxCommandsPerMatch) {
      const actionable = chooseActionClient(p1, p2);
      if (!actionable) {
        await delay(delayMs);
        idleTicks += 1;
        if (idleTicks > 200) {
          throw new Error(`No actionable prompt while match is unfinished; phase=${phase(p1)} tick=${snapshot(p1)?.tick}`);
        }
        continue;
      }

      idleTicks = 0;
      const command = chooseCommand(actionable);
      if (!command) {
        throw new Error(
          `No command strategy for ${actionable.playerId} prompt actions=${JSON.stringify(actionable.state.prompt?.actions)} `
          + `view=${actionable.state.prompt?.view?.type}`);
      }

      await submit(actionable, command, command.cmdType, commandLog);
    }

    if (!isFinished(p1)) {
      const error = new Error(`Match did not finish within ${maxCommandsPerMatch} commands.`);
      error.partial = partialMatchState(targetClient, opponentClient, commandLog);
      throw error;
    }

    const finalSnapshot = snapshot(targetClient);
    const final = {
      tick: finalSnapshot.tick,
      turnNumber: finalSnapshot.turnNumber,
      roomStatus: finalSnapshot.timing?.roomStatus,
      winnerPlayerId: finalSnapshot.timing?.winnerPlayerId ?? finalSnapshot.winnerPlayerId ?? null,
      scores: Object.fromEntries(
        Object.entries(finalSnapshot.players ?? {}).map(([playerId, player]) => [playerId, player.score ?? 0])),
      selectedBattlefields: Object.fromEntries(
        Object.keys(finalSnapshot.players ?? {}).map((playerId) => [playerId, playerBattlefieldCardNos(finalSnapshot, playerId)]))
    };
    const eventCounts = countEvents(targetClient.state.events);
    const scoreWin = final.winnerPlayerId
      && (final.scores[final.winnerPlayerId] ?? 0) >= 8
      && final.roomStatus === "FINISHED";
    if (!scoreWin) {
      throw new Error(`Finished without score win: ${JSON.stringify(final)}`);
    }

    return {
      roomId: context.roomId,
      target: {
        playerId: targetClient.playerId,
        seat: context.targetSeat,
        aggressive: context.targetAggressive,
        legendName: context.targetLegend.cardName,
        hero: context.targetLegend.hero,
        legendCardNo: context.targetLegend.cardNo,
        championCardNo: context.targetDeck.championCardNo,
        deckBattlefields: context.targetDeck.battlefields
      },
      opponent: {
        playerId: opponentClient.playerId,
        seat: context.targetSeat === "P1" ? "P2" : "P1",
        aggressive: !context.targetAggressive,
        legendName: context.opponentLegend.cardName,
        hero: context.opponentLegend.hero,
        legendCardNo: context.opponentLegend.cardNo,
        championCardNo: context.opponentDeck.championCardNo,
        deckBattlefields: context.opponentDeck.battlefields
      },
      final,
      eventCounts,
      commandCount: commandLog.length,
      commandSummary: summarizeCommands(commandLog)
    };
  } finally {
    await Promise.allSettled(clients.map((client) => client.connection.stop()));
  }
}

function partialMatchState(p1, p2, commandLog) {
  const current = snapshot(p1);
  return {
    tick: current?.tick ?? null,
    turnNumber: current?.turnNumber ?? null,
    phase: current?.timing?.phase ?? null,
    roomStatus: current?.timing?.roomStatus ?? null,
    activePlayerId: current?.activePlayerId ?? null,
    scores: current?.players
      ? Object.fromEntries(Object.entries(current.players).map(([playerId, player]) => [playerId, player.score ?? 0]))
      : {},
    deckCounts: current?.players
      ? Object.fromEntries(Object.entries(current.players).map(([playerId, player]) => [playerId, player.zones?.mainDeckCount ?? null]))
      : {},
    handSizes: current?.players
      ? Object.fromEntries(Object.entries(current.players).map(([playerId, player]) => [playerId, player.handSize ?? player.zones?.hand?.length ?? null]))
      : {},
    prompts: {
      [p1.playerId]: {
        actions: p1.state.prompt?.actions ?? [],
        viewType: p1.state.prompt?.view?.type ?? null,
        actionable: p1.state.prompt?.actionable ?? false
      },
      [p2.playerId]: {
        actions: p2.state.prompt?.actions ?? [],
        viewType: p2.state.prompt?.view?.type ?? null,
        actionable: p2.state.prompt?.actionable ?? false
      }
    },
    eventCounts: countEvents(p1.state.events),
    commandSummary: summarizeCommands(commandLog),
    recentCommands: commandLog.slice(-20)
  };
}

function createClient(playerId, roomId, targetSeat) {
  const state = {
    joined: undefined,
    prompt: undefined,
    snapshot: undefined,
    events: [],
    errors: []
  };
  const connection = new signalR.HubConnectionBuilder()
    .withUrl(`${serverUrl}/hubs/game`)
    .build();

  connection.on("Joined", (message) => {
    state.joined = message.payload;
  });
  connection.on("Snapshot", (message) => {
    state.snapshot = message.payload;
  });
  connection.on("Prompt", (message) => {
    state.prompt = message.payload;
  });
  connection.on("Events", (message) => {
    state.events.push(...message.payload);
  });
  connection.on("Error", (message) => {
    state.errors.push(message.payload);
  });

  return { playerId, roomId, connection, state, targetSeat };
}

async function invokeReady(client, commandLog) {
  const beforeTick = snapshot(client)?.tick ?? null;
  await invokeHub(client, "Ready", client.roomId, client.playerId, intentId(`${client.playerId}-ready`));
  commandLog.push({
    playerId: client.playerId,
    cmdType: "READY",
    note: "READY",
    beforeTick
  });
}

async function submit(client, command, note, commandLog) {
  const beforeTick = snapshot(client)?.tick ?? null;
  const prompt = client.state.prompt;
  await invokeHub(
    client,
    "SubmitIntent",
    client.roomId,
    client.playerId,
    intentId(`${client.playerId}-${note}`),
    command);
  commandLog.push({
    playerId: client.playerId,
    cmdType: command.cmdType,
    note,
    beforeTick,
    promptActions: prompt?.actions ?? [],
    viewType: prompt?.view?.type ?? null
  });
}

async function invokeHub(client, method, ...args) {
  const errorStart = client.state.errors.length;
  await client.connection.invoke(method, ...args);
  await delay(delayMs);
  if (client.state.errors.length > errorStart) {
    throw new Error(`${client.playerId} hub error: ${JSON.stringify(client.state.errors.slice(errorStart))}`);
  }
}

function chooseActionClient(p1, p2) {
  const candidates = [p1, p2].filter((client) => {
    const prompt = client.state.prompt;
    return prompt?.actionable && !(prompt.actions?.length === 1 && prompt.actions[0] === "WAIT");
  });

  if (candidates.length === 0) {
    return undefined;
  }

  const priority = [
    (client) => hasAction(client, "MULLIGAN"),
    (client) => hasAction(client, "PASS_PRIORITY") || hasAction(client, "PASS") || hasAction(client, "PASS_FOCUS"),
    (client) => hasAction(client, "ORDER_TRIGGERS") || hasAction(client, "PAY_COST") || hasAction(client, "ASSIGN_COMBAT_DAMAGE"),
    (client) => client.targetSeat && hasAction(client, "DECLARE_BATTLE"),
    (client) => hasAction(client, "TAP_RUNE") && shouldTap(client),
    (client) => hasAction(client, "PLAY_CARD"),
    (client) => hasAction(client, "END_TURN")
  ];

  for (const predicate of priority) {
    const selected = candidates.find(predicate);
    if (selected) {
      return selected;
    }
  }

  return candidates[0];
}

function chooseCommand(client) {
  if (hasAction(client, "MULLIGAN")) {
    return { cmdType: "MULLIGAN", handObjectIds: [] };
  }

  if (hasAction(client, "PASS_PRIORITY")) {
    return { cmdType: "PASS_PRIORITY" };
  }

  if (hasAction(client, "PASS_FOCUS")) {
    return { cmdType: "PASS_FOCUS" };
  }

  if (hasAction(client, "PASS")) {
    return { cmdType: "PASS" };
  }

  if (hasAction(client, "ORDER_TRIGGERS")) {
    const order = enabledCandidate(client, "ORDER_TRIGGERS");
    const triggerIds = order?.targets?.map((choice) => choice.id)
      ?? order?.metadata?.triggerIds
      ?? order?.metadata?.orderedTriggerIds
      ?? [];
    return { cmdType: "ORDER_TRIGGERS", triggerIds, orderedTriggerIds: triggerIds };
  }

  if (hasAction(client, "PAY_COST")) {
    const pay = enabledCandidate(client, "PAY_COST");
    const choices = pay?.sources?.map((choice) => choice.id)
      ?? pay?.targets?.map((choice) => choice.id)
      ?? pay?.metadata?.paymentChoiceIds
      ?? [];
    return { cmdType: "PAY_COST", paymentChoiceIds: choices };
  }

  if (hasAction(client, "ASSIGN_COMBAT_DAMAGE")) {
    const assign = enabledCandidate(client, "ASSIGN_COMBAT_DAMAGE");
    const assignments = assign?.metadata?.assignments ?? assign?.metadata?.legalAssignments ?? [];
    return { cmdType: "ASSIGN_COMBAT_DAMAGE", assignments };
  }

  if (client.targetSeat && hasAction(client, "DECLARE_BATTLE")) {
    const battle = declareBattleCommand(client);
    if (battle) {
      return battle;
    }
  }

  if (hasAction(client, "TAP_RUNE") && shouldTap(client)) {
    const tap = enabledCandidate(client, "TAP_RUNE");
    const source = tap?.sources?.[0];
    if (source?.id) {
      return { cmdType: "TAP_RUNE", sourceObjectId: source.id };
    }
  }

  if (hasAction(client, "PLAY_CARD")) {
    const play = playCardCommand(client);
    if (play) {
      return play;
    }
  }

  if (hasAction(client, "END_TURN")) {
    return { cmdType: "END_TURN" };
  }

  return undefined;
}

function shouldTap(client) {
  const mana = snapshot(client)?.players?.[client.playerId]?.runePool?.mana ?? 0;
  if (!hasAction(client, "PLAY_CARD")) {
    return mana < 4;
  }

  return client.targetSeat ? mana < 3 : mana < 2;
}

function playCardCommand(client) {
  const play = enabledCandidate(client, "PLAY_CARD");
  const requirements = sourceRequirements(play)
    .filter((requirement) => requirement?.composable !== false)
    .filter((requirement) => (requirement.minTargetCount ?? 0) === 0)
    .filter((requirement) => (requirement.requiredOptionalCosts?.length ?? 0) === 0);
  const requirement = requirements
    .find((entry) => destinationId(entry, `BATTLEFIELD:${client.playerId}-MAIN`))
    ?? requirements[0];

  if (!requirement?.sourceObjectId || !requirement.cardNo) {
    return undefined;
  }

  return {
    cmdType: "PLAY_CARD",
    sourceObjectId: requirement.sourceObjectId,
    cardNo: requirement.cardNo,
    targetObjectIds: [],
    destination: destinationId(requirement, `BATTLEFIELD:${client.playerId}-MAIN`) ?? destinationId(requirement, "BASE"),
    optionalCosts: []
  };
}

function declareBattleCommand(client) {
  const battle = enabledCandidate(client, "DECLARE_BATTLE");
  const requirements = sourceRequirements(battle).filter((requirement) => requirement?.composable !== false);
  const requirement = requirements.find((entry) => firstChoice(entry.attackerChoicesByIndex)?.id && firstChoice(entry.targetChoicesByIndex)?.id)
    ?? requirements[0];
  const attacker = firstChoice(requirement?.attackerChoicesByIndex);
  const defender = firstChoice(requirement?.targetChoicesByIndex);
  const battlefield = requirement?.battlefieldChoices?.find((choice) => choice.id?.includes(`${client.playerId}-BATTLEFIELD`))
    ?? requirement?.battlefieldChoices?.find((choice) => String(choice.id).startsWith("BATTLEFIELD:"))
    ?? requirement?.battlefieldChoices?.[0];

  if (!attacker?.id || !defender?.id || !battlefield?.id) {
    return undefined;
  }

  return {
    cmdType: "DECLARE_BATTLE",
    battlefieldId: battlefield.id,
    attackerObjectIds: [attacker.id],
    defenderObjectIds: [defender.id],
    optionalCosts: requirement.requiredOptionalCosts ?? requirement.optionalCostChoices?.map((choice) => choice.id) ?? []
  };
}

function sourceRequirements(candidate) {
  return Array.isArray(candidate?.metadata?.sourceRequirements)
    ? candidate.metadata.sourceRequirements
    : [];
}

function firstChoice(choicesByIndex) {
  if (!choicesByIndex || typeof choicesByIndex !== "object") {
    return undefined;
  }

  const firstKey = Object.keys(choicesByIndex).sort()[0];
  const choices = firstKey ? choicesByIndex[firstKey] : undefined;
  return Array.isArray(choices) ? choices[0] : undefined;
}

function destinationId(requirement, preferred) {
  return requirement?.destinationChoices?.find((choice) => choice.id === preferred)?.id
    ?? requirement?.destinationChoices?.find((choice) => String(choice.id).startsWith("BATTLEFIELD:"))?.id
    ?? requirement?.destinationChoices?.[0]?.id;
}

function hasAction(client, action) {
  return (client.state.prompt?.actions ?? []).includes(action)
    && (!client.state.prompt?.candidates
      || client.state.prompt.candidates.some((candidate) => candidate.action === action && candidate.enabled));
}

function enabledCandidate(client, action) {
  return (client.state.prompt?.candidates ?? [])
    .find((candidate) => candidate.action === action && candidate.enabled);
}

function isFinished(client) {
  return client.state.snapshot?.timing?.roomStatus === "FINISHED";
}

function snapshot(client) {
  return client.state.snapshot;
}

function phase(client) {
  return client.state.snapshot?.timing?.phase;
}

function playerBattlefieldCardNos(playerSnapshot, playerId) {
  const objectIds = playerSnapshot.players?.[playerId]?.zones?.battlefields ?? [];
  return objectIds
    .map((objectId) => cardObject(playerSnapshot, objectId)?.cardNo)
    .filter(Boolean);
}

function cardObject(playerSnapshot, objectId) {
  for (const player of Object.values(playerSnapshot.players ?? {})) {
    const object = player.objects?.[objectId];
    if (object) {
      return object;
    }
  }

  return undefined;
}

function countEvents(events) {
  const counts = {};
  for (const event of events) {
    counts[event.kind] = (counts[event.kind] ?? 0) + 1;
  }
  return counts;
}

function summarizeCommands(commands) {
  const counts = {};
  for (const command of commands) {
    counts[command.cmdType] = (counts[command.cmdType] ?? 0) + 1;
  }
  return counts;
}

function buildDeck(legend, index) {
  const allowed = colors(legend);
  const champion = chooseChampion(legend);
  if (!champion) {
    throw new Error(`No champion for ${legend.cardName} ${legend.cardNo}`);
  }

  const mainDeck = [champion.cardNo];
  const candidates = [...groupBy(cards
    .filter((card) => ["单位", "英雄单位"].includes(card.cardCategoryName))
    .filter((card) => card.status === 1)
    .filter((card) => card.cardNo !== champion.cardNo)
    .filter((card) => !isUnique(card))
    .filter((card) => subset(colors(card), allowed))
    .filter((card) => Number(card.energy ?? 99) <= 4)
    .filter((card) => Number(card.power ?? 0) > 0)
    .filter((card) => specByNo.get(card.cardNo)?.status === "implemented")
    .filter((card) => (specByNo.get(card.cardNo)?.targets ?? []).length === 0),
  (card) => card.cardName).values()]
    .map((group) => group.sort((a, b) => candidateScore(b) - candidateScore(a)
      || String(a.cardNo).localeCompare(String(b.cardNo)))[0])
    .sort((a, b) => candidateScore(b) - candidateScore(a)
      || String(a.cardNo).localeCompare(String(b.cardNo)));

  for (const candidate of candidates) {
    for (let copies = 0; copies < 3 && mainDeck.length < 40; copies++) {
      mainDeck.push(candidate.cardNo);
    }
    if (mainDeck.length >= 40) {
      break;
    }
  }

  return {
    cmdType: "SUBMIT_DECK",
    legendCardNo: legend.cardNo,
    championCardNo: champion.cardNo,
    mainDeck,
    runeDeck: pickRunes(legend, index),
    battlefields: pickBattlefields(index)
  };
}

function regularLegends() {
  return [...groupBy(cards
    .filter((card) => card.cardCategoryName === "传奇")
    .filter((card) => card.status === 1),
  (card) => card.cardName).values()]
    .map((group) => group.sort((a, b) => normalScore(b) - normalScore(a)
      || String(a.cardNo).localeCompare(String(b.cardNo)))[0])
    .sort((a, b) => a.cardName.localeCompare(b.cardName, "zh-Hans-CN"));
}

function chooseChampion(legend) {
  const allowed = colors(legend);
  return cards
    .filter((card) => card.cardCategoryName === "英雄单位")
    .filter((card) => card.status === 1)
    .filter((card) => card.hero === legend.hero)
    .filter((card) => subset(colors(card), allowed))
    .sort((a, b) => candidateScore(b) - candidateScore(a) || normalScore(b) - normalScore(a))[0];
}

function pickRunes(legend, index) {
  const available = runeCardsByColor();
  const legendColors = [...colors(legend)].filter((color) => color !== "colorless");
  const shares = legendColors.map((_, colorIndex) => Math.floor(12 / legendColors.length)
    + (colorIndex < 12 % legendColors.length ? 1 : 0));
  const out = [];
  legendColors.forEach((color, colorIndex) => {
    const runes = available.get(color) ?? [];
    for (let pick = 0; pick < shares[colorIndex]; pick++) {
      out.push(runes[(index + pick) % runes.length].cardNo);
    }
  });
  return out;
}

function pickBattlefields(index) {
  const battlefields = regularBattlefields();
  const out = [];
  for (let offset = 0; offset < battlefields.length && out.length < 3; offset++) {
    const candidate = battlefields[(index * 3 + offset) % battlefields.length];
    if (!out.some((cardNo) => byNo.get(cardNo)?.cardName === candidate.cardName)) {
      out.push(candidate.cardNo);
    }
  }
  return out;
}

function validateDeck(deck) {
  const errors = [];
  const legend = byNo.get(deck.legendCardNo);
  const champion = byNo.get(deck.championCardNo);
  const allowed = legend ? colors(legend) : new Set();
  if (!legend || legend.cardCategoryName !== "传奇") errors.push("bad legend");
  if (!champion || champion.cardCategoryName !== "英雄单位") errors.push("bad champion");
  if (legend && champion && legend.hero !== champion.hero) errors.push("champion hero mismatch");
  if (deck.mainDeck.length < 40) errors.push("short main");
  if (!deck.mainDeck.includes(deck.championCardNo)) errors.push("champion missing in main");
  if (deck.runeDeck.length !== 12) errors.push("bad rune count");
  if (deck.battlefields.length !== 3) errors.push("bad battlefield count");

  const mainCards = deck.mainDeck.map((cardNo) => byNo.get(cardNo));
  for (const card of mainCards) {
    if (!card) {
      errors.push("unknown main");
      continue;
    }
    if (!["单位", "英雄单位", "装备", "法术", "专属单位", "专属装备", "专属法术"].includes(card.cardCategoryName)) {
      errors.push(`bad main category ${card.cardNo}`);
    }
    if (!subset(colors(card), allowed)) {
      errors.push(`bad main color ${card.cardNo}`);
    }
  }

  for (const card of deck.runeDeck.map((cardNo) => byNo.get(cardNo))) {
    if (!card || card.cardCategoryName !== "符文") {
      errors.push(`bad rune ${card?.cardNo}`);
    } else if (!subset(colors(card), allowed)) {
      errors.push(`bad rune color ${card.cardNo}`);
    }
  }

  const battlefieldNames = new Set();
  for (const card of deck.battlefields.map((cardNo) => byNo.get(cardNo))) {
    if (!card || card.cardCategoryName !== "战场") {
      errors.push(`bad battlefield ${card?.cardNo}`);
      continue;
    }
    if (battlefieldNames.has(card.cardName)) {
      errors.push(`duplicate battlefield ${card.cardName}`);
    }
    battlefieldNames.add(card.cardName);
    if (!subset(colors(card), allowed)) {
      errors.push(`bad battlefield color ${card.cardNo}`);
    }
  }

  for (const [name, group] of groupBy(mainCards.filter(Boolean), (card) => card.cardName)) {
    const max = group.some(isUnique) ? 1 : 3;
    if (group.length > max) {
      errors.push(`copy limit ${name} ${group.length}/${max}`);
    }
  }

  return [...new Set(errors)];
}

function runeCardsByColor() {
  const map = new Map();
  for (const card of cards.filter((candidate) => candidate.cardCategoryName === "符文" && candidate.status === 1)) {
    for (const color of colors(card)) {
      if (color === "colorless") {
        continue;
      }
      if (!map.has(color)) {
        map.set(color, []);
      }
      map.get(color).push(card);
    }
  }

  for (const runes of map.values()) {
    runes.sort((a, b) => normalScore(b) - normalScore(a)
      || String(a.cardNo).localeCompare(String(b.cardNo)));
  }
  return map;
}

function regularBattlefields() {
  return [...groupBy(cards
    .filter((card) => card.cardCategoryName === "战场")
    .filter((card) => card.status === 1)
    .filter((card) => subset(colors(card), new Set(["colorless"]))),
  (card) => card.cardName).values()]
    .map((group) => group.sort((a, b) => normalScore(b) - normalScore(a)
      || String(a.cardNo).localeCompare(String(b.cardNo)))[0])
    .sort((a, b) => String(a.cardNo).localeCompare(String(b.cardNo)));
}

function normalScore(card) {
  const cardNo = String(card.cardNo ?? "");
  let score = 0;
  if (card.status === 1) score += 2000;
  if (card.extendRarityName === "平卡") score += 1000;
  if (card.extendRarity === "base_inscript") score += 500;
  if (!cardNo.includes("*")) score += 200;
  if (!/·P|\.P|P$/.test(cardNo)) score += 100;
  if (!/[a-z]\//i.test(cardNo)) score += 50;
  if (/^(UNL|OGN|SFD)/.test(cardNo)) score += 30;
  if (/^FND/.test(cardNo)) score -= 10;
  if (/^OGS/.test(cardNo)) score -= 20;
  score -= Number(card.listSort ?? 0) / 100000;
  return score;
}

function candidateScore(card) {
  const spec = specByNo.get(card.cardNo);
  let score = 0;
  if (spec?.status === "implemented") score += 1000;
  if ((spec?.targets ?? []).length === 0) score += 300;
  if (card.cardCategoryName === "单位") score += 100;
  if (card.cardCategoryName === "英雄单位") score += 50;
  score -= Number(card.energy ?? 9) * 20;
  score += Number(card.power ?? 0);
  if (isRegularNumber(card.cardNo)) score += 10;
  return score;
}

function isRegularNumber(cardNo) {
  return !String(cardNo).includes("*")
    && !/·P|\.P|P$/.test(String(cardNo))
    && !/[a-z]\//i.test(String(cardNo));
}

function isUnique(card) {
  return card.cardGroupLimit === 1 || String(card.cardEffect ?? "").includes("{{唯我}}");
}

function colors(card) {
  return new Set((card.cardColorList ?? []).filter(Boolean));
}

function subset(candidateColors, allowedColors) {
  return [...candidateColors].every((color) => color === "colorless" || allowedColors.has(color));
}

function groupBy(items, keySelector) {
  const map = new Map();
  for (const item of items) {
    const key = keySelector(item);
    if (!map.has(key)) {
      map.set(key, []);
    }
    map.get(key).push(item);
  }
  return map;
}

async function fetchJson(url) {
  const response = await fetch(url);
  if (!response.ok) {
    throw new Error(`GET ${url} failed: ${response.status} ${response.statusText}`);
  }
  return response.json();
}

function intentId(label) {
  return `${label}-${Date.now()}-${Math.random().toString(16).slice(2)}`;
}

function waitFor(predicate, label, timeoutMs = 20_000) {
  const started = Date.now();
  return new Promise((resolve, reject) => {
    const tick = () => {
      try {
        if (predicate()) {
          resolve();
          return;
        }
      } catch (error) {
        reject(error);
        return;
      }

      if (Date.now() - started > timeoutMs) {
        reject(new Error(`Timed out waiting for ${label}`));
        return;
      }

      setTimeout(tick, delayMs);
    };
    tick();
  });
}

function delay(ms) {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

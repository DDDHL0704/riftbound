import type { ActionPromptCandidateDto, ActionPromptDto, GameCommand } from "../types/protocol";
import { commandFromActionPromptTemplate } from "./actionPromptCommandTemplate";

type SourceCandidateOptions = {
  enabledOnly?: boolean;
};

export function sourceCandidatesForPrompt(
  prompt: ActionPromptDto | undefined,
  sourceObjectId: string | undefined,
  options: SourceCandidateOptions = {}
): ActionPromptCandidateDto[] {
  if (!prompt || !sourceObjectId) {
    return [];
  }

  const enabledOnly = options.enabledOnly ?? true;
  return (prompt.candidates ?? []).filter((candidate) =>
    (!enabledOnly || candidate.enabled)
    && candidateMatchesSource(candidate, sourceObjectId));
}

export function candidateMatchesSource(candidate: ActionPromptCandidateDto, sourceObjectId: string): boolean {
  return (candidate.sources ?? []).some((source) => source.id === sourceObjectId)
    || sourceRequirementIds(candidate).includes(sourceObjectId);
}

export function commandForSourceCandidate(
  candidate: ActionPromptCandidateDto,
  sourceObjectId: string | undefined
): GameCommand | undefined {
  if (!sourceObjectId || !candidate.enabled) {
    return undefined;
  }

  const templatedCommand = commandFromActionPromptTemplate(
    candidate.commandTemplate,
    { sourceId: sourceObjectId },
    sourceRequirementFor(candidate, sourceObjectId)
  );
  if (templatedCommand) {
    return templatedCommand;
  }

  if (candidate.action === "TAP_RUNE") {
    return { cmdType: "TAP_RUNE", sourceObjectId };
  }

  if (candidate.action === "RECYCLE_RUNE") {
    return { cmdType: "RECYCLE_RUNE", sourceObjectId };
  }

  return undefined;
}

export function promptStampedCommand(command: GameCommand, prompt: ActionPromptDto | undefined): GameCommand {
  if (!prompt || (command.promptId != null && command.snapshotTick != null)) {
    return command;
  }

  return {
    ...command,
    promptId: command.promptId ?? prompt.promptId ?? null,
    snapshotTick: command.snapshotTick ?? prompt.snapshotTick ?? null
  };
}

export function sourceRequirementRecords(candidate: ActionPromptCandidateDto): Record<string, unknown>[] {
  const requirements = candidate.metadata?.sourceRequirements;
  const records = Array.isArray(requirements)
    ? requirements
    : requirements && typeof requirements === "object"
      ? Object.values(requirements)
      : [];

  return records.filter((value): value is Record<string, unknown> =>
    Boolean(value) && typeof value === "object" && !Array.isArray(value));
}

export function sourceRequirementIds(candidate: ActionPromptCandidateDto): string[] {
  return sourceRequirementRecords(candidate)
    .map((record) => record.sourceObjectId)
    .filter((value): value is string => typeof value === "string" && value.trim().length > 0);
}

export function sourceRequirementFor(
  candidate: ActionPromptCandidateDto,
  sourceObjectId: string | undefined
): Record<string, unknown> | undefined {
  if (!sourceObjectId) {
    return undefined;
  }

  return sourceRequirementRecords(candidate)
    .find((record) => record.sourceObjectId === sourceObjectId);
}

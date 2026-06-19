import { Send } from "lucide-react";
import { useEffect, useMemo, useState } from "react";
import type {
  ActionPromptCandidateDto,
  ActionPromptChoiceDto,
  ActionPromptCommandTemplateBindingDto,
  ActionPromptCommandTemplateDto,
  ActionPromptDto,
  GameCommand,
  SnapshotDto
} from "../../types/protocol";
import { promptStampedCommand } from "../../utils/actionPromptCandidates";
import type { CandidateSelectionDraft } from "../../utils/candidateSelectionDraft";
import { promptActionLabel, promptReasonTitle } from "../../utils/formatters";
import { redactInternalText } from "../../utils/redaction";
import { Button } from "../ui/Button";
import { StatusPill } from "../ui/StatusPill";

type ChoiceGroup = {
  key: string;
  label: string;
  choices: ActionPromptChoiceDto[];
  required: boolean;
};

type CandidateComposerState = {
  destinationId?: string;
  mode?: string;
  optionalCostIds: string[];
  sourceId?: string;
  targetIdsByGroup: Record<string, string>;
};

type CandidateComposerModel = {
  resetKey: string;
  sourceRequirements: Array<Record<string, unknown>>;
  sourceRequirementById: Map<string, Record<string, unknown>>;
};

type CandidateComposerControls = {
  destinationChoices: ActionPromptChoiceDto[];
  destinationRequired: boolean;
  modeChoices: ActionPromptChoiceDto[];
  optionalCostChoices: ActionPromptChoiceDto[];
  requiredOptionalCostIds: string[];
  sourceRequired: boolean;
  sources: ActionPromptChoiceDto[];
  targetGroups: ChoiceGroup[];
};

type CandidateComposerProps = {
  candidate: ActionPromptCandidateDto;
  disabledByConnection: boolean;
  onCommand: (command: GameCommand) => void;
  onSubmitted?: () => void;
  prompt?: ActionPromptDto;
  snapshot?: SnapshotDto;
  forcedSourceObjectId?: string;
  selectionDraft?: CandidateSelectionDraft;
};

const composerCandidateActions = new Set([
  "PLAY_CARD",
  "HIDE_CARD",
  "REVEAL_CARD",
  "MOVE_UNIT",
  "ASSEMBLE_EQUIPMENT",
  "DECLARE_BATTLE",
  "ACTIVATE_ABILITY",
  "LEGEND_ACT"
]);

export function canComposeCandidate(candidate: ActionPromptCandidateDto): boolean {
  return Boolean(candidate.commandTemplate) || composerCandidateActions.has(candidate.action);
}

export function CandidateComposer({
  candidate,
  disabledByConnection,
  forcedSourceObjectId,
  onCommand,
  onSubmitted,
  prompt,
  selectionDraft,
  snapshot
}: CandidateComposerProps) {
  const model = useMemo(() => buildCandidateComposerModel(candidate), [candidate]);
  const [state, setState] = useState<CandidateComposerState>(() => initialComposerState(candidate, model, forcedSourceObjectId, selectionDraft));

  useEffect(() => {
    setState(initialComposerState(candidate, model, forcedSourceObjectId, selectionDraft));
  }, [candidate.action, candidate.label, forcedSourceObjectId, model.resetKey, candidate, selectionDraft]);

  const requirement = selectedRequirement(model, state.sourceId);
  const controls = composerControls(candidate, model, requirement, forcedSourceObjectId);
  const selectedTargetIds = controls.targetGroups
    .map((group) => state.targetIdsByGroup[group.key])
    .filter((id): id is string => Boolean(id));
  const optionalCostIds = uniqueStrings([...controls.requiredOptionalCostIds, ...state.optionalCostIds]);
  const command = composerCommand(candidate, snapshot, state, requirement, selectedTargetIds, optionalCostIds);
  const unsupportedReason = requirement && !booleanFromRecord(requirement, "composable", true)
    ? safePromptSummary(requirement.unsupportedReason) ?? "服务端暂未开放该候选的组合提交。"
    : undefined;
  const missingRequiredTarget = controls.targetGroups
    .some((group) => group.required && !state.targetIdsByGroup[group.key]);
  const canSubmit = !disabledByConnection
    && candidate.enabled
    && !unsupportedReason
    && Boolean(command)
    && (!controls.sourceRequired || Boolean(state.sourceId))
    && (!controls.destinationRequired || Boolean(state.destinationId))
    && !missingRequiredTarget;

  return (
    <div className="candidate-composer">
      <div className="candidate-composer-heading">
        <strong>{promptActionLabel(candidate)}</strong>
        <StatusPill tone={canSubmit ? "warn" : "neutral"}>{canSubmit ? "待服务端校验" : "需要选择"}</StatusPill>
      </div>
      <p className="candidate-composer-note">
        仅使用服务端候选组装命令；费用、目标和结果仍由服务端按规则校验。
      </p>
      {unsupportedReason && <span className="candidate-composer-warning">{unsupportedReason}</span>}
      {controls.sources.length > 0 && (
        <label className="candidate-composer-field">
          <span>来源</span>
          <select
            disabled={disabledByConnection || !candidate.enabled || Boolean(forcedSourceObjectId)}
            onChange={(event) => setState(initialComposerState(candidate, model, event.currentTarget.value))}
            value={state.sourceId ?? ""}
          >
            {controls.sources.map((choice) => (
              <option key={choice.id} value={choice.id}>{choiceLabel(choice)}</option>
            ))}
          </select>
        </label>
      )}
      {controls.modeChoices.length > 0 && (
        <label className="candidate-composer-field">
          <span>模式</span>
          <select
            disabled={disabledByConnection || !candidate.enabled}
            onChange={(event) => setState((current) => ({ ...current, mode: event.currentTarget.value || undefined }))}
            value={state.mode ?? ""}
          >
            {controls.modeChoices.map((choice) => (
              <option key={choice.id} value={choice.id}>{choiceLabel(choice)}</option>
            ))}
          </select>
        </label>
      )}
      {controls.destinationChoices.length > 0 && (
        <label className="candidate-composer-field">
          <span>位置</span>
          <select
            disabled={disabledByConnection || !candidate.enabled}
            onChange={(event) => setState((current) => ({ ...current, destinationId: event.currentTarget.value || undefined }))}
            value={state.destinationId ?? ""}
          >
            {!controls.destinationRequired && <option value="">不选择</option>}
            {controls.destinationChoices.map((choice) => (
              <option key={choice.id} value={choice.id}>{choiceLabel(choice)}</option>
            ))}
          </select>
        </label>
      )}
      {controls.targetGroups.map((group) => (
        <label className="candidate-composer-field" key={group.key}>
          <span>{group.label}</span>
          <select
            disabled={disabledByConnection || !candidate.enabled}
            onChange={(event) => setState((current) => ({
              ...current,
              targetIdsByGroup: {
                ...current.targetIdsByGroup,
                [group.key]: event.currentTarget.value
              }
            }))}
            value={state.targetIdsByGroup[group.key] ?? ""}
          >
            {!group.required && <option value="">不选择</option>}
            {group.choices.map((choice) => (
              <option key={choice.id} value={choice.id}>{choiceLabel(choice)}</option>
            ))}
          </select>
        </label>
      ))}
      {controls.optionalCostChoices.length > 0 && (
        <div className="candidate-composer-costs">
          <strong>费用 / 追加选项</strong>
          {controls.optionalCostChoices.map((choice) => {
            const locked = controls.requiredOptionalCostIds.includes(choice.id);
            const checked = locked || state.optionalCostIds.includes(choice.id);
            return (
              <label className="candidate-composer-check" key={choice.id}>
                <input
                  checked={checked}
                  disabled={disabledByConnection || !candidate.enabled || locked}
                  onChange={(event) => setState((current) => ({
                    ...current,
                    optionalCostIds: event.currentTarget.checked
                      ? uniqueStrings([...current.optionalCostIds, choice.id])
                      : current.optionalCostIds.filter((id) => id !== choice.id)
                  }))}
                  type="checkbox"
                />
                <span>{choiceLabel(choice)}{locked ? "（必需）" : ""}</span>
              </label>
            );
          })}
        </div>
      )}
      <CandidateCommandPreview
        canSubmit={canSubmit}
        controls={controls}
        state={state}
      />
      <Button
        disabled={!canSubmit}
        icon={<Send size={16} />}
        onClick={() => {
          if (!command) {
            return;
          }

          onCommand(promptStampedCommand(command, prompt));
          onSubmitted?.();
        }}
        title={disabledByConnection ? "连接恢复前不能提交行动" : promptReasonTitle(candidate.reason)}
        variant={canSubmit ? "primary" : "ghost"}
      >
        提交服务端候选
      </Button>
    </div>
  );
}

function CandidateCommandPreview({
  canSubmit,
  controls,
  state
}: {
  canSubmit: boolean;
  controls: CandidateComposerControls;
  state: CandidateComposerState;
}) {
  const sourceLabel = choiceLabelById(controls.sources, state.sourceId) ?? "未选择";
  const modeLabel = choiceLabelById(controls.modeChoices, state.mode);
  const destinationLabel = choiceLabelById(controls.destinationChoices, state.destinationId);
  const targetLabels = controls.targetGroups
    .map((group) => choiceLabelById(group.choices, state.targetIdsByGroup[group.key]))
    .filter((label): label is string => Boolean(label));
  const costLabels = uniqueStrings([
    ...controls.requiredOptionalCostIds
      .map((costId) => choiceLabelById(controls.optionalCostChoices, costId) ?? costId),
    ...state.optionalCostIds
      .map((costId) => choiceLabelById(controls.optionalCostChoices, costId) ?? costId)
  ]);

  return (
    <div className="candidate-command-preview" role="group" aria-label="候选提交摘要">
      <div>
        <strong>提交摘要</strong>
        <StatusPill tone={canSubmit ? "warn" : "neutral"}>{canSubmit ? "可送服务端" : "缺少选择"}</StatusPill>
      </div>
      <span>来源：{sourceLabel}</span>
      {modeLabel && <span>模式：{modeLabel}</span>}
      {destinationLabel && <span>位置：{destinationLabel}</span>}
      <span>目标：{targetLabels.length > 0 ? targetLabels.join("、") : "无"}</span>
      <span>费用：{costLabels.length > 0 ? costLabels.join("、") : "无"}</span>
    </div>
  );
}

export function candidateComposerKey(candidate: Pick<ActionPromptCandidateDto, "action" | "label">): string {
  return `${candidate.action}::${candidate.label}`;
}

function buildCandidateComposerModel(candidate: ActionPromptCandidateDto): CandidateComposerModel {
  const sourceRequirements = recordArrayMetadata(candidate.metadata?.sourceRequirements);
  const sourceRequirementById = new Map<string, Record<string, unknown>>();
  for (const requirement of sourceRequirements) {
    const sourceObjectId = stringMetadata(requirement, "sourceObjectId");
    if (sourceObjectId) {
      sourceRequirementById.set(sourceObjectId, requirement);
    }
  }

  const resetKey = [
    candidate.action,
    candidate.label,
    (candidate.sources ?? []).map((choice) => choice.id).join("|"),
    sourceRequirements
      .map((requirement) => [
        stringMetadata(requirement, "sourceObjectId"),
        stringMetadata(requirement, "cardNo") ?? stringMetadata(requirement, "equipmentCardNo"),
        stringMetadata(requirement, "abilityId"),
        choiceArrayMetadata(requirement.destinationChoices).map((choice) => choice.id).join(","),
        targetChoiceGroupCount(requirement.targetChoicesByIndex),
        choiceArrayMetadata(requirement.optionalCostChoices).map((choice) => choice.id).join(",")
      ].filter(Boolean).join(":"))
      .join("|")
  ].join("::");

  return { resetKey, sourceRequirements, sourceRequirementById };
}

function initialComposerState(
  candidate: ActionPromptCandidateDto,
  model: CandidateComposerModel,
  forcedSourceId?: string,
  selectionDraft?: CandidateSelectionDraft
): CandidateComposerState {
  const fallbackSourceId = forcedSourceId
    ?? selectionDraft?.sourceObjectId
    ?? candidate.sources?.[0]?.id
    ?? firstRequirementSourceId(model);
  const requirement = selectedRequirement(model, fallbackSourceId);
  const controls = composerControls(candidate, model, requirement, forcedSourceId);
  const sourceId = forcedSourceId
    ?? fallbackSourceId
    ?? controls.sources[0]?.id;
  const destinationId = controls.destinationChoices[0]?.id;
  const targetIdsByGroup: Record<string, string> = {};
  for (const group of controls.targetGroups) {
    if (group.required && group.choices[0]?.id) {
      targetIdsByGroup[group.key] = group.choices[0].id;
    }
  }

  if (selectionDraft?.candidateKey === candidateComposerKey(candidate)
    && selectionDraft.sourceObjectId === sourceId) {
    for (const [index, targetChoiceId] of selectionDraft.targetChoiceIds.entries()) {
      const group = controls.targetGroups.find((candidateGroup) =>
        !targetIdsByGroup[candidateGroup.key]
        && candidateGroup.choices.some((choice) => choice.id === targetChoiceId))
        ?? controls.targetGroups[index];
      if (group?.choices.some((choice) => choice.id === targetChoiceId)) {
        targetIdsByGroup[group.key] = targetChoiceId;
      }
    }
  }

  return {
    destinationId: selectionDraft?.candidateKey === candidateComposerKey(candidate)
      && controls.destinationChoices.some((choice) => choice.id === selectionDraft.destinationId)
      ? selectionDraft.destinationId
      : destinationId,
    mode: selectionDraft?.candidateKey === candidateComposerKey(candidate)
      && controls.modeChoices.some((choice) => choice.id === selectionDraft.mode)
      ? selectionDraft.mode
      : controls.modeChoices[0]?.id,
    optionalCostIds: selectionDraft?.candidateKey === candidateComposerKey(candidate)
      ? uniqueStrings(selectionDraft.optionalCostIds.filter((id) => controls.optionalCostChoices.some((choice) => choice.id === id)))
      : [],
    sourceId,
    targetIdsByGroup
  };
}

function selectedRequirement(
  model: CandidateComposerModel,
  sourceId: string | undefined
): Record<string, unknown> | undefined {
  if (sourceId) {
    const requirement = model.sourceRequirementById.get(sourceId);
    if (requirement) {
      return requirement;
    }
  }

  return model.sourceRequirements[0];
}

function composerControls(
  candidate: ActionPromptCandidateDto,
  model: CandidateComposerModel,
  requirement: Record<string, unknown> | undefined,
  forcedSourceObjectId?: string
): CandidateComposerControls {
  const minTargetCount = requirement ? firstNumberMetadata(requirement, ["minTargetCount", "requiredTargetCount"]) ?? 0 : 0;
  const minDefenderCount = requirement ? firstNumberMetadata(requirement, ["minDefenderCount"]) ?? 0 : 0;
  const sources = sourceChoicesForCandidate(candidate, model, forcedSourceObjectId);
  const modeChoices = uniqueChoices([
    ...(candidate.modes ?? []),
    ...modeChoiceForRequirement(requirement)
  ]);
  const destinationChoices = uniqueChoices([
    ...(candidate.destinations ?? []),
    ...choiceArrayMetadata(requirement?.destinationChoices),
    ...choiceArrayMetadata(requirement?.battlefieldChoices)
  ]);
  const targetGroups = targetGroupsForRequirement(candidate, requirement, minTargetCount, minDefenderCount);
  const requiredOptionalCostIds = uniqueStrings([
    ...(stringArrayFromValue(requirement?.requiredOptionalCosts) ?? [])
  ]);
  const optionalCostChoices = uniqueChoices([
    ...(candidate.optionalCosts ?? []),
    ...choiceArrayMetadata(requirement?.optionalCostChoices),
    ...choiceArrayMetadata(requirement?.additionalCostChoices),
    ...choiceArrayMetadata(requirement?.paymentResourceChoices),
    ...requiredOptionalCostIds.map((id) => ({ id, label: id }))
  ]);

  return {
    destinationChoices,
    destinationRequired: destinationChoices.length > 0,
    modeChoices,
    optionalCostChoices,
    requiredOptionalCostIds,
    sourceRequired: true,
    sources,
    targetGroups
  };
}

function sourceChoicesForCandidate(
  candidate: ActionPromptCandidateDto,
  model: CandidateComposerModel,
  forcedSourceObjectId: string | undefined
): ActionPromptChoiceDto[] {
  const requirementChoices = model.sourceRequirements
    .map((requirement) => {
      const id = stringMetadata(requirement, "sourceObjectId");
      if (!id) {
        return undefined;
      }

      return {
        id,
        label: firstStringFromRecord(requirement, ["displayName", "cardNo", "equipmentCardNo", "sourceObjectId"]) ?? id
      };
    })
    .filter((choice): choice is ActionPromptChoiceDto => Boolean(choice));
  const choices = uniqueChoices([...(candidate.sources ?? []), ...requirementChoices]);
  return forcedSourceObjectId
    ? choices.filter((choice) => choice.id === forcedSourceObjectId)
    : choices;
}

function modeChoiceForRequirement(requirement: Record<string, unknown> | undefined): ActionPromptChoiceDto[] {
  if (!requirement) {
    return [];
  }

  const mode = stringMetadata(requirement, "mode");
  if (!mode) {
    return [];
  }

  return [{ id: mode, label: stringMetadata(requirement, "modeLabel") ?? mode }];
}

function targetGroupsForRequirement(
  candidate: ActionPromptCandidateDto,
  requirement: Record<string, unknown> | undefined,
  minTargetCount: number,
  minDefenderCount: number
): ChoiceGroup[] {
  const indexedTargetGroups = choiceGroupsByIndex(
    requirement?.targetChoicesByIndex,
    candidate.action === "DECLARE_BATTLE" ? minDefenderCount : minTargetCount,
    candidate.action === "DECLARE_BATTLE" ? "防守方" : "目标"
  );
  if (indexedTargetGroups.length > 0) {
    return indexedTargetGroups;
  }

  const targetChoices = uniqueChoices([
    ...choiceArrayMetadata(requirement?.targetChoices),
    ...(candidate.targets ?? [])
  ]);
  if (targetChoices.length === 0) {
    return [];
  }

  return [{
    choices: targetChoices,
    key: "target-0",
    label: candidate.action === "DECLARE_BATTLE" ? "防守方 1" : "目标 1",
    required: candidate.action === "ASSEMBLE_EQUIPMENT"
      || candidate.action === "DECLARE_BATTLE"
      || minTargetCount > 0
      || minDefenderCount > 0
  }];
}

function choiceGroupsByIndex(value: unknown, requiredCount: number, labelPrefix: string): ChoiceGroup[] {
  if (!isRecord(value)) {
    return [];
  }

  return Object.entries(value)
    .map(([key, rawChoices]) => ({ key, choices: choiceArrayMetadata(rawChoices) }))
    .filter((group) => group.choices.length > 0)
    .sort((left, right) => numericKey(left.key) - numericKey(right.key))
    .map((group, index) => ({
      choices: group.choices,
      key: `${labelPrefix}-${group.key}`,
      label: `${labelPrefix} ${index + 1}`,
      required: index < requiredCount
    }));
}

function composerCommand(
  candidate: ActionPromptCandidateDto,
  snapshot: SnapshotDto | undefined,
  state: CandidateComposerState,
  requirement: Record<string, unknown> | undefined,
  targetObjectIds: string[],
  optionalCostIds: string[]
): GameCommand | undefined {
  const sourceObjectId = state.sourceId;
  const cardNo = cardNoForRequirement(requirement, snapshot, sourceObjectId);
  const optionalCosts = optionalCostIds.length > 0 ? optionalCostIds : undefined;
  const destination = state.destinationId || undefined;
  const mode = state.mode || undefined;
  const templatedCommand = commandFromTemplate(
    candidate.commandTemplate,
    state,
    requirement,
    targetObjectIds,
    optionalCostIds
  );
  if (templatedCommand) {
    return templatedCommand;
  }

  switch (candidate.action) {
    case "PLAY_CARD":
      return sourceObjectId && cardNo
        ? { cmdType: "PLAY_CARD", sourceObjectId, cardNo, targetObjectIds, mode, destination, optionalCosts }
        : undefined;
    case "HIDE_CARD":
      return sourceObjectId && cardNo
        ? { cmdType: "HIDE_CARD", sourceObjectId, cardNo, destination, optionalCosts }
        : undefined;
    case "REVEAL_CARD":
      return sourceObjectId && cardNo
        ? { cmdType: "REVEAL_CARD", sourceObjectId, cardNo, mode, destination, targetObjectIds, optionalCosts }
        : undefined;
    case "MOVE_UNIT":
      return sourceObjectId
        ? { cmdType: "MOVE_UNIT", sourceObjectId, origin: stringMetadata(requirement ?? {}, "origin"), destination, optionalCosts }
        : undefined;
    case "ASSEMBLE_EQUIPMENT":
      return sourceObjectId
        ? { cmdType: "ASSEMBLE_EQUIPMENT", sourceObjectId, targetObjectId: targetObjectIds[0], optionalCosts }
        : undefined;
    case "DECLARE_BATTLE":
      return sourceObjectId
        ? {
            cmdType: "DECLARE_BATTLE",
            attackerObjectIds: [sourceObjectId],
            battlefieldId: destination,
            battlefieldTargetObjectIds: destination ? [destination] : undefined,
            defenderObjectIds: targetObjectIds,
            optionalCosts
          }
        : undefined;
    case "ACTIVATE_ABILITY":
      return sourceObjectId && stringMetadata(requirement ?? {}, "abilityId")
        ? {
            cmdType: "ACTIVATE_ABILITY",
            sourceObjectId,
            abilityId: stringMetadata(requirement ?? {}, "abilityId")!,
            targetObjectIds,
            optionalCosts
          }
        : undefined;
    case "LEGEND_ACT":
      return sourceObjectId && stringMetadata(requirement ?? {}, "abilityId")
        ? {
            cmdType: "LEGEND_ACT",
            sourceObjectId,
            abilityId: stringMetadata(requirement ?? {}, "abilityId")!,
            targetObjectIds,
            optionalCosts
          }
        : undefined;
    default:
      return undefined;
  }
}

function commandFromTemplate(
  template: ActionPromptCommandTemplateDto | null | undefined,
  state: CandidateComposerState,
  requirement: Record<string, unknown> | undefined,
  targetObjectIds: string[],
  optionalCostIds: string[]
): GameCommand | undefined {
  if (!template?.cmdType || !Array.isArray(template.bindings)) {
    return undefined;
  }

  const command: Record<string, unknown> = { cmdType: template.cmdType };
  for (const binding of template.bindings) {
    const value = commandTemplateValue(binding, state, requirement, targetObjectIds, optionalCostIds);
    if (isMissingCommandTemplateValue(value)) {
      if (binding.required) {
        return undefined;
      }
      if (binding.omitEmpty !== false) {
        continue;
      }
    }

    command[binding.field] = value;
  }

  return command as GameCommand;
}

function commandTemplateValue(
  binding: ActionPromptCommandTemplateBindingDto,
  state: CandidateComposerState,
  requirement: Record<string, unknown> | undefined,
  targetObjectIds: string[],
  optionalCostIds: string[]
): string | string[] | undefined {
  const rawValue = commandTemplateRawValue(binding, state, requirement, targetObjectIds, optionalCostIds);
  if (binding.asArray) {
    if (Array.isArray(rawValue)) {
      return rawValue;
    }

    return typeof rawValue === "string" && rawValue.length > 0 ? [rawValue] : [];
  }

  return rawValue;
}

function commandTemplateRawValue(
  binding: ActionPromptCommandTemplateBindingDto,
  state: CandidateComposerState,
  requirement: Record<string, unknown> | undefined,
  targetObjectIds: string[],
  optionalCostIds: string[]
): string | string[] | undefined {
  switch (binding.source) {
    case "selectedSource":
      return state.sourceId;
    case "selectedTarget":
      return targetObjectIds[0];
    case "selectedTargets":
      return targetObjectIds;
    case "selectedDestination":
      return state.destinationId;
    case "selectedMode":
      return state.mode;
    case "selectedOptionalCosts":
      return optionalCostIds;
    case "requirementMetadata":
      return commandTemplateRequirementValue(binding, requirement);
    default:
      return undefined;
  }
}

function commandTemplateRequirementValue(
  binding: ActionPromptCommandTemplateBindingDto,
  requirement: Record<string, unknown> | undefined
): string | undefined {
  if (!requirement) {
    return undefined;
  }

  const keys = [
    ...(binding.metadataKey ? [binding.metadataKey] : []),
    ...(Array.isArray(binding.metadataKeys) ? binding.metadataKeys : [])
  ];
  for (const key of keys) {
    const value = stringMetadata(requirement, key);
    if (value) {
      return value;
    }
  }

  return undefined;
}

function isMissingCommandTemplateValue(value: string | string[] | undefined): boolean {
  return value == null
    || (typeof value === "string" && value.trim().length === 0)
    || (Array.isArray(value) && value.length === 0);
}

function cardNoForRequirement(
  requirement: Record<string, unknown> | undefined,
  snapshot: SnapshotDto | undefined,
  sourceObjectId: string | undefined
): string | undefined {
  return (requirement && (stringMetadata(requirement, "cardNo") ?? stringMetadata(requirement, "equipmentCardNo")))
    ?? (sourceObjectId ? findCardNo(snapshot, sourceObjectId) : undefined);
}

function findCardNo(snapshot: SnapshotDto | undefined, objectId: string): string | undefined {
  return findObject(snapshot, objectId)?.cardNo ?? undefined;
}

function findObject(snapshot: SnapshotDto | undefined, objectId: string) {
  if (!snapshot) {
    return undefined;
  }

  for (const player of Object.values(snapshot.players)) {
    const cardObject = player.objects?.[objectId];
    if (cardObject) {
      return cardObject;
    }
  }

  return undefined;
}

function targetChoiceGroupCount(value: unknown): number {
  if (!isRecord(value)) {
    return 0;
  }

  return Object.values(value).filter((choices) => choiceArrayMetadata(choices).length > 0).length;
}

function firstRequirementSourceId(model: CandidateComposerModel): string | undefined {
  for (const requirement of model.sourceRequirements) {
    const sourceObjectId = stringMetadata(requirement, "sourceObjectId");
    if (sourceObjectId) {
      return sourceObjectId;
    }
  }
  return undefined;
}

function numericKey(value: string): number {
  const parsed = Number.parseInt(value, 10);
  return Number.isFinite(parsed) ? parsed : Number.MAX_SAFE_INTEGER;
}

function uniqueChoices(choices: ActionPromptChoiceDto[]): ActionPromptChoiceDto[] {
  const byId = new Map<string, ActionPromptChoiceDto>();
  for (const choice of choices) {
    if (!choice.id || byId.has(choice.id)) {
      continue;
    }
    byId.set(choice.id, {
      id: choice.id,
      label: choice.label || choice.id,
      objectIds: normalizedObjectIds(choice.objectIds),
      reason: choice.reason
    });
  }
  return [...byId.values()];
}

function uniqueStrings(values: string[]): string[] {
  return [...new Set(values.filter((value) => value.trim().length > 0))];
}

function booleanFromRecord(record: Record<string, unknown>, key: string, fallback: boolean): boolean {
  const value = record[key];
  return typeof value === "boolean" ? value : fallback;
}

function choiceLabel(choice: ActionPromptChoiceDto): string {
  return redactInternalText(choice.label || choice.id || "服务端选项");
}

function choiceLabelById(choices: ActionPromptChoiceDto[], id: string | undefined): string | undefined {
  if (!id) {
    return undefined;
  }

  const choice = choices.find((candidate) => candidate.id === id);
  return choice ? choiceLabel(choice) : undefined;
}

function choiceArrayMetadata(value: unknown): ActionPromptChoiceDto[] {
  const choices: ActionPromptChoiceDto[] = [];
  for (const record of recordArrayMetadata(value)) {
    const id = firstStringFromRecord(record, ["id", "triggerId", "choiceId"]);
    if (id) {
      choices.push({
        id,
        label: firstStringFromRecord(record, ["label", "summary", "visibleText", "text", "title", "name"]) ?? id,
        objectIds: stringArrayFromValue(record.objectIds),
        reason: firstStringFromRecord(record, ["reason", "description"])
      });
    }
  }

  return choices;
}

function normalizedObjectIds(objectIds: string[] | null | undefined): string[] | undefined {
  if (!Array.isArray(objectIds)) {
    return undefined;
  }

  const normalized = objectIds
    .map((objectId) => objectId.trim())
    .filter(Boolean);
  return normalized.length > 0 ? [...new Set(normalized)] : undefined;
}

function recordArrayMetadata(value: unknown): Array<Record<string, unknown>> {
  if (Array.isArray(value)) {
    return value.filter(isRecord);
  }

  if (isRecord(value)) {
    return Object.values(value).filter(isRecord);
  }

  return [];
}

function numberMetadata(metadata: Record<string, unknown> | null | undefined, key: string): number | undefined {
  const value = metadata?.[key];
  return typeof value === "number" && Number.isFinite(value) ? value : undefined;
}

function firstNumberMetadata(metadata: Record<string, unknown>, keys: string[]): number | undefined {
  for (const key of keys) {
    const value = numberMetadata(metadata, key);
    if (value != null) {
      return value;
    }
  }
  return undefined;
}

function stringMetadata(metadata: Record<string, unknown>, key: string): string | undefined {
  const value = metadata[key];
  return typeof value === "string" && value.trim().length > 0 ? value.trim() : undefined;
}

function stringArrayFromValue(value: unknown, requireNonEmpty = false): string[] | undefined {
  if (!Array.isArray(value)) {
    return undefined;
  }

  const values = value.map((item) => typeof item === "string" ? item.trim() : "");
  return (!requireNonEmpty || values.length > 0) && values.every((item) => item.length > 0) ? values : undefined;
}

function safePromptSummary(value: unknown): string | undefined {
  if (typeof value === "string") {
    return safeOptionalText(value);
  }

  if (typeof value === "number" || typeof value === "boolean") {
    return String(value);
  }

  if (Array.isArray(value)) {
    const stringValues = value
      .filter((item): item is string => typeof item === "string" && item.trim().length > 0)
      .map((item) => safeOptionalText(item))
      .filter((item): item is string => item != null);
    return stringValues.length > 0 ? stringValues.slice(0, 3).join("、") : `${value.length} 项`;
  }

  if (isRecord(value)) {
    const label = firstStringFromRecord(value, ["label", "summary", "visibleText", "text", "description"]);
    return safeOptionalText(label) ?? `${Object.keys(value).length} 项`;
  }

  return undefined;
}

function safeOptionalText(value: string | undefined): string | undefined {
  if (!value?.trim()) {
    return undefined;
  }

  return redactInternalText(value);
}

function firstStringFromRecord(record: Record<string, unknown>, keys: string[]): string | undefined {
  for (const key of keys) {
    const value = record[key];
    if (typeof value === "string" && value.trim().length > 0) {
      return value.trim();
    }
  }
  return undefined;
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return Boolean(value) && typeof value === "object" && !Array.isArray(value);
}

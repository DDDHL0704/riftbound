import type { ActionPromptDto, ActionPromptObjectCandidateStepDto, ActionPromptSelectionStepDto } from "../types/protocol";
import {
  promptChoiceRoleFromString,
  promptChoiceRoleLabel
} from "./promptInteraction";
import type {
  TableObjectCandidateStepContext,
  TableObjectContext
} from "./tableObjectContext";

export type WireActionSyntaxSource = "object-context" | "prompt-derived" | "server-flow";

export type WireActionSyntaxState =
  | "missing-required"
  | "usable-optional"
  | "usable-required";

export type WireActionSyntaxRow = {
  candidateLabel: string;
  choiceCount: number;
  key: string;
  objectChoiceCount: number;
  required: boolean;
  role: string;
  roleLabel: string;
  source: WireActionSyntaxSource;
  sourceLabel: string;
  state: WireActionSyntaxState;
  stateLabel: string;
};

export type WireActionSyntaxPlan = {
  missingRequiredCount: number;
  rows: WireActionSyntaxRow[];
  summary: string;
  usableCount: number;
};

type WireActionSyntaxStep = Pick<ActionPromptObjectCandidateStepDto | TableObjectCandidateStepContext,
  "choiceCount" | "index" | "label" | "objectChoiceCount" | "required" | "role">;

export function buildWireActionSyntaxPlanForObject({
  objectId,
  prompt
}: {
  objectId?: string;
  prompt?: ActionPromptDto;
}): WireActionSyntaxPlan {
  const normalizedObjectId = objectId?.trim();
  if (!normalizedObjectId) {
    return buildWireActionSyntaxPlanFromRows([]);
  }

  const serverFlowRows = (prompt?.serverFlow?.relatedObjects ?? [])
    .filter((ref) => ref.objectId === normalizedObjectId)
    .flatMap((ref, refIndex) =>
      wireActionSyntaxRowsFromSteps({
        candidateLabel: uniqueStrings([ref.role, ...(ref.candidateRoles ?? [])]).join(" / ") || "服务端流程",
        keyPrefix: `server-flow:${refIndex}`,
        source: "server-flow",
        steps: ref.candidateSteps
      }));
  const objectContextRows = (prompt?.objectContexts ?? [])
    .filter((context) => context.objectId === normalizedObjectId)
    .flatMap((context) =>
      context.candidates.flatMap((candidate, candidateIndex) =>
        wireActionSyntaxRowsFromSteps({
          candidateLabel: candidate.label?.trim() || candidate.action,
          keyPrefix: `object-context:${candidateIndex}:${candidate.action}`,
          source: "object-context",
          steps: candidate.selectionSteps
        })));

  return buildWireActionSyntaxPlanFromRows([...serverFlowRows, ...objectContextRows]);
}

export function buildWireActionSyntaxPlanForPrompt({
  prompt
}: {
  prompt?: ActionPromptDto;
}): WireActionSyntaxPlan {
  const rows = (prompt?.candidates ?? []).flatMap((candidate, candidateIndex) =>
    wireActionSyntaxRowsFromPromptSteps({
      candidateLabel: candidate.label?.trim() || candidate.action,
      keyPrefix: `prompt:${candidateIndex}:${candidate.action}`,
      source: "prompt-derived",
      steps: candidate.selectionSteps
    }));

  const plan = buildWireActionSyntaxPlanFromRows(rows);
  return {
    ...plan,
    summary: wirePromptActionSyntaxSummary(plan.rows)
  };
}

export function buildWireActionSyntaxPlanFromTableContext(
  context?: TableObjectContext
): WireActionSyntaxPlan {
  if (!context) {
    return buildWireActionSyntaxPlanFromRows([]);
  }

  const source: WireActionSyntaxSource = context.candidateSource === "derived"
    ? "prompt-derived"
    : "object-context";
  const rows = context.candidateLinks.flatMap((candidate, candidateIndex) =>
    wireActionSyntaxRowsFromSteps({
      candidateLabel: candidate.label,
      keyPrefix: `table-context:${context.objectId}:${candidateIndex}:${candidate.commandType ?? candidate.label}`,
      source,
      steps: candidate.selectionSteps
    }));

  return buildWireActionSyntaxPlanFromRows(rows);
}

export function buildWireActionSyntaxPlanFromRows(
  rows: WireActionSyntaxRow[]
): WireActionSyntaxPlan {
  const sortedRows = [...rows].sort(wireActionSyntaxRowSort);
  return {
    missingRequiredCount: sortedRows.filter((row) => row.state === "missing-required").length,
    rows: sortedRows,
    summary: wireActionSyntaxSummary(sortedRows),
    usableCount: sortedRows.filter((row) => row.state === "usable-optional" || row.state === "usable-required").length
  };
}

function wireActionSyntaxRowsFromSteps({
  candidateLabel,
  keyPrefix,
  source,
  steps
}: {
  candidateLabel: string;
  keyPrefix: string;
  source: WireActionSyntaxSource;
  steps: readonly WireActionSyntaxStep[] | null | undefined;
}): WireActionSyntaxRow[] {
  return (steps ?? [])
    .filter((step) => step.required || finiteCountValue(step.objectChoiceCount) > 0)
    .map((step) => {
      const state = wireActionSyntaxState(step);
      const roleLabel = step.label?.trim() || wireActionSyntaxRoleLabel(step.role);
      return {
        candidateLabel,
        choiceCount: finiteCountValue(step.choiceCount),
        key: `syntax:${keyPrefix}:${step.index}:${step.role}`,
        objectChoiceCount: finiteCountValue(step.objectChoiceCount),
        required: Boolean(step.required),
        role: step.role,
        roleLabel,
        source,
        sourceLabel: wireActionSyntaxSourceLabel(source),
        state,
        stateLabel: wireActionSyntaxStateLabel(state)
      };
    });
}

function wireActionSyntaxRowsFromPromptSteps({
  candidateLabel,
  keyPrefix,
  source,
  steps
}: {
  candidateLabel: string;
  keyPrefix: string;
  source: WireActionSyntaxSource;
  steps: readonly ActionPromptSelectionStepDto[] | null | undefined;
}): WireActionSyntaxRow[] {
  return (steps ?? [])
    .filter((step) => step.required || uniqueChoiceCount(step) > 0)
    .map((step, stepIndex) => {
      const choiceCount = uniqueChoiceCount(step);
      const state = wireActionSyntaxState({
        choiceCount,
        index: stepIndex,
        label: step.label,
        objectChoiceCount: choiceCount,
        required: step.required,
        role: step.role
      });
      const roleLabel = step.label?.trim() || wireActionSyntaxRoleLabel(step.role);
      return {
        candidateLabel,
        choiceCount,
        key: `syntax:${keyPrefix}:${stepIndex}:${step.role}`,
        objectChoiceCount: choiceCount,
        required: Boolean(step.required),
        role: step.role,
        roleLabel,
        source,
        sourceLabel: wireActionSyntaxSourceLabel(source),
        state,
        stateLabel: wireActionSyntaxStateLabel(state)
      };
    });
}

function wireActionSyntaxState(step: WireActionSyntaxStep): WireActionSyntaxState {
  if (finiteCountValue(step.objectChoiceCount) > 0 && step.required) {
    return "usable-required";
  }

  if (finiteCountValue(step.objectChoiceCount) > 0) {
    return "usable-optional";
  }

  return "missing-required";
}

function wireActionSyntaxStateLabel(state: WireActionSyntaxState): string {
  switch (state) {
    case "missing-required":
      return "还需其他对象";
    case "usable-optional":
      return "可作为可选";
    case "usable-required":
      return "可承担必选";
  }
}

function wireActionSyntaxSourceLabel(source: WireActionSyntaxSource): string {
  switch (source) {
    case "object-context":
      return "对象上下文";
    case "prompt-derived":
      return "公开候选";
    case "server-flow":
      return "服务端流程";
  }
}

function wireActionSyntaxRoleLabel(role: string): string {
  const knownRole = promptChoiceRoleFromString(role);
  return knownRole ? promptChoiceRoleLabel(knownRole) : role;
}

function wireActionSyntaxRowSort(left: WireActionSyntaxRow, right: WireActionSyntaxRow): number {
  return wireActionSyntaxSourceRank(left.source) - wireActionSyntaxSourceRank(right.source)
    || wireActionSyntaxStateRank(left.state) - wireActionSyntaxStateRank(right.state)
    || left.roleLabel.localeCompare(right.roleLabel)
    || left.candidateLabel.localeCompare(right.candidateLabel);
}

function wireActionSyntaxSourceRank(source: WireActionSyntaxSource): number {
  switch (source) {
    case "server-flow":
      return 0;
    case "object-context":
      return 1;
    case "prompt-derived":
      return 2;
  }
}

function wireActionSyntaxStateRank(state: WireActionSyntaxState): number {
  switch (state) {
    case "usable-required":
      return 0;
    case "usable-optional":
      return 1;
    case "missing-required":
      return 2;
  }
}

function wireActionSyntaxSummary(rows: WireActionSyntaxRow[]): string {
  if (rows.length === 0) {
    return "服务端未公开该对象的候选语法。";
  }

  const usableRoles = uniqueStrings(rows
    .filter((row) => row.state === "usable-optional" || row.state === "usable-required")
    .map((row) => row.roleLabel));
  const missingRoles = uniqueStrings(rows
    .filter((row) => row.state === "missing-required")
    .map((row) => row.roleLabel));
  const usableLabel = usableRoles.length > 0 ? `可作为 ${usableRoles.join(" / ")}` : "当前对象不可直接填入候选角色";
  const missingLabel = missingRoles.length > 0 ? `还需 ${missingRoles.join(" / ")}` : "必选步骤已由当前对象覆盖或服务端未要求";
  return `${usableLabel}；${missingLabel}`;
}

function wirePromptActionSyntaxSummary(rows: WireActionSyntaxRow[]): string {
  if (rows.length === 0) {
    return "服务端未公开当前 prompt 的选择语法。";
  }

  const usableRoles = uniqueStrings(rows
    .filter((row) => row.state === "usable-optional" || row.state === "usable-required")
    .map((row) => row.roleLabel));
  const missingRoles = uniqueStrings(rows
    .filter((row) => row.state === "missing-required")
    .map((row) => row.roleLabel));
  const usableLabel = usableRoles.length > 0 ? `可选择 ${usableRoles.join(" / ")}` : "当前 prompt 暂无可选步骤";
  const missingLabel = missingRoles.length > 0 ? `还需 ${missingRoles.join(" / ")}` : "必选步骤已有服务端候选";
  return `${usableLabel}；${missingLabel}`;
}

function finiteCountValue(value: number | null | undefined): number {
  return typeof value === "number" && Number.isFinite(value) ? Math.max(0, Math.floor(value)) : 0;
}

function uniqueChoiceCount(step: ActionPromptSelectionStepDto): number {
  return new Set((step.choices ?? []).map((choice) => choice.id).filter(Boolean)).size;
}

function uniqueStrings(values: string[]): string[] {
  return Array.from(new Set(values.map((value) => value.trim()).filter(Boolean)));
}

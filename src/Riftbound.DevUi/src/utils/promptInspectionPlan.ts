import type { ActionPromptDto, ActionPromptInspectionGroupDto, ActionPromptInspectionRowDto } from "../types/protocol";

export type PromptInspectionTone = "bad" | "good" | "info" | "neutral" | "warn";

export type PromptInspectionRowPlan = {
  key: string;
  label: string;
  value: string;
  tone?: PromptInspectionTone;
};

export type PromptInspectionGroupPlan = {
  emptyLabel?: string;
  key: string;
  rows: PromptInspectionRowPlan[];
  title: string;
};

export type PromptInspectionPlan = {
  boundaryLabel: string;
  groups: PromptInspectionGroupPlan[];
  sourceLabel: string;
  summaryRows: PromptInspectionRowPlan[];
};

export function buildPromptInspectionPlan({
  candidateCount,
  enabledCandidateCount,
  prompt
}: {
  candidateCount?: number;
  enabledCandidateCount?: number;
  prompt?: ActionPromptDto;
}): PromptInspectionPlan {
  const inspection = prompt?.inspection;
  if (inspection) {
    return {
      boundaryLabel: inspection.boundary,
      groups: inspection.groups.map(inspectionGroupFromServer),
      sourceLabel: promptInspectionSourceLabel(inspection.source),
      summaryRows: inspection.summaryRows.map(inspectionRowFromServer)
    };
  }

  const actualCandidateCount = candidateCount ?? prompt?.candidates?.length ?? 0;
  const actualEnabledCount = enabledCandidateCount ?? (prompt?.candidates ?? []).filter((candidate) => candidate.enabled).length;
  const disabledCount = Math.max(0, actualCandidateCount - actualEnabledCount);
  return {
    boundaryLabel: "前端仅汇总当前 prompt 的公开字段；合法性仍以后端候选和提交校验为准。",
    groups: [
      {
        emptyLabel: "当前窗口没有公开候选。",
        key: "candidate",
        rows: (prompt?.candidates ?? []).slice(0, 6).map((candidate, index) => ({
          key: `candidate-${index}`,
          label: candidate.enabled ? "可提交" : "阻断",
          tone: candidate.enabled ? "good" : "warn",
          value: [candidate.action, candidate.enabled ? "" : candidate.reason].filter(Boolean).join(" / ")
        })),
        title: "服务端候选"
      },
      {
        key: "safe-boundary",
        rows: [
          { key: "candidate-source", label: "合法性", value: "以服务端候选和提交校验为准" },
          { key: "frontend", label: "前端职责", value: "展示与提交，不重算规则" }
        ],
        title: "信息边界"
      }
    ],
    sourceLabel: "前端公开 prompt 汇总",
    summaryRows: [
      { key: "kind", label: "提示类型", value: prompt?.view?.type ?? "WAIT" },
      { key: "candidate", label: "候选", value: `${actualEnabledCount} 可提交 / ${disabledCount} 阻断` }
    ]
  };
}

function inspectionGroupFromServer(group: ActionPromptInspectionGroupDto): PromptInspectionGroupPlan {
  return {
    emptyLabel: group.emptyLabel ?? undefined,
    key: group.key,
    rows: group.rows.map(inspectionRowFromServer),
    title: group.title
  };
}

function inspectionRowFromServer(row: ActionPromptInspectionRowDto): PromptInspectionRowPlan {
  return {
    key: row.key,
    label: row.label,
    tone: toneFromServer(row.tone),
    value: row.value
  };
}

function toneFromServer(tone: string | null | undefined): PromptInspectionTone | undefined {
  switch (tone) {
    case "bad":
    case "good":
    case "info":
    case "neutral":
    case "warn":
      return tone;
    default:
      return undefined;
  }
}

function promptInspectionSourceLabel(source: string | undefined): string {
  switch (source) {
    case "server-action-prompt":
      return "服务端提示检查";
    default:
      return source?.trim() || "服务端提示检查";
  }
}

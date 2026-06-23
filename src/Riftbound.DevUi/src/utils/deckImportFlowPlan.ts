import type { DeckImportIssue, DeckImportResult, DeckSubmissionSummary } from "./starterDeck";

export type DeckImportFlowState = "empty" | "invalid" | "valid";

export type DeckImportFlowMetric = {
  label: string;
  value: string;
};

export type DeckImportFlowIssueRow = {
  field: DeckImportIssue["field"] | "empty";
  message: string;
};

export type DeckImportFlowStep = {
  detail: string;
  id: string;
  label: string;
  state: "current" | "done" | "pending";
};

export type DeckImportFlowPlan = {
  authorityBoundary: string;
  canApplyImport: boolean;
  feedbackIcon: "invalid" | "valid";
  issueRows: DeckImportFlowIssueRow[];
  metrics: DeckImportFlowMetric[];
  nextStep: string;
  state: DeckImportFlowState;
  statusLabel: string;
  statusTone: "bad" | "good" | "neutral";
  steps: DeckImportFlowStep[];
};

export function buildDeckImportFlowPlan({
  importResult,
  previewSummary
}: {
  importResult?: DeckImportResult;
  previewSummary?: DeckSubmissionSummary;
}): DeckImportFlowPlan {
  if (!importResult) {
    return {
      authorityBoundary,
      canApplyImport: false,
      feedbackIcon: "invalid",
      issueRows: [{ field: "empty", message: "粘贴后会在这里显示结构校验结果。" }],
      metrics: emptyMetrics(),
      nextStep: "粘贴 JSON 或分区文本；前端只生成 SUBMIT_DECK 结构。",
      state: "empty",
      statusLabel: "等待粘贴",
      statusTone: "neutral",
      steps: flowSteps("empty")
    };
  }

  if (!importResult.ok) {
    return {
      authorityBoundary,
      canApplyImport: false,
      feedbackIcon: "invalid",
      issueRows: importResult.issues.map((issue) => ({ field: issue.field, message: issue.message })),
      metrics: emptyMetrics(),
      nextStep: "修正结构错误；服务端合法性仍未验证。",
      state: "invalid",
      statusLabel: "结构无效",
      statusTone: "bad",
      steps: flowSteps("invalid")
    };
  }

  return {
    authorityBoundary,
    canApplyImport: true,
    feedbackIcon: "valid",
    issueRows: [],
    metrics: summaryMetrics(previewSummary, importResult.format),
    nextStep: "导入为当前构筑后，到房间提交给服务端权威验证。",
    state: "valid",
    statusLabel: "结构可导入",
    statusTone: "good",
    steps: flowSteps("valid")
  };
}

const authorityBoundary = "前端不判定卡牌数量、同名上限、颜色或规则合法性。";

function emptyMetrics(): DeckImportFlowMetric[] {
  return [
    { label: "主牌堆", value: "-" },
    { label: "符文", value: "-" },
    { label: "战场", value: "-" },
    { label: "格式", value: "-" }
  ];
}

function summaryMetrics(summary: DeckSubmissionSummary | undefined, format: "json" | "text"): DeckImportFlowMetric[] {
  if (!summary) {
    return emptyMetrics();
  }

  return [
    { label: "主牌堆", value: `${summary.mainDeck} 张 / ${summary.distinctMainDeck} 种` },
    { label: "符文", value: `${summary.runeDeck} 张 / ${summary.distinctRuneDeck} 种` },
    { label: "战场", value: `${summary.battlefields} 张 / ${summary.distinctBattlefields} 种` },
    { label: "格式", value: format.toUpperCase() }
  ];
}

function flowSteps(state: DeckImportFlowState): DeckImportFlowStep[] {
  return [
    {
      detail: "粘贴 JSON 或分区文本",
      id: "paste",
      label: "01",
      state: state === "empty" ? "current" : "done"
    },
    {
      detail: "前端只做结构反馈",
      id: "structure",
      label: "02",
      state: state === "invalid" ? "current" : state === "valid" ? "done" : "pending"
    },
    {
      detail: "提交时以服务端为准",
      id: "server",
      label: "03",
      state: state === "valid" ? "current" : "pending"
    }
  ];
}

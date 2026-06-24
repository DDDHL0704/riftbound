import type { DeckImportFlowState } from "./deckImportFlowPlan";
import type { DeckSubmissionSummary } from "./starterDeck";

export type DeckSource = "query" | "starter" | "storage";

export type DeckImportHandoffSectionId = "command" | "current" | "intake" | "recovery" | "server";

export type DeckImportHandoffState = "authority" | "blocking" | "ready" | "waiting";

export type DeckImportHandoffSource =
  | "generated-command"
  | "local-cache"
  | "local-editor"
  | "local-state"
  | "server-authority";

export type DeckImportHandoffSection = {
  detail: string;
  id: DeckImportHandoffSectionId;
  label: string;
  nextStep: string;
  source: DeckImportHandoffSource;
  state: DeckImportHandoffState;
  value: string;
};

export type DeckImportServerHandoff = {
  authority: "server-authority";
  commandType: "SUBMIT_DECK";
  requiresPrompt: true;
  requiresSnapshotTick: true;
  targetActionId: "submitDeck";
  targetSurface: "room";
};

export type DeckImportHandoffPlan = {
  activeSectionId: DeckImportHandoffSectionId;
  serverHandoff: DeckImportServerHandoff;
  sections: DeckImportHandoffSection[];
  summary: string;
};

const serverHandoff: DeckImportServerHandoff = {
  authority: "server-authority",
  commandType: "SUBMIT_DECK",
  requiresPrompt: true,
  requiresSnapshotTick: true,
  targetActionId: "submitDeck",
  targetSurface: "room"
};

export function buildDeckImportHandoffPlan({
  canApplyImport,
  commandPreviewLength,
  currentSummary,
  deckSource,
  importState,
  previewSummary
}: {
  canApplyImport: boolean;
  commandPreviewLength: number;
  currentSummary: DeckSubmissionSummary;
  deckSource: DeckSource;
  importState: DeckImportFlowState;
  previewSummary?: DeckSubmissionSummary;
}): DeckImportHandoffPlan {
  const sections: DeckImportHandoffSection[] = [
    intakeSection(importState, canApplyImport, previewSummary),
    recoverySection(deckSource),
    currentSection(currentSummary),
    commandSection(commandPreviewLength),
    serverSection(canApplyImport)
  ];

  return {
    activeSectionId: activeSection(sections),
    serverHandoff,
    sections,
    summary: `导入：${importStateLabel(importState)} / 来源：${deckSourceLabel(deckSource)} / 命令：${summaryShort(currentSummary)}`
  };
}

function intakeSection(
  importState: DeckImportFlowState,
  canApplyImport: boolean,
  previewSummary?: DeckSubmissionSummary
): DeckImportHandoffSection {
  return {
    detail: previewSummary
      ? `预览 ${summaryText(previewSummary)}；仍只完成前端结构检查。`
      : "粘贴区只做格式与 SUBMIT_DECK 结构检查。",
    id: "intake",
    label: "导入",
    nextStep: canApplyImport ? "可应用到当前构筑；之后仍需服务端验证。" : "先修正粘贴内容或载入当前构筑。",
    source: "local-editor",
    state: importState === "invalid" ? "blocking" : importState === "valid" ? "ready" : "waiting",
    value: importStateLabel(importState)
  };
}

function recoverySection(deckSource: DeckSource): DeckImportHandoffSection {
  const cached = deckSource !== "starter";
  return {
    detail: cached ? "当前构筑来自浏览器保存或 URL 导入。" : "当前使用仓库内默认 starter 构筑。",
    id: "recovery",
    label: "恢复",
    nextStep: cached ? "可恢复默认清除本地覆盖。" : "导入后会写入本地缓存，房间页提交时读取当前构筑。",
    source: "local-cache",
    state: cached ? "ready" : "waiting",
    value: deckSourceLabel(deckSource)
  };
}

function currentSection(summary: DeckSubmissionSummary): DeckImportHandoffSection {
  return {
    detail: `传奇 ${summary.legendCardNo} / 英雄 ${summary.championCardNo}。`,
    id: "current",
    label: "当前",
    nextStep: "这份当前构筑会生成 SUBMIT_DECK 命令预览。",
    source: "local-state",
    state: "ready",
    value: summaryText(summary)
  };
}

function commandSection(commandPreviewLength: number): DeckImportHandoffSection {
  return {
    detail: `当前 JSON 预览 ${commandPreviewLength} 个字符。`,
    id: "command",
    label: "命令",
    nextStep: "房间快捷行动提交时携带服务端 prompt/tick 身份。",
    source: "generated-command",
    state: "ready",
    value: "SUBMIT_DECK"
  };
}

function serverSection(canApplyImport: boolean): DeckImportHandoffSection {
  return {
    detail: "同名上限、数量、颜色、规则合法性只由服务端判定。",
    id: "server",
    label: "服务端",
    nextStep: canApplyImport ? "导入当前构筑后进入房间提交。" : "结构有效后再进入服务端提交环节。",
    source: "server-authority",
    state: "authority",
    value: "待提交"
  };
}

function activeSection(sections: readonly DeckImportHandoffSection[]): DeckImportHandoffSectionId {
  const blocking = sections.find((section) => section.state === "blocking");
  if (blocking) {
    return blocking.id;
  }

  const intake = sections.find((section) => section.id === "intake");
  if (intake?.state === "waiting") {
    return "intake";
  }

  return "command";
}

function importStateLabel(state: DeckImportFlowState): string {
  switch (state) {
    case "empty":
      return "等待粘贴";
    case "invalid":
      return "结构无效";
    case "valid":
      return "结构可导入";
  }
}

function deckSourceLabel(source: DeckSource): string {
  switch (source) {
    case "query":
      return "URL 导入";
    case "storage":
      return "本地缓存";
    case "starter":
      return "默认 starter";
  }
}

function summaryShort(summary: DeckSubmissionSummary): string {
  return `${summary.mainDeck}/${summary.runeDeck}/${summary.battlefields}`;
}

function summaryText(summary: DeckSubmissionSummary): string {
  return `${summary.mainDeck} 主 / ${summary.runeDeck} 符文 / ${summary.battlefields} 战场`;
}

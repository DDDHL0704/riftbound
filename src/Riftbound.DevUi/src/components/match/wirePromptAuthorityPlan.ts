import type { ActionPromptCandidateDto, ActionPromptDto } from "../../types/protocol";
import type { ServerSubmissionGatePlan } from "../../utils/serverSubmissionGatePlan";

export type WirePromptAuthorityState = "fallback" | "missing" | "mixed" | "server";

export type WirePromptAuthorityMetric = {
  key: string;
  label: string;
  state: WirePromptAuthorityState;
  value: string;
};

export type WirePromptAuthorityRow = {
  key: string;
  label: string;
  state: WirePromptAuthorityState;
  stateLabel: string;
  value: string;
};

export type WirePromptAuthorityPlan = {
  issueCount: number;
  metrics: WirePromptAuthorityMetric[];
  rows: WirePromptAuthorityRow[];
  state: WirePromptAuthorityState;
  stateLabel: string;
  summary: string;
};

export function buildWirePromptAuthorityPlan({
  playerId,
  prompt,
  submissionGate
}: {
  playerId: string;
  prompt?: ActionPromptDto;
  submissionGate?: ServerSubmissionGatePlan;
}): WirePromptAuthorityPlan {
  const candidates = prompt?.candidates ?? [];
  const actions = prompt?.actions ?? [];
  const actionableForPlayer = Boolean(prompt?.actionable && prompt.playerId === playerId);
  const rows: WirePromptAuthorityRow[] = [
    promptWindowRow(prompt, playerId),
    candidateRow(prompt, candidates, actions),
    commandTemplateRow(candidates),
    composerSupportRow(candidates),
    objectContextRow(prompt, candidates),
    contractRow(prompt, candidates),
    submissionGateRow(submissionGate)
  ];
  const issueCount = rows.filter((row) => row.state !== "server").length;
  const state = authorityState(rows.map((row) => row.state));

  return {
    issueCount,
    metrics: [
      {
        key: "window",
        label: "窗口",
        state: prompt ? "server" : "missing",
        value: actionableForPlayer ? "可操作" : prompt ? "只读" : "缺失"
      },
      {
        key: "candidates",
        label: "候选",
        state: candidates.length > 0 ? "server" : actions.length > 0 ? "fallback" : "missing",
        value: String(candidates.length)
      },
      {
        key: "commands",
        label: "命令形态",
        state: commandTemplateState(candidates),
        value: commandTemplateMetricValue(candidates)
      },
      {
        key: "composer",
        label: "组合提交",
        state: composerSupportState(candidates),
        value: composerSupportMetricValue(candidates)
      },
      {
        key: "issues",
        label: "待补齐",
        state: issueCount === 0 ? "server" : state,
        value: String(issueCount)
      }
    ],
    rows,
    state,
    stateLabel: authorityStateLabel(state),
    summary: authoritySummary(state, issueCount)
  };
}

function promptWindowRow(prompt: ActionPromptDto | undefined, playerId: string): WirePromptAuthorityRow {
  if (!prompt) {
    return row("window", "行动窗口", "missing", "无服务端提示", "等待服务端 prompt");
  }

  const promptType = prompt.view?.type ?? "UNKNOWN";
  const ownership = prompt.playerId === playerId ? "本方" : "对方";
  return row("window", "行动窗口", "server", "服务端 prompt", `${promptType} / ${ownership}`);
}

function candidateRow(
  prompt: ActionPromptDto | undefined,
  candidates: ActionPromptCandidateDto[],
  actions: string[]
): WirePromptAuthorityRow {
  if (!prompt) {
    return row("candidates", "服务端候选", "missing", "无 prompt", "0");
  }

  if (candidates.length > 0) {
    const enabledCount = candidates.filter((candidate) => candidate.enabled).length;
    return row("candidates", "服务端候选", "server", "候选已公开", `${enabledCount} 可提交 / ${candidates.length} 总数`);
  }

  if (actions.length > 0) {
    return row("candidates", "服务端候选", "fallback", "仅有 action 列表", `${actions.length} 个 action`);
  }

  return row("candidates", "服务端候选", "missing", "无候选", "0");
}

function commandTemplateRow(candidates: ActionPromptCandidateDto[]): WirePromptAuthorityRow {
  const enabledCandidates = candidates.filter((candidate) => candidate.enabled);
  if (enabledCandidates.length === 0) {
    return row("commandTemplates", "命令形态", "missing", "无可提交候选", "0/0");
  }

  const count = enabledCandidates.filter(hasExecutableCommandShape).length;
  const state = commandTemplateState(candidates);
  return row("commandTemplates", "命令形态", state, commandTemplateStateLabel(state), `${count}/${enabledCandidates.length}`);
}

function composerSupportRow(candidates: ActionPromptCandidateDto[]): WirePromptAuthorityRow {
  const enabledCandidates = candidates.filter((candidate) => candidate.enabled);
  if (enabledCandidates.length === 0) {
    return row("composerSupport", "组合提交", "missing", "无可提交候选", "0/0");
  }

  const composerCandidates = composerRelevantCandidates(candidates);
  if (composerCandidates.length === 0) {
    const directCount = enabledCandidates.filter(hasDirectCommandShape).length;
    return directCount === enabledCandidates.length
      ? row("composerSupport", "组合提交", "server", "无需组合", `${directCount} 个直接命令`)
      : row("composerSupport", "组合提交", "fallback", "无组合协议", "0/0");
  }

  const supportedCount = composerCandidates.filter(hasServerComposerSupport).length;
  const blockedCount = composerCandidates.filter(hasBlockedServerComposer).length;
  const state = composerSupportState(candidates);
  const stateLabel = blockedCount > 0
    ? "服务端阻断"
    : composerSupportStateLabel(state);
  return row("composerSupport", "组合提交", state, stateLabel, `${supportedCount}/${composerCandidates.length}`);
}

function objectContextRow(
  prompt: ActionPromptDto | undefined,
  candidates: ActionPromptCandidateDto[]
): WirePromptAuthorityRow {
  const count = prompt?.objectContexts?.length ?? 0;
  if (count > 0) {
    return row("objectContexts", "对象上下文", "server", "服务端 objectContexts", `${count} 个对象`);
  }

  if (candidates.length > 0) {
    return row("objectContexts", "对象上下文", "mixed", "由候选派生", "0 个服务端对象上下文");
  }

  return row("objectContexts", "对象上下文", "missing", "无对象上下文", "0");
}

function contractRow(
  prompt: ActionPromptDto | undefined,
  candidates: ActionPromptCandidateDto[]
): WirePromptAuthorityRow {
  if (prompt?.contract) {
    return row("contract", "提示契约", "server", "服务端 contract", `${prompt.contract.promptKind} / ${prompt.contract.candidateAction}`);
  }

  if (candidates.length > 0) {
    return row("contract", "提示契约", "mixed", "候选存在但无 contract", "缺少提示契约");
  }

  return row("contract", "提示契约", "missing", "无契约", "缺少提示契约");
}

function submissionGateRow(submissionGate: ServerSubmissionGatePlan | undefined): WirePromptAuthorityRow {
  if (!submissionGate) {
    return row("submissionGate", "提交门禁", "fallback", "未接入门禁", "无额外门禁");
  }

  return row(
    "submissionGate",
    "提交门禁",
    submissionGate.canSubmit ? "server" : "mixed",
    submissionGate.stateLabel,
    submissionGate.reason
  );
}

function commandTemplateState(candidates: ActionPromptCandidateDto[]): WirePromptAuthorityState {
  const enabledCandidates = candidates.filter((candidate) => candidate.enabled);
  if (enabledCandidates.length === 0) {
    return "missing";
  }

  const count = enabledCandidates.filter(hasExecutableCommandShape).length;
  if (count === enabledCandidates.length) {
    return "server";
  }

  return count > 0 ? "mixed" : "fallback";
}

function commandTemplateStateLabel(state: WirePromptAuthorityState): string {
  switch (state) {
    case "server":
      return "全部可解释";
    case "mixed":
      return "部分可解释";
    case "fallback":
      return "无法直接提交";
    case "missing":
      return "无可提交候选";
  }
}

function commandTemplateMetricValue(candidates: ActionPromptCandidateDto[]): string {
  const enabledCandidates = candidates.filter((candidate) => candidate.enabled);
  if (enabledCandidates.length === 0) {
    return "0/0";
  }

  return `${enabledCandidates.filter(hasExecutableCommandShape).length}/${enabledCandidates.length}`;
}

function composerSupportState(candidates: ActionPromptCandidateDto[]): WirePromptAuthorityState {
  const enabledCandidates = candidates.filter((candidate) => candidate.enabled);
  if (enabledCandidates.length === 0) {
    return "missing";
  }

  const composerCandidates = composerRelevantCandidates(candidates);
  if (composerCandidates.length === 0) {
    return enabledCandidates.every(hasDirectCommandShape) ? "server" : "fallback";
  }

  const supportedCount = composerCandidates.filter(hasServerComposerSupport).length;
  const blockedCount = composerCandidates.filter(hasBlockedServerComposer).length;
  if (supportedCount === composerCandidates.length) {
    return "server";
  }

  if (supportedCount > 0 || blockedCount > 0) {
    return "mixed";
  }

  return "fallback";
}

function composerSupportStateLabel(state: WirePromptAuthorityState): string {
  switch (state) {
    case "server":
      return "服务端声明";
    case "mixed":
      return "部分声明";
    case "fallback":
      return "仅有模板";
    case "missing":
      return "无可提交候选";
  }
}

function composerSupportMetricValue(candidates: ActionPromptCandidateDto[]): string {
  const composerCandidates = composerRelevantCandidates(candidates);
  if (composerCandidates.length === 0) {
    const directCount = candidates.filter((candidate) => candidate.enabled && hasDirectCommandShape(candidate)).length;
    return directCount > 0 ? `${directCount} 直接` : "0/0";
  }

  return `${composerCandidates.filter(hasServerComposerSupport).length}/${composerCandidates.length}`;
}

function composerRelevantCandidates(candidates: ActionPromptCandidateDto[]): ActionPromptCandidateDto[] {
  return candidates.filter((candidate) =>
    candidate.enabled
    && Boolean(candidate.commandTemplate)
    && !hasDirectCommandShape(candidate)
  );
}

function hasServerComposerSupport(candidate: ActionPromptCandidateDto): boolean {
  return Boolean(candidate.commandTemplate && candidate.composer?.supported);
}

function hasBlockedServerComposer(candidate: ActionPromptCandidateDto): boolean {
  return Boolean(candidate.commandTemplate && candidate.composer && !candidate.composer.supported);
}

function hasExecutableCommandShape(candidate: ActionPromptCandidateDto): boolean {
  if (candidate.commandTemplate) {
    return true;
  }

  return hasDirectCommandShape(candidate);
}

function hasDirectCommandShape(candidate: ActionPromptCandidateDto): boolean {
  return directCommandActions.has(candidate.action);
}

const directCommandActions = new Set([
  "END_TURN",
  "PASS",
  "PASS_FOCUS",
  "PASS_PRIORITY",
  "READY",
  "RECYCLE_RUNE",
  "SUBMIT_DECK",
  "SURRENDER",
  "TAP_RUNE"
]);

function authorityState(states: WirePromptAuthorityState[]): WirePromptAuthorityState {
  if (states.length === 0 || states.includes("missing")) {
    return "missing";
  }

  if (states.every((state) => state === "server")) {
    return "server";
  }

  if (states.some((state) => state === "fallback")) {
    return states.some((state) => state === "server" || state === "mixed") ? "mixed" : "fallback";
  }

  return "mixed";
}

function authorityStateLabel(state: WirePromptAuthorityState): string {
  switch (state) {
    case "server":
      return "服务端权威";
    case "mixed":
      return "部分兜底";
    case "fallback":
      return "前端兜底";
    case "missing":
      return "材料缺失";
  }
}

function authoritySummary(state: WirePromptAuthorityState, issueCount: number): string {
  switch (state) {
    case "server":
      return "当前行动窗口的候选、命令、对象上下文与契约均可由服务端解释。";
    case "mixed":
      return `仍有 ${issueCount} 项行动窗口材料不完整，需要后端或协议继续补齐。`;
    case "fallback":
      return "当前只能依赖 action 列表等低保真材料，不适合作为最终交互入口。";
    case "missing":
      return "缺少服务端行动窗口材料，当前只能等待或只读展示。";
  }
}

function row(
  key: string,
  label: string,
  state: WirePromptAuthorityState,
  stateLabel: string,
  value: string
): WirePromptAuthorityRow {
  return {
    key,
    label,
    state,
    stateLabel,
    value
  };
}

import type { ActionPromptCandidateDto, ActionPromptDto, SnapshotDto } from "../types/protocol";

export type ActionPanelResponseWindowMode = "spell-duel" | "stack-priority" | "wait";

export type ActionPanelResponseMetricKey = "window" | "choice" | "stack" | "template" | "route";

export type ActionPanelResponseMetric = {
  detail: string;
  key: ActionPanelResponseMetricKey;
  label: string;
  value: string;
};

export type ActionPanelResponsePlan = {
  authorityLabel: string;
  commandFieldCount: number;
  metricRows: ActionPanelResponseMetric[];
  mode: ActionPanelResponseWindowMode;
  selectionStepCount: number;
  stackCount: number;
  state: "blocked" | "ready";
  statusLabel: string;
  windowLabel: string;
};

export type BuildActionPanelResponsePlanOptions = {
  prompt?: ActionPromptDto;
  snapshot?: SnapshotDto;
};

export function buildActionPanelResponsePlan(
  candidate: ActionPromptCandidateDto,
  { prompt, snapshot }: BuildActionPanelResponsePlanOptions = {}
): ActionPanelResponsePlan {
  const promptType = prompt?.view?.type?.trim() || "服务端窗口";
  const mode = responseMode(candidate.action, promptType);
  const stackCount = snapshot?.stack?.length ?? 0;
  const commandFieldCount = candidate.commandTemplate?.bindings.length ?? 0;
  const selectionStepCount = candidate.selectionSteps?.length ?? 0;
  const choiceCount = totalChoiceCount(candidate);
  const copy = responseCopy(candidate.action, mode, commandFieldCount, selectionStepCount);

  return {
    authorityLabel: copy.authorityLabel,
    commandFieldCount,
    metricRows: [
      {
        detail: prompt?.view?.message?.trim() || "服务端响应提示",
        key: "window",
        label: "窗口",
        value: copy.windowLabel
      },
      {
        detail: choiceCount > 0 ? "服务端公开的安全选项数量" : "没有公开来源/目标/模式选项",
        key: "choice",
        label: "公开选择",
        value: `${choiceCount} 项`
      },
      {
        detail: mode === "stack-priority" || mode === "spell-duel" ? "来自服务端结算链快照" : "等待窗口不展示结算链推进",
        key: "stack",
        label: "结算链",
        value: `${stackCount} 项`
      },
      {
        detail: candidate.commandTemplate ? "服务端命令模板" : "未提供直接提交模板",
        key: "template",
        label: "命令字段",
        value: String(commandFieldCount)
      },
      {
        detail: selectionStepCount > 0 ? "需要按服务端步骤选择后提交" : copy.routeDetail,
        key: "route",
        label: "候选步骤",
        value: `${selectionStepCount} 步`
      }
    ],
    mode,
    selectionStepCount,
    stackCount,
    state: candidate.enabled ? "ready" : "blocked",
    statusLabel: candidate.enabled ? copy.readyLabel : copy.blockedLabel,
    windowLabel: copy.windowLabel
  };
}

function responseMode(action: string, promptType: string): ActionPanelResponseWindowMode {
  if (promptType.startsWith("SPELL_DUEL")) {
    return "spell-duel";
  }
  if (action === "WAIT" || promptType === "WAIT") {
    return "wait";
  }
  return "stack-priority";
}

function responseCopy(action: string, mode: ActionPanelResponseWindowMode, commandFieldCount: number, selectionStepCount: number): {
  authorityLabel: string;
  blockedLabel: string;
  readyLabel: string;
  routeDetail: string;
  windowLabel: string;
} {
  const hasSubmissionRoute = commandFieldCount > 0 || selectionStepCount > 0;
  const routeDetail = hasSubmissionRoute
    ? "可沿服务端模板或组合步骤提交"
    : "未提供直接命令，不在前端伪造响应命令";

  if (mode === "spell-duel") {
    return {
      authorityLabel: `法术对决响应是否合法、如何进入结算链，全部由服务端候选和模板裁定。${routeDetail}`,
      blockedLabel: action === "WAIT" ? "等待法术对决" : "暂不可响应法术对决",
      readyLabel: action === "WAIT" ? "等待法术对决" : "可响应法术对决",
      routeDetail,
      windowLabel: "法术对决响应"
    };
  }

  if (mode === "wait") {
    return {
      authorityLabel: `等待状态只展示服务端窗口，不提交本地规则命令。${routeDetail}`,
      blockedLabel: "等待服务端",
      readyLabel: "等待服务端",
      routeDetail,
      windowLabel: "等待窗口"
    };
  }

  return {
    authorityLabel: `结算链响应是否合法、是否进入后续选择，全部由服务端候选和模板裁定。${routeDetail}`,
    blockedLabel: "暂不可响应",
    readyLabel: "可响应",
    routeDetail,
    windowLabel: "结算链响应"
  };
}

function totalChoiceCount(candidate: ActionPromptCandidateDto): number {
  return count(candidate.sources)
    + count(candidate.targets)
    + count(candidate.destinations)
    + count(candidate.modes)
    + count(candidate.optionalCosts);
}

function count(value: unknown[] | null | undefined): number {
  return Array.isArray(value) ? value.length : 0;
}

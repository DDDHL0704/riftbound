import type { ActionPromptCandidateDto, ActionPromptDto, SnapshotDto } from "../types/protocol";

export type ActionPanelPassWindowMode = "main-window" | "spell-duel" | "stack-priority";

export type ActionPanelPassMetricKey = "window" | "responsible" | "stack" | "passed" | "template";

export type ActionPanelPassMetric = {
  detail: string;
  key: ActionPanelPassMetricKey;
  label: string;
  value: string;
};

export type ActionPanelPassPlan = {
  authorityLabel: string;
  commandFieldCount: number;
  metricRows: ActionPanelPassMetric[];
  mode: ActionPanelPassWindowMode;
  passedCount: number;
  stackCount: number;
  state: "blocked" | "ready";
  statusLabel: string;
  windowLabel: string;
};

export type BuildActionPanelPassPlanOptions = {
  prompt?: ActionPromptDto;
  snapshot?: SnapshotDto;
};

export function buildActionPanelPassPlan(
  candidate: ActionPromptCandidateDto,
  { prompt, snapshot }: BuildActionPanelPassPlanOptions = {}
): ActionPanelPassPlan {
  const promptType = prompt?.view?.type?.trim() || "服务端窗口";
  const mode = passMode(candidate.action, promptType);
  const timing = record(snapshot?.timing);
  const turnWindow = record(timing.turnWindow);
  const spellDuel = record(timing.spellDuel);
  const passedPlayerIds = mode === "spell-duel"
    ? stringArray(spellDuel.passedFocusPlayerIds)
    : mode === "stack-priority"
      ? stringArray(timing.passedPriorityPlayerIds)
      : [];
  const responsiblePlayerId = firstText(
    prompt?.view?.responsibility?.responsiblePlayerId,
    mode === "spell-duel" ? stringValue(spellDuel.focusPlayerId) : undefined,
    mode === "stack-priority" ? stringValue(turnWindow.actingPlayerId) : undefined,
    stringValue(timing.priorityPlayerId),
    prompt?.playerId
  );
  const stackCount = snapshot?.stack?.length ?? 0;
  const commandFieldCount = candidate.commandTemplate?.bindings.length ?? 0;
  const copy = passWindowCopy(candidate.action, mode);

  return {
    authorityLabel: copy.authorityLabel,
    commandFieldCount,
    metricRows: [
      {
        detail: prompt?.view?.message?.trim() || "服务端行动提示",
        key: "window",
        label: "窗口",
        value: copy.windowLabel
      },
      {
        detail: prompt?.view?.responsibility?.nextStep || "等待服务端责任摘要",
        key: "responsible",
        label: "责任玩家",
        value: responsiblePlayerId || "服务端未公开"
      },
      {
        detail: mode === "main-window" ? "当前不是结算链专用窗口" : "来自服务端结算链快照",
        key: "stack",
        label: "结算链",
        value: `${stackCount} 项`
      },
      {
        detail: passedPlayerIds.length > 0 ? passedPlayerIds.join("、") : "无已公开让过玩家",
        key: "passed",
        label: "已让过",
        value: `${passedPlayerIds.length} 人`
      },
      {
        detail: candidate.commandTemplate ? "服务端命令模板" : "兼容旧候选命令",
        key: "template",
        label: "命令字段",
        value: String(commandFieldCount)
      }
    ],
    mode,
    passedCount: passedPlayerIds.length,
    stackCount,
    state: candidate.enabled ? "ready" : "blocked",
    statusLabel: candidate.enabled ? copy.readyLabel : copy.blockedLabel,
    windowLabel: copy.windowLabel
  };
}

function passMode(action: string, promptType: string): ActionPanelPassWindowMode {
  if (action === "PASS_FOCUS" || promptType.startsWith("SPELL_DUEL")) {
    return "spell-duel";
  }
  if (action === "PASS_PRIORITY" || promptType === "STACK_PRIORITY") {
    return "stack-priority";
  }
  return "main-window";
}

function passWindowCopy(action: string, mode: ActionPanelPassWindowMode): {
  authorityLabel: string;
  blockedLabel: string;
  readyLabel: string;
  windowLabel: string;
} {
  if (action === "PASS_FOCUS" || mode === "spell-duel") {
    return {
      authorityLabel: "焦点归属、法术对决推进和让过结果由服务端候选与后续校验裁定。",
      blockedLabel: "暂不可让过焦点",
      readyLabel: "可让过焦点",
      windowLabel: "法术对决焦点"
    };
  }

  if (action === "PASS_PRIORITY" || mode === "stack-priority") {
    return {
      authorityLabel: "优先权归属、结算链推进和让过结果由服务端候选与后续校验裁定。",
      blockedLabel: "暂不可让过优先权",
      readyLabel: "可让过优先权",
      windowLabel: "结算链优先权"
    };
  }

  return {
    authorityLabel: "当前窗口推进和让过结果由服务端候选与后续校验裁定。",
    blockedLabel: "暂不可让过",
    readyLabel: "可让过",
    windowLabel: "行动窗口"
  };
}

function record(value: unknown): Record<string, unknown> {
  return typeof value === "object" && value != null && !Array.isArray(value) ? value as Record<string, unknown> : {};
}

function stringArray(value: unknown): string[] {
  if (!Array.isArray(value)) {
    return [];
  }

  return value.filter((item): item is string => typeof item === "string" && item.trim().length > 0);
}

function stringValue(value: unknown): string | undefined {
  return typeof value === "string" && value.trim().length > 0 ? value : undefined;
}

function firstText(...values: Array<string | undefined | null>): string | undefined {
  return values.find((value): value is string => typeof value === "string" && value.trim().length > 0);
}

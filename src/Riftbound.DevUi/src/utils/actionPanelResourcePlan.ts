import type { ActionPromptCandidateDto, SnapshotDto } from "../types/protocol";
import { runePoolText } from "./formatters";

export type ActionPanelResourceMetricKey = "sources" | "mana" | "power" | "template" | "selection";

export type ActionPanelResourceMetric = {
  detail: string;
  key: ActionPanelResourceMetricKey;
  label: string;
  value: string;
};

export type ActionPanelResourcePlan = {
  authorityLabel: string;
  commandFieldCount: number;
  metricRows: ActionPanelResourceMetric[];
  poolLabel: string;
  powerTraitCount: number;
  selectionStepCount: number;
  sourceChoiceCount: number;
  state: "blocked" | "ready";
  statusLabel: string;
};

export type BuildActionPanelResourcePlanOptions = {
  playerId?: string;
  snapshot?: SnapshotDto;
};

export function buildActionPanelResourcePlan(
  candidate: ActionPromptCandidateDto,
  { playerId, snapshot }: BuildActionPanelResourcePlanOptions = {}
): ActionPanelResourcePlan {
  const pool = playerId ? snapshot?.players[playerId]?.runePool : undefined;
  const sourceChoiceCount = maxCount(
    candidate.sources?.length ?? 0,
    selectionStepChoiceCount(candidate, "source")
  );
  const commandFieldCount = candidate.commandTemplate?.bindings.length ?? 0;
  const selectionStepCount = candidate.selectionSteps?.length ?? candidate.composer?.selectionRoles.length ?? 0;
  const mana = numberValue(pool?.mana);
  const power = numberValue(pool?.power ?? pool?.totalPower);
  const powerByTrait = pool?.powerByTrait ?? {};
  const powerTraitCount = Object.values(powerByTrait).filter((amount) => amount > 0).length;
  const copy = resourceActionCopy(candidate.action);

  return {
    authorityLabel: copy.authorityLabel,
    commandFieldCount,
    metricRows: [
      {
        detail: `${selectionStepCount} 步选择`,
        key: "sources",
        label: "符文候选",
        value: String(sourceChoiceCount)
      },
      {
        detail: "来自服务端 runePool",
        key: "mana",
        label: "当前法力",
        value: mana == null ? "服务端未公开" : String(mana)
      },
      {
        detail: powerTraitCount > 0 ? `${powerTraitCount} 类符能` : "无公开符能类型",
        key: "power",
        label: "当前符能",
        value: power == null ? "服务端未公开" : String(power)
      },
      {
        detail: candidate.commandTemplate ? "服务端命令模板" : "服务端未公开模板",
        key: "template",
        label: "命令字段",
        value: String(commandFieldCount)
      },
      {
        detail: pool ? runePoolText(pool) : "等待服务端快照",
        key: "selection",
        label: "资源池",
        value: pool ? "已公开" : "未公开"
      }
    ],
    poolLabel: pool ? runePoolText(pool) : "服务端未公开资源池",
    powerTraitCount,
    selectionStepCount,
    sourceChoiceCount,
    state: candidate.enabled ? "ready" : "blocked",
    statusLabel: candidate.enabled ? copy.readyLabel : copy.blockedLabel
  };
}

function resourceActionCopy(action: string): { authorityLabel: string; blockedLabel: string; readyLabel: string } {
  switch (action) {
    case "RECYCLE_RUNE":
      return {
        authorityLabel: "可回收符文和资源变化由服务端候选与后续校验裁定。",
        blockedLabel: "暂不可回收",
        readyLabel: "可回收"
      };
    case "TAP_RUNE":
    default:
      return {
        authorityLabel: "可横置符文和资源变化由服务端候选与后续校验裁定。",
        blockedLabel: "暂不可横置",
        readyLabel: "可横置"
      };
  }
}

function selectionStepChoiceCount(candidate: ActionPromptCandidateDto, role: string): number {
  return (candidate.selectionSteps ?? [])
    .filter((step) => step.role === role)
    .reduce((count, step) => count + uniqueChoiceIds(step.choices).length, 0);
}

function uniqueChoiceIds(value: unknown): string[] {
  if (!Array.isArray(value)) {
    return [];
  }

  return [...new Set(value.map(choiceId).filter((id): id is string => id != null))];
}

function choiceId(value: unknown): string | undefined {
  if (!isRecord(value)) {
    return undefined;
  }

  const id = value.id;
  if (typeof id === "string" && id.trim().length > 0) {
    return id;
  }

  const label = value.label;
  return typeof label === "string" && label.trim().length > 0 ? label : undefined;
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value != null && !Array.isArray(value);
}

function maxCount(...values: number[]): number {
  return Math.max(...values.filter((value) => Number.isFinite(value)), 0);
}

function numberValue(value: unknown): number | undefined {
  return typeof value === "number" && Number.isFinite(value) ? value : undefined;
}

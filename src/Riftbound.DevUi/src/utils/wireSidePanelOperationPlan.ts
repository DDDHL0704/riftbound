import type { WireSidePanelSlot } from "../components/match/wireTableLayout";
import type { ActionPromptDto } from "../types/protocol";
import type { ServerSubmissionGatePlan } from "./serverSubmissionGatePlan";
import type { WireSidePanelFocusPlan } from "./wireSidePanelFocusPlan";
import type {
  WireSidePanelOrchestrationEntry,
  WireSidePanelOrchestrationPlan
} from "./wireSidePanelOrchestrationPlan";
import type { WireSidePanelRuleChainPlan } from "./wireSidePanelRuleChainPlan";

export type WireSidePanelOperationKey = "commands" | "focus" | "prompt" | "rules";

export type WireSidePanelOperationState = "active" | "blocked" | "empty" | "ready" | "review" | "waiting";

export type WireSidePanelOperationRoute = {
  key: string;
  label: string;
  slot: WireSidePanelSlot;
  state: "available" | "disabled";
};

export type WireSidePanelOperationSection = {
  count: number;
  key: WireSidePanelOperationKey;
  label: string;
  primarySlot: WireSidePanelSlot;
  routes: WireSidePanelOperationRoute[];
  state: WireSidePanelOperationState;
  stateLabel: string;
  summary: string;
  title: string;
};

export type WireSidePanelOperationPlan = {
  activeSectionKey: WireSidePanelOperationKey;
  issueCount: number;
  readyCount: number;
  sections: WireSidePanelOperationSection[];
  state: WireSidePanelOperationState;
  summary: string;
};

export function buildWireSidePanelOperationPlan({
  activeSlot,
  focusPlan,
  orchestration,
  prompt,
  ruleChainPlan,
  submissionGate
}: {
  activeSlot: WireSidePanelSlot;
  focusPlan: WireSidePanelFocusPlan;
  orchestration: WireSidePanelOrchestrationPlan;
  prompt?: ActionPromptDto;
  ruleChainPlan: WireSidePanelRuleChainPlan;
  submissionGate?: ServerSubmissionGatePlan;
}): WireSidePanelOperationPlan {
  const sections = [
    focusSection(focusPlan),
    promptSection({ orchestration, prompt, submissionGate }),
    rulesSection({ orchestration, ruleChainPlan }),
    commandSection(orchestration)
  ];
  const activeSection = sections.find((section) =>
    section.primarySlot === activeSlot || section.routes.some((route) => route.slot === activeSlot)
  ) ?? sections[0];
  const issueCount = sections.filter((section) => section.state === "blocked").length;
  const readyCount = sections.filter((section) => section.state === "ready" || section.state === "active").length;
  const state = issueCount > 0 ? "blocked" : readyCount > 0 ? "ready" : "waiting";

  return {
    activeSectionKey: activeSection.key,
    issueCount,
    readyCount,
    sections,
    state,
    summary: `${activeSection.label}：${activeSection.stateLabel}；${readyCount} 个可用入口。`
  };
}

function focusSection(focusPlan: WireSidePanelFocusPlan): WireSidePanelOperationSection {
  const availableRoutes = focusPlan.routes.filter((route) => route.state === "available");
  return {
    count: focusPlan.visible ? Math.max(1, focusPlan.relationCount + focusPlan.eventCount) : 0,
    key: "focus",
    label: "焦点卡牌",
    primarySlot: "interaction",
    routes: [
      route("actions", "候选", "interaction", focusPlan.visible),
      route("rules", "规则", "ruleQueue", focusPlan.routes.some((item) => item.key === "rules" && item.state === "available")),
      route("map", "地图", "actionMap", focusPlan.routes.some((item) => item.key === "map" && item.state === "available"))
    ],
    state: focusPlan.visible ? availableRoutes.length > 0 ? "ready" : "review" : "empty",
    stateLabel: focusPlan.visible ? focusPlan.stateLabel : "未选择",
    summary: focusPlan.visible ? focusPlan.nextStepLabel : "选择公开桌面对象后显示焦点候选。",
    title: focusPlan.title
  };
}

function promptSection({
  orchestration,
  prompt,
  submissionGate
}: {
  orchestration: WireSidePanelOrchestrationPlan;
  prompt?: ActionPromptDto;
  submissionGate?: ServerSubmissionGatePlan;
}): WireSidePanelOperationSection {
  const promptEntry = entryBySlot(orchestration, "actionPrompt");
  const responseEntry = entryBySlot(orchestration, "responseCoach");
  const candidateCount = prompt?.serverFlow?.candidateCount ?? prompt?.candidates?.length ?? promptEntry?.count ?? 0;
  const enabledCount = prompt?.serverFlow?.enabledCandidateCount
    ?? prompt?.candidates?.filter((candidate) => candidate.enabled).length
    ?? responseEntry?.count
    ?? 0;
  const blocked = Boolean(prompt?.actionable && !submissionGate?.canSubmit);

  return {
    count: candidateCount,
    key: "prompt",
    label: "当前行动提示",
    primarySlot: "actionPrompt",
    routes: [
      route("prompt", "提示", "actionPrompt", Boolean(prompt)),
      route("response", "响应", "responseCoach", enabledCount > 0 || blocked),
      route("window", "窗口", "turnWindow", true)
    ],
    state: blocked ? "blocked" : enabledCount > 0 ? "ready" : candidateCount > 0 ? "review" : "waiting",
    stateLabel: blocked ? "受限" : enabledCount > 0 ? "可提交" : candidateCount > 0 ? "待选择" : "等待",
    summary: promptEntry?.detail ?? "等待服务端公开当前行动窗口。",
    title: prompt?.view?.title ?? promptEntry?.label ?? "行动提示"
  };
}

function rulesSection({
  orchestration,
  ruleChainPlan
}: {
  orchestration: WireSidePanelOrchestrationPlan;
  ruleChainPlan: WireSidePanelRuleChainPlan;
}): WireSidePanelOperationSection {
  const logEntry = entryBySlot(orchestration, "log");
  const laneCount = ruleChainPlan.lanes.reduce((sum, lane) => sum + lane.count, 0);
  const logAvailable = Boolean(logEntry && logEntry.count > 0);
  const detailAvailable = Boolean(ruleChainPlan.detail);

  return {
    count: laneCount + (logEntry?.count ?? 0),
    key: "rules",
    label: "结算链 / 日志 / 队列",
    primarySlot: "ruleQueue",
    routes: [
      route("queue", "队列", "ruleQueue", true),
      route("flow", "流程", "serverFlow", true),
      route("log", "日志", "log", logAvailable),
      route("detail", "详情", "timelineDetail", detailAvailable)
    ],
    state: ruleChainPlan.state === "idle" && !logAvailable ? "waiting" : "active",
    stateLabel: ruleChainPlan.state === "idle" && !logAvailable ? "空" : ruleChainPlan.stateLabel,
    summary: ruleChainPlan.nextStepLabel,
    title: ruleChainPlan.title
  };
}

function commandSection(orchestration: WireSidePanelOrchestrationPlan): WireSidePanelOperationSection {
  const commandEntry = entryBySlot(orchestration, "commandCenter");
  const mapEntry = entryBySlot(orchestration, "actionMap");
  const focusEntry = entryBySlot(orchestration, "interaction");
  const promptEntry = entryBySlot(orchestration, "actionPrompt");
  const count = (commandEntry?.count ?? 0) + (mapEntry?.count ?? 0);
  const blocked = commandEntry?.state === "blocked" || mapEntry?.state === "blocked";

  return {
    count,
    key: "commands",
    label: "可选命令入口",
    primarySlot: "commandCenter",
    routes: [
      route("center", "指挥", "commandCenter", true),
      route("map", "地图", "actionMap", Boolean(mapEntry && mapEntry.count > 0)),
      route("focus", "焦点", "interaction", Boolean(focusEntry && focusEntry.count > 0)),
      route("prompt", "提示", "actionPrompt", Boolean(promptEntry && promptEntry.count > 0))
    ],
    state: blocked ? "blocked" : count > 0 ? "ready" : "waiting",
    stateLabel: blocked ? "阻断" : count > 0 ? "可用" : "等待",
    summary: commandEntry?.detail ?? "命令入口只提交服务端公开候选。",
    title: commandEntry?.label ?? "指挥中心"
  };
}

function route(
  key: string,
  label: string,
  slot: WireSidePanelSlot,
  available: boolean
): WireSidePanelOperationRoute {
  return {
    key,
    label,
    slot,
    state: available ? "available" : "disabled"
  };
}

function entryBySlot(
  orchestration: WireSidePanelOrchestrationPlan,
  slot: WireSidePanelSlot
): WireSidePanelOrchestrationEntry | undefined {
  return orchestration.entries.find((entry) => entry.slot === slot);
}

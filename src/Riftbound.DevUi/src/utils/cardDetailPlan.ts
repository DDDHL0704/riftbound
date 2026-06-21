import type { BehaviorSpec } from "../types/catalog";
import type { ActionPromptCandidateDto, ActionPromptDto, ActionPromptObjectInspectionGroupDto, ActionPromptObjectInspectionRowDto, CardObjectView } from "../types/protocol";
import { sourceCandidatesForPrompt } from "./actionPromptCandidates";
import {
  conformanceLabel,
  conformanceTone,
  costText,
  keywordsText,
  objectTypeText,
  rulesText,
  statusLabel
} from "./formatters";
import { isHiddenObject } from "./hiddenInfo";
import {
  tableObjectCandidateSourceLabel,
  tableObjectContextSourceLabel,
  type TableObjectContext
} from "./tableObjectContext";

export type CardDetailTone = "bad" | "good" | "info" | "neutral" | "warn";

export type CardDetailBadge = {
  key: string;
  label: string;
  tone: CardDetailTone;
};

export type CardDetailRow = {
  key: string;
  label: string;
  value: string;
};

export type CardDetailSection = {
  body: string;
  key: string;
  title: string;
};

export type CardDetailInspectorRow = {
  key: string;
  label: string;
  value: string;
  tone?: CardDetailTone;
};

export type CardDetailInspectorGroup = {
  emptyLabel?: string;
  key: string;
  rows: CardDetailInspectorRow[];
  title: string;
};

export type CardDetailInspectorAuthorityState =
  | "derived"
  | "hidden"
  | "server-inspection"
  | "server-object-context"
  | "snapshot";

export type CardDetailInspectorPlan = {
  authorityLabel: string;
  authorityState: CardDetailInspectorAuthorityState;
  boundaryLabel: string;
  groups: CardDetailInspectorGroup[];
  sourceLabel: string;
  summaryRows: CardDetailInspectorRow[];
};

export type CardDetailCheckState = "empty" | "hidden" | "ready" | "server" | "snapshot" | "warning";

export type CardDetailCheckRow = {
  count: number;
  key: string;
  label: string;
  sourceLabel: string;
  state: CardDetailCheckState;
  stateLabel: string;
  summary: string;
};

export type CardDetailPlan = {
  actionCandidates: ActionPromptCandidateDto[];
  actionEmptyLabel: string;
  badges: CardDetailBadge[];
  checkRows: CardDetailCheckRow[];
  detailRows: CardDetailRow[];
  hidden: boolean;
  hiddenMessage?: string;
  inspector: CardDetailInspectorPlan;
  sections: CardDetailSection[];
  sourceObjectId?: string;
  title: string;
};

export type CardDetailPlanCard = {
  object?: CardObjectView;
  objectId?: string;
  spec?: BehaviorSpec;
};

export function buildCardDetailPlan({
  card,
  objectContext,
  prompt
}: {
  card?: CardDetailPlanCard;
  objectContext?: TableObjectContext;
  prompt?: ActionPromptDto;
}): CardDetailPlan | undefined {
  if (!card) {
    return undefined;
  }

  const hidden = isHiddenObject(card.object) && !card.spec;
  const title = hidden ? "未公开卡牌" : card.spec?.cardName ?? card.object?.cardNo ?? "未知卡牌";
  const sourceObjectId = card.objectId ?? card.object?.objectId;

  if (hidden) {
    return {
      actionCandidates: [],
      actionEmptyLabel: "隐藏对象不会展示或提交任何前端推断操作。",
      badges: [
        { key: "visibility", label: "隐藏信息", tone: "warn" },
        { key: "card-no", label: "未公开", tone: "neutral" }
      ],
      checkRows: hiddenCheckRows(sourceObjectId),
      detailRows: [],
      hidden,
      hiddenMessage: "该对象未向当前玩家公开。前端只展示服务端快照允许的信息，不读取或推断卡名、费用、类型或规则文本。",
      inspector: buildHiddenInspectorPlan(sourceObjectId),
      sections: [],
      sourceObjectId,
      title
    };
  }

  const states = objectStateLabels(card.object);
  const actionCandidates = sourceCandidatesForPrompt(prompt, sourceObjectId, { enabledOnly: false });
  const detailRows: CardDetailRow[] = [
    { key: "cost", label: "费用", value: costText(card.spec) },
    { key: "power", label: "战力", value: `${card.object?.effectivePower ?? card.object?.power ?? card.object?.basePower ?? "未知"}` },
    { key: "owner", label: "所属方", value: card.object?.ownerId ?? "未知" },
    { key: "controller", label: "控制方", value: card.object?.controllerId ?? "未知" },
    { key: "zone", label: "位置", value: objectContext?.zone.label ?? formatLocation(card.object?.location) }
  ];
  const sections: CardDetailSection[] = [
    { key: "keywords", title: "关键词", body: keywordsText(card.spec) },
    { key: "rules", title: "规则文本", body: rulesText(card.spec?.officialText) },
    { key: "state", title: "对象状态", body: states.length ? states.join("、") : "正常" }
  ];

  if (card.spec) {
    sections.splice(2, 0, {
      key: "evidence",
      title: "服务端证据",
      body: `${conformanceLabel(card.spec.conformanceTier)}。${statusLabel(card.spec.status)}。前端只提交服务端当前候选允许的操作。`
    });
  }

  return {
    actionCandidates,
    actionEmptyLabel: "当前服务端行动提示没有给这张牌可提交的操作。",
    badges: [
      { key: "type", label: objectTypeText(card.object, card.spec), tone: "info" },
      { key: "card-no", label: card.spec?.cardNo ?? card.object?.cardNo ?? "无编号", tone: "neutral" },
      ...(card.spec
        ? [
            { key: "conformance", label: conformanceLabel(card.spec.conformanceTier), tone: conformanceTone(card.spec.conformanceTier) },
            { key: "status", label: statusLabel(card.spec.status), tone: card.spec.status === "implemented" ? "info" : "warn" }
          ] satisfies CardDetailBadge[]
        : [])
    ],
    checkRows: visibleCheckRows({
      actionCandidateCount: actionCandidates.length,
      objectContext,
      sections,
      sourceObjectId
    }),
    detailRows,
    hidden,
    inspector: buildVisibleInspectorPlan({
      actionCandidateCount: actionCandidates.length,
      card,
      detailRows,
      objectContext,
      prompt,
      sourceObjectId
    }),
    sections,
    sourceObjectId,
    title
  };
}

function buildHiddenInspectorPlan(sourceObjectId?: string): CardDetailInspectorPlan {
  return {
    authorityLabel: "隐藏信息边界",
    authorityState: "hidden",
    boundaryLabel: "隐藏对象：只展示服务端公开外壳，不展示卡名、费用、规则文本或前端推断操作。",
    sourceLabel: "隐藏对象安全外壳",
    summaryRows: [
      { key: "visibility", label: "信息边界", value: "隐藏信息", tone: "warn" },
      { key: "object", label: "对象", value: sourceObjectId || "服务端未公开", tone: "neutral" },
      { key: "candidates", label: "候选", value: "不从隐藏对象推导", tone: "neutral" }
    ],
    groups: [
      {
        key: "safe-boundary",
        title: "安全边界",
        rows: [
          { key: "card", label: "卡牌身份", value: "未公开", tone: "warn" },
          { key: "rules", label: "规则文本", value: "未公开", tone: "warn" },
          { key: "actions", label: "服务端候选", value: "不展示/不提交", tone: "neutral" }
        ]
      }
    ]
  };
}

function hiddenCheckRows(sourceObjectId?: string): CardDetailCheckRow[] {
  return [
    {
      count: sourceObjectId ? 1 : 0,
      key: "identity",
      label: "对象身份",
      sourceLabel: "隐藏对象安全外壳",
      state: "hidden",
      stateLabel: "未公开",
      summary: sourceObjectId || "服务端未公开对象 ID"
    },
    {
      count: 0,
      key: "rules",
      label: "规则文本",
      sourceLabel: "隐藏边界",
      state: "hidden",
      stateLabel: "不展示",
      summary: "卡名、费用、规则文本和卡面不向当前玩家公开。"
    },
    {
      count: 0,
      key: "candidates",
      label: "服务端候选",
      sourceLabel: "隐藏边界",
      state: "hidden",
      stateLabel: "不推导",
      summary: "隐藏对象不会展示或提交前端推断操作。"
    },
    {
      count: 1,
      key: "boundary",
      label: "信息边界",
      sourceLabel: "服务端快照",
      state: "hidden",
      stateLabel: "受保护",
      summary: "只展示服务端允许的隐藏外壳。"
    }
  ];
}

function visibleCheckRows({
  actionCandidateCount,
  objectContext,
  sections,
  sourceObjectId
}: {
  actionCandidateCount: number;
  objectContext?: TableObjectContext;
  sections: CardDetailSection[];
  sourceObjectId?: string;
}): CardDetailCheckRow[] {
  const enabledCount = objectContext?.promptEnabledCount ?? actionCandidateCount;
  const disabledCount = objectContext?.promptDisabledCount ?? 0;
  const candidateLinks = objectContext?.candidateLinks ?? [];
  const selectionStepCount = candidateLinks.reduce((count, candidate) => count + (candidate.selectionSteps?.length ?? 0), 0);
  const requiredStepMissingCount = candidateLinks.reduce((count, candidate) =>
    count + (candidate.selectionSteps ?? []).filter((step) => step.required && step.objectChoiceCount <= 0).length, 0);
  const stackCount = objectContext?.stackRoles.length ?? 0;
  const stackSummary = objectContext?.stackRoles.join(" / ") || "当前对象不在公开结算链中。";
  const eventCount = objectContext?.eventLinks.length ?? 0;
  const rulesSection = sections.find((section) => section.key === "rules");

  return [
    {
      count: sourceObjectId ? 1 : 0,
      key: "identity",
      label: "对象身份",
      sourceLabel: objectContext ? tableObjectContextSourceLabel(objectContext) : "公开快照索引",
      state: objectContext?.candidateSource === "server" ? "server" : "snapshot",
      stateLabel: sourceObjectId ? "已定位" : "无对象",
      summary: sourceObjectId || "服务端未公开对象 ID"
    },
    {
      count: objectContext ? 1 : 0,
      key: "zone",
      label: "状态与区域",
      sourceLabel: objectContext ? "快照对象索引" : "公开快照",
      state: objectContext ? "ready" : "snapshot",
      stateLabel: objectContext ? "已映射" : "快照兜底",
      summary: objectContext?.zone.label ?? "使用对象 location 兜底。"
    },
    {
      count: enabledCount + disabledCount,
      key: "candidates",
      label: "服务端候选",
      sourceLabel: objectContext ? tableObjectCandidateSourceLabel(objectContext.candidateSource) : "当前 prompt",
      state: enabledCount > 0 ? "server" : disabledCount > 0 ? "warning" : "empty",
      stateLabel: `${enabledCount} 可用 / ${disabledCount} 阻断`,
      summary: enabledCount + disabledCount > 0 ? "只显示服务端当前候选。" : "当前没有关联候选。"
    },
    {
      count: selectionStepCount,
      key: "selection",
      label: "选择步骤",
      sourceLabel: objectContext ? "服务端候选步骤" : "无候选步骤",
      state: requiredStepMissingCount > 0 ? "warning" : selectionStepCount > 0 ? "server" : "empty",
      stateLabel: requiredStepMissingCount > 0 ? `缺少 ${requiredStepMissingCount}` : selectionStepCount > 0 ? "已公开" : "无步骤",
      summary: selectionStepCount > 0 ? `${selectionStepCount} 步骤来自服务端候选。` : "没有需要展示的选择步骤。"
    },
    {
      count: stackCount,
      key: "stack",
      label: "结算链",
      sourceLabel: stackCount > 0 ? "snapshot.stack" : "无结算链",
      state: stackCount > 0 ? "ready" : "empty",
      stateLabel: stackCount > 0 ? "有关联" : "无关联",
      summary: stackSummary
    },
    {
      count: eventCount,
      key: "events",
      label: "近期事件",
      sourceLabel: eventCount > 0 ? "event log" : "无事件",
      state: eventCount > 0 ? "ready" : "empty",
      stateLabel: eventCount > 0 ? "有记录" : "无记录",
      summary: eventCount > 0 ? `${eventCount} 条公开事件引用。` : "最近事件没有公开引用这张牌。"
    },
    {
      count: rulesSection?.body ? 1 : 0,
      key: "rules",
      label: "规则文本",
      sourceLabel: "官方卡牌快照",
      state: rulesSection?.body ? "ready" : "empty",
      stateLabel: rulesSection?.body ? "已展示" : "无文本",
      summary: rulesSection?.body ? "规则文本来自卡牌快照/官方文本映射。" : "服务端未提供卡面规则文本。"
    }
  ];
}

function buildVisibleInspectorPlan({
  actionCandidateCount,
  card,
  detailRows,
  objectContext,
  prompt,
  sourceObjectId
}: {
  actionCandidateCount: number;
  card: CardDetailPlanCard;
  detailRows: CardDetailRow[];
  objectContext?: TableObjectContext;
  prompt?: ActionPromptDto;
  sourceObjectId?: string;
}): CardDetailInspectorPlan {
  const serverInspection = objectContext?.serverInspection;
  const authority = inspectorAuthorityFor(objectContext, serverInspection);
  const serverGroups = serverInspection?.groups.map(cardDetailGroupFromServerInspection) ?? [];
  const serverCandidateGroup = serverGroups.find((group) => group.key === "candidate");
  const serverSupportGroups = serverGroups.filter((group) => group.key !== "candidate");
  const candidateRows = objectContext?.candidateLinks.map((candidate, index) => ({
    key: `candidate-${index}`,
    label: candidate.enabled ? "可提交" : "阻断",
    tone: candidate.enabled ? "good" : "warn",
    value: [
      candidate.commandType ?? candidate.label,
      candidate.roles.length > 0 ? candidate.roles.join("/") : "",
      (candidate.selectionSteps?.length ?? 0) > 0 ? `步骤 ${cardDetailSelectionStepSummary(candidate.selectionSteps ?? [])}` : "",
      candidate.requiredCommandFields.length > 0 ? `需 ${candidate.requiredCommandFields.join("/")}` : "",
      candidate.enabled ? "" : candidate.reason
    ].filter(Boolean).join(" / ")
  } satisfies CardDetailInspectorRow)) ?? [];
  const candidateStepRows = objectContext?.candidateLinks.flatMap((candidate, index) => {
    const summary = cardDetailSelectionStepSummary(candidate.selectionSteps ?? []);
    if (!summary) {
      return [];
    }

    return [{
      key: `selection-step-${index}`,
      label: candidate.enabled ? "可提交" : "阻断",
      tone: candidate.enabled ? "good" : "warn",
      value: `${candidate.commandType ?? candidate.label} / ${summary}`
    } satisfies CardDetailInspectorRow];
  }) ?? [];
  const eventRows = objectContext?.eventLinks.map((event, index) => ({
    key: `event-${index}`,
    label: event.role,
    value: `${event.kind}：${event.description}`
  } satisfies CardDetailInspectorRow)) ?? [];
  const stackRows = objectContext?.stackRoles.map((role, index) => ({
    key: `stack-${index}`,
    label: "结算链",
    tone: "info",
    value: role
  } satisfies CardDetailInspectorRow)) ?? [];
  const stateRows = (objectContext?.stateLabels.length ? objectContext.stateLabels : objectStateLabels(card.object)).map((state, index) => ({
    key: `state-${index}`,
    label: "状态",
    value: state
  } satisfies CardDetailInspectorRow));

  return {
    authorityLabel: authority.authorityLabel,
    authorityState: authority.authorityState,
    boundaryLabel: serverInspection?.boundary
      ?? objectContext?.contextBoundary
      ?? "公开对象：详情只汇总当前快照、行动提示、结算链和公开事件，不在前端重算规则。",
    sourceLabel: authority.sourceLabel,
    summaryRows: uniqueRowsByKey([
      ...(serverInspection?.summaryRows
        .filter((row) => row.key !== "source")
        .map(cardDetailRowFromServerInspection) ?? []),
      { key: "object", label: "对象", value: sourceObjectId || card.object?.objectId || "服务端未公开" },
      { key: "zone", label: "区域", value: objectContext?.zone.label ?? detailRows.find((row) => row.key === "zone")?.value ?? "服务端未公开" },
      { key: "candidate", label: "候选", value: `${objectContext?.promptEnabledCount ?? actionCandidateCount} 可提交 / ${objectContext?.promptDisabledCount ?? 0} 阻断` },
      { key: "source", label: "来源", value: objectContext ? tableObjectContextSourceLabel(objectContext) : serverInspectionSourceLabel(serverInspection?.source) },
      { key: "authority", label: "权威", value: tableObjectCandidateSourceLabel(objectContext?.candidateSource) }
    ]),
    groups: [
      {
        key: "identity",
        title: "对象身份",
        rows: [
          { key: "card-no", label: "编号", value: card.spec?.cardNo ?? card.object?.cardNo ?? "服务端未公开" },
          { key: "owner", label: "所属方", value: card.object?.ownerId ?? objectContext?.ownerId ?? "未知" },
          { key: "controller", label: "控制方", value: card.object?.controllerId ?? objectContext?.controllerId ?? "未知" },
          { key: "prompt", label: "行动窗口", value: prompt?.promptId ? `prompt ${prompt.promptId}` : prompt?.actionable ? "当前可行动" : "无当前行动窗口" },
          { key: "tick", label: "快照", value: prompt?.snapshotTick != null ? `tick ${prompt.snapshotTick}` : "未随 prompt 公布" }
        ]
      },
      {
        key: "state",
        title: "状态与区域",
        rows: [
          { key: "zone", label: "区域", value: objectContext?.zone.label ?? detailRows.find((row) => row.key === "zone")?.value ?? "服务端未公开" },
          ...stateRows
        ]
      },
      serverCandidateGroup ?? {
          emptyLabel: "当前 prompt 没有把这张牌关联到公开候选。",
          key: "candidate",
          title: "服务端候选",
          rows: candidateRows
        },
      ...(candidateStepRows.length > 0
        ? [{
            key: "selection-steps",
            title: "选择步骤",
            rows: candidateStepRows
          } satisfies CardDetailInspectorGroup]
        : []),
      ...serverSupportGroups,
      {
        emptyLabel: "当前结算链没有公开引用这张牌。",
        key: "stack",
        title: "结算链关联",
        rows: stackRows
      },
      {
        emptyLabel: "最近事件没有公开引用这张牌。",
        key: "events",
        title: "近期事件",
        rows: eventRows
      }
    ]
  };
}

function inspectorAuthorityFor(
  objectContext: TableObjectContext | undefined,
  serverInspection: TableObjectContext["serverInspection"] | undefined
): {
  authorityLabel: string;
  authorityState: CardDetailInspectorAuthorityState;
  sourceLabel: string;
} {
  if (serverInspection) {
    return {
      authorityLabel: "服务端检查摘要",
      authorityState: "server-inspection",
      sourceLabel: serverInspectionSourceLabel(serverInspection.source)
    };
  }

  if (objectContext?.candidateSource === "server") {
    return {
      authorityLabel: "服务端对象上下文",
      authorityState: "server-object-context",
      sourceLabel: tableObjectContextSourceLabel(objectContext)
    };
  }

  if (objectContext?.candidateSource === "derived") {
    return {
      authorityLabel: "公开候选只读派生",
      authorityState: "derived",
      sourceLabel: tableObjectContextSourceLabel(objectContext)
    };
  }

  return {
    authorityLabel: "公开快照索引",
    authorityState: "snapshot",
    sourceLabel: objectContext ? tableObjectContextSourceLabel(objectContext) : "公开快照索引"
  };
}

function cardDetailGroupFromServerInspection(group: ActionPromptObjectInspectionGroupDto): CardDetailInspectorGroup {
  return {
    emptyLabel: group.emptyLabel ?? undefined,
    key: group.key,
    rows: group.rows.map(cardDetailRowFromServerInspection),
    title: group.title
  };
}

function cardDetailRowFromServerInspection(row: ActionPromptObjectInspectionRowDto): CardDetailInspectorRow {
  return {
    key: row.key,
    label: row.label,
    tone: cardDetailToneFromServer(row.tone),
    value: row.value
  };
}

function cardDetailToneFromServer(tone: string | null | undefined): CardDetailTone | undefined {
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

function cardDetailSelectionStepSummary(
  steps: Array<{ choiceCount: number; index: number; label: string; objectChoiceCount: number; required: boolean; role: string }>
): string {
  return steps
    .filter((step) => step.required || step.objectChoiceCount > 0)
    .sort((left, right) => left.index - right.index || left.role.localeCompare(right.role))
    .slice(0, 4)
    .map((step) => `${step.label}${step.required ? "*" : ""} ${step.objectChoiceCount}/${step.choiceCount}`)
    .join(" / ");
}

function uniqueRowsByKey(rows: CardDetailInspectorRow[]): CardDetailInspectorRow[] {
  const seen = new Set<string>();
  return rows.filter((row) => {
    if (seen.has(row.key)) {
      return false;
    }

    seen.add(row.key);
    return true;
  });
}

function objectStateLabels(object?: CardObjectView): string[] {
  if (!object) {
    return [];
  }

  return [
    object.isExhausted ? "横置" : "",
    object.isAttacking ? "攻击中" : "",
    object.isDefending ? "防守中" : "",
    object.isFaceDown ? "面朝下" : "",
    object.attachedToObjectId ? "已贴附" : "",
    object.damage != null && object.damage > 0 ? `${object.damage} 伤害` : "",
    object.basePower != null && object.effectivePower != null && object.basePower !== object.effectivePower
      ? `基础 ${object.basePower} / 有效 ${object.effectivePower}`
      : ""
  ].filter(Boolean);
}

function formatLocation(location?: Record<string, unknown> | null): string {
  if (!location) {
    return "服务端未公开";
  }

  const playerId = typeof location.playerId === "string" ? location.playerId : "";
  const zone = typeof location.zone === "string" ? location.zone : "";
  const serverZoneLabel = typeof location.zoneLabel === "string" ? location.zoneLabel.trim() : "";
  return [playerId, serverZoneLabel || zoneLabel(zone)].filter(Boolean).join(" / ") || "服务端未公开";
}

function serverInspectionSourceLabel(source: string | undefined): string {
  switch (source) {
    case "server-action-prompt":
      return "服务端检查摘要";
    default:
      return source?.trim() || "服务端检查摘要";
  }
}

function zoneLabel(zone: string): string {
  switch (zone) {
    case "LEGEND":
      return "传奇区";
    case "CHAMPION":
      return "英雄区";
    case "MAIN_DECK":
      return "主牌库";
    case "RUNE_DECK":
      return "符文牌堆";
    case "HAND":
      return "手牌";
    case "BASE":
      return "基地";
    case "BATTLEFIELD":
      return "战场";
    case "GRAVEYARD":
      return "已打出牌堆";
    case "BANISHED":
      return "放逐区";
    case "STACK":
      return "结算链";
    default:
      return zone ? "服务端区域" : "";
  }
}

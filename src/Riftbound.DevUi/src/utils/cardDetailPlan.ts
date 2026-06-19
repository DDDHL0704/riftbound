import type { BehaviorSpec } from "../types/catalog";
import type { ActionPromptCandidateDto, ActionPromptDto, CardObjectView } from "../types/protocol";
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
import type { TableObjectContext } from "./tableObjectContext";

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

export type CardDetailPlan = {
  actionCandidates: ActionPromptCandidateDto[];
  actionEmptyLabel: string;
  badges: CardDetailBadge[];
  detailRows: CardDetailRow[];
  hidden: boolean;
  hiddenMessage?: string;
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
      detailRows: [],
      hidden,
      hiddenMessage: "该对象未向当前玩家公开。前端只展示服务端快照允许的信息，不读取或推断卡名、费用、类型或规则文本。",
      sections: [],
      sourceObjectId,
      title
    };
  }

  const states = objectStateLabels(card.object);
  const actionCandidates = sourceCandidatesForPrompt(prompt, sourceObjectId);
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
    detailRows: [
      { key: "cost", label: "费用", value: costText(card.spec) },
      { key: "power", label: "战力", value: `${card.object?.effectivePower ?? card.object?.power ?? card.object?.basePower ?? "未知"}` },
      { key: "owner", label: "所属方", value: card.object?.ownerId ?? "未知" },
      { key: "controller", label: "控制方", value: card.object?.controllerId ?? "未知" },
      { key: "zone", label: "位置", value: objectContext?.zone.label ?? formatLocation(card.object?.location) }
    ],
    hidden,
    sections,
    sourceObjectId,
    title
  };
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
  return [playerId, zoneLabel(zone)].filter(Boolean).join(" / ") || "服务端未公开";
}

function zoneLabel(zone: string): string {
  switch (zone) {
    case "LEGEND":
      return "传奇区";
    case "CHAMPION":
      return "英雄区";
    case "MAIN_DECK":
      return "主牌堆";
    case "RUNE_DECK":
      return "符文牌堆";
    case "HAND":
      return "手牌";
    case "BASE":
      return "基地";
    case "BATTLEFIELD":
      return "战场";
    case "GRAVEYARD":
      return "废牌堆";
    case "BANISHED":
      return "放逐区";
    case "STACK":
      return "结算链";
    default:
      return zone ? "服务端区域" : "";
  }
}

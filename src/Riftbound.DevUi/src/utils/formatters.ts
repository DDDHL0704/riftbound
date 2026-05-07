import { BehaviorSpec } from "../types/catalog";
import { ActionPromptCandidateDto, CardObjectView, RunePoolView } from "../types/protocol";

export function costText(spec?: BehaviorSpec): string {
  if (!spec) {
    return "费用未知";
  }

  const parts = [
    spec.cost?.mana != null ? `${spec.cost.mana} 法力` : "",
    spec.cost?.power != null ? `${spec.cost.power} 符能` : "",
    spec.cost?.returnEnergy != null ? `${spec.cost.returnEnergy} 回能` : ""
  ].filter(Boolean);

  return parts.length > 0 ? parts.join(" / ") : "无费用";
}

export function keywordsText(spec?: BehaviorSpec): string {
  if (!spec || spec.keywords.length === 0) {
    return "无关键词";
  }

  return spec.keywords.map((keyword) => keyword.value ? `${keyword.keyword} ${keyword.value}` : keyword.keyword).join("、");
}

export function objectTypeText(object?: CardObjectView, spec?: BehaviorSpec): string {
  const tags = object?.tags ?? [];
  if (tags.includes("CARD_TYPE:UNIT")) {
    return "单位";
  }

  if (tags.includes("CARD_TYPE:EQUIPMENT")) {
    return "装备";
  }

  if (tags.includes("CARD_TYPE:BATTLEFIELD")) {
    return "战场";
  }

  if (tags.includes("CARD_TYPE:RUNE")) {
    return "符文";
  }

  if (tags.includes("CARD_TYPE:SPELL")) {
    return "法术";
  }

  return spec?.cardCategoryName ?? "对象";
}

export function conformanceLabel(tier?: string): string {
  switch (tier) {
    case "representative-rule-pass":
      return "代表性规则通过";
    case "full-official-rule-pass":
      return "官方完整通过（待复审）";
    case "manual-boundary":
      return "人工边界";
    case "blocked":
      return "阻断";
    default:
      return tier ?? "未知";
  }
}

export function conformanceTone(tier?: string): "good" | "warn" | "bad" | "info" | "neutral" {
  switch (tier) {
    case "full-official-rule-pass":
      return "warn";
    case "representative-rule-pass":
      return "info";
    case "blocked":
      return "bad";
    case "manual-boundary":
      return "warn";
    default:
      return "neutral";
  }
}

export function statusLabel(status?: string): string {
  switch (status) {
    case "implemented":
      return "已实现代表路径";
    case "manual-rule-required":
      return "需要人工规则";
    case "unimplemented":
      return "未实现";
    default:
      return status ?? "未知";
  }
}

export function promptActionLabel(candidate: ActionPromptCandidateDto): string {
  return candidate.label || actionLabel(candidate.action);
}

export function actionLabel(action: string): string {
  const labels: Record<string, string> = {
    READY: "准备",
    SUBMIT_DECK: "提交卡组",
    MULLIGAN: "确认起手调整",
    PASS_PRIORITY: "让过优先行动权",
    PASS_FOCUS: "让过焦点",
    PASS: "让过",
    END_TURN: "结束回合",
    PLAY_CARD: "打出卡牌",
    REVEAL_CARD: "翻开待命",
    TAP_RUNE: "横置符文",
    RECYCLE_RUNE: "回收符文",
    MOVE_UNIT: "移动单位",
    ASSEMBLE_EQUIPMENT: "装配装备",
    DECLARE_BATTLE: "声明战斗",
    ACTIVATE_ABILITY: "激活技能",
    LEGEND_ACT: "传奇行动",
    WAIT: "等待服务端规则任务"
  };
  return labels[action] ?? action;
}

export function runePoolText(pool?: RunePoolView): string {
  if (!pool) {
    return "法力 0 / 符能 0";
  }

  const traitText = Object.entries(pool.powerByTrait ?? {})
    .filter(([, value]) => value > 0)
    .map(([trait, value]) => `${trait}:${value}`)
    .join(" ");
  return `法力 ${pool.mana ?? 0} / 符能 ${pool.power ?? pool.totalPower ?? 0}${traitText ? ` / ${traitText}` : ""}`;
}

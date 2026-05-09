import { BehaviorSpec } from "../types/catalog";
import { ActionPromptCandidateDto, CardObjectView, ConnectionStatus, RunePoolView } from "../types/protocol";

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

export function rulesText(text?: string): string {
  if (!text) {
    return "服务端未提供卡面规则文本。";
  }

  return text
    .replace(/\{\{([^}]+)\}\}/g, (_match, token: string) => ruleTokenLabel(token.trim()))
    .replace(/\s+([，。：；、）])/g, "$1")
    .replace(/\s+（/g, "（")
    .replace(/（\s+/g, "（")
    .replace(/\n{3,}/g, "\n\n")
    .trim();
}

function ruleTokenLabel(token: string): string {
  const cleaned = token.replace(/>+$/g, "");

  if (token === ">>") {
    return "";
  }

  if (/^\d+$/.test(cleaned)) {
    return cleaned;
  }

  switch (cleaned) {
    case "S":
      return "战力";
    case "A":
      return "任意符能";
    case "红色":
    case "绿色":
    case "蓝色":
    case "黄色":
    case "紫色":
      return `${cleaned}符能`;
    default:
      return cleaned;
  }
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
      return "完整规则证据（待最终复审）";
    case "manual-boundary":
      return "人工边界";
    case "blocked":
      return "阻断";
    default:
      return protocolFallback(tier, "服务端证据", "未知");
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
    case "implemented-representative":
      return "已实现代表路径";
    case "manual-rule-required":
      return "需要人工规则";
    case "unimplemented":
      return "未实现";
    case "recognized-deferred":
      return "已识别待补";
    case "recognized-delegated":
      return "已识别代理";
    default:
      return protocolFallback(status, "服务端状态", "未知");
  }
}

export function readinessLabel(status?: string): string {
  switch (status) {
    case "NOT_READY":
      return "尚未就绪";
    case "READY":
      return "已就绪";
    default:
      return protocolFallback(status, "服务端就绪状态", "未知");
  }
}

export function serviceStatusLabel(status?: string): string {
  switch (status?.toLowerCase()) {
    case "ok":
    case "healthy":
      return "正常";
    default:
      return protocolFallback(status, "服务端状态", "未知");
  }
}

export function serviceNameLabel(service?: string): string {
  switch (service) {
    case "Riftbound.Api":
      return "符文战场 API";
    case "riftbound-dotnet":
      return "符文战场服务端";
    default:
      return protocolFallback(service, "服务端", "未知服务");
  }
}

export function serviceRoleLabel(role?: string): string {
  switch (role?.toLowerCase()) {
    case "development":
    case "dev":
      return "开发模式";
    case "production":
      return "生产模式";
    case "migration-skeleton":
      return "规则迁移模式";
    case "test":
    case "testing":
      return "测试模式";
    default:
      return protocolFallback(role, "服务端角色", "未知");
  }
}

export function connectionStatusLabel(status: ConnectionStatus): string {
  switch (status) {
    case "idle":
      return "未连接";
    case "connecting":
      return "连接中";
    case "connected":
      return "已连接";
    case "reconnecting":
      return "重连中";
    case "resyncing":
      return "重新同步中";
    case "disconnected":
      return "已断开";
    case "error":
      return "连接错误";
  }
}

export function connectionStatusTone(status: ConnectionStatus): "neutral" | "good" | "warn" | "bad" | "info" {
  switch (status) {
    case "idle":
      return "neutral";
    case "connecting":
      return "info";
    case "connected":
      return "good";
    case "reconnecting":
      return "warn";
    case "resyncing":
      return "info";
    case "disconnected":
      return "bad";
    case "error":
      return "bad";
  }
}

export function roomStatusLabel(status?: string): string {
  switch (status) {
    case "EMPTY":
      return "空房间";
    case "SEATING":
      return "等待入座";
    case "IN_PROGRESS":
      return "对局进行中";
    case "FINISHED":
      return "对局已结束";
    default:
      return protocolFallback(status, "服务端房间状态", "未知");
  }
}

export function roomStatusTone(status?: string): "neutral" | "good" | "warn" | "bad" | "info" {
  switch (status) {
    case "FINISHED":
      return "good";
    case "IN_PROGRESS":
      return "info";
    case "SEATING":
      return "warn";
    case "EMPTY":
      return "neutral";
    default:
      return "warn";
  }
}

export function matchPhaseLabel(phase?: string): string {
  switch (phase) {
    case "ROOM":
      return "房间阶段";
    case "MULLIGAN":
      return "起手调整";
    case "TURN_START":
      return "回合开始";
    case "MAIN":
      return "主阶段";
    case "TURN_END":
      return "回合结束";
    default:
      return protocolFallback(phase, "服务端阶段", "等待开局");
  }
}

export function timingStateLabel(state?: string): string {
  switch (state) {
    case "ROOM":
      return "房间窗口";
    case "MULLIGAN":
      return "起手调整";
    case "NEUTRAL_OPEN":
      return "普通开环";
    case "NEUTRAL_CLOSED":
      return "普通闭环";
    case "SPELL_DUEL_OPEN":
      return "法术对决开环";
    case "SPELL_DUEL_CLOSED":
      return "法术对决闭环";
    default:
      return protocolFallback(state, "服务端窗口", "未知窗口");
  }
}

export function promptActionLabel(candidate: ActionPromptCandidateDto): string {
  return candidate.label || actionLabel(candidate.action);
}

export function promptReasonLabel(reason: string | null | undefined, fallback = "服务端候选"): string {
  return playerFacingReason(reason) ?? fallback;
}

export function promptReasonTitle(reason: string | null | undefined, fallback = "服务端候选"): string | undefined {
  if (!reason?.trim()) {
    return undefined;
  }

  return playerFacingReason(reason) ?? fallback;
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
    SURRENDER: "投降",
    PLAY_CARD: "打出卡牌",
    REVEAL_CARD: "翻开待命",
    TAP_RUNE: "横置符文",
    RECYCLE_RUNE: "回收符文",
    MOVE_UNIT: "移动单位",
    ASSEMBLE_EQUIPMENT: "装配装备",
    DECLARE_BATTLE: "声明战斗",
    ACTIVATE_ABILITY: "激活技能",
    LEGEND_ACT: "传奇行动",
    PAY_COST: "支付费用",
    ASSIGN_COMBAT_DAMAGE: "分配战斗伤害",
    ORDER_TRIGGERS: "排列触发",
    WAIT: "等待服务端规则任务"
  };
  return labels[action] ?? protocolFallback(action, "服务端操作", "服务端操作");
}

function protocolFallback(value: string | undefined, protocolLabel: string, emptyLabel: string): string {
  if (!value || !value.trim()) {
    return emptyLabel;
  }

  return isProtocolToken(value) ? protocolLabel : value;
}

function isProtocolToken(value: string): boolean {
  const token = value.trim();
  return /^[A-Z0-9_:-]+$/.test(token) || /^[a-z0-9]+(?:[-_:][a-z0-9]+)+$/.test(token);
}

function playerFacingReason(reason: string | null | undefined): string | undefined {
  const cleaned = reason?.trim();
  if (!cleaned) {
    return undefined;
  }

  if (!/[\u3400-\u9fff]/.test(cleaned)) {
    return undefined;
  }

  return /[A-Z0-9]{2,}[_:-]/.test(cleaned) ? undefined : cleaned;
}

export function runeTraitLabel(trait?: string): string {
  switch (trait?.trim().toLowerCase()) {
    case "red":
    case "红色":
      return "红色符能";
    case "green":
    case "绿色":
      return "绿色符能";
    case "blue":
    case "蓝色":
      return "蓝色符能";
    case "yellow":
    case "黄色":
      return "黄色符能";
    case "purple":
    case "紫色":
      return "紫色符能";
    default:
      return "服务端符能";
  }
}

export function runePoolText(pool?: RunePoolView): string {
  if (!pool) {
    return "法力 0 / 符能 0";
  }

  const traitText = Object.entries(pool.powerByTrait ?? {})
    .filter(([, value]) => value > 0)
    .map(([trait, value]) => `${runeTraitLabel(trait)} ${value}`)
    .join(" / ");
  return `法力 ${pool.mana ?? 0} / 符能 ${pool.power ?? pool.totalPower ?? 0}${traitText ? ` / ${traitText}` : ""}`;
}

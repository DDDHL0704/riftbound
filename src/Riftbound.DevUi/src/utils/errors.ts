import { ErrorDto } from "../types/protocol";
import { redactInternalText } from "./redaction";

const errorCodeLabels: Record<string, string> = {
  CARD_NOT_IN_HAND: "手牌状态不匹配",
  CLIENT_INTENT_CONFLICT: "重复操作冲突",
  CLIENT_INTENT_ID_REQUIRED: "缺少操作编号",
  INSUFFICIENT_COST: "费用不足",
  INVALID_DECK: "卡组不合法",
  INVALID_RECONNECT_TOKEN: "重连凭证失效",
  INVALID_TARGET: "目标不合法",
  MATCH_FINISHED: "对局已结束",
  MATCH_NOT_STARTED: "对局尚未开始",
  PHASE_NOT_ALLOWED: "当前阶段不可执行",
  PLAYER_ID_REQUIRED: "缺少玩家身份",
  PLAYER_NOT_IN_ROOM: "玩家尚未入座",
  RECOVERY_INCONSISTENT: "恢复校验失败",
  ROOM_FULL: "房间已满",
  UNSUPPORTED_CARD_BEHAVIOR: "卡牌能力暂未开放",
  UNSUPPORTED_COMMAND: "操作暂不支持"
};

const errorMessageLabels: Record<string, string> = {
  CARD_NOT_IN_HAND: "服务端确认这张牌不在当前手牌中。",
  CLIENT_INTENT_CONFLICT: "这个操作编号已经对应过另一条命令，请重新提交最新操作。",
  CLIENT_INTENT_ID_REQUIRED: "客户端需要为每次提交提供唯一操作编号。",
  INSUFFICIENT_COST: "当前资源不足，无法支付这次操作。",
  INVALID_RECONNECT_TOKEN: "请重新连接并入座。",
  INVALID_TARGET: "请选择服务端行动提示允许的目标。",
  MATCH_FINISHED: "当前对局已经结算完成。",
  MATCH_NOT_STARTED: "双方完成卡组提交与准备后才能进入对局。",
  PHASE_NOT_ALLOWED: "请等待当前服务端窗口结算完成。",
  PLAYER_ID_REQUIRED: "请先在设置中选择玩家身份。",
  PLAYER_NOT_IN_ROOM: "请先连接房间。",
  RECOVERY_INCONSISTENT: "服务端回放恢复校验未通过，请重新同步。",
  ROOM_FULL: "该房间已经有两名玩家。",
  UNSUPPORTED_CARD_BEHAVIOR: "这张牌的对应能力还没有进入正式可玩路径。",
  UNSUPPORTED_COMMAND: "请使用当前服务端行动提示提供的操作。"
};

export function errorCodeLabel(code?: string | null): string {
  if (!code) {
    return "服务端错误";
  }

  return errorCodeLabels[code] ?? "服务端错误";
}

export function errorMessageLabel(error: ErrorDto): string {
  if (error.code === "INVALID_DECK" && error.message) {
    return `卡组不合法：${redactInternalText(error.message)}`;
  }

  return errorMessageLabels[error.code] ?? redactInternalText(error.message || "服务端拒绝了这次操作。");
}

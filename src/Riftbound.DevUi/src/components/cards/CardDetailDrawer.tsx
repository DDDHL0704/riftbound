import { Play, X } from "lucide-react";
import { ActionPromptCandidateDto, ActionPromptDto, GameCommand } from "../../types/protocol";
import { costText, keywordsText, promptActionLabel, statusLabel } from "../../utils/formatters";
import { isHiddenObject } from "../../utils/hiddenInfo";
import { Button } from "../ui/Button";
import { StatusPill } from "../ui/StatusPill";
import { InspectedCard, objectStateLabels } from "./CardFace";

type CardDetailDrawerProps = {
  card?: InspectedCard;
  onClose: () => void;
  onCommand?: (command: GameCommand) => void;
  prompt?: ActionPromptDto;
};

export function CardDetailDrawer({ card, onClose, onCommand, prompt }: CardDetailDrawerProps) {
  if (!card) {
    return null;
  }

  const hidden = isHiddenObject(card.object) && !card.spec;
  const title = hidden ? "未公开卡牌" : card.spec?.cardName ?? card.object?.cardNo ?? card.objectId ?? "未知卡牌";
  const states = objectStateLabels(card.object);
  const sourceObjectId = card.objectId ?? card.object?.objectId;
  const sourceActions = hidden ? [] : sourceCandidatesFor(prompt, sourceObjectId);

  return (
    <div className="detail-layer" role="dialog" aria-modal="true" aria-label="卡牌详情">
      <button className="detail-scrim" onClick={onClose} type="button" aria-label="关闭卡牌详情" />
      <aside className="detail-drawer">
        <header>
          <div>
            <span className="eyebrow">卡牌详情</span>
            <h2>{title}</h2>
          </div>
          <Button icon={<X size={18} />} onClick={onClose} variant="ghost">关闭</Button>
        </header>
        <div className="detail-section">
          <StatusPill tone={hidden ? "warn" : "info"}>{hidden ? "隐藏信息" : card.spec?.cardCategoryName ?? "公开对象"}</StatusPill>
          <StatusPill tone="neutral">{card.spec?.cardNo ?? card.object?.cardNo ?? card.objectId ?? "无编号"}</StatusPill>
          {card.spec && <StatusPill tone={card.spec.conformanceTier === "full-official-rule-pass" ? "good" : "warn"}>{statusLabel(card.spec.status)}</StatusPill>}
        </div>
        {hidden ? (
          <p className="detail-muted">该对象未向当前玩家公开。前端只展示服务端 snapshot 允许的信息，不读取或推断卡名、费用、类型或规则文本。</p>
        ) : (
          <>
            <dl className="detail-grid">
              <div>
                <dt>费用</dt>
                <dd>{costText(card.spec)}</dd>
              </div>
              <div>
                <dt>战力</dt>
                <dd>{card.object?.effectivePower ?? card.object?.power ?? card.object?.basePower ?? "未知"}</dd>
              </div>
              <div>
                <dt>所属方</dt>
                <dd>{card.object?.ownerId ?? "未知"}</dd>
              </div>
              <div>
                <dt>控制方</dt>
                <dd>{card.object?.controllerId ?? "未知"}</dd>
              </div>
              <div>
                <dt>对象 ID</dt>
                <dd>{card.objectId ?? card.object?.objectId ?? "无"}</dd>
              </div>
              <div>
                <dt>位置</dt>
                <dd>{formatLocation(card.object?.location)}</dd>
              </div>
            </dl>
            <section className="detail-section">
              <strong>关键词</strong>
              <p>{keywordsText(card.spec)}</p>
            </section>
            <section className="detail-section">
              <strong>规则文本</strong>
              <p className="card-rules">{card.spec?.officialText || "服务端未提供卡面规则文本。"}</p>
            </section>
            <section className="detail-section">
              <strong>对象状态</strong>
              <p>{states.length ? states.join("、") : "正常"}</p>
              {(card.object?.tags?.length ?? 0) > 0 && <p className="detail-muted">标签：{card.object?.tags?.join("、")}</p>}
              {(card.object?.untilEndOfTurnEffects?.length ?? 0) > 0 && <p className="detail-muted">本回合效果：{card.object?.untilEndOfTurnEffects?.join("、")}</p>}
            </section>
            <section className="detail-section detail-actions">
              <strong>可执行操作</strong>
              {sourceActions.length === 0 ? (
                <p className="detail-muted">当前服务端 prompt 没有给这张牌可提交的操作。</p>
              ) : (
                <div className="detail-action-list">
                  {sourceActions.map((candidate) => {
                    const command = commandForSourceCandidate(candidate, sourceObjectId, card);
                    return (
                      <Button
                        disabled={!candidate.enabled || !command || !onCommand}
                        icon={<Play size={16} />}
                        key={candidate.action}
                        onClick={() => {
                          if (command && onCommand) {
                            onCommand(command);
                            onClose();
                          }
                        }}
                        title={command ? candidate.reason : "该操作还需要服务端提供目标、模式或费用选择后才能提交"}
                        variant={candidate.enabled && command ? "primary" : "ghost"}
                      >
                        {promptActionLabel(candidate)}
                      </Button>
                    );
                  })}
                </div>
              )}
            </section>
          </>
        )}
      </aside>
    </div>
  );
}

function sourceCandidatesFor(prompt: ActionPromptDto | undefined, sourceObjectId: string | undefined): ActionPromptCandidateDto[] {
  if (!prompt || !sourceObjectId) {
    return [];
  }

  return (prompt.candidates ?? []).filter((candidate) =>
    (candidate.sources ?? []).some((source) => source.id === sourceObjectId));
}

function commandForSourceCandidate(
  candidate: ActionPromptCandidateDto,
  sourceObjectId: string | undefined,
  card: InspectedCard
): GameCommand | undefined {
  if (!sourceObjectId || !candidate.enabled) {
    return undefined;
  }

  if (candidate.action === "TAP_RUNE") {
    return { cmdType: "TAP_RUNE", sourceObjectId };
  }

  if (candidate.action === "PLAY_CARD" && card.object?.cardNo && !requiresFurtherChoice(candidate)) {
    return { cmdType: "PLAY_CARD", sourceObjectId, cardNo: card.object.cardNo, targetObjectIds: [] };
  }

  return undefined;
}

function requiresFurtherChoice(candidate: ActionPromptCandidateDto): boolean {
  return Boolean(
    (candidate.targets?.length ?? 0) > 0
    || (candidate.destinations?.length ?? 0) > 0
    || (candidate.modes?.length ?? 0) > 0
    || (candidate.optionalCosts?.length ?? 0) > 0
  );
}

function formatLocation(location?: Record<string, unknown> | null): string {
  if (!location) {
    return "服务端未公开";
  }

  const playerId = typeof location.playerId === "string" ? location.playerId : "";
  const zone = typeof location.zone === "string" ? location.zone : "";
  const battlefield = typeof location.battlefieldObjectId === "string" ? location.battlefieldObjectId : "";
  return [playerId, zone, battlefield].filter(Boolean).join(" / ") || "服务端未公开";
}

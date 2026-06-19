import { Play, X } from "lucide-react";
import { ActionPromptDto, GameCommand, SnapshotDto } from "../../types/protocol";
import { commandForSourceCandidate, promptStampedCommand, sourceCandidatesForPrompt } from "../../utils/actionPromptCandidates";
import {
  conformanceLabel,
  conformanceTone,
  costText,
  keywordsText,
  objectTypeText,
  promptActionLabel,
  promptReasonTitle,
  rulesText,
  statusLabel
} from "../../utils/formatters";
import { isHiddenObject } from "../../utils/hiddenInfo";
import type { TableObjectContext } from "../../utils/tableObjectContext";
import { CandidateComposer, canComposeCandidate } from "../match/CandidateComposer";
import { Button } from "../ui/Button";
import { StatusPill } from "../ui/StatusPill";
import { InspectedCard, objectStateLabels } from "./CardFace";

type CardDetailDrawerProps = {
  card?: InspectedCard;
  onClose: () => void;
  onCommand?: (command: GameCommand) => void;
  objectContext?: TableObjectContext;
  prompt?: ActionPromptDto;
  snapshot?: SnapshotDto;
};

export function CardDetailDrawer({ card, onClose, onCommand, objectContext, prompt, snapshot }: CardDetailDrawerProps) {
  if (!card) {
    return null;
  }

  const hidden = isHiddenObject(card.object) && !card.spec;
  const title = hidden ? "未公开卡牌" : card.spec?.cardName ?? card.object?.cardNo ?? "未知卡牌";
  const states = objectStateLabels(card.object);
  const sourceObjectId = card.objectId ?? card.object?.objectId;
  const sourceActions = hidden ? [] : sourceCandidatesForPrompt(prompt, sourceObjectId);
  const stampedOnCommand = onCommand
    ? (command: GameCommand) => onCommand(promptStampedCommand(command, prompt))
    : undefined;

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
          <StatusPill tone={hidden ? "warn" : "info"}>{hidden ? "隐藏信息" : objectTypeText(card.object, card.spec)}</StatusPill>
          <StatusPill tone="neutral">{hidden ? "未公开" : card.spec?.cardNo ?? card.object?.cardNo ?? "无编号"}</StatusPill>
          {card.spec && <StatusPill tone={conformanceTone(card.spec.conformanceTier)}>{conformanceLabel(card.spec.conformanceTier)}</StatusPill>}
          {card.spec && <StatusPill tone={card.spec.status === "implemented" ? "info" : "warn"}>{statusLabel(card.spec.status)}</StatusPill>}
        </div>
        {hidden ? (
          <p className="detail-muted">该对象未向当前玩家公开。前端只展示服务端快照允许的信息，不读取或推断卡名、费用、类型或规则文本。</p>
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
                <dt>位置</dt>
                <dd>{objectContext?.zone.label ?? formatLocation(card.object?.location)}</dd>
              </div>
            </dl>
            <DetailObjectContext context={objectContext} />
            <section className="detail-section">
              <strong>关键词</strong>
              <p>{keywordsText(card.spec)}</p>
            </section>
            <section className="detail-section">
              <strong>规则文本</strong>
              <p className="card-rules">{rulesText(card.spec?.officialText)}</p>
            </section>
            {card.spec && (
              <section className="detail-section">
                <strong>服务端证据</strong>
                <p>{conformanceLabel(card.spec.conformanceTier)}：完整官方规则完成度以最终复审为准。</p>
                <p>{statusLabel(card.spec.status)}：前端只提交服务端当前候选允许的操作。</p>
              </section>
            )}
            <section className="detail-section">
              <strong>对象状态</strong>
              <p>{states.length ? states.join("、") : "正常"}</p>
            </section>
            <section className="detail-section detail-actions">
              <strong>服务端可提交操作</strong>
              {sourceActions.length === 0 ? (
                <p className="detail-muted">当前服务端行动提示没有给这张牌可提交的操作。</p>
              ) : (
                <div className="detail-action-list">
                  {sourceActions.map((candidate) => {
                    const command = commandForSourceCandidate(candidate, sourceObjectId);

                    if (canComposeCandidate(candidate) && stampedOnCommand) {
                      return (
                        <CandidateComposer
                          candidate={candidate}
                          disabledByConnection={false}
                          forcedSourceObjectId={sourceObjectId}
                          key={candidate.action}
                          onCommand={stampedOnCommand}
                          onSubmitted={onClose}
                          prompt={prompt}
                          snapshot={snapshot}
                        />
                      );
                    }

                    return (
                      <Button
                        disabled={!candidate.enabled || !command || !stampedOnCommand}
                        icon={<Play size={16} />}
                        key={candidate.action}
                        onClick={() => {
                          if (command && stampedOnCommand) {
                            stampedOnCommand(command);
                            onClose();
                          }
                        }}
                        title={command ? promptReasonTitle(candidate.reason) : "该操作还需要服务端提供目标、模式或费用选择后才能提交"}
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

function DetailObjectContext({ context }: { context?: TableObjectContext }) {
  if (!context) {
    return (
      <section className="detail-section">
        <strong>规则上下文</strong>
        <p className="detail-muted">当前快照没有公开该对象的上下文索引。</p>
      </section>
    );
  }

  const events = context.eventLinks.slice(-4).reverse();
  return (
    <section className="detail-section detail-context" aria-label="卡牌规则上下文">
      <strong>规则上下文</strong>
      <div className="detail-context-grid">
        <span>
          <small>区域</small>
          <b>{context.zone.label}</b>
        </span>
        <span>
          <small>服务端候选</small>
          <b>{context.promptEnabledCount} 可用 / {context.promptDisabledCount} 阻断</b>
        </span>
        <span>
          <small>状态</small>
          <b>{context.stateLabels.join(" / ")}</b>
        </span>
      </div>
      {context.stackRoles.length > 0 && <p>结算链：{context.stackRoles.join(" / ")}</p>}
      {context.candidateLinks.length > 0 && (
        <p>候选：{context.candidateLinks.slice(0, 3).map((candidate) => `${candidate.label}（${candidate.roles.join("/") || "关联"}）`).join("、")}</p>
      )}
      {events.length > 0 ? (
        <ol className="detail-context-events">
          {events.map((event, index) => (
            <li key={`${event.kind}-${event.role}-${index}`}>
              <span>{event.role}</span>
              <b>{event.description}</b>
            </li>
          ))}
        </ol>
      ) : (
        <p className="detail-muted">暂无公开关联事件。</p>
      )}
    </section>
  );
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

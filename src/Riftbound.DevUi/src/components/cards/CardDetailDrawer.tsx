import { Play, X } from "lucide-react";
import { useEffect, useRef } from "react";
import type { ActionPromptContractDto, ActionPromptDto, GameCommand, SnapshotDto } from "../../types/protocol";
import { commandForSourceCandidate, promptStampedCommand, sourceCandidatesForPrompt } from "../../utils/actionPromptCandidates";
import { buildFocusedActionModel, type FocusedActionModel } from "../../utils/focusedActionModel";
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
import { buildPromptInteractionModel } from "../../utils/promptInteraction";
import type { TableObjectContext } from "../../utils/tableObjectContext";
import { CandidateComposer, canComposeCandidate } from "../match/CandidateComposer";
import { WireObjectContextSummary } from "../match/WireObjectContextSummary";
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
  const closeButtonRef = useRef<HTMLButtonElement | null>(null);
  const drawerRef = useRef<HTMLElement | null>(null);
  const onCloseRef = useRef(onClose);
  const previousActiveElementRef = useRef<HTMLElement | null>(null);
  onCloseRef.current = onClose;

  useEffect(() => {
    if (!card) {
      return undefined;
    }

    previousActiveElementRef.current = document.activeElement instanceof HTMLElement
      ? document.activeElement
      : null;
    const previousOverflow = document.body.style.overflow;
    document.body.style.overflow = "hidden";
    window.setTimeout(() => closeButtonRef.current?.focus(), 0);

    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        event.preventDefault();
        onCloseRef.current();
        return;
      }

      if (event.key === "Tab") {
        trapDialogFocus(event, drawerRef.current);
      }
    };

    document.addEventListener("keydown", handleKeyDown);
    return () => {
      document.removeEventListener("keydown", handleKeyDown);
      document.body.style.overflow = previousOverflow;
      previousActiveElementRef.current?.focus();
      previousActiveElementRef.current = null;
    };
  }, [card]);

  if (!card) {
    return null;
  }

  const hidden = isHiddenObject(card.object) && !card.spec;
  const title = hidden ? "未公开卡牌" : card.spec?.cardName ?? card.object?.cardNo ?? "未知卡牌";
  const states = objectStateLabels(card.object);
  const sourceObjectId = card.objectId ?? card.object?.objectId;
  const sourceActions = hidden ? [] : sourceCandidatesForPrompt(prompt, sourceObjectId);
  const detailFocusModel = buildFocusedActionModel({
    interactionModel: buildPromptInteractionModel(prompt),
    prompt,
    sourceObjectId
  });
  const stampedOnCommand = onCommand
    ? (command: GameCommand) => onCommand(promptStampedCommand(command, prompt))
    : undefined;

  return (
    <div className="detail-layer" role="dialog" aria-modal="true" aria-labelledby="card-detail-title" data-detail-dialog-state="open">
      <button className="detail-scrim" onClick={onClose} type="button" aria-label="关闭卡牌详情" />
      <aside className="detail-drawer" ref={drawerRef} tabIndex={-1}>
        <header>
          <div>
            <span className="eyebrow">卡牌详情</span>
            <h2 id="card-detail-title">{title}</h2>
          </div>
          <Button icon={<X size={18} />} onClick={onClose} ref={closeButtonRef} variant="ghost">关闭</Button>
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
            <DetailObjectContext context={objectContext} contract={prompt?.contract} focusModel={detailFocusModel} />
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

function trapDialogFocus(event: KeyboardEvent, container: HTMLElement | null) {
  if (!container) {
    return;
  }

  const focusable = Array.from(container.querySelectorAll<HTMLElement>(
    [
      "a[href]",
      "button:not([disabled])",
      "input:not([disabled])",
      "select:not([disabled])",
      "textarea:not([disabled])",
      "[tabindex]:not([tabindex='-1'])"
    ].join(",")
  )).filter((element) => element.offsetParent !== null || element === document.activeElement);

  if (focusable.length === 0) {
    event.preventDefault();
    container.focus();
    return;
  }

  const first = focusable[0];
  const last = focusable[focusable.length - 1];
  if (event.shiftKey && document.activeElement === first) {
    event.preventDefault();
    last.focus();
    return;
  }

  if (!event.shiftKey && document.activeElement === last) {
    event.preventDefault();
    first.focus();
  }
}

function DetailObjectContext({
  context,
  contract,
  focusModel
}: {
  context?: TableObjectContext;
  contract?: ActionPromptContractDto | null;
  focusModel?: FocusedActionModel;
}) {
  if (!context) {
    return (
      <section className="detail-section">
        <strong>规则上下文</strong>
        <p className="detail-muted">当前快照没有公开该对象的上下文索引。</p>
      </section>
    );
  }

  return (
    <section className="detail-section detail-context" aria-label="卡牌规则上下文">
      <strong>规则上下文</strong>
      <WireObjectContextSummary context={context} contract={contract} focusModel={focusModel} />
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

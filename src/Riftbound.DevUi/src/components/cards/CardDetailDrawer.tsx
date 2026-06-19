import { Play, X } from "lucide-react";
import { useEffect, useRef } from "react";
import type { ActionPromptContractDto, ActionPromptDto, GameCommand, SnapshotDto } from "../../types/protocol";
import { commandForSourceCandidate, promptStampedCommand } from "../../utils/actionPromptCandidates";
import { buildCardDetailPlan } from "../../utils/cardDetailPlan";
import { buildFocusedActionModel, type FocusedActionModel } from "../../utils/focusedActionModel";
import { promptActionLabel, promptReasonTitle } from "../../utils/formatters";
import { buildPromptInteractionModel } from "../../utils/promptInteraction";
import type { TableObjectContext } from "../../utils/tableObjectContext";
import { CandidateComposer, canComposeCandidate } from "../match/CandidateComposer";
import { WireObjectContextSummary } from "../match/WireObjectContextSummary";
import { Button } from "../ui/Button";
import { StatusPill } from "../ui/StatusPill";
import { InspectedCard } from "./CardFace";

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

  const detailPlan = buildCardDetailPlan({ card, objectContext, prompt });
  if (!detailPlan) {
    return null;
  }

  const sourceObjectId = detailPlan.sourceObjectId;
  const sourceActions = detailPlan.actionCandidates;
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
            <h2 id="card-detail-title">{detailPlan.title}</h2>
          </div>
          <Button icon={<X size={18} />} onClick={onClose} ref={closeButtonRef} variant="ghost">关闭</Button>
        </header>
        <div className="detail-section">
          {detailPlan.badges.map((badge) => (
            <StatusPill key={badge.key} tone={badge.tone}>{badge.label}</StatusPill>
          ))}
        </div>
        {detailPlan.hidden ? (
          <p className="detail-muted">{detailPlan.hiddenMessage}</p>
        ) : (
          <>
            <dl className="detail-grid">
              {detailPlan.detailRows.map((row) => (
                <div key={row.key}>
                  <dt>{row.label}</dt>
                  <dd>{row.value}</dd>
                </div>
              ))}
            </dl>
            <DetailObjectContext context={objectContext} contract={prompt?.contract} focusModel={detailFocusModel} />
            {detailPlan.sections.map((section) => (
              <section className="detail-section" key={section.key}>
                <strong>{section.title}</strong>
                <p className={section.key === "rules" ? "card-rules" : undefined}>{section.body}</p>
              </section>
            ))}
            <section className="detail-section detail-actions">
              <strong>服务端可提交操作</strong>
              {sourceActions.length === 0 ? (
                <p className="detail-muted">{detailPlan.actionEmptyLabel}</p>
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

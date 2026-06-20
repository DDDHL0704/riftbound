import { X } from "lucide-react";
import { useEffect, useRef } from "react";
import type { ActionPromptContractDto, ActionPromptDto, GameCommand, SnapshotDto } from "../../types/protocol";
import { buildCardDetailPlan, type CardDetailInspectorPlan } from "../../utils/cardDetailPlan";
import type { CandidateSelectionDraft } from "../../utils/candidateSelectionDraft";
import type { FocusedActionModel } from "../../utils/focusedActionModel";
import type { ServerSubmissionGatePlan } from "../../utils/serverSubmissionGatePlan";
import type { TableObjectContext } from "../../utils/tableObjectContext";
import { buildWireCardDetailActionPlan } from "../../utils/wireCardDetailActionPlan";
import { buildWireFocusedInteractionPlan } from "../../utils/wireFocusedInteractionPlan";
import { WireFocusedActionEntryList } from "../match/WireFocusedActionEntryList";
import { WireFocusedActionSummary } from "../match/WireFocusedActionSummary";
import { WireFocusedInteractionGrammar } from "../match/WireFocusedInteractionGrammar";
import { WireFocusedLegalActionMatrix } from "../match/WireFocusedLegalActionMatrix";
import { WireFocusedReadinessStrip } from "../match/WireFocusedReadinessStrip";
import { WireFocusedSelectionGuide } from "../match/WireFocusedSelectionGuide";
import { WireObjectContextSummary } from "../match/WireObjectContextSummary";
import { WirePromptCandidateRow } from "../match/WirePromptCandidateRow";
import { Button } from "../ui/Button";
import { StatusPill } from "../ui/StatusPill";
import { InspectedCard } from "./CardFace";

type CardDetailDrawerProps = {
  card?: InspectedCard;
  disabledByConnection?: boolean;
  onClose: () => void;
  onCommand?: (command: GameCommand) => void;
  onInspectObject?: (objectId: string) => void;
  objectContext?: TableObjectContext;
  prompt?: ActionPromptDto;
  selectionDraft?: CandidateSelectionDraft;
  snapshot?: SnapshotDto;
  submissionGate?: ServerSubmissionGatePlan;
};

export function CardDetailDrawer({
  card,
  disabledByConnection = false,
  onClose,
  onCommand,
  onInspectObject,
  objectContext,
  prompt,
  selectionDraft,
  snapshot,
  submissionGate
}: CardDetailDrawerProps) {
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
  const detailInteractionPlan = buildWireFocusedInteractionPlan({
    canSubmitCommands: Boolean(onCommand),
    disabledByConnection,
    prompt,
    selectionDraft,
    snapshot,
    sourceControllerId: card.object?.controllerId,
    sourceObjectId
  });
  const detailActionPlan = buildWireCardDetailActionPlan({
    canSubmitCommands: Boolean(onCommand),
    detailPlan,
    disabledByConnection
  });

  return (
    <div
      className="detail-layer"
      role="dialog"
      aria-modal="true"
      aria-labelledby="card-detail-title"
      data-card-detail-connection-state={disabledByConnection ? "blocked" : "ready"}
      data-detail-dialog-state="open"
    >
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
          <>
            <p className="detail-muted">{detailPlan.hiddenMessage}</p>
            <DetailInspector inspector={detailPlan.inspector} />
          </>
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
            <DetailObjectContext context={objectContext} contract={prompt?.contract} focusModel={detailInteractionPlan.focusModel} />
            <WireFocusedReadinessStrip plan={detailInteractionPlan} />
            <WireFocusedActionSummary focusModel={detailInteractionPlan.focusModel} />
            <WireFocusedInteractionGrammar plan={detailInteractionPlan.grammarPlan} />
            <WireFocusedSelectionGuide plan={detailInteractionPlan} />
            <WireFocusedLegalActionMatrix plan={detailInteractionPlan} />
            <DetailRelatedCandidates
              onInspectObject={onInspectObject}
              plan={detailInteractionPlan}
              selectedObjectId={sourceObjectId}
            />
            <DetailInspector inspector={detailPlan.inspector} />
            {detailPlan.sections.map((section) => (
              <section className="detail-section" key={section.key}>
                <strong>{section.title}</strong>
                <p className={section.key === "rules" ? "card-rules" : undefined}>{section.body}</p>
              </section>
            ))}
            <section
              className="detail-section detail-actions"
              data-card-detail-actions-state={detailActionPlan.state}
              data-card-detail-actions-source={detailActionPlan.sourceObjectId ?? ""}
            >
              <strong>服务端可提交操作</strong>
              <span className="detail-muted" data-card-detail-actions-label>{detailActionPlan.stateLabel}</span>
              <dl className="detail-action-summary" aria-label="卡牌详情操作摘要">
                {detailActionPlan.summaryRows.map((row) => (
                  <div data-card-detail-action-summary={row.key} key={row.key}>
                    <dt>{row.label}</dt>
                    <dd>{row.value}</dd>
                  </div>
                ))}
              </dl>
              {detailActionPlan.entries.length === 0 ? (
                <p className="detail-muted">{detailActionPlan.emptyLabel}</p>
              ) : (
                <WireFocusedActionEntryList
                  className="detail-action-list"
                  dataAttributes={{
                    count: "data-card-detail-action-count",
                    entry: "data-card-detail-action-entry",
                    mode: "data-card-detail-action-mode"
                  }}
                  disabledByConnection={disabledByConnection}
                  entryClassName="detail-action-entry"
                  onCommand={onCommand}
                  onSubmitted={onClose}
                  plan={detailInteractionPlan}
                  prompt={prompt}
                  snapshot={snapshot}
                  submissionGate={submissionGate}
                />
              )}
            </section>
          </>
        )}
      </aside>
    </div>
  );
}

function DetailRelatedCandidates({
  onInspectObject,
  plan,
  selectedObjectId
}: {
  onInspectObject?: (objectId: string) => void;
  plan: ReturnType<typeof buildWireFocusedInteractionPlan>;
  selectedObjectId?: string;
}) {
  return (
    <section
      aria-label="卡牌相关服务端候选"
      className="detail-section detail-related-candidates"
      data-card-detail-related-candidate-count={plan.relatedCandidateRows.length}
    >
      <strong>相关服务端候选</strong>
      {plan.relatedCandidateRows.length === 0 ? (
        <span className="detail-muted">该对象当前未出现在服务端行动候选中。</span>
      ) : (
        <div className="detail-related-candidate-list">
          {plan.relatedCandidateRows.slice(0, 5).map((row) => (
            <WirePromptCandidateRow
              key={row.key}
              objects={plan.objectIndex}
              onInspectObject={onInspectObject}
              row={row}
              selectedObjectId={selectedObjectId}
            />
          ))}
        </div>
      )}
    </section>
  );
}

function DetailInspector({ inspector }: { inspector: CardDetailInspectorPlan }) {
  return (
    <section className="detail-section detail-inspector" aria-label="卡牌检查" data-card-detail-inspector>
      <div className="detail-inspector-heading">
        <strong>卡牌检查</strong>
        <span>{inspector.boundaryLabel}</span>
      </div>
      <dl className="detail-inspector-summary">
        {inspector.summaryRows.map((row) => (
          <div data-card-detail-inspector-summary={row.key} key={row.key}>
            <dt>{row.label}</dt>
            <dd>{row.value}</dd>
          </div>
        ))}
      </dl>
      <div className="detail-inspector-groups">
        {inspector.groups.map((group) => (
          <section className="detail-inspector-group" data-card-detail-inspector-group={group.key} key={group.key}>
            <strong>{group.title}</strong>
            {group.rows.length === 0 ? (
              <p className="detail-muted">{group.emptyLabel ?? "当前没有公开记录。"}</p>
            ) : (
              <dl>
                {group.rows.slice(0, 6).map((row) => (
                  <div data-card-detail-inspector-row={`${group.key}:${row.key}`} key={row.key}>
                    <dt>{row.label}</dt>
                    <dd>{row.value}</dd>
                  </div>
                ))}
              </dl>
            )}
          </section>
        ))}
      </div>
    </section>
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

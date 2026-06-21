import { X } from "lucide-react";
import { useEffect, useRef, useState } from "react";
import type { ActionPromptContractDto, ActionPromptDto, SnapshotDto } from "../../types/protocol";
import { buildCardDetailPlan, type CardDetailInspectorPlan, type CardDetailPlan } from "../../utils/cardDetailPlan";
import type { CandidateSelectionDraft } from "../../utils/candidateSelectionDraft";
import type { CommandSubmitHandler, CommandSubmissionUiSource } from "../../utils/commandSubmissionFollowupPlan";
import type { FocusedActionModel } from "../../utils/focusedActionModel";
import { promptStampedCommand } from "../../utils/actionPromptCandidates";
import type { ServerSubmissionGatePlan } from "../../utils/serverSubmissionGatePlan";
import type { TableObjectContext } from "../../utils/tableObjectContext";
import {
  buildWireCardDetailActionPlan,
  type WireCardDetailActionEntryPlan,
  type WireCardDetailActionPlan,
  type WireCardDetailActionRouteRow
} from "../../utils/wireCardDetailActionPlan";
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
  onCommand?: CommandSubmitHandler;
  onInspectObject?: (objectId: string) => void;
  playerId?: string;
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
  playerId = "local",
  objectContext,
  prompt,
  selectionDraft,
  snapshot,
  submissionGate
}: CardDetailDrawerProps) {
  const [reviewEntryKey, setReviewEntryKey] = useState<string | undefined>();
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

  useEffect(() => {
    setReviewEntryKey(undefined);
  }, [card?.objectId]);

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
    playerId,
    prompt,
    selectionDraft,
    snapshot,
    sourceControllerId: card.object?.controllerId,
    sourceObjectId,
    submissionGate
  });
  const detailActionPlan = buildWireCardDetailActionPlan({
    canSubmitCommands: Boolean(onCommand),
    detailPlan,
    disabledByConnection
  });
  const reviewEntry = reviewEntryKey
    ? detailActionPlan.entries.find((entry) => entry.key === reviewEntryKey)
    : undefined;
  const reviewRoute = reviewEntry
    ? detailActionPlan.routeRows.find((row) => row.entryKey === reviewEntry.key)
    : undefined;

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
        <DetailCheckMap plan={detailPlan} />
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
              <DetailActionRoutes
                onReviewEntry={setReviewEntryKey}
                plan={detailActionPlan}
                selectedEntryKey={reviewEntry?.key}
              />
              {reviewEntry && reviewRoute ? (
                <DetailActionReview
                  entry={reviewEntry}
                  onCloseDetail={onClose}
                  onCloseReview={() => setReviewEntryKey(undefined)}
                  onCommand={onCommand}
                  prompt={prompt}
                  route={reviewRoute}
                  sourceObjectId={detailActionPlan.sourceObjectId}
                />
              ) : null}
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

function DetailCheckMap({ plan }: { plan: CardDetailPlan }) {
  return (
    <section
      aria-label="卡牌详情检查地图"
      className="detail-section detail-check-map"
      data-card-detail-check-map={plan.hidden ? "hidden" : "visible"}
      data-card-detail-check-map-count={plan.checkRows.length}
    >
      <strong>检查地图</strong>
      <ol>
        {plan.checkRows.map((row) => (
          <li
            data-card-detail-check-row={row.key}
            data-card-detail-check-row-count={row.count}
            data-card-detail-check-row-source={row.sourceLabel}
            data-card-detail-check-row-state={row.state}
            key={row.key}
          >
            <span>{row.label}</span>
            <strong>{row.stateLabel}</strong>
            <small>{row.summary}</small>
          </li>
        ))}
      </ol>
    </section>
  );
}

function DetailActionRoutes({
  onReviewEntry,
  plan,
  selectedEntryKey
}: {
  onReviewEntry?: (entryKey: string) => void;
  plan: WireCardDetailActionPlan;
  selectedEntryKey?: string;
}) {
  if (plan.routeRows.length === 0) {
    return null;
  }

  const entryKeys = new Set(plan.entries.map((entry) => entry.key));

  return (
    <section
      aria-label="卡牌详情候选入口路线"
      className="detail-action-routes"
      data-card-detail-route-count={plan.routeRows.length}
    >
      <header>
        <strong>候选入口路线</strong>
        <span>{plan.stateLabel}</span>
      </header>
      <ol>
        {plan.routeRows.map((row) => (
          <li
            data-card-detail-action-route={row.key}
            data-card-detail-action-route-entry={row.entryKey}
            data-card-detail-action-route-selected={row.entryKey === selectedEntryKey ? "true" : "false"}
            data-card-detail-action-route-state={row.state}
            key={row.key}
          >
            <span>{row.modeLabel}</span>
            <strong>{row.label}</strong>
            <small>{row.commandType} / {row.stateLabel}</small>
            <small>{row.fieldSummary}</small>
            <em>{row.nextStepLabel}</em>
            <Button
              data-card-detail-action-route-review={row.entryKey}
              disabled={!onReviewEntry || !entryKeys.has(row.entryKey)}
              onClick={() => onReviewEntry?.(row.entryKey)}
              variant="secondary"
            >
              审阅路线
            </Button>
          </li>
        ))}
      </ol>
    </section>
  );
}

function DetailActionReview({
  entry,
  onCloseDetail,
  onCloseReview,
  onCommand,
  prompt,
  route,
  sourceObjectId
}: {
  entry: WireCardDetailActionEntryPlan;
  onCloseDetail: () => void;
  onCloseReview: () => void;
  onCommand?: CommandSubmitHandler;
  prompt?: ActionPromptDto;
  route: WireCardDetailActionRouteRow;
  sourceObjectId?: string;
}) {
  const command = entry.actionPlan.command;
  const commandType = entry.candidate.commandTemplate?.cmdType?.trim() || entry.candidate.action;
  const canSubmit = Boolean(command && onCommand && !entry.actionPlan.disabled);

  return (
    <section
      aria-label="卡牌详情候选路线审阅"
      className="detail-action-review"
      data-card-detail-action-review-command={commandType}
      data-card-detail-action-review-entry={entry.key}
      data-card-detail-action-review-route-state={route.state}
      data-card-detail-action-review-source={sourceObjectId ?? ""}
      data-card-detail-action-review-state="open"
    >
      <header>
        <div>
          <strong>{route.label}</strong>
          <span>{route.modeLabel} / {route.stateLabel}</span>
        </div>
        <Button onClick={onCloseReview} variant="ghost">关闭审阅</Button>
      </header>
      <p className="detail-muted">只展示服务端候选、命令模板和当前选择草稿，不在前端推导额外规则。</p>
      <dl className="detail-action-review-grid" aria-label="卡牌详情候选路线审计">
        <div data-card-detail-action-review-row="action">
          <dt>动作</dt>
          <dd>{entry.candidate.action}</dd>
        </div>
        <div data-card-detail-action-review-row="command">
          <dt>命令</dt>
          <dd>{commandType}</dd>
        </div>
        <div data-card-detail-action-review-row="source">
          <dt>来源</dt>
          <dd>{sourceObjectId ?? "未绑定"}</dd>
        </div>
        <div data-card-detail-action-review-row="field">
          <dt>字段</dt>
          <dd>{route.fieldSummary}</dd>
        </div>
        <div data-card-detail-action-review-row="reason">
          <dt>原因</dt>
          <dd>{route.reasonLabel}</dd>
        </div>
        <div data-card-detail-action-review-row="next-step">
          <dt>下一步</dt>
          <dd>{route.nextStepLabel}</dd>
        </div>
      </dl>
      <Button
        data-card-detail-action-review-submit-state={canSubmit ? "ready" : "blocked"}
        disabled={!canSubmit}
        onClick={() => {
          if (!command || !onCommand) {
            return;
          }

          onCommand(promptStampedCommand(command, prompt), cardDetailActionUiSource(entry));
          onCloseReview();
          onCloseDetail();
        }}
        title={entry.actionPlan.title}
        variant={canSubmit ? "primary" : "ghost"}
      >
        提交这条服务端候选
      </Button>
    </section>
  );
}

function cardDetailActionUiSource(entry: WireCardDetailActionEntryPlan): Partial<CommandSubmissionUiSource> {
  return {
    candidateAction: entry.candidate.action,
    candidateLabel: entry.actionPlan.label,
    commandSource: entry.actionPlan.commandSource,
    commandSourceDetail: entry.actionPlan.commandSourceDetail,
    commandSourceLabel: entry.actionPlan.commandSourceLabel,
    label: entry.actionPlan.label
  };
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
    <section
      className="detail-section detail-inspector"
      aria-label="卡牌检查"
      data-card-detail-inspector
      data-card-detail-inspector-authority={inspector.authorityState}
      data-card-detail-inspector-source={inspector.sourceLabel}
    >
      <div className="detail-inspector-heading">
        <strong>卡牌检查</strong>
        <span>{inspector.sourceLabel} / {inspector.boundaryLabel}</span>
      </div>
      <dl className="detail-inspector-summary">
        <div data-card-detail-inspector-summary="authority">
          <dt>权威</dt>
          <dd>{inspector.authorityLabel}</dd>
        </div>
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

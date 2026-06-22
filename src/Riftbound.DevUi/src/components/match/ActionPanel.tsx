import { ArrowDown, ArrowUp, Check, Flag, Hourglass, ListOrdered, Play, Send, X } from "lucide-react";
import { type ReactNode, useEffect, useMemo, useState } from "react";
import type { ActionPromptCandidateDto, ActionPromptChoiceDto, ActionPromptDto, CombatDamageAssignmentDto, ConnectionStatus, SnapshotDto } from "../../types/protocol";
import { promptStampedCommand as withPromptStamp } from "../../utils/actionPromptCandidates";
import {
  buildDamageAssignmentModel,
  buildHandChoiceModel,
  buildOrderTriggersModel,
  buildPayCostModel,
  clampDamageInput,
  type PaymentChoiceItem,
  type TriggerOrderItem
} from "../../utils/actionPanelChoiceModels";
import {
  buildActionPanelCandidateCommandPlan,
  type ActionPanelCandidateButtonIcon,
  type ActionPanelCandidateCommandPlan,
  type ActionPanelDirectActionKind
} from "../../utils/actionPanelCommandPlan";
import { buildActionPanelPromptPlan, type ActionPanelGenericPromptPlan } from "../../utils/actionPanelPromptPlan";
import { buildActionPanelRenderPlan, type ActionPanelRenderEntry, type ActionPanelSubmitGate } from "../../utils/actionPanelRenderPlan";
import { promptActionLabel, promptReasonLabel, promptReasonTitle } from "../../utils/formatters";
import type { PromptInspectionPlan } from "../../utils/promptInspectionPlan";
import type { CommandSubmitHandler, CommandSubmissionUiSource } from "../../utils/commandSubmissionFollowupPlan";
import { buildServerSubmissionGatePlan, type ServerSubmissionGatePlan } from "../../utils/serverSubmissionGatePlan";
import { Button } from "../ui/Button";
import { ScrollArea } from "../ui/ScrollArea";
import { StatusPill } from "../ui/StatusPill";
import { CandidateComposer } from "./CandidateComposer";

type ActionPanelProps = {
  prompt?: ActionPromptDto;
  snapshot?: SnapshotDto;
  connectionStatus: ConnectionStatus;
  playerId: string;
  onReady: () => void;
  onSubmitStarterDeck: () => void;
  onCommand: CommandSubmitHandler;
};

export function ActionPanel({ prompt, snapshot, connectionStatus, playerId, onReady, onSubmitStarterDeck, onCommand }: ActionPanelProps) {
  const submissionGate = buildServerSubmissionGatePlan({ connectionStatus, prompt, snapshot });
  const promptPlan = buildActionPanelPromptPlan({ connectionStatus, playerId, prompt, snapshot, submissionGate });
  const renderPlan = buildActionPanelRenderPlan({
    canAct: promptPlan.canAct,
    prompt,
    submissionGate
  });

  return (
    <section className="side-panel action-panel">
      <ScrollArea className="action-panel-scroll">
        <div className="action-panel-content">
          <header>
            <span className="eyebrow">服务端行动提示</span>
            <h2>{promptPlan.promptTitle}</h2>
          </header>
          <div className="prompt-summary">
            <StatusPill tone={promptPlan.statusTone}>{promptPlan.statusLabel}</StatusPill>
            {promptPlan.rows.map((row) => <span key={row.key}>{row.text}</span>)}
          </div>
          {promptPlan.genericPrompt && <GenericPromptDetails plan={promptPlan.genericPrompt} />}
          {promptPlan.inspection && <ActionPromptInspection plan={promptPlan.inspection} />}
          <div
            className="action-buttons"
            data-action-render-count={renderPlan.entries.length}
            data-action-render-prompt-type={renderPlan.promptType}
            data-action-render-state={renderPlan.state}
          >
            {renderPlan.entries.length === 0 && <span className="empty-hint">{renderPlan.emptyLabel}</span>}
            {renderPlan.entries.map((entry) => (
              <ActionPanelRenderEntryView
                disabledByConnection={!submissionGate.canSubmit}
                entry={entry}
                key={entry.key}
                onCommand={onCommand}
                onReady={onReady}
                onSubmitStarterDeck={onSubmitStarterDeck}
                prompt={prompt}
                snapshot={snapshot}
                submissionGate={submissionGate}
              />
            ))}
          </div>
        </div>
      </ScrollArea>
    </section>
  );
}

function ActionPanelRenderEntryView({
  disabledByConnection,
  entry,
  onCommand,
  onReady,
  onSubmitStarterDeck,
  prompt,
  snapshot,
  submissionGate
}: {
  disabledByConnection: boolean;
  entry: ActionPanelRenderEntry;
  onCommand: CommandSubmitHandler;
  onReady: () => void;
  onSubmitStarterDeck: () => void;
  prompt?: ActionPromptDto;
  snapshot?: SnapshotDto;
  submissionGate: ServerSubmissionGatePlan;
}) {
  const candidate = entry.candidate;
  let content: ReactNode = null;

  switch (entry.kind) {
    case "mulligan":
      content = candidate ? (
        <MulliganCandidate
          candidate={candidate}
          onCommand={onCommand}
          prompt={prompt}
          submitGate={entry.submitGate}
        />
      ) : null;
      break;
    case "hand-choice":
      content = (
        <HandChoiceCandidate
          canAct={entry.canAct}
          candidate={candidate}
          onCommand={onCommand}
          prompt={prompt}
          readOnly={entry.readOnly}
          submitGate={entry.submitGate}
        />
      );
      break;
    case "damage-assignment":
      content = candidate ? (
        <DamageAssignmentCandidate
          candidate={candidate}
          onCommand={onCommand}
          prompt={prompt}
          snapshot={snapshot}
          submitGate={entry.submitGate}
        />
      ) : null;
      break;
    case "order-triggers":
      content = (
        <OrderTriggersCandidate
          canAct={entry.canAct}
          candidate={candidate}
          onCommand={onCommand}
          prompt={prompt}
          readOnly={entry.readOnly}
          submitGate={entry.submitGate}
        />
      );
      break;
    case "pay-cost":
      content = (
        <PayCostCandidate
          canAct={entry.canAct}
          candidate={candidate}
          onCommand={onCommand}
          prompt={prompt}
          submitGate={entry.submitGate}
        />
      );
      break;
    case "candidate-button":
      content = candidate ? (
        <CandidateButton
          candidate={candidate}
          disabledByConnection={disabledByConnection}
          onCommand={onCommand}
          onReady={onReady}
          onSubmitStarterDeck={onSubmitStarterDeck}
          prompt={prompt}
          snapshot={snapshot}
          submitGate={entry.submitGate}
          submissionGate={submissionGate}
        />
      ) : null;
      break;
  }

  if (!content) {
    return null;
  }

  return (
    <div
      className="action-render-entry"
      data-action-render-entry={entry.key}
      data-action-render-candidate-enabled={entry.candidate?.enabled == null ? "none" : entry.candidate.enabled ? "true" : "false"}
      data-action-render-kind={entry.kind}
      data-action-render-readonly={entry.readOnly ? "true" : "false"}
      data-action-render-submit-state={entry.submitGate.state}
    >
      {content}
    </div>
  );
}

function ActionPromptInspection({ plan }: { plan: PromptInspectionPlan }) {
  return (
    <div className="action-prompt-inspection" aria-label="服务端行动提示检查" data-action-prompt-inspection="true">
      <div className="action-prompt-inspection-heading">
        <strong>提示检查</strong>
        <span>{plan.sourceLabel}</span>
      </div>
      <p>{plan.boundaryLabel}</p>
      <dl className="action-prompt-inspection-summary">
        {plan.summaryRows.map((row) => (
          <div data-action-prompt-inspection-summary={row.key} key={row.key}>
            <dt>{row.label}</dt>
            <dd>{row.value}</dd>
          </div>
        ))}
      </dl>
      <div className="action-prompt-inspection-groups">
        {plan.groups.map((group) => (
          <section data-action-prompt-inspection-group={group.key} key={group.key}>
            <strong>{group.title}</strong>
            {group.rows.length === 0 ? (
              <span>{group.emptyLabel ?? "当前没有公开记录。"}</span>
            ) : (
              <ol>
                {group.rows.slice(0, 4).map((row) => (
                  <li data-action-prompt-inspection-row={`${group.key}:${row.key}`} key={row.key}>
                    <small>{row.label}</small>
                    <strong>{row.value}</strong>
                  </li>
                ))}
              </ol>
            )}
          </section>
        ))}
      </div>
    </div>
  );
}

function GenericPromptDetails({ plan }: { plan: ActionPanelGenericPromptPlan }) {
  return (
    <div className="generic-prompt-details">
      <div className="generic-prompt-heading">
        <strong>服务端选项</strong>
        <StatusPill tone="warn">{plan.statusLabel}</StatusPill>
      </div>
      <p className="generic-prompt-note">{plan.note}</p>
      {plan.emptyCandidateLabel && <span className="empty-hint">{plan.emptyCandidateLabel}</span>}
      {plan.candidateRows.map((candidate) => (
        <div className="generic-prompt-option" key={candidate.key}>
          <span>{candidate.label}</span>
          <small>{candidate.reason}</small>
          {candidate.previews.map((preview) => <span key={preview.key}>{preview.text}</span>)}
        </div>
      ))}
      {plan.metadataRows.length > 0 && (
        <div className="generic-prompt-metadata">
          <strong>窗口数据</strong>
          {plan.metadataRows.map((row) => (
            <span key={row.key}>{row.label}：{row.value}</span>
          ))}
        </div>
      )}
      {plan.contract && <PromptContractSummary contract={plan.contract} />}
    </div>
  );
}

function PromptContractSummary({ contract }: { contract: NonNullable<ActionPanelGenericPromptPlan["contract"]> }) {
  return (
    <div className="generic-prompt-contract" aria-label="服务端提示契约">
      <div className="generic-prompt-contract-heading">
        <strong>提示契约</strong>
        <span>{contract.heading}</span>
      </div>
      {contract.lines.map((line) => (
        <span key={line.key}>
          <b>{line.label}</b>
          <small>{line.value}</small>
        </span>
      ))}
    </div>
  );
}

function MulliganCandidate({
  candidate,
  onCommand,
  prompt,
  submitGate
}: {
  candidate: ActionPromptCandidateDto;
  onCommand: CommandSubmitHandler;
  prompt?: ActionPromptDto;
  submitGate: ActionPanelSubmitGate;
}) {
  const choices = useMemo(() => candidate.sources ?? [], [candidate.sources]);
  const maxSelectionCount = numberMetadata(candidate.metadata, "maxSelectionCount");
  const sourceKey = choices.map((choice) => choice.id).join("|");
  const [selectedObjectIds, setSelectedObjectIds] = useState<string[]>([]);

  useEffect(() => {
    setSelectedObjectIds((current) => {
      const allowed = new Set(choices.map((choice) => choice.id));
      const kept = current.filter((objectId) => allowed.has(objectId));
      return maxSelectionCount == null ? [] : kept.slice(0, maxSelectionCount);
    });
  }, [maxSelectionCount, sourceKey, choices]);

  const hasServerLimit = maxSelectionCount != null;
  const canSubmit = submitGate.canSubmit && hasServerLimit && selectedObjectIds.length <= maxSelectionCount;
  const buttonTitle = hasServerLimit ? submitGate.title ?? promptReasonTitle(candidate.reason) : "等待服务端提供选择上限";

  return (
    <div className="mulligan-selector">
      <div className="mulligan-summary">
        <strong>{promptActionLabel(candidate)}</strong>
        <span>{hasServerLimit ? `已选 ${selectedObjectIds.length} / ${maxSelectionCount}` : "等待服务端选择上限"}</span>
      </div>
      <div className="mulligan-choice-list">
        {choices.length === 0 && <span className="empty-hint">服务端未提供可调度手牌候选。</span>}
        {choices.map((choice) => (
          <MulliganChoiceButton
            choice={choice}
            disabled={!submitGate.canSubmit || !hasServerLimit}
            key={choice.id}
            maxSelectionCount={maxSelectionCount ?? 0}
            selected={selectedObjectIds.includes(choice.id)}
            selectedCount={selectedObjectIds.length}
            toggle={() => {
              setSelectedObjectIds((current) => current.includes(choice.id)
                ? current.filter((objectId) => objectId !== choice.id)
                : current.length < (maxSelectionCount ?? 0)
                  ? [...current, choice.id]
                  : current);
            }}
          />
        ))}
      </div>
      <Button
        disabled={!canSubmit}
        icon={<Check size={16} />}
        onClick={() => onCommand(withPromptStamp({ cmdType: "MULLIGAN", handObjectIds: selectedObjectIds }, prompt))}
        title={buttonTitle}
        variant={candidate.enabled ? "primary" : "ghost"}
      >
        确认起手调整
      </Button>
    </div>
  );
}

function MulliganChoiceButton({
  choice,
  disabled,
  maxSelectionCount,
  selected,
  selectedCount,
  toggle
}: {
  choice: ActionPromptChoiceDto;
  disabled: boolean;
  maxSelectionCount: number;
  selected: boolean;
  selectedCount: number;
  toggle: () => void;
}) {
  const lockedByLimit = !selected && selectedCount >= maxSelectionCount;
  return (
    <button
      className={`mulligan-choice ${selected ? "is-selected" : ""}`}
      disabled={disabled || lockedByLimit}
      onClick={toggle}
      title={promptReasonLabel(choice.reason, "服务端起手候选")}
      type="button"
    >
      <span>{choice.label}</span>
      <small>{selected ? "将调度" : lockedByLimit ? "已达上限" : "保留"}</small>
    </button>
  );
}

function HandChoiceCandidate({
  canAct,
  candidate,
  onCommand,
  prompt,
  readOnly = false,
  submitGate
}: {
  canAct: boolean;
  candidate?: ActionPromptCandidateDto;
  onCommand: CommandSubmitHandler;
  prompt?: ActionPromptDto;
  readOnly?: boolean;
  submitGate: ActionPanelSubmitGate;
}) {
  const model = useMemo(() => buildHandChoiceModel(candidate, prompt), [candidate, prompt]);
  const [selectedObjectIds, setSelectedObjectIds] = useState<string[]>([]);

  useEffect(() => {
    setSelectedObjectIds((current) => {
      const allowed = new Set(model.handChoices.map((choice) => choice.objectId));
      const kept = current.filter((objectId) => allowed.has(objectId));
      return model.maxCount == null ? kept : kept.slice(0, model.maxCount);
    });
  }, [model.resetKey, model.handChoices, model.maxCount]);

  const hasSelectionBounds = model.requiredCount != null && model.maxCount != null;
  const selectionCountValid = hasSelectionBounds
    && selectedObjectIds.length >= model.requiredCount!
    && selectedObjectIds.length <= model.maxCount!;
  const canSubmit = !readOnly
    && canAct
    && submitGate.canSubmit
    && Boolean(candidate?.enabled)
    && model.choiceId.length > 0
    && model.choiceWindow.length > 0
    && model.handChoices.length > 0
    && selectionCountValid;

  return (
    <div className="hand-choice-panel">
      <div className="hand-choice-heading">
        <strong>{candidate ? promptActionLabel(candidate) : "选择手牌"}</strong>
        <StatusPill tone={canSubmit ? "warn" : "neutral"}>{canSubmit ? "待服务端校验" : "等待选择"}</StatusPill>
      </div>
      <div className="hand-choice-summary">
        <span>窗口：{model.choiceWindow || "服务端未提供"}</span>
        <span>选择玩家：{model.choosingPlayerId ?? "服务端未提供"}</span>
        <span>数量：{hasSelectionBounds ? `${model.requiredCount} / ${model.maxCount}` : "服务端未提供"}</span>
        <span>已选：{selectedObjectIds.length}</span>
        <span>效果：{model.effectKind ?? "服务端未提供"}</span>
        <span>原因：{model.reason ?? "服务端未提供"}</span>
      </div>
      <p className="hand-choice-note">
        仅展示服务端发给当前玩家的手牌候选；选择结果和后续弃牌、抽牌或效果结算都由服务端处理。
      </p>
      <div className="hand-choice-list">
        {model.handChoices.length === 0 && (
          <span className="empty-hint">等待服务端选择窗口；当前视角没有可展示的手牌候选。</span>
        )}
        {model.handChoices.map((choice) => {
          const selected = selectedObjectIds.includes(choice.objectId);
          const lockedByLimit = !selected && model.maxCount != null && selectedObjectIds.length >= model.maxCount;
          return (
            <button
              className={`hand-choice-row ${selected ? "is-selected" : ""}`}
              disabled={readOnly || !submitGate.canSubmit || lockedByLimit}
              key={choice.objectId}
              onClick={() => {
                setSelectedObjectIds((current) => current.includes(choice.objectId)
                  ? current.filter((objectId) => objectId !== choice.objectId)
                  : [...current, choice.objectId]);
              }}
              title={choice.reason ?? "服务端手牌候选"}
              type="button"
            >
              <span>
                <strong>{choice.label}</strong>
                {choice.reason && <small>{choice.reason}</small>}
              </span>
              <small>{selected ? "已选择" : lockedByLimit ? "已达上限" : "可选择"}</small>
            </button>
          );
        })}
      </div>
      <Button
        disabled={!canSubmit}
        icon={<Check size={16} />}
        onClick={() => onCommand(withPromptStamp({
          cmdType: "CHOOSE_HAND_CARDS",
          choiceId: model.choiceId,
          choiceWindow: model.choiceWindow,
          chosenObjectIds: selectedObjectIds
        }, prompt))}
        title={submitGate.title ?? promptReasonTitle(candidate?.reason)}
        variant={canSubmit ? "primary" : "ghost"}
      >
        提交手牌选择
      </Button>
    </div>
  );
}

function DamageAssignmentCandidate({
  candidate,
  onCommand,
  prompt,
  snapshot,
  submitGate
}: {
  candidate: ActionPromptCandidateDto;
  onCommand: CommandSubmitHandler;
  prompt?: ActionPromptDto;
  snapshot?: SnapshotDto;
  submitGate: ActionPanelSubmitGate;
}) {
  const model = useMemo(() => buildDamageAssignmentModel(candidate, prompt, snapshot), [candidate, prompt, snapshot]);
  const [damageByKey, setDamageByKey] = useState<Record<string, number>>({});

  useEffect(() => {
    setDamageByKey((current) => {
      const allowed = new Set(model.choices.map((choice) => choice.key));
      return Object.fromEntries(
        Object.entries(current)
          .filter(([key]) => allowed.has(key))
          .map(([key, value]) => [key, clampDamageInput(value)])
      );
    });
  }, [model.resetKey, model.choices]);

  const assignments: CombatDamageAssignmentDto[] = model.choices
    .map((choice) => ({
      sourceObjectId: choice.sourceObjectId,
      targetObjectId: choice.targetObjectId,
      damage: clampDamageInput(damageByKey[choice.key] ?? 0)
    }))
    .filter((assignment) => assignment.damage > 0);
  const assignedDamage = assignments.reduce((total, assignment) => total + assignment.damage, 0);
  const canSubmit = submitGate.canSubmit
    && model.battleId.length > 0
    && model.battlefieldId.length > 0
    && assignments.length > 0;

  return (
    <div className="damage-assignment-panel">
      <div className="damage-assignment-heading">
        <strong>{promptActionLabel(candidate)}</strong>
        <StatusPill tone={canSubmit ? "warn" : "neutral"}>{assignments.length > 0 ? "待服务端校验" : "等待分配"}</StatusPill>
      </div>
      <div className="damage-assignment-summary">
        <span>战斗：{model.battleId || "服务端未提供"}</span>
        <span>战场：{model.battlefieldId || "服务端未提供"}</span>
        <span>伤害池：{model.damagePoolLabel ?? "服务端未提供"}</span>
        <span>已填写：{assignedDamage}</span>
        <span>合法目标：{model.choices.length} 项</span>
      </div>
      <p className="damage-assignment-note">
        仅按服务端候选提交伤害分配；总量、致命阈值和最终结算都由服务端校验。
      </p>
      <div className="damage-assignment-list">
        {model.choices.length === 0 && <span className="empty-hint">等待服务端提供伤害分配候选。</span>}
        {model.choices.map((choice) => (
          <label className="damage-assignment-row" key={choice.key}>
            <span>
              <strong>{choice.sourceLabel}</strong>
              <small>→ {choice.targetLabel}</small>
              <small>
                已有伤害 {choice.existingDamage ?? "未提供"} · 致命阈值 {choice.lethalThreshold ?? "未提供"}
                {choice.sourceDamagePool == null ? "" : ` · 来源伤害池 ${choice.sourceDamagePool}`}
              </small>
            </span>
            <input
              aria-label={`${choice.sourceLabel} 对 ${choice.targetLabel} 分配伤害`}
              disabled={!submitGate.canSubmit}
              inputMode="numeric"
              min={0}
              onChange={(event) => {
                const value = Number.parseInt(event.currentTarget.value, 10);
                setDamageByKey((current) => ({ ...current, [choice.key]: clampDamageInput(value) }));
              }}
              step={1}
              type="number"
              value={damageByKey[choice.key] ?? 0}
            />
          </label>
        ))}
      </div>
      <Button
        disabled={!canSubmit}
        icon={<Send size={16} />}
        onClick={() => onCommand(withPromptStamp({
          cmdType: "ASSIGN_COMBAT_DAMAGE",
          battleId: model.battleId,
          battlefieldId: model.battlefieldId,
          assignments
        }, prompt))}
        title={submitGate.title ?? promptReasonTitle(candidate.reason)}
        variant={candidate.enabled ? "primary" : "ghost"}
      >
        提交伤害分配
      </Button>
    </div>
  );
}

function OrderTriggersCandidate({
  canAct,
  candidate,
  onCommand,
  prompt,
  readOnly = false,
  submitGate
}: {
  canAct: boolean;
  candidate?: ActionPromptCandidateDto;
  onCommand: CommandSubmitHandler;
  prompt?: ActionPromptDto;
  readOnly?: boolean;
  submitGate: ActionPanelSubmitGate;
}) {
  const model = useMemo(() => buildOrderTriggersModel(candidate, prompt), [candidate, prompt]);
  const [orderedTriggerIds, setOrderedTriggerIds] = useState<string[]>([]);

  useEffect(() => {
    setOrderedTriggerIds(model.triggers.map((trigger) => trigger.triggerId));
  }, [model.resetKey, model.triggers]);

  const triggerById = new Map(model.triggers.map((trigger) => [trigger.triggerId, trigger]));
  const orderedTriggers = orderedTriggerIds
    .map((triggerId) => triggerById.get(triggerId))
    .filter((trigger): trigger is TriggerOrderItem => trigger != null);
  const submitIds = orderedTriggers.map((trigger) => trigger.triggerId);
  const canSubmit = !readOnly
    && canAct
    && submitGate.canSubmit
    && Boolean(candidate?.enabled)
    && submitIds.length === model.triggers.length
    && submitIds.length > 0;

  return (
    <div className="trigger-order-panel">
      <div className="trigger-order-heading">
        <strong>{candidate ? promptActionLabel(candidate) : "排列触发"}</strong>
        <StatusPill tone={canSubmit ? "warn" : "neutral"}>{canSubmit ? "待服务端校验" : "只读等待"}</StatusPill>
      </div>
      <div className="trigger-order-summary">
        <span>触发数量：{model.triggers.length} 项</span>
        <span>来源事件：{model.triggeredByEventKind ?? "服务端未提供"}</span>
        <span>排序约束：{model.constraints.length > 0 ? `${model.constraints.length} 项` : "服务端未提供"}</span>
      </div>
      <p className="trigger-order-note">
        仅提交服务端触发候选的顺序；排序合法性和触发结算都由服务端处理。
      </p>
      {model.constraints.length > 0 && (
        <div className="trigger-order-constraints">
          {model.constraints.slice(0, 4).map((constraint, index) => (
            <span key={`${constraint}-${index}`}>约束：{constraint}</span>
          ))}
          {model.constraints.length > 4 && <span>另有 {model.constraints.length - 4} 项服务端约束。</span>}
        </div>
      )}
      <div className="trigger-order-list">
        {orderedTriggers.length === 0 && <span className="empty-hint">等待服务端提供可排序触发。</span>}
        {orderedTriggers.map((trigger, index) => (
          <article className="trigger-order-row" key={trigger.triggerId}>
            <span className="trigger-order-index">{index + 1}</span>
            <div className="trigger-order-copy">
              <strong>{trigger.label}</strong>
              <small>ID：{trigger.triggerId}</small>
              {trigger.summary && <small>说明：{trigger.summary}</small>}
              <small>来源：{trigger.source ?? "服务端未提供"} · 控制者：{trigger.controller ?? "服务端未提供"}</small>
              {trigger.constraint && <small>约束：{trigger.constraint}</small>}
            </div>
            <div className="trigger-order-controls">
              <button
                aria-label={`${trigger.label} 上移`}
                className="trigger-order-move"
                disabled={readOnly || !submitGate.canSubmit || index === 0}
                onClick={() => setOrderedTriggerIds((current) => moveTriggerId(current, trigger.triggerId, -1))}
                type="button"
              >
                <ArrowUp size={14} />
              </button>
              <button
                aria-label={`${trigger.label} 下移`}
                className="trigger-order-move"
                disabled={readOnly || !submitGate.canSubmit || index === orderedTriggers.length - 1}
                onClick={() => setOrderedTriggerIds((current) => moveTriggerId(current, trigger.triggerId, 1))}
                type="button"
              >
                <ArrowDown size={14} />
              </button>
            </div>
          </article>
        ))}
      </div>
      <Button
        disabled={!canSubmit}
        icon={<ListOrdered size={16} />}
        onClick={() => onCommand(withPromptStamp({
          cmdType: "ORDER_TRIGGERS",
          orderedTriggerIds: submitIds,
          triggerIds: submitIds
        }, prompt))}
        title={submitGate.title ?? promptReasonTitle(candidate?.reason)}
        variant={canSubmit ? "primary" : "ghost"}
      >
        提交触发顺序
      </Button>
    </div>
  );
}

function PayCostCandidate({
  canAct,
  candidate,
  onCommand,
  prompt,
  submitGate
}: {
  canAct: boolean;
  candidate?: ActionPromptCandidateDto;
  onCommand: CommandSubmitHandler;
  prompt?: ActionPromptDto;
  submitGate: ActionPanelSubmitGate;
}) {
  const model = useMemo(() => buildPayCostModel(candidate, prompt), [candidate, prompt]);
  const lockedSpendChoiceIds = useMemo(
    () => model.choices.filter((choice) => choice.source === "spend").map((choice) => choice.id),
    [model.choices]
  );
  const [selectedChoiceIds, setSelectedChoiceIds] = useState<string[]>([]);

  useEffect(() => {
    const allowed = new Set(model.choices.map((choice) => choice.id));
    const defaultChoiceIds = uniqueStringList([
      ...lockedSpendChoiceIds,
      ...model.paymentChoiceIds
    ]).filter((choiceId) => allowed.size === 0 || allowed.has(choiceId));
    setSelectedChoiceIds(defaultChoiceIds);
  }, [lockedSpendChoiceIds, model.paymentChoiceIds, model.resetKey, model.choices]);

  const selectedChoiceIdSet = new Set(selectedChoiceIds);
  const commandChoiceIds = model.choices.length > 0
    ? model.choices.filter((choice) => selectedChoiceIdSet.has(choice.id)).map((choice) => choice.id)
    : model.paymentChoiceIds;
  const resourceCount = model.choices.filter((choice) => choice.source === "resource").length;
  const spendCount = model.choices.filter((choice) => choice.source === "spend").length;
  const canSubmit = canAct
    && submitGate.canSubmit
    && Boolean(candidate?.enabled)
    && model.paymentId.length > 0
    && model.paymentWindow.length > 0
    && (model.choices.length === 0 || commandChoiceIds.length > 0);
  const panelState = canSubmit
    ? "ready"
    : model.paymentId.length === 0 || model.paymentWindow.length === 0
      ? "missing-contract"
      : "waiting-selection";

  const toggleChoice = (choice: PaymentChoiceItem) => {
    if (choice.source === "spend") {
      return;
    }

    setSelectedChoiceIds((current) => current.includes(choice.id)
      ? current.filter((choiceId) => choiceId !== choice.id && !lockedSpendChoiceIds.includes(choiceId))
      : uniqueStringList([...current, choice.id]));
  };

  return (
    <div
      className="pay-cost-panel"
      data-pay-cost-choice-count={model.choices.length}
      data-pay-cost-resource-count={resourceCount}
      data-pay-cost-selected-count={commandChoiceIds.length}
      data-pay-cost-spend-count={spendCount}
      data-pay-cost-state={panelState}
    >
      <div className="pay-cost-heading">
        <strong>{candidate ? promptActionLabel(candidate) : "支付费用"}</strong>
        <StatusPill tone={canSubmit ? "warn" : "neutral"}>{canSubmit ? "待服务端校验" : "等待支付选择"}</StatusPill>
      </div>
      <div className="pay-cost-summary">
        <span>窗口：{model.paymentWindow || "服务端未提供"}</span>
        <span>支付 ID：{model.paymentId || "服务端未提供"}</span>
        <span>费用：{model.costLabel ?? "服务端未提供"}</span>
        <span>已提交项：{commandChoiceIds.length}</span>
        <span>支付项：{spendCount}</span>
        <span>资源动作：{resourceCount}</span>
      </div>
      <p className="pay-cost-note">
        支付项和资源动作来自服务端候选；前端只提交所选 ID，费用满足性、非法资源和不必要资源都由服务端校验。
      </p>
      <div className="pay-cost-choice-list">
        {model.choices.length === 0 && (
          <span className="empty-hint">
            服务端未公开可选支付项；将按服务端给定的支付组合提交。
          </span>
        )}
        {model.choices.map((choice) => {
          const selected = selectedChoiceIdSet.has(choice.id);
          return (
            <button
              className={`pay-cost-choice-row ${selected ? "is-selected" : ""}`}
              data-pay-cost-choice={choice.id}
              data-pay-cost-choice-selected={selected ? "true" : "false"}
              data-pay-cost-choice-source={choice.source}
              disabled={!canAct || !submitGate.canSubmit || choice.source === "spend"}
              key={`${choice.source}:${choice.id}`}
              onClick={() => toggleChoice(choice)}
              title={choice.reason ?? (choice.source === "spend" ? "服务端支付项" : "服务端资源动作")}
              type="button"
            >
              <span>
                <strong>{choice.label}</strong>
                {choice.reason && <small>{choice.reason}</small>}
              </span>
              <small>{choice.source === "spend" ? "支付项" : selected ? "已选择资源" : "可选资源"}</small>
            </button>
          );
        })}
      </div>
      <Button
        disabled={!canSubmit}
        icon={<Check size={16} />}
        onClick={() => onCommand(withPromptStamp({
          cmdType: "PAY_COST",
          paymentChoiceIds: commandChoiceIds,
          paymentId: model.paymentId,
          paymentWindow: model.paymentWindow
        }, prompt))}
        title={submitGate.title ?? promptReasonTitle(candidate?.reason)}
        variant={canSubmit ? "primary" : "ghost"}
      >
        提交支付
      </Button>
    </div>
  );
}

function moveTriggerId(triggerIds: string[], triggerId: string, delta: number): string[] {
  const from = triggerIds.indexOf(triggerId);
  const to = from + delta;
  if (from < 0 || to < 0 || to >= triggerIds.length) {
    return triggerIds;
  }

  const next = [...triggerIds];
  const [moved] = next.splice(from, 1);
  next.splice(to, 0, moved);
  return next;
}

function uniqueStringList(values: string[]): string[] {
  return [...new Set(values.filter((value) => value.trim().length > 0))];
}

function CandidateButton({
  candidate,
  disabledByConnection,
  onCommand,
  onReady,
  onSubmitStarterDeck,
  prompt,
  snapshot,
  submitGate,
  submissionGate
}: {
  candidate: ActionPromptCandidateDto;
  disabledByConnection: boolean;
  onCommand: CommandSubmitHandler;
  onReady: () => void;
  onSubmitStarterDeck: () => void;
  prompt?: ActionPromptDto;
  snapshot?: SnapshotDto;
  submitGate: ActionPanelSubmitGate;
  submissionGate: ServerSubmissionGatePlan;
}) {
  const [confirmingSurrender, setConfirmingSurrender] = useState(false);
  const disabledByActionGate = submitGate.state === "readonly" || submitGate.state === "window-blocked";
  const plan = buildActionPanelCandidateCommandPlan({ candidate, disabledByActionGate, disabledByConnection });
  const buttonTitle = submitGate.title ?? (disabledByConnection ? "当前行动入口不可提交" : promptReasonTitle(candidate.reason));

  useEffect(() => {
    setConfirmingSurrender(false);
  }, [candidate.action, candidate.enabled, candidate.label, disabledByConnection]);

  if (candidate.action === "SURRENDER" && plan.command) {
    if (confirmingSurrender) {
      return (
        <CandidateCommandPlanShell plan={plan}>
          <div className="surrender-confirm">
            <span>对手将获得本局胜利。</span>
            <div className="surrender-confirm-actions">
              <Button
                disabled={plan.disabled}
                icon={<Flag size={16} />}
                onClick={() => onCommand(withPromptStamp(plan.command!, prompt), candidateCommandUiSource(candidate, plan))}
                title={buttonTitle}
                variant="danger"
              >
                确认投降
              </Button>
              <Button icon={<X size={16} />} onClick={() => setConfirmingSurrender(false)} variant="ghost">
                取消
              </Button>
            </div>
          </div>
        </CandidateCommandPlanShell>
      );
    }

    return (
      <CandidateCommandPlanShell plan={plan}>
        <Button
          disabled={plan.disabled}
          icon={<Flag size={16} />}
          onClick={() => setConfirmingSurrender(true)}
          title={buttonTitle}
          variant="danger"
        >
          {promptActionLabel(candidate)}
        </Button>
      </CandidateCommandPlanShell>
    );
  }

  if (plan.needsComposer) {
    return (
      <CandidateCommandPlanShell plan={plan}>
        <CandidateComposer
          actionGateReason={disabledByActionGate ? submitGate.reason : undefined}
          actionGateStateLabel={disabledByActionGate ? submitGate.stateLabel : undefined}
          candidate={candidate}
          disabledByActionGate={disabledByActionGate}
          disabledByConnection={disabledByConnection}
          onCommand={onCommand}
          prompt={prompt}
          snapshot={snapshot}
          submissionGate={submissionGate}
        />
      </CandidateCommandPlanShell>
    );
  }

  return (
    <CandidateCommandPlanShell plan={plan}>
      <Button
        disabled={plan.disabled}
        icon={candidateIcon(plan.icon)}
        onClick={() => {
          if (plan.directAction) {
            runDirectAction(plan.directAction, onReady, onSubmitStarterDeck);
          } else if (plan.command) {
            onCommand(withPromptStamp(plan.command, prompt), candidateCommandUiSource(candidate, plan));
          }
        }}
        title={buttonTitle}
        variant={plan.variant}
      >
        {promptActionLabel(candidate)}
        {plan.labelSuffix}
      </Button>
    </CandidateCommandPlanShell>
  );
}

function candidateCommandUiSource(
  candidate: ActionPromptCandidateDto,
  plan: ActionPanelCandidateCommandPlan
): Partial<CommandSubmissionUiSource> {
  return {
    candidateAction: candidate.action,
    candidateLabel: promptActionLabel(candidate),
    commandSource: plan.commandSource,
    commandSourceDetail: plan.commandSourceDetail,
    commandSourceLabel: plan.commandSourceLabel,
    label: promptActionLabel(candidate)
  };
}

function CandidateCommandPlanShell({ children, plan }: { children: ReactNode; plan: ActionPanelCandidateCommandPlan }) {
  return (
    <div
      className="action-command-plan"
      data-action-command-disabled={plan.disabled ? "true" : "false"}
      data-action-command-source={plan.commandSource}
    >
      {children}
      <small className="action-command-plan-source">
        <strong>{plan.commandSourceLabel}</strong>
        <span>{plan.commandSourceDetail}</span>
      </small>
    </div>
  );
}

function runDirectAction(kind: ActionPanelDirectActionKind, onReady: () => void, onSubmitStarterDeck: () => void) {
  if (kind === "submitDeck") {
    onSubmitStarterDeck();
    return;
  }

  onReady();
}

function candidateIcon(icon: ActionPanelCandidateButtonIcon) {
  switch (icon) {
    case "check":
      return <Check size={16} />;
    case "flag":
      return <Flag size={16} />;
    case "hourglass":
      return <Hourglass size={16} />;
    case "play":
      return <Play size={16} />;
    case "send":
      return <Send size={16} />;
  }
}

function numberMetadata(metadata: Record<string, unknown> | null | undefined, key: string): number | undefined {
  const value = metadata?.[key];
  return typeof value === "number" && Number.isFinite(value) ? value : undefined;
}

export function candidateListLabel(prompt?: ActionPromptDto): string {
  const candidates = prompt?.candidates ?? [];
  if (candidates.length === 0) {
    return "无服务端候选";
  }

  const enabledCandidates = candidates.filter((candidate) => candidate.enabled);
  const blockedCount = candidates.length - enabledCandidates.length;
  const labels = enabledCandidates.map(promptActionLabel);
  const prefix = labels.length > 0 ? labels.join("、") : "无可提交行动";
  return blockedCount > 0 ? `${prefix}；${blockedCount} 个阻断候选` : prefix;
}

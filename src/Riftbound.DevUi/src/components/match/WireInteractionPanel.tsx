import type { InspectedCard } from "../cards/CardFace";
import { Maximize2, Play } from "lucide-react";
import type { ActionPromptDto, GameCommand, SnapshotDto } from "../../types/protocol";
import type { TableObjectContext } from "../../utils/tableObjectContext";
import { promptStampedCommand } from "../../utils/actionPromptCandidates";
import type { CandidateSelectionDraft } from "../../utils/candidateSelectionDraft";
import type { ServerSubmissionGatePlan } from "../../utils/serverSubmissionGatePlan";
import type { FocusedActionModel } from "../../utils/focusedActionModel";
import type { FocusedInteractionGrammarPlan } from "../../utils/focusedInteractionGrammarPlan";
import {
  buildWireFocusedInteractionPlan,
  type WireFocusedInteractionPlan,
  type WireFocusedLegalActionRowPlan,
  type WireFocusedLegalActionState
} from "../../utils/wireFocusedInteractionPlan";
import type { WirePromptCandidateListPlan, WirePromptCandidateRowPlan } from "../../utils/wirePromptCandidatePlan";
import { CardFace } from "../cards/CardFace";
import { CandidateComposer } from "./CandidateComposer";
import { Button } from "../ui/Button";
import { StatusPill } from "../ui/StatusPill";
import { WireObjectRefChips, type WireObjectIndex } from "./WireObjectRefChips";
import { WireObjectContextSummary } from "./WireObjectContextSummary";
import { WireEmpty } from "./wireCardFlow";

export function WireInteractionPanel({
  disabledByConnection,
  inspectedCard,
  onCommand,
  onClearInspectedCard,
  onOpenDetail,
  onInspectObject,
  objectContext,
  playerId,
  prompt,
  selectionDraft,
  snapshot,
  submissionGate
}: {
  disabledByConnection: boolean;
  inspectedCard?: InspectedCard;
  onCommand?: (command: GameCommand) => void;
  onClearInspectedCard: () => void;
  onInspectObject?: (objectId: string) => void;
  onOpenDetail: (card: InspectedCard) => void;
  objectContext?: TableObjectContext;
  playerId: string;
  prompt?: ActionPromptDto;
  selectionDraft?: CandidateSelectionDraft;
  snapshot?: SnapshotDto;
  submissionGate?: ServerSubmissionGatePlan;
}) {
  const selectedObjectId = inspectedCard?.objectId ?? inspectedCard?.object?.objectId;
  const plan = buildWireFocusedInteractionPlan({
    canSubmitCommands: Boolean(onCommand),
    disabledByConnection,
    prompt,
    selectionDraft,
    snapshot,
    sourceControllerId: inspectedCard?.object?.controllerId,
    sourceObjectId: selectedObjectId
  });

  return (
    <section className="wire-interaction-panel">
      <header className="wire-interaction-heading">
        <h2>焦点 / 候选 / 规则队列</h2>
        <StatusPill tone={prompt?.actionable && prompt.playerId === playerId ? "good" : "neutral"}>
          {prompt?.actionable && prompt.playerId === playerId ? "当前可操作" : "只读观察"}
        </StatusPill>
      </header>

      <div className="wire-focus-card">
        {inspectedCard ? (
          <>
            <CardFace object={inspectedCard.object} objectId={inspectedCard.objectId} selected spec={inspectedCard.spec} />
              <div className="wire-focus-copy">
                <strong>{inspectedCard.spec?.cardName ?? inspectedCard.object?.cardNo ?? inspectedCard.objectId ?? "卡牌"}</strong>
                <span>对象：{plan.sourceObject.objectIdLabel}</span>
                <span>控制：{plan.sourceObject.controllerLabel}</span>
                <span>服务端关联：{plan.sourceObject.serverCandidateLabel}</span>
                <WireObjectContextSummary context={objectContext} contract={prompt?.contract} focusModel={plan.focusModel} />
                <div className="wire-focus-actions">
                  <Button icon={<Maximize2 size={16} />} onClick={() => onOpenDetail(inspectedCard)} variant="secondary">查看详情</Button>
                  <Button onClick={onClearInspectedCard} variant="ghost">清除焦点</Button>
              </div>
            </div>
          </>
        ) : (
          <WireEmpty label="点击卡牌查看服务端候选关联" />
        )}
      </div>

      <FocusedReadinessStrip plan={plan} />
      {inspectedCard && <FocusedLegalActionMatrix plan={plan} />}

      {inspectedCard && (
        <div className="wire-selected-candidates">
          <strong>焦点候选</strong>
          {plan.relatedCandidateRows.length === 0 && <span className="empty-hint">该卡当前未出现在服务端候选中。</span>}
          {plan.relatedCandidateRows.slice(0, 5).map((row) => (
            <CandidateSummaryRow
              key={row.key}
              objects={plan.objectIndex}
              onInspectObject={onInspectObject}
              row={row}
              selectedObjectId={selectedObjectId}
            />
          ))}
        </div>
      )}

      <FocusedActionList
        disabledByConnection={disabledByConnection}
        inspectedCard={inspectedCard}
        onCommand={onCommand}
        plan={plan}
        prompt={prompt}
        snapshot={snapshot}
        submissionGate={submissionGate}
      />

      <PromptCandidateList
        objects={plan.objectIndex}
        onInspectObject={onInspectObject}
        plan={plan.promptCandidateList}
        selectedObjectId={selectedObjectId}
      />
    </section>
  );
}

function FocusedLegalActionMatrix({ plan }: { plan: WireFocusedInteractionPlan }) {
  return (
    <section
      aria-label="焦点合法操作矩阵"
      className="wire-focused-legal-actions"
      data-wire-focused-legal-action-count={plan.legalActionRows.length}
    >
      <div className="wire-focused-legal-actions-heading">
        <strong>合法操作矩阵</strong>
        <StatusPill tone={plan.legalActionRows.some((row) => row.state === "ready") ? "good" : "neutral"}>
          {plan.legalActionRows.length > 0 ? `${plan.legalActionRows.length} 项` : "无关联"}
        </StatusPill>
      </div>
      {plan.legalActionRows.length === 0 ? (
        <span className="empty-hint">该对象当前没有出现在服务端行动候选中。</span>
      ) : (
        <ol className="wire-focused-legal-action-list">
          {plan.legalActionRows.slice(0, 6).map((row) => (
            <FocusedLegalActionRow key={row.key} row={row} />
          ))}
        </ol>
      )}
    </section>
  );
}

function FocusedLegalActionRow({ row }: { row: WireFocusedLegalActionRowPlan }) {
  return (
    <li
      className={`is-${row.state}`}
      data-wire-focused-legal-action={row.action}
      data-wire-focused-legal-action-state={row.state}
    >
      <div>
        <strong>{row.label}</strong>
        <StatusPill tone={legalActionTone(row.state)}>{row.stateLabel}</StatusPill>
      </div>
      <span>{row.nextStepLabel}</span>
      <small>角色：{row.roleLabels.length > 0 ? row.roleLabels.join(" / ") : "无"}</small>
      <small>命令：{row.commandType ?? "未公开"}</small>
      {row.missingRequiredLabels.length > 0 && <small>缺少：{row.missingRequiredLabels.join(" / ")}</small>}
      <small>{row.reason}</small>
    </li>
  );
}

function legalActionTone(state: WireFocusedLegalActionState): "good" | "neutral" | "warn" {
  switch (state) {
    case "ready":
      return "good";
    case "blocked":
    case "needs-selection":
      return "warn";
    case "informational":
      return "neutral";
  }
}

function FocusedReadinessStrip({ plan }: { plan: WireFocusedInteractionPlan }) {
  return (
    <section
      aria-label="焦点行动就绪状态"
      className="wire-focused-readiness"
      data-wire-focused-readiness-can-submit={plan.readiness.canSubmit ? "true" : "false"}
      data-wire-focused-readiness-command={plan.readiness.commandType ?? ""}
      data-wire-focused-readiness-enabled-count={plan.readiness.enabledCount}
      data-wire-focused-readiness-missing-required-count={plan.readiness.missingRequiredCount}
      data-wire-focused-readiness-state={plan.readiness.state}
    >
      <div className="wire-focused-readiness-heading">
        <strong>行动状态</strong>
        <StatusPill tone={plan.readiness.tone}>{plan.readiness.stateLabel}</StatusPill>
      </div>
      <div className="wire-focused-readiness-grid">
        <span>
          <small>候选</small>
          <strong>{plan.readiness.candidateLabel}</strong>
        </span>
        <span>
          <small>可提交</small>
          <strong>{plan.readiness.enabledCount}</strong>
        </span>
        <span>
          <small>缺少</small>
          <strong>{plan.readiness.missingRequiredCount}</strong>
        </span>
        <span>
          <small>命令</small>
          <strong>{plan.readiness.commandType ?? "无"}</strong>
        </span>
      </div>
      <span className="wire-focused-readiness-next">{plan.readiness.nextStepLabel}</span>
    </section>
  );
}

function FocusedActionList({
  disabledByConnection,
  inspectedCard,
  onCommand,
  plan,
  prompt,
  snapshot,
  submissionGate
}: {
  disabledByConnection: boolean;
  inspectedCard?: InspectedCard;
  onCommand?: (command: GameCommand) => void;
  plan: WireFocusedInteractionPlan;
  prompt?: ActionPromptDto;
  snapshot?: SnapshotDto;
  submissionGate?: ServerSubmissionGatePlan;
}) {
  if (!inspectedCard) {
    return null;
  }

  return (
    <div className="wire-focused-actions">
      <div className="wire-focused-actions-heading">
        <strong>焦点操作入口</strong>
        <StatusPill tone={plan.sourceCandidates.length > 0 ? "good" : "neutral"}>{plan.sourceCandidates.length > 0 ? `${plan.sourceCandidates.length} 项` : "无可提交"}</StatusPill>
      </div>
      <p>只使用服务端当前候选；连接恢复前不会提交命令。</p>
      <FocusedActionSummary focusModel={plan.focusModel} />
      <FocusedInteractionGrammar plan={plan.grammarPlan} />
      {plan.sourceCandidates.length === 0 && <span className="empty-hint">当前服务端没有给该对象可提交操作。</span>}
      {plan.draft && (
        <div className="wire-selection-draft" role="group" aria-label="已点选候选草稿">
          <strong>桌面点选</strong>
          <span>目标 {plan.draft.targetCount}</span>
          <span>位置 {plan.draft.destinationSelected ? "已选" : "未选"}</span>
          <span>费用 {plan.draft.optionalCostCount}</span>
        </div>
      )}
      {plan.sourceCandidatePaths.length > 0 && (
        <div className="wire-focused-path" role="group" aria-label="焦点候选路径">
          {plan.sourceCandidatePaths.map((path) => (
            <article key={path.key}>
              <strong>{path.label}</strong>
              <ol>
                {path.steps.map((step) => (
                  <li className={step.required ? "is-required" : ""} key={step.key}>
                    <span>{step.label}</span>
                    <small>{step.required ? "必需；" : ""}{step.sampleLabel}</small>
                  </li>
                ))}
              </ol>
            </article>
          ))}
        </div>
      )}
      {plan.actionEntries.slice(0, 4).map(({ actionPlan, candidate, candidateDraft, key, mode }) => {
        if (mode === "composer" && onCommand) {
          return (
            <CandidateComposer
              candidate={candidate}
              disabledByConnection={disabledByConnection}
              forcedSourceObjectId={plan.sourceObjectId}
              key={key}
              onCommand={onCommand}
              prompt={prompt}
              selectionDraft={candidateDraft}
              snapshot={snapshot}
              submissionGate={submissionGate}
            />
          );
        }

        return (
          <Button
            disabled={actionPlan.disabled}
            icon={<Play size={16} />}
            key={key}
            onClick={() => {
              if (actionPlan.command && onCommand) {
                onCommand(promptStampedCommand(actionPlan.command, prompt));
              }
            }}
            title={actionPlan.title}
            variant={actionPlan.variant}
          >
            {actionPlan.label}
            {actionPlan.labelSuffix}
          </Button>
        );
      })}
    </div>
  );
}

function FocusedInteractionGrammar({ plan }: { plan: FocusedInteractionGrammarPlan }) {
  return (
    <div
      aria-label="焦点交互语法"
      className="wire-focused-grammar"
      data-wire-focused-grammar-composer-state={plan.composerState}
      data-wire-focused-grammar-state={plan.state}
      role="group"
    >
      <div className="wire-focused-grammar-heading">
        <strong>交互语法</strong>
        <StatusPill tone={plan.state === "ready" ? "good" : "neutral"}>{plan.stateLabel}</StatusPill>
      </div>
      <div className="wire-focused-grammar-summary">
        <span>{plan.candidateLabel}</span>
        <small>下一步：{plan.nextStepLabel}</small>
        <small>命令：{plan.commandType ?? "未公开"} / 字段 {plan.commandFieldCount}</small>
        <small title={plan.composerReason}>组合：{plan.composerStateLabel}</small>
      </div>
      {plan.steps.length > 0 ? (
        <ol className="wire-focused-grammar-steps">
          {plan.steps.map((step) => (
            <li className={`is-${step.state}`} data-wire-grammar-role={step.role} key={step.key}>
              <span>{step.label}</span>
              <strong>{step.stateLabel}</strong>
              <small>
                {step.required ? "必需" : "可选"}
                {"；候选 "}{step.availableCount}
                {"；已选 "}{step.selectedCount}
              </small>
              {step.sampleLabels.length > 0 && <small>{step.sampleLabels.slice(0, 3).join(" / ")}</small>}
            </li>
          ))}
        </ol>
      ) : (
        <span className="empty-hint">点击服务端候选对象后显示命令语法。</span>
      )}
    </div>
  );
}

function FocusedActionSummary({ focusModel }: { focusModel: FocusedActionModel }) {
  return (
    <div
      aria-label="焦点行动摘要"
      className="wire-focused-action-summary"
      data-wire-focused-action-state={focusModel.submittedByServer ? "server-candidate" : "no-candidate"}
      role="group"
    >
      <div className="wire-focused-action-metrics">
        <span>
          <small>服务端状态</small>
          <strong>{focusModel.stateLabel}</strong>
        </span>
        <span>
          <small>可提交</small>
          <strong>{focusModel.enabledCount}</strong>
        </span>
        <span>
          <small>阻断</small>
          <strong>{focusModel.blockedCount}</strong>
        </span>
      </div>
      <span className="wire-focused-next-step" data-wire-focused-next-step>
        {focusModel.nextStepLabel}
      </span>
      {focusModel.blockingReasons.length > 0 && (
        <div className="wire-focused-blockers">
          {focusModel.blockingReasons.map((reason) => (
            <small key={reason}>阻断：{reason}</small>
          ))}
        </div>
      )}
      {focusModel.candidates.length > 0 && (
        <ol className="wire-focused-candidate-plan">
          {focusModel.candidates.slice(0, 4).map(({ candidate, key, nextStep, stateLabel }) => (
            <li className={candidate.enabled ? "is-enabled" : "is-disabled"} key={key}>
              <span>{candidate.label}</span>
              <small>{stateLabel}{nextStep ? `；下一步 ${nextStep.label}` : ""}</small>
            </li>
          ))}
        </ol>
      )}
    </div>
  );
}

function PromptCandidateList({
  objects,
  onInspectObject,
  plan,
  selectedObjectId
}: {
  objects: WireObjectIndex;
  onInspectObject?: (objectId: string) => void;
  plan: WirePromptCandidateListPlan;
  selectedObjectId?: string;
}) {
  return (
    <div className="wire-prompt-candidates">
      <div className="wire-prompt-contract">
        <strong>{plan.promptTitle}</strong>
        <span>类型：{plan.promptType}</span>
        <span>提示：{plan.message}</span>
        <span>{plan.versionLabel}</span>
      </div>
      {plan.emptyLabel && <span className="empty-hint">{plan.emptyLabel}</span>}
      {plan.enabledRows.map((row) => (
        <CandidateSummaryRow
          key={row.key}
          objects={objects}
          onInspectObject={onInspectObject}
          row={row}
          selectedObjectId={selectedObjectId}
        />
      ))}
      {plan.disabledRows.map((row) => (
        <CandidateSummaryRow
          key={row.key}
          objects={objects}
          onInspectObject={onInspectObject}
          row={row}
          selectedObjectId={selectedObjectId}
        />
      ))}
    </div>
  );
}

function CandidateSummaryRow({
  objects,
  onInspectObject,
  row,
  selectedObjectId
}: {
  objects: WireObjectIndex;
  onInspectObject?: (objectId: string) => void;
  row: WirePromptCandidateRowPlan;
  selectedObjectId?: string;
}) {
  return (
    <article className={`wire-candidate-row ${row.enabled ? "is-enabled" : "is-disabled"}`}>
      <div>
        <strong>{row.label}</strong>
        <StatusPill tone={row.enabled ? "good" : "neutral"}>{row.enabled ? "可提交" : "不可提交"}</StatusPill>
      </div>
      <span>{row.reason}</span>
      {row.choiceGroups.map((group) => (
        <small key={group.key}>{group.summary}</small>
      ))}
      <WireObjectRefChips
        className="wire-candidate-object-ref-list"
        objects={objects}
        onInspectObject={onInspectObject}
        refs={row.objectRefs}
        selectedObjectId={selectedObjectId}
        source="candidate"
      />
    </article>
  );
}

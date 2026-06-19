import type { InspectedCard } from "../cards/CardFace";
import { Maximize2, Play } from "lucide-react";
import type { ActionPromptDto, GameCommand, SnapshotDto } from "../../types/protocol";
import type { TableObjectContext } from "../../utils/tableObjectContext";
import { promptStampedCommand, sourceCandidatesForPrompt } from "../../utils/actionPromptCandidates";
import {
  buildPromptInteractionModel,
  promptChoiceSummaryObjectIds,
  type PromptCandidateSummary,
  type PromptInteractionModel
} from "../../utils/promptInteraction";
import { buildFocusedActionModel, type FocusedActionModel } from "../../utils/focusedActionModel";
import type { CandidateSelectionDraft } from "../../utils/candidateSelectionDraft";
import { candidateComposerKey } from "../../utils/candidateComposerModel";
import { buildFocusedInteractionGrammarPlan, type FocusedInteractionGrammarPlan } from "../../utils/focusedInteractionGrammarPlan";
import { buildSourceCandidateActionPlan } from "../../utils/sourceCandidateActionPlan";
import { buildWirePromptCandidateListPlan, type WirePromptCandidateRowPlan } from "../../utils/wirePromptCandidatePlan";
import { CardFace } from "../cards/CardFace";
import { CandidateComposer } from "./CandidateComposer";
import { Button } from "../ui/Button";
import { StatusPill } from "../ui/StatusPill";
import { buildCardObjectIndex } from "../../utils/snapshotObjectIndex";
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
  snapshot
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
}) {
  const model = buildPromptInteractionModel(prompt);
  const inspectedObjectId = inspectedCard?.objectId;
  const objectIndex = buildCardObjectIndex(snapshot);
  const selectedObjectId = inspectedCard?.objectId ?? inspectedCard?.object?.objectId;
  const objectSummary = inspectedObjectId ? model.objectById.get(inspectedObjectId) : undefined;
  const focusModel = buildFocusedActionModel({
    interactionModel: model,
    prompt,
    selectionDraft,
    sourceObjectId: selectedObjectId
  });
  const relatedCandidates = inspectedObjectId
    ? model.candidates.filter((candidate) => candidate.choices.some((choice) => promptChoiceSummaryObjectIds(choice).includes(inspectedObjectId)))
    : [];
  const relatedCandidatePlan = buildWirePromptCandidateListPlan({
    model: {
      ...model,
      candidates: relatedCandidates
    },
    objects: objectIndex
  });
  const relatedCandidateRows = [
    ...relatedCandidatePlan.enabledRows,
    ...relatedCandidatePlan.disabledRows
  ];

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
              <span>对象：{inspectedCard.objectId ?? "无对象 ID"}</span>
              <span>控制：{inspectedCard.object?.controllerId ?? "未知"}</span>
              <span>服务端关联：{objectSummary ? `${objectSummary.enabledCandidateCount} 可用 / ${objectSummary.disabledCandidateCount} 禁用` : "无候选"}</span>
              <WireObjectContextSummary context={objectContext} contract={prompt?.contract} focusModel={focusModel} />
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

      {inspectedCard && (
        <div className="wire-selected-candidates">
          <strong>焦点候选</strong>
          {relatedCandidates.length === 0 && <span className="empty-hint">该卡当前未出现在服务端候选中。</span>}
          {relatedCandidateRows.slice(0, 5).map((row) => (
            <CandidateSummaryRow
              key={row.key}
              objects={objectIndex}
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
        focusModel={focusModel}
        model={model}
        onCommand={onCommand}
        prompt={prompt}
        selectionDraft={selectionDraft}
        snapshot={snapshot}
      />

      <PromptCandidateList
        model={model}
        objects={objectIndex}
        onInspectObject={onInspectObject}
        prompt={prompt}
        selectedObjectId={selectedObjectId}
      />
    </section>
  );
}

function FocusedActionList({
  disabledByConnection,
  focusModel,
  inspectedCard,
  model,
  onCommand,
  prompt,
  selectionDraft,
  snapshot
}: {
  disabledByConnection: boolean;
  focusModel: FocusedActionModel;
  inspectedCard?: InspectedCard;
  model: PromptInteractionModel;
  onCommand?: (command: GameCommand) => void;
  prompt?: ActionPromptDto;
  selectionDraft?: CandidateSelectionDraft;
  snapshot?: SnapshotDto;
}) {
  const sourceObjectId = inspectedCard?.objectId ?? inspectedCard?.object?.objectId;
  const candidates = sourceCandidatesForPrompt(prompt, sourceObjectId);
  const candidateSummaries = sourceObjectId
    ? model.candidates.filter((candidate) =>
      candidate.enabled
      && candidate.choices.some((choice) =>
        choice.role === "source"
        && promptChoiceSummaryObjectIds(choice).includes(sourceObjectId)))
    : [];
  const grammarPlan = buildFocusedInteractionGrammarPlan({
    candidates: candidateSummaries,
    disabledByConnection,
    selectionDraft,
    sourceObjectId
  });

  if (!inspectedCard) {
    return null;
  }

  return (
    <div className="wire-focused-actions">
      <div className="wire-focused-actions-heading">
        <strong>焦点操作入口</strong>
        <StatusPill tone={candidates.length > 0 ? "good" : "neutral"}>{candidates.length > 0 ? `${candidates.length} 项` : "无可提交"}</StatusPill>
      </div>
      <p>只使用服务端当前候选；连接恢复前不会提交命令。</p>
      <FocusedActionSummary focusModel={focusModel} />
      <FocusedInteractionGrammar plan={grammarPlan} />
      {candidates.length === 0 && <span className="empty-hint">当前服务端没有给该对象可提交操作。</span>}
      {selectionDraft && selectionDraft.sourceObjectId === sourceObjectId && (
        <div className="wire-selection-draft" role="group" aria-label="已点选候选草稿">
          <strong>桌面点选</strong>
          <span>目标 {selectionDraft.targetChoiceIds.length}</span>
          <span>位置 {selectionDraft.destinationId ? "已选" : "未选"}</span>
          <span>费用 {selectionDraft.optionalCostIds.length}</span>
        </div>
      )}
      {candidateSummaries.length > 0 && (
        <div className="wire-focused-path" role="group" aria-label="焦点候选路径">
          {candidateSummaries.slice(0, 2).map((candidate) => (
            <article key={`${candidate.action}-${candidate.label}`}>
              <strong>{candidate.label}</strong>
              <ol>
                {candidate.steps.map((step) => (
                  <li className={step.required ? "is-required" : ""} key={`${candidate.action}-${step.role}`}>
                    <span>{step.label}</span>
                    <small>{step.required ? "必需；" : ""}{step.sampleLabels.length > 0 ? step.sampleLabels.join(" / ") : "服务端候选"}</small>
                  </li>
                ))}
              </ol>
            </article>
          ))}
        </div>
      )}
      {candidates.slice(0, 4).map((candidate) => {
        const actionPlan = buildSourceCandidateActionPlan({
          canSubmitCommands: Boolean(onCommand),
          candidate,
          disabledByConnection,
          sourceObjectId
        });

        if (actionPlan.needsComposer && onCommand) {
          const candidateDraft = selectionDraft?.candidateKey === candidateComposerKey(candidate)
            ? selectionDraft
            : undefined;
          return (
            <CandidateComposer
              candidate={candidate}
              disabledByConnection={disabledByConnection}
              forcedSourceObjectId={sourceObjectId}
              key={`${candidate.action}-${candidate.label}`}
              onCommand={onCommand}
              prompt={prompt}
              selectionDraft={candidateDraft}
              snapshot={snapshot}
            />
          );
        }

        return (
          <Button
            disabled={actionPlan.disabled}
            icon={<Play size={16} />}
            key={`${candidate.action}-${candidate.label}`}
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
  model,
  objects,
  onInspectObject,
  prompt,
  selectedObjectId
}: {
  model: PromptInteractionModel;
  objects: WireObjectIndex;
  onInspectObject?: (objectId: string) => void;
  prompt?: ActionPromptDto;
  selectedObjectId?: string;
}) {
  const plan = buildWirePromptCandidateListPlan({
    model,
    objects,
    promptId: prompt?.promptId,
    promptMessage: prompt?.view?.message,
    promptReason: prompt?.reason,
    promptTitle: prompt?.view?.title,
    promptType: prompt?.view?.type,
    snapshotTick: prompt?.snapshotTick
  });

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

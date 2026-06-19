import type { InspectedCard } from "../cards/CardFace";
import { Maximize2, Play } from "lucide-react";
import type { ActionPromptDto, GameCommand, SnapshotDto } from "../../types/protocol";
import type { TableObjectContext } from "../../utils/tableObjectContext";
import { commandForSourceCandidate, promptStampedCommand, sourceCandidatesForPrompt } from "../../utils/actionPromptCandidates";
import {
  buildPromptInteractionModel,
  promptChoiceRoleLabel,
  promptChoiceSummaryObjectIds,
  type PromptCandidateSummary,
  type PromptInteractionModel
} from "../../utils/promptInteraction";
import { buildFocusedActionModel, type FocusedActionModel } from "../../utils/focusedActionModel";
import { promptActionLabel, promptReasonTitle } from "../../utils/formatters";
import type { CandidateSelectionDraft } from "../../utils/candidateSelectionDraft";
import { buildFocusedInteractionGrammarPlan, type FocusedInteractionGrammarPlan } from "../../utils/focusedInteractionGrammarPlan";
import { CardFace } from "../cards/CardFace";
import { CandidateComposer, candidateComposerKey, canComposeCandidate } from "./CandidateComposer";
import { Button } from "../ui/Button";
import { StatusPill } from "../ui/StatusPill";
import { buildCardObjectIndex } from "../../utils/snapshotObjectIndex";
import { WireObjectRefChips, type WireObjectIndex, type WireObjectRef } from "./WireObjectRefChips";
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
          {relatedCandidates.slice(0, 5).map((candidate) => (
            <CandidateSummaryRow
              candidate={candidate}
              key={`${candidate.action}-${candidate.label}`}
              objects={objectIndex}
              onInspectObject={onInspectObject}
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
        const command = commandForSourceCandidate(candidate, sourceObjectId);

        if (canComposeCandidate(candidate) && onCommand) {
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
            disabled={disabledByConnection || !candidate.enabled || !command || !onCommand}
            icon={<Play size={16} />}
            key={`${candidate.action}-${candidate.label}`}
            onClick={() => {
              if (command && onCommand) {
                onCommand(promptStampedCommand(command, prompt));
              }
            }}
            title={command ? promptReasonTitle(candidate.reason) : "该候选还需要服务端提供完整选择后才能提交"}
            variant={candidate.enabled && command ? "primary" : "ghost"}
          >
            {promptActionLabel(candidate)}
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
  const enabled = model.candidates.filter((candidate) => candidate.enabled);
  const disabled = model.candidates.filter((candidate) => !candidate.enabled);
  const promptType = prompt?.view?.type ?? "无";

  return (
    <div className="wire-prompt-candidates">
      <div className="wire-prompt-contract">
        <strong>{prompt?.view?.title?.trim() || "当前行动窗口"}</strong>
        <span>类型：{promptType}</span>
        <span>提示：{prompt?.view?.message?.trim() || prompt?.reason || "等待服务端提示"}</span>
        <span>版本：{prompt?.promptId ?? "无"} / tick {prompt?.snapshotTick ?? "无"}</span>
      </div>
      {model.candidates.length === 0 && <span className="empty-hint">服务端暂未提供候选行动。</span>}
      {enabled.slice(0, 6).map((candidate) => (
        <CandidateSummaryRow
          candidate={candidate}
          key={`enabled-${candidate.action}-${candidate.label}`}
          objects={objects}
          onInspectObject={onInspectObject}
          selectedObjectId={selectedObjectId}
        />
      ))}
      {disabled.slice(0, 4).map((candidate) => (
        <CandidateSummaryRow
          candidate={candidate}
          key={`disabled-${candidate.action}-${candidate.label}`}
          objects={objects}
          onInspectObject={onInspectObject}
          selectedObjectId={selectedObjectId}
        />
      ))}
    </div>
  );
}

function CandidateSummaryRow({
  candidate,
  objects,
  onInspectObject,
  selectedObjectId
}: {
  candidate: PromptCandidateSummary;
  objects: WireObjectIndex;
  onInspectObject?: (objectId: string) => void;
  selectedObjectId?: string;
}) {
  const choiceGroups = candidate.choices.reduce<Record<string, string[]>>((groups, choice) => {
    const key = promptChoiceRoleLabel(choice.role);
    groups[key] = [...(groups[key] ?? []), choice.label];
    return groups;
  }, {});
  const objectRefs = candidateObjectRefs(candidate, objects);

  return (
    <article className={`wire-candidate-row ${candidate.enabled ? "is-enabled" : "is-disabled"}`}>
      <div>
        <strong>{candidate.label}</strong>
        <StatusPill tone={candidate.enabled ? "good" : "neutral"}>{candidate.enabled ? "可提交" : "不可提交"}</StatusPill>
      </div>
      <span>{candidate.reason}</span>
      {Object.entries(choiceGroups).slice(0, 5).map(([role, labels]) => (
        <small key={role}>{role}：{labels.slice(0, 3).join("、")}{labels.length > 3 ? ` 等 ${labels.length} 项` : ""}</small>
      ))}
      <WireObjectRefChips
        className="wire-candidate-object-ref-list"
        objects={objects}
        onInspectObject={onInspectObject}
        refs={objectRefs}
        selectedObjectId={selectedObjectId}
        source="candidate"
      />
    </article>
  );
}

function candidateObjectRefs(candidate: PromptCandidateSummary, objects: WireObjectIndex): WireObjectRef[] {
  return candidate.choices.flatMap((choice) => {
    const role = promptChoiceRoleLabel(choice.role);
    return promptChoiceSummaryObjectIds(choice)
      .filter((objectId) => Boolean(objects[objectId]))
      .map((objectId) => ({ id: objectId, label: choice.label, role }));
  });
}

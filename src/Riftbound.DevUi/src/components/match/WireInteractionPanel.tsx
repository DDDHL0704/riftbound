import type { InspectedCard } from "../cards/CardFace";
import { Maximize2 } from "lucide-react";
import type { ActionPromptDto, GameCommand, SnapshotDto } from "../../types/protocol";
import type { TableObjectContext } from "../../utils/tableObjectContext";
import type { CandidateSelectionDraft } from "../../utils/candidateSelectionDraft";
import type { ServerSubmissionGatePlan } from "../../utils/serverSubmissionGatePlan";
import {
  buildWireFocusedInteractionPlan,
  type WireFocusedInteractionPlan
} from "../../utils/wireFocusedInteractionPlan";
import type { WirePromptCandidateListPlan } from "../../utils/wirePromptCandidatePlan";
import { CardFace } from "../cards/CardFace";
import { Button } from "../ui/Button";
import { StatusPill } from "../ui/StatusPill";
import type { WireObjectIndex } from "./WireObjectRefChips";
import { WireObjectContextSummary } from "./WireObjectContextSummary";
import { WireEmpty } from "./wireCardFlow";
import { WireFocusedActionSummary } from "./WireFocusedActionSummary";
import { WireFocusedActionEntryList } from "./WireFocusedActionEntryList";
import { WireFocusedInteractionGrammar } from "./WireFocusedInteractionGrammar";
import { WireFocusedLegalActionMatrix } from "./WireFocusedLegalActionMatrix";
import { WireFocusedReadinessStrip } from "./WireFocusedReadinessStrip";
import { WireFocusedSelectionGuide } from "./WireFocusedSelectionGuide";
import { WireObjectRouteReview } from "./WireObjectRouteReview";
import { WirePromptCandidateRow } from "./WirePromptCandidateRow";

export function WireInteractionPanel({
  disabledByConnection,
  focusedPlan,
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
  focusedPlan?: WireFocusedInteractionPlan;
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
  const plan = focusedPlan ?? buildWireFocusedInteractionPlan({
    canSubmitCommands: Boolean(onCommand),
    disabledByConnection,
    playerId,
    prompt,
    selectionDraft,
    snapshot,
    sourceControllerId: inspectedCard?.object?.controllerId,
    sourceObjectId: selectedObjectId,
    submissionGate
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

      <WireFocusedReadinessStrip plan={plan} />
      {inspectedCard && <WireFocusedLegalActionMatrix plan={plan} />}

      {inspectedCard && (
        <div className="wire-selected-candidates">
          <strong>焦点候选</strong>
          {plan.relatedCandidateRows.length === 0 && <span className="empty-hint">该卡当前未出现在服务端候选中。</span>}
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
      <WireFocusedActionSummary focusModel={plan.focusModel} />
      <WireFocusedInteractionGrammar plan={plan.grammarPlan} />
      <WireObjectRouteReview onCommand={onCommand} review={plan.commandReview} route={plan.route} />
      {plan.sourceCandidates.length === 0 && <span className="empty-hint">当前服务端没有给该对象可提交操作。</span>}
      <WireFocusedSelectionGuide plan={plan} />
      <WireFocusedActionEntryList
        disabledByConnection={disabledByConnection}
        maxEntries={4}
        onCommand={onCommand}
        plan={plan}
        prompt={prompt}
        snapshot={snapshot}
        submissionGate={submissionGate}
      />
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
        <WirePromptCandidateRow
          key={row.key}
          objects={objects}
          onInspectObject={onInspectObject}
          row={row}
          selectedObjectId={selectedObjectId}
        />
      ))}
      {plan.disabledRows.map((row) => (
        <WirePromptCandidateRow
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

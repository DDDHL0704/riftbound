import type { InspectedCard } from "../cards/CardFace";
import { Play } from "lucide-react";
import type { ActionPromptCandidateDto, ActionPromptDto, GameCommand, SnapshotDto } from "../../types/protocol";
import {
  buildPromptInteractionModel,
  promptChoiceRoleLabel,
  type PromptCandidateSummary,
  type PromptInteractionModel
} from "../../utils/promptInteraction";
import { promptActionLabel, promptReasonTitle } from "../../utils/formatters";
import { CardFace } from "../cards/CardFace";
import { CandidateComposer, canComposeCandidate } from "./CandidateComposer";
import { Button } from "../ui/Button";
import { StatusPill } from "../ui/StatusPill";
import { WireEmpty } from "./wireCardFlow";

export function WireInteractionPanel({
  disabledByConnection,
  inspectedCard,
  onCommand,
  onClearInspectedCard,
  playerId,
  prompt,
  snapshot
}: {
  disabledByConnection: boolean;
  inspectedCard?: InspectedCard;
  onCommand?: (command: GameCommand) => void;
  onClearInspectedCard: () => void;
  playerId: string;
  prompt?: ActionPromptDto;
  snapshot?: SnapshotDto;
}) {
  const model = buildPromptInteractionModel(prompt);
  const inspectedObjectId = inspectedCard?.objectId;
  const objectSummary = inspectedObjectId ? model.objectById.get(inspectedObjectId) : undefined;
  const relatedCandidates = inspectedObjectId
    ? model.candidates.filter((candidate) => candidate.choices.some((choice) => choice.id === inspectedObjectId || choice.id.endsWith(`:${inspectedObjectId}`)))
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
              <button type="button" onClick={onClearInspectedCard}>清除焦点</button>
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
            <CandidateSummaryRow candidate={candidate} key={`${candidate.action}-${candidate.label}`} />
          ))}
        </div>
      )}

      <FocusedActionList
        disabledByConnection={disabledByConnection}
        inspectedCard={inspectedCard}
        onCommand={onCommand}
        prompt={prompt}
        snapshot={snapshot}
      />

      <PromptCandidateList model={model} prompt={prompt} />
    </section>
  );
}

function FocusedActionList({
  disabledByConnection,
  inspectedCard,
  onCommand,
  prompt,
  snapshot
}: {
  disabledByConnection: boolean;
  inspectedCard?: InspectedCard;
  onCommand?: (command: GameCommand) => void;
  prompt?: ActionPromptDto;
  snapshot?: SnapshotDto;
}) {
  const sourceObjectId = inspectedCard?.objectId ?? inspectedCard?.object?.objectId;
  const candidates = sourceCandidatesFor(prompt, sourceObjectId);

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
      {candidates.length === 0 && <span className="empty-hint">当前服务端没有给该对象可提交操作。</span>}
      {candidates.slice(0, 4).map((candidate) => {
        const command = commandForSourceCandidate(candidate, sourceObjectId);

        if (canComposeCandidate(candidate) && onCommand) {
          return (
            <CandidateComposer
              candidate={candidate}
              disabledByConnection={disabledByConnection}
              forcedSourceObjectId={sourceObjectId}
              key={`${candidate.action}-${candidate.label}`}
              onCommand={onCommand}
              prompt={prompt}
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
                onCommand(withPromptStamp(command, prompt));
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

function PromptCandidateList({ model, prompt }: { model: PromptInteractionModel; prompt?: ActionPromptDto }) {
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
        <CandidateSummaryRow candidate={candidate} key={`enabled-${candidate.action}-${candidate.label}`} />
      ))}
      {disabled.slice(0, 4).map((candidate) => (
        <CandidateSummaryRow candidate={candidate} key={`disabled-${candidate.action}-${candidate.label}`} />
      ))}
    </div>
  );
}

function CandidateSummaryRow({ candidate }: { candidate: PromptCandidateSummary }) {
  const choiceGroups = candidate.choices.reduce<Record<string, string[]>>((groups, choice) => {
    const key = promptChoiceRoleLabel(choice.role);
    groups[key] = [...(groups[key] ?? []), choice.label];
    return groups;
  }, {});

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
    </article>
  );
}

function sourceCandidatesFor(prompt: ActionPromptDto | undefined, sourceObjectId: string | undefined): ActionPromptCandidateDto[] {
  if (!prompt || !sourceObjectId) {
    return [];
  }

  return (prompt.candidates ?? []).filter((candidate) =>
    candidate.enabled && (
      (candidate.sources ?? []).some((source) => source.id === sourceObjectId)
      || sourceRequirementIds(candidate).includes(sourceObjectId)
    ));
}

function sourceRequirementIds(candidate: ActionPromptCandidateDto): string[] {
  const requirements = candidate.metadata?.sourceRequirements;
  const records = Array.isArray(requirements)
    ? requirements
    : requirements && typeof requirements === "object"
      ? Object.values(requirements)
      : [];
  return records
    .filter((value): value is Record<string, unknown> => Boolean(value) && typeof value === "object" && !Array.isArray(value))
    .map((record) => record.sourceObjectId)
    .filter((value): value is string => typeof value === "string" && value.trim().length > 0);
}

function commandForSourceCandidate(
  candidate: ActionPromptCandidateDto,
  sourceObjectId: string | undefined
): GameCommand | undefined {
  if (!sourceObjectId || !candidate.enabled) {
    return undefined;
  }

  if (candidate.action === "TAP_RUNE") {
    return { cmdType: "TAP_RUNE", sourceObjectId };
  }

  if (candidate.action === "RECYCLE_RUNE") {
    return { cmdType: "RECYCLE_RUNE", sourceObjectId };
  }

  return undefined;
}

function withPromptStamp(command: GameCommand, prompt: ActionPromptDto | undefined): GameCommand {
  if (!prompt || (command.promptId != null && command.snapshotTick != null)) {
    return command;
  }

  return {
    ...command,
    promptId: command.promptId ?? prompt.promptId ?? null,
    snapshotTick: command.snapshotTick ?? prompt.snapshotTick ?? null
  };
}

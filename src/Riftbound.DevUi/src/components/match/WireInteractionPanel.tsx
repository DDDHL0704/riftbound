import type { InspectedCard } from "../cards/CardFace";
import type { ActionPromptDto } from "../../types/protocol";
import {
  buildPromptInteractionModel,
  promptChoiceRoleLabel,
  type PromptCandidateSummary,
  type PromptInteractionModel
} from "../../utils/promptInteraction";
import { CardFace } from "../cards/CardFace";
import { StatusPill } from "../ui/StatusPill";
import { WireEmpty } from "./wireCardFlow";

export function WireInteractionPanel({
  inspectedCard,
  onClearInspectedCard,
  playerId,
  prompt
}: {
  inspectedCard?: InspectedCard;
  onClearInspectedCard: () => void;
  playerId: string;
  prompt?: ActionPromptDto;
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

      <PromptCandidateList model={model} prompt={prompt} />
    </section>
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

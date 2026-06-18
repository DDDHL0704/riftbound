import type { InspectedCard } from "../cards/CardFace";
import type { ActionPromptDto, SnapshotDto } from "../../types/protocol";
import { asString } from "../../utils/collections";
import {
  buildPromptInteractionModel,
  promptChoiceRoleLabel,
  type PromptCandidateSummary,
  type PromptInteractionModel
} from "../../utils/promptInteraction";
import { redactInternalText } from "../../utils/redaction";
import { CardFace } from "../cards/CardFace";
import { StatusPill } from "../ui/StatusPill";
import { WireEmpty } from "./wireCardFlow";

export function WireInteractionPanel({
  inspectedCard,
  onClearInspectedCard,
  playerId,
  prompt,
  snapshot
}: {
  inspectedCard?: InspectedCard;
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

      <PromptCandidateList model={model} prompt={prompt} />
      <WireRuleQueue snapshot={snapshot} />
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

function WireRuleQueue({ snapshot }: { snapshot?: SnapshotDto }) {
  const timing = snapshot?.timing;
  const queue = timing?.pendingTaskQueue ?? {};
  const tasks = queue.tasks ?? [];
  const triggerQueue = timing?.triggerQueue ?? [];
  const battleResolutions = timing?.battleResolutions ?? [];
  const battlefieldResolutions = timing?.battlefieldResolutions ?? [];
  const stack = snapshot?.stack ?? [];

  return (
    <div className="wire-rule-queue">
      <strong>结算链 / 规则事件</strong>
      <span>结算链：{stack.length} 项</span>
      <span>任务队列：{tasks.length} 项{queue.isBlocking ? " / 阻塞行动" : ""}</span>
      <span>触发队列：{triggerQueue.length} 项</span>
      {stack.slice(0, 3).map((item, index) => {
        return (
          <small key={`stack-${index}`}>
            结算 {stack.length - index}：{stackKindLabel(item.effectKind)} / 来源 {asString(item.cardNo, "服务端效果")}
          </small>
        );
      })}
      {tasks.slice(0, 3).map((task, index) => (
        <small key={task.taskId ?? `task-${index}`}>
          任务：{protocolLabel(task.kind, "服务端任务")} / {protocolLabel(task.reason, "服务端规则")}
        </small>
      ))}
      {battlefieldResolutions.slice(0, 3).map((resolution, index) => (
        <small key={resolution.resolutionId ?? `battlefield-resolution-${index}`}>
          战场：{battlefieldResolutionLabel(resolution.kind)} / {asString(resolution.playerId ?? resolution.controllerId, "无控制者")}
        </small>
      ))}
      {battleResolutions.slice(0, 2).map((resolution, index) => (
        <small key={resolution.resolutionId ?? `battle-resolution-${index}`}>
          战斗：{protocolLabel(resolution.kind, "服务端结果")} / 胜者 {asString(resolution.winnerPlayerId, "无")}
        </small>
      ))}
    </div>
  );
}

function stackKindLabel(value: unknown): string {
  switch (asString(value, "")) {
    case "SPELL":
      return "法术";
    case "ABILITY":
      return "技能";
    case "LEGEND_ABILITY":
      return "传奇技能";
    case "TRIGGER":
      return "触发";
    case "REVEAL_CARD":
      return "翻开待命";
    default:
      return protocolLabel(value, "服务端效果");
  }
}

function battlefieldResolutionLabel(value: unknown): string {
  switch (asString(value, "")) {
    case "CONQUERED":
      return "征服";
    case "CONTROL_RESOLVED":
      return "控制结算";
    case "HELD":
      return "据守";
    default:
      return protocolLabel(value, "战场结果");
  }
}

function protocolLabel(value: unknown, fallback: string): string {
  const raw = asString(value, "");
  if (!raw) {
    return fallback;
  }
  return /^[A-Z0-9_:-]+$/.test(raw) ? fallback : redactInternalText(raw);
}

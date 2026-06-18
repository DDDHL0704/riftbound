import type { ActionPromptCandidateDto, ActionPromptDto, CardObjectView, SnapshotDto } from "../../types/protocol";
import { asRecord } from "../../utils/collections";
import { promptActionLabel, promptReasonLabel } from "../../utils/formatters";
import {
  buildPromptInteractionModel,
  promptChoiceRoleLabel,
  type PromptCandidateSummary,
  type PromptChoiceRole,
  type PromptInteractionModel
} from "../../utils/promptInteraction";
import { StatusPill } from "../ui/StatusPill";

type WireActionMapPanelProps = {
  playerId: string;
  prompt?: ActionPromptDto;
  snapshot?: SnapshotDto;
};

type ObjectIndex = Record<string, CardObjectView>;

type ActionGroup = {
  action: string;
  candidates: PromptCandidateSummary[];
  enabledCount: number;
};

const roleOrder: PromptChoiceRole[] = ["source", "target", "destination", "optionalCost", "mode"];

export function WireActionMapPanel({ playerId, prompt, snapshot }: WireActionMapPanelProps) {
  const model = buildPromptInteractionModel(prompt);
  const objects = objectIndex(snapshot);
  const groups = actionGroups(model);
  const enabledCandidates = model.candidates.filter((candidate) => candidate.enabled);
  const canAct = Boolean(prompt?.actionable && prompt.playerId === playerId);
  const enabledObjects = [...model.enabledObjectIds];
  const disabledOnlyObjects = [...model.disabledObjectIds];
  const knownEnabledObjects = enabledObjects.filter((objectId) => objects[objectId]);
  const knownDisabledOnlyObjects = disabledOnlyObjects.filter((objectId) => objects[objectId]);

  return (
    <section className="wire-action-map" aria-label="服务端合法操作地图">
      <header className="wire-action-map-header">
        <div>
          <strong>合法操作地图</strong>
          <span>只投影服务端候选，不在前端推断规则。</span>
        </div>
        <StatusPill tone={canAct ? "good" : "neutral"}>{canAct ? "可操作窗口" : "只读窗口"}</StatusPill>
      </header>

      <div className="wire-action-map-metrics">
        <Metric label="可提交" value={`${enabledCandidates.length}`} />
        <Metric label="全部候选" value={`${model.candidates.length}`} />
        <Metric label="对象入口" value={`${knownEnabledObjects.length}`} />
        <Metric label="不可提交关联" value={`${knownDisabledOnlyObjects.length}`} />
      </div>

      <div aria-label="服务端可操作对象入口" className="wire-action-entry-strip" tabIndex={0}>
        {knownEnabledObjects.length === 0 && <span className="empty-hint">当前没有服务端标记为可操作的场上对象。</span>}
        {knownEnabledObjects.slice(0, 6).map((objectId) => {
          const summary = model.objectById.get(objectId);
          return (
            <span className="wire-action-object-chip" key={objectId}>
              <strong>{objectLabel(objectId, objects)}</strong>
              <small>{summary?.enabledCandidateCount ?? 0} 项</small>
            </span>
          );
        })}
        {knownEnabledObjects.length > 6 && <span className="wire-action-object-chip">等 {knownEnabledObjects.length} 个对象</span>}
      </div>

      <div className="wire-action-group-list">
        {groups.length === 0 && <span className="empty-hint">等待服务端行动窗口。</span>}
        {groups.slice(0, 5).map((group) => (
          <article className={group.enabledCount > 0 ? "wire-action-group is-enabled" : "wire-action-group"} key={group.action}>
            <div className="wire-action-group-heading">
              <strong>{actionGroupLabel(group.action, group.candidates)}</strong>
              <span>{group.enabledCount} / {group.candidates.length}</span>
            </div>
            <div className="wire-action-role-grid">
              {roleOrder.map((role) => (
                <span key={role}>
                  {promptChoiceRoleLabel(role)} {roleCount(group.candidates, role)}
                </span>
              ))}
            </div>
            <small>{groupReason(group.candidates)}</small>
          </article>
        ))}
      </div>

      <div className="wire-action-grammar" aria-label="服务端候选交互语法">
        <strong>交互语法</strong>
        {model.candidates.length === 0 && <span className="empty-hint">暂无候选步骤。</span>}
        {model.candidates
          .filter((candidate) => candidate.enabled)
          .slice(0, 4)
          .map((candidate) => (
            <article className="wire-action-sequence" key={`${candidate.action}-${candidate.label}`}>
              <div className="wire-action-sequence-title">
                <span>{candidate.label}</span>
                <small>{candidate.steps.length} 步</small>
              </div>
              <ol>
                {candidate.steps.map((step) => (
                  <li className={`wire-action-step wire-action-step-${step.role} ${step.required ? "is-required" : ""}`} key={`${candidate.action}-${step.role}`}>
                    <span>{step.label}</span>
                    <strong>{step.count}</strong>
                    <small>{step.required ? "必需；" : ""}{step.sampleLabels.length > 0 ? step.sampleLabels.join(" / ") : "由服务端候选决定"}</small>
                  </li>
                ))}
              </ol>
            </article>
          ))}
      </div>
    </section>
  );
}

function Metric({ label, value }: { label: string; value: string }) {
  return (
    <span className="wire-action-map-metric">
      <span>{label}</span>
      <strong>{value}</strong>
    </span>
  );
}

function actionGroups(model: PromptInteractionModel): ActionGroup[] {
  const byAction = new Map<string, PromptCandidateSummary[]>();
  for (const candidate of model.candidates) {
    byAction.set(candidate.action, [...(byAction.get(candidate.action) ?? []), candidate]);
  }

  return [...byAction.entries()]
    .map(([action, candidates]) => ({
      action,
      candidates,
      enabledCount: candidates.filter((candidate) => candidate.enabled).length
    }))
    .sort((left, right) => right.enabledCount - left.enabledCount || right.candidates.length - left.candidates.length || left.action.localeCompare(right.action));
}

function actionGroupLabel(action: string, candidates: PromptCandidateSummary[]): string {
  const source = { action, label: candidates[0]?.label ?? action, enabled: true, reason: "" } satisfies ActionPromptCandidateDto;
  return promptActionLabel(source);
}

function roleCount(candidates: PromptCandidateSummary[], role: PromptChoiceRole): number {
  return new Set(candidates.flatMap((candidate) => candidate.choices.filter((choice) => choice.role === role).map((choice) => choice.id))).size;
}

function groupReason(candidates: PromptCandidateSummary[]): string {
  const enabled = candidates.find((candidate) => candidate.enabled);
  if (enabled) {
    return enabled.reason;
  }

  return candidates[0]?.reason ?? "服务端未提供候选原因。";
}

function objectIndex(snapshot?: SnapshotDto): ObjectIndex {
  return Object.values(snapshot?.players ?? {}).reduce<ObjectIndex>((index, player) => {
    const objects = asRecord(player.objects);
    Object.entries(objects).forEach(([objectId, value]) => {
      const object = value as CardObjectView;
      index[object.objectId ?? objectId] = object;
    });
    return index;
  }, {});
}

function objectLabel(objectId: string, objects: ObjectIndex): string {
  if (objectId === "HIDDEN") {
    return "隐藏对象";
  }

  const object = objects[objectId];
  return object?.cardNo ?? safeChoiceId(objectId);
}

function safeChoiceId(value: string): string {
  return /^[A-Z0-9_:-]+$/.test(value) || /^[a-z0-9]+(?:[-_:][a-z0-9]+)+$/.test(value)
    ? "服务端对象"
    : promptReasonLabel(value, "服务端对象");
}

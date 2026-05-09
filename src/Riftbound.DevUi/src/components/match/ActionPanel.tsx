import { ArrowDown, ArrowUp, Check, Flag, Hourglass, ListOrdered, Play, Send, X } from "lucide-react";
import { useEffect, useMemo, useState } from "react";
import type { ActionPromptCandidateDto, ActionPromptChoiceDto, ActionPromptDto, CombatDamageAssignmentDto, ConnectionStatus, GameCommand, SnapshotDto } from "../../types/protocol";
import { connectionStatusLabel, promptActionLabel, promptReasonLabel, promptReasonTitle } from "../../utils/formatters";
import { redactInternalText } from "../../utils/redaction";
import { Button } from "../ui/Button";
import { StatusPill } from "../ui/StatusPill";

type ActionPanelProps = {
  prompt?: ActionPromptDto;
  snapshot?: SnapshotDto;
  connectionStatus: ConnectionStatus;
  playerId: string;
  onReady: () => void;
  onSubmitStarterDeck: () => void;
  onCommand: (command: GameCommand) => void;
};

export function ActionPanel({ prompt, snapshot, connectionStatus, playerId, onReady, onSubmitStarterDeck, onCommand }: ActionPanelProps) {
  const allCandidates = prompt?.candidates ?? [];
  const candidates = allCandidates.filter((candidate) => candidate.enabled);
  const connected = connectionStatus === "connected";
  const canAct = connected && prompt?.actionable && prompt.playerId === playerId;
  const promptView = prompt?.view;
  const orderTriggersCandidate = allCandidates.find((candidate) => candidate.action === "ORDER_TRIGGERS");
  const showReadonlyOrderTriggers = promptView?.type === "ORDER_TRIGGERS"
    && !candidates.some((candidate) => candidate.action === "ORDER_TRIGGERS");
  const promptTitle = promptView?.title?.trim() || "当前行动";
  const promptMessage = promptView?.message?.trim()
    || (prompt ? promptReasonLabel(prompt.reason, "服务端行动提示") : "尚未收到行动提示");

  return (
    <section className="side-panel action-panel">
      <header>
        <span className="eyebrow">服务端行动提示</span>
        <h2>{promptTitle}</h2>
      </header>
      <div className="prompt-summary">
        <StatusPill tone={canAct ? "good" : "neutral"}>{canAct ? "轮到你操作" : "等待服务端或对手"}</StatusPill>
        <span>提示状态：{prompt ? "已收到" : "无"}</span>
        {promptView?.type && <span>类型：{promptView.type}</span>}
        <span>{promptView ? "说明" : "原因"}：{promptMessage}</span>
        {promptView?.relatedBattlefieldId && <span>关联战场：{promptView.relatedBattlefieldId}</span>}
        {promptView?.relatedBattleId && <span>关联战斗：{promptView.relatedBattleId}</span>}
        {promptView?.relatedSpellDuelId && <span>关联法术对决：{promptView.relatedSpellDuelId}</span>}
        {promptView?.relatedStackItemId && <span>关联结算链：{promptView.relatedStackItemId}</span>}
        {!connected && <span>连接状态：{connectionStatusLabel(connectionStatus)}，行动入口已暂停。</span>}
      </div>
      {prompt && shouldShowGenericPromptDetails(prompt) && <GenericPromptDetails prompt={prompt} />}
      <div className="action-buttons">
        {showReadonlyOrderTriggers && (
          <OrderTriggersCandidate
            canAct={false}
            candidate={orderTriggersCandidate}
            disabledByConnection={!connected}
            onCommand={onCommand}
            prompt={prompt}
            readOnly
          />
        )}
        {candidates.length === 0 && !showReadonlyOrderTriggers && <span className="empty-hint">服务端暂未提供可提交候选。</span>}
        {candidates.map((candidate) => candidate.action === "MULLIGAN" ? (
          <MulliganCandidate
            candidate={candidate}
            disabledByConnection={!connected}
            key={`${candidate.action}-${candidate.label}`}
            onCommand={onCommand}
            prompt={prompt}
          />
        ) : candidate.action === "ASSIGN_COMBAT_DAMAGE" ? (
          <DamageAssignmentCandidate
            candidate={candidate}
            disabledByConnection={!connected}
            key={`${candidate.action}-${candidate.label}`}
            onCommand={onCommand}
            prompt={prompt}
            snapshot={snapshot}
          />
        ) : candidate.action === "ORDER_TRIGGERS" ? (
          <OrderTriggersCandidate
            canAct={Boolean(canAct)}
            candidate={candidate}
            disabledByConnection={!connected}
            key={`${candidate.action}-${candidate.label}`}
            onCommand={onCommand}
            prompt={prompt}
          />
        ) : (
          <CandidateButton
            candidate={candidate}
            key={`${candidate.action}-${candidate.label}`}
            onCommand={onCommand}
            onReady={onReady}
            onSubmitStarterDeck={onSubmitStarterDeck}
            disabledByConnection={!connected}
            prompt={prompt}
            snapshot={snapshot}
          />
        ))}
      </div>
    </section>
  );
}

function GenericPromptDetails({ prompt }: { prompt: ActionPromptDto }) {
  const metadataEntries = Object.entries(prompt.view?.metadata ?? {})
    .filter(([, value]) => value != null)
    .slice(0, 6);
  const candidates = (prompt.candidates ?? []).slice(0, 6);
  const type = prompt.view?.type?.trim() ?? "";
  const fallbackLabel = knownPromptTypes.has(type) ? "复杂窗口" : "未知窗口";

  return (
    <div className="generic-prompt-details">
      <div className="generic-prompt-heading">
        <strong>服务端选项</strong>
        <StatusPill tone="warn">{fallbackLabel}</StatusPill>
      </div>
      <p className="generic-prompt-note">
        该窗口需要服务端正式交互字段支持；当前只展示安全候选摘要，不在前端计算或模拟规则结果。
      </p>
      {candidates.length === 0 && <span className="empty-hint">当前窗口没有服务端可提交选项。</span>}
      {candidates.map((candidate) => (
        <div className="generic-prompt-option" key={`${candidate.action}-${candidate.label}`}>
          <span>{promptActionLabel(candidate)}</span>
          <small>{candidate.enabled ? promptReasonLabel(candidate.reason, "可提交") : promptReasonLabel(candidate.reason, "暂不可提交")}</small>
          <ChoicePreview title="来源" choices={candidate.sources} />
          <ChoicePreview title="目标" choices={candidate.targets} />
          <ChoicePreview title="位置" choices={candidate.destinations} />
          <ChoicePreview title="模式" choices={candidate.modes} />
          <ChoicePreview title="费用" choices={candidate.optionalCosts} />
        </div>
      ))}
      {metadataEntries.length > 0 && (
        <div className="generic-prompt-metadata">
          <strong>窗口数据</strong>
          {metadataEntries.map(([key, value]) => (
            <span key={key}>{redactInternalText(key)}：{safePromptValue(key, value)}</span>
          ))}
        </div>
      )}
    </div>
  );
}

function ChoicePreview({ title, choices }: { title: string; choices?: ActionPromptChoiceDto[] | null }) {
  if (!choices || choices.length === 0) {
    return null;
  }

  return (
    <span>{title}：{choices.slice(0, 4).map(choiceLabel).join("、")}{choices.length > 4 ? ` 等 ${choices.length} 项` : ""}</span>
  );
}

function MulliganCandidate({
  candidate,
  disabledByConnection,
  onCommand,
  prompt
}: {
  candidate: ActionPromptCandidateDto;
  disabledByConnection: boolean;
  onCommand: (command: GameCommand) => void;
  prompt?: ActionPromptDto;
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
  const canSubmit = !disabledByConnection && candidate.enabled && hasServerLimit && selectedObjectIds.length <= maxSelectionCount;

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
            disabled={disabledByConnection || !candidate.enabled || !hasServerLimit}
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
        title={disabledByConnection ? "连接恢复前不能提交起手调整" : promptReasonTitle(candidate.reason)}
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

type DamageAssignmentChoice = {
  key: string;
  sourceObjectId: string;
  sourceLabel: string;
  targetObjectId: string;
  targetLabel: string;
  existingDamage?: number;
  lethalThreshold?: number;
  sourceDamagePool?: number;
};

type DamageAssignmentModel = {
  battleId: string;
  battlefieldId: string;
  damagePoolLabel?: string;
  choices: DamageAssignmentChoice[];
  resetKey: string;
};

function DamageAssignmentCandidate({
  candidate,
  disabledByConnection,
  onCommand,
  prompt,
  snapshot
}: {
  candidate: ActionPromptCandidateDto;
  disabledByConnection: boolean;
  onCommand: (command: GameCommand) => void;
  prompt?: ActionPromptDto;
  snapshot?: SnapshotDto;
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
  const canSubmit = !disabledByConnection
    && candidate.enabled
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
              disabled={disabledByConnection || !candidate.enabled}
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
        title={disabledByConnection ? "连接恢复前不能提交伤害分配" : promptReasonTitle(candidate.reason)}
        variant={candidate.enabled ? "primary" : "ghost"}
      >
        提交伤害分配
      </Button>
    </div>
  );
}

function buildDamageAssignmentModel(
  candidate: ActionPromptCandidateDto,
  prompt: ActionPromptDto | undefined,
  snapshot: SnapshotDto | undefined
): DamageAssignmentModel {
  const metadata = candidate.metadata ?? {};
  const battleId = stringMetadata(metadata, "battleId") ?? prompt?.view?.relatedBattleId ?? "";
  const battlefieldId = stringMetadata(metadata, "battlefieldId")
    ?? stringMetadata(metadata, "battlefieldObjectId")
    ?? prompt?.view?.relatedBattlefieldId
    ?? "";
  const scalarDamagePool = firstNumberMetadata(metadata, ["damagePool", "totalDamage", "assignableDamage"]);
  const damagePoolBySource = numberMapMetadata(metadata, ["damagePool", "damagePoolBySource"]);
  const damagePoolLabel = scalarDamagePool == null
    ? damagePoolBySource.size > 0 ? `${damagePoolBySource.size} 个来源` : undefined
    : String(scalarDamagePool);
  const assignmentRecords = recordArrayMetadata(metadata.assignmentChoices);
  const existingDamageByTarget = numberMapMetadata(metadata, ["existingDamage", "existingDamageByTarget", "damageByTarget"]);
  const lethalThresholdByTarget = numberMapMetadata(metadata, ["lethalThreshold", "lethalThresholdByTarget", "lethalThresholds", "lethalDamageThreshold"]);
  const legalTargetsBySource = stringListMapMetadata(metadata, ["legalTargets", "legalTargetsBySource"]);
  const choices = assignmentRecords
    .map((record) => damageChoiceFromRecord(record, snapshot, existingDamageByTarget, lethalThresholdByTarget, damagePoolBySource))
    .filter((choice): choice is DamageAssignmentChoice => choice != null);

  const fallbackChoices = choices.length > 0
    ? choices
    : legalTargetsBySource.size > 0
      ? damageChoicesFromLegalTargets(legalTargetsBySource, snapshot, existingDamageByTarget, lethalThresholdByTarget, damagePoolBySource)
      : damageChoicesFromCandidate(candidate, snapshot, existingDamageByTarget, lethalThresholdByTarget, damagePoolBySource);
  const dedupedChoices = uniqueDamageChoices(fallbackChoices);

  return {
    battleId,
    battlefieldId,
    damagePoolLabel,
    choices: dedupedChoices,
    resetKey: [
      battleId,
      battlefieldId,
      damagePoolLabel ?? "none",
      dedupedChoices.map((choice) => choice.key).join("|")
    ].join("::")
  };
}

function damageChoiceFromRecord(
  record: Record<string, unknown>,
  snapshot: SnapshotDto | undefined,
  existingDamageByTarget: Map<string, number>,
  lethalThresholdByTarget: Map<string, number>,
  damagePoolBySource: Map<string, number>
): DamageAssignmentChoice | undefined {
  const parsedChoiceId = parseAssignmentChoiceId(firstStringFromRecord(record, ["id", "choiceId"]));
  const sourceObjectId = firstStringFromRecord(record, ["sourceObjectId", "sourceId", "attackerObjectId", "objectId"])
    ?? parsedChoiceId?.sourceObjectId;
  const targetObjectId = firstStringFromRecord(record, ["targetObjectId", "targetId", "defenderObjectId", "legalTargetId"])
    ?? parsedChoiceId?.targetObjectId;
  if (!sourceObjectId || !targetObjectId) {
    return undefined;
  }

  return makeDamageChoice({
    sourceObjectId,
    sourceLabel: firstStringFromRecord(record, ["sourceLabel", "sourceName"]) ?? objectLabel(snapshot, sourceObjectId),
    targetObjectId,
    targetLabel: firstStringFromRecord(record, ["targetLabel", "targetName", "label"]) ?? objectLabel(snapshot, targetObjectId),
    existingDamage: firstNumberFromRecord(record, ["existingDamage", "currentDamage"])
      ?? existingDamageByTarget.get(targetObjectId)
      ?? findObject(snapshot, targetObjectId)?.damage,
    lethalThreshold: firstNumberFromRecord(record, ["lethalThreshold", "lethalDamage", "lethalAt"])
      ?? lethalThresholdByTarget.get(targetObjectId),
    sourceDamagePool: firstNumberFromRecord(record, ["sourceDamagePool", "damagePool", "assignableDamage", "maxDamage"])
      ?? damagePoolBySource.get(sourceObjectId)
  });
}

function damageChoicesFromLegalTargets(
  legalTargetsBySource: Map<string, string[]>,
  snapshot: SnapshotDto | undefined,
  existingDamageByTarget: Map<string, number>,
  lethalThresholdByTarget: Map<string, number>,
  damagePoolBySource: Map<string, number>
): DamageAssignmentChoice[] {
  return [...legalTargetsBySource.entries()].flatMap(([sourceObjectId, targetObjectIds]) =>
    targetObjectIds.map((targetObjectId) => makeDamageChoice({
      sourceObjectId,
      sourceLabel: objectLabel(snapshot, sourceObjectId),
      targetObjectId,
      targetLabel: objectLabel(snapshot, targetObjectId),
      existingDamage: existingDamageByTarget.get(targetObjectId) ?? findObject(snapshot, targetObjectId)?.damage,
      lethalThreshold: lethalThresholdByTarget.get(targetObjectId),
      sourceDamagePool: damagePoolBySource.get(sourceObjectId)
    }))
  );
}

function damageChoicesFromCandidate(
  candidate: ActionPromptCandidateDto,
  snapshot: SnapshotDto | undefined,
  existingDamageByTarget: Map<string, number>,
  lethalThresholdByTarget: Map<string, number>,
  damagePoolBySource: Map<string, number>
): DamageAssignmentChoice[] {
  const sources = candidate.sources ?? [];
  const targets = candidate.targets ?? [];
  if (sources.length !== 1 || targets.length === 0) {
    return [];
  }

  const source = sources[0];
  return targets.map((target) => makeDamageChoice({
    sourceObjectId: source.id,
    sourceLabel: choiceLabel(source),
    targetObjectId: target.id,
    targetLabel: choiceLabel(target),
    existingDamage: existingDamageByTarget.get(target.id) ?? findObject(snapshot, target.id)?.damage,
    lethalThreshold: lethalThresholdByTarget.get(target.id),
    sourceDamagePool: damagePoolBySource.get(source.id)
  }));
}

function makeDamageChoice(choice: Omit<DamageAssignmentChoice, "key">): DamageAssignmentChoice {
  return {
    ...choice,
    key: `${choice.sourceObjectId}->${choice.targetObjectId}`
  };
}

function uniqueDamageChoices(choices: DamageAssignmentChoice[]): DamageAssignmentChoice[] {
  const seen = new Set<string>();
  const result: DamageAssignmentChoice[] = [];
  for (const choice of choices) {
    if (!seen.has(choice.key)) {
      seen.add(choice.key);
      result.push(choice);
    }
  }
  return result;
}

function clampDamageInput(value: number): number {
  return Number.isFinite(value) && value > 0 ? Math.floor(value) : 0;
}

type TriggerOrderItem = {
  triggerId: string;
  label: string;
  source?: string;
  controller?: string;
  summary?: string;
  constraint?: string;
};

type OrderTriggersModel = {
  constraints: string[];
  triggeredByEventKind?: string;
  triggers: TriggerOrderItem[];
  resetKey: string;
};

function OrderTriggersCandidate({
  canAct,
  candidate,
  disabledByConnection,
  onCommand,
  prompt,
  readOnly = false
}: {
  canAct: boolean;
  candidate?: ActionPromptCandidateDto;
  disabledByConnection: boolean;
  onCommand: (command: GameCommand) => void;
  prompt?: ActionPromptDto;
  readOnly?: boolean;
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
    && !disabledByConnection
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
                disabled={readOnly || !canAct || index === 0}
                onClick={() => setOrderedTriggerIds((current) => moveTriggerId(current, trigger.triggerId, -1))}
                type="button"
              >
                <ArrowUp size={14} />
              </button>
              <button
                aria-label={`${trigger.label} 下移`}
                className="trigger-order-move"
                disabled={readOnly || !canAct || index === orderedTriggers.length - 1}
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
        title={disabledByConnection ? "连接恢复前不能提交触发排序" : promptReasonTitle(candidate?.reason)}
        variant={canSubmit ? "primary" : "ghost"}
      >
        提交触发顺序
      </Button>
    </div>
  );
}

function buildOrderTriggersModel(candidate: ActionPromptCandidateDto | undefined, prompt: ActionPromptDto | undefined): OrderTriggersModel {
  const metadata = {
    ...(prompt?.view?.metadata ?? {}),
    ...(candidate?.metadata ?? {})
  };
  const triggerChoices = choiceArrayMetadata(metadata.triggerChoices);
  const triggerRecords = [
    ...recordArrayMetadata(metadata.triggers),
    ...recordArrayMetadata(metadata.triggerOrdering)
  ];
  const preferredOrder = firstStringArrayMetadata(metadata, ["orderedTriggerIds", "triggerIds"])
    ?? firstStringArrayFromRecord(metadata.triggerOrdering, ["orderedTriggerIds", "triggerIds", "order"])
    ?? stringArrayFromValue(metadata.triggerOrdering, true)
    ?? triggerChoices.map((choice) => choice.id);
  const sourceItems = [
    ...triggerRecords.map(triggerItemFromRecord).filter((item): item is TriggerOrderItem => item != null),
    ...triggerChoices.map(triggerItemFromChoice),
    ...(candidate?.sources ?? []).map(triggerItemFromChoice),
    ...preferredOrder.map((triggerId) => triggerItemFromId(triggerId))
  ];
  const triggers = orderTriggerItems(uniqueTriggerItems(sourceItems), preferredOrder);
  const constraints = constraintSummaries(
    metadata.legalOrderingConstraints
      ?? metadata.orderingConstraints
      ?? metadata.constraints
      ?? (isRecord(metadata.triggerOrdering) ? metadata.triggerOrdering.constraints : undefined)
  );
  const triggeredByEventKind = safeOptionalText(stringMetadata(metadata, "triggeredByEventKind"));

  return {
    constraints,
    triggeredByEventKind,
    triggers,
    resetKey: [
      triggeredByEventKind ?? "none",
      triggers.map((trigger) => trigger.triggerId).join("|"),
      constraints.join("|")
    ].join("::")
  };
}

function triggerItemFromChoice(choice: ActionPromptChoiceDto): TriggerOrderItem {
  return {
    triggerId: choice.id,
    label: choiceLabel(choice),
    summary: safeOptionalText(choice.reason ?? undefined)
  };
}

function triggerItemFromId(triggerId: string): TriggerOrderItem {
  return {
    triggerId,
    label: triggerId
  };
}

function triggerItemFromRecord(record: Record<string, unknown>): TriggerOrderItem | undefined {
  const triggerId = firstStringFromRecord(record, ["triggerId", "id", "choiceId"]);
  if (!triggerId) {
    return undefined;
  }

  return {
    triggerId,
    label: safeOptionalText(firstStringFromRecord(record, ["label", "summary", "visibleText", "text", "title", "name"])) ?? triggerId,
    source: safeOptionalText(firstStringFromRecord(record, ["source", "sourceLabel", "sourceId", "sourceObjectId", "sourceCardNo"])),
    controller: safeOptionalText(firstStringFromRecord(record, ["controller", "controllerId", "playerId"])),
    summary: safeOptionalText(firstStringFromRecord(record, ["summary", "visibleText", "text", "description"])),
    constraint: safePromptSummary(record.legalOrderingConstraint ?? record.orderingConstraint ?? record.constraint ?? record.constraints)
  };
}

function uniqueTriggerItems(items: TriggerOrderItem[]): TriggerOrderItem[] {
  const byId = new Map<string, TriggerOrderItem>();
  for (const item of items) {
    const existing = byId.get(item.triggerId);
    byId.set(item.triggerId, existing ? { ...item, ...existing } : item);
  }
  return [...byId.values()];
}

function orderTriggerItems(items: TriggerOrderItem[], preferredOrder: string[]): TriggerOrderItem[] {
  if (preferredOrder.length === 0) {
    return items;
  }

  const byId = new Map(items.map((item) => [item.triggerId, item]));
  const ordered = preferredOrder
    .map((triggerId) => byId.get(triggerId) ?? triggerItemFromId(triggerId))
    .filter((item, index, array) => array.findIndex((candidate) => candidate.triggerId === item.triggerId) === index);
  const orderedIds = new Set(ordered.map((item) => item.triggerId));
  return [...ordered, ...items.filter((item) => !orderedIds.has(item.triggerId))];
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

function CandidateButton({
  candidate,
  disabledByConnection,
  onCommand,
  onReady,
  onSubmitStarterDeck,
  prompt,
  snapshot
}: {
  candidate: ActionPromptCandidateDto;
  disabledByConnection: boolean;
  onCommand: (command: GameCommand) => void;
  onReady: () => void;
  onSubmitStarterDeck: () => void;
  prompt?: ActionPromptDto;
  snapshot?: SnapshotDto;
}) {
  const [confirmingSurrender, setConfirmingSurrender] = useState(false);
  const command = simpleCommand(candidate, snapshot);
  const directAction = directCandidateAction(candidate, onReady, onSubmitStarterDeck);
  const disabled = disabledByConnection || !candidate.enabled || (!command && !directAction);

  useEffect(() => {
    setConfirmingSurrender(false);
  }, [candidate.action, candidate.enabled, candidate.label, disabledByConnection]);

  if (candidate.action === "SURRENDER" && command) {
    if (confirmingSurrender) {
      return (
        <div className="surrender-confirm">
          <span>对手将获得本局胜利。</span>
          <div className="surrender-confirm-actions">
            <Button
              disabled={disabled}
              icon={<Flag size={16} />}
              onClick={() => onCommand(withPromptStamp(command, prompt))}
              title={disabledByConnection ? "连接恢复前不能提交行动" : promptReasonTitle(candidate.reason)}
              variant="danger"
            >
              确认投降
            </Button>
            <Button icon={<X size={16} />} onClick={() => setConfirmingSurrender(false)} variant="ghost">
              取消
            </Button>
          </div>
        </div>
      );
    }

    return (
      <Button
        disabled={disabled}
        icon={<Flag size={16} />}
        onClick={() => setConfirmingSurrender(true)}
        title={disabledByConnection ? "连接恢复前不能提交行动" : promptReasonTitle(candidate.reason)}
        variant="danger"
      >
        {promptActionLabel(candidate)}
      </Button>
    );
  }

  return (
    <Button
      disabled={disabled}
      icon={candidateIcon(candidate, command || directAction)}
      onClick={() => {
        if (directAction) {
          directAction();
        } else if (command) {
          onCommand(withPromptStamp(command, prompt));
        }
      }}
      title={disabledByConnection ? "连接恢复前不能提交行动" : promptReasonTitle(candidate.reason)}
      variant={candidate.action === "SURRENDER" && candidate.enabled ? "danger" : candidate.enabled ? "primary" : "ghost"}
    >
      {promptActionLabel(candidate)}
      {!command && !directAction && candidate.action !== "WAIT" ? `（需选择）` : ""}
    </Button>
  );
}

function directCandidateAction(candidate: ActionPromptCandidateDto, onReady: () => void, onSubmitStarterDeck: () => void): (() => void) | undefined {
  if (candidate.action === "SUBMIT_DECK") {
    return onSubmitStarterDeck;
  }
  if (candidate.action === "READY") {
    return onReady;
  }
  return undefined;
}

function candidateIcon(candidate: ActionPromptCandidateDto, executable: unknown) {
  if (candidate.action === "SUBMIT_DECK") {
    return <Send size={16} />;
  }
  if (candidate.action === "READY") {
    return <Check size={16} />;
  }
  if (candidate.action === "SURRENDER") {
    return <Flag size={16} />;
  }
  return executable ? <Play size={16} /> : <Hourglass size={16} />;
}

function simpleCommand(candidate: ActionPromptCandidateDto, snapshot?: SnapshotDto): GameCommand | undefined {
  switch (candidate.action) {
    case "PASS_PRIORITY":
      return { cmdType: "PASS_PRIORITY" };
    case "PASS_FOCUS":
      return { cmdType: "PASS_FOCUS" };
    case "PASS":
      return { cmdType: "PASS" };
    case "END_TURN":
      return { cmdType: "END_TURN" };
    case "SURRENDER":
      return { cmdType: "SURRENDER" };
    case "PAY_COST":
      return payCostCommand(candidate);
    case "WAIT":
      return undefined;
    default:
      if (hasSingleChoice(candidate.sources) && candidate.action === "TAP_RUNE") {
        return { cmdType: "TAP_RUNE", sourceObjectId: candidate.sources![0].id };
      }
      if (hasSingleChoice(candidate.sources) && candidate.action === "RECYCLE_RUNE") {
        return { cmdType: "RECYCLE_RUNE", sourceObjectId: candidate.sources![0].id };
      }
      if (hasSingleChoice(candidate.sources) && candidate.action === "PLAY_CARD") {
        const source = candidate.sources![0].id;
        const cardNo = findCardNo(snapshot, source);
        return cardNo && !requiresFurtherChoice(candidate)
          ? { cmdType: "PLAY_CARD", sourceObjectId: source, cardNo, targetObjectIds: [] }
          : undefined;
      }
      return undefined;
  }
}

function payCostCommand(candidate: ActionPromptCandidateDto): GameCommand | undefined {
  const metadata = candidate.metadata ?? {};
  const paymentId = stringMetadata(metadata, "paymentId");
  const paymentWindow = stringMetadata(metadata, "paymentWindow");
  const paymentChoiceIds = stringArrayMetadata(metadata, "paymentChoiceIds");
  if (!paymentId || !paymentWindow || paymentChoiceIds == null) {
    return undefined;
  }

  return { cmdType: "PAY_COST", paymentId, paymentWindow, paymentChoiceIds };
}

function requiresFurtherChoice(candidate: ActionPromptCandidateDto): boolean {
  return Boolean(
    (candidate.targets?.length ?? 0) > 0
    || (candidate.destinations?.length ?? 0) > 0
    || (candidate.modes?.length ?? 0) > 0
    || (candidate.optionalCosts?.length ?? 0) > 0
  );
}

function hasSingleChoice(choices?: Array<{ id: string }> | null): boolean {
  return Array.isArray(choices) && choices.length === 1;
}

function findCardNo(snapshot: SnapshotDto | undefined, objectId: string): string | undefined {
  return findObject(snapshot, objectId)?.cardNo ?? undefined;
}

function findObject(snapshot: SnapshotDto | undefined, objectId: string) {
  if (!snapshot) {
    return undefined;
  }

  for (const player of Object.values(snapshot.players)) {
    const cardObject = player.objects?.[objectId];
    if (cardObject) {
      return cardObject;
    }
  }

  return undefined;
}

function objectLabel(snapshot: SnapshotDto | undefined, objectId: string): string {
  const cardNo = findCardNo(snapshot, objectId);
  return cardNo ? `${cardNo} · ${objectId}` : objectId;
}

function shouldShowGenericPromptDetails(prompt: ActionPromptDto): boolean {
  const type = prompt.view?.type?.trim();
  if (!type) {
    return false;
  }

  return complexPromptTypes.has(type) || !knownPromptTypes.has(type);
}

function choiceLabel(choice: ActionPromptChoiceDto): string {
  return redactInternalText(choice.label || choice.id || "服务端选项");
}

function safePromptValue(key: string, value: unknown): string {
  if (typeof value === "string") {
    return safeStringMetadataKeys.has(key) ? redactInternalText(value) || "文本" : "文本";
  }

  if (typeof value === "number" || typeof value === "boolean") {
    return String(value);
  }

  if (Array.isArray(value)) {
    return `${value.length} 项`;
  }

  if (value && typeof value === "object") {
    return `${Object.keys(value).length} 项`;
  }

  return "无";
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

const knownPromptTypes = new Set<string>([
  "ROOM_SETUP",
  "MULLIGAN",
  "MAIN_ACTION",
  "STACK_PRIORITY",
  "SPELL_DUEL_FOCUS",
  "SPELL_DUEL_ACTION",
  "BATTLE_DECLARATION",
  "ASSIGN_COMBAT_DAMAGE",
  "PAY_COST",
  "ORDER_TRIGGERS",
  "TASK_QUEUE",
  "WAIT",
  "MATCH_RESULT"
]);

const complexPromptTypes = new Set<string>([
  "PAY_COST",
  "ORDER_TRIGGERS",
  "ASSIGN_COMBAT_DAMAGE",
  "SPELL_DUEL_ACTION"
]);

const safeStringMetadataKeys = new Set<string>([
  "action",
  "actionType",
  "kind",
  "phase",
  "promptType",
  "state",
  "status",
  "window"
]);

function numberMetadata(metadata: Record<string, unknown> | null | undefined, key: string): number | undefined {
  const value = metadata?.[key];
  return typeof value === "number" && Number.isFinite(value) ? value : undefined;
}

function firstNumberMetadata(metadata: Record<string, unknown>, keys: string[]): number | undefined {
  for (const key of keys) {
    const value = numberMetadata(metadata, key);
    if (value != null) {
      return value;
    }
  }
  return undefined;
}

function stringMetadata(metadata: Record<string, unknown>, key: string): string | undefined {
  const value = metadata[key];
  return typeof value === "string" && value.trim().length > 0 ? value.trim() : undefined;
}

function stringArrayMetadata(metadata: Record<string, unknown>, key: string): string[] | undefined {
  const value = metadata[key];
  return stringArrayFromValue(value);
}

function firstStringArrayMetadata(metadata: Record<string, unknown>, keys: string[]): string[] | undefined {
  for (const key of keys) {
    const values = stringArrayFromValue(metadata[key], true);
    if (values && values.length > 0) {
      return values;
    }
  }
  return undefined;
}

function firstStringArrayFromRecord(value: unknown, keys: string[]): string[] | undefined {
  if (!isRecord(value)) {
    return undefined;
  }

  return firstStringArrayMetadata(value, keys);
}

function stringArrayFromValue(value: unknown, requireNonEmpty = false): string[] | undefined {
  if (!Array.isArray(value)) {
    return undefined;
  }

  const values = value.map((item) => typeof item === "string" ? item.trim() : "");
  return (!requireNonEmpty || values.length > 0) && values.every((item) => item.length > 0) ? values : undefined;
}

function choiceArrayMetadata(value: unknown): ActionPromptChoiceDto[] {
  const choices: ActionPromptChoiceDto[] = [];
  for (const record of recordArrayMetadata(value)) {
    const id = firstStringFromRecord(record, ["id", "triggerId", "choiceId"]);
    if (id) {
      choices.push({
        id,
        label: firstStringFromRecord(record, ["label", "summary", "visibleText", "text", "title", "name"]) ?? id,
        reason: firstStringFromRecord(record, ["reason", "description"])
      });
    }
  }

  return choices;
}

function recordArrayMetadata(value: unknown): Array<Record<string, unknown>> {
  if (Array.isArray(value)) {
    return value.filter(isRecord);
  }

  if (isRecord(value)) {
    return Object.values(value).filter(isRecord);
  }

  return [];
}

function numberMapMetadata(metadata: Record<string, unknown>, keys: string[]): Map<string, number> {
  for (const key of keys) {
    const value = metadata[key];
    if (isRecord(value)) {
      return new Map(
        Object.entries(value)
          .filter((entry): entry is [string, number] => typeof entry[1] === "number" && Number.isFinite(entry[1]))
      );
    }
  }

  return new Map();
}

function stringListMapMetadata(metadata: Record<string, unknown>, keys: string[]): Map<string, string[]> {
  for (const key of keys) {
    const value = metadata[key];
    if (isRecord(value)) {
      return new Map(
        Object.entries(value)
          .map(([sourceObjectId, targets]) => [
            sourceObjectId,
            Array.isArray(targets)
              ? targets.filter((target): target is string => typeof target === "string" && target.trim().length > 0)
              : []
          ] as const)
          .filter(([, targets]) => targets.length > 0)
      );
    }
  }

  return new Map();
}

function parseAssignmentChoiceId(value: string | undefined): { sourceObjectId: string; targetObjectId: string } | undefined {
  if (!value) {
    return undefined;
  }

  const [sourceObjectId, targetObjectId] = value.split("->").map((part) => part.trim());
  return sourceObjectId && targetObjectId ? { sourceObjectId, targetObjectId } : undefined;
}

function constraintSummaries(value: unknown): string[] {
  if (Array.isArray(value)) {
    return value
      .map(safePromptSummary)
      .filter((item): item is string => item != null)
      .slice(0, 12);
  }

  const summary = safePromptSummary(value);
  return summary ? [summary] : [];
}

function safePromptSummary(value: unknown): string | undefined {
  if (typeof value === "string") {
    return safeOptionalText(value);
  }

  if (typeof value === "number" || typeof value === "boolean") {
    return String(value);
  }

  if (Array.isArray(value)) {
    const stringValues = value
      .filter((item): item is string => typeof item === "string" && item.trim().length > 0)
      .map((item) => safeOptionalText(item))
      .filter((item): item is string => item != null);
    return stringValues.length > 0 ? stringValues.slice(0, 3).join("、") : `${value.length} 项`;
  }

  if (isRecord(value)) {
    const label = firstStringFromRecord(value, ["label", "summary", "visibleText", "text", "description"]);
    return safeOptionalText(label) ?? `${Object.keys(value).length} 项`;
  }

  return undefined;
}

function safeOptionalText(value: string | undefined): string | undefined {
  if (!value?.trim()) {
    return undefined;
  }

  return redactInternalText(value);
}

function firstStringFromRecord(record: Record<string, unknown>, keys: string[]): string | undefined {
  for (const key of keys) {
    const value = record[key];
    if (typeof value === "string" && value.trim().length > 0) {
      return value.trim();
    }
  }
  return undefined;
}

function firstNumberFromRecord(record: Record<string, unknown>, keys: string[]): number | undefined {
  for (const key of keys) {
    const value = record[key];
    if (typeof value === "number" && Number.isFinite(value)) {
      return value;
    }
  }
  return undefined;
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return Boolean(value) && typeof value === "object" && !Array.isArray(value);
}

export function candidateListLabel(prompt?: ActionPromptDto): string {
  const enabledCandidates = (prompt?.candidates ?? []).filter((candidate) => candidate.enabled);
  return enabledCandidates.map(promptActionLabel).join("、") || "无可提交行动";
}

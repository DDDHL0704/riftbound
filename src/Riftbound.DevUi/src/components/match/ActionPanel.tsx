import { Check, Flag, Hourglass, Play, Send, X } from "lucide-react";
import { useEffect, useMemo, useState } from "react";
import { ActionPromptCandidateDto, ActionPromptChoiceDto, ActionPromptDto, ConnectionStatus, GameCommand, SnapshotDto } from "../../types/protocol";
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
  const candidates = (prompt?.candidates ?? []).filter((candidate) => candidate.enabled);
  const connected = connectionStatus === "connected";
  const canAct = connected && prompt?.actionable && prompt.playerId === playerId;
  const promptView = prompt?.view;
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
        {promptView?.relatedStackItemId && <span>关联结算链：{promptView.relatedStackItemId}</span>}
        {!connected && <span>连接状态：{connectionStatusLabel(connectionStatus)}，行动入口已暂停。</span>}
      </div>
      {prompt && shouldShowGenericPromptDetails(prompt) && <GenericPromptDetails prompt={prompt} />}
      <div className="action-buttons">
        {candidates.length === 0 && <span className="empty-hint">服务端暂未提供可提交候选。</span>}
        {candidates.map((candidate) => candidate.action === "MULLIGAN" ? (
          <MulliganCandidate
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

  return (
    <div className="generic-prompt-details">
      <strong>服务端选项</strong>
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
  if (!snapshot) {
    return undefined;
  }

  for (const player of Object.values(snapshot.players)) {
    const cardNo = player.objects?.[objectId]?.cardNo;
    if (cardNo) {
      return cardNo;
    }
  }

  return undefined;
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

export function candidateListLabel(prompt?: ActionPromptDto): string {
  const enabledCandidates = (prompt?.candidates ?? []).filter((candidate) => candidate.enabled);
  return enabledCandidates.map(promptActionLabel).join("、") || "无可提交行动";
}

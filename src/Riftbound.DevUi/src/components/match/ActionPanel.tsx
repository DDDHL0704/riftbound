import { Check, Flag, Hourglass, Play, Send } from "lucide-react";
import { useEffect, useMemo, useState } from "react";
import { ActionPromptCandidateDto, ActionPromptChoiceDto, ActionPromptDto, ConnectionStatus, GameCommand, SnapshotDto } from "../../types/protocol";
import { promptActionLabel } from "../../utils/formatters";
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

  return (
    <section className="side-panel action-panel">
      <header>
        <span className="eyebrow">服务端 Prompt</span>
        <h2>当前行动</h2>
      </header>
      <div className="prompt-summary">
        <StatusPill tone={canAct ? "good" : "neutral"}>{canAct ? "轮到你操作" : "等待服务端或对手"}</StatusPill>
        <span>Prompt：{prompt?.promptId ?? "无"}</span>
        <span>原因：{prompt?.reason ?? "尚未收到 prompt"}</span>
        {!connected && <span>连接状态：{connectionStatusLabel(connectionStatus)}，行动入口已暂停。</span>}
      </div>
      <div className="action-buttons">
        {candidates.length === 0 && <span className="empty-hint">服务端暂未提供可提交候选。</span>}
        {candidates.map((candidate) => candidate.action === "MULLIGAN" ? (
          <MulliganCandidate
            candidate={candidate}
            disabledByConnection={!connected}
            key={`${candidate.action}-${candidate.label}`}
            onCommand={onCommand}
          />
        ) : (
          <CandidateButton
            candidate={candidate}
            key={`${candidate.action}-${candidate.label}`}
            onCommand={onCommand}
            onReady={onReady}
            onSubmitStarterDeck={onSubmitStarterDeck}
            disabledByConnection={!connected}
            snapshot={snapshot}
          />
        ))}
      </div>
    </section>
  );
}

function MulliganCandidate({
  candidate,
  disabledByConnection,
  onCommand
}: {
  candidate: ActionPromptCandidateDto;
  disabledByConnection: boolean;
  onCommand: (command: GameCommand) => void;
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
        onClick={() => onCommand({ cmdType: "MULLIGAN", handObjectIds: selectedObjectIds })}
        title={disabledByConnection ? "连接恢复前不能提交起手调整" : candidate.reason}
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
      title={choice.reason ?? choice.id}
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
  snapshot
}: {
  candidate: ActionPromptCandidateDto;
  disabledByConnection: boolean;
  onCommand: (command: GameCommand) => void;
  onReady: () => void;
  onSubmitStarterDeck: () => void;
  snapshot?: SnapshotDto;
}) {
  const command = simpleCommand(candidate, snapshot);
  const directAction = directCandidateAction(candidate, onReady, onSubmitStarterDeck);
  const disabled = disabledByConnection || !candidate.enabled || (!command && !directAction);
  return (
    <Button
      disabled={disabled}
      icon={candidateIcon(candidate, command || directAction)}
      onClick={() => {
        if (directAction) {
          directAction();
        } else if (command) {
          if (candidate.action === "SURRENDER" && !window.confirm("确认投降？对手将获得本局胜利。")) {
            return;
          }
          onCommand(command);
        }
      }}
      title={disabledByConnection ? "连接恢复前不能提交行动" : candidate.reason}
      variant={candidate.action === "SURRENDER" && candidate.enabled ? "danger" : candidate.enabled ? "primary" : "ghost"}
    >
      {promptActionLabel(candidate)}
      {!command && !directAction && candidate.action !== "WAIT" ? `（需选择）` : ""}
    </Button>
  );
}

function connectionStatusLabel(status: ConnectionStatus): string {
  switch (status) {
    case "idle":
      return "未连接";
    case "connecting":
      return "连接中";
    case "connected":
      return "已连接";
    case "reconnecting":
      return "重连中";
    case "resyncing":
      return "重新同步中";
    case "disconnected":
      return "已断开";
    case "error":
      return "连接错误";
  }
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

function numberMetadata(metadata: Record<string, unknown> | null | undefined, key: string): number | undefined {
  const value = metadata?.[key];
  return typeof value === "number" && Number.isFinite(value) ? value : undefined;
}

export function candidateListLabel(prompt?: ActionPromptDto): string {
  const enabledCandidates = (prompt?.candidates ?? []).filter((candidate) => candidate.enabled);
  return enabledCandidates.map(promptActionLabel).join("、") || "无可提交行动";
}

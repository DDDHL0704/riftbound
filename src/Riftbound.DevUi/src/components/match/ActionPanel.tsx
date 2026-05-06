import { Check, Hourglass, Play, Send } from "lucide-react";
import { ActionPromptCandidateDto, ActionPromptDto, GameCommand, SnapshotDto } from "../../types/protocol";
import { actionLabel, promptActionLabel } from "../../utils/formatters";
import { Button } from "../ui/Button";
import { StatusPill } from "../ui/StatusPill";

type ActionPanelProps = {
  prompt?: ActionPromptDto;
  snapshot?: SnapshotDto;
  playerId: string;
  onReady: () => void;
  onSubmitStarterDeck: () => void;
  onCommand: (command: GameCommand) => void;
};

export function ActionPanel({ prompt, snapshot, playerId, onReady, onSubmitStarterDeck, onCommand }: ActionPanelProps) {
  const candidates = prompt?.candidates ?? [];
  const canAct = prompt?.actionable && prompt.playerId === playerId;

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
      </div>
      <div className="action-buttons">
        {candidates.length === 0 && <span className="empty-hint">服务端暂未提供可执行候选。</span>}
        {candidates.map((candidate) => (
          <CandidateButton
            candidate={candidate}
            key={`${candidate.action}-${candidate.label}`}
            onCommand={onCommand}
            onReady={onReady}
            onSubmitStarterDeck={onSubmitStarterDeck}
            snapshot={snapshot}
          />
        ))}
      </div>
    </section>
  );
}

function CandidateButton({
  candidate,
  onCommand,
  onReady,
  onSubmitStarterDeck,
  snapshot
}: {
  candidate: ActionPromptCandidateDto;
  onCommand: (command: GameCommand) => void;
  onReady: () => void;
  onSubmitStarterDeck: () => void;
  snapshot?: SnapshotDto;
}) {
  const command = simpleCommand(candidate, snapshot);
  const directAction = directCandidateAction(candidate, onReady, onSubmitStarterDeck);
  const disabled = !candidate.enabled || (!command && !directAction);
  return (
    <Button
      disabled={disabled}
      icon={candidateIcon(candidate, command || directAction)}
      onClick={() => {
        if (directAction) {
          directAction();
        } else if (command) {
          onCommand(command);
        }
      }}
      title={candidate.reason}
      variant={candidate.enabled ? "primary" : "ghost"}
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
    case "MULLIGAN":
      return { cmdType: "MULLIGAN", handObjectIds: [] };
    case "WAIT":
      return undefined;
    default:
      if (hasSingleChoice(candidate.sources) && candidate.action === "PLAY_CARD") {
        const source = candidate.sources![0].id;
        const cardNo = findCardNo(snapshot, source);
        return cardNo ? { cmdType: "PLAY_CARD", sourceObjectId: source, cardNo, targetObjectIds: [] } : undefined;
      }
      return undefined;
  }
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

export function candidateListLabel(prompt?: ActionPromptDto): string {
  return (prompt?.actions ?? []).map(actionLabel).join("、") || "无";
}

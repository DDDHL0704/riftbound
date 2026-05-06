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
        <Button icon={<Send size={16} />} onClick={onSubmitStarterDeck} variant="secondary">提交测试卡组</Button>
        <Button icon={<Check size={16} />} onClick={onReady} variant="secondary">准备</Button>
        {candidates.length === 0 && <span className="empty-hint">服务端暂未提供可执行候选。</span>}
        {candidates.map((candidate) => (
          <CandidateButton
            candidate={candidate}
            key={`${candidate.action}-${candidate.label}`}
            onCommand={onCommand}
            snapshot={snapshot}
          />
        ))}
      </div>
    </section>
  );
}

function CandidateButton({ candidate, onCommand, snapshot }: { candidate: ActionPromptCandidateDto; onCommand: (command: GameCommand) => void; snapshot?: SnapshotDto }) {
  const command = simpleCommand(candidate, snapshot);
  const disabled = !candidate.enabled || !command;
  return (
    <Button
      disabled={disabled}
      icon={command ? <Play size={16} /> : <Hourglass size={16} />}
      onClick={() => command && onCommand(command)}
      title={candidate.reason}
      variant={candidate.enabled ? "primary" : "ghost"}
    >
      {promptActionLabel(candidate)}
      {!command && candidate.action !== "WAIT" ? `（需选择）` : ""}
    </Button>
  );
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

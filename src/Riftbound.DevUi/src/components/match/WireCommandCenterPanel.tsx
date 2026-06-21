import type { ActionPromptDto, ConnectionStatus, GameCommand, SnapshotDto } from "../../types/protocol";
import type { CandidateSelectionDraft } from "../../utils/candidateSelectionDraft";
import {
  buildCommandSubmissionFollowupPlan,
  type CommandSubmissionFollowupFeedback,
  type ObservedGameEvent
} from "../../utils/commandSubmissionFollowupPlan";
import type { ServerSubmissionGatePlan } from "../../utils/serverSubmissionGatePlan";
import type { TableObjectContext } from "../../utils/tableObjectContext";
import {
  buildWireCommandCenterPlan,
  type WireCommandCenterActionRow,
  type WireCommandCenterPlan,
  type WireCommandCenterRow
} from "../../utils/wireCommandCenterPlan";
import { buildWireResponseCoachPlan } from "../../utils/wireResponseCoachPlan";
import type { WireFocusedInteractionPlan } from "../../utils/wireFocusedInteractionPlan";
import { Button } from "../ui/Button";
import { StatusPill } from "../ui/StatusPill";
import { WireFocusedActionEntryList } from "./WireFocusedActionEntryList";

export function WireCommandCenterPanel({
  connectionStatus,
  disabledByConnection,
  events,
  focusedPlan,
  objectContext,
  onClearFocus,
  onCommand,
  playerId,
  prompt,
  selectionDraft,
  snapshot,
  submissionFeedback,
  submissionGate
}: {
  connectionStatus: ConnectionStatus;
  disabledByConnection: boolean;
  events?: readonly ObservedGameEvent[];
  focusedPlan: WireFocusedInteractionPlan;
  objectContext?: TableObjectContext;
  onClearFocus: () => void;
  onCommand?: (command: GameCommand) => void;
  playerId: string;
  prompt?: ActionPromptDto;
  selectionDraft?: CandidateSelectionDraft;
  snapshot?: SnapshotDto;
  submissionFeedback?: CommandSubmissionFollowupFeedback;
  submissionGate?: ServerSubmissionGatePlan;
}) {
  const coachPlan = buildWireResponseCoachPlan({
    connectionStatus,
    playerId,
    prompt,
    selectionDraft,
    snapshot,
    submissionGate
  });
  const plan = buildWireCommandCenterPlan({
    coachPlan,
    focusedPlan,
    objectContext,
    submissionFollowup: buildCommandSubmissionFollowupPlan({
      events,
      feedback: submissionFeedback,
      snapshot
    })
  });

  return (
    <section
      aria-label="当前行动指挥中心"
      className="wire-command-center"
      data-wire-command-center-state={plan.state}
      data-wire-command-center-step-role={plan.stepRole}
    >
      <header className="wire-command-center-header">
        <div>
          <strong>指挥中心</strong>
          <span>{plan.headline}</span>
        </div>
        <StatusPill tone={plan.tone}>{plan.stateLabel}</StatusPill>
      </header>

      <div className="wire-command-center-next" data-wire-command-center-next-step={plan.stepRole}>
        <small>下一步</small>
        <strong>{plan.nextStepLabel}</strong>
        <span>{plan.reason}</span>
      </div>

      <CommandCenterRows plan={plan} />
      <CommandCenterActionRows rows={plan.actionRows} />

      {plan.canShowFocusedActions ? (
        <WireFocusedActionEntryList
          className="wire-command-center-actions"
          dataAttributes={{
            count: "data-wire-command-center-action-count",
            entry: "data-wire-command-center-action",
            mode: "data-wire-command-center-action-mode"
          }}
          disabledByConnection={disabledByConnection}
          entryClassName="wire-command-center-action"
          maxEntries={2}
          onCommand={onCommand}
          plan={focusedPlan}
          prompt={prompt}
          snapshot={snapshot}
          submissionGate={submissionGate}
        />
      ) : (
        <span className="wire-command-center-empty">等待服务端候选或桌面焦点。</span>
      )}

      {focusedPlan.sourceObjectId && (
        <div className="wire-command-center-buttons">
          <Button onClick={onClearFocus} variant="ghost">清除焦点</Button>
        </div>
      )}
    </section>
  );
}

function CommandCenterRows({ plan }: { plan: WireCommandCenterPlan }) {
  return (
    <dl className="wire-command-center-rows" aria-label="当前行动状态行">
      {plan.rows.map((row) => (
        <CommandCenterRow key={row.key} row={row} />
      ))}
    </dl>
  );
}

function CommandCenterRow({ row }: { row: WireCommandCenterRow }) {
  return (
    <div data-wire-command-center-row={row.key} data-wire-command-center-row-state={row.state}>
      <dt>{row.label}</dt>
      <dd>
        <strong>{row.value}</strong>
        <span>{row.detail}</span>
      </dd>
    </div>
  );
}

function CommandCenterActionRows({ rows }: { rows: WireCommandCenterActionRow[] }) {
  if (rows.length === 0) {
    return null;
  }

  return (
    <ol className="wire-command-center-candidates" aria-label="焦点合法行动摘要">
      {rows.map((row) => (
        <li
          data-wire-command-center-candidate={row.action}
          data-wire-command-center-candidate-state={row.state}
          key={row.key}
        >
          <span>{row.label}</span>
          <strong>{row.stateLabel}</strong>
          <small>{row.commandType ?? "无命令"} / {row.roleLabel}</small>
          <em>{row.nextStepLabel}</em>
        </li>
      ))}
    </ol>
  );
}

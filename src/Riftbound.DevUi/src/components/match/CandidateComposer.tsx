import { Send } from "lucide-react";
import { useEffect, useMemo, useState } from "react";
import type {
  ActionPromptCandidateDto,
  ActionPromptDto,
  GameCommand,
  SnapshotDto
} from "../../types/protocol";
import { promptStampedCommand } from "../../utils/actionPromptCandidates";
import type { CandidateSelectionDraft } from "../../utils/candidateSelectionDraft";
import type { ServerSubmissionGatePlan } from "../../utils/serverSubmissionGatePlan";
import {
  buildCandidateComposerModel,
  buildCandidateComposerSubmissionPlan,
  buildCandidateCommandPreviewPlan,
  choiceLabel,
  composerControls,
  initialComposerState,
  selectedRequirement,
  uniqueStrings,
  type CandidateComposerControls,
  type CandidateComposerState
} from "../../utils/candidateComposerModel";
import { canComposeActionCandidate } from "../../utils/candidateComposerSupport";
import { promptActionLabel, promptReasonTitle } from "../../utils/formatters";
import { Button } from "../ui/Button";
import { StatusPill } from "../ui/StatusPill";

type CandidateComposerProps = {
  candidate: ActionPromptCandidateDto;
  disabledByConnection: boolean;
  onCommand: (command: GameCommand) => void;
  onSubmitted?: () => void;
  prompt?: ActionPromptDto;
  snapshot?: SnapshotDto;
  submissionGate?: ServerSubmissionGatePlan;
  forcedSourceObjectId?: string;
  selectionDraft?: CandidateSelectionDraft;
};

export function canComposeCandidate(candidate: ActionPromptCandidateDto): boolean {
  return canComposeActionCandidate(candidate);
}

export function CandidateComposer({
  candidate,
  disabledByConnection,
  forcedSourceObjectId,
  onCommand,
  onSubmitted,
  prompt,
  selectionDraft,
  snapshot,
  submissionGate
}: CandidateComposerProps) {
  const model = useMemo(() => buildCandidateComposerModel(candidate), [candidate]);
  const [state, setState] = useState<CandidateComposerState>(() => initialComposerState(candidate, model, forcedSourceObjectId, selectionDraft));

  useEffect(() => {
    setState(initialComposerState(candidate, model, forcedSourceObjectId, selectionDraft));
  }, [candidate.action, candidate.label, forcedSourceObjectId, model.resetKey, candidate, selectionDraft]);

  const requirement = selectedRequirement(model, state.sourceId);
  const controls = composerControls(candidate, model, requirement, forcedSourceObjectId);
  const submission = buildCandidateComposerSubmissionPlan({
    candidate,
    controls,
    disabledByConnection,
    requirement,
    snapshot,
    submissionGate,
    state
  });
  const gateDisabled = disabledByConnection || !submission.gateCanSubmit;

  return (
    <div
      className="candidate-composer"
      data-candidate-composer-can-submit={submission.canSubmit ? "true" : "false"}
      data-candidate-composer-gate-state={submission.gateStateLabel}
    >
      <div className="candidate-composer-heading">
        <strong>{promptActionLabel(candidate)}</strong>
        <StatusPill tone={submission.canSubmit ? "warn" : "neutral"}>{submission.stateLabel}</StatusPill>
      </div>
      <p className="candidate-composer-note">
        仅使用服务端候选组装命令；费用、目标和结果仍由服务端按规则校验。
      </p>
      <span className="candidate-composer-gate" data-candidate-composer-gate-reason>
        提交门禁：{submission.gateStateLabel} / {submission.gateReason}
      </span>
      {submission.blockReason && <span className="candidate-composer-warning" data-candidate-composer-block-reason>{submission.blockReason}</span>}
      {controls.sources.length > 0 && (
        <label className="candidate-composer-field">
          <span>来源</span>
          <select
            disabled={gateDisabled || !candidate.enabled || Boolean(forcedSourceObjectId)}
            onChange={(event) => setState(initialComposerState(candidate, model, event.currentTarget.value))}
            value={state.sourceId ?? ""}
          >
            {controls.sources.map((choice) => (
              <option key={choice.id} value={choice.id}>{choiceLabel(choice)}</option>
            ))}
          </select>
        </label>
      )}
      {controls.modeChoices.length > 0 && (
        <label className="candidate-composer-field">
          <span>模式</span>
          <select
            disabled={gateDisabled || !candidate.enabled}
            onChange={(event) => setState((current) => ({ ...current, mode: event.currentTarget.value || undefined }))}
            value={state.mode ?? ""}
          >
            {controls.modeChoices.map((choice) => (
              <option key={choice.id} value={choice.id}>{choiceLabel(choice)}</option>
            ))}
          </select>
        </label>
      )}
      {controls.destinationChoices.length > 0 && (
        <label className="candidate-composer-field">
          <span>位置</span>
          <select
            disabled={gateDisabled || !candidate.enabled}
            onChange={(event) => setState((current) => ({ ...current, destinationId: event.currentTarget.value || undefined }))}
            value={state.destinationId ?? ""}
          >
            {!controls.destinationRequired && <option value="">不选择</option>}
            {controls.destinationChoices.map((choice) => (
              <option key={choice.id} value={choice.id}>{choiceLabel(choice)}</option>
            ))}
          </select>
        </label>
      )}
      {controls.targetGroups.map((group) => (
        <label className="candidate-composer-field" key={group.key}>
          <span>{group.label}</span>
          <select
            disabled={gateDisabled || !candidate.enabled}
            onChange={(event) => setState((current) => ({
              ...current,
              targetIdsByGroup: {
                ...current.targetIdsByGroup,
                [group.key]: event.currentTarget.value
              }
            }))}
            value={state.targetIdsByGroup[group.key] ?? ""}
          >
            {!group.required && <option value="">不选择</option>}
            {group.choices.map((choice) => (
              <option key={choice.id} value={choice.id}>{choiceLabel(choice)}</option>
            ))}
          </select>
        </label>
      ))}
      {controls.optionalCostChoices.length > 0 && (
        <div className="candidate-composer-costs">
          <strong>费用 / 追加选项</strong>
          {controls.optionalCostChoices.map((choice) => {
            const locked = controls.requiredOptionalCostIds.includes(choice.id);
            const checked = locked || state.optionalCostIds.includes(choice.id);
            return (
              <label className="candidate-composer-check" key={choice.id}>
                <input
                  checked={checked}
                  disabled={gateDisabled || !candidate.enabled || locked}
                  onChange={(event) => setState((current) => ({
                    ...current,
                    optionalCostIds: event.currentTarget.checked
                      ? uniqueStrings([...current.optionalCostIds, choice.id])
                      : current.optionalCostIds.filter((id) => id !== choice.id)
                  }))}
                  type="checkbox"
                />
                <span>{choiceLabel(choice)}{locked ? "（必需）" : ""}</span>
              </label>
            );
          })}
        </div>
      )}
      <CandidateCommandPreview
        canSubmit={submission.canSubmit}
        statusLabel={submission.stateLabel}
        controls={controls}
        state={state}
      />
      <Button
        disabled={!submission.canSubmit}
        icon={<Send size={16} />}
        onClick={() => {
          if (!submission.command) {
            return;
          }

          onCommand(promptStampedCommand(submission.command, prompt));
          onSubmitted?.();
        }}
        title={submission.blockReason ?? promptReasonTitle(candidate.reason)}
        variant={submission.canSubmit ? "primary" : "ghost"}
      >
        提交服务端候选
      </Button>
    </div>
  );
}

function CandidateCommandPreview({
  canSubmit,
  controls,
  statusLabel,
  state
}: {
  canSubmit: boolean;
  controls: CandidateComposerControls;
  statusLabel: string;
  state: CandidateComposerState;
}) {
  const plan = buildCandidateCommandPreviewPlan(controls, state);

  return (
    <div className="candidate-command-preview" role="group" aria-label="候选提交摘要">
      <div>
        <strong>提交摘要</strong>
        <StatusPill tone={canSubmit ? "warn" : "neutral"}>{canSubmit ? "可送服务端" : statusLabel}</StatusPill>
      </div>
      <span>来源：{plan.sourceLabel}</span>
      {plan.modeLabel && <span>模式：{plan.modeLabel}</span>}
      {plan.destinationLabel && <span>位置：{plan.destinationLabel}</span>}
      <span>目标：{plan.targetLabels.length > 0 ? plan.targetLabels.join("、") : "无"}</span>
      <span>费用：{plan.costLabels.length > 0 ? plan.costLabels.join("、") : "无"}</span>
    </div>
  );
}

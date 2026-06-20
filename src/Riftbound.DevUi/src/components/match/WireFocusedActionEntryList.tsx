import { Play } from "lucide-react";
import type { ActionPromptDto, GameCommand, SnapshotDto } from "../../types/protocol";
import { promptStampedCommand } from "../../utils/actionPromptCandidates";
import type { ServerSubmissionGatePlan } from "../../utils/serverSubmissionGatePlan";
import type { WireFocusedInteractionPlan } from "../../utils/wireFocusedInteractionPlan";
import { Button } from "../ui/Button";
import { CandidateComposer } from "./CandidateComposer";

type DataAttributeName = `data-${string}`;

type WireFocusedActionEntryDataAttributes = {
  count?: DataAttributeName;
  entry?: DataAttributeName;
  mode?: DataAttributeName;
};

type WireFocusedActionEntryListProps = {
  className?: string;
  dataAttributes?: WireFocusedActionEntryDataAttributes;
  disabledByConnection: boolean;
  entryClassName?: string;
  maxEntries?: number;
  onCommand?: (command: GameCommand) => void;
  onSubmitted?: () => void;
  plan: WireFocusedInteractionPlan;
  prompt?: ActionPromptDto;
  snapshot?: SnapshotDto;
  submissionGate?: ServerSubmissionGatePlan;
};

export function WireFocusedActionEntryList({
  className,
  dataAttributes,
  disabledByConnection,
  entryClassName,
  maxEntries,
  onCommand,
  onSubmitted,
  plan,
  prompt,
  snapshot,
  submissionGate
}: WireFocusedActionEntryListProps) {
  const entries = typeof maxEntries === "number"
    ? plan.actionEntries.slice(0, maxEntries)
    : plan.actionEntries;

  if (entries.length === 0) {
    return null;
  }

  const countDataAttributes = dataAttributes?.count
    ? { [dataAttributes.count]: entries.length }
    : {};

  return (
    <div
      className={joinClasses("wire-focused-action-entry-list", className)}
      data-wire-focused-action-entry-count={entries.length}
      {...countDataAttributes}
    >
      {entries.map(({ actionGateReason, actionGateStateLabel, actionPlan, candidate, candidateDraft, disabledByActionGate, key, mode }) => {
        const entryDataAttributes = {
          ...(dataAttributes?.entry ? { [dataAttributes.entry]: key } : {}),
          ...(dataAttributes?.mode ? { [dataAttributes.mode]: mode } : {})
        };

        return (
          <div
            className={joinClasses("wire-focused-action-entry", entryClassName)}
            data-wire-focused-action-entry={key}
            data-wire-focused-action-mode={mode}
            key={key}
            {...entryDataAttributes}
          >
            {mode === "composer" && onCommand ? (
              <CandidateComposer
                actionGateReason={actionGateReason}
                actionGateStateLabel={actionGateStateLabel}
                candidate={candidate}
                disabledByActionGate={disabledByActionGate}
                disabledByConnection={disabledByConnection}
                forcedSourceObjectId={plan.sourceObjectId}
                onCommand={onCommand}
                onSubmitted={onSubmitted}
                prompt={prompt}
                selectionDraft={candidateDraft}
                snapshot={snapshot}
                submissionGate={submissionGate}
              />
            ) : (
              <Button
                disabled={actionPlan.disabled}
                icon={<Play size={16} />}
                onClick={() => {
                  if (actionPlan.command && onCommand) {
                    onCommand(promptStampedCommand(actionPlan.command, prompt));
                    onSubmitted?.();
                  }
                }}
                title={actionPlan.title}
                variant={actionPlan.variant}
              >
                {actionPlan.label}
                {actionPlan.labelSuffix}
              </Button>
            )}
          </div>
        );
      })}
    </div>
  );
}

function joinClasses(...classes: Array<string | undefined>): string {
  return classes.filter(Boolean).join(" ");
}

import type { ReactNode } from "react";
import type { ActionPromptCandidateDto, ActionPromptDto, SnapshotDto } from "../../types/protocol";
import { buildActionPanelBattleDeclarationPlan } from "../../utils/actionPanelBattleDeclarationPlan";
import { buildActionPanelMovementPlan } from "../../utils/actionPanelMovementPlan";
import { buildActionPanelPassPlan } from "../../utils/actionPanelPassPlan";
import type { ActionPanelSubmitGate } from "../../utils/actionPanelRenderPlan";
import { buildActionPanelResponsePlan } from "../../utils/actionPanelResponsePlan";
import { buildActionPanelResourcePlan } from "../../utils/actionPanelResourcePlan";
import { promptActionLabel, promptReasonLabel } from "../../utils/formatters";
import { StatusPill } from "../ui/StatusPill";

type CandidatePreviewProps = {
  candidate: ActionPromptCandidateDto;
  children: ReactNode;
  submitGate: ActionPanelSubmitGate;
};

type PromptCandidatePreviewProps = CandidatePreviewProps & {
  prompt?: ActionPromptDto;
  snapshot?: SnapshotDto;
};

export function BattleDeclarationCandidatePreview({ candidate, children, submitGate }: CandidatePreviewProps) {
  const plan = buildActionPanelBattleDeclarationPlan(candidate);

  return (
    <div
      className="battle-declaration-panel"
      data-battle-declaration-battlefield-count={plan.battlefieldChoiceCount}
      data-battle-declaration-cost-count={plan.optionalCostChoiceCount}
      data-battle-declaration-defender-count={plan.defenderChoiceCount}
      data-battle-declaration-payment-resource-count={plan.paymentResourceChoiceCount}
      data-battle-declaration-requirement-count={plan.requirementCount}
      data-battle-declaration-source-count={plan.sourceChoiceCount}
      data-battle-declaration-state={plan.state}
      data-battle-declaration-template-field-count={plan.commandFieldCount}
    >
      <div className="battle-declaration-heading">
        <strong>{promptActionLabel(candidate)}</strong>
        <StatusPill tone={submitGate.canSubmit ? "good" : "neutral"}>{submitGate.canSubmit ? plan.statusLabel : submitGate.stateLabel}</StatusPill>
      </div>
      <dl className="battle-declaration-summary">
        {plan.metricRows.map((metric) => (
          <div data-battle-declaration-metric={metric.key} key={metric.key}>
            <dt>{metric.label}</dt>
            <dd>{metric.value}</dd>
            <small>{metric.detail}</small>
          </div>
        ))}
      </dl>
      <p className="battle-declaration-note">
        {plan.authorityLabel} {promptReasonLabel(candidate.reason, "服务端声明战斗候选")}
      </p>
      {children}
    </div>
  );
}

export function UnitMovementCandidatePreview({ candidate, children, submitGate }: CandidatePreviewProps) {
  const plan = buildActionPanelMovementPlan(candidate);

  return (
    <div
      className="unit-movement-panel"
      data-unit-movement-cost-count={plan.optionalCostChoiceCount}
      data-unit-movement-destination-count={plan.destinationChoiceCount}
      data-unit-movement-origin-count={plan.originCount}
      data-unit-movement-requirement-count={plan.requirementCount}
      data-unit-movement-source-count={plan.sourceChoiceCount}
      data-unit-movement-state={plan.state}
      data-unit-movement-template-field-count={plan.commandFieldCount}
    >
      <div className="unit-movement-heading">
        <strong>{promptActionLabel(candidate)}</strong>
        <StatusPill tone={submitGate.canSubmit ? "good" : "neutral"}>{submitGate.canSubmit ? plan.statusLabel : submitGate.stateLabel}</StatusPill>
      </div>
      <dl className="unit-movement-summary">
        {plan.metricRows.map((metric) => (
          <div data-unit-movement-metric={metric.key} key={metric.key}>
            <dt>{metric.label}</dt>
            <dd>{metric.value}</dd>
            <small>{metric.detail}</small>
          </div>
        ))}
      </dl>
      <p className="unit-movement-note">
        {plan.authorityLabel} {promptReasonLabel(candidate.reason, "服务端移动候选")}
      </p>
      {children}
    </div>
  );
}

export function RuneResourceCandidatePreview({
  candidate,
  children,
  prompt,
  snapshot,
  submitGate
}: PromptCandidatePreviewProps) {
  const plan = buildActionPanelResourcePlan(candidate, { playerId: prompt?.playerId, snapshot });

  return (
    <div
      className="rune-resource-panel"
      data-rune-resource-command-field-count={plan.commandFieldCount}
      data-rune-resource-power-trait-count={plan.powerTraitCount}
      data-rune-resource-selection-step-count={plan.selectionStepCount}
      data-rune-resource-source-count={plan.sourceChoiceCount}
      data-rune-resource-state={plan.state}
    >
      <div className="rune-resource-heading">
        <strong>{promptActionLabel(candidate)}</strong>
        <StatusPill tone={submitGate.canSubmit ? "good" : "neutral"}>{submitGate.canSubmit ? plan.statusLabel : submitGate.stateLabel}</StatusPill>
      </div>
      <dl className="rune-resource-summary">
        {plan.metricRows.map((metric) => (
          <div data-rune-resource-metric={metric.key} key={metric.key}>
            <dt>{metric.label}</dt>
            <dd>{metric.value}</dd>
            <small>{metric.detail}</small>
          </div>
        ))}
      </dl>
      <p className="rune-resource-note">
        {plan.authorityLabel} {promptReasonLabel(candidate.reason, "服务端符文资源候选")}
      </p>
      <small className="rune-resource-pool">{plan.poolLabel}</small>
      {children}
    </div>
  );
}

export function WindowPassCandidatePreview({
  candidate,
  children,
  prompt,
  snapshot,
  submitGate
}: PromptCandidatePreviewProps) {
  const plan = buildActionPanelPassPlan(candidate, { prompt, snapshot });

  return (
    <div
      className="window-pass-panel"
      data-window-pass-command-field-count={plan.commandFieldCount}
      data-window-pass-mode={plan.mode}
      data-window-pass-passed-count={plan.passedCount}
      data-window-pass-stack-count={plan.stackCount}
      data-window-pass-state={plan.state}
    >
      <div className="window-pass-heading">
        <strong>{promptActionLabel(candidate)}</strong>
        <StatusPill tone={submitGate.canSubmit ? "good" : "neutral"}>{submitGate.canSubmit ? plan.statusLabel : submitGate.stateLabel}</StatusPill>
      </div>
      <dl className="window-pass-summary">
        {plan.metricRows.map((metric) => (
          <div data-window-pass-metric={metric.key} key={metric.key}>
            <dt>{metric.label}</dt>
            <dd>{metric.value}</dd>
            <small>{metric.detail}</small>
          </div>
        ))}
      </dl>
      <p className="window-pass-note">
        {plan.authorityLabel} {promptReasonLabel(candidate.reason, "服务端让过候选")}
      </p>
      {children}
    </div>
  );
}

export function ResponseWindowCandidatePreview({
  candidate,
  children,
  prompt,
  snapshot,
  submitGate
}: PromptCandidatePreviewProps) {
  const plan = buildActionPanelResponsePlan(candidate, { prompt, snapshot });

  return (
    <div
      className="response-window-panel"
      data-response-window-command-field-count={plan.commandFieldCount}
      data-response-window-mode={plan.mode}
      data-response-window-selection-step-count={plan.selectionStepCount}
      data-response-window-stack-count={plan.stackCount}
      data-response-window-state={plan.state}
    >
      <div className="response-window-heading">
        <strong>{promptActionLabel(candidate)}</strong>
        <StatusPill tone={submitGate.canSubmit ? "good" : "neutral"}>{submitGate.canSubmit ? plan.statusLabel : submitGate.stateLabel}</StatusPill>
      </div>
      <dl className="response-window-summary">
        {plan.metricRows.map((metric) => (
          <div data-response-window-metric={metric.key} key={metric.key}>
            <dt>{metric.label}</dt>
            <dd>{metric.value}</dd>
            <small>{metric.detail}</small>
          </div>
        ))}
      </dl>
      <p className="response-window-note">
        {plan.authorityLabel} {promptReasonLabel(candidate.reason, "服务端响应候选")}
      </p>
      {children}
    </div>
  );
}

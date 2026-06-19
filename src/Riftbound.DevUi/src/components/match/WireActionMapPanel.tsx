import type { ActionPromptDto, SnapshotDto } from "../../types/protocol";
import type { CandidateSelectionDraft } from "../../utils/candidateSelectionDraft";
import {
  buildWireActionMapPlan,
  type WireActionContractPlan,
  type WireActionGrammarCandidatePlan,
  type WireActionMapMetric,
  type WireActionMapPlan,
  type WireActionRoutePlan
} from "../../utils/wireActionMapPlan";
import { StatusPill } from "../ui/StatusPill";

type WireActionMapPanelProps = {
  onChooseObject?: (objectId: string) => void;
  onInspectObject?: (objectId: string) => void;
  playerId: string;
  prompt?: ActionPromptDto;
  selectedObjectId?: string;
  selectionDraft?: CandidateSelectionDraft;
  snapshot?: SnapshotDto;
};

export function WireActionMapPanel({
  onChooseObject,
  onInspectObject,
  playerId,
  prompt,
  selectedObjectId,
  selectionDraft,
  snapshot
}: WireActionMapPanelProps) {
  const plan = buildWireActionMapPlan({ playerId, prompt, selectedObjectId, selectionDraft, snapshot });

  return (
    <section className="wire-action-map" aria-label="服务端合法操作地图">
      <header className="wire-action-map-header">
        <div>
          <strong>合法操作地图</strong>
          <span>只投影服务端候选，不在前端推断规则。</span>
        </div>
        <StatusPill tone={plan.canAct ? "good" : "neutral"}>{plan.canAct ? "可操作窗口" : "只读窗口"}</StatusPill>
      </header>

      <div className="wire-action-map-metrics">
        {plan.metrics.map((metric) => <Metric key={metric.key} metric={metric} />)}
      </div>

      {plan.contract && <PromptContractStrip contract={plan.contract} />}
      <CurrentRouteStrip route={plan.route} />

      <div aria-label="服务端可操作对象入口" className="wire-action-entry-strip" role="group" tabIndex={0}>
        {plan.objectEntries.length === 0 && <span className="empty-hint">当前没有服务端标记为可操作的场上对象。</span>}
        {plan.objectEntries.map((entry) => (
          <button
            className="wire-action-object-chip"
            data-action-object-id={entry.objectId}
            data-action-object-state="enabled"
            data-selected={entry.selected ? "true" : "false"}
            key={entry.objectId}
            onClick={() => onInspectObject?.(entry.objectId)}
            type="button"
          >
            <strong>{entry.label}</strong>
            <small>{entry.enabledCandidateCount} 项</small>
          </button>
        ))}
        {plan.objectEntryOverflowCount > 0 && <span className="wire-action-object-chip">等 {plan.objectEntryOverflowCount} 个对象</span>}
        {plan.blockedObjectEntries.map((entry) => (
          <button
            className="wire-action-object-chip"
            data-action-object-id={entry.objectId}
            data-action-object-state="blocked"
            data-selected={entry.selected ? "true" : "false"}
            key={`blocked:${entry.objectId}`}
            onClick={() => onInspectObject?.(entry.objectId)}
            type="button"
          >
            <strong>{entry.label}</strong>
            <small>{entry.disabledCandidateCount} 阻断</small>
          </button>
        ))}
        {plan.blockedObjectEntryOverflowCount > 0 && <span className="wire-action-object-chip" data-action-object-state="blocked">等 {plan.blockedObjectEntryOverflowCount} 个阻断对象</span>}
      </div>

      {plan.focus && <FocusedActionBridge onChooseObject={onChooseObject ?? onInspectObject} plan={plan} />}

      <div className="wire-action-group-list">
        {plan.groups.length === 0 && <span className="empty-hint">等待服务端行动窗口。</span>}
        {plan.groups.map((group) => (
          <article className={group.enabled ? "wire-action-group is-enabled" : "wire-action-group"} key={group.key}>
            <div className="wire-action-group-heading">
              <strong>{group.label}</strong>
              <span>{group.enabledCount} / {group.totalCount}</span>
            </div>
            <div className="wire-action-role-grid">
              {group.roleCounts.map((role) => (
                <span key={role.role}>
                  {role.label} {role.count}
                </span>
              ))}
            </div>
            <small>{group.reason}</small>
          </article>
        ))}
      </div>

      <CandidateInteractionPlanList onChooseObject={onChooseObject ?? onInspectObject} plan={plan} />

      <div className="wire-action-grammar" role="group" aria-label="服务端候选交互语法">
        <strong>交互语法</strong>
        {plan.grammarCandidateTotalCount === 0 && <span className="empty-hint">暂无候选步骤。</span>}
        {plan.grammarCandidates.map((candidate) => (
          <article className="wire-action-sequence" key={candidate.key}>
            <div className="wire-action-sequence-title">
              <span>{candidate.label}</span>
              <small>{candidate.stepCount} 步 / {candidate.commandFieldCount} 字段</small>
            </div>
            <ol>
              {candidate.steps.map((step) => (
                <li className={`wire-action-step wire-action-step-${step.role} ${step.required ? "is-required" : ""}`} key={step.key}>
                  <span>{step.label}</span>
                  <strong>{step.count}</strong>
                  <small>{step.required ? "必需；" : ""}{step.sampleLabel}</small>
                </li>
              ))}
            </ol>
            <CommandFieldList candidate={candidate} />
          </article>
        ))}
      </div>
    </section>
  );
}

function CurrentRouteStrip({ route }: { route?: WireActionRoutePlan }) {
  if (!route) {
    return (
      <div className="wire-action-route-strip" data-action-route-state="empty" role="group" aria-label="当前候选路径">
        <strong>当前路径</strong>
        <span>点击服务端候选对象后显示选择路线。</span>
      </div>
    );
  }

  return (
    <div className="wire-action-route-strip" data-action-route-state={route.state} role="group" aria-label="当前候选路径">
      <div className="wire-action-route-heading">
        <strong>{route.candidateLabel}</strong>
        <span>{route.stateLabel}</span>
      </div>
      <small>{route.commandType ?? "未公开命令"} / 已选步骤 {route.selectedStepCount} / 缺少 {route.missingRequiredSelectionCount} / {route.nextStepLabel}</small>
      <ol>
        {route.steps.map((step) => (
          <li data-route-step-role={step.role} data-route-step-state={step.state} key={step.key}>
            <span>{step.label}</span>
            <strong>{step.stateLabel}</strong>
            <small>{step.required ? "必需" : "可选"} / 候选 {step.totalCount} / 已选 {step.selectedCount}</small>
          </li>
        ))}
      </ol>
    </div>
  );
}

function FocusedActionBridge({ onChooseObject, plan }: { onChooseObject?: (objectId: string) => void; plan: WireActionMapPlan }) {
  const focus = plan.focus;
  if (!focus) {
    return null;
  }

  return (
    <section
      aria-label="焦点对象合法操作联动"
      className="wire-action-focus-bridge"
      data-action-focus-state={focus.enabledCandidateCount > 0 ? "enabled" : focus.candidateCount > 0 ? "blocked" : "empty"}
    >
      <div className="wire-action-focus-bridge-heading">
        <strong>{focus.label}</strong>
        <span>{focus.stateLabel}</span>
      </div>
      <div className="wire-action-focus-bridge-metrics">
        <small>对象 {focus.objectId}</small>
        <small>{focus.roleLabels.length > 0 ? `角色 ${focus.roleLabels.join(" / ")}` : "无候选角色"}</small>
        <small>{focus.enabledCandidateCount} 可提交 / {focus.disabledCandidateCount} 阻断</small>
      </div>
      {focus.relatedCandidates.length > 0 ? (
        <ol className="wire-action-focus-candidate-list">
          {focus.relatedCandidates.map((candidate) => (
            <li className={candidate.enabled ? "is-enabled" : "is-disabled"} key={candidate.key}>
              <span>{candidate.label}</span>
              <strong>{candidate.nextStepLabel}</strong>
              <small>{candidate.roleLabels.join(" / ")} / {candidate.commandType ?? "未公开命令"} / {candidate.stateLabel}</small>
              {!candidate.enabled && <small>{candidate.reason}</small>}
              {candidate.nextObjectRefs.length > 0 && (
                <div className="wire-action-focus-choice-list" role="group" aria-label={`${candidate.label} 下一步对象`}>
                  {candidate.nextObjectRefs.map((ref) => (
                    <button
                      data-action-focus-choice-object-id={ref.objectId}
                      key={ref.key}
                      onClick={() => onChooseObject?.(ref.objectId)}
                      type="button"
                    >
                      <span>{ref.roleLabel}</span>
                      <strong>{ref.label}</strong>
                    </button>
                  ))}
                </div>
              )}
            </li>
          ))}
        </ol>
      ) : (
        <span className="empty-hint">焦点对象当前没有服务端候选。</span>
      )}
    </section>
  );
}

function CandidateInteractionPlanList({ onChooseObject, plan }: { onChooseObject?: (objectId: string) => void; plan: WireActionMapPlan }) {
  return (
    <div className="wire-action-candidate-plan" role="group" aria-label="服务端候选步骤计划">
      <div className="wire-action-candidate-plan-heading">
        <strong>候选步骤</strong>
        <span>{plan.candidatePlanTotalCount} 项</span>
      </div>
      {plan.candidatePlans.length === 0 && <span className="empty-hint">暂无服务端候选。</span>}
      {plan.candidatePlans.map((candidatePlan) => (
        <article
          className={candidatePlan.enabled ? "wire-action-candidate-plan-card is-enabled" : "wire-action-candidate-plan-card"}
          data-candidate-plan-action={candidatePlan.action}
          data-candidate-plan-draft-active={candidatePlan.draftActive ? "true" : "false"}
          data-candidate-plan-enabled={candidatePlan.enabled ? "true" : "false"}
          key={candidatePlan.key}
        >
          <div className="wire-action-candidate-plan-title">
            <span>{candidatePlan.candidateLabel}</span>
            <small>{candidatePlan.summary}</small>
          </div>
          <div className="wire-action-candidate-plan-next" data-candidate-plan-next-step={candidatePlan.nextRequiredStep?.role ?? "none"}>
            下一步：{candidatePlan.nextRequiredStep?.label ?? "等待服务端候选"}
          </div>
          <ol>
            {candidatePlan.stepRows.slice(0, 4).map((step) => (
              <li
                className={step.required ? "is-required" : ""}
                data-step-progress={step.selectionState}
                data-step-role={step.role}
                data-step-state={step.state}
                key={step.key}
              >
                <span>{step.label}</span>
                <strong>{step.count}</strong>
                <small>{step.stateLabel} / {step.sampleLabels.length > 0 ? step.sampleLabels.join(" / ") : "由服务端候选决定"}</small>
                <small data-step-progress-label={step.selectionState}>
                  {step.progressLabel}{step.selectedLabels.length > 0 ? ` / ${step.selectedLabels.join(" / ")}` : ""}
                </small>
                {step.objectRefs.length > 0 && (
                  <div className="wire-action-candidate-step-ref-list" role="group" aria-label={`${candidatePlan.candidateLabel} ${step.label}对象`}>
                    {step.objectRefs.map((ref) => (
                      <button
                        data-action-candidate-step-object-id={ref.objectId}
                        data-action-candidate-step-role={step.role}
                        key={ref.key}
                        onClick={() => onChooseObject?.(ref.objectId)}
                        type="button"
                      >
                        <span>{ref.roleLabel}</span>
                        <strong>{ref.label}</strong>
                      </button>
                    ))}
                  </div>
                )}
              </li>
            ))}
          </ol>
          <small>命令字段 {candidatePlan.commandFieldCount}{candidatePlan.commandType ? ` / ${candidatePlan.commandType}` : ""}</small>
        </article>
      ))}
    </div>
  );
}

function PromptContractStrip({ contract }: { contract: WireActionContractPlan }) {
  return (
    <div className="wire-action-contract-strip" aria-label="当前提示服务端契约">
      <div>
        <strong>提示契约</strong>
        <span>{contract.promptKind} / {contract.candidateAction}</span>
      </div>
      <ContractMetric label="提交字段" value={contract.requiredPayloadCount} />
      <ContractMetric label="合法选项" value={contract.legalChoicesCount} />
      <ContractMetric label="公开数据" value={contract.visibleMetadataCount} />
      <span>
        <b>隐藏数据</b>
        <small>{contract.hiddenMetadataCount} 项由服务端保留</small>
      </span>
    </div>
  );
}

function ContractMetric({ label, value }: { label: string; value: number }) {
  return (
    <span>
      <b>{label}</b>
      <small>{value} 项</small>
    </span>
  );
}

function CommandFieldList({ candidate }: { candidate: WireActionGrammarCandidatePlan }) {
  if (candidate.commandFields.length === 0) {
    return <span className="wire-action-command-empty">命令字段：服务端未公开模板</span>;
  }

  return (
    <div className="wire-action-command" aria-label={`${candidate.label} 服务端命令字段`}>
      <div className="wire-action-command-title">
        <span>命令字段</span>
        <strong>{candidate.commandType}</strong>
      </div>
      <ol>
        {candidate.commandFields.map((field) => (
          <li className={field.required ? "is-required" : ""} data-command-field={field.field} key={field.key}>
            <span>{field.label}</span>
            <small>{field.required ? "必需" : "可选"} / {field.sourceLabel}</small>
          </li>
        ))}
      </ol>
    </div>
  );
}

function Metric({ metric }: { metric: WireActionMapMetric }) {
  return (
    <span className="wire-action-map-metric">
      <span>{metric.label}</span>
      <strong>{metric.value}</strong>
    </span>
  );
}

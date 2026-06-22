import type { ActionPromptContractDto } from "../../types/protocol";
import type { TableObjectContext } from "../../utils/tableObjectContext";
import {
  buildWireObjectInspectionPlan,
  type WireObjectInspectionGroup,
  type WireObjectInspectionPlan
} from "../../utils/wireObjectInspectionPlan";

export function WireObjectInspectionSummary({
  context,
  contract
}: {
  context?: TableObjectContext;
  contract?: ActionPromptContractDto | null;
}) {
  const plan = buildWireObjectInspectionPlan({ context, contract });

  if (!plan) {
    return <span className="empty-hint">没有可检查的服务端对象上下文。</span>;
  }

  return (
    <section
      aria-label="服务端对象检查摘要"
      className="wire-object-inspection"
      data-wire-object-inspection-authority={plan.authorityState}
      data-wire-object-inspection-object-id={plan.objectId}
      data-wire-object-inspection-source={plan.contextSourceLabel}
    >
      <header className="wire-object-inspection-heading">
        <div>
          <strong>对象检查摘要</strong>
          <span>{plan.authorityLabel} / {plan.contextSourceLabel}</span>
        </div>
      </header>
      <p data-wire-object-inspection-boundary>{plan.boundaryLabel}</p>
      <ol className="wire-object-inspection-metrics" aria-label="服务端对象检查指标">
        {plan.metrics.map((metric) => (
          <li
            data-wire-object-inspection-metric={metric.key}
            data-wire-object-inspection-metric-source={metric.sourceLabel}
            data-wire-object-inspection-metric-state={metric.state}
            key={metric.key}
          >
            <span>{metric.label}</span>
            <strong>{metric.value}</strong>
            <small>{metric.sourceLabel}</small>
          </li>
        ))}
      </ol>
      <ol className="wire-object-inspection-routes" aria-label="服务端对象检查路线">
        {plan.routeRows.map((row) => (
          <li
            data-wire-object-inspection-route={row.key}
            data-wire-object-inspection-route-source={row.sourceLabel}
            data-wire-object-inspection-route-state={row.state}
            key={row.key}
          >
            <span>{row.label}</span>
            <strong>{row.stateLabel}</strong>
            <small>{row.summary}</small>
          </li>
        ))}
      </ol>
      <div className="wire-object-inspection-groups">
        {plan.groups.map((group) => (
          <ObjectInspectionGroup group={group} key={group.key} />
        ))}
      </div>
    </section>
  );
}

function ObjectInspectionGroup({ group }: { group: WireObjectInspectionGroup }) {
  return (
    <section
      data-wire-object-inspection-group={group.key}
      data-wire-object-inspection-group-count={group.rows.length}
      data-wire-object-inspection-group-source={group.sourceLabel}
    >
      <header>
        <strong>{group.title}</strong>
        <span>{group.sourceLabel}</span>
      </header>
      {group.rows.length === 0 ? (
        <p className="empty-hint">{group.emptyLabel ?? "当前没有公开记录。"}</p>
      ) : (
        <ol>
          {group.rows.slice(0, 8).map((row) => (
            <li
              data-wire-object-inspection-row={`${group.key}:${row.key}`}
              data-wire-object-inspection-row-tone={row.tone ?? "neutral"}
              key={row.key}
            >
              <span>{row.label}</span>
              <strong>{row.value}</strong>
            </li>
          ))}
        </ol>
      )}
    </section>
  );
}

export type { WireObjectInspectionPlan };

import { StatusPill } from "../ui/StatusPill";
import { buildWireTableAuthorityPlan, type WireTableAuthorityState } from "./wireTableAuthorityPlan";
import type { WireTableViewModel } from "./wireTableViewModel";

export function WireTableAuthorityPanel({ table }: { table: WireTableViewModel }) {
  const plan = buildWireTableAuthorityPlan(table);

  return (
    <section
      aria-label="服务端桌面布局契约"
      className="wire-table-authority"
      data-wire-table-authority-state={plan.state}
    >
      <header className="wire-table-authority-header">
        <div>
          <strong>桌面布局契约</strong>
          <span>{plan.summary}</span>
        </div>
        <StatusPill tone={stateTone(plan.state)}>{plan.stateLabel}</StatusPill>
      </header>

      <div className="wire-table-authority-metrics">
        {plan.metrics.map((metric) => (
          <span data-wire-table-authority-metric={metric.key} data-wire-table-authority-metric-state={metric.state} key={metric.key}>
            <small>{metric.label}</small>
            <strong>{metric.value}</strong>
          </span>
        ))}
      </div>

      <section className="wire-table-authority-group" aria-label="玩家基础区来源">
        <strong>基础区分区</strong>
        <ol>
          {plan.players.map((row) => (
            <li
              data-wire-table-authority-player-source={row.source}
              data-wire-table-authority-row-state={row.state}
              key={row.key}
            >
              <span>{row.label}</span>
              <strong>{row.sourceLabel}</strong>
              <small>基地 {row.baseCount} / 符文 {row.runeCount}</small>
            </li>
          ))}
        </ol>
      </section>

      <section className="wire-table-authority-group" aria-label="战场单位分边来源">
        <strong>战场单位分边</strong>
        <ol>
          {plan.lanes.map((row) => (
            <li
              data-wire-table-authority-lane-source={row.source}
              data-wire-table-authority-row-state={row.state}
              key={row.key}
            >
              <span>{row.label}</span>
              <strong>{row.sourceLabel}</strong>
              <small>我方 {row.ownCount} / 对方 {row.opposingCount}</small>
            </li>
          ))}
        </ol>
      </section>

      <section className="wire-table-authority-group" aria-label="战场待命槽位来源">
        <strong>待命槽位</strong>
        <ol>
          {plan.lanes.map((row) => (
            <li
              data-wire-table-authority-row-state={row.standbyState}
              data-wire-table-authority-standby-source={row.standbySource}
              key={`${row.key}:standby`}
            >
              <span>{row.label}</span>
              <strong>{row.standbySourceLabel}</strong>
              <small>槽位 {row.standbyCount} / 隐藏 {row.hiddenStandbyCount}</small>
            </li>
          ))}
        </ol>
      </section>
    </section>
  );
}

function stateTone(state: WireTableAuthorityState): "good" | "info" | "neutral" | "warn" {
  if (state === "server") {
    return "good";
  }

  if (state === "mixed") {
    return "warn";
  }

  if (state === "fallback") {
    return "neutral";
  }

  return "info";
}

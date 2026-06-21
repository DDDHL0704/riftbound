import { StatusPill } from "../ui/StatusPill";
import { buildWireTableAuthorityPlan, type WireTableAuthorityState } from "./wireTableAuthorityPlan";
import type { WireTableViewModel } from "./wireTableViewModel";

export function WireTableAuthorityPanel({ selectedObjectId, table }: { selectedObjectId?: string; table: WireTableViewModel }) {
  const plan = buildWireTableAuthorityPlan(table, { selectedObjectId });

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

      <section className="wire-table-authority-group" aria-label="区域容量与溢出策略">
        <strong>区域容量矩阵</strong>
        <ol>
          {plan.capacityRows.map((row) => (
            <li
              data-wire-table-capacity-count={row.itemCount}
              data-wire-table-capacity-kind={row.kind}
              data-wire-table-capacity-overflow={row.overflow}
              data-wire-table-capacity-overflow-count={row.overflowCount}
              data-wire-table-capacity-row={row.key}
              data-wire-table-capacity-slots={row.slotCount}
              data-wire-table-capacity-state={row.state}
              data-wire-table-capacity-visible-slots={row.visibleSlotCount}
              key={row.key}
            >
              <span>{row.label}</span>
              <strong>{row.stateLabel}</strong>
              <small>{row.cardWidth}x{row.cardHeight} / 牌 {row.itemCount} / 槽 {row.slotCount} / 可见 {row.visibleSlotCount} / 溢出 {row.overflowCount}</small>
            </li>
          ))}
        </ol>
      </section>

      <section
        aria-label="选中对象布局定位"
        className="wire-table-authority-group"
        data-wire-table-selected-layout-capacity-row={plan.selectedLayout.capacityRowKey ?? ""}
        data-wire-table-selected-layout-kind={plan.selectedLayout.kind}
        data-wire-table-selected-layout-object={plan.selectedLayout.objectId ?? ""}
        data-wire-table-selected-layout-source={plan.selectedLayout.source}
        data-wire-table-selected-layout-state={plan.selectedLayout.state}
        data-wire-table-selected-layout-zone={plan.selectedLayout.zoneKey ?? ""}
      >
        <strong>选中对象布局定位</strong>
        <ol>
          <li data-wire-table-selected-layout-row="summary" data-wire-table-selected-layout-row-state={plan.selectedLayout.state}>
            <span>{plan.selectedLayout.zoneLabel}</span>
            <strong>{plan.selectedLayout.stateLabel}</strong>
            <small>{plan.selectedLayout.summary}</small>
          </li>
        </ol>
      </section>

      <section
        aria-label="共享布局计划一致性"
        className="wire-table-authority-group"
        data-wire-table-consistency-state={plan.consistencyState}
      >
        <strong>共享布局计划</strong>
        <ol>
          {plan.consistencyRows.map((row) => (
            <li
              data-wire-table-consistency-kind={row.expectedKind}
              data-wire-table-consistency-row={row.key}
              data-wire-table-consistency-state={row.state}
              key={row.key}
            >
              <span>{row.label}</span>
              <strong>{row.stateLabel}</strong>
              <small>{row.cardWidth}x{row.cardHeight} / 槽 {row.slotCount} / 可见 {row.visibleSlotCount}</small>
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

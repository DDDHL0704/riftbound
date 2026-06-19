import type { WireTimelineDetail } from "./WireTimelineDetailPanel";

type WireDetailTriggerProps = {
  detail: WireTimelineDetail;
  label?: string;
  onSelectDetail?: (detail: WireTimelineDetail) => void;
  selectedDetailId?: string;
};

export function WireDetailTrigger({
  detail,
  label = "详情",
  onSelectDetail,
  selectedDetailId
}: WireDetailTriggerProps) {
  if (!onSelectDetail) {
    return null;
  }

  const selected = selectedDetailId === detail.id;
  const sourceLabel = detail.source === "event" ? "日志事件" : "规则队列";

  return (
    <button
      aria-controls="wire-timeline-detail-body"
      aria-label={`查看${sourceLabel}详情：${detail.title}`}
      aria-pressed={selected}
      className="wire-detail-trigger"
      data-detail-selected={selected ? "true" : "false"}
      data-wire-detail-id={detail.id}
      data-wire-detail-source={detail.source}
      onClick={() => onSelectDetail(detail)}
      type="button"
    >
      {label}
    </button>
  );
}

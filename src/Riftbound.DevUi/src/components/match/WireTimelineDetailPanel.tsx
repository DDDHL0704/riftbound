import { WireObjectRefChips, type WireObjectIndex, type WireObjectRef } from "./WireObjectRefChips";

export type WireTimelineDetailLine = {
  label: string;
  mine?: boolean;
  value: string;
};

export type WireTimelineDetail = {
  id: string;
  lines: WireTimelineDetailLine[];
  refs: WireObjectRef[];
  source: "event" | "rule";
  subtitle?: string;
  title: string;
};

export function WireTimelineDetailPanel({
  detail,
  objectIndex,
  onClear,
  onInspectObject,
  selectedObjectId
}: {
  detail?: WireTimelineDetail;
  objectIndex: WireObjectIndex;
  onClear: () => void;
  onInspectObject?: (objectId: string) => void;
  selectedObjectId?: string;
}) {
  return (
    <section className="wire-timeline-detail" aria-label="规则与事件详情">
      <header className="wire-timeline-detail-header">
        <div>
          <strong>{detail ? detail.title : "未选择规则事件"}</strong>
          <span>{detail?.subtitle ?? "从结算链、规则任务、触发队列或日志中选择一项。"}</span>
        </div>
        {detail && (
          <button className="wire-detail-clear" onClick={onClear} type="button">
            清除
          </button>
        )}
      </header>
      {detail ? (
        <>
          <div className="wire-timeline-detail-lines">
            {detail.lines.map((line) => (
              <span className={line.mine ? "wire-timeline-detail-line is-mine" : "wire-timeline-detail-line"} key={`${line.label}-${line.value}`}>
                <span>{line.label}</span>
                <strong>{line.value || "无"}</strong>
              </span>
            ))}
          </div>
          <WireObjectRefChips
            objects={objectIndex}
            onInspectObject={onInspectObject}
            refs={detail.refs}
            selectedObjectId={selectedObjectId}
            source={detail.source}
          />
        </>
      ) : (
        <span className="empty-hint">暂无焦点事件。</span>
      )}
    </section>
  );
}

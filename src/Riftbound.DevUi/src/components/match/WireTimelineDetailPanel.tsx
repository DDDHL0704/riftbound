import type { TableObjectContext } from "../../utils/tableObjectContext";
import { WireObjectContextSummary } from "./WireObjectContextSummary";
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
  selectedObjectContext,
  selectedObjectId
}: {
  detail?: WireTimelineDetail;
  objectIndex: WireObjectIndex;
  onClear: () => void;
  onInspectObject?: (objectId: string) => void;
  selectedObjectContext?: TableObjectContext;
  selectedObjectId?: string;
}) {
  const headerTitle = detail ? detail.title : selectedObjectContext ? "焦点对象规则上下文" : "未选择规则事件";
  const headerSubtitle = detail?.subtitle
    ?? (selectedObjectContext
      ? "来自服务端快照、行动窗口、结算链和事件索引。"
      : "从结算链、规则任务、触发队列或日志中选择一项。");

  return (
    <section className="wire-timeline-detail" aria-label="规则与事件详情">
      <header className="wire-timeline-detail-header">
        <div>
          <strong>{headerTitle}</strong>
          <span>{headerSubtitle}</span>
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
          {selectedObjectContext && (
            <ObjectContextDetail
              context={selectedObjectContext}
              objectIndex={objectIndex}
              onInspectObject={onInspectObject}
              selectedObjectId={selectedObjectId}
              title="当前桌面焦点"
            />
          )}
        </>
      ) : selectedObjectContext ? (
        <ObjectContextDetail
          context={selectedObjectContext}
          objectIndex={objectIndex}
          onInspectObject={onInspectObject}
          selectedObjectId={selectedObjectId}
          title="焦点对象"
        />
      ) : (
        <span className="empty-hint">暂无焦点事件。</span>
      )}
    </section>
  );
}

function ObjectContextDetail({
  context,
  objectIndex,
  onInspectObject,
  selectedObjectId,
  title
}: {
  context: TableObjectContext;
  objectIndex: WireObjectIndex;
  onInspectObject?: (objectId: string) => void;
  selectedObjectId?: string;
  title: string;
}) {
  return (
    <div className="wire-selected-object-context" data-wire-selected-object-context={context.objectId}>
      <strong>{title}</strong>
      <WireObjectRefChips
        objects={objectIndex}
        onInspectObject={onInspectObject}
        refs={[{ id: context.objectId, label: context.cardNo ?? context.object?.cardNo ?? undefined, role: "对象" }]}
        selectedObjectId={selectedObjectId}
        source="rule"
      />
      <WireObjectContextSummary context={context} />
    </div>
  );
}

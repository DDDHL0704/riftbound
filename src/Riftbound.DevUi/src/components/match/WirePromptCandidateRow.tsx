import type { WirePromptCandidateRowPlan } from "../../utils/wirePromptCandidatePlan";
import { StatusPill } from "../ui/StatusPill";
import { WireObjectRefChips, type WireObjectIndex } from "./WireObjectRefChips";

export function WirePromptCandidateRow({
  objects,
  onInspectObject,
  row,
  selectedObjectId
}: {
  objects: WireObjectIndex;
  onInspectObject?: (objectId: string) => void;
  row: WirePromptCandidateRowPlan;
  selectedObjectId?: string;
}) {
  return (
    <article className={`wire-candidate-row ${row.enabled ? "is-enabled" : "is-disabled"}`}>
      <div>
        <strong>{row.label}</strong>
        <StatusPill tone={row.enabled ? "good" : "neutral"}>{row.enabled ? "可提交" : "不可提交"}</StatusPill>
      </div>
      <span>{row.reason}</span>
      {row.choiceGroups.map((group) => (
        <small key={group.key}>{group.summary}</small>
      ))}
      <WireObjectRefChips
        className="wire-candidate-object-ref-list"
        objects={objects}
        onInspectObject={onInspectObject}
        refs={row.objectRefs}
        selectedObjectId={selectedObjectId}
        source="candidate"
      />
    </article>
  );
}

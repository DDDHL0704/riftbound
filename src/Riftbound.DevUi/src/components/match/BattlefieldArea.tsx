import { BehaviorSpec } from "../../types/catalog";
import { SnapshotDto } from "../../types/protocol";
import { asArray, asRecord, asString } from "../../utils/collections";
import { CardFace, InspectedCard } from "../cards/CardFace";
import { StatusPill } from "../ui/StatusPill";

export function BattlefieldArea({ onInspectCard, snapshot, specs }: { onInspectCard: (card: InspectedCard) => void; snapshot?: SnapshotDto; specs: Record<string, BehaviorSpec> }) {
  const lanes = asRecord(snapshot?.lanes);
  const battlefields = asArray<Record<string, unknown>>(lanes.battlefields);

  return (
    <section className="battlefield-area">
      <header>
        <div>
          <span className="eyebrow">公共区域</span>
          <h2>中央战场</h2>
        </div>
        <StatusPill tone={battlefields.length === 2 ? "good" : "warn"}>{battlefields.length || 0} 处战场</StatusPill>
      </header>
      <div className="battlefield-grid">
        {battlefields.length === 0 && <div className="empty-panel">等待服务端下发战场 snapshot。</div>}
        {battlefields.map((field, index) => {
          const cardNo = asString(field.cardNo, "");
          const occupants = asArray<string>(field.occupantObjectIds);
          const standby = asArray<string>(field.standbyObjectIds);
          return (
            <article className="battlefield-card" key={asString(field.battlefieldObjectId, `battlefield-${index}`)}>
              <header>
                <div>
                  <span className="eyebrow">战场 {index + 1}</span>
                  <h3>{specs[cardNo]?.cardName ?? (cardNo || "未命名战场")}</h3>
                </div>
                <StatusPill tone={field.contested ? "warn" : "neutral"}>{field.contested ? "争夺中" : `控制：${asString(field.controllerId, "无人")}`}</StatusPill>
              </header>
              <CardFace
                compact
                object={{
                  cardNo,
                  controllerId: asString(field.controllerId, ""),
                  objectId: asString(field.battlefieldObjectId, ""),
                  ownerId: asString(field.zonePlayerId, "")
                }}
                objectId={asString(field.battlefieldObjectId, "")}
                onInspect={onInspectCard}
                spec={specs[cardNo]}
              />
              <div className="battlefield-occupants">
                <div>
                  <strong>单位</strong>
                  <span>{occupants.length ? occupants.join("、") : "无"}</span>
                </div>
                <div>
                  <strong>待命</strong>
                  <span>{standby.length ? standby.join("、") : `${field.faceDownStandbyCount ?? 0} 张面朝下`}</span>
                </div>
              </div>
            </article>
          );
        })}
      </div>
    </section>
  );
}

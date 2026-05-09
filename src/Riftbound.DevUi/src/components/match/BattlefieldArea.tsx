import { BehaviorSpec } from "../../types/catalog";
import { BattlefieldSnapshotView, CardObjectView, GameEvent, SnapshotDto } from "../../types/protocol";
import { asArray, asRecord, asString } from "../../utils/collections";
import { CardFace, InspectedCard } from "../cards/CardFace";
import { eventDescriptionLabel, eventKindLabel } from "./EventLog";
import { StatusPill } from "../ui/StatusPill";

export function BattlefieldArea({
  events = [],
  onInspectCard,
  snapshot,
  specs
}: {
  events?: GameEvent[];
  onInspectCard: (card: InspectedCard) => void;
  snapshot?: SnapshotDto;
  specs: Record<string, BehaviorSpec>;
}) {
  const lanes = asRecord(snapshot?.lanes);
  const battlefields = asArray<BattlefieldSnapshotView>(lanes.battlefields);
  const objects = objectIndex(snapshot);

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
        {battlefields.length === 0 && <div className="empty-panel">等待服务端下发战场快照；控制者、待命区和本回合得分状态将在快照到达后显示。</div>}
        {battlefields.map((field, index) => {
          const cardNo = asString(field.cardNo, "");
          const occupants = asArray<string>(field.occupantObjectIds);
          const standby = asArray<string>(field.standbyObjectIds);
          const status = asString(field.status, field.contested ? "CONTESTED" : "");
          const contested = field.contested === true || status === "CONTESTED";
          const controllerId = asString(field.controllerId, "无人");
          const occupantControllers = asArray<string>(field.occupantControllerIds);
          const pendingTaskKinds = asArray<string>(field.pendingTaskKinds);
          const battlefieldId = asString(field.battlefieldObjectId, `battlefield-${index}`);
          const groupedOccupants = groupObjectsByController(occupants, occupantControllers, objects);
          const battlefieldEvents = events
            .filter((event) => isBattlefieldEventFor(event, battlefieldId))
            .slice(0, 3);
          return (
            <article className="battlefield-card" key={battlefieldId}>
              <header>
                <div>
                  <span className="eyebrow">战场 {index + 1}</span>
                  <h3>{specs[cardNo]?.cardName ?? (cardNo || "未命名战场")}</h3>
                </div>
                <StatusPill tone={contested ? "warn" : controllerId === "无人" ? "neutral" : "good"}>
                  {contested ? "争夺中" : `控制：${controllerId}`}
                </StatusPill>
              </header>
              <div className="battlefield-state-line">
                <span>{battlefieldStatusLabel(status)}</span>
                <span>归属区 {asString(field.zonePlayerId, "服务端未提供")}</span>
                <span>参战方 {occupantControllers.length || 0}</span>
                <span>待命 {standby.length} / 面朝下 {Number(field.faceDownStandbyCount ?? 0)}</span>
                <span>{battlefieldScoreStatus(field)}</span>
              </div>
              {pendingTaskKinds.length > 0 && (
                <div className="battlefield-task-line">
                  {pendingTaskKinds.slice(0, 3).map((kind) => <span key={kind}>{battlefieldTaskLabel(kind)}</span>)}
                </div>
              )}
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
                <div className="battlefield-controller-lanes">
                  <strong>双方单位</strong>
                  {groupedOccupants.length === 0 ? (
                    <span>无</span>
                  ) : (
                    groupedOccupants.map((group) => (
                      <BattlefieldObjectStrip
                        emptyText="无"
                        ids={group.ids}
                        key={group.controllerId}
                        label={group.controllerId}
                        objects={objects}
                        onInspectCard={onInspectCard}
                        specs={specs}
                      />
                    ))
                  )}
                </div>
                <BattlefieldObjectStrip
                  emptyText={`${field.faceDownStandbyCount ?? 0} 张面朝下`}
                  ids={standby}
                  label="待命区"
                  objects={objects}
                  onInspectCard={onInspectCard}
                  specs={specs}
                />
              </div>
              <BattlefieldLog events={battlefieldEvents} />
            </article>
          );
        })}
      </div>
    </section>
  );
}

function battlefieldStatusLabel(status: string): string {
  switch (status) {
    case "CONTROLLED":
      return "已控制";
    case "CONTESTED":
      return "争夺中";
    case "UNCONTROLLED":
      return "无人控制";
    default:
      return status ? "服务端战场状态" : "等待状态";
  }
}

function battlefieldScoreStatus(field: BattlefieldSnapshotView): string {
  if (typeof field.scoredThisTurn === "boolean") {
    return field.scoredThisTurn ? "本回合已得分" : "本回合未得分";
  }

  const scoredPlayerId = asString(field.scoredPlayerId, "");
  if (scoredPlayerId) {
    return `本回合得分：${scoredPlayerId}`;
  }

  const scoreStatus = asString(field.scoreStatus, "");
  if (scoreStatus) {
    return /^[A-Z0-9_:-]+$/.test(scoreStatus) ? "本回合得分：服务端状态" : `本回合得分：${scoreStatus}`;
  }

  return "本回合得分：等待服务端字段";
}

function battlefieldTaskLabel(kind: string): string {
  switch (kind) {
    case "BATTLEFIELD_CONTESTED":
      return "控制检查";
    case "START_SPELL_DUEL":
      return "法术对决";
    case "START_BATTLE":
      return "战斗";
    case "REMOVE_ILLEGAL_STANDBY":
      return "待命清理";
    default:
      return /^[A-Z0-9_:-]+$/.test(kind) ? "服务端任务" : kind;
  }
}

function groupObjectsByController(
  objectIds: string[],
  controllerIds: string[],
  objects: Record<string, CardObjectView>
): Array<{ controllerId: string; ids: string[] }> {
  const preferredControllers = controllerIds.length > 0 ? controllerIds : ["服务端未提供控制者"];
  const groups = new Map<string, string[]>();
  for (const controllerId of preferredControllers) {
    groups.set(controllerId, []);
  }

  for (const objectId of objectIds) {
    const object = objects[objectId];
    const controllerId = object?.controllerId || object?.ownerId || "服务端未提供控制者";
    if (!groups.has(controllerId)) {
      groups.set(controllerId, []);
    }
    groups.get(controllerId)!.push(objectId);
  }

  return Array.from(groups.entries())
    .map(([controllerId, ids]) => ({ controllerId, ids }))
    .filter((group) => group.ids.length > 0);
}

function isBattlefieldEventFor(event: GameEvent, battlefieldId: string): boolean {
  if (event.kind.startsWith("BATTLEFIELD_")) {
    const payloadBattlefieldId = asString(event.payload.battlefieldObjectId ?? event.payload.battlefieldId, "");
    return !payloadBattlefieldId || payloadBattlefieldId === battlefieldId;
  }

  const payloadBattlefieldId = asString(event.payload.battlefieldObjectId ?? event.payload.battlefieldId, "");
  return payloadBattlefieldId === battlefieldId;
}

function BattlefieldLog({ events }: { events: GameEvent[] }) {
  return (
    <div className="battlefield-log">
      <strong>战场日志</strong>
      {events.length === 0 ? (
        <span>暂无服务端战场事件。</span>
      ) : (
        events.map((event, index) => (
          <span key={`${event.kind}-${index}`}>{eventKindLabel(event.kind)}：{eventDescriptionLabel(event)}</span>
        ))
      )}
    </div>
  );
}

function BattlefieldObjectStrip({
  emptyText = "无",
  ids,
  label,
  objects,
  onInspectCard,
  specs
}: {
  emptyText?: string;
  ids: string[];
  label: string;
  objects: Record<string, CardObjectView>;
  onInspectCard: (card: InspectedCard) => void;
  specs: Record<string, BehaviorSpec>;
}) {
  return (
    <div>
      <strong>{label}</strong>
      {ids.length === 0 ? (
        <span>{emptyText}</span>
      ) : (
        <div className="battlefield-object-row">
          {ids.map((id) => {
            const object = objects[id];
            return object ? (
              <CardFace
                compact
                key={id}
                object={object}
                objectId={id}
                onInspect={onInspectCard}
                spec={object.cardNo ? specs[object.cardNo] : undefined}
              />
            ) : (
              <span className="empty-hint" key={id}>未公开对象</span>
            );
          })}
        </div>
      )}
    </div>
  );
}

function objectIndex(snapshot?: SnapshotDto): Record<string, CardObjectView> {
  const indexed: Record<string, CardObjectView> = {};
  for (const player of Object.values(snapshot?.players ?? {})) {
    for (const [objectId, object] of Object.entries(player.objects ?? {})) {
      indexed[objectId] = object;
    }
  }
  return indexed;
}

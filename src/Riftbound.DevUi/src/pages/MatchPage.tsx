import { type ReactNode, useEffect, useMemo, useState } from "react";
import { AppRoute } from "../app/router";
import { CardFace, InspectedCard } from "../components/cards/CardFace";
import { ActionPanel } from "../components/match/ActionPanel";
import { EventLog } from "../components/match/EventLog";
import { Button } from "../components/ui/Button";
import { ScrollArea } from "../components/ui/ScrollArea";
import { buildWireLayoutFixtureSnapshot, isWireLayoutFixtureEnabled, wireLayoutFixtureSpecByNo } from "../fixtures/wireLayoutFixture";
import { useCatalog } from "../stores/catalogStore";
import { useSettings } from "../stores/settingsStore";
import { useMatchController } from "../stores/useMatchController";
import { BehaviorSpec } from "../types/catalog";
import { BattlefieldSnapshotView, CardObjectView, PlayerSnapshotView, SnapshotDto } from "../types/protocol";
import { asArray, asRecord, asString } from "../utils/collections";
import { connectionStatusLabel, matchPhaseLabel, timingStateLabel } from "../utils/formatters";

type PlayerEntry = {
  id: string;
  player: PlayerSnapshotView;
  side: "self" | "opponent";
};

type ZoneObjects = NonNullable<PlayerSnapshotView["objects"]>;

export function MatchPage({ matchId, onNavigate }: { matchId: string; onNavigate: (route: AppRoute) => void }) {
  const { settings } = useSettings();
  const { specByNo } = useCatalog();
  const controller = useMatchController(settings.serverUrl, matchId, settings.playerId);
  const snapshot = controller.state.snapshot;
  const [inspectedCard, setInspectedCard] = useState<InspectedCard | undefined>();
  const layoutFixtureEnabled = useMemo(() => isWireLayoutFixtureEnabled(), []);
  const tableSnapshot = useMemo(
    () => layoutFixtureEnabled ? buildWireLayoutFixtureSnapshot(settings.playerId) : snapshot,
    [layoutFixtureEnabled, settings.playerId, snapshot]
  );
  const tableSpecByNo = useMemo(
    () => layoutFixtureEnabled ? { ...wireLayoutFixtureSpecByNo, ...specByNo } : specByNo,
    [layoutFixtureEnabled, specByNo]
  );

  const playerEntries = useMemo(() => buildPlayerEntries(tableSnapshot, settings.playerId), [tableSnapshot, settings.playerId]);
  const self = playerEntries.find((entry) => entry.side === "self");
  const opponent = playerEntries.find((entry) => entry.side === "opponent");
  const battlefields = useMemo(() => asArray<BattlefieldSnapshotView>(asRecord(tableSnapshot?.lanes).battlefields), [tableSnapshot?.lanes]);
  const timing = asRecord(snapshot?.timing);
  const turnWindow = asRecord(timing.turnWindow);
  const phase = asString(timing.phase, snapshot?.turnState ?? "");
  const windowState = asString(turnWindow.state, asString(timing.timingState, ""));
  const roomStatus = asString(timing.roomStatus, "");
  const promptTitle = controller.state.prompt?.view?.title?.trim() || "无行动窗口";
  const canAct = Boolean(controller.state.prompt?.actionable && controller.state.prompt.playerId === settings.playerId);

  useEffect(() => {
    if (roomStatus === "FINISHED") {
      onNavigate({ name: "result", matchId });
    }
  }, [matchId, onNavigate, roomStatus]);

  return (
    <div className="wire-match-page">
      <header className="wire-topbar" aria-label="对战基础状态">
        <div className="wire-topbar-title">
          <strong>符文战场对战线框</strong>
          <span>房间 {matchId}</span>
        </div>
        <div className="wire-status-line" aria-label="服务端状态">
          <span>连接 {connectionStatusLabel(controller.state.status)}</span>
          <span>回合 {snapshot?.turnNumber ?? 0}</span>
          <span>阶段 {matchPhaseLabel(phase)}</span>
          <span>窗口 {timingStateLabel(windowState)}</span>
          <span>提示 {promptTitle}</span>
          {layoutFixtureEnabled && <span>桌面 前端样例</span>}
          <span>{canAct ? "当前可操作" : "等待"}</span>
        </div>
        <div className="wire-topbar-actions">
          <Button onClick={() => onNavigate({ name: "lobby" })} variant="ghost">大厅</Button>
          <Button onClick={() => void controller.join()} variant="secondary">连接</Button>
          <Button onClick={() => void controller.requestSnapshot()} variant="secondary">同步</Button>
          <Button onClick={() => void controller.ready()} variant="secondary">准备</Button>
          <Button onClick={() => void controller.submitStarterDeck()} variant="secondary">导入构筑</Button>
          <Button onClick={() => void controller.submitCommand({ cmdType: "PASS" })} variant="secondary">跳过</Button>
          <Button onClick={() => void controller.submitCommand({ cmdType: "END_TURN" })} variant="secondary">结束回合</Button>
          <Button onClick={() => void controller.submitCommand({ cmdType: "SURRENDER" })} variant="danger">投降</Button>
        </div>
      </header>

      <main className="wire-match-body">
        <section className="wire-table-shell" aria-label="黑白线框对战桌面">
          <div className="wire-table">
            <WireHandRail entry={opponent} fallbackSide="opponent" hidden onInspectCard={setInspectedCard} specs={tableSpecByNo} />
            <WirePlayerHome entry={opponent} fallbackSide="opponent" onInspectCard={setInspectedCard} specs={tableSpecByNo} />
            <WireBattlefieldTable
              battlefields={battlefields}
              onInspectCard={setInspectedCard}
              perspectivePlayerId={settings.playerId}
              snapshot={tableSnapshot}
              specs={tableSpecByNo}
            />
            <WirePlayerHome entry={self} fallbackSide="self" onInspectCard={setInspectedCard} specs={tableSpecByNo} />
            <WireHandRail entry={self} fallbackSide="self" onInspectCard={setInspectedCard} specs={tableSpecByNo} />
          </div>
        </section>

        <aside className="wire-side-panel" aria-label="行动与日志">
          <section className="wire-panel">
            <h2>焦点卡牌</h2>
            {inspectedCard ? (
              <div className="wire-focus-card">
                <CardFace object={inspectedCard.object} objectId={inspectedCard.objectId} spec={inspectedCard.spec} />
                <Button onClick={() => setInspectedCard(undefined)} variant="ghost">清除</Button>
              </div>
            ) : (
              <WireEmpty label="点击任意卡牌查看" />
            )}
          </section>
          <section className="wire-panel wire-action-panel">
            <ActionPanel
              connectionStatus={controller.state.status}
              onCommand={(command) => void controller.submitCommand(command)}
              onReady={() => void controller.ready()}
              onSubmitStarterDeck={() => void controller.submitStarterDeck()}
              playerId={settings.playerId}
              prompt={controller.state.prompt}
              snapshot={snapshot}
            />
          </section>
          <section className="wire-panel wire-log-panel">
            <h2>日志</h2>
            <ScrollArea className="wire-log-scroll">
              <EventLog density={settings.logDensity} errors={controller.state.errors} events={controller.state.events} />
            </ScrollArea>
          </section>
        </aside>
      </main>
    </div>
  );
}

function buildPlayerEntries(snapshot: SnapshotDto | undefined, perspectivePlayerId: string): PlayerEntry[] {
  const entries = Object.entries(snapshot?.players ?? {}).map(([id, player]): PlayerEntry => ({
    id,
    player,
    side: id === perspectivePlayerId ? "self" : "opponent"
  }));

  return entries.sort((left, right) => sideOrder(left.side) - sideOrder(right.side));
}

function sideOrder(side: PlayerEntry["side"]): number {
  return side === "opponent" ? 0 : 1;
}

function WirePlayerHome({
  entry,
  fallbackSide,
  onInspectCard,
  specs
}: {
  entry?: PlayerEntry;
  fallbackSide: PlayerEntry["side"];
  onInspectCard: (card: InspectedCard) => void;
  specs: Record<string, BehaviorSpec>;
}) {
  const side = entry?.side ?? fallbackSide;
  const zones = entry?.player.zones ?? {};
  const objects = entry?.player.objects ?? {};
  const baseIds = zones.base ?? [];
  const runeIds = baseIds.filter((id) => isRuneCard(objects[id], specs[objects[id]?.cardNo ?? ""]));
  const baseObjectIds = baseIds.filter((id) => !runeIds.includes(id));

  return (
    <section className={`wire-player-home wire-player-${side} ${entry ? "" : "wire-player-missing"}`} aria-label={`${entry ? playerLabel(entry) : side === "self" ? "P1 我方" : "P2 对手"} 基础区`}>
      <WireZone className="wire-home-legend wire-signature-zone" title="传奇">
        <WireFixedCardStrip emptyLabel="传奇" ids={zones.legendZone ?? []} objects={objects} onInspectCard={onInspectCard} specs={specs} />
      </WireZone>
      <WireZone className="wire-home-hero wire-signature-zone" title="英雄">
        <WireFixedCardStrip emptyLabel="英雄" ids={zones.championZone ?? []} objects={objects} onInspectCard={onInspectCard} specs={specs} />
      </WireZone>
      <WireZone className="wire-home-base" title="基地 / 放逐">
        <div className="wire-base-banish-grid">
          <section className="wire-base-main" aria-label="基地">
            <div className="wire-mini-title">基地</div>
            <WireFixedCardStrip className="wire-card-scroll-grid wire-base-card-grid" emptyLabel="基地" ids={baseObjectIds} objects={objects} onInspectCard={onInspectCard} specs={specs} />
          </section>
          <section className="wire-banish-main" aria-label="放逐区">
            <div className="wire-mini-title">放逐区</div>
            <WirePublicPile ids={zones.banished ?? []} label="放逐" objects={objects} onInspectCard={onInspectCard} specs={specs} />
          </section>
        </div>
      </WireZone>
    </section>
  );
}

function WireHandRail({
  entry,
  fallbackSide,
  hidden = false,
  onInspectCard,
  specs
}: {
  entry?: PlayerEntry;
  fallbackSide: PlayerEntry["side"];
  hidden?: boolean;
  onInspectCard: (card: InspectedCard) => void;
  specs: Record<string, BehaviorSpec>;
}) {
  const side = entry?.side ?? fallbackSide;

  if (!entry) {
    return (
      <section className={`wire-hand-rail wire-hand-${side} wire-hand-missing`}>
        <div className="wire-hand-rune-deck">
          <WireStackCount count={12} label="符文牌堆" />
        </div>
        <div className="wire-hand-rune-track" aria-label="已抽出符文占位">
          <div className="wire-zone-title">已抽出符文 / 最多 12</div>
          <WireRuneTrack ids={[]} objects={{}} onInspectCard={onInspectCard} reverse={side === "opponent"} specs={specs} />
        </div>
        <div className="wire-hand-zone">
          <div className="wire-section-label wire-hand-title">
            <strong>手牌</strong>
          </div>
          <div className="wire-hand-body">
            <div className="wire-hand-cards">
              <div className="wire-card-row wire-card-row-centered">
                <WireEmpty label="等待手牌信息" />
              </div>
            </div>
            <div className="wire-hand-piles">
              <div className="wire-hand-library-pile">
                <WireStackCount count={0} label="牌库" />
              </div>
              <div className="wire-hand-played-pile">
                <WirePublicPile ids={[]} label="已打出" objects={{}} onInspectCard={onInspectCard} specs={specs} />
              </div>
            </div>
          </div>
        </div>
      </section>
    );
  }

  const zones = entry.player.zones ?? {};
  const objects = entry.player.objects ?? {};
  const baseIds = zones.base ?? [];
  const runeIds = baseIds.filter((id) => isRuneCard(objects[id], specs[objects[id]?.cardNo ?? ""]));
  const ids = hidden ? hiddenCards(entry.player.handSize ?? zones.handHidden ?? 0, entry.id) : zones.hand ?? [];

  return (
    <section className={`wire-hand-rail wire-hand-${side}`} aria-label={`${playerLabel(entry)} 手牌`}>
      <div className="wire-hand-rune-deck">
        <WireStackCount count={zones.runeDeckCount ?? 12} label="符文牌堆" />
      </div>
      <div className="wire-hand-rune-track" aria-label={`${playerLabel(entry)} 已抽出符文`}>
        <div className="wire-zone-title">已抽出符文 / 最多 12</div>
        <WireRuneTrack ids={runeIds} objects={objects} onInspectCard={onInspectCard} reverse={side === "opponent"} specs={specs} />
      </div>
      <div className="wire-hand-zone">
        <div className="wire-section-label wire-hand-title">
          <strong>{entry.side === "self" ? "我方手牌" : "对手手牌"}</strong>
        </div>
        <div className="wire-hand-body">
          <div className="wire-hand-cards">
            <WireCardStrip ids={ids} objects={objects} onInspectCard={onInspectCard} specs={specs} />
          </div>
          <div className="wire-hand-piles">
            <div className="wire-hand-library-pile">
              <WireStackCount count={zones.mainDeckCount ?? 0} label="牌库" />
            </div>
            <div className="wire-hand-played-pile">
              <WirePublicPile ids={zones.graveyard ?? []} label="已打出" objects={objects} onInspectCard={onInspectCard} specs={specs} />
            </div>
          </div>
        </div>
      </div>
    </section>
  );
}

function WireBattlefieldTable({
  battlefields,
  onInspectCard,
  perspectivePlayerId,
  snapshot,
  specs
}: {
  battlefields: BattlefieldSnapshotView[];
  onInspectCard: (card: InspectedCard) => void;
  perspectivePlayerId: string;
  snapshot?: SnapshotDto;
  specs: Record<string, BehaviorSpec>;
}) {
  const objects = objectIndex(snapshot);
  const lanes = [0, 1].map((index) => buildBattlefieldLane(battlefields[index], index, objects, perspectivePlayerId));

  return (
    <section className="wire-battlefield-stack" aria-label="公共战场">
      <WireBattlefieldSite lane={lanes[0]} onInspectCard={onInspectCard} sideLabel="左战场牌" specs={specs} />
      <div className="wire-battlefield-center-grid">
        <WireBattlefieldUnitZone ids={lanes[0].opposingOccupants} objects={objects} onInspectCard={onInspectCard} specs={specs} title="左战场 / 对方" />
        <WireBattlefieldUnitZone ids={lanes[1].opposingOccupants} objects={objects} onInspectCard={onInspectCard} specs={specs} title="右战场 / 对方" />
        <WireBattlefieldUnitZone ids={lanes[0].ownOccupants} objects={objects} onInspectCard={onInspectCard} specs={specs} title="左战场 / 我方" />
        <WireBattlefieldUnitZone ids={lanes[1].ownOccupants} objects={objects} onInspectCard={onInspectCard} specs={specs} title="右战场 / 我方" />
      </div>
      <WireBattlefieldSite lane={lanes[1]} onInspectCard={onInspectCard} sideLabel="右战场牌" specs={specs} />
    </section>
  );
}

type WireBattlefieldLane = {
  battlefield?: BattlefieldSnapshotView;
  battlefieldId: string;
  cardNo: string;
  controllerId: string;
  index: number;
  ownOccupants: string[];
  opposingOccupants: string[];
  zonePlayerId: string;
};

function buildBattlefieldLane(
  battlefield: BattlefieldSnapshotView | undefined,
  index: number,
  objects: Record<string, CardObjectView>,
  perspectivePlayerId: string
): WireBattlefieldLane {
  const occupants = asArray<string>(battlefield?.occupantObjectIds);
  return {
    battlefield,
    battlefieldId: asString(battlefield?.battlefieldObjectId, `empty-battlefield-${index}`),
    cardNo: asString(battlefield?.cardNo, ""),
    controllerId: asString(battlefield?.controllerId, ""),
    index,
    ownOccupants: occupants.filter((id) => ownerOrController(objects[id]) === perspectivePlayerId),
    opposingOccupants: occupants.filter((id) => ownerOrController(objects[id]) !== perspectivePlayerId),
    zonePlayerId: asString(battlefield?.zonePlayerId, "")
  };
}

function WireBattlefieldSite({
  lane,
  onInspectCard,
  sideLabel,
  specs
}: {
  lane: WireBattlefieldLane;
  onInspectCard: (card: InspectedCard) => void;
  sideLabel: string;
  specs: Record<string, BehaviorSpec>;
}) {
  return (
    <section className="wire-battlefield-site" aria-label={sideLabel}>
      <div className="wire-mini-title">{sideLabel}</div>
      <div className="wire-battlefield-site-body">
        {lane.cardNo ? (
          <CardFace
            compact
            object={{ cardNo: lane.cardNo, controllerId: lane.controllerId, objectId: lane.battlefieldId, ownerId: lane.zonePlayerId }}
            objectId={lane.battlefieldId}
            onInspect={onInspectCard}
            spec={specs[lane.cardNo]}
          />
        ) : (
          <WireCardSlot label="战场" />
        )}
      </div>
    </section>
  );
}

function WireBattlefieldUnitZone({
  ids,
  objects,
  onInspectCard,
  specs,
  title
}: {
  ids: string[];
  objects: Record<string, CardObjectView>;
  onInspectCard: (card: InspectedCard) => void;
  specs: Record<string, BehaviorSpec>;
  title: string;
}) {
  return (
    <section className="wire-battlefield-unit-zone" aria-label={title}>
      <div className="wire-mini-title">{title}</div>
      <WireBattlefieldSlotRow ids={ids} objects={objects} onInspectCard={onInspectCard} specs={specs} />
    </section>
  );
}

function WireBattlefieldSlotRow({
  ids,
  objects,
  onInspectCard,
  specs
}: {
  ids: string[];
  objects: Record<string, CardObjectView>;
  onInspectCard: (card: InspectedCard) => void;
  specs: Record<string, BehaviorSpec>;
}) {
  const slotCount = Math.max(3, ids.length);

  return (
    <div className="wire-battlefield-slot-row wire-card-scroll-grid">
      {Array.from({ length: slotCount }, (_, index) => {
        const id = ids[index];
        if (!id) {
          return <WireCardSlot key={`empty-unit-slot-${index}`} label="" />;
        }

        const object = objects[id] ?? hiddenObject(id);
        return <CardFace compact key={id} object={object} objectId={id} onInspect={onInspectCard} spec={object.cardNo ? specs[object.cardNo] : undefined} />;
      })}
    </div>
  );
}

function WireZone({ children, className = "", title }: { children: ReactNode; className?: string; title: string }) {
  return (
    <section className={`wire-zone ${className}`} aria-label={title}>
      <div className="wire-zone-title">{title}</div>
      <div className="wire-zone-body">{children}</div>
    </section>
  );
}

function WireCardStrip({
  ids,
  objects,
  onInspectCard,
  specs
}: {
  ids: string[];
  objects: ZoneObjects | Record<string, CardObjectView>;
  onInspectCard: (card: InspectedCard) => void;
  specs: Record<string, BehaviorSpec>;
}) {
  return (
    <div className="wire-card-row">
      {ids.length === 0 && <WireEmpty label="空" />}
      {ids.map((id) => {
        const object = objects[id] ?? hiddenObject(id);
        return <CardFace compact key={id} object={object} objectId={id} onInspect={onInspectCard} spec={object.cardNo ? specs[object.cardNo] : undefined} />;
      })}
    </div>
  );
}

function WireFixedCardStrip({
  className = "",
  emptyLabel,
  ids,
  objects,
  onInspectCard,
  specs
}: {
  className?: string;
  emptyLabel: string;
  ids: string[];
  objects: ZoneObjects | Record<string, CardObjectView>;
  onInspectCard: (card: InspectedCard) => void;
  specs: Record<string, BehaviorSpec>;
}) {
  return (
    <div className={`wire-card-row ${className}`.trim()}>
      {ids.length === 0 && <WireCardSlot label={emptyLabel} />}
      {ids.map((id) => {
        const object = objects[id] ?? hiddenObject(id);
        return <CardFace compact key={id} object={object} objectId={id} onInspect={onInspectCard} spec={object.cardNo ? specs[object.cardNo] : undefined} />;
      })}
    </div>
  );
}

function WireRuneTrack({
  ids,
  objects,
  onInspectCard,
  reverse = false,
  specs
}: {
  ids: string[];
  objects: ZoneObjects;
  onInspectCard: (card: InspectedCard) => void;
  reverse?: boolean;
  specs: Record<string, BehaviorSpec>;
}) {
  const slotIndexes = Array.from({ length: 12 }, (_, index) => reverse ? 11 - index : index);

  return (
    <div className="wire-rune-track" aria-label="12 个符文槽">
      {slotIndexes.map((slotIndex) => {
        const id = ids[slotIndex];
        const object = id ? objects[id] : undefined;
        const exhausted = Boolean(object?.isExhausted);
        return (
          <div className={`wire-rune-slot ${exhausted ? "is-exhausted" : ""}`} key={id ?? `rune-slot-${slotIndex}`}>
            {id ? (
              <div
                aria-label={exhausted ? "已横置符文" : "竖置符文"}
                className={`wire-rune-card-frame ${exhausted ? (reverse ? "is-exhausted-counter" : "is-exhausted-clockwise") : ""}`}
              >
                <CardFace compact object={object} objectId={id} onInspect={onInspectCard} spec={object?.cardNo ? specs[object.cardNo] : undefined} />
              </div>
            ) : (
              <span>{slotIndex + 1}</span>
            )}
          </div>
        );
      })}
    </div>
  );
}

function WireCardSlot({ label }: { label: string }) {
  return (
    <div className="wire-card-slot" aria-label={`${label}空槽`}>
      <span>{label}</span>
    </div>
  );
}

function WirePublicPile({
  ids,
  label,
  objects,
  onInspectCard,
  specs
}: {
  ids: string[];
  label: string;
  objects: ZoneObjects;
  onInspectCard: (card: InspectedCard) => void;
  specs: Record<string, BehaviorSpec>;
}) {
  const topId = ids.at(-1);

  return (
    <div className="wire-stack-count" role="group" aria-label={`${label} ${ids.length} 张`}>
      {topId ? (
        <CardFace compact object={objects[topId]} objectId={topId} onInspect={onInspectCard} spec={objects[topId]?.cardNo ? specs[objects[topId].cardNo] : undefined} />
      ) : (
        <div className="wire-stack-box" aria-hidden="true" />
      )}
    </div>
  );
}

function WireStackCount({ count, label }: { count: number; label: string }) {
  return (
    <div className="wire-stack-count" role="group" aria-label={`${label} ${count} 张`}>
      <div className="wire-stack-box" aria-hidden="true" />
    </div>
  );
}

function WireEmpty({ label }: { label: string }) {
  return <span className="wire-empty">{label}</span>;
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

function ownerOrController(object?: CardObjectView): string {
  return object?.controllerId || object?.ownerId || "";
}

function playerLabel(entry: PlayerEntry): string {
  return `${entry.side === "self" ? "P1 我方" : "P2 对手"} · ${entry.player.name ?? entry.id}`;
}

function hiddenCards(count: number, playerId: string): string[] {
  return Array.from({ length: count }, (_, index) => `hidden-${playerId}-${index}`);
}

function hiddenObject(objectId: string): CardObjectView {
  return { objectId, isFaceDown: true };
}

function isRuneCard(object?: CardObjectView, spec?: BehaviorSpec): boolean {
  return Boolean(object?.tags?.includes("CARD_TYPE:RUNE") || spec?.cardCategoryName === "符文");
}

import { type CSSProperties, type ReactNode } from "react";
import { BehaviorSpec } from "../../types/catalog";
import { CardObjectView, PlayerSnapshotView } from "../../types/protocol";
import { runePoolText, runeTraitLabel } from "../../utils/formatters";
import { CardFace, InspectedCard } from "../cards/CardFace";
import { LayoutBox, PLAYER_TABLE_LAYOUT, RUNE_DECK_SIZE, TableSide } from "./tabletopLayout";

type PlayerBoardProps = {
  playerId: string;
  player: PlayerSnapshotView;
  perspectivePlayerId: string;
  specs: Record<string, BehaviorSpec>;
  onInspectCard: (card: InspectedCard) => void;
};

export function PlayerBoard({ playerId, player, perspectivePlayerId, specs, onInspectCard }: PlayerBoardProps) {
  const own = playerId === perspectivePlayerId;
  const side: TableSide = own ? "self" : "opponent";
  const layout = PLAYER_TABLE_LAYOUT[side];
  const zones = player.zones ?? {};
  const objects = player.objects ?? {};
  const legendIds = zones.legendZone ?? [];
  const championIds = zones.championZone ?? [];
  const graveyardIds = zones.graveyard ?? [];
  const banishedIds = zones.banished ?? [];
  const battlefieldDeckIds = zones.battlefields ?? [];
  const baseIds = zones.base ?? [];
  const runeIds = baseIds.filter((id) => isRuneCard(objects[id], specs[objects[id]?.cardNo ?? ""]));
  const baseObjectIds = baseIds.filter((id) => !runeIds.includes(id));

  return (
    <section className={`player-board tabletop-player-board player-${side}`}>
      <div className="tabletop-player-surface" data-side={side}>
        <TabletopSlot box={layout.legend} className="tabletop-signature-slot" label="传奇">
          <SingleCardZone ids={legendIds} objects={objects} onInspectCard={onInspectCard} specs={specs} title="传奇" />
        </TabletopSlot>
        <TabletopSlot box={layout.champion} className="tabletop-signature-slot" label="英雄">
          <SingleCardZone ids={championIds} objects={objects} onInspectCard={onInspectCard} specs={specs} title="英雄" />
        </TabletopSlot>
        <TabletopSlot box={layout.piles} className="tabletop-pile-bank" label="牌堆">
          <DeckCounterSlot count={zones.mainDeckCount ?? 0} title="主牌堆" />
          <PublicPileSlot ids={graveyardIds} objects={objects} onInspectCard={onInspectCard} specs={specs} title="废牌堆" />
          <PublicPileSlot ids={banishedIds} objects={objects} onInspectCard={onInspectCard} specs={specs} title="放逐区" />
          <DeckCounterSlot count={battlefieldDeckIds.length} title="战场池" variant="battlefield" />
        </TabletopSlot>
        <TabletopSlot box={layout.score} className="tabletop-score-bank" label={`${player.name ?? playerId} 分数`}>
          <PlayerScoreToken player={player} playerId={playerId} />
        </TabletopSlot>
        <TabletopSlot box={layout.base} className="tabletop-base-slot" label="基地">
          <BaseZone
            baseIds={baseObjectIds}
            objects={objects}
            onInspectCard={onInspectCard}
            runePool={player.runePool}
            specs={specs}
          />
        </TabletopSlot>
        <TabletopSlot box={layout.runeBank} className="tabletop-rune-bank-slot" label="符文堆与符文槽">
          <RuneBank
            objects={objects}
            onInspectCard={onInspectCard}
            player={player}
            playerId={playerId}
            runeDeckCount={zones.runeDeckCount ?? RUNE_DECK_SIZE}
            runeIds={runeIds}
            specs={specs}
          />
        </TabletopSlot>
        <TabletopSlot box={layout.hand} className="tabletop-hand-slot" label={own ? "手牌" : "对手手牌"}>
          <ZoneStrip
            className="zone-hand"
            onInspectCard={onInspectCard}
            title={own ? "手牌" : "对手手牌"}
            ids={own ? zones.hand ?? [] : hiddenCards(player.handSize ?? zones.handHidden ?? 0)}
            objects={objects}
            specs={specs}
            compact
          />
        </TabletopSlot>
      </div>
    </section>
  );
}

function TabletopSlot({ box, children, className = "", label }: { box: LayoutBox; children: ReactNode; className?: string; label: string }) {
  return (
    <div aria-label={label} className={`tabletop-slot ${className}`} role="group" style={boxStyle(box)}>
      {children}
    </div>
  );
}

function boxStyle(box: LayoutBox): CSSProperties {
  return {
    left: `${box.x}%`,
    top: `${box.y}%`,
    width: `${box.width}%`,
    height: `${box.height}%`
  };
}

function PlayerScoreToken({ player, playerId }: { player: PlayerSnapshotView; playerId: string }) {
  const zones = player.zones ?? {};

  return (
    <div className="tabletop-score-token" role="group" aria-label={`${player.name ?? playerId} 分数 ${player.score ?? 0}`}>
      <div className="tabletop-score-name">{player.name ?? playerId}</div>
      <div className="tabletop-score-main">
        <span>牌堆</span>
        <strong>{player.score ?? 0}</strong>
      </div>
      <div className="tabletop-score-meta">主牌 {zones.mainDeckCount ?? 0} / 符文 {zones.runeDeckCount ?? 0}</div>
    </div>
  );
}

function RuneBank({
  objects,
  onInspectCard,
  player,
  playerId,
  runeDeckCount,
  runeIds,
  specs
}: {
  objects: NonNullable<PlayerSnapshotView["objects"]>;
  onInspectCard: (card: InspectedCard) => void;
  player: PlayerSnapshotView;
  playerId: string;
  runeDeckCount: number;
  runeIds: string[];
  specs: Record<string, BehaviorSpec>;
}) {
  const remaining = Math.max(0, Math.min(RUNE_DECK_SIZE, runeDeckCount));
  const drawnCount = Math.max(runeIds.length, RUNE_DECK_SIZE - remaining);
  const slots = Array.from({ length: RUNE_DECK_SIZE }, (_, index) => index);

  return (
    <section className="rune-bank self-rune-bank" aria-label={`${player.name ?? playerId} 符文堆与本局符文槽`}>
      <div className="rune-deck-stack" role="group" aria-label={`符文堆，共 ${RUNE_DECK_SIZE} 张，剩余 ${remaining} 张`}>
        <span>符文堆</span>
        <strong>{RUNE_DECK_SIZE}</strong>
        <small>剩余 {remaining}</small>
      </div>
      <div className="rune-bank-track" role="group" aria-label={`已抽出符文 ${drawnCount} 张，最多 ${RUNE_DECK_SIZE} 张`}>
        {slots.map((index) => {
          const id = runeIds[index];
          if (id) {
            const object = objects[id];
            return (
              <div className="rune-bank-card-slot is-revealed" key={id}>
                <CardFace compact object={object} objectId={id} onInspect={onInspectCard} spec={object?.cardNo ? specs[object.cardNo] : undefined} />
              </div>
            );
          }

          const drawn = index < drawnCount;
          return (
            <div aria-hidden="true" className={drawn ? "rune-bank-card-slot is-drawn" : "rune-bank-card-slot"} key={`rune-slot-${index}`}>
              <span>{drawn ? `符文 ${index + 1}` : ""}</span>
            </div>
          );
        })}
      </div>
      <div className="rune-bank-resource">
        <span>可用资源</span>
        <strong>{runePoolText(player.runePool)}</strong>
      </div>
    </section>
  );
}

function DeckCounterSlot({ count, title, variant = "deck" }: { count: number; title: string; variant?: "deck" | "battlefield" }) {
  return (
    <div className={`tts-pile-slot tts-pile-${variant}`} role="group" aria-label={`${title} ${count} 张`}>
      <div className="tts-card-stack" aria-hidden="true" />
      <span>{title}</span>
      <strong>{count}</strong>
    </div>
  );
}

function SingleCardZone({
  ids,
  objects,
  onInspectCard,
  specs,
  title
}: {
  ids: string[];
  objects: NonNullable<PlayerSnapshotView["objects"]>;
  onInspectCard: (card: InspectedCard) => void;
  specs: Record<string, BehaviorSpec>;
  title: string;
}) {
  const id = ids[0];
  const object = id ? objects[id] : undefined;

  return (
    <div className="single-card-zone">
      <div className="tabletop-zone-label">
        <strong>{title}</strong>
        <span>{ids.length}</span>
      </div>
      <div className="tabletop-card-socket">
        {id ? (
          <CardFace compact object={object} objectId={id} onInspect={onInspectCard} spec={object?.cardNo ? specs[object.cardNo] : undefined} />
        ) : (
          <span className="empty-hint">无</span>
        )}
      </div>
    </div>
  );
}

function PublicPileSlot({
  ids,
  objects,
  onInspectCard,
  specs,
  title
}: {
  ids: string[];
  objects: NonNullable<PlayerSnapshotView["objects"]>;
  onInspectCard: (card: InspectedCard) => void;
  specs: Record<string, BehaviorSpec>;
  title: string;
}) {
  const topId = ids.at(-1);
  const topObject = topId ? objects[topId] : undefined;

  return (
    <div className="tts-pile-slot tts-public-pile" role="group" aria-label={`${title} ${ids.length} 张`}>
      {topId ? (
        <CardFace compact object={topObject} objectId={topId} onInspect={onInspectCard} spec={topObject?.cardNo ? specs[topObject.cardNo] : undefined} />
      ) : (
        <div className="tts-empty-pile" aria-hidden="true" />
      )}
      <span>{title}</span>
      <strong>{ids.length}</strong>
    </div>
  );
}

function ZoneStrip({
  title,
  ids,
  objects,
  onInspectCard,
  specs,
  compact,
  className
}: {
  title: string;
  ids: string[];
  objects: NonNullable<PlayerSnapshotView["objects"]>;
  onInspectCard: (card: InspectedCard) => void;
  specs: Record<string, BehaviorSpec>;
  compact?: boolean;
  className?: string;
}) {
  return (
    <div className={`zone-strip ${className ?? ""}`}>
      <div className="zone-title">
        <strong>{title}</strong>
        <span>{ids.length}</span>
      </div>
      <div className="card-row">
        {ids.length === 0 && <span className="empty-hint">无公开对象</span>}
        {ids.map((id) => {
          const object = objects[id] ?? (id.startsWith("hidden-") ? { objectId: id, isFaceDown: true } : undefined);
          return <CardFace compact={compact} key={id} object={object} objectId={id} onInspect={onInspectCard} spec={object?.cardNo ? specs[object.cardNo] : undefined} />;
        })}
      </div>
    </div>
  );
}

function BaseZone({
  baseIds,
  objects,
  onInspectCard,
  runePool,
  specs
}: {
  baseIds: string[];
  objects: NonNullable<PlayerSnapshotView["objects"]>;
  onInspectCard: (card: InspectedCard) => void;
  runePool?: PlayerSnapshotView["runePool"];
  specs: Record<string, BehaviorSpec>;
}) {
  const traits = Object.entries(runePool?.powerByTrait ?? {}).filter(([, amount]) => amount > 0);

  return (
    <div className="zone-strip zone-base base-zone tabletop-base-zone">
      <div className="zone-title tabletop-zone-label">
        <strong>基地</strong>
        <span>待命 / 基地公开区 {baseIds.length}</span>
      </div>
      <div className="base-zone-layout">
        <div className="base-card-lane">
          {baseIds.length === 0 && <span className="empty-hint">基地牌会显示在这里</span>}
          {baseIds.map((id) => {
            const object = objects[id];
            return (
              <div className="card-slot base-card-slot" key={id}>
                <CardFace compact object={object} objectId={id} onInspect={onInspectCard} spec={object?.cardNo ? specs[object.cardNo] : undefined} />
              </div>
            );
          })}
        </div>
        <div className="base-resource-meter">
          <span>资源池</span>
          <strong>{runePoolText(runePool)}</strong>
          <div className="rune-trait-list">
            {traits.length === 0 ? (
              <span>暂无特性符能</span>
            ) : (
              traits.map(([trait, amount]) => <span key={trait}>{runeTraitLabel(trait)} {amount}</span>)
            )}
          </div>
        </div>
      </div>
    </div>
  );
}

function hiddenCards(count: number): string[] {
  return Array.from({ length: count }, (_, index) => `hidden-${index}`);
}

function isRuneCard(object?: CardObjectView, spec?: BehaviorSpec): boolean {
  return Boolean(object?.tags?.includes("CARD_TYPE:RUNE") || spec?.cardCategoryName === "符文");
}

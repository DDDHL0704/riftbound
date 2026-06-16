import { BehaviorSpec } from "../../types/catalog";
import { CardObjectView, PlayerSnapshotView } from "../../types/protocol";
import { runePoolText, runeTraitLabel } from "../../utils/formatters";
import { CardFace, InspectedCard } from "../cards/CardFace";
import { StatusPill } from "../ui/StatusPill";

type PlayerBoardProps = {
  playerId: string;
  player: PlayerSnapshotView;
  perspectivePlayerId: string;
  specs: Record<string, BehaviorSpec>;
  onInspectCard: (card: InspectedCard) => void;
};

export function PlayerBoard({ playerId, player, perspectivePlayerId, specs, onInspectCard }: PlayerBoardProps) {
  const own = playerId === perspectivePlayerId;
  const zones = player.zones ?? {};
  const objects = player.objects ?? {};
  const legendIds = zones.legendZone ?? [];
  const championIds = zones.championZone ?? [];
  const baseIds = zones.base ?? [];
  const runeIds = baseIds.filter((id) => isRuneCard(objects[id], specs[objects[id]?.cardNo ?? ""]));
  const baseObjectIds = baseIds.filter((id) => !runeIds.includes(id));
  const fieldObjects = (zones.battlefields ?? []).filter((id) => !isBattlefieldCard(objects[id]));

  return (
    <section className={`player-board ${own ? "player-self" : "player-opponent"}`}>
      <header>
        <div>
          <span className="eyebrow">{own ? "我方" : "对手"}</span>
          <h2>{player.name ?? playerId}</h2>
        </div>
        <div className="player-pills">
          <StatusPill tone="good">分数 {player.score ?? 0}/8</StatusPill>
          <StatusPill tone="info">经验 {player.experience ?? 0}</StatusPill>
          <StatusPill tone={player.ready ? "good" : "warn"}>{player.ready ? "已准备" : "未准备"}</StatusPill>
        </div>
      </header>
      <div className="player-board-meta">
        <div className="player-state-strip">
          <span>{player.deckSubmitted ? "卡组已提交" : "等待卡组"}</span>
          <span>{player.mulliganCompleted ? "起手已确认" : "起手未确认"}</span>
          <span>本回合已出牌 {player.cardsPlayedThisTurn ?? 0}</span>
        </div>
      </div>
      <div className="player-board-zones">
        <div className="player-board-primary-zones">
          <div className="signature-zones">
            <ZoneStrip className="zone-signature" onInspectCard={onInspectCard} title="传奇" ids={legendIds} objects={objects} specs={specs} compact />
            <ZoneStrip className="zone-signature" onInspectCard={onInspectCard} title="英雄" ids={championIds} objects={objects} specs={specs} compact />
          </div>
          <RuneSlot
            ids={runeIds}
            objects={objects}
            onInspectCard={onInspectCard}
            runeDeckCount={zones.runeDeckCount ?? 0}
            runePool={player.runePool}
            specs={specs}
          />
          <ZoneStrip className="zone-base" onInspectCard={onInspectCard} title="基地" ids={baseObjectIds} objects={objects} specs={specs} compact />
          <ZoneStrip className="zone-field" onInspectCard={onInspectCard} title="场上对象" ids={fieldObjects} objects={objects} specs={specs} compact />
        </div>
        <ZoneStrip
          className="zone-hand"
          onInspectCard={onInspectCard}
          title={own ? "手牌" : "对手手牌"}
          ids={own ? zones.hand ?? [] : hiddenCards(player.handSize ?? zones.handHidden ?? 0)}
          objects={objects}
          specs={specs}
          compact
        />
      </div>
      <div className="zone-counts">
        <span>主牌堆 {zones.mainDeckCount ?? 0}</span>
        <span>废牌堆 {zones.graveyard?.length ?? 0}</span>
        <span>放逐 {zones.banished?.length ?? 0}</span>
      </div>
    </section>
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

function RuneSlot({
  ids,
  objects,
  onInspectCard,
  runeDeckCount,
  runePool,
  specs
}: {
  ids: string[];
  objects: NonNullable<PlayerSnapshotView["objects"]>;
  onInspectCard: (card: InspectedCard) => void;
  runeDeckCount: number;
  runePool?: PlayerSnapshotView["runePool"];
  specs: Record<string, BehaviorSpec>;
}) {
  const traits = Object.entries(runePool?.powerByTrait ?? {}).filter(([, amount]) => amount > 0);

  return (
    <div className="zone-strip rune-slot">
      <div className="zone-title">
        <strong>符文区</strong>
        <span>已召出 {ids.length} / 牌堆 {runeDeckCount}</span>
      </div>
      <div className="rune-slot-body">
        <div className="rune-slot-meter">
          <span>可用资源池</span>
          <strong>{runePoolText(runePool)}</strong>
          <div className="rune-trait-list">
            {traits.length === 0 ? (
              <span>暂无可用特性符能</span>
            ) : (
              traits.map(([trait, amount]) => <span key={trait}>{runeTraitLabel(trait)} {amount}</span>)
            )}
          </div>
        </div>
        <div className="card-row rune-card-row">
          {ids.length === 0 && <span className="empty-hint">符文入场后会显示在这里</span>}
          {ids.map((id) => {
            const object = objects[id];
            return <CardFace compact key={id} object={object} objectId={id} onInspect={onInspectCard} spec={object?.cardNo ? specs[object.cardNo] : undefined} />;
          })}
        </div>
      </div>
    </div>
  );
}

function hiddenCards(count: number): string[] {
  return Array.from({ length: count }, (_, index) => `hidden-${index}`);
}

function isBattlefieldCard(object?: CardObjectView): boolean {
  return Boolean(object?.tags?.includes("CARD_TYPE:BATTLEFIELD"));
}

function isRuneCard(object?: CardObjectView, spec?: BehaviorSpec): boolean {
  return Boolean(object?.tags?.includes("CARD_TYPE:RUNE") || spec?.cardCategoryName === "符文");
}

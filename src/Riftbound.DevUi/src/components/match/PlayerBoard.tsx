import { BehaviorSpec } from "../../types/catalog";
import { PlayerSnapshotView } from "../../types/protocol";
import { runePoolText } from "../../utils/formatters";
import { CardFace } from "../cards/CardFace";
import { StatusPill } from "../ui/StatusPill";

type PlayerBoardProps = {
  playerId: string;
  player: PlayerSnapshotView;
  perspectivePlayerId: string;
  specs: Record<string, BehaviorSpec>;
};

export function PlayerBoard({ playerId, player, perspectivePlayerId, specs }: PlayerBoardProps) {
  const own = playerId === perspectivePlayerId;
  const zones = player.zones ?? {};
  const objects = player.objects ?? {};

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
      <div className="resource-line">{runePoolText(player.runePool)}</div>
      <ZoneStrip title="传奇" ids={zones.legendZone ?? []} objects={objects} specs={specs} compact />
      <ZoneStrip title="英雄" ids={zones.championZone ?? []} objects={objects} specs={specs} compact />
      <ZoneStrip title="基地" ids={zones.base ?? []} objects={objects} specs={specs} compact />
      <ZoneStrip title={own ? "手牌" : "对手手牌"} ids={own ? zones.hand ?? [] : hiddenCards(player.handSize ?? zones.handHidden ?? 0)} objects={objects} specs={specs} compact />
      <div className="zone-counts">
        <span>主牌堆 {zones.mainDeckCount ?? 0}</span>
        <span>符文牌堆 {zones.runeDeckCount ?? 0}</span>
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
  specs,
  compact
}: {
  title: string;
  ids: string[];
  objects: NonNullable<PlayerSnapshotView["objects"]>;
  specs: Record<string, BehaviorSpec>;
  compact?: boolean;
}) {
  return (
    <div className="zone-strip">
      <div className="zone-title">
        <strong>{title}</strong>
        <span>{ids.length}</span>
      </div>
      <div className="card-row">
        {ids.length === 0 && <span className="empty-hint">无公开对象</span>}
        {ids.map((id) => {
          const object = objects[id];
          return <CardFace compact={compact} key={id} object={object} objectId={id} spec={object?.cardNo ? specs[object.cardNo] : undefined} />;
        })}
      </div>
    </div>
  );
}

function hiddenCards(count: number): string[] {
  return Array.from({ length: count }, (_, index) => `hidden-${index}`);
}

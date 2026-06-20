import { type ReactNode, useCallback, useEffect, useMemo, useRef, useState } from "react";
import { AppRoute } from "../app/router";
import { CardDetailDrawer } from "../components/cards/CardDetailDrawer";
import { CardFace, InspectedCard } from "../components/cards/CardFace";
import { ActionPanel } from "../components/match/ActionPanel";
import { EventLog } from "../components/match/EventLog";
import { WireActionMapPanel } from "../components/match/WireActionMapPanel";
import { useDelayedWireCardPreview, WireCardPreview } from "../components/match/WireCardPreview";
import { WireInteractionPanel } from "../components/match/WireInteractionPanel";
import { WireRuleQueuePanel } from "../components/match/WireRuleQueuePanel";
import { WireTimelineDetailPanel, type WireTimelineDetail } from "../components/match/WireTimelineDetailPanel";
import { WireTurnWindowPanel } from "../components/match/WireTurnWindowPanel";
import {
  WireCardFlow,
  type WireCardFlowPlan,
  type WireTimelineObjectState,
  WireCardSlot,
  WirePublicPile,
  WireStackCount
} from "../components/match/wireCardFlow";
import {
  buildWireTableViewModel,
  playerLabel,
  type WireBattlefieldLane,
  type WireBattlefieldModel,
  type WirePlayerEntry,
  type WireZoneObjects
} from "../components/match/wireTableViewModel";
import {
  WIRE_TABLE_LAYOUT,
  wireGridColumnsStyle,
  wireGridTemplateStyle,
  wireMatchPageStyle,
  wireTableStyle
} from "../components/match/wireTableLayout";
import {
  buildWireInteractionMap,
  buildWireTimelineMap,
  candidateChoiceForObject,
  emptySelectionDraft,
  focusedCandidateSummaries,
  sourceCandidateForObject,
  updateSelectionDraft
} from "../components/match/wireTableInteractionModel";
import { Button } from "../components/ui/Button";
import { ScrollArea } from "../components/ui/ScrollArea";
import { buildWireLayoutFixtureEvents, buildWireLayoutFixturePrompt, buildWireLayoutFixtureSnapshot, isWireLayoutFixtureEnabled, wireLayoutFixtureSpecByNo } from "../fixtures/wireLayoutFixture";
import { useCatalog } from "../stores/catalogStore";
import { useSettings } from "../stores/settingsStore";
import { useMatchController } from "../stores/useMatchController";
import { BehaviorSpec } from "../types/catalog";
import { CardObjectView } from "../types/protocol";
import { asRecord, asString } from "../utils/collections";
import { connectionStatusLabel, matchPhaseLabel, timingStateLabel } from "../utils/formatters";
import type { CandidateSelectionDraft } from "../utils/candidateSelectionDraft";
import { buildPromptInteractionModel, type PromptObjectState } from "../utils/promptInteraction";
import { buildCardObjectIndex } from "../utils/snapshotObjectIndex";
import { buildTableObjectContextModel } from "../utils/tableObjectContext";

type WireTableInteraction = {
  interactionByObjectId: Record<string, PromptObjectState | undefined>;
  selectedObjectId?: string;
  timelineByObjectId: Record<string, WireTimelineObjectState | undefined>;
};

export function MatchPage({ matchId, onNavigate }: { matchId: string; onNavigate: (route: AppRoute) => void }) {
  const { settings } = useSettings();
  const { specByNo } = useCatalog();
  const controller = useMatchController(settings.serverUrl, matchId, settings.playerId);
  const snapshot = controller.state.snapshot;
  const [inspectedCard, setInspectedCard] = useState<InspectedCard | undefined>();
  const [detailCard, setDetailCard] = useState<InspectedCard | undefined>();
  const [selectionDraft, setSelectionDraft] = useState<CandidateSelectionDraft | undefined>();
  const [timelineDetail, setTimelineDetail] = useState<WireTimelineDetail | undefined>();
  const timelineDetailTriggerIdRef = useRef<string | undefined>(undefined);
  const { previewCard, queuePreviewCard } = useDelayedWireCardPreview();
  const layoutFixtureEnabled = useMemo(() => isWireLayoutFixtureEnabled(), []);
  const tableSnapshot = useMemo(
    () => layoutFixtureEnabled ? buildWireLayoutFixtureSnapshot(settings.playerId) : snapshot,
    [layoutFixtureEnabled, settings.playerId, snapshot]
  );
  const tablePrompt = useMemo(
    () => layoutFixtureEnabled ? buildWireLayoutFixturePrompt(settings.playerId) : controller.state.prompt,
    [controller.state.prompt, layoutFixtureEnabled, settings.playerId]
  );
  const tableEvents = useMemo(
    () => layoutFixtureEnabled ? buildWireLayoutFixtureEvents(settings.playerId) : controller.state.events,
    [controller.state.events, layoutFixtureEnabled, settings.playerId]
  );
  const tableSpecByNo = useMemo(
    () => layoutFixtureEnabled ? { ...wireLayoutFixtureSpecByNo, ...specByNo } : specByNo,
    [layoutFixtureEnabled, specByNo]
  );
  const tableObjectIndex = useMemo(() => buildCardObjectIndex(tableSnapshot), [tableSnapshot]);
  const tableConnectionStatus = layoutFixtureEnabled ? "connected" : controller.state.status;
  const tableView = useMemo(
    () => buildWireTableViewModel({
      perspectivePlayerId: settings.playerId,
      snapshot: tableSnapshot,
      specs: tableSpecByNo
    }),
    [settings.playerId, tableSnapshot, tableSpecByNo]
  );
  const tableObjectContextModel = useMemo(() => buildTableObjectContextModel({
    events: tableEvents,
    perspectivePlayerId: settings.playerId,
    prompt: tablePrompt,
    snapshot: tableSnapshot
  }), [settings.playerId, tableEvents, tablePrompt, tableSnapshot]);
  const promptInteraction = useMemo(() => buildPromptInteractionModel(tablePrompt), [tablePrompt]);
  const selectedObjectId = inspectedCard?.objectId ?? inspectedCard?.object?.objectId;
  const selectedObjectContext = selectedObjectId ? tableObjectContextModel.byId[selectedObjectId] : undefined;
  const detailObjectId = detailCard?.objectId ?? detailCard?.object?.objectId;
  const detailObjectContext = detailObjectId ? tableObjectContextModel.byId[detailObjectId] : undefined;
  const focusedSourceCandidates = useMemo(
    () => focusedCandidateSummaries(promptInteraction.candidates, selectedObjectId),
    [promptInteraction.candidates, selectedObjectId]
  );
  const tableInteraction = useMemo<WireTableInteraction>(() => ({
    interactionByObjectId: buildWireInteractionMap(promptInteraction, focusedSourceCandidates, selectedObjectId, selectionDraft),
    selectedObjectId,
    timelineByObjectId: buildWireTimelineMap(timelineDetail)
  }), [focusedSourceCandidates, promptInteraction, selectedObjectId, selectionDraft, timelineDetail]);

  const self = tableView.self;
  const opponent = tableView.opponent;
  const timing = asRecord(tableSnapshot?.timing);
  const turnWindow = asRecord(timing.turnWindow);
  const phase = asString(timing.phase, tableSnapshot?.turnState ?? "");
  const windowState = asString(turnWindow.state, asString(timing.timingState, ""));
  const roomStatus = asString(timing.roomStatus, "");
  const promptTitle = tablePrompt?.view?.title?.trim() || "无行动窗口";
  const canAct = Boolean(tablePrompt?.actionable && tablePrompt.playerId === settings.playerId);
  const inspectCard = useCallback((card: InspectedCard) => {
    const clickedObjectId = card.objectId ?? card.object?.objectId;
    if (!clickedObjectId) {
      setInspectedCard(card);
      setSelectionDraft(undefined);
      return;
    }

    const focusedSourceObjectId = inspectedCard?.objectId ?? inspectedCard?.object?.objectId;
    const selectedCandidateChoice = focusedSourceObjectId
      ? candidateChoiceForObject(focusedSourceCandidates, clickedObjectId)
      : undefined;
    if (focusedSourceObjectId && selectedCandidateChoice && selectedCandidateChoice.choice.role !== "source") {
      setSelectionDraft((current) => updateSelectionDraft(current, focusedSourceObjectId, selectedCandidateChoice.candidate, selectedCandidateChoice.choice));
      return;
    }

    setInspectedCard(card);
    const sourceCandidate = sourceCandidateForObject(promptInteraction.candidates, clickedObjectId);
    setSelectionDraft(sourceCandidate ? emptySelectionDraft(clickedObjectId, sourceCandidate) : undefined);
  }, [focusedSourceCandidates, inspectedCard?.object?.objectId, inspectedCard?.objectId, promptInteraction.candidates]);
  const inspectObjectFromTable = useCallback((objectId: string) => {
    const object = tableObjectIndex[objectId];
    if (!object) {
      return;
    }

    const sourceCandidate = sourceCandidateForObject(promptInteraction.candidates, objectId);
    setInspectedCard({
      object,
      objectId,
      spec: object.cardNo ? tableSpecByNo[object.cardNo] : undefined
    });
    setSelectionDraft(sourceCandidate ? emptySelectionDraft(objectId, sourceCandidate) : undefined);
  }, [promptInteraction.candidates, tableObjectIndex, tableSpecByNo]);
  const openObjectDetail = useCallback((objectId: string) => {
    const object = tableObjectIndex[objectId];
    if (!object) {
      return;
    }

    setDetailCard({
      object,
      objectId,
      spec: object.cardNo ? tableSpecByNo[object.cardNo] : undefined
    });
  }, [tableObjectIndex, tableSpecByNo]);
  const chooseObjectFromActionMap = useCallback((objectId: string) => {
    const object = tableObjectIndex[objectId];
    if (!object) {
      return;
    }

    inspectCard({
      object,
      objectId,
      spec: object.cardNo ? tableSpecByNo[object.cardNo] : undefined
    });
  }, [inspectCard, tableObjectIndex, tableSpecByNo]);
  const tableRows = WIRE_TABLE_LAYOUT.table.rows.map((row) => {
    if (row.kind === "battlefield") {
      return (
        <WireBattlefieldTable
          battlefield={tableView.battlefield}
          interaction={tableInteraction}
          key={row.id}
          onInspectCard={inspectCard}
          onPreviewCard={queuePreviewCard}
          specs={tableSpecByNo}
        />
      );
    }

    const entry = row.side === "self" ? self : opponent;
    if (row.kind === "handRail") {
      return (
        <WireHandRail
          entry={entry}
          fallbackSide={row.side}
          hidden={row.side === "opponent"}
          interaction={tableInteraction}
          key={row.id}
          onInspectCard={inspectCard}
          onPreviewCard={queuePreviewCard}
          specs={tableSpecByNo}
        />
      );
    }

    return (
      <WirePlayerHome
        entry={entry}
        fallbackSide={row.side}
        interaction={tableInteraction}
        key={row.id}
        onInspectCard={inspectCard}
        onPreviewCard={queuePreviewCard}
        specs={tableSpecByNo}
      />
    );
  });

  useEffect(() => {
    if (roomStatus === "FINISHED") {
      onNavigate({ name: "result", matchId });
    }
  }, [matchId, onNavigate, roomStatus]);

  useEffect(() => {
    setSelectionDraft(undefined);
  }, [tablePrompt?.promptId, tablePrompt?.snapshotTick]);

  useEffect(() => {
    setTimelineDetail(undefined);
    timelineDetailTriggerIdRef.current = undefined;
  }, [tableSnapshot?.tick]);

  const selectTimelineDetail = useCallback((detail: WireTimelineDetail) => {
    timelineDetailTriggerIdRef.current = detail.id;
    setTimelineDetail(detail);
  }, []);

  const clearTimelineDetail = useCallback(() => {
    const triggerId = timelineDetailTriggerIdRef.current ?? timelineDetail?.id;
    setTimelineDetail(undefined);
    if (!triggerId) {
      return;
    }

    window.setTimeout(() => {
      const trigger = Array.from(document.querySelectorAll<HTMLButtonElement>("[data-wire-detail-id]"))
        .find((button) => button.getAttribute("data-wire-detail-id") === triggerId);
      trigger?.focus();
    }, 0);
  }, [timelineDetail?.id]);

  return (
    <div className="wire-match-page" style={wireMatchPageStyle()}>
      <header className="wire-topbar" aria-label="对战基础状态">
        <div className="wire-topbar-title">
          <h1>符文战场对战线框</h1>
          <span>房间 {matchId}</span>
        </div>
        <div className="wire-status-line" role="group" aria-label="服务端状态">
          <span>连接 {connectionStatusLabel(tableConnectionStatus)}</span>
          <span>回合 {tableSnapshot?.turnNumber ?? 0}</span>
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

      <div className="wire-match-body">
        <section className="wire-table-shell" aria-label="黑白线框对战桌面">
          <div className="wire-table" style={wireTableStyle()}>{tableRows}</div>
        </section>

        <aside className="wire-side-panel" aria-label="行动与日志">
          <section aria-label="服务端窗口总览区" className="wire-panel wire-window-plan-panel" tabIndex={0}>
            <WireTurnWindowPanel
              connectionStatus={tableConnectionStatus}
              playerId={settings.playerId}
              prompt={tablePrompt}
              snapshot={tableSnapshot}
            />
          </section>
          <section aria-label="右侧合法操作区" className="wire-panel wire-action-map-panel" tabIndex={0}>
            <WireActionMapPanel
              onChooseObject={chooseObjectFromActionMap}
              onInspectObject={inspectObjectFromTable}
              playerId={settings.playerId}
              prompt={tablePrompt}
              selectedObjectId={selectedObjectId}
              selectionDraft={selectionDraft}
              snapshot={tableSnapshot}
            />
          </section>
          <section aria-label="焦点卡牌和候选行动" className="wire-panel" tabIndex={0}>
            <WireInteractionPanel
              disabledByConnection={tableConnectionStatus !== "connected"}
              inspectedCard={inspectedCard}
              onCommand={(command) => void controller.submitCommand(command)}
              onClearInspectedCard={() => {
                setInspectedCard(undefined);
                setSelectionDraft(undefined);
              }}
              onInspectObject={inspectObjectFromTable}
              onOpenDetail={setDetailCard}
              objectContext={selectedObjectContext}
              playerId={settings.playerId}
              prompt={tablePrompt}
              selectionDraft={selectionDraft}
              snapshot={tableSnapshot}
            />
          </section>
          <section aria-label="右侧规则队列区" className="wire-panel wire-rule-panel" tabIndex={0}>
            <WireRuleQueuePanel
              onInspectObject={inspectObjectFromTable}
              onSelectDetail={selectTimelineDetail}
              playerId={settings.playerId}
              prompt={tablePrompt}
              selectedDetailId={timelineDetail?.id}
              selectedObjectId={selectedObjectId}
              snapshot={tableSnapshot}
            />
          </section>
          <section aria-label="规则与事件详情区" className="wire-panel wire-timeline-detail-panel" tabIndex={0}>
            <WireTimelineDetailPanel
              detail={timelineDetail}
              objectContextById={tableObjectContextModel.byId}
              objectIndex={tableObjectIndex}
              onChooseObject={chooseObjectFromActionMap}
              onClear={clearTimelineDetail}
              onInspectObject={inspectObjectFromTable}
              onOpenObjectDetail={openObjectDetail}
              prompt={tablePrompt}
              selectionDraft={selectionDraft}
              selectedObjectContext={selectedObjectContext}
              selectedObjectId={selectedObjectId}
            />
          </section>
          <section aria-label="服务端行动提示" className="wire-panel wire-action-panel" tabIndex={0}>
            <ActionPanel
              connectionStatus={tableConnectionStatus}
              onCommand={(command) => void controller.submitCommand(command)}
              onReady={() => void controller.ready()}
              onSubmitStarterDeck={() => void controller.submitStarterDeck()}
              playerId={settings.playerId}
              prompt={tablePrompt}
              snapshot={tableSnapshot}
            />
          </section>
          <section aria-label="事件日志" className="wire-panel wire-log-panel" tabIndex={0}>
            <h2>日志</h2>
            <ScrollArea className="wire-log-scroll">
              <EventLog
                density={settings.logDensity}
                errors={controller.state.errors}
                events={tableEvents}
                objectIndex={tableObjectIndex}
                onInspectObject={inspectObjectFromTable}
                onSelectDetail={selectTimelineDetail}
                selectedDetailId={timelineDetail?.id}
                selectedObjectId={selectedObjectId}
              />
            </ScrollArea>
          </section>
        </aside>
      </div>
      <CardDetailDrawer
        card={detailCard}
        disabledByConnection={tableConnectionStatus !== "connected"}
        objectContext={detailObjectContext}
        onClose={() => setDetailCard(undefined)}
        onCommand={(command) => void controller.submitCommand(command)}
        prompt={tablePrompt}
        selectionDraft={selectionDraft}
        snapshot={tableSnapshot}
      />
      <WireCardPreview card={previewCard} />
    </div>
  );
}

function WirePlayerHome({
  entry,
  fallbackSide,
  interaction,
  onInspectCard,
  onPreviewCard,
  specs
}: {
  entry?: WirePlayerEntry;
  fallbackSide: WirePlayerEntry["side"];
  interaction: WireTableInteraction;
  onInspectCard: (card: InspectedCard) => void;
  onPreviewCard: (card?: InspectedCard) => void;
  specs: Record<string, BehaviorSpec>;
}) {
  const side = entry?.side ?? fallbackSide;
  const layout = WIRE_TABLE_LAYOUT.playerHomes[side];
  const zones = entry?.zones ?? {};
  const objects = entry?.objects ?? {};
  const baseObjectIds = entry?.baseObjectIds ?? [];
  const ownerLabel = entry?.label ?? (side === "self" ? "P1 我方" : "P2 对手");
  const baseSections = {
    banish: (
      <section className="wire-banish-main" key="banish" aria-label={`${ownerLabel} 放逐区`}>
        <WirePublicPile ids={zones.banished ?? []} interactionByObjectId={interaction.interactionByObjectId} kind="banished" label="放逐" objects={objects} onInspectCard={onInspectCard} onPreviewCard={onPreviewCard} selectedObjectId={interaction.selectedObjectId} specs={specs} timelineByObjectId={interaction.timelineByObjectId} />
      </section>
    ),
    base: (
      <section className="wire-base-main" key="base" aria-label={`${ownerLabel} 基地`}>
        <WireCardFlow className="wire-base-card-grid" emptyLabel="基地" ids={baseObjectIds} interactionByObjectId={interaction.interactionByObjectId} kind="base" minSlots={1} objects={objects} onInspectCard={onInspectCard} onPreviewCard={onPreviewCard} renderEmptySlots selectedObjectId={interaction.selectedObjectId} specs={specs} timelineByObjectId={interaction.timelineByObjectId} />
      </section>
    )
  } satisfies Record<string, ReactNode>;
  const homeSections = {
    base: (
      <WireZone className="wire-home-base" key="base" title={`${ownerLabel} 基地 / 放逐`}>
        <div className="wire-base-banish-grid" style={wireGridColumnsStyle(layout.baseColumns)}>
          {layout.baseSlots.map((slot) => baseSections[slot])}
        </div>
      </WireZone>
    ),
    hero: (
      <WireZone className="wire-home-hero wire-signature-zone" key="hero" title={`${ownerLabel} 英雄`}>
        <WireCardFlow emptyLabel="英雄" ids={zones.championZone ?? []} interactionByObjectId={interaction.interactionByObjectId} kind="signature" minSlots={1} objects={objects} onInspectCard={onInspectCard} onPreviewCard={onPreviewCard} renderEmptySlots selectedObjectId={interaction.selectedObjectId} specs={specs} timelineByObjectId={interaction.timelineByObjectId} />
      </WireZone>
    ),
    legend: (
      <WireZone className="wire-home-legend wire-signature-zone" key="legend" title={`${ownerLabel} 传奇`}>
        <WireCardFlow emptyLabel="传奇" ids={zones.legendZone ?? []} interactionByObjectId={interaction.interactionByObjectId} kind="signature" minSlots={1} objects={objects} onInspectCard={onInspectCard} onPreviewCard={onPreviewCard} renderEmptySlots selectedObjectId={interaction.selectedObjectId} specs={specs} timelineByObjectId={interaction.timelineByObjectId} />
      </WireZone>
    )
  } satisfies Record<string, ReactNode>;

  return (
    <section className={`wire-player-home wire-player-${side} ${entry ? "" : "wire-player-missing"}`} style={wireGridColumnsStyle(layout.columns)} aria-label={`${ownerLabel} 基础区`} data-wire-base-partition-source={entry?.basePartitionSource ?? "missing"}>
      {layout.slots.map((slot) => homeSections[slot])}
    </section>
  );
}

function WireHandRail({
  entry,
  fallbackSide,
  hidden = false,
  interaction,
  onInspectCard,
  onPreviewCard,
  specs
}: {
  entry?: WirePlayerEntry;
  fallbackSide: WirePlayerEntry["side"];
  hidden?: boolean;
  interaction: WireTableInteraction;
  onInspectCard: (card: InspectedCard) => void;
  onPreviewCard?: (card?: InspectedCard) => void;
  specs: Record<string, BehaviorSpec>;
}) {
  const side = entry?.side ?? fallbackSide;
  const layout = WIRE_TABLE_LAYOUT.handRails[side];
  const zones = entry?.zones ?? {};
  const runeIds = entry?.runeIds ?? [];
  const ids = entry ? hidden ? entry.hiddenHandIds : entry.handIds : [];
  const emptyObjects: Record<string, CardObjectView> = {};
  const zoneObjects = entry ? entry.objects : emptyObjects;
  const pileSections = {
    library: (
      <div className="wire-hand-library-pile" key="library">
        <WireStackCount count={zones.mainDeckCount ?? 0} kind="library" label="牌库" />
      </div>
    ),
    played: (
      <div className="wire-hand-played-pile" key="played">
        <WirePublicPile ids={zones.graveyard ?? []} interactionByObjectId={interaction.interactionByObjectId} kind="graveyard" label="已打出" objects={zoneObjects} onInspectCard={onInspectCard} onPreviewCard={onPreviewCard} selectedObjectId={interaction.selectedObjectId} specs={specs} timelineByObjectId={interaction.timelineByObjectId} />
      </div>
    )
  } satisfies Record<string, ReactNode>;
  const handBodySections = {
    cards: (
      <div className="wire-hand-cards" key="cards">
        <WireCardFlow ids={ids} interactionByObjectId={interaction.interactionByObjectId} kind="hand" objects={zoneObjects} onInspectCard={onInspectCard} onPreviewCard={onPreviewCard} selectedObjectId={interaction.selectedObjectId} specs={specs} timelineByObjectId={interaction.timelineByObjectId} />
      </div>
    ),
    piles: (
      <div className="wire-hand-piles" key="piles">
        {layout.pileSlots.map((slot) => pileSections[slot])}
      </div>
    )
  } satisfies Record<string, ReactNode>;
  const handRailSections = {
    hand: (
      <div className="wire-hand-zone" key="hand">
        <div className="wire-hand-body" style={wireGridColumnsStyle(layout.handBodyColumns)}>
          {layout.handBodySlots.map((slot) => handBodySections[slot])}
        </div>
      </div>
    ),
    runeDeck: (
      <div className="wire-hand-rune-deck" key="runeDeck">
        <WireStackCount count={zones.runeDeckCount ?? WIRE_TABLE_LAYOUT.runeDeckSize} kind="runeDeck" label="符文牌堆" />
      </div>
    ),
    runeTrack: (
      <div className="wire-hand-rune-track" key="runeTrack" role="group" aria-label={entry ? `${playerLabel(entry)} 已抽出符文` : "已抽出符文占位"}>
        <WireRuneTrack ids={runeIds} interaction={interaction} objects={zoneObjects} onInspectCard={onInspectCard} onPreviewCard={onPreviewCard} reverse={layout.runeReverse} specs={specs} />
      </div>
    )
  } satisfies Record<string, ReactNode>;

  return (
    <section className={`wire-hand-rail wire-hand-${side} ${entry ? "" : "wire-hand-missing"}`} style={wireGridColumnsStyle(layout.columns)} aria-label={entry ? `${playerLabel(entry)} 手牌` : `${side === "self" ? "P1 我方" : "P2 对手"} 手牌`}>
      {layout.slots.map((slot) => handRailSections[slot])}
    </section>
  );
}

function WireBattlefieldTable({
  battlefield,
  interaction,
  onInspectCard,
  onPreviewCard,
  specs
}: {
  battlefield: WireBattlefieldModel;
  interaction: WireTableInteraction;
  onInspectCard: (card: InspectedCard) => void;
  onPreviewCard: (card?: InspectedCard) => void;
  specs: Record<string, BehaviorSpec>;
}) {
  const objects = battlefield.objects;
  const lanes = battlefield.lanes;
  const layout = WIRE_TABLE_LAYOUT.battlefield;
  const unitPlan = battlefield.unitPlan;
  const battlefieldSections = {
    center: (
      <div className="wire-battlefield-center-grid" key="center" style={wireGridTemplateStyle(layout.centerColumns, layout.centerRows)}>
        {layout.unitZones.map((zone) => {
          const lane = lanes[zone.laneIndex];
          const ids = zone.side === "self" ? lane.ownOccupants : lane.opposingOccupants;
          return (
            <WireBattlefieldUnitZone
              ids={ids}
              interaction={interaction}
              key={zone.id}
              objects={objects}
              onInspectCard={onInspectCard}
              onPreviewCard={onPreviewCard}
              plan={unitPlan}
              splitSource={lane.occupantSplitSource}
              specs={specs}
              title={battlefieldUnitZoneLabel(zone.laneIndex, zone.side)}
            />
          );
        })}
      </div>
    ),
    leftSite: (
      <WireBattlefieldSite interaction={interaction} key="leftSite" lane={lanes[0]} onInspectCard={onInspectCard} onPreviewCard={onPreviewCard} sideLabel="左战场牌" specs={specs} />
    ),
    rightSite: (
      <WireBattlefieldSite interaction={interaction} key="rightSite" lane={lanes[1]} onInspectCard={onInspectCard} onPreviewCard={onPreviewCard} sideLabel="右战场牌" specs={specs} />
    )
  } satisfies Record<string, ReactNode>;

  return (
    <section className="wire-battlefield-stack" style={wireGridColumnsStyle(layout.columns)} aria-label="公共战场">
      {layout.slots.map((slot) => battlefieldSections[slot])}
    </section>
  );
}

function battlefieldUnitZoneLabel(laneIndex: number, side: "opponent" | "self"): string {
  const laneName = laneIndex === 0 ? "左战场" : "右战场";
  const sideName = side === "self" ? "我方" : "对方";
  return `${laneName} / ${sideName}`;
}

function WireBattlefieldSite({
  interaction,
  lane,
  onInspectCard,
  onPreviewCard,
  sideLabel,
  specs
}: {
  interaction: WireTableInteraction;
  lane: WireBattlefieldLane;
  onInspectCard: (card: InspectedCard) => void;
  onPreviewCard: (card?: InspectedCard) => void;
  sideLabel: string;
  specs: Record<string, BehaviorSpec>;
}) {
  return (
    <section className="wire-battlefield-site" aria-label={sideLabel}>
      <div className="wire-battlefield-site-body wire-density-single">
        {lane.cardNo ? (
          <CardFace
            compact
            interactionState={interaction.interactionByObjectId[lane.battlefieldId]}
            object={{ cardNo: lane.cardNo, controllerId: lane.controllerId, objectId: lane.battlefieldId, ownerId: lane.zonePlayerId }}
            objectId={lane.battlefieldId}
            onInspect={onInspectCard}
            onPreview={onPreviewCard}
            selected={interaction.selectedObjectId === lane.battlefieldId}
            spec={specs[lane.cardNo]}
            timelineState={interaction.timelineByObjectId[lane.battlefieldId]}
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
  interaction,
  objects,
  onInspectCard,
  onPreviewCard,
  plan,
  splitSource,
  specs,
  title
}: {
  ids: string[];
  interaction: WireTableInteraction;
  objects: WireZoneObjects;
  onInspectCard: (card: InspectedCard) => void;
  onPreviewCard: (card?: InspectedCard) => void;
  plan: WireCardFlowPlan;
  splitSource: WireBattlefieldLane["occupantSplitSource"];
  specs: Record<string, BehaviorSpec>;
  title: string;
}) {
  return (
    <section className="wire-battlefield-unit-zone" aria-label={title} data-wire-battlefield-split-source={splitSource}>
      <WireCardFlow ids={ids} interactionByObjectId={interaction.interactionByObjectId} kind="battlefield-unit" minSlots={3} objects={objects} onInspectCard={onInspectCard} onPreviewCard={onPreviewCard} plan={plan} renderEmptySlots selectedObjectId={interaction.selectedObjectId} specs={specs} timelineByObjectId={interaction.timelineByObjectId} />
    </section>
  );
}

function WireZone({ children, className = "", title }: { children: ReactNode; className?: string; title: string }) {
  return (
    <section className={`wire-zone ${className}`} aria-label={title}>
      <div className="wire-zone-body">{children}</div>
    </section>
  );
}

function WireRuneTrack({
  ids,
  interaction,
  objects,
  onInspectCard,
  onPreviewCard,
  reverse = false,
  specs
}: {
  ids: string[];
  interaction: WireTableInteraction;
  objects: WireZoneObjects;
  onInspectCard: (card: InspectedCard) => void;
  onPreviewCard?: (card?: InspectedCard) => void;
  reverse?: boolean;
  specs: Record<string, BehaviorSpec>;
}) {
  const runeDeckSize = WIRE_TABLE_LAYOUT.runeDeckSize;
  const slotIndexes = Array.from({ length: runeDeckSize }, (_, index) => reverse ? runeDeckSize - 1 - index : index);

  return (
    <div className="wire-rune-track" role="group" aria-label="12 个符文槽">
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
                role="group"
              >
                <CardFace compact interactionState={interaction.interactionByObjectId[id]} object={object} objectId={id} onInspect={onInspectCard} onPreview={onPreviewCard} selected={interaction.selectedObjectId === id} spec={object?.cardNo ? specs[object.cardNo] : undefined} timelineState={interaction.timelineByObjectId[id]} />
              </div>
            ) : (
              <span aria-hidden="true" />
            )}
          </div>
        );
      })}
    </div>
  );
}

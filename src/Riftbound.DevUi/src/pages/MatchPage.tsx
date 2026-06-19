import { type ReactNode, useCallback, useEffect, useMemo, useRef, useState } from "react";
import { AppRoute } from "../app/router";
import { CardDetailDrawer } from "../components/cards/CardDetailDrawer";
import { CardFace, InspectedCard } from "../components/cards/CardFace";
import { candidateComposerKey, type CandidateSelectionDraft } from "../components/match/CandidateComposer";
import { ActionPanel } from "../components/match/ActionPanel";
import { EventLog } from "../components/match/EventLog";
import { WireActionMapPanel } from "../components/match/WireActionMapPanel";
import { WireInteractionPanel } from "../components/match/WireInteractionPanel";
import { WireRuleQueuePanel } from "../components/match/WireRuleQueuePanel";
import {
  buildWireCardFlowPlan,
  WireCardFlow,
  type WireCardFlowPlan,
  WireCardSlot,
  WirePublicPile,
  WireStackCount
} from "../components/match/wireCardFlow";
import {
  WIRE_TABLE_LAYOUT,
  wireGridColumnsStyle,
  wireGridTemplateStyle,
  wireMatchPageStyle,
  wireTableStyle
} from "../components/match/wireTableLayout";
import { Button } from "../components/ui/Button";
import { ScrollArea } from "../components/ui/ScrollArea";
import { buildWireLayoutFixturePrompt, buildWireLayoutFixtureSnapshot, isWireLayoutFixtureEnabled, wireLayoutFixtureSpecByNo } from "../fixtures/wireLayoutFixture";
import { useCatalog } from "../stores/catalogStore";
import { useSettings } from "../stores/settingsStore";
import { useMatchController } from "../stores/useMatchController";
import { BehaviorSpec } from "../types/catalog";
import { BattlefieldSnapshotView, CardObjectView, PlayerSnapshotView, SnapshotDto } from "../types/protocol";
import { asArray, asRecord, asString } from "../utils/collections";
import { connectionStatusLabel, matchPhaseLabel, timingStateLabel } from "../utils/formatters";
import { buildPromptInteractionModel, promptChoiceSummaryObjectIds, type PromptCandidateSummary, type PromptChoiceRole, type PromptChoiceSummary, type PromptObjectState } from "../utils/promptInteraction";
import { buildCardObjectIndex } from "../utils/snapshotObjectIndex";

type PlayerEntry = {
  id: string;
  player: PlayerSnapshotView;
  side: "self" | "opponent";
};

type ZoneObjects = NonNullable<PlayerSnapshotView["objects"]>;
type WireTableInteraction = {
  interactionByObjectId: Record<string, PromptObjectState | undefined>;
  selectedObjectId?: string;
};

export function MatchPage({ matchId, onNavigate }: { matchId: string; onNavigate: (route: AppRoute) => void }) {
  const { settings } = useSettings();
  const { specByNo } = useCatalog();
  const controller = useMatchController(settings.serverUrl, matchId, settings.playerId);
  const snapshot = controller.state.snapshot;
  const [inspectedCard, setInspectedCard] = useState<InspectedCard | undefined>();
  const [detailCard, setDetailCard] = useState<InspectedCard | undefined>();
  const [previewCard, setPreviewCard] = useState<InspectedCard | undefined>();
  const [selectionDraft, setSelectionDraft] = useState<CandidateSelectionDraft | undefined>();
  const previewDelayRef = useRef<number | undefined>(undefined);
  const layoutFixtureEnabled = useMemo(() => isWireLayoutFixtureEnabled(), []);
  const tableSnapshot = useMemo(
    () => layoutFixtureEnabled ? buildWireLayoutFixtureSnapshot(settings.playerId) : snapshot,
    [layoutFixtureEnabled, settings.playerId, snapshot]
  );
  const tablePrompt = useMemo(
    () => layoutFixtureEnabled ? buildWireLayoutFixturePrompt(settings.playerId) : controller.state.prompt,
    [controller.state.prompt, layoutFixtureEnabled, settings.playerId]
  );
  const tableSpecByNo = useMemo(
    () => layoutFixtureEnabled ? { ...wireLayoutFixtureSpecByNo, ...specByNo } : specByNo,
    [layoutFixtureEnabled, specByNo]
  );
  const tableObjectIndex = useMemo(() => buildCardObjectIndex(tableSnapshot), [tableSnapshot]);
  const promptInteraction = useMemo(() => buildPromptInteractionModel(tablePrompt), [tablePrompt]);
  const focusedSourceCandidates = useMemo(
    () => focusedCandidateSummaries(promptInteraction.candidates, inspectedCard?.objectId),
    [inspectedCard?.objectId, promptInteraction.candidates]
  );
  const tableInteraction = useMemo<WireTableInteraction>(() => ({
    interactionByObjectId: buildWireInteractionMap(promptInteraction, focusedSourceCandidates, inspectedCard?.objectId, selectionDraft),
    selectedObjectId: inspectedCard?.objectId
  }), [focusedSourceCandidates, inspectedCard?.objectId, promptInteraction, selectionDraft]);

  const playerEntries = useMemo(() => buildPlayerEntries(tableSnapshot, settings.playerId), [tableSnapshot, settings.playerId]);
  const self = playerEntries.find((entry) => entry.side === "self");
  const opponent = playerEntries.find((entry) => entry.side === "opponent");
  const battlefields = useMemo(() => asArray<BattlefieldSnapshotView>(asRecord(tableSnapshot?.lanes).battlefields), [tableSnapshot?.lanes]);
  const timing = asRecord(tableSnapshot?.timing);
  const turnWindow = asRecord(timing.turnWindow);
  const phase = asString(timing.phase, tableSnapshot?.turnState ?? "");
  const windowState = asString(turnWindow.state, asString(timing.timingState, ""));
  const roomStatus = asString(timing.roomStatus, "");
  const promptTitle = tablePrompt?.view?.title?.trim() || "无行动窗口";
  const canAct = Boolean(tablePrompt?.actionable && tablePrompt.playerId === settings.playerId);
  const queuePreviewCard = useCallback((card?: InspectedCard) => {
    if (previewDelayRef.current != null) {
      window.clearTimeout(previewDelayRef.current);
      previewDelayRef.current = undefined;
    }

    if (!card) {
      setPreviewCard(undefined);
      return;
    }

    previewDelayRef.current = window.setTimeout(() => {
      setPreviewCard(card);
      previewDelayRef.current = undefined;
    }, 520);
  }, []);
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
  const tableRows = WIRE_TABLE_LAYOUT.table.rows.map((row) => {
    if (row.kind === "battlefield") {
      return (
        <WireBattlefieldTable
          battlefields={battlefields}
          interaction={tableInteraction}
          key={row.id}
          onInspectCard={inspectCard}
          onPreviewCard={queuePreviewCard}
          perspectivePlayerId={settings.playerId}
          snapshot={tableSnapshot}
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
    return () => {
      if (previewDelayRef.current != null) {
        window.clearTimeout(previewDelayRef.current);
      }
    };
  }, []);

  useEffect(() => {
    setSelectionDraft(undefined);
  }, [tablePrompt?.promptId, tablePrompt?.snapshotTick]);

  return (
    <div className="wire-match-page" style={wireMatchPageStyle()}>
      <header className="wire-topbar" aria-label="对战基础状态">
        <div className="wire-topbar-title">
          <h1>符文战场对战线框</h1>
          <span>房间 {matchId}</span>
        </div>
        <div className="wire-status-line" aria-label="服务端状态">
          <span>连接 {connectionStatusLabel(controller.state.status)}</span>
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
          <section aria-label="右侧合法操作区" className="wire-panel wire-action-map-panel" tabIndex={0}>
            <WireActionMapPanel playerId={settings.playerId} prompt={tablePrompt} snapshot={tableSnapshot} />
          </section>
          <section aria-label="焦点卡牌和候选行动" className="wire-panel" tabIndex={0}>
            <WireInteractionPanel
              disabledByConnection={controller.state.status !== "connected"}
              inspectedCard={inspectedCard}
              onCommand={(command) => void controller.submitCommand(command)}
              onClearInspectedCard={() => {
                setInspectedCard(undefined);
                setSelectionDraft(undefined);
              }}
              onOpenDetail={setDetailCard}
              playerId={settings.playerId}
              prompt={tablePrompt}
              selectionDraft={selectionDraft}
              snapshot={tableSnapshot}
            />
          </section>
          <section aria-label="右侧规则队列区" className="wire-panel wire-rule-panel" tabIndex={0}>
            <WireRuleQueuePanel
              onInspectObject={inspectObjectFromTable}
              playerId={settings.playerId}
              prompt={tablePrompt}
              selectedObjectId={inspectedCard?.objectId ?? inspectedCard?.object?.objectId}
              snapshot={tableSnapshot}
            />
          </section>
          <section aria-label="服务端行动提示" className="wire-panel wire-action-panel" tabIndex={0}>
            <ActionPanel
              connectionStatus={controller.state.status}
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
              <EventLog density={settings.logDensity} errors={controller.state.errors} events={controller.state.events} />
            </ScrollArea>
          </section>
        </aside>
      </div>
      <CardDetailDrawer
        card={detailCard}
        onClose={() => setDetailCard(undefined)}
        onCommand={(command) => void controller.submitCommand(command)}
        prompt={tablePrompt}
        snapshot={tableSnapshot}
      />
      <WireCardPreview card={previewCard} />
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

function focusedCandidateSummaries(
  candidates: PromptCandidateSummary[],
  focusedObjectId?: string
): PromptCandidateSummary[] {
  if (!focusedObjectId) {
    return [];
  }

  return candidates.filter((candidate) =>
    candidate.enabled
    && candidate.choices.some((choice) =>
      choice.role === "source"
      && promptChoiceSummaryObjectIds(choice).includes(focusedObjectId)));
}

function sourceCandidateForObject(
  candidates: PromptCandidateSummary[],
  objectId: string
): PromptCandidateSummary | undefined {
  return candidates.find((candidate) =>
    candidate.enabled
    && candidate.choices.some((choice) =>
      choice.role === "source"
      && promptChoiceSummaryObjectIds(choice).includes(objectId)));
}

function candidateChoiceForObject(
  candidates: PromptCandidateSummary[],
  objectId: string
): { candidate: PromptCandidateSummary; choice: PromptChoiceSummary } | undefined {
  for (const candidate of candidates.filter((candidate) => candidate.enabled)) {
    const choice = candidate.choices.find((candidateChoice) =>
      candidateChoice.role !== "mode"
      && promptChoiceSummaryObjectIds(candidateChoice).includes(objectId));
    if (choice) {
      return { candidate, choice };
    }
  }

  return undefined;
}

function emptySelectionDraft(sourceObjectId: string, candidate: PromptCandidateSummary): CandidateSelectionDraft {
  return {
    candidateKey: candidateComposerKey(candidate),
    optionalCostIds: [],
    sourceObjectId,
    targetChoiceIds: []
  };
}

function updateSelectionDraft(
  current: CandidateSelectionDraft | undefined,
  sourceObjectId: string,
  candidate: PromptCandidateSummary,
  choice: PromptChoiceSummary
): CandidateSelectionDraft {
  const candidateKey = candidateComposerKey(candidate);
  const base = current?.candidateKey === candidateKey && current.sourceObjectId === sourceObjectId
    ? current
    : emptySelectionDraft(sourceObjectId, candidate);

  if (choice.role === "target") {
    return {
      ...base,
      targetChoiceIds: uniqueSelectionIds([choice.id, ...base.targetChoiceIds.filter((id) => id !== choice.id)]).slice(0, 8)
    };
  }

  if (choice.role === "destination") {
    return {
      ...base,
      destinationId: choice.id
    };
  }

  if (choice.role === "optionalCost") {
    const selected = base.optionalCostIds.includes(choice.id);
    return {
      ...base,
      optionalCostIds: selected
        ? base.optionalCostIds.filter((id) => id !== choice.id)
        : uniqueSelectionIds([...base.optionalCostIds, choice.id])
    };
  }

  return base;
}

function uniqueSelectionIds(ids: string[]): string[] {
  return Array.from(new Set(ids.filter((id) => id.trim().length > 0)));
}

function buildWireInteractionMap(
  model: ReturnType<typeof buildPromptInteractionModel>,
  focusedCandidates: PromptCandidateSummary[],
  focusedObjectId?: string,
  selectionDraft?: CandidateSelectionDraft
): Record<string, PromptObjectState | undefined> {
  const states: Record<string, PromptObjectState | undefined> = Object.fromEntries([
    ...[...model.disabledObjectIds].map((objectId) => [objectId, "disabled" as const]),
    ...[...model.enabledObjectIds].map((objectId) => [objectId, "enabled" as const])
  ]);

  for (const candidate of focusedCandidates.filter((candidate) => candidate.enabled)) {
    for (const choice of candidate.choices) {
      const roleState = promptRoleState(choice.role);
      if (!roleState) {
        continue;
      }

      for (const objectId of promptChoiceSummaryObjectIds(choice)) {
        states[objectId] = mergePromptObjectState(states[objectId], roleState);
      }
    }
  }

  if (focusedObjectId && focusedCandidates.some((candidate) => candidate.enabled)) {
    states[focusedObjectId] = "source";
  }

  if (selectionDraft) {
    const selectedChoiceIds = new Set([
      ...selectionDraft.targetChoiceIds,
      selectionDraft.destinationId,
      selectionDraft.mode,
      ...selectionDraft.optionalCostIds
    ].filter((id): id is string => Boolean(id)));
    const draftCandidate = model.candidates.find((candidate) => candidateComposerKey(candidate) === selectionDraft.candidateKey);
    for (const choice of draftCandidate?.choices ?? []) {
      if (!selectedChoiceIds.has(choice.id)) {
        continue;
      }

      for (const objectId of promptChoiceSummaryObjectIds(choice)) {
        states[objectId] = mergePromptObjectState(states[objectId], "chosen");
      }
    }
  }

  return states;
}

function promptRoleState(role: PromptChoiceRole): PromptObjectState | undefined {
  return role === "mode" ? undefined : role;
}

function mergePromptObjectState(current: PromptObjectState | undefined, next: PromptObjectState): PromptObjectState {
  return promptStatePriority(next) >= promptStatePriority(current) ? next : current ?? next;
}

function promptStatePriority(state: PromptObjectState | undefined): number {
  switch (state) {
    case "chosen":
      return 7;
    case "source":
      return 6;
    case "target":
      return 5;
    case "destination":
      return 4;
    case "optionalCost":
      return 3;
    case "enabled":
      return 2;
    case "disabled":
      return 1;
    default:
      return 0;
  }
}

function WirePlayerHome({
  entry,
  fallbackSide,
  interaction,
  onInspectCard,
  onPreviewCard,
  specs
}: {
  entry?: PlayerEntry;
  fallbackSide: PlayerEntry["side"];
  interaction: WireTableInteraction;
  onInspectCard: (card: InspectedCard) => void;
  onPreviewCard: (card?: InspectedCard) => void;
  specs: Record<string, BehaviorSpec>;
}) {
  const side = entry?.side ?? fallbackSide;
  const layout = WIRE_TABLE_LAYOUT.playerHomes[side];
  const zones = entry?.player.zones ?? {};
  const objects = entry?.player.objects ?? {};
  const baseIds = zones.base ?? [];
  const runeIds = baseIds.filter((id) => isRuneCard(objects[id], specs[objects[id]?.cardNo ?? ""]));
  const baseObjectIds = baseIds.filter((id) => !runeIds.includes(id));
  const ownerLabel = entry ? playerLabel(entry) : side === "self" ? "P1 我方" : "P2 对手";
  const baseSections = {
    banish: (
      <section className="wire-banish-main" key="banish" aria-label={`${ownerLabel} 放逐区`}>
        <WirePublicPile ids={zones.banished ?? []} interactionByObjectId={interaction.interactionByObjectId} label="放逐" objects={objects} onInspectCard={onInspectCard} onPreviewCard={onPreviewCard} selectedObjectId={interaction.selectedObjectId} specs={specs} />
      </section>
    ),
    base: (
      <section className="wire-base-main" key="base" aria-label={`${ownerLabel} 基地`}>
        <WireCardFlow className="wire-base-card-grid" emptyLabel="基地" ids={baseObjectIds} interactionByObjectId={interaction.interactionByObjectId} kind="base" minSlots={1} objects={objects} onInspectCard={onInspectCard} onPreviewCard={onPreviewCard} renderEmptySlots selectedObjectId={interaction.selectedObjectId} specs={specs} />
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
        <WireCardFlow emptyLabel="英雄" ids={zones.championZone ?? []} interactionByObjectId={interaction.interactionByObjectId} kind="signature" minSlots={1} objects={objects} onInspectCard={onInspectCard} onPreviewCard={onPreviewCard} renderEmptySlots selectedObjectId={interaction.selectedObjectId} specs={specs} />
      </WireZone>
    ),
    legend: (
      <WireZone className="wire-home-legend wire-signature-zone" key="legend" title={`${ownerLabel} 传奇`}>
        <WireCardFlow emptyLabel="传奇" ids={zones.legendZone ?? []} interactionByObjectId={interaction.interactionByObjectId} kind="signature" minSlots={1} objects={objects} onInspectCard={onInspectCard} onPreviewCard={onPreviewCard} renderEmptySlots selectedObjectId={interaction.selectedObjectId} specs={specs} />
      </WireZone>
    )
  } satisfies Record<string, ReactNode>;

  return (
    <section className={`wire-player-home wire-player-${side} ${entry ? "" : "wire-player-missing"}`} style={wireGridColumnsStyle(layout.columns)} aria-label={`${ownerLabel} 基础区`}>
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
  entry?: PlayerEntry;
  fallbackSide: PlayerEntry["side"];
  hidden?: boolean;
  interaction: WireTableInteraction;
  onInspectCard: (card: InspectedCard) => void;
  onPreviewCard?: (card?: InspectedCard) => void;
  specs: Record<string, BehaviorSpec>;
}) {
  const side = entry?.side ?? fallbackSide;
  const layout = WIRE_TABLE_LAYOUT.handRails[side];
  const zones = entry?.player.zones ?? {};
  const objects = entry?.player.objects ?? {};
  const baseIds = zones.base ?? [];
  const runeIds = baseIds.filter((id) => isRuneCard(objects[id], specs[objects[id]?.cardNo ?? ""]));
  const ids = entry ? hidden ? hiddenCards(entry.player.handSize ?? zones.handHidden ?? 0, entry.id) : zones.hand ?? [] : [];
  const emptyObjects: Record<string, CardObjectView> = {};
  const zoneObjects = entry ? objects : emptyObjects;
  const pileSections = {
    library: (
      <div className="wire-hand-library-pile" key="library">
        <WireStackCount count={zones.mainDeckCount ?? 0} label="牌库" />
      </div>
    ),
    played: (
      <div className="wire-hand-played-pile" key="played">
        <WirePublicPile ids={zones.graveyard ?? []} interactionByObjectId={interaction.interactionByObjectId} label="已打出" objects={zoneObjects} onInspectCard={onInspectCard} onPreviewCard={onPreviewCard} selectedObjectId={interaction.selectedObjectId} specs={specs} />
      </div>
    )
  } satisfies Record<string, ReactNode>;
  const handBodySections = {
    cards: (
      <div className="wire-hand-cards" key="cards">
        <WireCardFlow ids={ids} interactionByObjectId={interaction.interactionByObjectId} kind="hand" objects={zoneObjects} onInspectCard={onInspectCard} onPreviewCard={onPreviewCard} selectedObjectId={interaction.selectedObjectId} specs={specs} />
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
        <WireStackCount count={zones.runeDeckCount ?? WIRE_TABLE_LAYOUT.runeDeckSize} label="符文牌堆" />
      </div>
    ),
    runeTrack: (
      <div className="wire-hand-rune-track" key="runeTrack" aria-label={entry ? `${playerLabel(entry)} 已抽出符文` : "已抽出符文占位"}>
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
  battlefields,
  interaction,
  onInspectCard,
  onPreviewCard,
  perspectivePlayerId,
  snapshot,
  specs
}: {
  battlefields: BattlefieldSnapshotView[];
  interaction: WireTableInteraction;
  onInspectCard: (card: InspectedCard) => void;
  onPreviewCard: (card?: InspectedCard) => void;
  perspectivePlayerId: string;
  snapshot?: SnapshotDto;
  specs: Record<string, BehaviorSpec>;
}) {
  const objects = buildCardObjectIndex(snapshot);
  const lanes = [0, 1].map((index) => buildBattlefieldLane(battlefields[index], index, objects, perspectivePlayerId));
  const layout = WIRE_TABLE_LAYOUT.battlefield;
  const unitPlan = buildWireCardFlowPlan({
    itemCount: Math.max(...lanes.flatMap((lane) => [lane.ownOccupants.length, lane.opposingOccupants.length]), 0),
    kind: "battlefield-unit",
    minSlots: 3
  });
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
  specs,
  title
}: {
  ids: string[];
  interaction: WireTableInteraction;
  objects: Record<string, CardObjectView>;
  onInspectCard: (card: InspectedCard) => void;
  onPreviewCard: (card?: InspectedCard) => void;
  plan: WireCardFlowPlan;
  specs: Record<string, BehaviorSpec>;
  title: string;
}) {
  return (
    <section className="wire-battlefield-unit-zone" aria-label={title}>
      <WireCardFlow ids={ids} interactionByObjectId={interaction.interactionByObjectId} kind="battlefield-unit" minSlots={3} objects={objects} onInspectCard={onInspectCard} onPreviewCard={onPreviewCard} plan={plan} renderEmptySlots selectedObjectId={interaction.selectedObjectId} specs={specs} />
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
  objects: ZoneObjects;
  onInspectCard: (card: InspectedCard) => void;
  onPreviewCard?: (card?: InspectedCard) => void;
  reverse?: boolean;
  specs: Record<string, BehaviorSpec>;
}) {
  const runeDeckSize = WIRE_TABLE_LAYOUT.runeDeckSize;
  const slotIndexes = Array.from({ length: runeDeckSize }, (_, index) => reverse ? runeDeckSize - 1 - index : index);

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
                <CardFace compact interactionState={interaction.interactionByObjectId[id]} object={object} objectId={id} onInspect={onInspectCard} onPreview={onPreviewCard} selected={interaction.selectedObjectId === id} spec={object?.cardNo ? specs[object.cardNo] : undefined} />
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

function WireCardPreview({ card }: { card?: InspectedCard }) {
  const frontImage = card?.spec?.frontImage?.trim();
  if (!card || !frontImage) {
    return null;
  }

  const title = card.spec?.cardName ?? card.object?.cardNo ?? "卡牌";
  const battlefield = card.spec?.cardCategoryName === "战场" || card.object?.tags?.includes("CARD_TYPE:BATTLEFIELD");
  return (
    <div className={`wire-card-preview ${battlefield ? "is-battlefield-preview" : ""}`} aria-label={`预览 ${title}`} role="presentation">
      <img alt={title} src={frontImage} />
    </div>
  );
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

function isRuneCard(object?: CardObjectView, spec?: BehaviorSpec): boolean {
  return Boolean(object?.tags?.includes("CARD_TYPE:RUNE") || spec?.cardCategoryName === "符文");
}

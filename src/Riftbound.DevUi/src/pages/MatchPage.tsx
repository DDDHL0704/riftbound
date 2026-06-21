import { type CSSProperties, type ReactNode, useCallback, useEffect, useMemo, useRef, useState } from "react";
import { AppRoute } from "../app/router";
import { CardDetailDrawer } from "../components/cards/CardDetailDrawer";
import { CardFace, InspectedCard } from "../components/cards/CardFace";
import { ActionPanel } from "../components/match/ActionPanel";
import { EventLog } from "../components/match/EventLog";
import { WireActionMapPanel } from "../components/match/WireActionMapPanel";
import { WireCommandCenterPanel } from "../components/match/WireCommandCenterPanel";
import { useDelayedWireCardPreview, WireCardPreview } from "../components/match/WireCardPreview";
import { WireInformationBoundaryPanel } from "../components/match/WireInformationBoundaryPanel";
import { WireInteractionPanel } from "../components/match/WireInteractionPanel";
import { WireMatchOverviewPanel } from "../components/match/WireMatchOverviewPanel";
import { WireObjectCommandTray } from "../components/match/WireObjectCommandTray";
import { WirePromptAuthorityPanel } from "../components/match/WirePromptAuthorityPanel";
import { WireResponseCoachPanel } from "../components/match/WireResponseCoachPanel";
import { WireRuleQueuePanel } from "../components/match/WireRuleQueuePanel";
import { WireServerFlowPanel } from "../components/match/WireServerFlowPanel";
import { WireTableAuthorityPanel } from "../components/match/WireTableAuthorityPanel";
import { WireTimelineDetailPanel, type WireTimelineDetail } from "../components/match/WireTimelineDetailPanel";
import { WireTimelineDetailLayer } from "../components/match/WireTimelineDetailLayer";
import { WireTurnWindowPanel } from "../components/match/WireTurnWindowPanel";
import {
  WireCardFlow,
  type WireCardFlowPlan,
  type WireTimelineObjectState,
  WireCardSlot,
  WirePublicPile,
  WireStackCount,
  resolveWireCardFlowRenderPlan
} from "../components/match/wireCardFlow";
import {
  buildWireTableViewModel,
  playerLabel,
  type WireBattlefieldLane,
  type WireBattlefieldModel,
  type WireBattlefieldStandbySlot,
  type WirePlayerEntry,
  type WireZoneObjects
} from "../components/match/wireTableViewModel";
import {
  WIRE_TABLE_LAYOUT,
  type WireSidePanelSlot,
  wireGridColumnsStyle,
  wireGridTemplateStyle,
  wireMatchPageStyle,
  wireTableStyle
} from "../components/match/wireTableLayout";
import {
  buildWireInteractionMap,
  buildWireObjectHintMap,
  buildWireTimelineMap,
  candidateChoiceForObject,
  emptySelectionDraft,
  focusedCandidateSummaries,
  mergeWireTimelineMaps,
  sourceCandidateForObject,
  updateSelectionDraft,
  type WireTableObjectHint
} from "../components/match/wireTableInteractionModel";
import { Button } from "../components/ui/Button";
import { ScrollArea } from "../components/ui/ScrollArea";
import {
  buildWireLayoutFixtureCommandSubmission,
  buildWireLayoutFixtureEvents,
  buildWireLayoutFixturePrompt,
  buildWireLayoutFixtureSnapshot,
  isWireLayoutFixtureCommandSubmissionEnabled,
  isWireLayoutFixtureEnabled,
  wireLayoutFixtureSpecByNo
} from "../fixtures/wireLayoutFixture";
import { useCatalog } from "../stores/catalogStore";
import { useSettings } from "../stores/settingsStore";
import { type CommandSubmissionFeedback, useMatchController } from "../stores/useMatchController";
import { BehaviorSpec } from "../types/catalog";
import type { CardObjectView, GameCommand } from "../types/protocol";
import { asRecord, asString } from "../utils/collections";
import { connectionStatusLabel, matchPhaseLabel, timingStateLabel } from "../utils/formatters";
import type { CandidateSelectionDraft } from "../utils/candidateSelectionDraft";
import { buildPromptInteractionModel, type PromptObjectState } from "../utils/promptInteraction";
import { buildCardObjectIndex } from "../utils/snapshotObjectIndex";
import { buildTableObjectContextModel } from "../utils/tableObjectContext";
import type {
  CommandSubmissionFollowupEventRow,
  CommandSubmissionUiSource,
  CommandSubmissionFollowupServerEventKind,
  ObservedGameEvent
} from "../utils/commandSubmissionFollowupPlan";
import { buildEventLogPlan } from "../utils/eventLogPlan";
import { buildServerQuickActionPlan, type ServerQuickActionEntry } from "../utils/serverQuickActionPlan";
import { buildServerSubmissionGatePlan } from "../utils/serverSubmissionGatePlan";
import { buildWireFocusedInteractionPlan } from "../utils/wireFocusedInteractionPlan";
import { buildWireServerFlowProjectionPlan } from "../utils/wireServerFlowProjectionPlan";
import { buildWireSidePanelDirectoryPlan, type WireSidePanelDirectoryPlan } from "../utils/wireSidePanelDirectoryPlan";

type WireTableInteraction = {
  hintByObjectId: Record<string, WireTableObjectHint | undefined>;
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
  const [timelineLayerOpen, setTimelineLayerOpen] = useState(false);
  const [fixtureSubmissionFeedback, setFixtureSubmissionFeedback] = useState<CommandSubmissionFeedback | undefined>();
  const timelineDetailTriggerIdRef = useRef<string | undefined>(undefined);
  const { previewCard, queuePreviewCard } = useDelayedWireCardPreview();
  const layoutFixtureEnabled = useMemo(() => isWireLayoutFixtureEnabled(), []);
  const layoutFixtureCommandSubmissionEnabled = useMemo(() => isWireLayoutFixtureCommandSubmissionEnabled(), []);
  const tableSnapshot = useMemo(
    () => layoutFixtureEnabled ? buildWireLayoutFixtureSnapshot(settings.playerId) : snapshot,
    [layoutFixtureEnabled, settings.playerId, snapshot]
  );
  const tablePrompt = useMemo(
    () => layoutFixtureEnabled ? buildWireLayoutFixturePrompt(settings.playerId) : controller.state.prompt,
    [controller.state.prompt, layoutFixtureEnabled, settings.playerId]
  );
  const tableEvents = useMemo<ObservedGameEvent[]>(
    () => layoutFixtureEnabled
      ? buildWireLayoutFixtureEvents(settings.playerId).map((event, index) => ({
        ...event,
        receivedBatchIndex: index,
        receivedMessageType: "EVENTS",
        receivedServerTick: 7
      }))
      : controller.state.events,
    [controller.state.events, layoutFixtureEnabled, settings.playerId]
  );
  const tableSubmissionFeedback = useMemo(
    () => layoutFixtureEnabled
      ? fixtureSubmissionFeedback ?? (layoutFixtureCommandSubmissionEnabled ? buildWireLayoutFixtureCommandSubmission() : undefined)
      : controller.state.lastCommandSubmission,
    [controller.state.lastCommandSubmission, fixtureSubmissionFeedback, layoutFixtureCommandSubmissionEnabled, layoutFixtureEnabled]
  );
  const tableSpecByNo = useMemo(
    () => layoutFixtureEnabled ? { ...wireLayoutFixtureSpecByNo, ...specByNo } : specByNo,
    [layoutFixtureEnabled, specByNo]
  );
  const tableObjectIndex = useMemo(() => buildCardObjectIndex(tableSnapshot), [tableSnapshot]);
  const tableConnectionStatus = layoutFixtureEnabled ? "connected" : controller.state.status;
  const tableSubmissionGate = useMemo(() => buildServerSubmissionGatePlan({
    connectionStatus: tableConnectionStatus,
    prompt: tablePrompt,
    snapshot: tableSnapshot
  }), [tableConnectionStatus, tablePrompt, tableSnapshot]);
  const submitTableCommand = useCallback((command: GameCommand, uiSource?: CommandSubmissionUiSource) => {
    if (layoutFixtureEnabled) {
      setFixtureSubmissionFeedback(buildWireLayoutFixtureCommandSubmission({
        cmdType: command.cmdType,
        mode: "timeline",
        uiSource
      }));
      return;
    }

    void controller.submitCommand(command, uiSource);
  }, [controller, layoutFixtureEnabled]);
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
  const selectedFocusPlan = useMemo(() => buildWireFocusedInteractionPlan({
    canSubmitCommands: true,
    disabledByConnection: !tableSubmissionGate.canSubmit,
    playerId: settings.playerId,
    prompt: tablePrompt,
    selectionDraft,
    snapshot: tableSnapshot,
    sourceControllerId: inspectedCard?.object?.controllerId,
    sourceObjectId: selectedObjectId,
    submissionGate: tableSubmissionGate
  }), [
    inspectedCard?.object?.controllerId,
    selectedObjectId,
    selectionDraft,
    settings.playerId,
    tablePrompt,
    tableSnapshot,
    tableSubmissionGate
  ]);
  const serverFlowProjection = useMemo(
    () => buildWireServerFlowProjectionPlan(tablePrompt),
    [tablePrompt]
  );
  const tableInteraction = useMemo<WireTableInteraction>(() => ({
    hintByObjectId: buildWireObjectHintMap(promptInteraction, focusedSourceCandidates, selectedObjectId, selectionDraft),
    interactionByObjectId: buildWireInteractionMap(promptInteraction, focusedSourceCandidates, selectedObjectId, selectionDraft),
    selectedObjectId,
    timelineByObjectId: mergeWireTimelineMaps(
      serverFlowProjection.timelineByObjectId,
      buildWireTimelineMap(timelineDetail)
    )
  }), [focusedSourceCandidates, promptInteraction, selectedObjectId, selectionDraft, serverFlowProjection.timelineByObjectId, timelineDetail]);

  const self = tableView.self;
  const opponent = tableView.opponent;
  const timing = asRecord(tableSnapshot?.timing);
  const turnWindow = asRecord(timing.turnWindow);
  const phase = asString(timing.phase, tableSnapshot?.turnState ?? "");
  const windowState = asString(turnWindow.state, asString(timing.timingState, ""));
  const roomStatus = asString(timing.roomStatus, "");
  const promptTitle = tablePrompt?.view?.title?.trim() || "无行动窗口";
  const promptCanAct = Boolean(tablePrompt?.actionable && tablePrompt.playerId === settings.playerId);
  const canAct = promptCanAct && tableSubmissionGate.canSubmit;
  const topbarQuickActionPlan = useMemo(() => buildServerQuickActionPlan({
    canAct: promptCanAct,
    connected: tableConnectionStatus === "connected",
    prompt: tablePrompt,
    submissionGate: tableSubmissionGate,
    snapshot: tableSnapshot
  }), [promptCanAct, tableConnectionStatus, tablePrompt, tableSubmissionGate, tableSnapshot]);
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
  const clearInspectedCard = useCallback(() => {
    setInspectedCard(undefined);
    setSelectionDraft(undefined);
  }, []);
  const openDetailCard = useCallback((card: InspectedCard) => {
    setDetailCard(card);
  }, []);
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
  const inspectObjectFromDetail = useCallback((objectId: string) => {
    inspectObjectFromTable(objectId);
    openObjectDetail(objectId);
  }, [inspectObjectFromTable, openObjectDetail]);
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
          handPlan={tableView.playerPlans.handPlan}
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
        basePlan={tableView.playerPlans.basePlan}
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
    setTimelineLayerOpen(false);
    timelineDetailTriggerIdRef.current = undefined;
  }, [tableSnapshot?.tick]);

  const selectTimelineDetail = useCallback((detail: WireTimelineDetail) => {
    timelineDetailTriggerIdRef.current = detail.id;
    setTimelineDetail(detail);
  }, []);
  const selectTimelineEvent = useCallback((target: { kind: string; order?: number; serverTick?: number }) => {
    const eventPlan = buildEventLogPlan({
      errors: [],
      events: tableEvents,
      objectIndex: tableObjectIndex
    });
    const exactIndex = target.serverTick != null && target.order != null
      ? tableEvents.findIndex((event) => {
        const observed = event as { receivedBatchIndex?: number; receivedServerTick?: number };
        return event.kind === target.kind
          && observed.receivedServerTick === target.serverTick
          && observed.receivedBatchIndex === target.order;
      })
      : -1;
    const exactRow = exactIndex >= 0 ? eventPlan.events[exactIndex] : undefined;
    const fixtureRow = exactRow == null && target.order != null
      ? eventPlan.events[target.order]
      : undefined;
    const row = exactRow
      ?? (fixtureRow?.kind === target.kind ? fixtureRow : undefined)
      ?? eventPlan.events.find((event) => event.kind === target.kind);
    if (row) {
      selectTimelineDetail(row.detail);
    }
  }, [selectTimelineDetail, tableEvents, tableObjectIndex]);
  const selectServerEventKind = useCallback((eventKind: CommandSubmissionFollowupServerEventKind) => {
    selectTimelineEvent(eventKind);
  }, [selectTimelineEvent]);
  const selectFollowupEvent = useCallback((event: CommandSubmissionFollowupEventRow) => {
    selectTimelineEvent(event);
  }, [selectTimelineEvent]);

  const clearTimelineDetail = useCallback(() => {
    const triggerId = timelineDetailTriggerIdRef.current ?? timelineDetail?.id;
    setTimelineDetail(undefined);
    setTimelineLayerOpen(false);
    if (!triggerId) {
      return;
    }

    window.setTimeout(() => {
      const trigger = Array.from(document.querySelectorAll<HTMLButtonElement>("[data-wire-detail-id]"))
        .find((button) => button.getAttribute("data-wire-detail-id") === triggerId);
      trigger?.focus();
    }, 0);
  }, [timelineDetail?.id]);
  const runTopbarQuickAction = useCallback((entry: ServerQuickActionEntry) => {
    if (entry.disabled) {
      return;
    }

    if (entry.command) {
      submitTableCommand(entry.command, {
        label: `顶部快捷：${entry.label}`,
        surface: "topbar"
      });
      return;
    }

    if (entry.directAction === "ready") {
      void controller.ready({
        label: `顶部快捷：${entry.label}`,
        surface: "topbar"
      });
      return;
    }

    if (entry.directAction === "submitDeck") {
      void controller.submitStarterDeck({
        label: `顶部快捷：${entry.label}`,
        surface: "topbar"
      });
    }
  }, [controller, submitTableCommand]);
  const sidePanelDirectory = useMemo(() => buildWireSidePanelDirectoryPlan(WIRE_TABLE_LAYOUT.sidePanel.slots), []);
  const sidePanelSections = {
    overview: (
      <section aria-label="当前对局态势总览区" className="wire-panel wire-match-overview-panel" data-wire-side-panel-slot="overview" id={sidePanelDirectory.bySlot.overview.anchorId} key="overview" tabIndex={0}>
        <WireMatchOverviewPanel
          connectionStatus={tableConnectionStatus}
          events={tableEvents}
          playerId={settings.playerId}
          prompt={tablePrompt}
          selectedObjectContext={selectedObjectContext}
          selectedObjectId={selectedObjectId}
          snapshot={tableSnapshot}
          submissionGate={tableSubmissionGate}
          timelineDetail={timelineDetail}
        />
      </section>
    ),
    turnWindow: (
      <section aria-label="服务端窗口总览区" className="wire-panel wire-window-plan-panel" data-wire-side-panel-slot="turnWindow" id={sidePanelDirectory.bySlot.turnWindow.anchorId} key="turnWindow" tabIndex={0}>
        <WireTurnWindowPanel
          connectionStatus={tableConnectionStatus}
          playerId={settings.playerId}
          prompt={tablePrompt}
          snapshot={tableSnapshot}
        />
      </section>
    ),
    commandCenter: (
      <section aria-label="当前行动指挥中心区" className="wire-panel wire-command-center-panel" data-wire-side-panel-slot="commandCenter" id={sidePanelDirectory.bySlot.commandCenter.anchorId} key="commandCenter" tabIndex={0}>
        <WireCommandCenterPanel
          connectionStatus={tableConnectionStatus}
          disabledByConnection={!tableSubmissionGate.canSubmit}
          events={tableEvents}
          focusedPlan={selectedFocusPlan}
          objectContext={selectedObjectContext}
          onClearFocus={clearInspectedCard}
          onCommand={(command) => submitTableCommand(command, {
            label: "指挥中心",
            objectId: selectedObjectId,
            surface: "command-center"
          })}
          onInspectObject={inspectObjectFromTable}
          onSelectFollowupEvent={selectFollowupEvent}
          onSelectServerEventKind={selectServerEventKind}
          playerId={settings.playerId}
          prompt={tablePrompt}
          selectionDraft={selectionDraft}
          snapshot={tableSnapshot}
          submissionFeedback={tableSubmissionFeedback}
          submissionGate={tableSubmissionGate}
          table={tableView}
        />
      </section>
    ),
    serverFlow: (
      <section aria-label="服务端结算与行动总览区" className="wire-panel wire-server-flow-panel" data-wire-side-panel-slot="serverFlow" id={sidePanelDirectory.bySlot.serverFlow.anchorId} key="serverFlow" tabIndex={0}>
        <WireServerFlowPanel
          connectionStatus={tableConnectionStatus}
          events={tableEvents}
          objectIndex={tableObjectIndex}
          onInspectObject={inspectObjectFromTable}
          onSelectDetail={selectTimelineDetail}
          playerId={settings.playerId}
          prompt={tablePrompt}
          selectionDraft={selectionDraft}
          selectedObjectId={selectedObjectId}
          snapshot={tableSnapshot}
          submissionGate={tableSubmissionGate}
        />
      </section>
    ),
    responseCoach: (
      <section aria-label="当前响应导航区" className="wire-panel wire-response-coach-panel" data-wire-side-panel-slot="responseCoach" id={sidePanelDirectory.bySlot.responseCoach.anchorId} key="responseCoach" tabIndex={0}>
        <WireResponseCoachPanel
          connectionStatus={tableConnectionStatus}
          playerId={settings.playerId}
          prompt={tablePrompt}
          selectionDraft={selectionDraft}
          snapshot={tableSnapshot}
          submissionGate={tableSubmissionGate}
        />
      </section>
    ),
    tableAuthority: (
      <section aria-label="服务端桌面布局契约区" className="wire-panel wire-table-authority-panel" data-wire-side-panel-slot="tableAuthority" id={sidePanelDirectory.bySlot.tableAuthority.anchorId} key="tableAuthority" tabIndex={0}>
        <WireTableAuthorityPanel selectedObjectId={selectedObjectId} table={tableView} />
      </section>
    ),
    informationBoundary: (
      <section aria-label="隐藏信息边界契约区" className="wire-panel wire-information-boundary-panel" data-wire-side-panel-slot="informationBoundary" id={sidePanelDirectory.bySlot.informationBoundary.anchorId} key="informationBoundary" tabIndex={0}>
        <WireInformationBoundaryPanel events={tableEvents} table={tableView} />
      </section>
    ),
    promptAuthority: (
      <section aria-label="服务端行动窗口契约区" className="wire-panel wire-prompt-authority-panel" data-wire-side-panel-slot="promptAuthority" id={sidePanelDirectory.bySlot.promptAuthority.anchorId} key="promptAuthority" tabIndex={0}>
        <WirePromptAuthorityPanel
          playerId={settings.playerId}
          prompt={tablePrompt}
          submissionGate={tableSubmissionGate}
        />
      </section>
    ),
    actionMap: (
      <section aria-label="右侧合法操作区" className="wire-panel wire-action-map-panel" data-wire-side-panel-slot="actionMap" id={sidePanelDirectory.bySlot.actionMap.anchorId} key="actionMap" tabIndex={0}>
        <WireActionMapPanel
          events={tableEvents}
          onChooseObject={chooseObjectFromActionMap}
          onCommand={(command) => submitTableCommand(command, {
            label: "右侧合法操作",
            objectId: selectedObjectId,
            surface: "action-map"
          })}
          onInspectObject={inspectObjectFromTable}
          onSelectFollowupEvent={selectFollowupEvent}
          onSelectServerEventKind={selectServerEventKind}
          playerId={settings.playerId}
          prompt={tablePrompt}
          selectedObjectId={selectedObjectId}
          selectionDraft={selectionDraft}
          snapshot={tableSnapshot}
          submissionFeedback={tableSubmissionFeedback}
          submissionGate={tableSubmissionGate}
          table={tableView}
        />
      </section>
    ),
    interaction: (
      <section aria-label="焦点卡牌和候选行动" className="wire-panel" data-wire-side-panel-slot="interaction" id={sidePanelDirectory.bySlot.interaction.anchorId} key="interaction" tabIndex={0}>
        <WireInteractionPanel
          disabledByConnection={!tableSubmissionGate.canSubmit}
          focusedPlan={selectedFocusPlan}
          inspectedCard={inspectedCard}
          onCommand={(command) => submitTableCommand(command, {
            label: "焦点卡牌和候选行动",
            objectId: selectedObjectId,
            surface: "interaction-panel"
          })}
          onClearInspectedCard={clearInspectedCard}
          onInspectObject={inspectObjectFromTable}
          onOpenDetail={openDetailCard}
          onSelectDetail={selectTimelineDetail}
          objectContext={selectedObjectContext}
          playerId={settings.playerId}
          prompt={tablePrompt}
          selectionDraft={selectionDraft}
          selectedDetailId={timelineDetail?.id}
          snapshot={tableSnapshot}
          submissionGate={tableSubmissionGate}
        />
      </section>
    ),
    ruleQueue: (
      <section aria-label="右侧规则队列区" className="wire-panel wire-rule-panel" data-wire-side-panel-slot="ruleQueue" id={sidePanelDirectory.bySlot.ruleQueue.anchorId} key="ruleQueue" tabIndex={0}>
        <WireRuleQueuePanel
          events={tableEvents}
          onInspectObject={inspectObjectFromTable}
          onSelectDetail={selectTimelineDetail}
          playerId={settings.playerId}
          prompt={tablePrompt}
          selectedDetailId={timelineDetail?.id}
          selectedObjectId={selectedObjectId}
          snapshot={tableSnapshot}
        />
      </section>
    ),
    timelineDetail: (
      <section aria-label="规则与事件详情区" className="wire-panel wire-timeline-detail-panel" data-wire-side-panel-slot="timelineDetail" id={sidePanelDirectory.bySlot.timelineDetail.anchorId} key="timelineDetail" tabIndex={0}>
        <WireTimelineDetailPanel
          detail={timelineDetail}
          disabledByConnection={!tableSubmissionGate.canSubmit}
          events={tableEvents}
          objectContextById={tableObjectContextModel.byId}
          objectIndex={tableObjectIndex}
          onChooseObject={chooseObjectFromActionMap}
          onCommand={(command) => submitTableCommand(command, {
            detailId: timelineDetail?.id,
            label: "规则与事件详情",
            objectId: selectedObjectId,
            surface: "timeline-detail"
          })}
          onClear={clearTimelineDetail}
          onInspectObject={inspectObjectFromTable}
          onOpenLayer={() => setTimelineLayerOpen(true)}
          onOpenObjectDetail={openObjectDetail}
          onSelectDetail={selectTimelineDetail}
          onSelectFollowupEvent={selectFollowupEvent}
          onSelectServerEventKind={selectServerEventKind}
          prompt={tablePrompt}
          selectionDraft={selectionDraft}
          selectedObjectContext={selectedObjectContext}
          selectedObjectId={selectedObjectId}
          snapshot={tableSnapshot}
          submissionFeedback={tableSubmissionFeedback}
          table={tableView}
        />
      </section>
    ),
    actionPrompt: (
      <section aria-label="服务端行动提示" className="wire-panel wire-action-panel" data-wire-side-panel-slot="actionPrompt" id={sidePanelDirectory.bySlot.actionPrompt.anchorId} key="actionPrompt" tabIndex={0}>
        <ActionPanel
          connectionStatus={tableConnectionStatus}
          onCommand={(command) => submitTableCommand(command, {
            label: "服务端行动提示",
            surface: "action-prompt"
          })}
          onReady={() => void controller.ready({
            label: "服务端行动提示：准备",
            surface: "action-prompt"
          })}
          onSubmitStarterDeck={() => void controller.submitStarterDeck({
            label: "服务端行动提示：提交构筑",
            surface: "action-prompt"
          })}
          playerId={settings.playerId}
          prompt={tablePrompt}
          snapshot={tableSnapshot}
        />
      </section>
    ),
    log: (
      <section aria-label="事件日志" className="wire-panel wire-log-panel" data-wire-side-panel-slot="log" id={sidePanelDirectory.bySlot.log.anchorId} key="log" tabIndex={0}>
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
    )
  } satisfies Record<WireSidePanelSlot, ReactNode>;

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
          {topbarQuickActionPlan.entries.map((entry) => (
            <Button
              data-topbar-quick-action={entry.id}
              data-topbar-quick-action-candidate={entry.candidateAction ?? ""}
              data-topbar-quick-action-command-source={entry.commandSource}
              data-topbar-quick-action-command-source-label={entry.commandSourceLabel}
              data-topbar-quick-action-state={entry.state}
              disabled={entry.disabled}
              key={entry.id}
              onClick={() => runTopbarQuickAction(entry)}
              title={entry.title}
              variant={entry.variant}
            >
              {entry.label}
            </Button>
          ))}
        </div>
      </header>

      <div className="wire-match-body">
        <section className="wire-table-shell" aria-label="黑白线框对战桌面">
          <div className="wire-table" style={wireTableStyle()}>{tableRows}</div>
          <WireObjectCommandTray
            disabledByConnection={!tableSubmissionGate.canSubmit}
            focusedPlan={selectedFocusPlan}
            inspectedCard={inspectedCard}
            objectContext={selectedObjectContext}
            onClear={clearInspectedCard}
            onCommand={(command) => submitTableCommand(command, {
              label: "桌面对象命令托盘",
              objectId: selectedObjectId,
              surface: "object-command-tray"
            })}
            onOpenDetail={openDetailCard}
            prompt={tablePrompt}
            snapshot={tableSnapshot}
            submissionGate={tableSubmissionGate}
          />
        </section>

        <aside className="wire-side-panel" aria-label="行动与日志">
          <WireSidePanelDirectory plan={sidePanelDirectory} />
          {WIRE_TABLE_LAYOUT.sidePanel.slots.map((slot) => sidePanelSections[slot])}
        </aside>
      </div>
      <CardDetailDrawer
        card={detailCard}
        disabledByConnection={!tableSubmissionGate.canSubmit}
        objectContext={detailObjectContext}
        onClose={() => setDetailCard(undefined)}
        onCommand={(command) => submitTableCommand(command, {
          label: "卡牌详情抽屉",
          objectId: detailObjectId,
          surface: "card-detail"
        })}
        onInspectObject={inspectObjectFromDetail}
        playerId={settings.playerId}
        prompt={tablePrompt}
        selectionDraft={selectionDraft}
        snapshot={tableSnapshot}
        submissionGate={tableSubmissionGate}
      />
      <WireTimelineDetailLayer
        detail={timelineDetail}
        disabledByConnection={!tableSubmissionGate.canSubmit}
        events={tableEvents}
        objectContextById={tableObjectContextModel.byId}
        objectIndex={tableObjectIndex}
        onChooseObject={chooseObjectFromActionMap}
        onCommand={(command) => submitTableCommand(command, {
          detailId: timelineDetail?.id,
          label: "规则事件检查层",
          objectId: selectedObjectId,
          surface: "timeline-detail-layer"
        })}
        onClear={clearTimelineDetail}
        onClose={() => setTimelineLayerOpen(false)}
        onInspectObject={inspectObjectFromTable}
        onOpenObjectDetail={openObjectDetail}
        onSelectDetail={selectTimelineDetail}
        onSelectFollowupEvent={selectFollowupEvent}
        onSelectServerEventKind={selectServerEventKind}
        open={timelineLayerOpen}
        prompt={tablePrompt}
        selectionDraft={selectionDraft}
        selectedObjectContext={selectedObjectContext}
        selectedObjectId={selectedObjectId}
        snapshot={tableSnapshot}
        submissionFeedback={tableSubmissionFeedback}
        table={tableView}
      />
      <WireCardPreview card={previewCard} />
    </div>
  );
}

function WireSidePanelDirectory({ plan }: { plan: WireSidePanelDirectoryPlan }) {
  return (
    <nav aria-label="右侧面板目录" className="wire-side-panel-directory" data-wire-side-panel-directory data-wire-side-panel-directory-count={plan.entries.length}>
      <h2>目录</h2>
      <ol>
        {plan.entries.map((entry) => (
          <li data-wire-side-panel-directory-group={entry.group} data-wire-side-panel-directory-item={entry.slot} key={entry.slot}>
            <a data-wire-side-panel-directory-link={entry.slot} href={`#${entry.anchorId}`}>
              <span>{entry.groupLabel}</span>
              <strong>{entry.order}. {entry.label}</strong>
            </a>
          </li>
        ))}
      </ol>
    </nav>
  );
}

function WirePlayerHome({
  basePlan,
  entry,
  fallbackSide,
  interaction,
  onInspectCard,
  onPreviewCard,
  specs
}: {
  basePlan: WireCardFlowPlan;
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
        <WirePublicPile ids={zones.banished ?? []} hintByObjectId={interaction.hintByObjectId} interactionByObjectId={interaction.interactionByObjectId} kind="banished" label="放逐" objects={objects} onInspectCard={onInspectCard} onPreviewCard={onPreviewCard} selectedObjectId={interaction.selectedObjectId} specs={specs} timelineByObjectId={interaction.timelineByObjectId} />
      </section>
    ),
    base: (
      <section className="wire-base-main" key="base" aria-label={`${ownerLabel} 基地`}>
        <WireCardFlow className="wire-base-card-grid" emptyLabel="基地" hintByObjectId={interaction.hintByObjectId} ids={baseObjectIds} interactionByObjectId={interaction.interactionByObjectId} kind="base" minSlots={1} objects={objects} onInspectCard={onInspectCard} onPreviewCard={onPreviewCard} plan={basePlan} renderEmptySlots selectedObjectId={interaction.selectedObjectId} specs={specs} timelineByObjectId={interaction.timelineByObjectId} />
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
        <WireCardFlow emptyLabel="英雄" hintByObjectId={interaction.hintByObjectId} ids={zones.championZone ?? []} interactionByObjectId={interaction.interactionByObjectId} kind="signature" minSlots={1} objects={objects} onInspectCard={onInspectCard} onPreviewCard={onPreviewCard} renderEmptySlots selectedObjectId={interaction.selectedObjectId} specs={specs} timelineByObjectId={interaction.timelineByObjectId} />
      </WireZone>
    ),
    legend: (
      <WireZone className="wire-home-legend wire-signature-zone" key="legend" title={`${ownerLabel} 传奇`}>
        <WireCardFlow emptyLabel="传奇" hintByObjectId={interaction.hintByObjectId} ids={zones.legendZone ?? []} interactionByObjectId={interaction.interactionByObjectId} kind="signature" minSlots={1} objects={objects} onInspectCard={onInspectCard} onPreviewCard={onPreviewCard} renderEmptySlots selectedObjectId={interaction.selectedObjectId} specs={specs} timelineByObjectId={interaction.timelineByObjectId} />
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
  handPlan,
  hidden = false,
  interaction,
  onInspectCard,
  onPreviewCard,
  specs
}: {
  entry?: WirePlayerEntry;
  fallbackSide: WirePlayerEntry["side"];
  handPlan: WireCardFlowPlan;
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
        <WirePublicPile ids={zones.graveyard ?? []} hintByObjectId={interaction.hintByObjectId} interactionByObjectId={interaction.interactionByObjectId} kind="graveyard" label="已打出" objects={zoneObjects} onInspectCard={onInspectCard} onPreviewCard={onPreviewCard} selectedObjectId={interaction.selectedObjectId} specs={specs} timelineByObjectId={interaction.timelineByObjectId} />
      </div>
    )
  } satisfies Record<string, ReactNode>;
  const handBodySections = {
    cards: (
      <div className="wire-hand-cards" key="cards">
        <WireCardFlow hintByObjectId={interaction.hintByObjectId} ids={ids} interactionByObjectId={interaction.interactionByObjectId} kind="hand" objects={zoneObjects} onInspectCard={onInspectCard} onPreviewCard={onPreviewCard} plan={handPlan} selectedObjectId={interaction.selectedObjectId} specs={specs} timelineByObjectId={interaction.timelineByObjectId} />
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
  const standbyPlan = battlefield.standbyPlan;
  const unitZones = (side: "opponent" | "self") => layout.unitZones
    .filter((zone) => zone.side === side)
    .map((zone) => {
      const lane = lanes[zone.laneIndex];
      const ids = side === "self" ? lane.ownOccupants : lane.opposingOccupants;
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
          title={battlefieldUnitZoneLabel(zone.laneIndex, side)}
        />
      );
    });
  const battlefieldSections = {
    center: (
      <div className="wire-battlefield-center-grid" key="center" style={wireGridTemplateStyle(layout.centerColumns, layout.centerRows)}>
        {unitZones("opponent")}
        {layout.standbyZones.map((zone) => (
          <WireBattlefieldStandbyZone
            interaction={interaction}
            key={zone.id}
            lane={lanes[zone.laneIndex]}
            objects={objects}
            onInspectCard={onInspectCard}
            onPreviewCard={onPreviewCard}
            plan={standbyPlan}
            specs={specs}
          />
        ))}
        {unitZones("self")}
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

function WireBattlefieldStandbyZone({
  interaction,
  lane,
  objects,
  onInspectCard,
  onPreviewCard,
  plan,
  specs
}: {
  interaction: WireTableInteraction;
  lane: WireBattlefieldLane;
  objects: WireZoneObjects;
  onInspectCard: (card: InspectedCard) => void;
  onPreviewCard: (card?: InspectedCard) => void;
  plan: WireCardFlowPlan;
  specs: Record<string, BehaviorSpec>;
}) {
  const slots = lane.standbySlots;
  const slotCount = Math.max(slots.length, plan.minSlots);
  const flowPlan = resolveWireCardFlowRenderPlan({
    itemCount: slots.length,
    minSlots: plan.minSlots,
    sizingPlan: plan,
    slotCount
  });
  const emptySlotCount = slots.length > 0 ? 0 : Math.max(1, flowPlan.minSlots);

  return (
    <section
      aria-label={`${lane.index === 0 ? "左战场" : "右战场"} 待命槽`}
      className="wire-battlefield-standby-zone"
      data-wire-battlefield-hidden-standby-count={lane.hiddenStandbyCount}
      data-wire-battlefield-standby-count={slots.length}
      data-wire-battlefield-standby-source={lane.standbySlotSource}
    >
      <div
        className={`wire-card-flow wire-card-flow-standby wire-card-flow-rail wire-flow-${flowPlan.density}`}
        data-flow-capacity={String(flowPlan.capacity)}
        data-flow-card-height={flowPlan.cardHeight}
        data-flow-card-width={flowPlan.cardWidth}
        data-flow-count={slots.length}
        data-flow-density={flowPlan.density}
        data-flow-fit={flowPlan.fit}
        data-flow-kind={flowPlan.kind}
        data-flow-layout={flowPlan.layout}
        data-flow-min-slots={flowPlan.minSlots}
        data-flow-overflow={flowPlan.overflow}
        data-flow-overflow-count={flowPlan.overflowCount}
        data-flow-scroll-after={flowPlan.scrollAfter}
        data-flow-slots={slotCount}
        data-flow-visible-slots={flowPlan.visibleSlotCount}
        style={wirePlanStyle(flowPlan)}
      >
        {slots.map((slot) => (
          <WireStandbySlotCard
            interaction={interaction}
            key={slot.slotId}
            objects={objects}
            onInspectCard={onInspectCard}
            onPreviewCard={onPreviewCard}
            slot={slot}
            specs={specs}
          />
        ))}
        {Array.from({ length: emptySlotCount }, (_, index) => <WireCardSlot key={`empty-standby-${lane.battlefieldId}-${index}`} label="待命" />)}
      </div>
    </section>
  );
}

function WireStandbySlotCard({
  interaction,
  objects,
  onInspectCard,
  onPreviewCard,
  slot,
  specs
}: {
  interaction: WireTableInteraction;
  objects: WireZoneObjects;
  onInspectCard: (card: InspectedCard) => void;
  onPreviewCard: (card?: InspectedCard) => void;
  slot: WireBattlefieldStandbySlot;
  specs: Record<string, BehaviorSpec>;
}) {
  const objectId = slot.objectId ?? slot.slotId;
  const object = slot.objectId ? objects[slot.objectId] : hiddenStandbyObject(slot);
  const spec = slot.objectId && object?.cardNo ? specs[object.cardNo] : undefined;

  return (
    <CardFace
      compact
      interactionHint={interaction.hintByObjectId[objectId]}
      interactionState={interaction.interactionByObjectId[objectId]}
      object={object}
      objectId={objectId}
      onInspect={onInspectCard}
      onPreview={onPreviewCard}
      selected={interaction.selectedObjectId === objectId}
      spec={spec}
      timelineState={interaction.timelineByObjectId[objectId]}
    />
  );
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
            interactionHint={interaction.hintByObjectId[lane.battlefieldId]}
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
      <WireCardFlow hintByObjectId={interaction.hintByObjectId} ids={ids} interactionByObjectId={interaction.interactionByObjectId} kind="battlefield-unit" minSlots={3} objects={objects} onInspectCard={onInspectCard} onPreviewCard={onPreviewCard} plan={plan} renderEmptySlots selectedObjectId={interaction.selectedObjectId} specs={specs} timelineByObjectId={interaction.timelineByObjectId} />
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

type WireCssProperties = CSSProperties & Record<`--${string}`, string | number>;

function wirePlanStyle(plan: WireCardFlowPlan): WireCssProperties {
  return {
    "--wire-card-h": `${plan.cardHeight}px`,
    "--wire-card-w": `${plan.cardWidth}px`,
    "--wire-flow-gap": `${plan.gap}px`,
    "--wire-flow-visible-slots": plan.visibleSlotCount
  };
}

function hiddenStandbyObject(slot: WireBattlefieldStandbySlot): CardObjectView {
  return {
    controllerId: slot.controllerId || undefined,
    isFaceDown: true,
    location: {
      battlefieldObjectId: slot.battlefieldObjectId,
      playerId: slot.sidePlayerId || undefined,
      zone: "BATTLEFIELD"
    },
    objectId: slot.slotId
  };
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
                <CardFace compact interactionHint={interaction.hintByObjectId[id]} interactionState={interaction.interactionByObjectId[id]} object={object} objectId={id} onInspect={onInspectCard} onPreview={onPreviewCard} selected={interaction.selectedObjectId === id} spec={object?.cardNo ? specs[object.cardNo] : undefined} timelineState={interaction.timelineByObjectId[id]} />
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

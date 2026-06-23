import { type CSSProperties, type ReactNode, useCallback, useEffect, useMemo, useRef, useState } from "react";
import { AppRoute } from "../app/router";
import { CardDetailDrawer } from "../components/cards/CardDetailDrawer";
import { CardFace, InspectedCard } from "../components/cards/CardFace";
import { ActionPanel } from "../components/match/ActionPanel";
import { ConnectionRecoveryPanel } from "../components/match/ConnectionRecoveryPanel";
import { EventLog } from "../components/match/EventLog";
import { CommandSubmissionFeedbackPanel, WireActionMapPanel } from "../components/match/WireActionMapPanel";
import { WireCommandCenterPanel } from "../components/match/WireCommandCenterPanel";
import { useDelayedWireCardPreview, WireCardPreview } from "../components/match/WireCardPreview";
import { WireInformationBoundaryPanel } from "../components/match/WireInformationBoundaryPanel";
import { WireInteractionPanel } from "../components/match/WireInteractionPanel";
import { WireMatchOverviewPanel } from "../components/match/WireMatchOverviewPanel";
import { WireObjectCommandTray } from "../components/match/WireObjectCommandTray";
import { WireSidePanelFocusStrip } from "../components/match/WireSidePanelFocusStrip";
import { WireSidePanelOperationPanel } from "../components/match/WireSidePanelOperationPanel";
import { WireSidePanelRuleChainStrip } from "../components/match/WireSidePanelRuleChainStrip";
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
import { buildServerQuickActionPlan, quickActionCommandUiSource, type ServerQuickActionEntry } from "../utils/serverQuickActionPlan";
import { buildServerSubmissionGatePlan } from "../utils/serverSubmissionGatePlan";
import { buildWireFocusedInteractionPlan } from "../utils/wireFocusedInteractionPlan";
import { buildWireObjectCommandTrayPlan } from "../utils/wireObjectCommandTrayPlan";
import { buildWireServerFlowProjectionPlan } from "../utils/wireServerFlowProjectionPlan";
import { buildWireSidePanelDirectoryPlan, type WireSidePanelDirectoryPlan } from "../utils/wireSidePanelDirectoryPlan";
import {
  buildWireSidePanelDirectoryLayerPlan,
  buildWireSidePanelDirectoryViewPlan
} from "../utils/wireSidePanelDirectoryViewPlan";
import { buildWireSidePanelControlPlan } from "../utils/wireSidePanelControlPlan";
import { buildWireSidePanelFramePlan } from "../utils/wireSidePanelFramePlan";
import { buildWireSidePanelFocusPlan } from "../utils/wireSidePanelFocusPlan";
import { buildWireSidePanelOrchestrationPlan, type WireSidePanelOrchestrationPlan } from "../utils/wireSidePanelOrchestrationPlan";
import { buildWireSidePanelRuleChainPlan } from "../utils/wireSidePanelRuleChainPlan";
import { buildWireSidePanelStackPlan, type WireSidePanelStackRailEntry } from "../utils/wireSidePanelStackPlan";
import { buildWireSidePanelOperationPlan } from "../utils/wireSidePanelOperationPlan";
import {
  buildWireSidePanelTransitionPlan,
  isStickyWireSidePanelState,
  type WireSidePanelNavigationSource,
  type WireSidePanelTransitionPlan
} from "../utils/wireSidePanelNavigationPlan";
import {
  WIRE_SIDE_PANEL_SHORT_LABELS,
  WIRE_SIDE_PANEL_TAB_BY_SLOT,
  WIRE_SIDE_PANEL_TABS,
  wireSidePanelTabPanelIdForSlot,
  type WireSidePanelTab
} from "../utils/wireSidePanelTabPlan";
import { buildWireRuleQueuePlan } from "../utils/wireRuleQueuePlan";

type WireTableInteraction = {
  hintByObjectId: Record<string, WireTableObjectHint | undefined>;
  interactionByObjectId: Record<string, PromptObjectState | undefined>;
  selectedObjectId?: string;
  timelineByObjectId: Record<string, WireTimelineObjectState | undefined>;
};

type WireSidePanelSelectionIntent = "auto" | "manual";

function commandUiSource(
  base: CommandSubmissionUiSource,
  routeSource?: Partial<CommandSubmissionUiSource>
): CommandSubmissionUiSource {
  return {
    ...routeSource,
    ...base,
    label: routeSource?.label ? `${base.label}：${routeSource.label}` : base.label
  };
}

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
  const [activeSidePanelSlot, setActiveSidePanelSlot] = useState<WireSidePanelSlot>("commandCenter");
  const [fixtureSubmissionFeedback, setFixtureSubmissionFeedback] = useState<CommandSubmissionFeedback | undefined>();
  const timelineDetailTriggerIdRef = useRef<string | undefined>(undefined);
  const sidePanelSelectionIntentRef = useRef<WireSidePanelSelectionIntent>("auto");
  const { previewCard, queuePreviewCard } = useDelayedWireCardPreview();
  const selectSidePanelSlot = useCallback((slot: WireSidePanelSlot, intent: WireSidePanelSelectionIntent = "manual") => {
    sidePanelSelectionIntentRef.current = intent;
    setActiveSidePanelSlot(slot);
  }, []);
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
  const selectedObjectCommandTrayPlan = useMemo(() => buildWireObjectCommandTrayPlan({
    card: inspectedCard,
    focusedPlan: selectedFocusPlan,
    objectContext: selectedObjectContext
  }), [inspectedCard, selectedFocusPlan, selectedObjectContext]);
  const ruleQueuePlan = useMemo(() => buildWireRuleQueuePlan({
    events: tableEvents,
    playerId: settings.playerId,
    prompt: tablePrompt,
    selectedObjectId,
    snapshot: tableSnapshot
  }), [selectedObjectId, settings.playerId, tableEvents, tablePrompt, tableSnapshot]);
  const sidePanelFocusPlan = useMemo(() => buildWireSidePanelFocusPlan({
    objectContext: selectedObjectContext,
    selectedObjectPlan: ruleQueuePlan.selectedObject,
    trayPlan: selectedObjectCommandTrayPlan
  }), [ruleQueuePlan.selectedObject, selectedObjectCommandTrayPlan, selectedObjectContext]);
  const sidePanelRuleChainPlan = useMemo(() => buildWireSidePanelRuleChainPlan({
    ruleQueuePlan
  }), [ruleQueuePlan]);
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
    selectSidePanelSlot("timelineDetail", "manual");
    setTimelineDetail(detail);
  }, [selectSidePanelSlot]);
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
    const restoreSlot = timelineDetail?.source === "event"
      ? "log"
      : timelineDetail?.source === "rule"
        ? "ruleQueue"
        : undefined;
    setTimelineDetail(undefined);
    setTimelineLayerOpen(false);
    if (restoreSlot) {
      selectSidePanelSlot(restoreSlot, "manual");
    }
    if (!triggerId) {
      return;
    }

    window.setTimeout(() => {
      const triggers = Array.from(document.querySelectorAll<HTMLButtonElement>("[data-wire-detail-id]"))
        .filter((button) => button.getAttribute("data-wire-detail-id") === triggerId);
      const trigger = triggers.find(isVisibleElement) ?? triggers[0];
      trigger?.focus();
    }, 0);
  }, [selectSidePanelSlot, timelineDetail?.id, timelineDetail?.source]);
  const runTopbarQuickAction = useCallback((entry: ServerQuickActionEntry) => {
    if (entry.disabled) {
      return;
    }

    if (entry.command) {
      submitTableCommand(entry.command, {
        ...quickActionCommandUiSource(entry),
        label: `顶部快捷：${entry.label}`,
        surface: "topbar"
      });
      return;
    }

    if (entry.directAction === "ready") {
      void controller.ready({
        ...quickActionCommandUiSource(entry),
        label: `顶部快捷：${entry.label}`,
        surface: "topbar"
      });
      return;
    }

    if (entry.directAction === "submitDeck") {
      void controller.submitStarterDeck({
        ...quickActionCommandUiSource(entry),
        label: `顶部快捷：${entry.label}`,
        surface: "topbar"
      });
    }
  }, [controller, submitTableCommand]);
  const sidePanelDirectory = useMemo(() => buildWireSidePanelDirectoryPlan(WIRE_TABLE_LAYOUT.sidePanel.slots), []);
  const sidePanelOrchestration = useMemo(() => buildWireSidePanelOrchestrationPlan({
    connectionStatus: tableConnectionStatus,
    directory: sidePanelDirectory,
    events: tableEvents,
    prompt: tablePrompt,
    selectedObjectId,
    selectionDraft,
    snapshot: tableSnapshot,
    submissionGate: tableSubmissionGate,
    timelineDetail
  }), [
    selectedObjectId,
    selectionDraft,
    sidePanelDirectory,
    tableConnectionStatus,
    tableEvents,
    tablePrompt,
    tableSnapshot,
    tableSubmissionGate,
    timelineDetail
  ]);
  const sidePanelEntryBySlot = useMemo(() => Object.fromEntries(
    sidePanelOrchestration.entries.map((entry) => [entry.slot, entry])
  ) as Record<WireSidePanelSlot, WireSidePanelOrchestrationPlan["entries"][number]>, [sidePanelOrchestration.entries]);
  const sidePanelFrame = useMemo(() => buildWireSidePanelFramePlan({
    activeSlot: activeSidePanelSlot,
    slots: WIRE_TABLE_LAYOUT.sidePanel.slots
  }), [activeSidePanelSlot]);
  const sidePanelStackPlan = useMemo(() => buildWireSidePanelStackPlan({
    activeSlot: activeSidePanelSlot,
    focusPlan: sidePanelFocusPlan,
    orchestration: sidePanelOrchestration,
    ruleChainPlan: sidePanelRuleChainPlan,
    submissionFeedback: tableSubmissionFeedback
  }), [
    activeSidePanelSlot,
    sidePanelFocusPlan,
    sidePanelOrchestration,
    sidePanelRuleChainPlan,
    tableSubmissionFeedback
  ]);
  const activeSidePanelTab = WIRE_SIDE_PANEL_TAB_BY_SLOT[activeSidePanelSlot] ?? "action";
  const activeSidePanelEntry = sidePanelEntryBySlot[activeSidePanelSlot];
  const sidePanelTransitionForSlot = useCallback((targetSlot: WireSidePanelSlot, source: WireSidePanelNavigationSource) => buildWireSidePanelTransitionPlan({
    activeSlot: activeSidePanelSlot,
    entries: sidePanelOrchestration.entries,
    primarySlot: sidePanelOrchestration.primarySlot,
    source,
    tabs: WIRE_SIDE_PANEL_TABS,
    targetSlot
  }), [activeSidePanelSlot, sidePanelOrchestration.entries, sidePanelOrchestration.primarySlot]);
  const sidePanelMainTransition = useMemo(
    () => sidePanelTransitionForSlot(activeSidePanelSlot, "auto"),
    [activeSidePanelSlot, sidePanelTransitionForSlot]
  );
  const sidePanelOperationPlan = useMemo(() => buildWireSidePanelOperationPlan({
    activeSlot: activeSidePanelSlot,
    focusPlan: sidePanelFocusPlan,
    orchestration: sidePanelOrchestration,
    prompt: tablePrompt,
    ruleChainPlan: sidePanelRuleChainPlan,
    submissionGate: tableSubmissionGate
  }), [
    activeSidePanelSlot,
    sidePanelFocusPlan,
    sidePanelOrchestration,
    sidePanelRuleChainPlan,
    tablePrompt,
    tableSubmissionGate
  ]);

  useEffect(() => {
    setActiveSidePanelSlot((current) => {
      const currentEntry = sidePanelEntryBySlot[current];
      if (currentEntry && sidePanelSelectionIntentRef.current === "manual") {
        return current;
      }
      if (currentEntry && isStickyWireSidePanelState(currentEntry.state)) {
        return current;
      }

      sidePanelSelectionIntentRef.current = "auto";
      return sidePanelOrchestration.primarySlot;
    });
  }, [sidePanelEntryBySlot, sidePanelOrchestration.primarySlot]);

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
          objectContextById={tableObjectContextModel.byId}
          onClearFocus={clearInspectedCard}
          onCommand={(command, routeSource) => submitTableCommand(command, commandUiSource({
            label: "指挥中心",
            objectId: selectedObjectId,
            surface: "command-center"
          }, routeSource))}
          onInspectObject={inspectObjectFromTable}
          onSelectFollowupEvent={selectFollowupEvent}
          onSelectServerEventKind={selectServerEventKind}
          playerId={settings.playerId}
          prompt={tablePrompt}
          selectionDraft={selectionDraft}
          selectedObjectId={selectedObjectId}
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
          objectContextById={tableObjectContextModel.byId}
          onChooseObject={chooseObjectFromActionMap}
          onCommand={(command, routeSource) => submitTableCommand(command, commandUiSource({
            label: "右侧合法操作",
            objectId: selectedObjectId,
            surface: "action-map"
          }, routeSource))}
          onInspectObject={inspectObjectFromTable}
          onSelectFollowupEvent={selectFollowupEvent}
          onSelectServerEventKind={selectServerEventKind}
          playerId={settings.playerId}
          prompt={tablePrompt}
          selectedObjectId={selectedObjectId}
          selectionDraft={selectionDraft}
          snapshot={tableSnapshot}
          showSubmissionFeedback={false}
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
          onCommand={(command, routeSource) => submitTableCommand(command, commandUiSource({
            label: "焦点卡牌和候选行动",
            objectId: selectedObjectId,
            surface: "interaction-panel"
          }, routeSource))}
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
          disabledByConnection={!tableSubmissionGate.canSubmit}
          events={tableEvents}
          focusedPlan={selectedFocusPlan}
          inspectedCard={inspectedCard}
          onClearInspectedCard={clearInspectedCard}
          onCommand={(command, routeSource) => submitTableCommand(command, commandUiSource({
            label: "规则队列对象托盘",
            objectId: selectedObjectId,
            surface: "rule-queue"
          }, routeSource))}
          onInspectObject={inspectObjectFromTable}
          onOpenDetail={openDetailCard}
          onSelectDetail={selectTimelineDetail}
          playerId={settings.playerId}
          plan={ruleQueuePlan}
          prompt={tablePrompt}
          selectedDetailId={timelineDetail?.id}
          selectedObjectId={selectedObjectId}
          snapshot={tableSnapshot}
          submissionGate={tableSubmissionGate}
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
          onCommand={(command, routeSource) => submitTableCommand(command, commandUiSource({
            detailId: timelineDetail?.id,
            label: "规则与事件详情",
            objectId: selectedObjectId,
            surface: "timeline-detail"
          }, routeSource))}
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
          onCommand={(command, routeSource) => submitTableCommand(command, commandUiSource({
            label: "服务端行动提示",
            surface: "action-prompt"
          }, routeSource))}
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
          <ConnectionRecoveryPanel
            actionsDisabled={layoutFixtureEnabled}
            connectionStatus={tableConnectionStatus}
            density="compact"
            hasSnapshot={Boolean(tableSnapshot)}
            lastSystemMessage={layoutFixtureEnabled ? "前端样例快照；真实连接状态请关闭样例模式。" : controller.state.lastSystemMessage}
            onConnect={() => void controller.join()}
            onDisconnect={() => void controller.disconnect()}
            onResync={() => void controller.requestSnapshot()}
            promptSnapshotTick={tablePrompt?.snapshotTick}
            snapshotTick={tableSnapshot?.tick}
            surface="match"
          />
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
            onCommand={(command, routeSource) => submitTableCommand(command, commandUiSource({
              label: "桌面对象命令托盘",
              objectId: selectedObjectId,
              surface: "object-command-tray"
            }, routeSource))}
            onOpenDetail={openDetailCard}
            prompt={tablePrompt}
            snapshot={tableSnapshot}
            submissionGate={tableSubmissionGate}
            trayPlan={selectedObjectCommandTrayPlan}
          />
        </section>

        <aside className="wire-side-panel" aria-label="行动与日志">
          <WireSidePanelDirectory
            activeSlot={activeSidePanelSlot}
            activeTab={activeSidePanelTab}
            onSelectSlot={selectSidePanelSlot}
            orchestration={sidePanelOrchestration}
            plan={sidePanelDirectory}
          />
          <WireSidePanelOperationPanel
            activeSlot={activeSidePanelSlot}
            onSelectSlot={selectSidePanelSlot}
            plan={sidePanelOperationPlan}
          />
          <div
            aria-label="右侧控制台摘要堆栈"
            className="wire-side-panel-rail-stack"
            data-wire-side-panel-rail-capacity-body-count={sidePanelStackPlan.renderedBodyCount}
            data-wire-side-panel-rail-capacity-max-weight={sidePanelStackPlan.capacityMaxWeight}
            data-wire-side-panel-rail-capacity-overflow={sidePanelStackPlan.capacityOverflow}
            data-wire-side-panel-rail-capacity-weight={sidePanelStackPlan.capacityWeight}
            data-wire-side-panel-rail-density={sidePanelStackPlan.density}
            data-wire-side-panel-rail-expanded-count={sidePanelStackPlan.expandedCount}
            data-wire-side-panel-rail-hidden-count={sidePanelStackPlan.hiddenCount}
            data-wire-side-panel-rail-stack
            data-wire-side-panel-rail-state={sidePanelStackPlan.state}
            data-wire-side-panel-rail-summary={sidePanelStackPlan.summary}
            data-wire-side-panel-rail-summary-count={sidePanelStackPlan.summaryCount}
            data-wire-side-panel-rail-visible-count={sidePanelStackPlan.visibleEntries.length}
          >
            <WireSidePanelRailEntry
              entry={sidePanelStackPlan.byRail.status}
              onSelectSlot={selectSidePanelSlot}
              transition={sidePanelStackPlan.byRail.status.actionSlot ? sidePanelTransitionForSlot(sidePanelStackPlan.byRail.status.actionSlot, "rail") : undefined}
            >
              <WireSidePanelStatus
                activeEntry={activeSidePanelEntry}
                canAct={canAct}
                connectionStatus={connectionStatusLabel(tableConnectionStatus)}
                orchestration={sidePanelOrchestration}
                phase={matchPhaseLabel(phase)}
                promptTitle={promptTitle}
                windowState={timingStateLabel(windowState)}
              />
            </WireSidePanelRailEntry>
            <WireSidePanelRailEntry
              entry={sidePanelStackPlan.byRail.focus}
              onSelectSlot={selectSidePanelSlot}
              transition={sidePanelStackPlan.byRail.focus.actionSlot ? sidePanelTransitionForSlot(sidePanelStackPlan.byRail.focus.actionSlot, "rail") : undefined}
            >
              <WireSidePanelFocusStrip
                inspectedCard={inspectedCard}
                onClear={clearInspectedCard}
                onOpenDetail={openDetailCard}
                onSelectDetail={selectTimelineDetail}
                onSelectSlot={selectSidePanelSlot}
                plan={sidePanelFocusPlan}
              />
            </WireSidePanelRailEntry>
            <WireSidePanelRailEntry
              entry={sidePanelStackPlan.byRail.rules}
              onSelectSlot={selectSidePanelSlot}
              transition={sidePanelStackPlan.byRail.rules.actionSlot ? sidePanelTransitionForSlot(sidePanelStackPlan.byRail.rules.actionSlot, "rail") : undefined}
            >
              <WireSidePanelRuleChainStrip
                onSelectDetail={selectTimelineDetail}
                onSelectSlot={selectSidePanelSlot}
                plan={sidePanelRuleChainPlan}
              />
            </WireSidePanelRailEntry>
            <WireSidePanelRailEntry
              entry={sidePanelStackPlan.byRail.receipt}
              onSelectSlot={selectSidePanelSlot}
              transition={sidePanelStackPlan.byRail.receipt.actionSlot ? sidePanelTransitionForSlot(sidePanelStackPlan.byRail.receipt.actionSlot, "rail") : undefined}
            >
              <div
                aria-label="服务端提交回执常驻区"
                className="wire-side-panel-receipt"
                data-wire-side-panel-receipt
                tabIndex={0}
              >
                <CommandSubmissionFeedbackPanel
                  contract={tablePrompt?.contract}
                  events={tableEvents}
                  feedback={tableSubmissionFeedback}
                  objectContextById={tableObjectContextModel.byId}
                  onInspectObject={inspectObjectFromTable}
                  onSelectFollowupEvent={selectFollowupEvent}
                  onSelectServerEventKind={selectServerEventKind}
                  selectedObjectId={selectedObjectId}
                  snapshot={tableSnapshot}
                  table={tableView}
                  variant="compact"
                />
              </div>
            </WireSidePanelRailEntry>
          </div>
          <div
            className="wire-side-panel-stack"
            data-wire-side-panel-active-slot={activeSidePanelSlot}
            data-wire-side-panel-active-tab={activeSidePanelTab}
            data-wire-side-panel-persistent-count={sidePanelFrame.persistentSlots.length}
            data-wire-side-panel-rail={sidePanelStackPlan.byRail.main.key}
            data-wire-side-panel-rail-action-label={sidePanelStackPlan.byRail.main.actionLabel}
            data-wire-side-panel-rail-action-slot={sidePanelStackPlan.byRail.main.actionSlot ?? ""}
            data-wire-side-panel-rail-actionable={false}
            data-wire-side-panel-rail-body-mode={sidePanelStackPlan.byRail.main.bodyMode}
            data-wire-side-panel-rail-capacity-weight={sidePanelStackPlan.byRail.main.capacityWeight}
            data-wire-side-panel-rail-mode={sidePanelStackPlan.byRail.main.mode}
            data-wire-side-panel-rail-priority={sidePanelStackPlan.byRail.main.priority}
            data-wire-side-panel-rail-reason={sidePanelStackPlan.byRail.main.reason}
            data-wire-side-panel-rail-state={sidePanelStackPlan.byRail.main.state}
            data-wire-side-panel-rail-target={sidePanelStackPlan.byRail.main.slot ?? ""}
            data-wire-side-panel-transition-from-slot={sidePanelMainTransition.fromSlot}
            data-wire-side-panel-transition-from-tab={sidePanelMainTransition.fromTab}
            data-wire-side-panel-transition-reason={sidePanelMainTransition.reason}
            data-wire-side-panel-transition-selectable={sidePanelMainTransition.selectable}
            data-wire-side-panel-transition-source={sidePanelMainTransition.source}
            data-wire-side-panel-transition-tab-change={sidePanelMainTransition.tabChanges}
            data-wire-side-panel-transition-target-slot={sidePanelMainTransition.targetSlot}
            data-wire-side-panel-transition-target-tab={sidePanelMainTransition.targetTab}
            data-wire-side-panel-visible-count={sidePanelFrame.visibleSlots.length}
          >
            {sidePanelFrame.entries.map((entry) => (
              <div
                aria-hidden={entry.ariaHidden}
                className="wire-side-panel-pane"
                data-wire-side-panel-pane={entry.slot}
                data-wire-side-panel-pane-active={entry.active}
                data-wire-side-panel-pane-region={entry.region}
                data-wire-side-panel-pane-visible={entry.visible}
                id={wireSidePanelTabPanelIdForSlot(entry.slot)}
                key={entry.slot}
                role={wireSidePanelTabPanelIdForSlot(entry.slot) ? "tabpanel" : undefined}
              >
                {sidePanelSections[entry.slot]}
              </div>
            ))}
          </div>
        </aside>
      </div>
      <CardDetailDrawer
        card={detailCard}
        disabledByConnection={!tableSubmissionGate.canSubmit}
        objectContext={detailObjectContext}
        onClose={() => setDetailCard(undefined)}
        onCommand={(command, routeSource) => submitTableCommand(command, commandUiSource({
          label: "卡牌详情抽屉",
          objectId: detailObjectId,
          surface: "card-detail"
        }, routeSource))}
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
        onCommand={(command, routeSource) => submitTableCommand(command, commandUiSource({
          detailId: timelineDetail?.id,
          label: "规则事件检查层",
          objectId: selectedObjectId,
          surface: "timeline-detail-layer"
        }, routeSource))}
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

function WireSidePanelRailEntry({
  children,
  entry,
  onSelectSlot,
  transition
}: {
  children: ReactNode;
  entry: WireSidePanelStackRailEntry;
  onSelectSlot: (slot: WireSidePanelSlot) => void;
  transition?: WireSidePanelTransitionPlan<WireSidePanelTab>;
}) {
  const targetSlot = transition?.targetSlot ?? entry.actionSlot;
  const selectable = entry.mode === "summary" && Boolean(targetSlot) && (transition?.selectable ?? true);

  return (
    <div
      className="wire-side-panel-rail-entry"
      data-wire-side-panel-rail={entry.key}
      data-wire-side-panel-rail-action-label={entry.actionLabel}
      data-wire-side-panel-rail-action-slot={entry.actionSlot ?? ""}
      data-wire-side-panel-rail-actionable={selectable}
      data-wire-side-panel-rail-body-mode={entry.bodyMode}
      data-wire-side-panel-rail-capacity-weight={entry.capacityWeight}
      data-wire-side-panel-rail-mode={entry.mode}
      data-wire-side-panel-rail-priority={entry.priority}
      data-wire-side-panel-rail-reason={entry.reason}
      data-wire-side-panel-rail-state={entry.state}
      data-wire-side-panel-rail-target={entry.slot ?? ""}
      data-wire-side-panel-transition-from-slot={transition?.fromSlot ?? ""}
      data-wire-side-panel-transition-from-tab={transition?.fromTab ?? ""}
      data-wire-side-panel-transition-reason={transition?.reason ?? ""}
      data-wire-side-panel-transition-selectable={transition?.selectable ?? false}
      data-wire-side-panel-transition-source={transition?.source ?? ""}
      data-wire-side-panel-transition-tab-change={transition?.tabChanges ?? false}
      data-wire-side-panel-transition-target-slot={transition?.targetSlot ?? ""}
      data-wire-side-panel-transition-target-tab={transition?.targetTab ?? ""}
    >
      {entry.mode === "summary" ? (
        <div className="wire-side-panel-rail-action">
          <span>{entry.label}</span>
          <strong>{entry.reason}</strong>
          <button
            data-wire-side-panel-rail-action={entry.key}
            disabled={!selectable || !targetSlot}
            onClick={() => {
              if (targetSlot) {
                onSelectSlot(targetSlot);
              }
            }}
            type="button"
          >
            {entry.actionLabel}
          </button>
        </div>
      ) : null}
      {entry.bodyMode === "collapsed" ? null : (
        <div
          className="wire-side-panel-rail-body"
          data-wire-side-panel-rail-body={entry.key}
          data-wire-side-panel-rail-body-mode={entry.bodyMode}
        >
          {children}
        </div>
      )}
    </div>
  );
}

function WireSidePanelDirectory({
  activeSlot,
  activeTab,
  onSelectSlot,
  orchestration,
  plan
}: {
  activeSlot: WireSidePanelSlot;
  activeTab: WireSidePanelTab;
  onSelectSlot: (slot: WireSidePanelSlot) => void;
  orchestration: WireSidePanelOrchestrationPlan;
  plan: WireSidePanelDirectoryPlan;
}) {
  const view = buildWireSidePanelDirectoryViewPlan({
    activeSlot,
    activeTab,
    entries: orchestration.entries,
    tabs: WIRE_SIDE_PANEL_TABS
  });
  const layers = buildWireSidePanelDirectoryLayerPlan({ directory: plan, view });
  const controlPlan = buildWireSidePanelControlPlan({ orchestration, view });
  const transitionForSlot = (targetSlot: WireSidePanelSlot, source: WireSidePanelNavigationSource) => buildWireSidePanelTransitionPlan({
    activeSlot,
    entries: orchestration.entries,
    primarySlot: orchestration.primarySlot,
    source,
    tabs: WIRE_SIDE_PANEL_TABS,
    targetSlot
  });
  const transitionForTab = (targetTab: WireSidePanelTab) => buildWireSidePanelTransitionPlan({
    activeSlot,
    entries: orchestration.entries,
    primarySlot: orchestration.primarySlot,
    source: "tab",
    tabs: WIRE_SIDE_PANEL_TABS,
    targetTab
  });

  return (
    <nav
      aria-label="右侧面板目录"
      className="wire-side-panel-directory"
      data-wire-side-panel-directory
      data-wire-side-panel-directory-active-count={orchestration.activeCount}
      data-wire-side-panel-directory-active-slot={activeSlot}
      data-wire-side-panel-directory-active-tab={activeTab}
      data-wire-side-panel-directory-count={plan.entries.length}
      data-wire-side-panel-directory-density={view.density}
      data-wire-side-panel-directory-hidden-count={view.hiddenCount}
      data-wire-side-panel-directory-index-mode={view.indexMode}
      data-wire-side-panel-directory-primary-slot={orchestration.primarySlot}
      data-wire-side-panel-directory-state={orchestration.state}
      data-wire-side-panel-directory-urgent-count={orchestration.urgentCount}
      data-wire-side-panel-directory-visible-count={view.visibleEntries.length}
      data-wire-side-panel-control-route-count={controlPlan.routeCount}
      data-wire-side-panel-control-state={controlPlan.state}
    >
      <div className="wire-side-panel-directory-summary" data-wire-side-panel-control-summary={controlPlan.state}>
        <h2>控制台</h2>
        <strong>{view.activeEntry.label}</strong>
        <span>{view.activeEntry.stateLabel} / {view.activeEntry.count}</span>
      </div>
      <ol className="wire-side-panel-control-routes" aria-label="右侧控制台路由摘要">
        {controlPlan.routes.map((route) => {
          const transition = route.slot ? transitionForSlot(route.slot, "control-route") : undefined;
          const selectable = Boolean(route.selectable && transition?.selectable);
          return (
            <li
              data-wire-side-panel-control-route={route.key}
              data-wire-side-panel-control-route-selectable={selectable}
              data-wire-side-panel-control-route-slot={route.slot ?? ""}
              data-wire-side-panel-control-route-state={route.state}
              data-wire-side-panel-control-route-tone={route.tone}
              data-wire-side-panel-transition-from-slot={transition?.fromSlot ?? ""}
              data-wire-side-panel-transition-from-tab={transition?.fromTab ?? ""}
              data-wire-side-panel-transition-reason={transition?.reason ?? ""}
              data-wire-side-panel-transition-selectable={transition?.selectable ?? false}
              data-wire-side-panel-transition-source={transition?.source ?? "control-route"}
              data-wire-side-panel-transition-tab-change={transition?.tabChanges ?? false}
              data-wire-side-panel-transition-target-slot={transition?.targetSlot ?? ""}
              data-wire-side-panel-transition-target-tab={transition?.targetTab ?? ""}
              key={`${route.key}:${route.slot ?? "none"}`}
            >
              <button
                aria-label={`${route.label}：${route.slotLabel}，${route.stateLabel}，${route.detail}`}
                data-wire-side-panel-control-route-action={route.key}
                disabled={!selectable || !transition}
                onClick={() => {
                  if (transition) {
                    onSelectSlot(transition.targetSlot);
                  }
                }}
                title={`${route.label} / ${route.slotLabel} / ${route.detail}`}
                type="button"
              >
                <span>{route.label}</span>
                <strong>{route.slotLabel}</strong>
                <small>{route.stateLabel} / {route.count}</small>
              </button>
            </li>
          );
        })}
      </ol>
      <div className="wire-side-panel-tabs" role="tablist" aria-label="右侧主面板">
        {view.tabs.map((tab) => {
          const transition = transitionForTab(tab.id);
          return (
            <button
              aria-selected={tab.active}
              data-wire-side-panel-tab={tab.id}
              data-wire-side-panel-tab-active={tab.active}
              data-wire-side-panel-tab-count={tab.count}
              data-wire-side-panel-tab-state={tab.state}
              data-wire-side-panel-tab-target-slot={transition.targetSlot}
              data-wire-side-panel-tab-urgent={tab.urgent}
              data-wire-side-panel-transition-from-slot={transition.fromSlot}
              data-wire-side-panel-transition-from-tab={transition.fromTab}
              data-wire-side-panel-transition-reason={transition.reason}
              data-wire-side-panel-transition-selectable={transition.selectable}
              data-wire-side-panel-transition-source={transition.source}
              data-wire-side-panel-transition-tab-change={transition.tabChanges}
              data-wire-side-panel-transition-target-slot={transition.targetSlot}
              data-wire-side-panel-transition-target-tab={transition.targetTab}
              key={tab.id}
              onClick={() => onSelectSlot(transition.targetSlot)}
              role="tab"
              type="button"
            >
              <span>{tab.label}</span>
              <small>{tab.count}</small>
            </button>
          );
        })}
      </div>
      <div className="wire-side-panel-directory-groups" data-wire-side-panel-directory-layer-count={layers.length}>
        {layers.map((layer) => (
          <section
            aria-label={`${layer.label}入口`}
            className="wire-side-panel-directory-group"
            data-wire-side-panel-directory-layer={layer.key}
            data-wire-side-panel-directory-layer-count={layer.count}
            key={layer.key}
          >
            <span>{layer.label}</span>
            <ol className="wire-side-panel-entry-grid" data-wire-side-panel-directory-index-mode={view.indexMode}>
              {layer.entries.map((entry) => {
                const shortLabel = WIRE_SIDE_PANEL_SHORT_LABELS[entry.slot];
                const transition = transitionForSlot(entry.slot, "directory");
                return (
                  <li
                    data-wire-side-panel-directory-active={entry.active}
                    data-wire-side-panel-directory-group={plan.bySlot[entry.slot].group}
                    data-wire-side-panel-directory-item={entry.slot}
                    data-wire-side-panel-directory-primary={entry.primary}
                    data-wire-side-panel-directory-state={entry.state}
                    data-wire-side-panel-directory-tab={entry.tabId}
                    key={entry.slot}
                  >
                    <a
                      aria-label={`${entry.order}. ${entry.label}：${entry.stateLabel}，${entry.detail}`}
                      aria-current={entry.active ? "page" : undefined}
                      data-wire-side-panel-directory-count-value={entry.count}
                      data-wire-side-panel-directory-label={entry.label}
                      data-wire-side-panel-directory-link={entry.slot}
                      data-wire-side-panel-directory-short-label={shortLabel}
                      data-wire-side-panel-directory-state={entry.state}
                      data-wire-side-panel-directory-tab={entry.tabId}
                      data-wire-side-panel-directory-tone={entry.tone}
                      data-wire-side-panel-transition-from-slot={transition.fromSlot}
                      data-wire-side-panel-transition-from-tab={transition.fromTab}
                      data-wire-side-panel-transition-reason={transition.reason}
                      data-wire-side-panel-transition-selectable={transition.selectable}
                      data-wire-side-panel-transition-source={transition.source}
                      data-wire-side-panel-transition-tab-change={transition.tabChanges}
                      data-wire-side-panel-transition-target-slot={transition.targetSlot}
                      data-wire-side-panel-transition-target-tab={transition.targetTab}
                      href={entry.href}
                      onClick={(event) => {
                        event.preventDefault();
                        onSelectSlot(transition.targetSlot);
                      }}
                      title={`${entry.label} / ${entry.stateLabel} / ${entry.detail}`}
                    >
                      <strong>{shortLabel}</strong>
                      <small>{entry.count}</small>
                      <em>{entry.stateLabel}</em>
                    </a>
                  </li>
                );
              })}
            </ol>
          </section>
        ))}
      </div>
    </nav>
  );
}

function WireSidePanelStatus({
  activeEntry,
  canAct,
  connectionStatus,
  orchestration,
  phase,
  promptTitle,
  windowState
}: {
  activeEntry?: WireSidePanelOrchestrationPlan["entries"][number];
  canAct: boolean;
  connectionStatus: string;
  orchestration: WireSidePanelOrchestrationPlan;
  phase: string;
  promptTitle: string;
  windowState: string;
}) {
  return (
    <section
      aria-label="右侧行动状态摘要"
      className="wire-side-panel-status"
      data-wire-side-panel-status
      data-wire-side-panel-status-active-slot={activeEntry?.slot ?? ""}
      data-wire-side-panel-status-state={orchestration.state}
    >
      <div>
        <small>窗口</small>
        <strong>{promptTitle}</strong>
        <span>{canAct ? "当前可操作" : `${phase} / ${windowState}`}</span>
      </div>
      <div>
        <small>下一步</small>
        <strong>{orchestration.stateLabel}</strong>
        <span>{orchestration.nextStepLabel}</span>
      </div>
      <div>
        <small>当前页</small>
        <strong>{activeEntry?.label ?? "指挥中心"}</strong>
        <span>{activeEntry?.detail ?? connectionStatus}</span>
      </div>
    </section>
  );
}

function isVisibleElement(element: HTMLElement): boolean {
  return element.offsetParent !== null || element.getClientRects().length > 0;
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
  const renderUnitZone = (zone: typeof layout.unitZones[number]) => {
    const lane = lanes[zone.laneIndex];
    const ids = zone.side === "self" ? lane.ownOccupants : lane.opposingOccupants;
    const standbySlots = lane.standbySlotsBySide[zone.side];
    return (
      <WireBattlefieldUnitZone
        ids={ids}
        interaction={interaction}
        key={zone.id}
        lane={lane}
        objects={objects}
        onInspectCard={onInspectCard}
        onPreviewCard={onPreviewCard}
        plan={unitPlan}
        side={zone.side}
        standbyPlan={standbyPlan}
        standbySlots={standbySlots}
        splitSource={lane.occupantSplitSource}
        specs={specs}
        title={battlefieldUnitZoneLabel(zone.laneIndex, zone.side)}
      />
    );
  };
  const unitZoneById = new Map(layout.unitZones.map((zone) => [zone.id, zone]));
  const battlefieldSections = {
    center: (
      <div className="wire-battlefield-center-grid" key="center" style={wireGridColumnsStyle(layout.centerColumns)}>
        {layout.laneZones.map((zone) => {
          const laneUnitZones = zone.unitZoneIds.map((id) => unitZoneById.get(id)).filter((unitZone): unitZone is typeof layout.unitZones[number] => Boolean(unitZone));
          const opponentZone = laneUnitZones.find((unitZone) => unitZone.side === "opponent");
          const selfZone = laneUnitZones.find((unitZone) => unitZone.side === "self");
          return (
            <section
              aria-label={`${zone.laneIndex === 0 ? "左战场" : "右战场"} 单位与待命区`}
              className="wire-battlefield-lane"
              data-wire-battlefield-lane-zone-id={zone.id}
              data-wire-battlefield-lane-index={zone.laneIndex}
              data-wire-battlefield-standby-zone-id={zone.standbyZoneId}
              key={zone.id}
              style={wireGridTemplateStyle(["minmax(0, 1fr)"], layout.laneRows)}
            >
              {opponentZone ? renderUnitZone(opponentZone) : null}
              {selfZone ? renderUnitZone(selfZone) : null}
            </section>
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

function WireBattlefieldStandbyZone({
  interaction,
  lane,
  objects,
  onInspectCard,
  onPreviewCard,
  plan,
  side,
  slots,
  specs
}: {
  interaction: WireTableInteraction;
  lane: WireBattlefieldLane;
  objects: WireZoneObjects;
  onInspectCard: (card: InspectedCard) => void;
  onPreviewCard: (card?: InspectedCard) => void;
  plan: WireCardFlowPlan;
  side: "opponent" | "self";
  slots: WireBattlefieldStandbySlot[];
  specs: Record<string, BehaviorSpec>;
}) {
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
      aria-label={`${lane.index === 0 ? "左战场" : "右战场"} ${side === "self" ? "我方" : "对方"}待命槽`}
      className="wire-battlefield-standby-zone"
      data-wire-battlefield-side={side}
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
  const visibleObject = slot.visible && slot.objectId ? objects[slot.objectId] : undefined;
  const objectId = visibleObject?.objectId ?? slot.slotId;
  const object = visibleObject ?? hiddenStandbyObject(slot);
  const spec = visibleObject?.cardNo ? specs[visibleObject.cardNo] : undefined;

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
  lane,
  objects,
  onInspectCard,
  onPreviewCard,
  plan,
  side,
  standbyPlan,
  standbySlots,
  splitSource,
  specs,
  title
}: {
  ids: string[];
  interaction: WireTableInteraction;
  lane: WireBattlefieldLane;
  objects: WireZoneObjects;
  onInspectCard: (card: InspectedCard) => void;
  onPreviewCard: (card?: InspectedCard) => void;
  plan: WireCardFlowPlan;
  side: "opponent" | "self";
  standbyPlan: WireCardFlowPlan;
  standbySlots: WireBattlefieldStandbySlot[];
  splitSource: WireBattlefieldLane["occupantSplitSource"];
  specs: Record<string, BehaviorSpec>;
  title: string;
}) {
  return (
    <section className="wire-battlefield-unit-zone" aria-label={title} data-wire-battlefield-side={side} data-wire-battlefield-split-source={splitSource}>
      <div className="wire-battlefield-unit-zone-body">
        <WireCardFlow hintByObjectId={interaction.hintByObjectId} ids={ids} interactionByObjectId={interaction.interactionByObjectId} kind="battlefield-unit" minSlots={3} objects={objects} onInspectCard={onInspectCard} onPreviewCard={onPreviewCard} plan={plan} renderEmptySlots selectedObjectId={interaction.selectedObjectId} specs={specs} timelineByObjectId={interaction.timelineByObjectId} />
        <WireBattlefieldStandbyZone interaction={interaction} lane={lane} objects={objects} onInspectCard={onInspectCard} onPreviewCard={onPreviewCard} plan={standbyPlan} side={side} slots={standbySlots} specs={specs} />
      </div>
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

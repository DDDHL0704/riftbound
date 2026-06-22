import type { WireSidePanelSlot } from "../components/match/wireTableLayout";

export type WireSidePanelFrameRegion = "main" | "persistent";

export type WireSidePanelFrameEntry = {
  active: boolean;
  ariaHidden: boolean;
  region: WireSidePanelFrameRegion;
  slot: WireSidePanelSlot;
  visible: boolean;
};

export type WireSidePanelFramePlan = {
  activeSlot: WireSidePanelSlot;
  entries: WireSidePanelFrameEntry[];
  mainSlots: WireSidePanelSlot[];
  persistentSlots: WireSidePanelSlot[];
  visibleSlots: WireSidePanelSlot[];
};

export function buildWireSidePanelFramePlan({
  activeSlot,
  persistentSlots = ["serverFlow"],
  slots
}: {
  activeSlot: WireSidePanelSlot;
  persistentSlots?: readonly WireSidePanelSlot[];
  slots: readonly WireSidePanelSlot[];
}): WireSidePanelFramePlan {
  const slotSet = new Set(slots);
  const persistentSet = new Set<WireSidePanelSlot>();

  for (const slot of persistentSlots) {
    if (!slotSet.has(slot)) {
      throw new Error(`Persistent side panel slot is not in layout: ${slot}`);
    }
    if (persistentSet.has(slot)) {
      throw new Error(`Duplicate persistent side panel slot: ${slot}`);
    }
    persistentSet.add(slot);
  }

  if (!slotSet.has(activeSlot)) {
    throw new Error(`Active side panel slot is not in layout: ${activeSlot}`);
  }

  const entries = slots.map((slot) => {
    const persistent = persistentSet.has(slot);
    const active = slot === activeSlot;
    const visible = persistent || active;
    return {
      active,
      ariaHidden: !visible,
      region: persistent ? "persistent" : "main",
      slot,
      visible
    } satisfies WireSidePanelFrameEntry;
  });

  return {
    activeSlot,
    entries,
    mainSlots: slots.filter((slot) => !persistentSet.has(slot)),
    persistentSlots: slots.filter((slot) => persistentSet.has(slot)),
    visibleSlots: entries.filter((entry) => entry.visible).map((entry) => entry.slot)
  };
}

export type ArenaActionRect = {
  bottom: number;
  height: number;
  left: number;
  right: number;
  top: number;
  width: number;
};

export type ArenaActionPlacementName =
  | "above"
  | "below"
  | "left"
  | "right"
  | "top-left"
  | "top-center"
  | "top-right"
  | "bottom-left"
  | "bottom-center"
  | "bottom-right";

export type ArenaActionPlacement = {
  anchorOverlapArea: number;
  left: number;
  placement: ArenaActionPlacementName;
  protectedOverlapArea: number;
  top: number;
};

export function chooseArenaActionPlacement(input: {
  anchor: ArenaActionRect;
  host: ArenaActionRect;
  panelHeight: number;
  panelWidth: number;
  protectedRects: ArenaActionRect[];
  gap?: number;
  margin?: number;
}): ArenaActionPlacement {
  const margin = input.margin ?? 12;
  const gap = input.gap ?? 10;
  const panelWidth = Math.min(input.panelWidth, Math.max(0, input.host.width - margin * 2));
  const panelHeight = Math.min(input.panelHeight, Math.max(0, input.host.height - margin * 2));
  const anchorCenterX = input.anchor.left - input.host.left + input.anchor.width / 2;
  const anchorCenterY = input.anchor.top - input.host.top + input.anchor.height / 2;
  const minLeft = margin;
  const maxLeft = Math.max(minLeft, input.host.width - panelWidth - margin);
  const minTop = margin;
  const maxTop = Math.max(minTop, input.host.height - panelHeight - margin);
  const centeredLeft = anchorCenterX - panelWidth / 2;
  const centeredTop = anchorCenterY - panelHeight / 2;
  const hostCenterLeft = input.host.width / 2 - panelWidth / 2;

  const candidates: Array<{ left: number; placement: ArenaActionPlacementName; top: number }> = [
    { left: centeredLeft, placement: "above", top: input.anchor.top - input.host.top - panelHeight - gap },
    { left: centeredLeft, placement: "below", top: input.anchor.bottom - input.host.top + gap },
    { left: input.anchor.left - input.host.left - panelWidth - gap, placement: "left", top: centeredTop },
    { left: input.anchor.right - input.host.left + gap, placement: "right", top: centeredTop },
    { left: minLeft, placement: "top-left", top: minTop },
    { left: hostCenterLeft, placement: "top-center", top: minTop },
    { left: maxLeft, placement: "top-right", top: minTop },
    { left: minLeft, placement: "bottom-left", top: maxTop },
    { left: hostCenterLeft, placement: "bottom-center", top: maxTop },
    { left: maxLeft, placement: "bottom-right", top: maxTop }
  ];

  return candidates
    .map((candidate, index) => {
      const left = clamp(candidate.left, minLeft, maxLeft);
      const top = clamp(candidate.top, minTop, maxTop);
      const rect = viewportRect(input.host.left + left, input.host.top + top, panelWidth, panelHeight);
      const protectedOverlapArea = input.protectedRects.reduce(
        (total, protectedRect) => total + overlapArea(rect, protectedRect),
        0
      );
      const anchorOverlapArea = overlapArea(rect, input.anchor);
      const panelCenterX = left + panelWidth / 2;
      const panelCenterY = top + panelHeight / 2;
      const distance = Math.hypot(panelCenterX - anchorCenterX, panelCenterY - anchorCenterY);

      return {
        anchorOverlapArea,
        distance,
        index,
        left,
        placement: candidate.placement,
        protectedOverlapArea,
        top
      };
    })
    .sort((left, right) => (
      left.protectedOverlapArea - right.protectedOverlapArea
      || left.anchorOverlapArea - right.anchorOverlapArea
      || left.distance - right.distance
      || left.index - right.index
    ))[0];
}

export function overlapArea(first: ArenaActionRect, second: ArenaActionRect): number {
  return Math.max(0, Math.min(first.right, second.right) - Math.max(first.left, second.left))
    * Math.max(0, Math.min(first.bottom, second.bottom) - Math.max(first.top, second.top));
}

function clamp(value: number, minimum: number, maximum: number): number {
  return Math.min(maximum, Math.max(minimum, value));
}

function viewportRect(left: number, top: number, width: number, height: number): ArenaActionRect {
  return {
    bottom: top + height,
    height,
    left,
    right: left + width,
    top,
    width
  };
}

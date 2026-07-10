# Riftbound Godot Client Assets

This client loads official card art from catalog URLs by default. Selected
official images may be committed under `assets/cards/` when offline stability or
deterministic visual verification requires them; record the source here.

## Sources

| Path | Source | License | Notes |
| --- | --- | --- | --- |
| `icon.svg` | Local placeholder | Project-local | Temporary vector icon for the Godot project. |
| `scripts/RunestoneBackdrop.cs`, `scripts/RunestoneTheme.cs` | Project-authored legacy procedural visual theme | Project-local | Current runtime only. The black/ivory inksteel route is being replaced by a neutral minimal theme. |
| `scripts/CardControlRenderer.cs`, `scripts/Main.cs`, `scenes/Main.tscn` | Project-authored legacy UI and motion | Project-local | Current runtime only. Wire-table frames and the permanent prompt rail will be removed after replacement parity. |
| Runtime card fronts | `frontImage` URLs from `data/official/card-catalog.zh-CN.json` | Riot / official card art | Loaded at runtime into `user://official-card-cache`; this remains the default path. |

## Approved Asset Direction

- Selected route: minimal rules-first table using official card fronts as the
  complete face presentation.
- Runtime additions should be limited to a neutral table surface, one card back,
  compact zone surfaces, familiar action/state icons, and restrained feedback.
- Do not generate or draw ornamental card frames over official images.
- The earlier black/white inksteel concept and the later ornate full-table mock
  are rejected explorations. They are not runtime assets or implementation
  references.
- Design specification:
  `../docs/2026-07-10-minimal-official-card-client-design.md`.

## Future Asset Rules

- `assets/backs/`, `assets/backgrounds/`, `assets/cards/`, `assets/icons/`,
  `assets/vfx/`, `assets/ui/`, and `assets/audio/` should contain only original,
  official, licensed, or generated assets with source notes here.
- `assets/frames/` is not needed for normal card faces. Keep it only for a future
  asset that does not cover or restyle official card artwork.
- Generated assets must record prompt and seed when available.
- Official card images are the preferred card-face source while available. They are loaded from catalog URLs at runtime and cached locally outside git.

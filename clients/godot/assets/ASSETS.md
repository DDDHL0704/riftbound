# Riftbound Godot Client Assets

This client loads official card art from catalog URLs by default. Selected
official images may be committed under `assets/cards/` when offline stability or
deterministic visual verification requires them; record the source here.

## Sources

| Path | Source | License | Notes |
| --- | --- | --- | --- |
| `icon.svg` | Local placeholder | Project-local | Temporary vector icon for the Godot project. |
| Runtime card fronts | `frontImage` URLs from `data/official/card-catalog.zh-CN.json` | Riot / official card art | Loaded at runtime into `user://official-card-cache`; this remains the default path. |
| `scenes/components/OfficialCardView.tscn` | Project-authored minimal wrapper around runtime official card fronts | Project-local | Adds only neutral fallback, count badge, focus, selection, target, disabled, and hidden states outside the official face. |
| `scripts/ui/CardTextureLoader.cs` | Project-authored runtime loader | Project-local | Decodes cached official PNG/JPEG/WebP fronts and rotates official battlefield images when the safe snapshot requests it; no new raster asset. |
| `scenes/components/ActionBar.tscn` | Project-authored minimal UI | Project-local | Localized server action and selection controls; no raster asset or custom card treatment was introduced. |
| `scenes/overlays/CardInspectOverlay.tscn`, `scenes/overlays/ResultOverlay.tscn` | Project-authored minimal UI | Project-local | Uses theme surfaces and runtime official card art; no additional raster assets were introduced. |
| `scenes/overlays/MulliganOverlay.tscn`, `TriggerOrderOverlay.tscn`, `DamageAssignmentOverlay.tscn` | Project-authored minimal UI | Project-local | Focused server-prompt controls. Mulligan reuses runtime official card fronts; trigger and damage overlays add no raster assets. |
| Final `match-sequence-NN.png` / `match-sequence.mp4` evidence | Captured from visible Godot runtime | Project evidence | Packaged by the playtest tools when present; these are verification artifacts, not runtime assets or design references. |
| `screenshots/units/m8-minimal-client-flow-16s.mp4` | Visible Godot runtime captures composed with OpenCV `mp4v` | Project evidence | 1440x900, 12 fps, 202 frames; documents the minimal official-card client flow and adds no runtime asset. |

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

# Riftbound Godot Client Assets

This client keeps official card art as runtime-loaded remote content only.
Do not commit Riot/official `frontImage` card images into this repository.

## Sources

| Path | Source | License | Notes |
| --- | --- | --- | --- |
| `icon.svg` | Local placeholder | Project-local | Temporary vector icon for the Godot project. |
| `scripts/RunestoneBackdrop.cs`, `scripts/RunestoneTheme.cs` | Project-authored procedural visual theme | Project-local | User selected style route A: dark basalt rune-carved tabletop with brass/rune accents. No external binary asset is used at runtime for this first pass. |
| Runtime card fronts | `frontImage` URLs from `data/official/card-catalog.zh-CN.json` | Riot / official card art | Loaded at runtime into `user://official-card-cache`; not committed to git. |

## Generated Style Checkpoint

- Selected route: A, rune-carved stone tabletop.
- Prompt summary: preserve the existing opponent rail / opponent home / two-lane battlefield / self home / self hand / right preview-prompt layout; dark basalt, engraved glowing rune grid lines, subtle gold edge inlays, readable card sockets, fantasy TCG atmosphere; no logos, characters, or card text.
- Generated concept image: kept outside the repository under the local Codex generated image cache and used only as visual direction. It is not a runtime asset.

## Future Asset Rules

- `assets/frames/`, `assets/backs/`, `assets/backgrounds/`, `assets/icons/`, `assets/vfx/`, and `assets/ui/` should contain only original, licensed, or generated assets with source notes here.
- Generated assets must record prompt and seed when available.
- Official card images are the preferred card-face source while available. They are loaded from catalog URLs at runtime and cached locally outside git.

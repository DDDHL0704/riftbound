# Riftbound Godot Client Assets

This client keeps official card art as runtime-loaded remote content only.
Do not commit Riot/official `frontImage` card images into this repository.

## Sources

| Path | Source | License | Notes |
| --- | --- | --- | --- |
| `icon.svg` | Local placeholder | Project-local | Temporary vector icon for the Godot project. |

## Future Asset Rules

- `assets/frames/`, `assets/backs/`, `assets/backgrounds/`, `assets/icons/`, `assets/vfx/`, and `assets/ui/` should contain only original, licensed, or generated assets with source notes here.
- Generated assets must record prompt and seed when available.
- Official card images are loaded from catalog URLs at runtime and cached locally outside git.

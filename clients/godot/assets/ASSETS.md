# Riftbound Godot Client Assets

This client keeps official card art as runtime-loaded remote content only.
Do not commit Riot/official `frontImage` card images into this repository.

## Sources

| Path | Source | License | Notes |
| --- | --- | --- | --- |
| `icon.svg` | Local placeholder | Project-local | Temporary vector icon for the Godot project. |
| `scripts/RunestoneBackdrop.cs`, `scripts/RunestoneTheme.cs` | Project-authored procedural visual theme | Project-local | User selected style route C: black-white inksteel tabletop with restrained crimson and antique-gold accents. No external binary asset is used at runtime for this pass. |
| Runtime card fronts | `frontImage` URLs from `data/official/card-catalog.zh-CN.json` | Riot / official card art | Loaded at runtime into `user://official-card-cache`; not committed to git. |

## Generated Style Checkpoint

- Selected route: C, black-white ink battlefield with restrained red and gold power accents.
- Prompt summary: preserve the existing opponent rail / opponent home / two-lane battlefield / self home / self hand / right preview-prompt layout; near-black ink panels, warm ivory lines, dark steel bevels, crimson active markers, small antique-gold highlights, readable card sockets, premium fantasy TCG atmosphere; no logos, characters, or readable card text in the style sample.
- Generated concept image: `/Users/dinghaolin/.codex/generated_images/019f1b45-2249-77f0-b86c-3b32f9544ef4/ig_0789e3792a80c753016a44bd0481708199997913ddaabbf394.png`. This image was used only as visual direction; it is not a runtime asset.

## Future Asset Rules

- `assets/frames/`, `assets/backs/`, `assets/backgrounds/`, `assets/icons/`, `assets/vfx/`, and `assets/ui/` should contain only original, licensed, or generated assets with source notes here.
- Generated assets must record prompt and seed when available.
- Official card images are the preferred card-face source while available. They are loaded from catalog URLs at runtime and cached locally outside git.

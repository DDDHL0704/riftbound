# Godot Card Client Reference Notes

This note records reference projects and Godot patterns that are useful for the
Riftbound Godot client. It is intentionally not a vendored dependency list.

## Reference Sources

- Godot Card Framework: https://github.com/chun92/card-framework
  - Useful pattern: `CardManager` as orchestration root, `Card` as reusable node,
    `CardContainer` base with `Pile` and `Hand` specializations, JSON/card-data
    factory, and signal-driven movement history.
  - Adoption: use the manager/factory/container vocabulary for our own C# code.
    Do not import the addon because our authoritative server already owns rules,
    zones, legality, and hidden information.
- Godot Card Pile Framework: https://github.com/Ggross98/Godot-CardPileFramework
  - Useful pattern: C# card objects as `Control` nodes, typed pile managers, JSON
    save/load boundaries, and animation hooks around draw/use/discard actions.
  - Adoption: favor small C# services around card images, prompts, and zone
    rendering instead of growing `Main.cs` indefinitely.
- Smart Drag and Drop Cards:
  https://github.com/SesinIvan/relocatable-drag-and-drop-cards
  - Useful pattern: a card remembers its home field, returns if dropped outside a
    legal field, and fields decide insertion/reorder location.
  - Adoption: later drag/drop should only visualize server-provided prompt
    candidates. Dropping outside a legal prompt target should snap back.
- Godot official docs:
  - `Control` GUI/input APIs: https://docs.godotengine.org/en/stable/classes/class_control.html
  - Containers: https://docs.godotengine.org/en/stable/tutorials/ui/gui_containers.html
  - Resources: https://docs.godotengine.org/en/stable/tutorials/scripting/resources.html
  - Adoption: keep cards/zones as GUI `Control` trees and use containers/layout
    managers first. Use exported configuration/resources for later visual tuning
    rather than hard-coding sizes inside prompt or transport code.

## Project-Specific Decisions

- The server remains the only rules engine. Card containers never decide whether
  a play, move, payment, trigger order, or combat-damage assignment is legal.
- A card interaction is valid only when it maps to the current server prompt
  candidate and command template/metadata.
- Hidden cards are not `Card` objects with concealed front data. They are explicit
  hidden card backs/counts built from the viewer snapshot.
- Official `frontImage` remains the preferred visible card-face source. It is
  cached under `user://official-card-cache` and is never committed.
- Future visual work should be layered behind these boundaries:
  `SnapshotProjection -> CardViewData -> Zone/Hand/Pile Renderer -> Effects`.
- Future drag/drop work should be layered as:
  `PromptCandidate -> LegalInteractionPlan -> Hover/Drop affordance -> SubmitIntent`.

## Immediate Engineering Direction

1. Continue extracting data-driven planners from `Main.cs` before adding visual
   complexity.
2. Build reusable card/zone controls only after the prompt and snapshot models
   are clean enough to feed them.
3. Add manual UI for complex prompt choices before adding drag/drop shortcuts.
4. Keep smoke automation server-driven: it may choose the first exposed legal
   candidate, but must not compute legality locally.

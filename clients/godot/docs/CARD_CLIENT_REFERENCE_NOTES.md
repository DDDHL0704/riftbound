# Godot Card Client Reference Notes

This note records reference projects and Godot patterns that are useful for the
Riftbound Godot client. It is intentionally not a vendored dependency list.

## Reference Sources

- Godot Card Framework: https://github.com/chun92/card-framework
  - Useful pattern: `CardManager` as orchestration root, `Card` as reusable node,
    `CardContainer` base with `Pile` and `Hand` specializations, JSON/card-data
    factory, and signal-driven movement history.
  - Fresh check: current README positions it as a Godot 4.6+ addon with drag and
    drop, pile/hand containers, JSON card data, editor previews, factory
    patterns, and event systems.
  - Adoption: use the manager/factory/container vocabulary for our own C# code.
    Do not import the addon because our authoritative server already owns rules,
    zones, legality, and hidden information.
- Godot Card Game Framework: https://github.com/db0/godot-card-game-framework
  - Useful pattern: mature card UI affordances such as hover focus, larger card
    viewer, multiple hands/piles, grid/free-form board placement, card rotation,
    face-down state, targeting arrows, tokens, and GUT regression tests.
  - Fresh check: its README explicitly includes a card scripting/rules engine,
    rich manipulation buttons, deck builder, card library, and broad tabletop
    interactions.
  - Adoption: borrow UI affordance ideas and regression-test discipline only.
    Do not import or mirror the scripting engine because Riftbound legality,
    timing, scoring, and victory remain server-authoritative.
- Simple CardPileUI:
  https://github.com/insideout-andrew/simple-card-pile-ui
  - Useful pattern: explicit `CardPileUI`, `CardUIData`, `CardUI`, and
    `CardDropzone` concepts with draw, hand, discard, dropzone, hover, click,
    and card-movement signals.
  - Fresh check: its README emphasizes configurable pile positions, stack
    display gaps, hand spread, hover behavior, JSON card data, and signals for
    pile/dropzone mutations.
  - Adoption: mirror the pile/dropzone vocabulary for client-side rendering
    nodes. Server prompts still decide whether a drop/click is legal.
- Deckbuilder Framework:
  https://github.com/insideout-andrew/deckbuilder-framework
  - Useful pattern: cards are instantiated from data resources, decks emit
    card-level signals, and deck behavior is configured with spread, stack,
    drag, rotation, and vertical hand curves.
  - Fresh check: its README separates `CardData`, `Card`, and `Deck` and notes
    that game code can listen to deck signals instead of wiring every card
    individually.
  - Adoption: build our own C# `CardViewData -> CardNode -> Zone/Deck
    renderer` boundary so visible/hidden card behavior is consistent across
    hand, battlefield, base, discard, and preview surfaces.
- Godot Card Pile Framework: https://github.com/Ggross98/Godot-CardPileFramework
  - Useful pattern: C# card objects as `Control` nodes, typed pile managers, JSON
    save/load boundaries, and animation hooks around draw/use/discard actions.
  - Fresh check: its README explicitly targets Godot 4.6.2 + .NET 8.0 and uses
    C# `Control` card objects, pile managers, JSON persistence, and signal-based
    animation hooks.
  - Adoption: favor small C# services around card images, prompts, and zone
    rendering instead of growing `Main.cs` indefinitely.
- Smart Drag and Drop Cards:
  https://github.com/SesinIvan/relocatable-drag-and-drop-cards
  - Useful pattern: a card remembers its home field, returns if dropped outside a
    legal field, and fields decide insertion/reorder location.
  - Fresh check: its README separates `Card` and `Field` scenes, uses a state
    machine for hover/click/drag/release, reparents dragged cards above the board,
    and snaps illegal drops back to the home field.
  - Adoption: later drag/drop should only visualize server-provided prompt
    candidates. Dropping outside a legal prompt target should snap back.
- Godot official docs:
  - `Control` GUI/input APIs: https://docs.godotengine.org/en/stable/classes/class_control.html
  - Containers: https://docs.godotengine.org/en/stable/tutorials/ui/gui_containers.html
  - Resources: https://docs.godotengine.org/en/stable/tutorials/scripting/resources.html
  - Adoption: keep cards/zones as GUI `Control` trees and use Godot containers
    for responsive layout first. Use exported configuration/resources for later
    visual tuning rather than hard-coding sizes inside prompt or transport code.

## Open-Source Lessons To Keep

- Borrow structure, not rules. Open-source card frameworks often validate drops
  or game legality inside containers; our containers only render server snapshots
  and expose server prompt affordances.
- Split future Godot code into small layers:
  `Hub Transport -> Session/Lobby Controller -> Snapshot Projection -> Zone
  Renderer -> Card Control -> Interaction Affordances`.
- Treat all piles, hands, battlefields, bases, discard/play piles, and hidden
  cards as zone/container renderers fed by view data. This makes the later visual
  pass safer than direct coordinate edits.
- Drag/drop should be a late affordance over prompts: card remembers its home
  zone, candidate zones are highlighted from the current prompt, legal drops
  submit the server command template, and everything else snaps back.
- Result/settlement UI should be event-driven. The client displays a winner only
  from authoritative server events such as `MATCH_WON` or equivalent snapshot
  state, never from local score arithmetic.

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
2. Build reusable card/zone/result controls only after the prompt, snapshot, and
   event models are clean enough to feed them.
3. Add manual UI for complex prompt choices before adding drag/drop shortcuts.
4. Keep smoke automation server-driven: it may choose the first exposed legal
   candidate, including surrender for result smoke, but must not compute
   legality locally.

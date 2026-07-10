# Minimal Official-Card Godot Client Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the wire-table/debug presentation with a minimal, responsive,
official-card-first Godot client that two humans can use to complete a match
without logs or technical identifiers.

**Architecture:** Keep the existing SignalR/HTTP clients, snapshot mapping,
command submission, session isolation, and evidence tooling. Migrate view
ownership incrementally from `Main.cs` into focused Godot scenes and C# controls;
direct interaction state is derived only from the current server prompt and is
cleared whenever the prompt identity changes.

**Tech Stack:** Godot 4.7 .NET, C# / .NET 8, Godot Control scenes, SignalR,
existing `Riftbound.Contracts`, shell-based integrity checks, visible two-window
playtests.

## Global Constraints

- Modify only `clients/godot/` unless the user explicitly expands scope.
- Do not change engine rules, contracts, API behavior, or React DevUi.
- Render only per-player snapshots and prompt candidates supplied by the server.
- Opponent hands, hidden standby cards, and face-down cards remain backs/counts.
- Official `frontImage` art is the complete face presentation; do not draw an
  ornamental frame over it.
- Reference viewport is 1440x900, large viewport is 1920x1080, and minimum
  supported viewport is 1280x720.
- Primary text is at least 16 px, secondary text at least 14 px, and primary
  targets at least 40 px high at 1440x900.
- Auto-smoke proves protocol regression only. Every visual task needs a visible
  Godot window and direct screenshot inspection.
- Preserve the existing hidden-information log check and final two-human package
  requirements.
- Design authority:
  `clients/godot/docs/2026-07-10-minimal-official-card-client-design.md`.

---

### Task 1: Minimal Theme and Official Card Component

**Files:**
- Create: `clients/godot/scripts/ui/MinimalTheme.cs`
- Create: `clients/godot/scripts/ui/OfficialCardVisualState.cs`
- Create: `clients/godot/scripts/ui/OfficialCardView.cs`
- Create: `clients/godot/scenes/components/OfficialCardView.tscn`
- Create: `clients/godot/tools/check-minimal-card-component.sh`
- Modify: `clients/godot/STRUCTURE.md`
- Modify: `clients/godot/assets/ASSETS.md`

**Interfaces:**
- Consumes: the dictionaries returned by `CardViewData.ToGodotDictionary()` and
  cached `imagePath` values from `OfficialCardImageLoader`.
- Produces: `OfficialCardView.Display(Dictionary card, OfficialCardVisualState state)`,
  `OfficialCardView.Clear()`, and `OfficialCardView.Activated(Dictionary card)`.

- [x] **Step 1: Write the failing component integrity check**

```bash
#!/usr/bin/env bash
set -euo pipefail
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
rg -q 'class OfficialCardView' "$root/clients/godot/scripts/ui/OfficialCardView.cs"
rg -q 'PreserveOfficialAspect' "$root/clients/godot/scripts/ui/OfficialCardView.cs"
rg -q 'OfficialCardView.cs' "$root/clients/godot/scenes/components/OfficialCardView.tscn"
rg -q 'enum OfficialCardVisualState' "$root/clients/godot/scripts/ui/OfficialCardVisualState.cs"
```

- [x] **Step 2: Run the check and confirm it fails because the component is absent**

Run: `clients/godot/tools/check-minimal-card-component.sh`

Expected: non-zero exit with `OfficialCardView.cs` missing.

- [x] **Step 3: Add minimal theme tokens and visual state**

```csharp
namespace Riftbound.GodotClient.Ui;

public enum OfficialCardVisualState
{
    Normal,
    Selectable,
    Selected,
    LegalTarget,
    HostileTarget,
    Disabled,
    Hidden
}
```

`MinimalTheme.cs` defines graphite surfaces, off-white text, cool-gray secondary
text, green selectable, amber selected, red hostile/destructive, and blue-gray
waiting colors. It exposes `Apply(Control root)`, `Panel(Color background)`, and
`Outline(OfficialCardVisualState state)`; it does not reference `RunestoneTheme`.

- [x] **Step 4: Implement the official card control**

```csharp
public partial class OfficialCardView : PanelContainer
{
    [Signal]
    public delegate void ActivatedEventHandler(Godot.Collections.Dictionary card);

    public bool PreserveOfficialAspect => true;

    public void Display(
        Godot.Collections.Dictionary card,
        OfficialCardVisualState state)
    {
        // Store only the server-visible dictionary, load imagePath when present,
        // use a neutral fallback when absent, and apply an external state outline.
    }

    public void Clear()
    {
        // Remove visible identity, texture, tooltip, and interaction state.
    }
}
```

The scene contains one `TextureRect` using `KeepAspectCentered`, one fallback
label layer, one external state border, and one count badge. `Display` refuses to
load `imagePath` when `visible=false` or `faceDown=true` and emits `Activated`
only for non-disabled interactive states.

- [x] **Step 5: Build and run the integrity check**

Run:

```bash
~/.dotnet/dotnet build clients/godot/Riftbound.GodotClient.csproj
clients/godot/tools/check-minimal-card-component.sh
```

Expected: build succeeds with zero errors and the check exits 0.

- [x] **Step 6: Create a visible component proof scene and inspect it**

Use Godot MCP to instantiate `OfficialCardView.tscn` in a temporary editor scene
with one visible official card dictionary, one hidden card, and each interaction
state. Capture at 1440x900 and inspect that the official face is uncropped, the
hidden card has no identity, and state outlines sit outside the art.

- [x] **Step 7: Update docs and commit**

```bash
git add clients/godot/scripts/ui clients/godot/scenes/components \
  clients/godot/tools/check-minimal-card-component.sh \
  clients/godot/STRUCTURE.md clients/godot/assets/ASSETS.md
git commit -m "Add official Godot card component" \
  -m "Why: official card art must be readable before rebuilding the table."
```

---

### Task 2: App Shell and Focused Lobby Screen

**Files:**
- Create: `clients/godot/scripts/ui/AppScreen.cs`
- Create: `clients/godot/scripts/ui/LobbyScreen.cs`
- Create: `clients/godot/scenes/screens/LobbyScreen.tscn`
- Create: `clients/godot/tools/check-minimal-lobby-scene.sh`
- Modify: `clients/godot/scenes/Main.tscn`
- Modify: `clients/godot/scripts/Main.cs`
- Modify: `clients/godot/STRUCTURE.md`

**Interfaces:**
- Consumes: existing `ConnectAndRequestSnapshotAsync`, matchmaking methods,
  `SubmitSelectedDeckAsync`, `ReadyAsync`, deck/public-match data, and status.
- Produces: `LobbyScreen` events `ConnectRequested`, `ReconnectRequested`,
  `CreatePublicMatchRequested`, `QueueRequested`, `CancelQueueRequested`,
  `JoinPublicMatchRequested`, `SubmitDeckRequested`, and `ReadyRequested`;
  setters for status, deck options, room options, and control availability.

- [x] **Step 1: Write a failing lobby scene check**

The check requires `LobbyScreen.tscn`, its attached script, a `PrimaryFlow`
container, `DeckSelect`, `SubmitDeckButton`, `ReadyButton`, and no
`SnapshotScroll`, `PromptScroll`, or raw log node inside the lobby scene.

- [x] **Step 2: Run the check and confirm it fails**

Run: `clients/godot/tools/check-minimal-lobby-scene.sh`

Expected: non-zero exit because `LobbyScreen.tscn` does not exist.

- [x] **Step 3: Implement the screen contract**

```csharp
public partial class LobbyScreen : Control
{
    public event Action? ConnectRequested;
    public event Action? ReconnectRequested;
    public event Action? CreatePublicMatchRequested;
    public event Action? QueueRequested;
    public event Action? CancelQueueRequested;
    public event Action? JoinPublicMatchRequested;
    public event Action? SubmitDeckRequested;
    public event Action? ReadyRequested;

    public string HandleText { get; set; } = string.Empty;
    public string RoomText { get; set; } = string.Empty;
    public int SelectedDeckIndex { get; }
    public int SelectedPublicMatchIndex { get; }

    public void SetStatus(string text, bool connected, bool waiting);
    public void SetDeckOptions(Godot.Collections.Array<Godot.Collections.Dictionary> decks, int selected);
    public void SetPublicMatches(Godot.Collections.Array<Godot.Collections.Dictionary> matches);
    public void SetSetupState(bool canSubmitDeck, bool canReady, string guidance);
}
```

The scene presents room/matchmaking entry, a deck selector, and one dominant next
button. Player key, reconnect token, and transport diagnostics stay out of the
primary flow.

- [x] **Step 4: Mount the lobby in `Main.tscn` and wire existing methods**

`Main.cs` remains the coordinator. It forwards data into `LobbyScreen` and
connects screen events to existing async methods. Do not duplicate HTTP or
SignalR logic in `LobbyScreen`.

- [x] **Step 5: Build, run checks, and open the real lobby**

Run:

```bash
~/.dotnet/dotnet build clients/godot/Riftbound.GodotClient.csproj
clients/godot/tools/check-minimal-lobby-scene.sh
ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://127.0.0.1:5088 \
  ConnectionStrings__Riftbound="" ~/.dotnet/dotnet run --project src/Riftbound.Api
```

Open Godot at 1440x900 and 1920x1080. Confirm deck selection, submit, and ready
still send existing commands and the battle view is not visible in lobby state.

Visible evidence is archived under `screenshots/units/m2-minimal-lobby-*`. The
1280x720, 1440x900, and 1920x1080 captures use a real memory-mode API room and
show the server-enabled `SUBMIT_DECK` state while `READY` remains disabled.
Paired `*-visible-run.txt` logs and `m2-minimal-lobby-visible-runs.md` record the
non-headless renderer, exact capture path, Prompt state, and hidden-info check.

- [x] **Step 6: Update docs and commit**

Commit message: `Separate the Godot lobby from the match table` with a body
explaining that setup controls no longer compete with combat state.

---

### Task 3: Responsive Match Screen and Official-Card Table

**Files:**
- Create: `clients/godot/scripts/ui/MatchScreen.cs`
- Create: `clients/godot/scripts/ui/MatchTableRenderer.cs`
- Create: `clients/godot/scenes/screens/MatchScreen.tscn`
- Create: `clients/godot/tools/check-minimal-match-scene.sh`
- Modify: `clients/godot/scenes/Main.tscn`
- Modify: `clients/godot/scripts/Main.cs`
- Modify: `clients/godot/STRUCTURE.md`

**Interfaces:**
- Consumes: the existing single `wireTable` section dictionary produced from
  the per-player snapshot and `OfficialCardView` from Task 1.
- Produces: `MatchScreen.RenderSections(Array<Dictionary> sections)`,
  `MatchScreen.SetTurnStatus(string headline, string detail, bool actionable)`,
  `MatchScreen.SetVisible(bool visible)`, and `CardActivated(Dictionary card)`.

- [x] **Step 1: Write the failing match scene check**

Require stable node paths `OpponentArea`, `Battlefields/BattlefieldOne`,
`Battlefields/BattlefieldTwo`, `SelfArea`, `HandArea`, and `ActionBarHost`.
Reject `SnapshotScroll`, a fixed 820 px table height, and a permanent 320 px
right rail.

- [x] **Step 2: Run the check and confirm it fails**

Run: `clients/godot/tools/check-minimal-match-scene.sh`

Expected: non-zero exit because the match scene is absent.

- [x] **Step 3: Implement the match scene contract**

```csharp
public partial class MatchScreen : Control
{
    public event Action<Godot.Collections.Dictionary>? CardActivated;

    public void RenderSections(
        Godot.Collections.Array<Godot.Collections.Dictionary> sections);

    public void SetTurnStatus(string headline, string detail, bool actionable);
    public void ClearPromptStates();
    public void SetObjectState(string objectId, OfficialCardVisualState state);
}
```

`MatchTableRenderer` creates `OfficialCardView` instances for visible cards and
neutral backs for hidden cards. It maps only the existing safe table dictionary:
opponent summary/hand, opponent units, two battlefields, self units, self hand,
and compact public zones. Empty zones use a small label and do not stretch.

- [x] **Step 4: Route battle snapshots into `MatchScreen`**

`Main.ApplySnapshotSections` shows `MatchScreen` for a non-ROOM table section and
shows `LobbyScreen` otherwise. Retain `CardControlRenderer` behind a temporary
fallback flag until the new screen reaches result parity.

- [x] **Step 5: Build and run the current visible simulated stack**

Run:

```bash
~/.dotnet/dotnet build clients/godot/Riftbound.GodotClient.csproj
clients/godot/tools/check-minimal-match-scene.sh
RIFTBOUND_SCREENSHOT_DIR=/tmp/riftbound-minimal-match \
  clients/godot/tools/run-local-simulated-playtest-stack.sh
```

Inspect both 1440x900 snapshots. Run the existing hidden-info check, but do not
require legacy inksteel or wire-layout guards for the new screen.

- [x] **Step 6: Capture 1920x1080 layout proof**

Repeat the visible stack with `RIFTBOUND_RESOLUTION=1920x1080`. Confirm the hand,
action host, both battlefields, and public zones remain visible without adding a
permanent side panel.

Visible 1280x720, dual-perspective 1440x900, and 1920x1080 evidence plus paired
normal-window logs is archived under `screenshots/units/m3-minimal-match-*`.

- [x] **Step 7: Update docs and commit**

Commit message: `Render the match with readable official cards` and explain that
the official artwork now carries card identity instead of custom mini frames.

---

### Task 4: Card Inspection and Centered Result Overlay

**Files:**
- Create: `clients/godot/scripts/ui/CardInspectOverlay.cs`
- Create: `clients/godot/scripts/ui/ResultOverlay.cs`
- Create: `clients/godot/scenes/overlays/CardInspectOverlay.tscn`
- Create: `clients/godot/scenes/overlays/ResultOverlay.tscn`
- Create: `clients/godot/tools/check-minimal-overlays.sh`
- Modify: `clients/godot/scenes/Main.tscn`
- Modify: `clients/godot/scripts/Main.cs`

**Interfaces:**
- Consumes: visible card dictionaries and existing match-result dictionaries.
- Produces: `CardInspectOverlay.ShowCard(Dictionary card)`, `HideCard()`,
  `ResultOverlay.ShowResult(Dictionary result)`, `HideResult()`, and
  `ReturnLobbyRequested`.

- [x] **Step 1: Write and run the failing overlay check**

Require centered full-screen overlay anchors, Escape-close behavior for inspect,
a return-lobby button for result, and no raw `serverTick`, `source`, or prompt
panel inside either scene.

- [x] **Step 2: Implement card inspection**

The overlay displays one large uncropped official image plus safe catalog text.
`ShowCard` returns without opening when `visible=false` or `faceDown=true`.

- [x] **Step 3: Implement result presentation**

The result overlay shows localized win/loss, winner, score, and reason over the
latched final table. It replaces the narrow right-rail `ResultFrame`.

- [x] **Step 4: Wire existing preview and result callbacks**

`Main.ApplyCardPreview` delegates to `CardInspectOverlay`; `ApplyMatchResult`
delegates to `ResultOverlay` while preserving the existing screenshot latch and
server-authoritative result event.

- [x] **Step 5: Build and capture inspect/result proof**

Use a visible two-window smoke that previews the first visible card and reaches
`MATCH_WON`. Inspect both final screenshots and verify hidden opponent cards
cannot open the inspect overlay.

Visible 1440x900 evidence is archived under
`screenshots/units/m4-card-inspect-*` and `screenshots/units/m4-result-*`.
Both clients rendered official-card tables before capture, and the result smoke
did not submit `SURRENDER` until the table had rendered at least once. The paired
visible-run logs record zero opponent hand faces, zero opponent standby faces,
and zero hidden identity leaks.

- [x] **Step 6: Commit**

Commit message: `Add focused card and result overlays`.

---

### Task 5: Prompt Interaction Controller and Contextual Action Bar

**Files:**
- Create: `clients/godot/scripts/interaction/PromptChoice.cs`
- Create: `clients/godot/scripts/interaction/PromptSelectionState.cs`
- Create: `clients/godot/scripts/interaction/PromptInteractionController.cs`
- Create: `clients/godot/scripts/ui/ActionBar.cs`
- Create: `clients/godot/scenes/components/ActionBar.tscn`
- Create: `clients/godot/tools/check-prompt-interaction-controller.sh`
- Modify: `clients/godot/scripts/Main.cs`
- Modify: `clients/godot/scripts/ui/MatchScreen.cs`

**Interfaces:**
- Consumes: dictionaries from `BuildPromptView`, including `promptId`,
  `snapshotTick`, `actions`, `sourceChoices`, and `selectionSteps`.
- Produces: `Load(Dictionary promptView)`, `TrySelectObject(string objectId)`,
  `ClearSelection()`, `Current`, and `SelectionChanged`; `Current` contains only
  IDs from the current server prompt.

- [ ] **Step 1: Write a failing pure-state integrity test**

The check requires prompt identity storage, selection reset on identity change,
membership checks against current prompt choices, and no references to snapshot
card placement or engine legality helpers.

- [ ] **Step 2: Implement the selection records**

```csharp
internal sealed record PromptSelectionState(
    string PromptId,
    long SnapshotTick,
    string ActionName,
    string? SourceId,
    IReadOnlyList<string> TargetIds,
    string? DestinationId,
    string? Mode,
    IReadOnlyList<string> OptionalCostIds,
    bool CanSubmit,
    string Summary);
```

- [ ] **Step 3: Implement controller invariants**

```csharp
internal sealed class PromptInteractionController
{
    public event Action<PromptSelectionState>? SelectionChanged;
    public PromptSelectionState? Current { get; private set; }

    public void Load(Godot.Collections.Dictionary promptView);
    public bool SelectAction(string actionName);
    public bool TrySelectObject(string objectId);
    public void ClearSelection();
}
```

`TrySelectObject` accepts only IDs present in the selected action's current
server choices. `Load` clears selection when prompt ID or snapshot tick changes.
It never scans the table to discover legal objects.

- [ ] **Step 4: Implement the action bar**

`ActionBar` shows turn guidance, enabled server actions, selection summary,
cancel, and one submit button. It emits `ActionSelected`, `CancelRequested`, and
`SubmitRequested(PromptSelectionState state)`. Technical prompt identity remains
internal.

- [ ] **Step 5: Connect direct table selection**

`Main.ApplyPrompt` loads the controller and applies selectable state to matching
cards/zones in `MatchScreen`. Card activation calls `TrySelectObject`; selection
changes update legal target states and the action bar.

- [ ] **Step 6: Reuse existing command submission**

Translate `PromptSelectionState` to the existing command-template submission
path. Do not add client-side action legality or new command payload semantics.

- [ ] **Step 7: Build and visibly prove first-turn actions**

Run two visible clients. Manually complete mulligan, tap a rune, play one card,
select a destination, pass priority, and end turn. Capture each state and verify
no object ID or dropdown is needed.

- [ ] **Step 8: Commit**

Commit message: `Drive Godot actions from direct prompt selection`.

---

### Task 6: Purpose-Built Mulligan, Trigger, and Damage Overlays

**Files:**
- Create: `clients/godot/scripts/ui/MulliganOverlay.cs`
- Create: `clients/godot/scripts/ui/TriggerOrderOverlay.cs`
- Create: `clients/godot/scripts/ui/DamageAssignmentOverlay.cs`
- Create: `clients/godot/scenes/overlays/MulliganOverlay.tscn`
- Create: `clients/godot/scenes/overlays/TriggerOrderOverlay.tscn`
- Create: `clients/godot/scenes/overlays/DamageAssignmentOverlay.tscn`
- Create: `clients/godot/tools/check-focused-prompt-overlays.sh`
- Modify: `clients/godot/scripts/Main.cs`
- Modify: `clients/godot/scripts/SpecialPromptCommandBuilder.cs` only if an
  adapter method is needed; command semantics stay unchanged.

**Interfaces:**
- Consumes: current prompt action dictionaries and existing server metadata.
- Produces: confirmed selections expressed in the same payload types already
  accepted by `SubmitMulliganAsync`, `SubmitPromptTemplateAsync`, and
  `SubmitPromptPayloadAsync`.

- [ ] **Step 1: Write the failing focused-overlay check**

Reject `OptionButton` in all three overlay scenes. Require official-card choices
for mulligan, reorder controls for triggers, remaining-damage controls for
damage assignment, cancel, and one confirm button.

- [ ] **Step 2: Implement mulligan with official hand cards**

Selection count obeys server `minSelectionCount` and `maxSelectionCount`.
Confirm submits only selected server source IDs.

- [ ] **Step 3: Implement trigger ordering**

Display server-provided labels, preserve every trigger ID, and allow keyboard
move-up/move-down. Confirm passes the resulting order to the existing builder.

- [ ] **Step 4: Implement damage assignment**

Display attackers, blockers, and remaining damage from server metadata. Enable
only assignments exposed by the server and preserve exact IDs internally.

- [ ] **Step 5: Keep a guarded legacy fallback**

If an action lacks the metadata required by its focused overlay, disable it,
write a diagnostic, and report the missing field. Do not silently fall back to
client-side rule inference.

- [ ] **Step 6: Build and run targeted visible scenarios**

Capture mulligan, trigger order when encountered, and damage assignment when
encountered. If preconstructed decks cannot reach a prompt, retain a safe
server-produced fixture or stop and report the missing scenario path.

- [ ] **Step 7: Commit**

Commit message: `Replace generic Godot prompt forms`.

---

### Task 7: Responsive, Keyboard, and Legacy Removal Pass

**Files:**
- Create: `clients/godot/tools/check-minimal-responsive-scene.sh`
- Modify: `clients/godot/project.godot`
- Modify: `clients/godot/scenes/Main.tscn`
- Modify: all new UI scenes/scripts from Tasks 1-6
- Modify: `clients/godot/scripts/Main.cs`
- Modify or remove: `clients/godot/scripts/CardControlRenderer.cs`
- Modify or remove: `clients/godot/scripts/RunestoneBackdrop.cs`
- Modify or remove: `clients/godot/scripts/RunestoneTheme.cs`
- Modify: legacy inksteel/wire layout checks so they no longer gate the new UI
- Modify: `clients/godot/STRUCTURE.md`, `MEMORY.md`, and `assets/ASSETS.md`

**Interfaces:**
- Consumes: all new screen/component contracts.
- Produces: one active minimal runtime path with no old wire-table or right-rail
  rendering dependency.

- [ ] **Step 1: Write the responsive/legacy failing check**

Require 1280x720, 1440x900, and 1920x1080 layout assertions; reject runtime
references to the old `WireTableNode`, fixed 820 px table height, old
`PromptScroll`, and right-rail result ownership.

- [ ] **Step 2: Add keyboard focus and shortcuts**

Define `ui_inspect_card`, `ui_cancel_selection`, `ui_confirm_action`,
`ui_action_previous`, and `ui_action_next` input actions.
Set deterministic focus neighbors across lobby, hand, table, action bar, and
overlays. Escape cancels local selection or closes the top overlay; Enter
submits only an enabled focused action.

- [ ] **Step 3: Capture all supported viewports**

Run the visible stack at 1280x720, 1440x900, and 1920x1080. Inspect text,
official card aspect ratio, hand overlap, both battlefields, action bar, and
overlays. Fix any clipped or unreachable control.

- [ ] **Step 4: Remove the legacy runtime path**

After screenshot and protocol parity, delete or disconnect wire-table rendering,
the permanent right prompt rail, obsolete preview/result frames, and unused
inksteel theme code. Keep evidence tooling only where it remains valid; replace
legacy palette/layout guards with checks for readable cards, visible action bar,
and result overlay presence.

- [ ] **Step 5: Run adjacent checks**

Run:

```bash
~/.dotnet/dotnet build clients/godot/Riftbound.GodotClient.csproj
find clients/godot/tools -name '*.sh' -print0 | xargs -0 -n1 bash -n
clients/godot/tools/check-human-playtest-script-safety.sh
clients/godot/tools/check-human-playtest-evidence-integrity.sh
clients/godot/tools/check-human-playtest-package-integrity.sh
git diff --check -- clients/godot
```

- [ ] **Step 6: Commit**

Commit message: `Remove the obsolete Godot wire-table path`.

---

### Task 8: Full Match and Final Evidence

**Files:**
- Modify: `clients/godot/tools/check-human-playtest-evidence.sh` (replace
  inksteel/wire-table conclusions with official-card, contextual-action-bar,
  centered-result, and hidden-information conclusions)
- Modify: `clients/godot/tools/package-human-playtest-evidence.sh` (package the
  minimal-client conclusions and the new final media)
- Modify: `clients/godot/tools/verify-human-playtest-package.sh` (verify the
  minimal-client conclusions and packaged screenshots/video)
- Modify: `clients/godot/README.md`, `PLAN.md`, `STRUCTURE.md`, `MEMORY.md`,
  and `assets/ASSETS.md`
- Create: `clients/godot/screenshots/result/<next>/video.mp4`
- Create: `clients/godot/screenshots/result/<next>/frame*.png`

**Interfaces:**
- Consumes: clean pushed `origin/main`, two distinct human identities, two Godot
  windows, preconstructed decks, and the existing evidence package contract.
- Produces: a verified package proving two-human completion, hidden-information
  safety, visible result state, supported viewports, and no technical UI
  dependency.

- [ ] **Step 1: Update evidence expectations for the minimal UI**

Replace obsolete inksteel/wire-layout conclusions with machine checks that the
official-card table, contextual action bar, and centered result overlay appear.
Keep manual screenshot confirmation and hidden-information checks mandatory.

- [ ] **Step 2: Run a clean-main visible automated regression**

Use `run-clean-main-simulated-playtest-stack.sh` to catch protocol, screenshot,
result, and hidden-info regressions. Label the output non-final.

- [ ] **Step 3: Run the final two-human session**

Both humans must choose preconstructed decks and complete setup, mulligan,
resource actions, card play, movement, battle, damage/trigger prompts when
encountered, turn changes, and result using only the Godot UI.

- [ ] **Step 4: Capture final media**

Store 15-30 seconds of distinct frames showing setup, direct selection, an
accepted action, and result. Encode the same frame sequence at 30 fps into
`video.mp4`; do not use a static loop.

- [ ] **Step 5: Verify every completion requirement**

Confirm zero build errors, no actionable Godot errors, stable supported
viewports, zero hidden identity leaks, readable official cards, no technical IDs
in primary UI, final result visible to both humans, asset sources documented,
and the package verifier passing.

- [ ] **Step 6: Commit and push final evidence/docs**

Commit message: `Record the playable Godot full-match proof`. Fetch and rebase
`origin/main`, push `main`, then perform the completion audit before updating the
thread goal.

## Execution Order

Execute Tasks 1-8 in order. Tasks 1-4 are the first visual vertical slice;
Tasks 5-6 provide direct rules-facing playability; Task 7 removes the obsolete
runtime only after parity; Task 8 is the non-negotiable human acceptance gate.

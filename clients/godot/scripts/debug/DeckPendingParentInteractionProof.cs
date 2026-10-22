using System.Reflection;
using System.Text.Json;
using Godot;
using Riftbound.GodotClient.Interaction;
using Riftbound.GodotClient.Ui;

namespace Riftbound.GodotClient.Debug;

public partial class DeckPendingParentInteractionProof : Control
{
    public override void _Ready()
    {
        try
        {
            var root = OS.GetCmdlineUserArgs().Single(arg => arg.StartsWith("--evidence="))[11..];
            using var index = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "index.json")));
            var map = typeof(Main).GetMethod("BuildPromptView", BindingFlags.Static | BindingFlags.NonPublic)!;
            var build = typeof(Main).GetMethod("CommandFromTemplate", BindingFlags.Static | BindingFlags.NonPublic)!;
            var selectionType = typeof(Main).GetNestedType("PromptSelection", BindingFlags.NonPublic)!;
            var count = 0;
            foreach (var row in index.RootElement.EnumerateArray())
            {
                using var document = JsonDocument.Parse(File.ReadAllText(row.GetProperty("promptPath").GetString()!));
                using var originalDocument = JsonDocument.Parse(File.ReadAllText(row.GetProperty("originalCommandPath").GetString()!));
                var prompt = document.RootElement; var original = originalDocument.RootElement;
                var selectedIds = original.GetProperty("chosenObjectIds").Deserialize<string[]>()!;
                var candidate = prompt.GetProperty("candidates").EnumerateArray().Single(c => c.GetProperty("action").GetString() == "CHOOSE_CARDS");
                var controller = new PromptInteractionController();
                var bar = GD.Load<PackedScene>("res://scenes/components/ActionBar.tscn").Instantiate<ActionBar>(); AddChild(bar);
                Dictionary<string, object?>? command = null;
                bar.ChoiceSelected += (role, id) => Check(controller.TrySelectChoice(role, id), "The actual choice button must select a legal current-view card.");
                controller.SelectionChanged += state => bar.ShowSelection(state, controller.CurrentChoices, controller.CurrentStepLabel, controller.CurrentStepRequired);
                controller.SelectionCleared += bar.ClearSelectionDisplay;
                bar.SubmitRequested += state => {
                    var selected = Activator.CreateInstance(selectionType, [state.SourceId, state.TargetIds.ToArray(), state.DestinationId, state.Mode, state.OptionalCostIds.ToArray()]);
                    command = (Dictionary<string, object?>)build.Invoke(null, [candidate, selected, controller.PromptId, controller.SnapshotTick])!;
                };
                controller.Load((Godot.Collections.Dictionary)map.Invoke(null, [prompt])!);
                Check(controller.Current is { ActionName: "CHOOSE_CARDS" }, "The actual deck prompt must open its production choice controls.");
                foreach (var id in row.GetProperty("priorPendingIds").Deserialize<string[]>()!)
                    Check(!controller.TrySelectObject(id), "An earlier pending card is not a selectable card in the current deck view.");
                Check(!controller.TrySelectObject(row.GetProperty("unobservedId").GetString()!), "A card below the viewed interval is not selectable.");
                var confirm = bar.GetNode<Button>("%SubmitButton");
                if (row.GetProperty("mandatory").GetBoolean() && controller.Current is { CanSubmit: false })
                {
                    Check(confirm.Disabled, "A mandatory choice starts incomplete.");
                    confirm.EmitSignal(BaseButton.SignalName.Pressed); Check(command is null, "A forced incomplete submit cannot emit a command.");
                }
                else if (row.GetProperty("mandatory").GetBoolean())
                    Check(controller.Current!.TargetIds.SequenceEqual(selectedIds) && selectedIds.Length > 0,
                        "A sole forced mandatory choice may be automatically selected, and must match the original legal selection.");
                foreach (var id in selectedIds)
                {
                    if (controller.Current!.TargetIds.Contains(id)) continue;
                    var position = controller.CurrentChoices.ToList().FindIndex(choice => choice.Id == id);
                    Check(position >= 0, "The original selected card must have a current production choice button.");
                    bar.GetNode<HBoxContainer>("%StepChoices").GetChild<Button>(position).EmitSignal(BaseButton.SignalName.Pressed);
                }
                Check(!confirm.Disabled && controller.Current is { CanSubmit: true }, "Only the actual legal selection or optional decline enables submission.");
                bar.SetPending(true); confirm.EmitSignal(BaseButton.SignalName.Pressed); Check(command is null, "Network-pending submit must stay disabled.");
                bar.SetPending(false); confirm.EmitSignal(BaseButton.SignalName.Pressed);
                Check(command is not null && command["cmdType"] as string == "CHOOSE_CARDS", "The production confirm button emits its real command template.");
                using var emitted = JsonDocument.Parse(JsonSerializer.Serialize(command)); var actual = emitted.RootElement;
                foreach (var field in new[] { "cmdType", "choiceId", "choiceWindow", "promptId" })
                    Check(actual.GetProperty(field).GetString() == original.GetProperty(field).GetString(), "Keep the original server command/choice/prompt identity.");
                Check(actual.GetProperty("snapshotTick").GetInt64() == original.GetProperty("snapshotTick").GetInt64()
                    && actual.GetProperty("chosenObjectIds").Deserialize<string[]>()!.SequenceEqual(selectedIds), "Keep the exact tick and selected object sequence.");
                File.WriteAllText(row.GetProperty("outputCommandPath").GetString()!, JsonSerializer.Serialize(command));
                command = null;
                controller.Load(new Godot.Collections.Dictionary { ["promptId"] = "next", ["snapshotTick"] = controller.SnapshotTick + 1,
                    ["actions"] = new Godot.Collections.Array<Godot.Collections.Dictionary>() });
                confirm.EmitSignal(BaseButton.SignalName.Pressed); Check(command is null && controller.Current is null, "A new server window invalidates the stale submit.");
                bar.Free(); count++;
            }
            GD.Print($"DECK_PENDING_PARENT_INTERACTION_PASS: {count} actual production choice and confirm buttons; prior-pending, unobserved, mandatory, pending and stale guards");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
    private static void Check(bool valid, string message) { if (!valid) throw new InvalidOperationException(message); }
}

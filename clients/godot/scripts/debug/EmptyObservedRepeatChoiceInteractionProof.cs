using System.Reflection;
using System.Text.Json;
using Godot;
using Riftbound.GodotClient.Interaction;
using Riftbound.GodotClient.Ui;

namespace Riftbound.GodotClient.Debug;

public partial class EmptyObservedRepeatChoiceInteractionProof : Control
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
                var name = row.GetProperty("name").GetString()!;
                var ids = row.GetProperty("secondIds").Deserialize<string[]>()!;
                using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, name + "-prompt.json")));
                var prompt = document.RootElement;
                var candidate = prompt.GetProperty("candidates").EnumerateArray().Single(value => value.GetProperty("action").GetString() == "CHOOSE_CARDS");
                var controller = new PromptInteractionController();
                var bar = GD.Load<PackedScene>("res://scenes/components/ActionBar.tscn").Instantiate<ActionBar>();
                AddChild(bar);
                Dictionary<string, object?>? command = null;
                bar.ChoiceSelected += (role, id) => Check(controller.TrySelectChoice(role, id), "Native choice button must select an actual second-view card.");
                controller.SelectionChanged += state => bar.ShowSelection(state, controller.CurrentChoices, controller.CurrentStepLabel, controller.CurrentStepRequired);
                controller.SelectionCleared += bar.ClearSelectionDisplay;
                bar.SubmitRequested += state => {
                    var selected = Activator.CreateInstance(selectionType, [state.SourceId, state.TargetIds.ToArray(), state.DestinationId, state.Mode, state.OptionalCostIds.ToArray()]);
                    command = (Dictionary<string, object?>)build.Invoke(null, [candidate, selected, controller.PromptId, controller.SnapshotTick])!;
                };
                controller.Load((Godot.Collections.Dictionary)map.Invoke(null, [prompt])!);
                Check(controller.Current is { ActionName: "CHOOSE_CARDS" }, "Actual second-view prompt must open the native choice.");
                Check(!controller.TrySelectObject(row.GetProperty("firstId").GetString()!), "A prior execution's observed unit cannot be selected.");
                Check(!controller.TrySelectObject(row.GetProperty("unobservedId").GetString()!), "An unobserved tail card cannot be selected.");
                var confirm = bar.GetNode<Button>("%SubmitButton");
                if (row.GetProperty("mandatory").GetBoolean())
                {
                    Check(confirm.Disabled, "Mandatory second view requires a choice.");
                    confirm.EmitSignal(BaseButton.SignalName.Pressed);
                    Check(command is null, "A forced signal cannot submit an incomplete mandatory selection.");
                }
                if (row.GetProperty("draw").GetBoolean())
                {
                    var selectedIndex = controller.CurrentChoices.ToList().FindIndex(choice => choice.Id == ids[1]);
                    Check(selectedIndex >= 0, "The second viewed card must have a production choice button.");
                    bar.GetNode<HBoxContainer>("%StepChoices").GetChild<Button>(selectedIndex).EmitSignal(BaseButton.SignalName.Pressed);
                }
                Check(!confirm.Disabled && controller.Current is { CanSubmit: true }, "Valid draw or optional decline enables confirmation.");
                bar.SetPending(true); confirm.EmitSignal(BaseButton.SignalName.Pressed);
                Check(command is null, "A pending network operation cannot submit twice.");
                bar.SetPending(false); confirm.EmitSignal(BaseButton.SignalName.Pressed);
                Check(command is not null && command["cmdType"] as string == "CHOOSE_CARDS", "Production confirm button must emit the choice template.");
                var payload = command!;
                Check(((IEnumerable<string>)payload["chosenObjectIds"]!).SequenceEqual(row.GetProperty("draw").GetBoolean() ? [ids[1]] : []), "Keep the actual selected second card or optional empty choice.");
                Check(payload["promptId"] as string == prompt.GetProperty("promptId").GetString(), "Keep the exact server prompt identity.");
                File.WriteAllText(Path.Combine(root, name + "-command.json"), JsonSerializer.Serialize(command));
                command = null;
                controller.Load(new Godot.Collections.Dictionary { ["promptId"] = "next", ["snapshotTick"] = controller.SnapshotTick + 1,
                    ["actions"] = new Godot.Collections.Array<Godot.Collections.Dictionary>() });
                confirm.EmitSignal(BaseButton.SignalName.Pressed);
                Check(command is null && controller.Current is null, "A new server window clears stale choice submission.");
                bar.Free(); count++;
            }
            GD.Print($"EMPTY_OBSERVED_REPEAT_CHOICE_PASS: {count} production choice/confirm buttons; mandatory, optional, prior-card, tail, pending and stale-window guards");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}

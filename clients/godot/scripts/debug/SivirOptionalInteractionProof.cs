using System.Reflection;
using System.Text.Json;
using Godot;
using Riftbound.GodotClient.Interaction;
using Riftbound.GodotClient.Ui;

namespace Riftbound.GodotClient.Debug;

public partial class SivirOptionalInteractionProof : Control
{
    public override void _Ready()
    {
        try {
            var root = OS.GetCmdlineUserArgs().Single(a => a.StartsWith("--evidence="))[11..]; var count = 0;
            foreach (var branch in new[] { "primary-pay", "primary-decline", "alternate-pay", "alternate-decline" }) {
                var dir = Path.Combine(root, branch);
                foreach (var step in JsonSerializer.Deserialize<string[]>(File.ReadAllText(Path.Combine(dir, "steps.json")))!) {
                    Produce(dir, step); count++;
                }
            }
            GD.Print($"SIVIR_OPTIONAL_PASS: {count} production rune, optional cost and response commands"); GetTree().Quit();
        } catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
    private void Produce(string dir, string step)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, step + "-prompt.json")));
        using var selected = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, step + "-selection.json")));
        var prompt = doc.RootElement; var fallback = selected.RootElement; var action = fallback.GetProperty("cmdType").GetString()!;
        var candidate = prompt.GetProperty("candidates").EnumerateArray().Single(c => c.GetProperty("action").GetString() == action);
        var view = (Godot.Collections.Dictionary)typeof(Main).GetMethod("BuildPromptView", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [prompt])!;
        var controller = new PromptInteractionController(); controller.Load(view);
        var bar = GD.Load<PackedScene>("res://scenes/components/ActionBar.tscn").Instantiate<ActionBar>(); AddChild(bar);
        void Refresh() => bar.ShowSelection(controller.Current!, controller.CurrentChoices, controller.CurrentStepLabel, controller.CurrentStepRequired);
        bar.ActionSelected += id => { Check(controller.SelectAction(id), "Server action is selectable"); Refresh(); };
        bar.ChoiceSelected += (role, id) => { Check(controller.TrySelectChoice(role, id), "Server choice is selectable"); Refresh(); };
        bar.ShowPrompt(view["message"].AsString(), controller.Actions);
        if (action == "PASS_PRIORITY") {
            var pass = (Button)typeof(ActionBar).GetField("_primaryAction", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(bar)!;
            Check(!pass.Disabled, "Response is available"); pass.EmitSignal(BaseButton.SignalName.Pressed);
        } else {
            var label = controller.Actions.Single(a => a.Name == action).Label;
            bar.GetNode<HBoxContainer>("%ActionChoices").GetChildren().OfType<Button>().Single(b => b.Text == label).EmitSignal(BaseButton.SignalName.Pressed);
            var picks = action == "RECYCLE_RUNE" ? new[] { fallback.GetProperty("sourceObjectId").GetString()! }
                : fallback.GetProperty("chosenObjectIds").EnumerateArray().Select(v => v.GetString()!).ToArray();
            foreach (var pick in picks) {
                if (controller.Current!.SourceId == pick || controller.Current.TargetIds.Contains(pick)) continue;
                var choices = controller.CurrentChoices.ToArray(); var index = Array.FindIndex(choices, c => c.Id == pick);
                Check(index >= 0, "Requested object is offered by the server");
                bar.GetNode<HBoxContainer>("%StepChoices").GetChildren().OfType<Button>().ElementAt(index).EmitSignal(BaseButton.SignalName.Pressed);
            }
        }
        Refresh(); Dictionary<string, object?>? command = null;
        bar.SubmitRequested += selection => {
            var type = typeof(Main).GetNestedType("PromptSelection", BindingFlags.NonPublic)!;
            var value = Activator.CreateInstance(type, [selection.SourceId, selection.TargetIds.ToArray(), selection.DestinationId, selection.Mode, selection.OptionalCostIds.ToArray()]);
            command = (Dictionary<string, object?>)typeof(Main).GetMethod("CommandFromTemplate", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [candidate, value, controller.PromptId, controller.SnapshotTick])!;
        };
        var submit = bar.GetNode<Button>("%SubmitButton"); Check(!submit.Disabled, "Valid optional selection can be confirmed"); submit.EmitSignal(BaseButton.SignalName.Pressed);
        Check(command is not null, "Production control emitted a command");
        File.WriteAllText(Path.Combine(dir, step + "-command.json"), JsonSerializer.Serialize(command)); bar.Free();
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}

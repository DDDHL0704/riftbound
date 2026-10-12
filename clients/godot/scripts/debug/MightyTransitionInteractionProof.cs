using System.Reflection;
using System.Text.Json;
using Godot;
using Riftbound.GodotClient.Interaction;
using Riftbound.GodotClient.Ui;

namespace Riftbound.GodotClient.Debug;

public partial class MightyTransitionInteractionProof : Control
{
    public override void _Ready()
    {
        try {
            var root = OS.GetCmdlineUserArgs().Single(a => a.StartsWith("--evidence="))[11..]; var count = 0;
            foreach (var branch in new[] { "unit-pay", "unit-decline", "legend-pay", "legend-decline" }) {
                var dir = Path.Combine(root, branch);
                var steps = JsonSerializer.Deserialize<string[]>(File.ReadAllText(Path.Combine(dir, "steps.json")))!;
                foreach (var step in steps) { Produce(dir, step, branch); count++; }
            }
            GD.Print($"MIGHTY_TRANSITION_PASS: {count} production assemble, optional cost and response commands"); GetTree().Quit();
        } catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }

    private void Produce(string dir, string step, string branch)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, step + "-prompt.json")));
        var prompt = document.RootElement; var legend = branch.StartsWith("legend"); var pay = branch.EndsWith("-pay");
        var action = step == "assemble" ? "ASSEMBLE_EQUIPMENT" : step == "cost" ? legend ? "CHOOSE_CARDS" : "PAY_COST" : "PASS_PRIORITY";
        var view = (Godot.Collections.Dictionary)typeof(Main).GetMethod("BuildPromptView", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [prompt])!;
        var controller = new PromptInteractionController(); controller.Load(view);
        var bar = GD.Load<PackedScene>("res://scenes/components/ActionBar.tscn").Instantiate<ActionBar>(); AddChild(bar);
        bar.ActionSelected += id => controller.SelectAction(id); bar.ShowPrompt(view["message"].AsString(), controller.Actions);
        if (action == "PASS_PRIORITY") {
            var primary = (Button)typeof(ActionBar).GetField("_primaryAction", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(bar)!;
            Check(!primary.Disabled, "Production response pass enabled"); primary.EmitSignal(BaseButton.SignalName.Pressed);
        } else {
            var label = controller.Actions.Single(a => a.Name == action).Label;
            bar.GetNode<HBoxContainer>("%ActionChoices").GetChildren().OfType<Button>().Single(b => b.Text == label).EmitSignal(BaseButton.SignalName.Pressed);
            if (step == "assemble") {
                if (controller.Current!.SourceId != "GEAR") Check(controller.TrySelectChoice("source", "GEAR"), "Server offers source equipment");
                if (!controller.Current!.TargetIds.Contains("HOST")) Check(controller.TrySelectChoice("target", "HOST"), "Server offers the attachment host");
            } else if (legend) {
                if (pay) Check(controller.TrySelectChoice("target", "LEGEND"), "Server offers optional source exhaustion");
                else Check(controller.Current!.TargetIds.Count == 0, "Decline retains the empty optional selection");
            } else Check(controller.TrySelectChoice("target", pay ? "SPEND_POWER:yellow:1" : "DECLINE"), "Server offers the typed cost or decline");
        }
        bar.ShowSelection(controller.Current!, controller.CurrentChoices, controller.CurrentStepLabel, controller.CurrentStepRequired);
        var candidate = prompt.GetProperty("candidates").EnumerateArray().Single(c => c.GetProperty("action").GetString() == action);
        Dictionary<string, object?>? command = null;
        bar.SubmitRequested += selected => {
            var type = typeof(Main).GetNestedType("PromptSelection", BindingFlags.NonPublic)!;
            var selection = Activator.CreateInstance(type, [selected.SourceId, selected.TargetIds.ToArray(), selected.DestinationId, selected.Mode, selected.OptionalCostIds.ToArray()]);
            command = (Dictionary<string, object?>)typeof(Main).GetMethod("CommandFromTemplate", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [candidate, selection, controller.PromptId, controller.SnapshotTick])!;
        };
        var submit = bar.GetNode<Button>("%SubmitButton"); Check(!submit.Disabled, "Production confirmation enabled"); submit.EmitSignal(BaseButton.SignalName.Pressed);
        Check(command is not null, "Production command emitted");
        File.WriteAllText(Path.Combine(dir, step + "-command.json"), JsonSerializer.Serialize(command)); bar.Free();
    }

    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}

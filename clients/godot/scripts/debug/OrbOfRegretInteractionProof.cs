using System.Reflection;
using System.Text.Json;
using Godot;
using Riftbound.GodotClient.Interaction;
using Riftbound.GodotClient.Ui;

namespace Riftbound.GodotClient.Debug;

public partial class OrbOfRegretInteractionProof : Control
{
    public override void _Ready()
    {
        try {
            var root = OS.GetCmdlineUserArgs().Single(a => a.StartsWith("--evidence="))[11..]; var count = 0;
            foreach (var branch in new[] { "aura-floor", "deflect-recycle" }) {
                var dir = Path.Combine(root, branch);
                using var selectionDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, "selection.json")));
                var expected = selectionDocument.RootElement;
                foreach (var name in new[] { "activate", "pass-1", "pass-2" }) {
                    using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, name + "-prompt.json")));
                    var prompt = document.RootElement;
                    var view = (Godot.Collections.Dictionary)typeof(Main).GetMethod("BuildPromptView", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [prompt])!;
                    var controller = new PromptInteractionController(); controller.Load(view);
                    var bar = GD.Load<PackedScene>("res://scenes/components/ActionBar.tscn").Instantiate<ActionBar>(); AddChild(bar);
                    bar.ActionSelected += id => controller.SelectAction(id); bar.ShowPrompt(view["message"].AsString(), controller.Actions);
                    var action = name == "activate" ? "ACTIVATE_ABILITY" : "PASS_PRIORITY";
                    if (name == "activate") {
                        var label = controller.Actions.Single(a => a.Name == action).Label;
                        bar.GetNode<HBoxContainer>("%ActionChoices").GetChildren().OfType<Button>().Single(b => b.Text == label).EmitSignal(BaseButton.SignalName.Pressed);
                        if (controller.Current!.SourceId != "ORB") Check(controller.TrySelectChoice("source", "ORB"), "Exact equipment source selected");
                        Check(!controller.TrySelectChoice("target", "HIDDEN-OPPONENT-CARD"), "Unrevealed identity is not offered");
                        Check(!controller.TrySelectChoice("target", "ORB"), "Equipment cannot replace the unit target");
                        if (!controller.Current!.TargetIds.Contains("D")) Check(controller.TrySelectChoice("target", "D"), "Public unit selected");
                        foreach (var cost in expected.GetProperty("optionalCosts").EnumerateArray().Select(v => v.GetString()!))
                            Check(controller.TrySelectChoice("optionalCost", cost), "Server-offered rune payment selected");
                        Check(controller.Current!.CanSubmit, "Payment and target make the ability submittable");
                        controller.Load(view);
                        Check(controller.Current!.CanSubmit && controller.Current.TargetIds.SequenceEqual(new[] { "D" }), "Reload retains target and payment");
                    } else {
                        var primary = (Button)typeof(ActionBar).GetField("_primaryAction", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(bar)!;
                        Check(!primary.Disabled && primary.Text == "不响应 · 让过", "Production response pass is enabled");
                        primary.EmitSignal(BaseButton.SignalName.Pressed);
                    }
                    bar.ShowSelection(controller.Current!, controller.CurrentChoices, controller.CurrentStepLabel, controller.CurrentStepRequired);
                    var candidate = prompt.GetProperty("candidates").EnumerateArray().Single(c => c.GetProperty("action").GetString() == action);
                    Dictionary<string, object?>? command = null;
                    bar.SubmitRequested += selected => {
                        var type = typeof(Main).GetNestedType("PromptSelection", BindingFlags.NonPublic)!;
                        var selection = Activator.CreateInstance(type, [selected.SourceId, selected.TargetIds.ToArray(), selected.DestinationId, selected.Mode, selected.OptionalCostIds.ToArray()]);
                        command = (Dictionary<string, object?>)typeof(Main).GetMethod("CommandFromTemplate", BindingFlags.Static | BindingFlags.NonPublic)!
                            .Invoke(null, [candidate, selection, controller.PromptId, controller.SnapshotTick])!;
                    };
                    var submit = bar.GetNode<Button>("%SubmitButton"); Check(!submit.Disabled, "Production submit is enabled"); submit.EmitSignal(BaseButton.SignalName.Pressed);
                    Check(command is not null, "Production command emitted");
                    if (name == "activate") {
                        Check((string)command!["abilityId"]! == expected.GetProperty("abilityId").GetString(), "Exact ability identity survives mapping");
                        Check(((IEnumerable<string>)command["targetObjectIds"]!).SequenceEqual(new[] { "D" }), "Only the selected unit is sent");
                        var costs = command.TryGetValue("optionalCosts", out var value) ? (IEnumerable<string>)value! : [];
                        Check(costs.SequenceEqual(expected.GetProperty("optionalCosts").EnumerateArray().Select(v => v.GetString()!)), "Selected payment survives mapping");
                    }
                    File.WriteAllText(Path.Combine(dir, name + "-command.json"), JsonSerializer.Serialize(command)); bar.Free(); count++;
                }
            }
            GD.Print($"ORB_OF_REGRET_PASS: {count} production ability and response commands"); GetTree().Quit();
        } catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }

    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}

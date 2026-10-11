using System.Reflection;
using System.Text.Json;
using Godot;
using Riftbound.GodotClient.Interaction;
using Riftbound.GodotClient.Ui;

namespace Riftbound.GodotClient.Debug;

public partial class AzirMovementInteractionProof : Control
{
    public override void _Ready()
    {
        try {
            var root = OS.GetCmdlineUserArgs().Single(a => a.StartsWith("--evidence="))[11..]; var count = 0;
            foreach (var scenario in new[] { "same-location", "move-attach", "move-decline", "opponent-focus", "copied-attach", "copied-decline" }) {
                var names = scenario == "same-location" ? new[] { "activate", "move-pass-1", "move-pass-2", "reattach-choice" }
                    : new[] { "activate", "move-pass-1", "move-pass-2", "reattach-choice", "gold-pass-1", "gold-pass-2" };
                foreach (var name in names) {
                    var dir = Path.Combine(root, scenario);
                    using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, name + "-prompt.json"))); var prompt = document.RootElement;
                    var view = (Godot.Collections.Dictionary)typeof(Main).GetMethod("BuildPromptView", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [prompt])!;
                    var controller = new PromptInteractionController(); controller.Load(view);
                    var bar = GD.Load<PackedScene>("res://scenes/components/ActionBar.tscn").Instantiate<ActionBar>(); AddChild(bar);
                    bar.ActionSelected += id => controller.SelectAction(id); bar.ShowPrompt(view["message"].AsString(), controller.Actions);
                    var action = name == "activate" ? "ACTIVATE_ABILITY" : name == "reattach-choice" ? "CHOOSE_CARDS" : "PASS_PRIORITY";
                    if (action == "PASS_PRIORITY") {
                        var primary = (Button)typeof(ActionBar).GetField("_primaryAction", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(bar)!;
                        Check(!primary.Disabled && primary.Text == "不响应 · 让过", "Production pass is enabled"); primary.EmitSignal(BaseButton.SignalName.Pressed);
                    } else if (action == "ACTIVATE_ABILITY") {
                        var label = controller.Actions.Single(a => a.Name == action).Label;
                        bar.GetNode<HBoxContainer>("%ActionChoices").GetChildren().OfType<Button>().Single(b => b.Text == label).EmitSignal(BaseButton.SignalName.Pressed);
                        if (controller.Current!.SourceId != "HOST") Check(controller.TrySelectChoice("source", "HOST"), "Azir source selection");
                        if (scenario.StartsWith("copied", StringComparison.Ordinal)) {
                            var row = prompt.GetProperty("candidates").EnumerateArray().Single(c => c.GetProperty("action").GetString() == action)
                                .GetProperty("metadata").GetProperty("sourceRequirements").EnumerateArray()
                                .Single(r => r.GetProperty("sourceObjectId").GetString() == "HOST" && r.TryGetProperty("abilityInstanceId", out _));
                            Check(controller.TrySelectChoice("mode", row.GetProperty("abilityInstanceId").GetString()!), "Explicit copied instance selected");
                        }
                        if (!controller.Current.TargetIds.SequenceEqual(new[] { "TARGET" })) Check(controller.TrySelectChoice("target", "TARGET"), "Unit target selection");
                        if (!controller.Current!.TargetIds.Contains("GEAR")) Check(controller.TrySelectChoice("target", "GEAR"), "Mandatory weapon reference selected");
                        Check(controller.Current!.CanSubmit && controller.Current.OptionalCostIds.Count == 0, "References are confirmed without deciding the later optional action");
                        controller.Load(view);
                        Check(controller.Current!.TargetIds.SequenceEqual(new[] { "TARGET", "GEAR" }), "Prompt reload retains both references");
                        Check(controller.Current.OptionalCostIds.Count == 0, "Weapon reference is not an optional cost");
                    } else {
                        var label = controller.Actions.Single(a => a.Name == action).Label;
                        Check(label == "决定是否改贴武装", "Late optional decision has a specific Chinese action label");
                        bar.GetNode<HBoxContainer>("%ActionChoices").GetChildren().OfType<Button>().Single(b => b.Text == label).EmitSignal(BaseButton.SignalName.Pressed);
                        if (!scenario.EndsWith("decline", StringComparison.Ordinal)) Check(controller.TrySelectChoice("target", "GEAR"), "Optional action uses only the previously confirmed weapon");
                        Check(controller.Current!.CanSubmit, "Optional action can be accepted or declined after movement");
                    }
                    bar.ShowSelection(controller.Current!, controller.CurrentChoices, controller.CurrentStepLabel, controller.CurrentStepRequired);
                    var candidate = prompt.GetProperty("candidates").EnumerateArray().Single(c => c.GetProperty("action").GetString() == action);
                    Dictionary<string, object?>? command = null;
                    bar.SubmitRequested += selection => {
                        var type = typeof(Main).GetNestedType("PromptSelection", BindingFlags.NonPublic)!;
                        var selected = Activator.CreateInstance(type, [selection.SourceId, selection.TargetIds.ToArray(), selection.DestinationId, selection.Mode, selection.OptionalCostIds.ToArray()]);
                        command = (Dictionary<string, object?>)typeof(Main).GetMethod("CommandFromTemplate", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [candidate, selected, controller.PromptId, controller.SnapshotTick])!;
                    };
                    var submit = bar.GetNode<Button>("%SubmitButton"); Check(!submit.Disabled, "Production command is submittable"); submit.EmitSignal(BaseButton.SignalName.Pressed);
                    Check(command is not null, "Production submit emitted");
                    if (action == "ACTIVATE_ABILITY") {
                        Check((string)command!["abilityId"]! == "AZIR_SWIFT_PAY_GREEN_SWAP_WITH_CONTROLLED_UNIT", "Exact Azir ability selected");
                        var costs = command.TryGetValue("optionalCosts", out var selectedCosts)
                            ? ((IEnumerable<string>)selectedCosts!).ToArray() : Array.Empty<string>();
                        Check(costs.Length == 0, "Activation contains no premature optional attachment decision");
                        Check(((IEnumerable<string>)command["targetObjectIds"]!).SequenceEqual(new[] { "TARGET", "GEAR" }), "Both confirmed targets are sent");
                        if (scenario.StartsWith("copied", StringComparison.Ordinal)) Check(command.ContainsKey("abilityInstanceId"), "Copied command retains its instance binding");
                    }
                    File.WriteAllText(Path.Combine(dir, name + "-command.json"), JsonSerializer.Serialize(command)); bar.Free(); count++;
                }
            }
            GD.Print($"AZIR_MOVEMENT_PASS: {count} production ability and response commands"); GetTree().Quit();
        } catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}

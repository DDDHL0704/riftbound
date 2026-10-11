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
            foreach (var scenario in new[] { "same-location", "move-attach", "move-decline", "opponent-focus" }) {
                var names = scenario == "same-location" ? new[] { "activate", "move-pass-1", "move-pass-2" }
                    : new[] { "activate", "move-pass-1", "move-pass-2", "gold-pass-1", "gold-pass-2" };
                foreach (var name in names) {
                    var dir = Path.Combine(root, scenario);
                    using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, name + "-prompt.json"))); var prompt = document.RootElement;
                    var view = (Godot.Collections.Dictionary)typeof(Main).GetMethod("BuildPromptView", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [prompt])!;
                    var controller = new PromptInteractionController(); controller.Load(view);
                    var bar = GD.Load<PackedScene>("res://scenes/components/ActionBar.tscn").Instantiate<ActionBar>(); AddChild(bar);
                    bar.ActionSelected += id => controller.SelectAction(id); bar.ShowPrompt(view["message"].AsString(), controller.Actions);
                    var action = name == "activate" ? "ACTIVATE_ABILITY" : "PASS_PRIORITY";
                    if (action == "PASS_PRIORITY") {
                        var primary = (Button)typeof(ActionBar).GetField("_primaryAction", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(bar)!;
                        Check(!primary.Disabled && primary.Text == "不响应 · 让过", "Production pass is enabled"); primary.EmitSignal(BaseButton.SignalName.Pressed);
                    } else {
                        var label = controller.Actions.Single(a => a.Name == action).Label;
                        bar.GetNode<HBoxContainer>("%ActionChoices").GetChildren().OfType<Button>().Single(b => b.Text == label).EmitSignal(BaseButton.SignalName.Pressed);
                        if (controller.Current!.SourceId != "HOST") Check(controller.TrySelectChoice("source", "HOST"), "Azir source selection");
                        if (!controller.Current.TargetIds.SequenceEqual(new[] { "TARGET" })) Check(controller.TrySelectChoice("target", "TARGET"), "Unit target selection");
                        Check(controller.Current.CanSubmit && controller.Current.OptionalCostIds.Count == 0, "Declining the printed optional attachment is legal");
                        if (scenario != "move-decline") {
                            Check(controller.TrySelectChoice("optionalCost", "AZIR_REATTACH_ARMAMENT:GEAR"), "Selected unit's weapon is available before activation");
                            Check(controller.Current!.OptionalCostIds.SequenceEqual(new[] { "AZIR_REATTACH_ARMAMENT:GEAR" }), "Chosen weapon retained");
                        }
                        controller.Load(view);
                        Check(controller.Current!.TargetIds.SequenceEqual(new[] { "TARGET" }), "Prompt reload retains unit target");
                        Check(controller.Current.OptionalCostIds.Count == (scenario == "move-decline" ? 0 : 1), "Prompt reload retains attachment decision");
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
                        Check(costs.SequenceEqual(scenario == "move-decline" ? Array.Empty<string>() : new[] { "AZIR_REATTACH_ARMAMENT:GEAR" }), "Command carries chosen weapon and no late substitution");
                    }
                    File.WriteAllText(Path.Combine(dir, name + "-command.json"), JsonSerializer.Serialize(command)); bar.Free(); count++;
                }
            }
            GD.Print($"AZIR_MOVEMENT_PASS: {count} production ability and response commands"); GetTree().Quit();
        } catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}

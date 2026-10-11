using System.Reflection;
using System.Text.Json;
using Godot;
using Riftbound.GodotClient.Interaction;
using Riftbound.GodotClient.Ui;

namespace Riftbound.GodotClient.Debug;

public partial class LegendAttachmentInteractionProof : Control
{
    public override void _Ready()
    {
        try {
            var root = OS.GetCmdlineUserArgs().Single(a => a.StartsWith("--evidence="))[11..]; var count = 0;
            foreach (var scenario in new[] { "attach", "same-host", "forge" }) {
                var names = scenario == "same-host" ? new[] { "activate", "attach-pass-1", "attach-pass-2" }
                    : new[] { "activate", "attach-pass-1", "attach-pass-2", "pay", "draw-pass-1", "draw-pass-2" };
                foreach (var name in names) {
                    var dir = Path.Combine(root, scenario);
                    using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, name + "-prompt.json"))); var prompt = document.RootElement;
                    var view = (Godot.Collections.Dictionary)typeof(Main).GetMethod("BuildPromptView", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [prompt])!;
                    var controller = new PromptInteractionController(); controller.Load(view);
                    var bar = GD.Load<PackedScene>("res://scenes/components/ActionBar.tscn").Instantiate<ActionBar>(); AddChild(bar);
                    bar.ActionSelected += id => controller.SelectAction(id); bar.ShowPrompt(view["message"].AsString(), controller.Actions);
                    var action = name == "activate" ? "LEGEND_ACT" : name == "pay" ? "PAY_COST" : "PASS_PRIORITY";
                    string? ability = null;
                    if (action == "PASS_PRIORITY") {
                        var primary = (Button)typeof(ActionBar).GetField("_primaryAction", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(bar)!;
                        Check(!primary.Disabled && primary.Text == "不响应 · 让过", "Production pass is enabled"); primary.EmitSignal(BaseButton.SignalName.Pressed);
                    } else {
                        var label = controller.Actions.Single(a => a.Name == action).Label;
                        bar.GetNode<HBoxContainer>("%ActionChoices").GetChildren().OfType<Button>().Single(b => b.Text == label).EmitSignal(BaseButton.SignalName.Pressed);
                        if (action == "LEGEND_ACT") {
                            ability = scenario == "attach" ? "LEGEND_PAY_1_EXHAUST_ATTACH_UNATTACHED_ARMAMENT"
                                : scenario == "same-host" ? "LEGEND_EXHAUST_REATTACH_ATTACHED_ARMAMENT"
                                : "LEGEND_EXHAUST_ATTACH_CONTROLLED_ARMAMENT_FROM_BATTLEFIELD";
                            if (controller.Current!.SourceId != "LEGEND") Check(controller.TrySelectChoice("source", "LEGEND"), "Legend source selection");
                            if (scenario == "forge") {
                                Check(controller.TrySelectChoice("mode", "LEGEND_PAY_1_EXHAUST_CREATE_MINION"), "Other legitimate ability on the same source");
                                Check(controller.Current!.CanSubmit && controller.Current.TargetIds.Count == 0, "Zero-target legend mode uses its own requirements");
                            }
                            if (controller.Current!.Mode != ability) Check(controller.TrySelectChoice("mode", ability), "Exact activated legend mode selection");
                            Check(!controller.Current!.CanSubmit, "Both selected-mode targets are required before submission");
                            Check(controller.TrySelectChoice("target", "HOST"), "Unit target selection");
                            Check(controller.TrySelectChoice("target", "GEAR"), "Equipment target selection");
                            Check(controller.Current!.TargetIds.SequenceEqual(new[] { "HOST", "GEAR" }), "Both ordered legend targets retained");
                            Check(controller.Current.Mode == ability, "Target selection retains the selected ability");
                            controller.Load(view);
                            Check(controller.Current!.TargetIds.SequenceEqual(new[] { "HOST", "GEAR" }) && controller.Current.Mode == ability,
                                "Reloading the same prompt retains both targets and the selected mode");
                        } else {
                            Check(controller.TrySelectChoice("target", "PAY"), "Explicit captured trigger payment");
                        }
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
                    if (ability is not null) Check((string)command!["abilityId"]! == ability, "Command uses the selected ability requirement");
                    File.WriteAllText(Path.Combine(dir, name + "-command.json"), JsonSerializer.Serialize(command)); bar.Free(); count++;
                }
            }
            GD.Print($"LEGEND_ATTACHMENT_PASS: {count} production legend, payment and response commands"); GetTree().Quit();
        } catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}

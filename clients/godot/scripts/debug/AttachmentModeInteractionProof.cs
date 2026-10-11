using System.Reflection;
using System.Text.Json;
using Godot;
using Riftbound.GodotClient.Interaction;
using Riftbound.GodotClient.Ui;

namespace Riftbound.GodotClient.Debug;

public partial class AttachmentModeInteractionProof : Control
{
    public override void _Ready()
    {
        try {
            var root = OS.GetCmdlineUserArgs().Single(a => a.StartsWith("--evidence="))[11..]; var count = 0;
            foreach (var scenario in new[] { "ready", "channel", "boon", "copied", "detach", "source-leaves" }) {
                var dir = Path.Combine(root, scenario);
                using var steps = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, "steps.json")));
                foreach (var step in steps.RootElement.EnumerateArray()) {
                    var name = step.GetProperty("name").GetString()!; var action = step.GetProperty("action").GetString()!;
                    using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, name + "-prompt.json"))); var prompt = doc.RootElement;
                    var view = (Godot.Collections.Dictionary)typeof(Main).GetMethod("BuildPromptView", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [prompt])!;
                    Dictionary<string, object?>? command = null;
                    if (action == "ORDER_TRIGGERS") {
                        var candidate = view["actions"].AsGodotArray().Select(v => v.AsGodotDictionary()).Single(a => a["action"].AsString() == action);
                        var overlay = GD.Load<PackedScene>("res://scenes/overlays/TriggerOrderOverlay.tscn").Instantiate<TriggerOrderOverlay>(); AddChild(overlay);
                        Check(overlay.ShowPrompt(candidate, out var reason), reason);
                        var before = overlay.OrderedTriggerIds.ToArray(); Check(before.Length == 2 && before.Distinct().Count() == 2, "Native and copied mode instances are distinct");
                        if (step.GetProperty("reverseTriggers").GetBoolean()) overlay.GetNode<VBoxContainer>("%TriggerRows").GetChild(0).GetNode<Button>("MoveDownButton").EmitSignal(BaseButton.SignalName.Pressed);
                        Check(overlay.OrderedTriggerIds.SequenceEqual(Enumerable.Reverse(before)), "Production ordering retains both instances");
                        overlay.Confirmed += ids => {
                            Check(SpecialPromptCommandBuilder.TryBuildOrderTriggersPayload(candidate, ids, out var payload, out _, out var error), error);
                            payload["promptId"] = prompt.GetProperty("promptId").GetString(); payload["snapshotTick"] = prompt.GetProperty("snapshotTick").GetInt64(); command = payload;
                        };
                        overlay.GetNode<Button>("%ConfirmButton").EmitSignal(BaseButton.SignalName.Pressed); overlay.Free();
                    } else {
                        var controller = new PromptInteractionController(); controller.Load(view);
                        var bar = GD.Load<PackedScene>("res://scenes/components/ActionBar.tscn").Instantiate<ActionBar>(); AddChild(bar);
                        bar.ActionSelected += id => controller.SelectAction(id); bar.ShowPrompt(view["message"].AsString(), controller.Actions);
                        if (action == "PASS_PRIORITY") {
                            var primary = (Button)typeof(ActionBar).GetField("_primaryAction", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(bar)!;
                            Check(!primary.Disabled, "Response button enabled"); primary.EmitSignal(BaseButton.SignalName.Pressed);
                        } else {
                            var label = controller.Actions.Single(a => a.Name == action).Label;
                            bar.GetNode<HBoxContainer>("%ActionChoices").GetChildren().OfType<Button>().Single(b => b.Text == label).EmitSignal(BaseButton.SignalName.Pressed);
                            if (step.GetProperty("source").ValueKind == JsonValueKind.String) {
                                var source = step.GetProperty("source").GetString()!;
                                if (controller.Current!.SourceId != source) Check(controller.TrySelectChoice("source", source), "Printed source selected");
                            }
                            foreach (var target in step.GetProperty("targets").EnumerateArray().Select(t => t.GetString()!))
                                if (!controller.Current!.TargetIds.Contains(target)) Check(controller.TrySelectChoice("target", target), "Mode or reference target selected");
                            foreach (var cost in step.GetProperty("costs").EnumerateArray().Select(t => t.GetString()!))
                                Check(controller.TrySelectChoice("optionalCost", cost), "Equip cost selected");
                            controller.Load(view); Check(controller.Current!.CanSubmit, "Reload retains confirmed selection");
                        }
                        bar.ShowSelection(controller.Current!, controller.CurrentChoices, controller.CurrentStepLabel, controller.CurrentStepRequired);
                        var candidate = prompt.GetProperty("candidates").EnumerateArray().Single(c => c.GetProperty("action").GetString() == action);
                        bar.SubmitRequested += selection => {
                            var type = typeof(Main).GetNestedType("PromptSelection", BindingFlags.NonPublic)!;
                            var selected = Activator.CreateInstance(type, [selection.SourceId, selection.TargetIds.ToArray(), selection.DestinationId, selection.Mode, selection.OptionalCostIds.ToArray()]);
                            command = (Dictionary<string, object?>)typeof(Main).GetMethod("CommandFromTemplate", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [candidate, selected, controller.PromptId, controller.SnapshotTick])!;
                        };
                        var submit = bar.GetNode<Button>("%SubmitButton"); Check(!submit.Disabled, "Production submit enabled"); submit.EmitSignal(BaseButton.SignalName.Pressed); bar.Free();
                    }
                    Check(command is not null, "Production command emitted"); File.WriteAllText(Path.Combine(dir, name + "-command.json"), JsonSerializer.Serialize(command)); count++;
                }
            }
            GD.Print($"ATTACHMENT_MODE_PASS: {count} production commands"); GetTree().Quit();
        } catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}

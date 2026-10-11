using System.Reflection;
using System.Text.Json;
using Godot;
using Riftbound.GodotClient.Interaction;
using Riftbound.GodotClient.Ui;

namespace Riftbound.GodotClient.Debug;

public partial class CopiedAttachmentObserverInteractionProof : Control
{
    public override void _Ready()
    {
        try {
            var root = OS.GetCmdlineUserArgs().Single(a => a.StartsWith("--evidence="))[11..]; var count = 0;
            foreach (var scenario in new[] { "native-first", "copy-first", "decline-copy", "detach" }) {
                var names = new List<string> { "song", "song-pass-1", "song-pass-2", "song-decline", "sword", "sword-pass-1", "sword-pass-2", "order", "confirm-1", "confirm-2" };
                if (scenario == "detach") names.AddRange(["detach", "detach-pass-1", "detach-pass-2"]);
                names.AddRange(["resolve-1-pass-1", "resolve-1-pass-2"]);
                if (scenario != "decline-copy") names.AddRange(["resolve-2-pass-1", "resolve-2-pass-2"]);
                foreach (var name in names) {
                    var dir = Path.Combine(root, scenario);
                    using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, name + "-prompt.json"))); var prompt = doc.RootElement;
                    var view = (Godot.Collections.Dictionary)typeof(Main).GetMethod("BuildPromptView", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [prompt])!;
                    Dictionary<string, object?>? command = null;
                    if (name == "order") {
                        var action = view["actions"].AsGodotArray().Select(v => v.AsGodotDictionary()).Single(a => a["action"].AsString() == "ORDER_TRIGGERS");
                        var overlay = GD.Load<PackedScene>("res://scenes/overlays/TriggerOrderOverlay.tscn").Instantiate<TriggerOrderOverlay>(); AddChild(overlay);
                        Check(overlay.ShowPrompt(action, out var reason), reason);
                        var before = overlay.OrderedTriggerIds.ToArray(); Check(before.Length == 2 && before.Distinct().Count() == 2, "Two independently identified observers");
                        if (scenario is "copy-first" or "detach") overlay.GetNode<VBoxContainer>("%TriggerRows").GetChild(0).GetNode<Button>("MoveDownButton").EmitSignal(BaseButton.SignalName.Pressed);
                        Check(overlay.OrderedTriggerIds.SequenceEqual(scenario is "copy-first" or "detach" ? Enumerable.Reverse(before) : before), "Production order buttons retain both instances");
                        overlay.Confirmed += ids => {
                            Check(SpecialPromptCommandBuilder.TryBuildOrderTriggersPayload(action, ids, out var payload, out _, out var error), error);
                            payload["promptId"] = prompt.GetProperty("promptId").GetString(); payload["snapshotTick"] = prompt.GetProperty("snapshotTick").GetInt64(); command = payload;
                        };
                        overlay.GetNode<Button>("%ConfirmButton").EmitSignal(BaseButton.SignalName.Pressed); overlay.Free();
                    } else {
                        var controller = new PromptInteractionController(); controller.Load(view);
                        var bar = GD.Load<PackedScene>("res://scenes/components/ActionBar.tscn").Instantiate<ActionBar>(); AddChild(bar);
                        bar.ActionSelected += id => controller.SelectAction(id); bar.ShowPrompt(view["message"].AsString(), controller.Actions);
                        var action = name.Contains("-pass-") ? "PASS_PRIORITY" : name is "song" or "sword" ? "ASSEMBLE_EQUIPMENT" : name == "detach" ? "PLAY_CARD" : "PAY_COST";
                        if (action == "PASS_PRIORITY") {
                            var primary = (Button)typeof(ActionBar).GetField("_primaryAction", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(bar)!;
                            Check(!primary.Disabled, "Response button enabled"); primary.EmitSignal(BaseButton.SignalName.Pressed);
                        } else {
                            var label = controller.Actions.Single(a => a.Name == action).Label;
                            bar.GetNode<HBoxContainer>("%ActionChoices").GetChildren().OfType<Button>().Single(b => b.Text == label).EmitSignal(BaseButton.SignalName.Pressed);
                            if (action == "PAY_COST") {
                                var choice = name == "song-decline" || name == "confirm-1" && scenario == "decline-copy" ? "DECLINE" : "PAY";
                                Check(controller.TrySelectChoice("target", choice), "Independent confirmation choice");
                            } else {
                                var source = name == "song" ? "GEAR" : name == "sword" ? "SWORD" : "SPELL";
                                if (controller.Current!.SourceId != source) Check(controller.TrySelectChoice("source", source), "Weapon or spell source selection");
                                foreach (var target in name == "detach" ? new[] { "HOST", "GEAR" } : new[] { "HOST" })
                                    if (!controller.Current!.TargetIds.Contains(target)) Check(controller.TrySelectChoice("target", target), "Ordered unit/weapon targets");
                                if (action == "ASSEMBLE_EQUIPMENT") Check(controller.TrySelectChoice("optionalCost", name == "song" ? "ASSEMBLE_GREEN" : "ASSEMBLE_RED"), "Printed Equip payment");
                            }
                            controller.Load(view); Check(controller.Current!.CanSubmit, "Reload retains selection");
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
            GD.Print($"COPIED_ATTACHMENT_OBSERVER_PASS: {count} production commands"); GetTree().Quit();
        } catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}

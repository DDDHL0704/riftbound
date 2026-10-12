using System.Reflection;
using System.Text.Json;
using Godot;
using Riftbound.GodotClient.Interaction;
using Riftbound.GodotClient.Ui;

namespace Riftbound.GodotClient.Debug;

public partial class AttackDamageInteractionProof : Control
{
    public override void _Ready()
    {
        try {
            var root = OS.GetCmdlineUserArgs().Single(a => a.StartsWith("--evidence="))[11..]; var count = 0;
            foreach (var scenario in new[] { "printed", "alternate", "copied" }) {
                var dir = Path.Combine(root, scenario);
                using var steps = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, "steps.json")));
                foreach (var step in steps.RootElement.EnumerateArray()) {
                    var name = step.GetProperty("name").GetString()!; var expected = step.GetProperty("command");
                    var actionName = expected.GetProperty("cmdType").GetString()!;
                    using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, name + "-prompt.json"))); var prompt = document.RootElement;
                    var view = (Godot.Collections.Dictionary)typeof(Main).GetMethod("BuildPromptView", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [prompt])!;
                    var controller = new PromptInteractionController(); controller.Load(view);
                    if (actionName is "ORDER_TRIGGERS" or "ASSIGN_EFFECT_DAMAGE")
                        Check(controller.Actions.Single(a => a.Name == actionName).IsSpecial, "Production overlay owns this action");
                    else Check(controller.SelectAction(actionName), "Production action is selectable");
                    var action = view["actions"].As<Godot.Collections.Array<Godot.Collections.Dictionary>>().Single(a => a["action"].AsString() == actionName);
                    Check(action["enabled"].AsBool(), "Server action is enabled");
                    Dictionary<string, object?>? command = null;
                    if (actionName == "ASSIGN_EFFECT_DAMAGE") {
                        var overlay = GD.Load<PackedScene>("res://scenes/overlays/DamageAssignmentOverlay.tscn").Instantiate<DamageAssignmentOverlay>(); AddChild(overlay);
                        Check(overlay.ShowPrompt(action, out var reason), reason);
                        Check(overlay.GetNode<Label>("%DamageTitle").Text == "分配技能伤害", "Ability allocation uses its own title");
                        Check(overlay.GetNode<Button>("%ConfirmButton").Disabled, "Empty allocation is disabled");
                        Check(SpecialPromptCommandBuilder.TryReadDamageAssignmentPrompt(action, out var sources, out reason), reason);
                        Check(sources.Sum(s => s.DamagePool) == 5, "Printed five damage is independent of nine Might");
                        var steppers = (Dictionary<string, SpinBox>)typeof(DamageAssignmentOverlay).GetField("_steppers", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(overlay)!;
                        foreach (var value in expected.GetProperty("assignments").EnumerateArray())
                            steppers[value.GetProperty("targetObjectId").GetString()!].Value = value.GetProperty("damage").GetInt32();
                        overlay.ShowServerRejection("每个留存目标至少分配 1 点伤害");
                        Check(overlay.GetNode<Label>("%DamageFeedback").Text.Contains("至少"), "Server feedback remains visible");
                        overlay.Confirmed += selected => {
                            Check(SpecialPromptCommandBuilder.TryBuildDamageAssignmentPayload(action, selected, out var payload, out _, out var failure), failure);
                            command = payload;
                        };
                        Check(overlay.ConfirmCurrent(), "Valid positive distribution is submittable"); overlay.Free();
                    } else if (actionName == "ORDER_TRIGGERS") {
                        var overlay = GD.Load<PackedScene>("res://scenes/overlays/TriggerOrderOverlay.tscn").Instantiate<TriggerOrderOverlay>(); AddChild(overlay);
                        Check(overlay.ShowPrompt(action, out var reason), reason);
                        var desired = expected.GetProperty("orderedTriggerIds").EnumerateArray().Select(v => v.GetString()!).ToArray();
                        if (!overlay.OrderedTriggerIds.SequenceEqual(desired))
                            overlay.GetNode<VBoxContainer>("%TriggerRows").GetChild(0).GetNode<Button>("MoveDownButton").EmitSignal(BaseButton.SignalName.Pressed);
                        Check(overlay.OrderedTriggerIds.SequenceEqual(desired), "User order preserves both printed instances");
                        overlay.Confirmed += selected => {
                            Check(SpecialPromptCommandBuilder.TryBuildOrderTriggersPayload(action, selected, out var payload, out _, out var failure), failure);
                            command = payload;
                        };
                        Check(overlay.ConfirmCurrent(), "Trigger order is submittable"); overlay.Free();
                    } else {
                        var bar = GD.Load<PackedScene>("res://scenes/components/ActionBar.tscn").Instantiate<ActionBar>(); AddChild(bar);
                        bar.ActionSelected += id => controller.SelectAction(id); bar.ShowPrompt(view["message"].AsString(), controller.Actions);
                        if (actionName == "CHOOSE_CARDS")
                            foreach (var target in expected.GetProperty("chosenObjectIds").EnumerateArray().Select(v => v.GetString()!))
                                Check(controller.TrySelectChoice("target", target), "Confirmed enemy selected before amounts");
                        else {
                            var primary = (Button)typeof(ActionBar).GetField("_primaryAction", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(bar)!;
                            Check(!primary.Disabled, "Response pass is enabled"); primary.EmitSignal(BaseButton.SignalName.Pressed);
                        }
                        bar.ShowSelection(controller.Current!, controller.CurrentChoices, controller.CurrentStepLabel, controller.CurrentStepRequired);
                        var candidate = prompt.GetProperty("candidates").EnumerateArray().Single(c => c.GetProperty("action").GetString() == actionName);
                        bar.SubmitRequested += selection => {
                            var type = typeof(Main).GetNestedType("PromptSelection", BindingFlags.NonPublic)!;
                            var selected = Activator.CreateInstance(type, [selection.SourceId, selection.TargetIds.ToArray(), selection.DestinationId, selection.Mode, selection.OptionalCostIds.ToArray()]);
                            command = (Dictionary<string, object?>)typeof(Main).GetMethod("CommandFromTemplate", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [candidate, selected, controller.PromptId, controller.SnapshotTick])!;
                        };
                        Check(!bar.GetNode<Button>("%SubmitButton").Disabled, "Production submit is enabled");
                        bar.GetNode<Button>("%SubmitButton").EmitSignal(BaseButton.SignalName.Pressed); bar.Free();
                    }
                    Check(command is not null, "Production command emitted");
                    command!["promptId"] = prompt.GetProperty("promptId").GetString(); command["snapshotTick"] = prompt.GetProperty("snapshotTick").GetInt64();
                    File.WriteAllText(Path.Combine(dir, name + "-command.json"), JsonSerializer.Serialize(command)); count++;
                }
            }
            GD.Print($"ATTACK_DAMAGE_PASS: {count} production trigger confirmation, response and allocation commands"); GetTree().Quit();
        } catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}

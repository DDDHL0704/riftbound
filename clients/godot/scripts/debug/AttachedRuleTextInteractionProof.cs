using System.Reflection;
using System.Text.Json;
using Godot;
using Riftbound.GodotClient.Interaction;
using Riftbound.GodotClient.Ui;

namespace Riftbound.GodotClient.Debug;

public partial class AttachedRuleTextInteractionProof : Control
{
    public override void _Ready()
    {
        try {
            var root = OS.GetCmdlineUserArgs().Single(a => a.StartsWith("--evidence="))[11..]; var count = 0;
            foreach (var scenario in new[] { "might", "azir", "reset" }) {
                var names = new List<string> { "equip", "equip-pass-1", "equip-pass-2" };
                if (scenario != "might") names.AddRange(["native", "native-pass-1", "native-pass-2", "copy", "copy-pass-1", "copy-pass-2"]);
                if (scenario == "reset") names.AddRange(["detach", "detach-pass-1", "detach-pass-2", "reequip", "reequip-pass-1", "reequip-pass-2", "fresh", "fresh-pass-1", "fresh-pass-2"]);
                foreach (var name in names) {
                    var dir = Path.Combine(root, scenario);
                    using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, name + "-prompt.json"))); var prompt = document.RootElement;
                    var view = (Godot.Collections.Dictionary)typeof(Main).GetMethod("BuildPromptView", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [prompt])!;
                    var controller = new PromptInteractionController(); controller.Load(view);
                    var bar = GD.Load<PackedScene>("res://scenes/components/ActionBar.tscn").Instantiate<ActionBar>(); AddChild(bar);
                    bar.ActionSelected += id => controller.SelectAction(id); bar.ShowPrompt(view["message"].AsString(), controller.Actions);
                    var action = name.Contains("-pass-") ? "PASS_PRIORITY" : name is "equip" or "reequip" ? "ASSEMBLE_EQUIPMENT"
                        : name == "detach" ? "PLAY_CARD" : "ACTIVATE_ABILITY";
                    var candidate = prompt.GetProperty("candidates").EnumerateArray().Single(c => c.GetProperty("action").GetString() == action);
                    if (action == "PASS_PRIORITY") {
                        var primary = (Button)typeof(ActionBar).GetField("_primaryAction", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(bar)!;
                        Check(!primary.Disabled, "Production response button enabled"); primary.EmitSignal(BaseButton.SignalName.Pressed);
                    } else {
                        var label = controller.Actions.Single(a => a.Name == action).Label;
                        bar.GetNode<HBoxContainer>("%ActionChoices").GetChildren().OfType<Button>().Single(b => b.Text == label).EmitSignal(BaseButton.SignalName.Pressed);
                        var source = action == "ASSEMBLE_EQUIPMENT" ? "GEAR" : action == "PLAY_CARD" ? "SPELL" : "HOST";
                        if (controller.Current!.SourceId != source) Check(controller.TrySelectChoice("source", source), "Source selection");
                        if (action == "ACTIVATE_ABILITY") {
                            var rows = candidate.GetProperty("metadata").GetProperty("sourceRequirements").EnumerateArray();
                            var row = rows.Single(r => r.GetProperty("sourceObjectId").GetString() == "HOST"
                                && (r.TryGetProperty("abilityInstanceId", out _) == (name != "native")));
                            var mode = row.TryGetProperty("abilityInstanceId", out var copyId) ? copyId.GetString()! : row.GetProperty("abilityId").GetString()!;
                            Check(controller.TrySelectChoice("mode", mode), "Explicit original/copied ability instance selection");
                            if (!controller.Current!.TargetIds.SequenceEqual(new[] { "TARGET" })) Check(controller.TrySelectChoice("target", "TARGET"), "Controlled target");
                            controller.Load(view); Check(controller.Current!.Mode == mode, "Reload retains the exact copied instance");
                        } else {
                            var targets = action == "PLAY_CARD" ? new[] { "HOST", "GEAR" } : new[] { "HOST" };
                            foreach (var target in targets) if (!controller.Current!.TargetIds.Contains(target)) Check(controller.TrySelectChoice("target", target), "Unit/equipment target selection");
                            if (action == "ASSEMBLE_EQUIPMENT" && !controller.Current!.OptionalCostIds.Contains("ASSEMBLE_GREEN"))
                                Check(controller.TrySelectChoice("optionalCost", "ASSEMBLE_GREEN"), "Printed Equip cost selection");
                        }
                    }
                    bar.ShowSelection(controller.Current!, controller.CurrentChoices, controller.CurrentStepLabel, controller.CurrentStepRequired);
                    Dictionary<string, object?>? command = null;
                    bar.SubmitRequested += selection => {
                        var type = typeof(Main).GetNestedType("PromptSelection", BindingFlags.NonPublic)!;
                        var selected = Activator.CreateInstance(type, [selection.SourceId, selection.TargetIds.ToArray(), selection.DestinationId, selection.Mode, selection.OptionalCostIds.ToArray()]);
                        command = (Dictionary<string, object?>)typeof(Main).GetMethod("CommandFromTemplate", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [candidate, selected, controller.PromptId, controller.SnapshotTick])!;
                    };
                    var submit = bar.GetNode<Button>("%SubmitButton"); Check(!submit.Disabled, "Production submit enabled"); submit.EmitSignal(BaseButton.SignalName.Pressed);
                    Check(command is not null, "Production command emitted");
                    if (name is "copy" or "fresh") Check(command!.TryGetValue("abilityInstanceId", out var id) && id is string, "Command carries selected instance id");
                    if (name == "native") Check(!command!.ContainsKey("abilityInstanceId"), "Original command stays on original instance");
                    if (name == "detach") Check(controller.Current!.TargetIds.SequenceEqual(new[] { "HOST", "GEAR" }), "Both ordered targets survive production selection");
                    File.WriteAllText(Path.Combine(dir, name + "-command.json"), JsonSerializer.Serialize(command)); bar.Free(); count++;
                }
            }
            GD.Print($"ATTACHED_RULE_TEXT_PASS: {count} production commands"); GetTree().Quit();
        } catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}

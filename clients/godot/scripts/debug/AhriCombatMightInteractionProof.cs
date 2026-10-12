using System.Reflection;
using System.Text.Json;
using Godot;
using Riftbound.GodotClient.Interaction;
using Riftbound.GodotClient.Ui;

namespace Riftbound.GodotClient.Debug;

public partial class AhriCombatMightInteractionProof : Control
{
    public override void _Ready()
    {
        try {
            var root = OS.GetCmdlineUserArgs().Single(a => a.StartsWith("--evidence="))[11..]; var count = 0;
            foreach (var branch in new[] { "attack-aura", "defend-ven" }) {
                var dir = Path.Combine(root, branch); Move(dir); count++;
                using var selectionDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, "selection.json")));
                var target = selectionDocument.RootElement.GetProperty("targetObjectIds")[0].GetString()!;
                foreach (var name in new[] { "choose", "pass-1", "pass-2" }) {
                    using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, name + "-prompt.json")));
                    var prompt = document.RootElement;
                    var view = (Godot.Collections.Dictionary)typeof(Main).GetMethod("BuildPromptView", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [prompt])!;
                    var controller = new PromptInteractionController(); controller.Load(view);
                    var bar = GD.Load<PackedScene>("res://scenes/components/ActionBar.tscn").Instantiate<ActionBar>(); AddChild(bar);
                    bar.ActionSelected += id => controller.SelectAction(id); bar.ShowPrompt(view["message"].AsString(), controller.Actions);
                    var action = name == "choose" ? "CHOOSE_CARDS" : "PASS_PRIORITY";
                    if (name == "choose") {
                        var label = controller.Actions.Single(a => a.Name == action).Label;
                        bar.GetNode<HBoxContainer>("%ActionChoices").GetChildren().OfType<Button>().Single(b => b.Text == label).EmitSignal(BaseButton.SignalName.Pressed);
                        Check(!controller.TrySelectChoice("target", "HIDDEN-OPPONENT-CARD"), "Unrevealed target is not offered");
                        Check(controller.TrySelectChoice("target", target), "Exactly one server-offered local enemy selected");
                        Check(controller.Current is { CanSubmit: true }, "Confirmed target can be submitted");
                        controller.Load(view); Check(controller.Current!.TargetIds.SequenceEqual(new[] { target }), "Reload retains the enemy choice");
                    } else {
                        var primary = (Button)typeof(ActionBar).GetField("_primaryAction", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(bar)!;
                        Check(!primary.Disabled && primary.Text == "不响应 · 让过", "Production response pass is enabled"); primary.EmitSignal(BaseButton.SignalName.Pressed);
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
                    var submit = bar.GetNode<Button>("%SubmitButton"); Check(!submit.Disabled, "Production submit enabled"); submit.EmitSignal(BaseButton.SignalName.Pressed);
                    Check(command is not null, "Production command emitted");
                    if (name == "choose") Check(((IEnumerable<string>)command!["chosenObjectIds"]!).SequenceEqual(new[] { target }), "Exact enemy survives mapping");
                    File.WriteAllText(Path.Combine(dir, name + "-command.json"), JsonSerializer.Serialize(command)); bar.Free(); count++;
                }
            }
            GD.Print($"AHRI_COMBAT_MIGHT_PASS: {count} production movement, target and response commands"); GetTree().Quit();
        } catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }

    private void Move(string dir)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, "move-prompt.json"))); var prompt = document.RootElement;
        var candidate = prompt.GetProperty("candidates").EnumerateArray().Single(c => c.GetProperty("action").GetString() == "MOVE_UNIT");
        var overlay = new MovementOverlay { TableMode = true }; AddChild(overlay);
        Check(overlay.Open(candidate, prompt.GetProperty("promptId").GetString()!, prompt.GetProperty("snapshotTick").GetInt64(), _ => null, d => d, "HOST"), "Open production movement composer");
        Check(!overlay.TrySelectTableDestination("BATTLEFIELD:FORGED"), "Unknown destination rejected");
        Check(overlay.TrySelectTableDestination("BATTLEFIELD:F"), "Real battlefield selected");
        Check(overlay.TableSelectedObjects.SequenceEqual(new[] { "HOST" }), "Selected unit retained");
        var calls = 0;
        overlay.Confirmed += payload => {
            payload["promptId"] = overlay.PromptId; payload["snapshotTick"] = overlay.SnapshotTick;
            File.WriteAllText(Path.Combine(dir, "move-command.json"), JsonSerializer.Serialize(payload)); calls++;
        };
        var confirm = overlay.FindChild("ConfirmMovementButton", true, false) as Button;
        Check(confirm is not null && !confirm.Disabled, "Production movement submit enabled"); confirm!.EmitSignal(BaseButton.SignalName.Pressed);
        confirm.EmitSignal(BaseButton.SignalName.Pressed); Check(calls == 1, "Duplicate movement click submits once"); overlay.Free();
    }

    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}

using System.Reflection;
using System.Text.Json;
using Godot;
using Riftbound.GodotClient.Interaction;
using Riftbound.GodotClient.Ui;

namespace Riftbound.GodotClient.Debug;

public partial class SunkenTempleInteractionProof : Control
{
    public override void _Ready()
    {
        try {
            var root = OS.GetCmdlineUserArgs().Single(a => a.StartsWith("--evidence="))[11..]; var count = 0;
            foreach (var branch in new[] { "pay", "decline", "below" }) {
                var dir = Path.Combine(root, branch);
                var steps = JsonSerializer.Deserialize<string[]>(File.ReadAllText(Path.Combine(dir, "steps.json")))!;
                foreach (var step in steps) {
                    using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, step + "-prompt.json")));
                    var prompt = document.RootElement;
                    if (step == "move") Move(dir, prompt);
                    else Action(dir, step, branch, prompt);
                    count++;
                }
            }
            GD.Print($"SUNKEN_TEMPLE_PASS: {count} production move, focus, cost and response commands"); GetTree().Quit();
        } catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }

    private void Move(string dir, JsonElement prompt)
    {
        var candidate = prompt.GetProperty("candidates").EnumerateArray().Single(c => c.GetProperty("action").GetString() == "MOVE_UNIT");
        var overlay = new MovementOverlay { TableMode = true }; AddChild(overlay);
        Check(overlay.Open(candidate, prompt.GetProperty("promptId").GetString()!, prompt.GetProperty("snapshotTick").GetInt64(), _ => null, id => id, "UNIT"), "Production movement composer opened");
        Check(overlay.TrySelectTableDestination("BATTLEFIELD:BF"), "Server offers the temple destination");
        Check(!overlay.TryToggleTableSource("HIDDEN-OPPONENT-CARD"), "Hidden source is not selectable");
        Dictionary<string, object?>? command = null;
        overlay.Confirmed += payload => { command = payload; payload["promptId"] = overlay.PromptId; payload["snapshotTick"] = overlay.SnapshotTick; };
        var button = (Button)typeof(MovementOverlay).GetField("_confirm", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(overlay)!;
        Check(!button.Disabled, "Production move confirmation enabled"); button.EmitSignal(BaseButton.SignalName.Pressed);
        Check(command is not null, "Production move command emitted");
        File.WriteAllText(Path.Combine(dir, "move-command.json"), JsonSerializer.Serialize(command)); overlay.Free();
    }

    private void Action(string dir, string step, string branch, JsonElement prompt)
    {
        var action = step.StartsWith("focus-") ? "PASS_FOCUS" : step == "cost" ? "PAY_COST" : "PASS_PRIORITY";
        var view = (Godot.Collections.Dictionary)typeof(Main).GetMethod("BuildPromptView", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [prompt])!;
        var controller = new PromptInteractionController(); controller.Load(view);
        var bar = GD.Load<PackedScene>("res://scenes/components/ActionBar.tscn").Instantiate<ActionBar>(); AddChild(bar);
        bar.ActionSelected += id => controller.SelectAction(id); bar.ShowPrompt(view["message"].AsString(), controller.Actions);
        if (step == "cost") {
            var label = controller.Actions.Single(a => a.Name == action).Label;
            bar.GetNode<HBoxContainer>("%ActionChoices").GetChildren().OfType<Button>().Single(b => b.Text == label).EmitSignal(BaseButton.SignalName.Pressed);
            Check(controller.TrySelectChoice("target", branch == "pay" ? "PAY" : "DECLINE"), "Server offers the selected cost choice");
        } else {
            ((Button)typeof(ActionBar).GetField("_primaryAction", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(bar)!).EmitSignal(BaseButton.SignalName.Pressed);
        }
        bar.ShowSelection(controller.Current!, controller.CurrentChoices, controller.CurrentStepLabel, controller.CurrentStepRequired);
        var candidate = prompt.GetProperty("candidates").EnumerateArray().Single(c => c.GetProperty("action").GetString() == action);
        Dictionary<string, object?>? command = null;
        bar.SubmitRequested += selected => {
            var type = typeof(Main).GetNestedType("PromptSelection", BindingFlags.NonPublic)!;
            var selection = Activator.CreateInstance(type, [selected.SourceId, selected.TargetIds.ToArray(), selected.DestinationId, selected.Mode, selected.OptionalCostIds.ToArray()]);
            command = (Dictionary<string, object?>)typeof(Main).GetMethod("CommandFromTemplate", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [candidate, selection, controller.PromptId, controller.SnapshotTick])!;
        };
        var submit = bar.GetNode<Button>("%SubmitButton"); Check(!submit.Disabled, "Production action confirmation enabled"); submit.EmitSignal(BaseButton.SignalName.Pressed);
        Check(command is not null, "Production action command emitted");
        File.WriteAllText(Path.Combine(dir, step + "-command.json"), JsonSerializer.Serialize(command)); bar.Free();
    }

    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}

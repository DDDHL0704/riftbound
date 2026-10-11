using System.Reflection;
using System.Text.Json;
using Godot;
using Riftbound.GodotClient.Interaction;
using Riftbound.GodotClient.Ui;

namespace Riftbound.GodotClient.Debug;

public partial class AbilityCounterInteractionProof : Control
{
    public override void _Ready()
    {
        try {
            var root = OS.GetCmdlineUserArgs().Single(a => a.StartsWith("--evidence="))[11..]; var count = 0;
            foreach (var scenario in new[] { "azir", "copied", "play-trigger", "source-gone", "laurent-base" }) {
                var dir = Path.Combine(root, scenario);
                using var selectedDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, "selection.json")));
                var expected = selectedDocument.RootElement;
                foreach (var name in new[] { "counter", "pass-1", "pass-2" }) {
                    using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, name + "-prompt.json"))); var prompt = document.RootElement;
                    var view = (Godot.Collections.Dictionary)typeof(Main).GetMethod("BuildPromptView", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [prompt])!;
                    var controller = new PromptInteractionController(); controller.Load(view);
                    var bar = GD.Load<PackedScene>("res://scenes/components/ActionBar.tscn").Instantiate<ActionBar>(); AddChild(bar);
                    bar.ActionSelected += id => controller.SelectAction(id); bar.ShowPrompt(view["message"].AsString(), controller.Actions);
                    var action = name == "counter" ? "PLAY_CARD" : "PASS_PRIORITY";
                    if (action == "PASS_PRIORITY") {
                        var primary = (Button)typeof(ActionBar).GetField("_primaryAction", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(bar)!;
                        Check(!primary.Disabled && primary.Text == "不响应 · 让过", "Production pass is enabled"); primary.EmitSignal(BaseButton.SignalName.Pressed);
                    } else {
                        var label = controller.Actions.Single(a => a.Name == action).Label;
                        bar.GetNode<HBoxContainer>("%ActionChoices").GetChildren().OfType<Button>().Single(b => b.Text == label).EmitSignal(BaseButton.SignalName.Pressed);
                        var source = expected.GetProperty("sourceObjectId").GetString()!;
                        if (controller.Current!.SourceId != source) Check(controller.TrySelectChoice("source", source), "Counter source selected");
                        foreach (var target in expected.GetProperty("targetObjectIds").EnumerateArray().Select(v => v.GetString()!))
                            if (!controller.Current!.TargetIds.Contains(target)) Check(controller.TrySelectChoice("target", target), "Exact unit or independent stack reference selected");
                        Check(controller.Current!.CanSubmit, "Counter is submittable");
                        controller.Load(view); Check(controller.Current!.CanSubmit, "Reload retains counter selection");
                    }
                    bar.ShowSelection(controller.Current!, controller.CurrentChoices, controller.CurrentStepLabel, controller.CurrentStepRequired);
                    var candidate = prompt.GetProperty("candidates").EnumerateArray().Single(c => c.GetProperty("action").GetString() == action);
                    Dictionary<string, object?>? command = null;
                    bar.SubmitRequested += selection => {
                        var type = typeof(Main).GetNestedType("PromptSelection", BindingFlags.NonPublic)!;
                        var selected = Activator.CreateInstance(type, [selection.SourceId, selection.TargetIds.ToArray(), selection.DestinationId, selection.Mode, selection.OptionalCostIds.ToArray()]);
                        command = (Dictionary<string, object?>)typeof(Main).GetMethod("CommandFromTemplate", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [candidate, selected, controller.PromptId, controller.SnapshotTick])!;
                    };
                    var submit = bar.GetNode<Button>("%SubmitButton"); Check(!submit.Disabled, "Production submit is enabled"); submit.EmitSignal(BaseButton.SignalName.Pressed);
                    Check(command is not null, "Production command emitted");
                    if (name == "counter") Check(((IEnumerable<string>)command!["targetObjectIds"]!).SequenceEqual(expected.GetProperty("targetObjectIds").EnumerateArray().Select(v => v.GetString()!)), "Counter targets preserve their order and stack identity");
                    File.WriteAllText(Path.Combine(dir, name + "-command.json"), JsonSerializer.Serialize(command)); bar.Free(); count++;
                }
            }
            GD.Print($"ABILITY_COUNTER_PASS: {count} production counter and response commands"); GetTree().Quit();
        } catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}

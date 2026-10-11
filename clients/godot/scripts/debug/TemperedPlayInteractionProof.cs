using System.Reflection;
using System.Text.Json;
using Godot;
using Riftbound.GodotClient.Interaction;
using Riftbound.GodotClient.Ui;

namespace Riftbound.GodotClient.Debug;

public partial class TemperedPlayInteractionProof : Control
{
    public override void _Ready()
    {
        try {
            var root = OS.GetCmdlineUserArgs().Single(a => a.StartsWith("--evidence="))[11..]; var count = 0;
            foreach (var scenario in new[] { "axe", "decline", "colored-rune", "jax" }) {
                var names = scenario == "decline" ? new[] { "play", "confirm" }
                    : scenario == "colored-rune" ? new[] { "play", "confirm", "pass-1", "pass-2", "tap", "recycle", "pay" }
                    : scenario == "jax" ? new[] { "play", "confirm", "pass-1", "pass-2", "draw-pay", "draw-pass-1", "draw-pass-2" }
                    : new[] { "play", "confirm", "pass-1", "pass-2" };
                foreach (var name in names) {
                    var dir = Path.Combine(root, scenario);
                    using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, name + "-prompt.json"))); var prompt = document.RootElement;
                    var view = (Godot.Collections.Dictionary)typeof(Main).GetMethod("BuildPromptView", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [prompt])!;
                    var controller = new PromptInteractionController(); controller.Load(view);
                    var bar = GD.Load<PackedScene>("res://scenes/components/ActionBar.tscn").Instantiate<ActionBar>(); AddChild(bar);
                    bar.ActionSelected += id => controller.SelectAction(id); bar.ShowPrompt(view["message"].AsString(), controller.Actions);
                    var action = name switch { "play" => "PLAY_CARD", "confirm" => "CHOOSE_CARDS", "tap" => "TAP_RUNE",
                        "recycle" => "RECYCLE_RUNE", "pay" or "draw-pay" => "PAY_COST", _ => "PASS_PRIORITY" };
                    if (action == "PASS_PRIORITY") {
                        var primary = (Button)typeof(ActionBar).GetField("_primaryAction", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(bar)!;
                        Check(!primary.Disabled && primary.Text == "不响应 · 让过", "Production pass is enabled"); primary.EmitSignal(BaseButton.SignalName.Pressed);
                    } else {
                        var label = controller.Actions.Single(a => a.Name == action).Label;
                        bar.GetNode<HBoxContainer>("%ActionChoices").GetChildren().OfType<Button>().Single(b => b.Text == label).EmitSignal(BaseButton.SignalName.Pressed);
                        if (action == "PLAY_CARD" && controller.Current!.SourceId != "HOST") Check(controller.TrySelectChoice("source", "HOST"), "Played unit selection");
                        if (action is "TAP_RUNE" or "RECYCLE_RUNE" && controller.Current!.SourceId != "RUNE") Check(controller.TrySelectChoice("source", "RUNE"), "Payment rune selection");
                        if (action == "CHOOSE_CARDS" && scenario != "decline") Check(controller.TrySelectChoice("target", "GEAR"), "Weaponmaster chooses gear after play");
                        if (action == "PAY_COST") Check(controller.TrySelectChoice("target", "PAY"), "Instruction payment choice");
                        Check(controller.Current!.CanSubmit, "Production selection can submit");
                        controller.Load(view);
                        Check(controller.Current!.CanSubmit, "Reload retains submission legality");
                        if (action == "PLAY_CARD") Check(controller.Current.OptionalCostIds.Count == 0, "Weaponmaster is not a card play cost");
                        if (action == "CHOOSE_CARDS") Check(controller.Current.TargetIds.SequenceEqual(scenario == "decline" ? Array.Empty<string>() : new[] { "GEAR" }), "Reload retains keyword decision");
                        if (action == "PAY_COST") Check(controller.Current.TargetIds.SequenceEqual(new[] { "PAY" }), "Reload retains payment choice");
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
                    Check(command is not null && (string)command["cmdType"]! == action, "Exact production command emitted");
                    if (action == "PLAY_CARD") Check(!command!.TryGetValue("optionalCosts", out var costs) || !((IEnumerable<string>)costs!).Any(), "No obsolete inline gear cost");
                    File.WriteAllText(Path.Combine(dir, name + "-command.json"), JsonSerializer.Serialize(command)); bar.Free(); count++;
                }
            }
            GD.Print($"TEMPERED_PLAY_PASS: {count} production play, confirmation, rune, payment and response commands"); GetTree().Quit();
        } catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}

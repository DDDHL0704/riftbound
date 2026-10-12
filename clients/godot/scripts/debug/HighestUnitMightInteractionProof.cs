using System.Reflection;
using System.Text.Json;
using Godot;
using Riftbound.GodotClient.Interaction;
using Riftbound.Contracts;
using Riftbound.GodotClient.Ui;

namespace Riftbound.GodotClient.Debug;

public partial class HighestUnitMightInteractionProof : Control
{
    public override void _Ready()
    {
        try {
            var root = OS.GetCmdlineUserArgs().Single(a => a.StartsWith("--evidence="))[11..]; var count = 0;
            foreach (var branch in new[] { "equipped", "below", "missing-red" }) {
                var dir = Path.Combine(root, branch); if (!Play(dir)) continue; count++;
                foreach (var name in new[] { "pass-1", "pass-2", "last-breath-pass-1", "last-breath-pass-2" }) {
                    using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, name + "-prompt.json")));
                    var prompt = document.RootElement;
                    var view = (Godot.Collections.Dictionary)typeof(Main).GetMethod("BuildPromptView", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [prompt])!;
                    var controller = new PromptInteractionController(); controller.Load(view);
                    var bar = GD.Load<PackedScene>("res://scenes/components/ActionBar.tscn").Instantiate<ActionBar>(); AddChild(bar);
                    bar.ActionSelected += id => controller.SelectAction(id); bar.ShowPrompt(view["message"].AsString(), controller.Actions);
                    const string action = "PASS_PRIORITY";
                    Check(!controller.Actions.Any(a => a.Name == "CHOOSE_CARDS"), "Last Breath draw chooses no target");
                    var primary = (Button)typeof(ActionBar).GetField("_primaryAction", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(bar)!;
                    Check(!primary.Disabled && primary.Text == "不响应 · 让过", "Production response pass enabled"); primary.EmitSignal(BaseButton.SignalName.Pressed);
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
                    File.WriteAllText(Path.Combine(dir, name + "-command.json"), JsonSerializer.Serialize(command)); bar.Free(); count++;
                }
            }
            GD.Print($"HIGHEST_MIGHT_PASS: {count} production quote-approved play and response commands; two insufficient quotes blocked"); GetTree().Quit();
        } catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }

    private bool Play(string dir)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, "play-prompt.json")));
        using var selection = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, "selection.json")));
        var prompt = document.RootElement; var expected = selection.RootElement;
        var source = expected.GetProperty("sourceObjectId").GetString()!;
        var target = expected.GetProperty("targetObjectIds")[0].GetString()!;
        var candidate = prompt.GetProperty("candidates").EnumerateArray().Single(c => c.GetProperty("action").GetString() == "PLAY_CARD");
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var overlay = new PlayCardOverlay { TableMode = true }; AddChild(overlay);
        PlayCostPreviewRequestDto? request = null; overlay.PreviewRequested += value => request = value;
        if (!candidate.GetProperty("enabled").GetBoolean()) {
            Check(!overlay.Open(candidate, prompt.GetProperty("promptId").GetString()!, prompt.GetProperty("snapshotTick").GetInt64(), _ => null, source), "Server withholds an unaffordable source from the production composer");
            Check(request is null, "An unavailable source emits no preview or command");
            File.WriteAllText(Path.Combine(dir, "blocked-confirmation.json"), JsonSerializer.Serialize(new { candidateEnabled = false, composerOpened = false, commandEmitted = false }));
            overlay.Free(); return false;
        }
        Check(overlay.Open(candidate, prompt.GetProperty("promptId").GetString()!, prompt.GetProperty("snapshotTick").GetInt64(), _ => null, source), "Open production play composer");
        Check(!overlay.TrySelectTableObject("HIDDEN-OPPONENT-CARD"), "Reject an unrevealed object");
        Check(overlay.TrySelectTableObject(target), "Select the server-offered unit");
        Check(request is not null && request.Command.SourceObjectId == source
            && request.Command.TargetObjectIds.SequenceEqual(new[] { target }), "Preview retains source and target");
        File.WriteAllText(Path.Combine(dir, "play-preview.json"), JsonSerializer.Serialize(request, json));
        var quote = JsonSerializer.Deserialize<PlayCostQuoteDto>(File.ReadAllText(Path.Combine(dir, "play-quote.json")), json)!;
        overlay.ApplyQuote(quote with { RequestId = request!.RequestId });
        if (!quote.CanPay) {
            var blocked = (Button)typeof(PlayCardOverlay).GetField("_confirm", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(overlay)!;
            Check(blocked.Disabled, "Insufficient mana or printed red power blocks production confirmation");
            File.WriteAllText(Path.Combine(dir, "blocked-confirmation.json"), JsonSerializer.Serialize(new { quote.CanPay, disabled = blocked.Disabled, commandEmitted = false }));
            overlay.Free(); return false;
        }
        Dictionary<string, object?>? command = null;
        overlay.Confirmed += payload => {
            command = payload; command["promptId"] = overlay.PromptId; command["snapshotTick"] = overlay.SnapshotTick;
        };
        var submit = (Button)typeof(PlayCardOverlay).GetField("_confirm", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(overlay)!;
        Check(!submit.Disabled, "Backend quote enables production confirmation"); submit.EmitSignal(BaseButton.SignalName.Pressed);
        Check(command is not null, "Production play command emitted");
        Check((string)command!["cardNo"]! == expected.GetProperty("cardNo").GetString(), "Exact card identity preserved");
        File.WriteAllText(Path.Combine(dir, "play-command.json"), JsonSerializer.Serialize(command, json)); overlay.Free(); return true;
    }

    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}

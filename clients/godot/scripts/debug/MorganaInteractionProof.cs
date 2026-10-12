using System.Reflection;
using System.Text.Json;
using Godot;
using Riftbound.GodotClient.Interaction;
using Riftbound.GodotClient.Ui;
using Riftbound.Contracts;

namespace Riftbound.GodotClient.Debug;

public partial class MorganaInteractionProof : Control
{
    public override void _Ready()
    {
        try {
            var root = OS.GetCmdlineUserArgs().Single(a => a.StartsWith("--evidence="))[11..]; var count = 0;
            foreach (var branch in new[] { "base", "ambush", "ward-pay", "ward-decline" }) {
                var dir = Path.Combine(root, branch);
                var steps = JsonSerializer.Deserialize<string[]>(File.ReadAllText(Path.Combine(dir, "steps.json")))!;
                Play(dir); count++;
                foreach (var step in steps) { Produce(dir, step, branch); count++; }
            }
            GD.Print($"MORGANA_MARKED_DAMAGE_PASS: {count} production unit target, battlefield and independent ability response commands"); GetTree().Quit();
        } catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }

    private void Produce(string dir, string step, string branch)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, step + "-prompt.json")));
        var prompt = document.RootElement;
        var action = step == "target-choice" ? "CHOOSE_CARDS" : step == "ward-cost" ? "PAY_COST" : "PASS_PRIORITY";
        var view = (Godot.Collections.Dictionary)typeof(Main).GetMethod("BuildPromptView", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [prompt])!;
        var controller = new PromptInteractionController(); controller.Load(view);
        var bar = GD.Load<PackedScene>("res://scenes/components/ActionBar.tscn").Instantiate<ActionBar>(); AddChild(bar);
        bar.ActionSelected += id => controller.SelectAction(id); bar.ShowPrompt(view["message"].AsString(), controller.Actions);
        if (step is "target-choice" or "ward-cost") {
            var label = controller.Actions.Single(a => a.Name == action).Label;
            bar.GetNode<HBoxContainer>("%ActionChoices").GetChildren().OfType<Button>().Single(b => b.Text == label).EmitSignal(BaseButton.SignalName.Pressed);
            Check(controller.TrySelectChoice("target", step == "target-choice" ? "HOST" : branch == "ward-pay" ? "PAY" : "DECLINE"), "Server offers the post-entry target/cost decision");
        } else {
            var primary = (Button)typeof(ActionBar).GetField("_primaryAction", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(bar)!;
            Check(!primary.Disabled, "Production response pass enabled"); primary.EmitSignal(BaseButton.SignalName.Pressed);
        }
        bar.ShowSelection(controller.Current!, controller.CurrentChoices, controller.CurrentStepLabel, controller.CurrentStepRequired);
        var candidate = prompt.GetProperty("candidates").EnumerateArray().Single(c => c.GetProperty("action").GetString() == action);
        Dictionary<string, object?>? command = null;
        bar.SubmitRequested += selected => {
            var type = typeof(Main).GetNestedType("PromptSelection", BindingFlags.NonPublic)!;
            var selection = Activator.CreateInstance(type, [selected.SourceId, selected.TargetIds.ToArray(), selected.DestinationId, selected.Mode, selected.OptionalCostIds.ToArray()]);
            command = (Dictionary<string, object?>)typeof(Main).GetMethod("CommandFromTemplate", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [candidate, selection, controller.PromptId, controller.SnapshotTick])!;
        };
        var submit = bar.GetNode<Button>("%SubmitButton"); Check(!submit.Disabled, "Production confirmation enabled"); submit.EmitSignal(BaseButton.SignalName.Pressed);
        Check(command is not null, "Production command emitted");
        File.WriteAllText(Path.Combine(dir, step + "-command.json"), JsonSerializer.Serialize(command)); bar.Free();
    }

    private void Play(string dir)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, "play-prompt.json")));
        using var selection = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, "selection.json")));
        var prompt = document.RootElement; var expected = selection.RootElement;
        var source = expected.GetProperty("sourceObjectId").GetString()!;
        var targets = expected.GetProperty("targetObjectIds").EnumerateArray().Select(t => t.GetString()!).ToArray();
        var candidate = prompt.GetProperty("candidates").EnumerateArray().Single(c => c.GetProperty("action").GetString() == "PLAY_CARD");
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var overlay = new PlayCardOverlay { TableMode = true }; AddChild(overlay);
        PlayCostPreviewRequestDto? request = null; overlay.PreviewRequested += value => request = value;
        Check(overlay.Open(candidate, prompt.GetProperty("promptId").GetString()!, prompt.GetProperty("snapshotTick").GetInt64(), _ => null, source), "Open production play composer");
        Check(!overlay.TrySelectTableObject("HIDDEN-OPPONENT-CARD"), "Reject an unrevealed object");
        foreach (var target in targets) Check(overlay.TrySelectTableObject(target), "Select a server-offered unit");
        var destination = expected.GetProperty("destination").GetString()!;
        Check(overlay.TrySelectTableDestination(destination), "Select server-offered entry destination");
        Check(request is not null && request.Command.SourceObjectId == source
            && request.Command.TargetObjectIds.SequenceEqual(targets), "Preview retains source and target");
        File.WriteAllText(Path.Combine(dir, "play-preview.json"), JsonSerializer.Serialize(request, json));
        var quote = JsonSerializer.Deserialize<PlayCostQuoteDto>(File.ReadAllText(Path.Combine(dir, "play-quote.json")), json)!;
        overlay.ApplyQuote(quote with { RequestId = request!.RequestId });
        Dictionary<string, object?>? command = null;
        overlay.Confirmed += payload => {
            command = payload; command["promptId"] = overlay.PromptId; command["snapshotTick"] = overlay.SnapshotTick;
        };
        var submit = (Button)typeof(PlayCardOverlay).GetField("_confirm", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(overlay)!;
        Check(!submit.Disabled, "Backend quote enables production confirmation"); submit.EmitSignal(BaseButton.SignalName.Pressed);
        Check(command is not null, "Production play command emitted");
        Check((string)command!["cardNo"]! == expected.GetProperty("cardNo").GetString(), "Exact card identity preserved");
        File.WriteAllText(Path.Combine(dir, "play-command.json"), JsonSerializer.Serialize(command, json)); overlay.Free();
    }

    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}

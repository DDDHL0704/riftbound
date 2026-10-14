using System.Reflection;
using System.Text.Json;
using Godot;
using Riftbound.GodotClient.Interaction;
using Riftbound.GodotClient.Ui;

namespace Riftbound.GodotClient.Debug;

public partial class ConquestTokenInteractionProof : Control
{
    public override void _Ready()
    {
        try {
            var root = OS.GetCmdlineUserArgs().Single(a => a.StartsWith("--evidence="))[11..]; var count = 0;
            foreach (var branch in new[] { "primary-plain", "primary-reaction", "alternate-plain", "alternate-reaction" }) {
                var dir = Path.Combine(root, branch);
                foreach (var step in JsonSerializer.Deserialize<string[]>(File.ReadAllText(Path.Combine(dir, "steps.json")))!) {
                    Produce(dir, step); count++;
                }
            }
            GD.Print($"CONQUEST_TOKEN_PASS: {count} production movement, focus, reaction and Gold response commands"); GetTree().Quit();
        } catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
    private void Produce(string dir, string step)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, step + "-prompt.json")));
        using var selected = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, step + "-selection.json")));
        var prompt = doc.RootElement; var fallback = selected.RootElement; var action = fallback.GetProperty("cmdType").GetString()!;
        if (action == "PLAY_CARD") { Play(dir, step); return; }
        if (action == "MOVE_UNIT") { Move(dir, step); return; }
        var candidate = prompt.GetProperty("candidates").EnumerateArray().Single(c => c.GetProperty("action").GetString() == action);
        var view = (Godot.Collections.Dictionary)typeof(Main).GetMethod("BuildPromptView", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [prompt])!;
        var controller = new PromptInteractionController(); controller.Load(view);
        var bar = GD.Load<PackedScene>("res://scenes/components/ActionBar.tscn").Instantiate<ActionBar>(); AddChild(bar);
        void Refresh() => bar.ShowSelection(controller.Current!, controller.CurrentChoices, controller.CurrentStepLabel, controller.CurrentStepRequired);
        bar.ActionSelected += id => { Check(controller.SelectAction(id), "Server action is selectable"); Refresh(); };
        bar.ChoiceSelected += (role, id) => { Check(controller.TrySelectChoice(role, id), "Server choice is selectable"); Refresh(); };
        bar.ShowPrompt(view["message"].AsString(), controller.Actions);
        if (action is "PASS_PRIORITY" or "PASS_FOCUS") {
            var pass = (Button)typeof(ActionBar).GetField("_primaryAction", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(bar)!;
            Check(!pass.Disabled, "Response is available"); pass.EmitSignal(BaseButton.SignalName.Pressed);
        } else {
            var label = controller.Actions.Single(a => a.Name == action).Label;
            bar.GetNode<HBoxContainer>("%ActionChoices").GetChildren().OfType<Button>().Single(b => b.Text == label).EmitSignal(BaseButton.SignalName.Pressed);
            var picks = action == "RECYCLE_RUNE" ? new[] { fallback.GetProperty("sourceObjectId").GetString()! }
                : fallback.GetProperty(action == "PAY_COST" ? "paymentChoiceIds" : "chosenObjectIds").EnumerateArray().Select(v => v.GetString()!).ToArray();
            foreach (var pick in picks) {
                if (controller.Current!.SourceId == pick || controller.Current.TargetIds.Contains(pick)) continue;
                var choices = controller.CurrentChoices.ToArray(); var index = Array.FindIndex(choices, c => c.Id == pick);
                Check(index >= 0, "Requested object is offered by the server");
                bar.GetNode<HBoxContainer>("%StepChoices").GetChildren().OfType<Button>().ElementAt(index).EmitSignal(BaseButton.SignalName.Pressed);
            }
        }
        Refresh(); Dictionary<string, object?>? command = null;
        bar.SubmitRequested += selection => {
            var type = typeof(Main).GetNestedType("PromptSelection", BindingFlags.NonPublic)!;
            var value = Activator.CreateInstance(type, [selection.SourceId, selection.TargetIds.ToArray(), selection.DestinationId, selection.Mode, selection.OptionalCostIds.ToArray()]);
            command = (Dictionary<string, object?>)typeof(Main).GetMethod("CommandFromTemplate", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [candidate, value, controller.PromptId, controller.SnapshotTick])!;
        };
        var submit = bar.GetNode<Button>("%SubmitButton"); Check(!submit.Disabled, "Valid optional selection can be confirmed"); submit.EmitSignal(BaseButton.SignalName.Pressed);
        Check(command is not null, "Production control emitted a command");
        File.WriteAllText(Path.Combine(dir, step + "-command.json"), JsonSerializer.Serialize(command)); bar.Free();
    }
    private void Play(string dir, string step)
    {
        using var promptDoc = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, step + "-prompt.json")));
        using var selection = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, step + "-selection.json")));
        var prompt = promptDoc.RootElement; var expected = selection.RootElement;
        var candidate = prompt.GetProperty("candidates").EnumerateArray().Single(c => c.GetProperty("action").GetString() == "PLAY_CARD");
        var overlay = new PlayCardOverlay { TableMode = true }; AddChild(overlay);
        Riftbound.Contracts.PlayCostPreviewRequestDto? preview = null; overlay.PreviewRequested += value => preview = value;
        Check(overlay.Open(candidate, prompt.GetProperty("promptId").GetString()!, prompt.GetProperty("snapshotTick").GetInt64(), _ => null, "COUNTER"), "Production play overlay opens");
        Check(preview is not null && preview.Command.TargetObjectIds.Count == 0, "Production reaction preview has no selected targets");
        var quote = JsonSerializer.Deserialize<Riftbound.Contracts.PlayCostQuoteDto>(File.ReadAllText(Path.Combine(dir, step + "-quote.json")), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        overlay.ApplyQuote(quote with { RequestId = preview!.RequestId });
        Dictionary<string, object?>? command = null;
        overlay.Confirmed += value => { command = value; command["promptId"] = overlay.PromptId; command["snapshotTick"] = overlay.SnapshotTick; };
        var confirm = (Button)typeof(PlayCardOverlay).GetField("_confirm", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(overlay)!;
        Check(!confirm.Disabled, "Quoted resource payment is available"); confirm.EmitSignal(BaseButton.SignalName.Pressed);
        Check(command is not null, "Production play emitted a command"); File.WriteAllText(Path.Combine(dir, step + "-command.json"), JsonSerializer.Serialize(command)); overlay.Free();
    }
    private void Move(string dir, string step)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, step + "-prompt.json")));
        var prompt = doc.RootElement;
        var candidate = prompt.GetProperty("candidates").EnumerateArray().Single(c => c.GetProperty("action").GetString() == "MOVE_UNIT");
        var overlay = new MovementOverlay { TableMode = true }; AddChild(overlay);
        Check(overlay.Open(candidate, prompt.GetProperty("promptId").GetString()!, prompt.GetProperty("snapshotTick").GetInt64(), _ => null, id => id, "UNIT"), "Production movement composer opens");
        Check(!overlay.TrySelectTableDestination("BATTLEFIELD:FORGED"), "Forged destination is rejected");
        Check(overlay.TrySelectTableDestination("BATTLEFIELD:BF"), "Server battlefield is selected");
        Dictionary<string, object?>? command = null;
        overlay.Confirmed += value => { command = value; command["promptId"] = overlay.PromptId; command["snapshotTick"] = overlay.SnapshotTick; };
        var confirm = overlay.FindChild("ConfirmMovementButton", true, false) as Button;
        Check(confirm is not null && !confirm.Disabled, "Actual movement button is enabled"); confirm!.EmitSignal(BaseButton.SignalName.Pressed);
        Check(command is not null, "Production movement emitted command"); File.WriteAllText(Path.Combine(dir, step + "-command.json"), JsonSerializer.Serialize(command)); overlay.Free();
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}

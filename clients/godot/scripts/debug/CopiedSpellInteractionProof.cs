using System.Reflection;
using System.Text.Json;
using Godot;
using Riftbound.GodotClient.Interaction;
using Riftbound.GodotClient.Ui;
using Riftbound.Contracts;

namespace Riftbound.GodotClient.Debug;

public partial class CopiedSpellInteractionProof : Control
{
    public override void _Ready()
    {
        try {
            var root = OS.GetCmdlineUserArgs().Single(a => a.StartsWith("--evidence="))[11..]; var count = 0;
            foreach (var branch in new[] { "one", "two", "detach" }) {
                var dir = Path.Combine(root, branch);
                using var steps = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, "steps.json")));
                foreach (var step in steps.RootElement.EnumerateArray()) {
                    var name = step.GetProperty("name").GetString()!; var action = step.GetProperty("action").GetString()!;
                    if (action == "PLAY_CARD") Play(dir, name); else Produce(dir, name, action);
                    count++;
                }
            }
            GD.Print($"COPIED_SPELL_INSTANCE_PASS: {count} production spell, original/copied ordering and independent response commands"); GetTree().Quit();
        } catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }

    private void Produce(string dir, string step, string actionName)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, step + "-prompt.json"))); var prompt = doc.RootElement;
        var view = (Godot.Collections.Dictionary)typeof(Main).GetMethod("BuildPromptView", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [prompt])!;
        Dictionary<string, object?>? command = null;
        if (actionName == "ORDER_TRIGGERS") {
            var action = view["actions"].AsGodotArray().Select(v => v.AsGodotDictionary()).Single(a => a["action"].AsString() == actionName);
            var overlay = GD.Load<PackedScene>("res://scenes/overlays/TriggerOrderOverlay.tscn").Instantiate<TriggerOrderOverlay>(); AddChild(overlay);
            Check(overlay.ShowPrompt(action, out var reason), reason);
            using var expected = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, step + "-selection.json")));
            var ids = expected.RootElement.GetProperty("triggerIds").EnumerateArray().Select(v => v.GetString()!).ToArray();
            var count = overlay.OrderedTriggerIds.Count;
            for (var i = 0; i < count - 1; i++) for (var j = 0; j < count - i - 1; j++)
                overlay.GetNode<VBoxContainer>("%TriggerRows").GetChild(j).GetNode<Button>("MoveDownButton").EmitSignal(BaseButton.SignalName.Pressed);
            Check(overlay.OrderedTriggerIds.SequenceEqual(ids), "User order retains each original/copied trigger identity");
            overlay.Confirmed += selected => {
                Check(SpecialPromptCommandBuilder.TryBuildOrderTriggersPayload(action, selected, out var payload, out _, out var error), error);
                payload["promptId"] = prompt.GetProperty("promptId").GetString(); payload["snapshotTick"] = prompt.GetProperty("snapshotTick").GetInt64(); command = payload;
            };
            overlay.GetNode<Button>("%ConfirmButton").EmitSignal(BaseButton.SignalName.Pressed); overlay.Free();
        } else {
            var controller = new PromptInteractionController(); controller.Load(view);
            var bar = GD.Load<PackedScene>("res://scenes/components/ActionBar.tscn").Instantiate<ActionBar>(); AddChild(bar);
            bar.ActionSelected += id => controller.SelectAction(id); bar.ShowPrompt(view["message"].AsString(), controller.Actions);
            var primary = (Button)typeof(ActionBar).GetField("_primaryAction", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(bar)!;
            Check(!primary.Disabled, "Independent response enabled"); primary.EmitSignal(BaseButton.SignalName.Pressed);
            var candidate = prompt.GetProperty("candidates").EnumerateArray().Single(c => c.GetProperty("action").GetString() == actionName);
            bar.ShowSelection(controller.Current!, controller.CurrentChoices, controller.CurrentStepLabel, controller.CurrentStepRequired);
            bar.SubmitRequested += selected => {
                var type = typeof(Main).GetNestedType("PromptSelection", BindingFlags.NonPublic)!;
                var value = Activator.CreateInstance(type, [selected.SourceId, selected.TargetIds.ToArray(), selected.DestinationId, selected.Mode, selected.OptionalCostIds.ToArray()]);
                command = (Dictionary<string, object?>)typeof(Main).GetMethod("CommandFromTemplate", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [candidate, value, controller.PromptId, controller.SnapshotTick])!;
            };
            var submit = bar.GetNode<Button>("%SubmitButton"); Check(!submit.Disabled, "Pass confirmation enabled"); submit.EmitSignal(BaseButton.SignalName.Pressed); bar.Free();
        }
        Check(command is not null, "Production command emitted");
        File.WriteAllText(Path.Combine(dir, step + "-command.json"), JsonSerializer.Serialize(command));
    }

    private void Play(string dir, string step)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, step + "-prompt.json")));
        using var selection = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, step + "-selection.json")));
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
        var destination = expected.TryGetProperty("destination", out var entry) && entry.ValueKind == JsonValueKind.String ? entry.GetString()! : "";
        if (!string.IsNullOrEmpty(destination)) Check(overlay.TrySelectTableDestination(destination), "Select server-offered entry destination");
        Check(request is not null && request.Command.SourceObjectId == source
            && request.Command.TargetObjectIds.SequenceEqual(targets), "Preview retains source and target");
        File.WriteAllText(Path.Combine(dir, step + "-preview.json"), JsonSerializer.Serialize(request, json));
        var quote = JsonSerializer.Deserialize<PlayCostQuoteDto>(File.ReadAllText(Path.Combine(dir, step + "-quote.json")), json)!;
        overlay.ApplyQuote(quote with { RequestId = request!.RequestId });
        Dictionary<string, object?>? command = null;
        overlay.Confirmed += payload => {
            command = payload; command["promptId"] = overlay.PromptId; command["snapshotTick"] = overlay.SnapshotTick;
        };
        var submit = (Button)typeof(PlayCardOverlay).GetField("_confirm", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(overlay)!;
        Check(!submit.Disabled, "Backend quote enables production confirmation"); submit.EmitSignal(BaseButton.SignalName.Pressed);
        Check(command is not null, "Production play command emitted");
        Check((string)command!["cardNo"]! == expected.GetProperty("cardNo").GetString(), "Exact card identity preserved");
        File.WriteAllText(Path.Combine(dir, step + "-command.json"), JsonSerializer.Serialize(command, json)); overlay.Free();
    }

    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}

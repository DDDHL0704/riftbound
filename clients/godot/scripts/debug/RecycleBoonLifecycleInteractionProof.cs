using System.Reflection;
using System.Text.Json;
using Godot;
using Riftbound.GodotClient.Interaction;
using Riftbound.GodotClient.Ui;

namespace Riftbound.GodotClient.Debug;

public partial class RecycleBoonLifecycleInteractionProof : Control
{
    public override void _Ready()
    {
        try {
            var root = OS.GetCmdlineUserArgs().Single(a => a.StartsWith("--evidence="))[11..]; var count = 0;
            foreach (var dir in Directory.GetDirectories(root).SelectMany(path => File.Exists(Path.Combine(path, "steps.json"))
                ? new[] { path } : Directory.GetDirectories(path)).Order(StringComparer.Ordinal)) {
                foreach (var step in JsonSerializer.Deserialize<string[]>(File.ReadAllText(Path.Combine(dir, "steps.json")))!) {
                    Produce(dir, step); count++;
                }
            }
            GD.Print($"RECYCLED_BOON_LIFECYCLE_PASS: {count} production copy, look, recycle, return and surviving ability commands"); GetTree().Quit();
        } catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
    private void Produce(string dir, string step)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, step + "-prompt.json")));
        using var selected = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, step + "-selection.json")));
        var prompt = doc.RootElement; var fallback = selected.RootElement; var action = fallback.GetProperty("cmdType").GetString()!;
        if (action == "PLAY_CARD") { Play(dir, step); return; }
        var candidate = prompt.GetProperty("candidates").EnumerateArray().Single(c => c.GetProperty("action").GetString() == action);
        var view = (Godot.Collections.Dictionary)typeof(Main).GetMethod("BuildPromptView", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [prompt])!;
        if (action == "ORDER_TRIGGERS") {
            var orderAction = view["actions"].AsGodotArray().Select(v => v.AsGodotDictionary()).Single(a => a["action"].AsString() == action);
            var overlay = GD.Load<PackedScene>("res://scenes/overlays/TriggerOrderOverlay.tscn").Instantiate<TriggerOrderOverlay>(); AddChild(overlay);
            Check(overlay.ShowPrompt(orderAction, out var reason), reason);
            var ids = fallback.GetProperty("orderedTriggerIds").EnumerateArray().Select(v => v.GetString()!).ToArray();
            Check(overlay.OrderedTriggerIds.Count == ids.Length && ids.Distinct(StringComparer.Ordinal).Count() == ids.Length, "Each trigger appears once");
            for (var position = 0; position < ids.Length; position++) {
                var current = overlay.OrderedTriggerIds.ToList().IndexOf(ids[position]); Check(current >= position, "Offered trigger belongs to order");
                while (current > position) { overlay.GetNode<VBoxContainer>("%TriggerRows").GetChild(current).GetNode<Button>("MoveUpButton").EmitSignal(BaseButton.SignalName.Pressed); current--; }
            }
            Dictionary<string, object?>? ordered = null;
            overlay.Confirmed += selectedIds => {
                Check(SpecialPromptCommandBuilder.TryBuildOrderTriggersPayload(orderAction, selectedIds, out var payload, out _, out var error), error);
                payload["promptId"] = prompt.GetProperty("promptId").GetString(); payload["snapshotTick"] = prompt.GetProperty("snapshotTick").GetInt64(); ordered = payload;
            };
            overlay.GetNode<Button>("%ConfirmButton").EmitSignal(BaseButton.SignalName.Pressed); Check(ordered is not null, "Order control emitted command");
            File.WriteAllText(Path.Combine(dir, step + "-command.json"), ClientCommandSerializer.Serialize(ordered!).GetRawText()); overlay.Free(); return;
        }
        var controller = new PromptInteractionController(); controller.Load(view);
        var bar = GD.Load<PackedScene>("res://scenes/components/ActionBar.tscn").Instantiate<ActionBar>(); AddChild(bar);
        void Refresh() => bar.ShowSelection(controller.Current!, controller.CurrentChoices, controller.CurrentStepLabel, controller.CurrentStepRequired);
        bar.ActionSelected += id => { Check(controller.SelectAction(id), "Server action is selectable"); Refresh(); };
        bar.ChoiceSelected += (role, id) => { Check(controller.TrySelectChoice(role, id), "Server choice is selectable"); Refresh(); };
        bar.ShowPrompt(view["message"].AsString(), controller.Actions);
        if (action == "PASS_PRIORITY") {
            var pass = (Button)typeof(ActionBar).GetField("_primaryAction", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(bar)!;
            Check(!pass.Disabled, "Response is available"); pass.EmitSignal(BaseButton.SignalName.Pressed);
        } else {
            var label = controller.Actions.Single(a => a.Name == action).Label;
            bar.GetNode<HBoxContainer>("%ActionChoices").GetChildren().OfType<Button>().Single(b => b.Text == label).EmitSignal(BaseButton.SignalName.Pressed);
            var picks = action == "ACTIVATE_ABILITY" ? new[] { fallback.GetProperty("sourceObjectId").GetString()! }.Concat(fallback.GetProperty("targetObjectIds").EnumerateArray().Select(v => v.GetString()!)).ToArray()
                : action == "ASSEMBLE_EQUIPMENT" ? new[] { fallback.GetProperty("sourceObjectId").GetString()!, fallback.GetProperty("targetObjectId").GetString()! }
                    .Concat(fallback.GetProperty("optionalCosts").EnumerateArray().Select(v => v.GetString()!)).ToArray()
                : fallback.GetProperty("chosenObjectIds").EnumerateArray().Select(v => v.GetString()!).ToArray();
            foreach (var pick in picks) {
                if (controller.Current!.SourceId == pick || controller.Current.TargetIds.Contains(pick) || controller.Current.OptionalCostIds.Contains(pick)) continue;
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
        File.WriteAllText(Path.Combine(dir, step + "-command.json"), ClientCommandSerializer.Serialize(command!).GetRawText()); bar.Free();
    }
    private void Play(string dir, string step)
    {
        using var promptDoc = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, step + "-prompt.json")));
        using var selection = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, step + "-selection.json")));
        var prompt = promptDoc.RootElement; var expected = selection.RootElement;
        var candidate = prompt.GetProperty("candidates").EnumerateArray().Single(c => c.GetProperty("action").GetString() == "PLAY_CARD");
        var overlay = new PlayCardOverlay { TableMode = true }; AddChild(overlay);
        Riftbound.Contracts.PlayCostPreviewRequestDto? preview = null; overlay.PreviewRequested += value => preview = value;
        Check(overlay.Open(candidate, prompt.GetProperty("promptId").GetString()!, prompt.GetProperty("snapshotTick").GetInt64(), _ => null, expected.GetProperty("sourceObjectId").GetString()!), "Production play overlay opens");
        foreach (var target in expected.GetProperty("targetObjectIds").EnumerateArray())
            Check(overlay.TrySelectTableObject(target.GetString()!), "Server target is selectable in the production composer");
        if (expected.TryGetProperty("optionalCosts", out var costs) && costs.ValueKind == JsonValueKind.Array)
        {
            var controls = (Dictionary<string, CheckBox>)typeof(PlayCardOverlay).GetField("_optional", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(overlay)!;
            foreach (var cost in costs.EnumerateArray())
            {
                Check(controls.TryGetValue(cost.GetString()!, out var control), "Quoted optional cost has a production checkbox");
                control!.ButtonPressed = true;
            }
            Check(preview is not null && (preview.Command.OptionalCosts ?? []).SequenceEqual(costs.EnumerateArray().Select(c => c.GetString()!)), "Production preview preserves selected optional costs");
        }
        if (expected.TryGetProperty("repeatChoices", out var repeatChoices) && repeatChoices.ValueKind == JsonValueKind.Array && repeatChoices.GetArrayLength() > 0)
        {
            var repeats = (System.Collections.IList)typeof(PlayCardOverlay).GetField("_repeats", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(overlay)!;
            Check(repeats.Count == repeatChoices.GetArrayLength(), "Every paid repeat has an independent production declaration");
            for (var index = 0; index < repeats.Count; index++)
            {
                var entry = repeats[index]!; var expectedChoice = repeatChoices[index];
                var targets = (List<(OptionButton Picker, string[] Ids, bool Required)>)entry.GetType().GetField("Targets")!.GetValue(entry)!;
                var ids = expectedChoice.GetProperty("targetObjectIds").EnumerateArray().Select(value => value.GetString()!).ToArray();
                Check(targets.Count == ids.Length, "Each declared target has a production target picker");
                for (var target = 0; target < targets.Count; target++)
                {
                    targets[target].Picker.GrabFocus();
                    Check(overlay.TrySelectTableObject(ids[target]), "Focused repeat target accepts the declared legal table object");
                }
            }
            Check(preview is not null && preview.Command.RepeatChoices is { } declarations
                && declarations.Count == repeatChoices.GetArrayLength()
                && declarations.Select((choice, index) => choice.TargetObjectIds.SequenceEqual(repeatChoices[index].GetProperty("targetObjectIds").EnumerateArray().Select(value => value.GetString()!))).All(value => value),
                "Production preview preserves every independent repeated target");
        }
        Check(preview is not null && preview.Command.TargetObjectIds.SequenceEqual(expected.GetProperty("targetObjectIds").EnumerateArray().Select(v => v.GetString()!)), "Production preview preserves the actual targets");
        var quote = JsonSerializer.Deserialize<Riftbound.Contracts.PlayCostQuoteDto>(File.ReadAllText(Path.Combine(dir, step + "-quote.json")), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        overlay.ApplyQuote(quote with { RequestId = preview!.RequestId });
        Dictionary<string, object?>? command = null;
        overlay.Confirmed += value => { command = value; command["promptId"] = overlay.PromptId; command["snapshotTick"] = overlay.SnapshotTick; };
        var confirm = (Button)typeof(PlayCardOverlay).GetField("_confirm", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(overlay)!;
        Check(!confirm.Disabled, "Quoted resource payment is available"); confirm.EmitSignal(BaseButton.SignalName.Pressed);
        Check(command is not null, "Production play emitted a command"); File.WriteAllText(Path.Combine(dir, step + "-command.json"), ClientCommandSerializer.Serialize(command!).GetRawText()); overlay.Free();
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}

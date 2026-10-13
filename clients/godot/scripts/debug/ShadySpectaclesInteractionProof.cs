using System.Reflection;
using System.Text.Json;
using Godot;
using Riftbound.GodotClient.Interaction;
using Riftbound.GodotClient.Ui;
using Riftbound.Contracts;

namespace Riftbound.GodotClient.Debug;

public partial class ShadySpectaclesInteractionProof : Control
{
    public override void _Ready()
    {
        try {
            var root = OS.GetCmdlineUserArgs().Single(a => a.StartsWith("--evidence="))[11..]; var count = 0;
            var branches = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--branches="))?[11..].Split(',')
                ?? ["refresh", "spell", "return"];
            foreach (var branch in branches) {
                var dir = Path.Combine(root, branch);
                using var steps = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, "steps.json")));
                foreach (var step in steps.RootElement.EnumerateArray()) {
                    var name = step.GetProperty("name").GetString()!; var action = step.GetProperty("action").GetString()!;
                    try {
                        if (action == "PLAY_CARD") Play(dir, name);
                        else if (action == "MOVE_UNIT") Move(dir, name);
                        else if (action == "DECLARE_BATTLE") Declare(dir, name);
                        else if (action == "ASSIGN_COMBAT_DAMAGE") Damage(dir, name);
                        else Produce(dir, name, action);
                    }
                    catch (Exception error) { throw new InvalidOperationException($"Native step {branch}/{name}: {error.Message}", error); }
                    count++;
                }
            }
            GD.Print($"SHADY_SPECTACLES_PASS: {count} production spell, original/copied ordering and independent response commands"); GetTree().Quit();
        } catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }

    private void Move(string dir, string step)
    {
        using var prompt = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, step + "-prompt.json")));
        using var selection = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, step + "-selection.json")));
        var p = prompt.RootElement; var expected = selection.RootElement;
        var candidate = p.GetProperty("candidates").EnumerateArray().Single(c => c.GetProperty("action").GetString() == "MOVE_UNIT");
        var overlay = new MovementOverlay { TableMode = true }; AddChild(overlay);
        Check(overlay.Open(candidate, p.GetProperty("promptId").GetString()!, p.GetProperty("snapshotTick").GetInt64(),
            _ => null, label => label), "Production movement prompt opens");
        Check(!overlay.TrySelectTableDestination("BATTLEFIELD:FORGED"), "Unknown movement destination rejected");
        Check(overlay.TrySelectTableDestination(expected.GetProperty("destination").GetString()!), "Select real movement destination");
        var sources = expected.TryGetProperty("sourceObjectIds", out var group) && group.ValueKind == JsonValueKind.Array
            ? group.EnumerateArray().Select(x => x.GetString()!).ToArray() : [expected.GetProperty("sourceObjectId").GetString()!];
        foreach (var id in sources) Check(overlay.TryToggleTableSource(id), "Select a server-offered movement source");
        Dictionary<string, object?>? payload = null;
        overlay.Confirmed += command => { command["promptId"] = p.GetProperty("promptId").GetString();
            command["snapshotTick"] = p.GetProperty("snapshotTick").GetInt64(); payload = command; };
        ((Button)overlay.FindChild("ConfirmMovementButton", true, false)).EmitSignal(BaseButton.SignalName.Pressed);
        Check(payload is not null, "Production movement confirmation emits command");
        File.WriteAllText(Path.Combine(dir, step + "-command.json"), JsonSerializer.Serialize(payload)); overlay.Free();
    }

    private void Declare(string dir, string step)
    {
        using var prompt = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, step + "-prompt.json")));
        using var selection = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, step + "-selection.json")));
        var p = prompt.RootElement;
        var candidate = p.GetProperty("candidates").EnumerateArray().Single(c => c.GetProperty("action").GetString() == "DECLARE_BATTLE");
        var overlay = new BattleDeclarationOverlay(); AddChild(overlay);
        Check(overlay.Open(candidate, p.GetProperty("promptId").GetString()!, p.GetProperty("snapshotTick").GetInt64(), id => id),
            "Production battle declaration opens");
        var picks = (IEnumerable<(string Field, OptionButton Picker, string[] Ids, bool Required)>)typeof(BattleDeclarationOverlay)
            .GetField("_picks", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(overlay)!;
        var selectedByField = new Dictionary<string, HashSet<string>>();
        foreach (var pick in picks)
        {
            if (!selectedByField.TryGetValue(pick.Field, out var used)) selectedByField[pick.Field] = used = new(StringComparer.Ordinal);
            var expected = selection.RootElement.GetProperty(pick.Field);
            var id = pick.Field == "battlefieldId" ? expected.GetString()!
                : expected.EnumerateArray().Select(value => value.GetString()!).FirstOrDefault(value => pick.Ids.Contains(value) && !used.Contains(value)) ?? "";
            var position = Array.IndexOf(pick.Ids, id);
            Check(position >= 0 && (!pick.Required || position > 0), "Select only legal server battle participants");
            if (id.Length > 0) used.Add(id);
            pick.Picker.Select(position); pick.Picker.EmitSignal(OptionButton.SignalName.ItemSelected, position);
        }
        var costs = (Dictionary<string, CheckBox>)typeof(BattleDeclarationOverlay).GetField("_costs", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(overlay)!;
        var required = (string[])typeof(BattleDeclarationOverlay).GetField("_requiredCosts", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(overlay)!;
        foreach (var cost in selection.RootElement.GetProperty("optionalCosts").EnumerateArray())
        {
            var id = cost.GetString()!;
            Check(costs.ContainsKey(id) || required.Contains(id), "Battle cost comes from current server prompt");
            if (costs.TryGetValue(id, out var check)) check.ButtonPressed = true;
        }
        Dictionary<string, object?>? payload = null;
        overlay.Confirmed += command => { command["promptId"] = p.GetProperty("promptId").GetString();
            command["snapshotTick"] = p.GetProperty("snapshotTick").GetInt64(); payload = command; };
        ((Button)overlay.FindChild("ConfirmBattleButton", true, false)).EmitSignal(BaseButton.SignalName.Pressed);
        Check(payload is not null, "Production battle confirmation emits command");
        File.WriteAllText(Path.Combine(dir, step + "-command.json"), JsonSerializer.Serialize(payload)); overlay.Free();
    }

    private void Damage(string dir, string step)
    {
        using var prompt = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, step + "-prompt.json")));
        var p = prompt.RootElement;
        var candidate = p.GetProperty("candidates").EnumerateArray().Single(c => c.GetProperty("action").GetString() == "ASSIGN_COMBAT_DAMAGE");
        var action = new Godot.Collections.Dictionary { ["action"] = "ASSIGN_COMBAT_DAMAGE", ["enabled"] = true,
            ["candidateJson"] = candidate.GetRawText() };
        var overlay = GD.Load<PackedScene>("res://scenes/overlays/DamageAssignmentOverlay.tscn").Instantiate<DamageAssignmentOverlay>(); AddChild(overlay);
        Check(overlay.ShowPrompt(action, out var reason), reason);
        overlay.GetNode<Button>("%SuggestButton").EmitSignal(BaseButton.SignalName.Pressed);
        Dictionary<string, object?>? payload = null;
        overlay.Confirmed += choices => { Check(SpecialPromptCommandBuilder.TryBuildDamageAssignmentPayload(action, choices, out var command, out _, out var failure), failure);
            command["promptId"] = p.GetProperty("promptId").GetString(); command["snapshotTick"] = p.GetProperty("snapshotTick").GetInt64(); payload = command; };
        overlay.GetNode<Button>("%ConfirmButton").EmitSignal(BaseButton.SignalName.Pressed);
        Check(payload is not null, "Production damage confirmation emits command");
        File.WriteAllText(Path.Combine(dir, step + "-command.json"), JsonSerializer.Serialize(payload)); overlay.Free();
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
            var selectedIds = expected.RootElement.TryGetProperty("orderedTriggerIds", out var orderedIds) && orderedIds.ValueKind == JsonValueKind.Array
                ? orderedIds : expected.RootElement.GetProperty("triggerIds");
            var ids = selectedIds.EnumerateArray().Select(v => v.GetString()!).ToArray();
            Check(overlay.OrderedTriggerIds.Count == ids.Length && ids.Distinct(StringComparer.Ordinal).Count() == ids.Length,
                "Each server trigger is selected exactly once");
            for (var position = 0; position < ids.Length; position++)
            {
                var current = overlay.OrderedTriggerIds.ToList().IndexOf(ids[position]);
                Check(current >= position, "Expected trigger belongs to the server order");
                while (current > position)
                {
                    overlay.GetNode<VBoxContainer>("%TriggerRows").GetChild(current).GetNode<Button>("MoveUpButton").EmitSignal(BaseButton.SignalName.Pressed);
                    current--;
                }
            }
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
            var candidate = prompt.GetProperty("candidates").EnumerateArray().Single(c => c.GetProperty("action").GetString() == actionName);
            if (actionName is "PASS_PRIORITY" or "PASS_FOCUS" or "END_TURN") {
                var primary = (Button)typeof(ActionBar).GetField("_primaryAction", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(bar)!;
                Check(!primary.Disabled, "Primary action enabled"); primary.EmitSignal(BaseButton.SignalName.Pressed);
            } else {
                var label = controller.Actions.Single(a => a.Name == actionName).Label;
                if (actionName == "PAY_COST") {
                    Check(label == candidate.GetProperty("label").GetString(), "Settlement choice uses its server label");
                    using var snapshot = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, step + "-snapshot.json")));
                    if (snapshot.RootElement.GetProperty("timing").GetProperty("pendingPayment").TryGetProperty("choiceKind", out var kind)
                        && kind.GetString() == "ATTACHMENT_COPY_SELECTION")
                        Check(label == "选择复制单位", "Copy choice is explicit");
                }
                bar.GetNode<HBoxContainer>("%ActionChoices").GetChildren().OfType<Button>().Single(button => button.Text == label).EmitSignal(BaseButton.SignalName.Pressed);
                using var selection = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, step + "-selection.json")));
                var expected = selection.RootElement;
                if (actionName is "PAY_COST" or "CHOOSE_CARDS") {
                    foreach (var choice in expected.GetProperty(actionName == "PAY_COST" ? "paymentChoiceIds" : "chosenObjectIds").EnumerateArray())
                        Check(controller.TrySelectChoice("target", choice.GetString()!), "Server-offered friendly copy source");
                } else {
                    var source = expected.GetProperty("sourceObjectId").GetString()!;
                    if (controller.Current!.SourceId != source) Check(controller.TrySelectChoice("source", source), "Select equipment or acquired ability source");
                    if (actionName == "ACTIVATE_ABILITY") {
                        var instance = expected.TryGetProperty("abilityInstanceId", out var copied) && copied.ValueKind == JsonValueKind.String
                            ? copied.GetString()! : expected.GetProperty("abilityId").GetString()!;
                        if (controller.CurrentStepRole == "mode") {
                            foreach (var choice in controller.CurrentChoices) {
                                var requirement = candidate.GetProperty("metadata").GetProperty("sourceRequirements").EnumerateArray()
                                    .First(r => r.GetProperty("sourceObjectId").GetString() == source
                                        && (r.TryGetProperty("abilityChoiceId", out var id) ? id.GetString() : r.GetProperty("abilityId").GetString()) == choice.Id);
                                Check(choice.Label == requirement.GetProperty("abilityLabel").GetString(), "Mode labels use the selected source's actual cost");
                            }
                        }
                        if (controller.Current!.Mode != instance && controller.CurrentStepRole == "mode")
                            Check(controller.TrySelectChoice("mode", instance), "Select the acquired ability instance");
                    }
                    if (actionName == "REVEAL_CARD" && expected.TryGetProperty("mode", out var revealMode)
                        && controller.Current!.Mode != revealMode.GetString() && controller.CurrentStepRole == "mode")
                        Check(controller.TrySelectChoice("mode", revealMode.GetString()!), "Select the server-offered reveal mode");
                    var targets = actionName == "ASSEMBLE_EQUIPMENT" ? [expected.GetProperty("targetObjectId").GetString()!]
                        : expected.TryGetProperty("targetObjectIds", out var chosenTargets)
                            ? chosenTargets.EnumerateArray().Select(target => target.GetString()!).ToArray() : [];
                    foreach (var target in targets)
                        if (!controller.Current!.TargetIds.Contains(target)) Check(controller.TrySelectChoice("target", target), "Select legal target");
                    if (expected.TryGetProperty("destination", out var destination) && destination.ValueKind == JsonValueKind.String)
                        if (controller.Current!.DestinationId != destination.GetString())
                            Check(controller.TrySelectChoice("destination", destination.GetString()!), "Select a server-offered destination");
                    if (expected.TryGetProperty("optionalCosts", out var costs)) foreach (var cost in costs.EnumerateArray())
                        if (!controller.Current!.OptionalCostIds.Contains(cost.GetString()!)) Check(controller.TrySelectChoice("optionalCost", cost.GetString()!), "Select printed cost");
                }
            }
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
        var mode = expected.TryGetProperty("mode", out var selectedMode) ? selectedMode.GetString()! : "";
        var requirements = candidate.GetProperty("metadata").GetProperty("sourceRequirements").EnumerateArray()
            .Where(r => r.GetProperty("sourceObjectId").GetString() == source).ToArray();
        var modeIndex = Array.FindIndex(requirements, r => (r.GetProperty("mode").GetString() ?? "") == mode);
        Check(modeIndex >= 0, "Server offers the chosen spell mode");
        var sourcePicker = (OptionButton)typeof(PlayCardOverlay).GetField("_source", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(overlay)!;
        sourcePicker.Select(modeIndex); sourcePicker.EmitSignal(OptionButton.SignalName.ItemSelected, modeIndex);
        Check(!overlay.TrySelectTableObject("HIDDEN-OPPONENT-CARD"), "Reject an unrevealed object");
        foreach (var target in targets) Check(overlay.TrySelectTableObject(target), "Select a server-offered unit");
        if (expected.TryGetProperty("optionalCosts", out var optionalCosts) && optionalCosts.ValueKind == JsonValueKind.Array) {
            var checks = (Dictionary<string, CheckBox>)typeof(PlayCardOverlay).GetField("_optional", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(overlay)!;
            foreach (var cost in optionalCosts.EnumerateArray()) {
                Check(checks.TryGetValue(cost.GetString()!, out var check), "Server offers the chosen extra cost");
                check!.ButtonPressed = true;
            }
        }
        if (expected.TryGetProperty("repeatChoices", out var repeated) && repeated.ValueKind == JsonValueKind.Array)
        {
            var repeats = (System.Collections.IList)typeof(PlayCardOverlay).GetField("_repeats", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(overlay)!;
            Check(repeats.Count == repeated.GetArrayLength(), "Every declared Echo execution has production controls");
            for (var repeatIndex = 0; repeatIndex < repeats.Count; repeatIndex++)
            {
                var selected = repeated[repeatIndex]; var repeat = repeats[repeatIndex]!; var type = repeat.GetType();
                var picker = (OptionButton)type.GetField("Mode")!.GetValue(repeat)!;
                var modes = (JsonElement[])type.GetField("Modes")!.GetValue(repeat)!;
                var repeatMode = selected.GetProperty("mode").GetString() ?? "";
                var choice = Array.FindIndex(modes, r => (r.GetProperty("mode").GetString() ?? "") == repeatMode);
                Check(choice >= 0, "Echo mode comes from the server prompt");
                picker.Select(choice); picker.EmitSignal(OptionButton.SignalName.ItemSelected, choice);
                var picks = (List<(OptionButton Picker, string[] Ids, bool Required)>)type.GetField("Targets")!.GetValue(repeat)!;
                var chosen = selected.GetProperty("targetObjectIds").EnumerateArray().Select(id => id.GetString()!).ToArray();
                Check(chosen.Length <= picks.Count, "Echo target controls cover the selected targets");
                for (var index = 0; index < picks.Count; index++)
                {
                    var id = index < chosen.Length ? chosen[index] : "";
                    var selectedIndex = Array.IndexOf(picks[index].Ids, id);
                    Check(selectedIndex >= 0 && (!picks[index].Required || selectedIndex > 0), "Echo target is a server-offered choice");
                    picks[index].Picker.Select(selectedIndex);
                    picks[index].Picker.EmitSignal(OptionButton.SignalName.ItemSelected, selectedIndex);
                }
            }
            Check(request?.Command.RepeatChoices?.Count == repeated.GetArrayLength(), "Preview retains every Echo execution");
            for (var index = 0; index < repeated.GetArrayLength(); index++)
                Check(request!.Command.RepeatChoices![index].Mode == (repeated[index].GetProperty("mode").GetString() ?? "")
                    && request.Command.RepeatChoices[index].TargetObjectIds.SequenceEqual(repeated[index].GetProperty("targetObjectIds").EnumerateArray().Select(id => id.GetString()!)),
                    "Preview retains independent Echo targets and modes");
        }
        var destination = expected.TryGetProperty("destination", out var entry) && entry.ValueKind == JsonValueKind.String ? entry.GetString()! : "";
        if (!string.IsNullOrEmpty(destination)) Check(overlay.TrySelectTableDestination(destination), "Select server-offered entry destination");
        var movementDestinations = expected.TryGetProperty("movementDestinations", out var movements) && movements.ValueKind == JsonValueKind.Array
            ? movements.EnumerateArray().Select(d => d.GetString()!).ToArray() : [];
        foreach (var movement in movementDestinations) Check(overlay.TrySelectTableDestination(movement), "Select each server-offered movement destination");
        Check(request is not null && request.Command.SourceObjectId == source
            && request.Command.Mode == mode
            && request.Command.TargetObjectIds.SequenceEqual(targets)
            && (request.Command.MovementDestinations ?? []).SequenceEqual(movementDestinations)
            && (request.Command.OptionalCosts ?? []).SequenceEqual(expected.TryGetProperty("optionalCosts", out var selectedCosts)
                && selectedCosts.ValueKind == JsonValueKind.Array
                ? selectedCosts.EnumerateArray().Select(cost => cost.GetString()!).ToArray() : []), "Preview retains source, target and extra costs");
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

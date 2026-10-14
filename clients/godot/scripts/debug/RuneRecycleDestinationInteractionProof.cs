using System.Reflection;
using System.Text.Json;
using Godot;
using Riftbound.GodotClient.Interaction;
using Riftbound.GodotClient.Ui;

namespace Riftbound.GodotClient.Debug;

public partial class RuneRecycleDestinationInteractionProof : Control
{
    public override void _Ready()
    {
        try {
            var root = OS.GetCmdlineUserArgs().Single(a => a.StartsWith("--evidence="))[11..]; var count = 0;
            foreach (var branch in new[] { "single", "multiple" }) {
                var dir = Path.Combine(root, branch);
                foreach (var step in JsonSerializer.Deserialize<string[]>(File.ReadAllText(Path.Combine(dir, "steps.json")))!) {
                    Produce(dir, step); count++;
                }
            }
            GD.Print($"RUNE_DESTINATION_PASS: {count} production battle and optional rune-trigger commands"); GetTree().Quit();
        } catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
    private void Produce(string dir, string step)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, step + "-prompt.json")));
        using var selected = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, step + "-selection.json")));
        var prompt = doc.RootElement; var fallback = selected.RootElement; var action = fallback.GetProperty("cmdType").GetString()!;
        if (action == "DECLARE_BATTLE") { Declare(dir, step); return; }
        var candidate = prompt.GetProperty("candidates").EnumerateArray().Single(c => c.GetProperty("action").GetString() == action);
        var view = (Godot.Collections.Dictionary)typeof(Main).GetMethod("BuildPromptView", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [prompt])!;
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
            var picks = action == "RECYCLE_RUNE" ? new[] { fallback.GetProperty("sourceObjectId").GetString()! }
                : fallback.GetProperty("chosenObjectIds").EnumerateArray().Select(v => v.GetString()!).ToArray();
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

    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}

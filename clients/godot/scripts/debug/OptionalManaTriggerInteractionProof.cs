using System.Reflection;
using System.Text.Json;
using Godot;
using Riftbound.GodotClient.Interaction;
using Riftbound.GodotClient.Ui;

namespace Riftbound.GodotClient.Debug;

public partial class OptionalManaTriggerInteractionProof : Control
{
    public override void _Ready()
    {
        try
        {
            var root = OS.GetCmdlineUserArgs().Single(a => a.StartsWith("--evidence="))[11..];
            var count = 0;
            foreach (var family in new[] { "vayne", "gold", "legend", "sand", "icevale" })
                foreach (var decline in new[] { false, true })
                {
                    var dir = Path.Combine(root, family + (decline ? "-decline" : "-pay"));
                    foreach (var step in JsonSerializer.Deserialize<string[]>(File.ReadAllText(Path.Combine(dir, "steps.json")))!)
                    { Produce(dir, step, decline); count++; }
                }
            GD.Print($"OPTIONAL_MANA_TRIGGER_PASS: {count} actual production explicit payment and decline commands");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }

    private void Produce(string dir, string step, bool decline)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, step + "-prompt.json")));
        var prompt = doc.RootElement;
        var candidate = prompt.GetProperty("candidates").EnumerateArray().Single(c => c.GetProperty("action").GetString() == "PAY_COST");
        var view = (Godot.Collections.Dictionary)typeof(Main).GetMethod("BuildPromptView", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [prompt])!;
        var controller = new PromptInteractionController(); controller.Load(view);
        var bar = GD.Load<PackedScene>("res://scenes/components/ActionBar.tscn").Instantiate<ActionBar>(); AddChild(bar);
        void Refresh() => bar.ShowSelection(controller.Current!, controller.CurrentChoices, controller.CurrentStepLabel, controller.CurrentStepRequired);
        bar.ActionSelected += id => { Check(controller.SelectAction(id), "Payment must be selectable"); Refresh(); };
        bar.ChoiceSelected += (role, id) => { Check(controller.TrySelectChoice(role, id), "Actual server decision must be selectable"); Refresh(); };
        bar.ShowPrompt(view["message"].AsString(), controller.Actions);
        var label = controller.Actions.Single(a => a.Name == "PAY_COST").Label;
        bar.GetNode<HBoxContainer>("%ActionChoices").GetChildren().OfType<Button>().Single(b => b.Text == label).EmitSignal(BaseButton.SignalName.Pressed);
        Check(!controller.Current!.CanSubmit && bar.GetNode<Button>("%SubmitButton").Disabled, "No implicit payment or empty submit");
        Check(controller.CurrentChoices.Select(c => c.Id).SequenceEqual(new[] { "SPEND_MANA:1", "DECLINE" }), "Both exact server branches are visible");
        Check(!controller.TrySelectChoice("target", "PAY"), "Client must not invent a payment alias");
        var choices = bar.GetNode<HBoxContainer>("%StepChoices").GetChildren().OfType<Button>().ToArray();
        Check(choices.Length == 2 && choices[0].Text == "支付 1 法力" && choices[1].Text == "放弃此效果", "Actual controls explain both decisions");
        choices[decline ? 1 : 0].EmitSignal(BaseButton.SignalName.Pressed);
        Check(controller.Current!.CanSubmit && controller.Current.TargetIds.SequenceEqual(new[] { decline ? "DECLINE" : "SPEND_MANA:1" }), "Explicit choice retained");
        controller.Load(view); Refresh();
        Check(controller.Current!.TargetIds.SequenceEqual(new[] { decline ? "DECLINE" : "SPEND_MANA:1" }), "Same prompt retains explicit decision");
        Dictionary<string, object?>? command = null;
        bar.SubmitRequested += selection =>
        {
            var type = typeof(Main).GetNestedType("PromptSelection", BindingFlags.NonPublic)!;
            var value = Activator.CreateInstance(type, [selection.SourceId, selection.TargetIds.ToArray(), selection.DestinationId, selection.Mode, selection.OptionalCostIds.ToArray()]);
            command = (Dictionary<string, object?>)typeof(Main).GetMethod("CommandFromTemplate", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [candidate, value, controller.PromptId, controller.SnapshotTick])!;
        };
        var submit = bar.GetNode<Button>("%SubmitButton"); Check(!submit.Disabled, "Actual submit enabled after explicit choice");
        submit.EmitSignal(BaseButton.SignalName.Pressed);
        Check(command is not null, "Production control emitted a command");
        var wire = ClientCommandSerializer.Serialize(command!);
        Check(wire.GetProperty("paymentChoiceIds").EnumerateArray().Select(v => v.GetString()).SequenceEqual(new[] { decline ? "DECLINE" : "SPEND_MANA:1" }), "Outgoing wire retains exact branch");
        File.WriteAllText(Path.Combine(dir, step + "-command.json"), wire.GetRawText());
        bar.Free();
    }

    private static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
}

using System.Reflection;
using System.Text.Json;
using Godot;
using Riftbound.GodotClient.Interaction;
using Riftbound.GodotClient.Ui;

namespace Riftbound.GodotClient.Debug;

public partial class HeldConfirmationInteractionProof : Control
{
    public override void _Ready()
    {
        try
        {
            var root = OS.GetCmdlineUserArgs().Single(a => a.StartsWith("--evidence="))[11..];
            foreach (var source in new[] { "boon", "move", "return", "channel", "ward" })
            foreach (var accept in new[] { true, false })
            {
                if (source == "boon" && !accept) continue;
                var dir = Path.Combine(root, source + (accept ? "-accept" : "-decline"));
                using var prompt = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, "prompt.json")));
                var view = (Godot.Collections.Dictionary)typeof(Main).GetMethod("BuildPromptView", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [prompt.RootElement])!;
                var controller = new PromptInteractionController(); controller.Load(view);
                var bar = GD.Load<PackedScene>("res://scenes/components/ActionBar.tscn").Instantiate<ActionBar>(); AddChild(bar);
                var action = source == "ward" ? "PAY_COST" : "CHOOSE_CARDS";
                bar.ActionSelected += id => controller.SelectAction(id); bar.ShowPrompt(view["message"].AsString(), controller.Actions);
                var label = controller.Actions.Single(a => a.Name == action).Label;
                if (source != "ward") Check(label == "确认触发技能", "Confirmation has a specific label");
                bar.GetNode<HBoxContainer>("%ActionChoices").GetChildren().OfType<Button>().Single(b => b.Text == label).EmitSignal(BaseButton.SignalName.Pressed);
                var selected = source == "ward" ? new[] { accept ? "PAY" : "DECLINE" } : accept ? [source switch { "boon" => "UNIT", "move" => "ENEMY", "return" => "G2", _ => "F" }] : Array.Empty<string>();
                foreach (var id in selected)
                {
                    if (controller.Current!.TargetIds.Contains(id)) continue;
                    var option = controller.CurrentChoices.Single(c => c.Id == id);
                    Check(controller.TrySelectChoice("target", option.Id), "The server offers the cost choice");
                }
                Check(controller.Current!.TargetIds.SequenceEqual(selected), "Selected decision is retained");
                Check(controller.Current.Summary.Contains(accept ? "双方响应后结算" : source == "ward" ? "不支付费用" : "放弃"), "Summary explains the timing and cost");
                bar.ShowSelection(controller.Current, controller.CurrentChoices, controller.CurrentStepLabel, controller.CurrentStepRequired);
                var candidate = prompt.RootElement.GetProperty("candidates").EnumerateArray().Single(c => c.GetProperty("action").GetString() == action);
                Dictionary<string, object?>? command = null;
                bar.SubmitRequested += selection => {
                    var type = typeof(Main).GetNestedType("PromptSelection", BindingFlags.NonPublic)!;
                    var state = Activator.CreateInstance(type, [selection.SourceId, selection.TargetIds.ToArray(), selection.DestinationId, selection.Mode, selection.OptionalCostIds.ToArray()]);
                    command = (Dictionary<string, object?>)typeof(Main).GetMethod("CommandFromTemplate", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [candidate, state, controller.PromptId, controller.SnapshotTick])!;
                };
                var submit = bar.GetNode<Button>("%SubmitButton"); Check(!submit.Disabled, "Both payment and decline submit"); submit.EmitSignal(BaseButton.SignalName.Pressed);
                Check(command is not null, "Production submit creates the intent");
                File.WriteAllText(Path.Combine(dir, "command.json"), JsonSerializer.Serialize(command)); bar.Free();
            }
            GD.Print("HELD_CONFIRMATION_INTERACTION_PASS: nine production target, optional and ward decisions"); GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}

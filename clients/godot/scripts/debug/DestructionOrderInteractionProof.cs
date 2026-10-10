using System.Reflection;
using System.Text.Json;
using Godot;
using Riftbound.GodotClient.Interaction;
using Riftbound.GodotClient.Ui;

namespace Riftbound.GodotClient.Debug;

public partial class DestructionOrderInteractionProof : Control
{
    public override void _Ready()
    {
        try {
            var root = OS.GetCmdlineUserArgs().Single(a => a.StartsWith("--evidence="))[11..];
            foreach (var branch in new[] { "gear", "sett", "mandatory", "banish", "altar-pay", "altar-decline" }) {
                var dir = Path.Combine(root, branch);
                using var prompt = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, "prompt.json")));
                using var selection = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, "selection.json")));
                var selected = selection.RootElement.GetProperty("selected").GetString()!;
                var view = (Godot.Collections.Dictionary)typeof(Main).GetMethod("BuildPromptView", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [prompt.RootElement])!;
                var controller = new PromptInteractionController(); controller.Load(view);
                var bar = GD.Load<PackedScene>("res://scenes/components/ActionBar.tscn").Instantiate<ActionBar>(); AddChild(bar);
                bar.ActionSelected += id => controller.SelectAction(id); bar.ShowPrompt(view["message"].AsString(), controller.Actions);
                var label = controller.Actions.Single(a => a.Name == "PAY_COST").Label;
                Check(label == "选择摧毁替换", "Server labels the replacement choice");
                bar.GetNode<HBoxContainer>("%ActionChoices").GetChildren().OfType<Button>().Single(b => b.Text == label).EmitSignal(BaseButton.SignalName.Pressed);
                Check(controller.TrySelectChoice("target", selected), "Select the server's replacement option");
                if (branch == "mandatory") Check(controller.CurrentChoices.All(c => c.Id != "DECLINE"), "Mandatory replacement cannot be declined");
                Check(controller.Current!.Summary.Contains("立即继续原结算"), "Choice continues original resolution");
                using var snapshot = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, "snapshot.json")));
                var phase = MatchPhasePresentation.Build(snapshot.RootElement, "P2", id => id);
                Check(phase["title"].AsString() == "摧毁替换 · 等待选择", "Replacement phase");
                Check(phase["detail"].AsString().Contains("强制替换不能放弃"), "Mandatory and optional decisions are explained");
                bar.ShowSelection(controller.Current, controller.CurrentChoices, controller.CurrentStepLabel, controller.CurrentStepRequired);
                var candidate = prompt.RootElement.GetProperty("candidates").EnumerateArray().Single(c => c.GetProperty("action").GetString() == "PAY_COST");
                Dictionary<string, object?>? command = null;
                bar.SubmitRequested += choice => {
                    var type = typeof(Main).GetNestedType("PromptSelection", BindingFlags.NonPublic)!;
                    var state = Activator.CreateInstance(type, [choice.SourceId, choice.TargetIds.ToArray(), choice.DestinationId, choice.Mode, choice.OptionalCostIds.ToArray()]);
                    command = (Dictionary<string, object?>)typeof(Main).GetMethod("CommandFromTemplate", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [candidate, state, controller.PromptId, controller.SnapshotTick])!;
                };
                var submit = bar.GetNode<Button>("%SubmitButton"); Check(!submit.Disabled, "Decision can be submitted"); submit.EmitSignal(BaseButton.SignalName.Pressed);
                Check(command is not null, "Production submit generates a command");
                File.WriteAllText(Path.Combine(dir, "command.json"), JsonSerializer.Serialize(command)); bar.Free();
            }
            GD.Print("DESTRUCTION_ORDER_PASS: six production replacement commands"); GetTree().Quit();
        } catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
    private static void Check(bool valid, string message) { if (!valid) throw new InvalidOperationException(message); }
}

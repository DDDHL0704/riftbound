using System.Reflection;
using System.Text.Json;
using Godot;
using Riftbound.GodotClient.Interaction;
using Riftbound.GodotClient.Ui;

namespace Riftbound.GodotClient.Debug;

public partial class TriggerCostInteractionProof : Control
{
    public override void _Ready()
    {
        try
        {
            var root = OS.GetCmdlineUserArgs().Single(a => a.StartsWith("--evidence="))[11..];
            var battlefield = OS.GetCmdlineUserArgs().Contains("--battlefield-replacement");
            var sett = OS.GetCmdlineUserArgs().Contains("--sett-replacement");
            var deck = OS.GetCmdlineUserArgs().Contains("--legend-deck");
            var conquest = OS.GetCmdlineUserArgs().Contains("--legend-conquest");
            foreach (var source in sett ? new[] { "sett", "sett-rune" } : deck ? new[] { "cost" } : conquest ? new[] { "irelia", "vi" } : battlefield ? new[] { "ivern", "return" } : new[] { "vex", "renata", "hub" })
            foreach (var accept in new[] { true, false })
            {
                var dir = Path.Combine(root, source + (accept ? "-accept" : "-decline"));
                using var prompt = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, "prompt.json")));
                var view = (Godot.Collections.Dictionary)typeof(Main).GetMethod("BuildPromptView", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [prompt.RootElement])!;
                var controller = new PromptInteractionController(); controller.Load(view);
                var bar = GD.Load<PackedScene>("res://scenes/components/ActionBar.tscn").Instantiate<ActionBar>(); AddChild(bar);
                var action = sett || source is "hub" or "irelia" ? "PAY_COST" : "CHOOSE_CARDS";
                bar.ActionSelected += id => controller.SelectAction(id); bar.ShowPrompt(view["message"].AsString(), controller.Actions);
                var label = controller.Actions.Single(a => a.Name == action).Label;
                if (!sett && source is not ("hub" or "irelia")) Check(label == (source is "return" or "vi" ? "确认触发技能" : "确认触发费用"), "Cost confirmation has a specific label");
                bar.GetNode<HBoxContainer>("%ActionChoices").GetChildren().OfType<Button>().Single(b => b.Text == label).EmitSignal(BaseButton.SignalName.Pressed);
                var selected = sett ? new[] { accept ? $"SETT:D2:LEGEND:{(source == "sett-rune" ? "RUNE:RUNE" : "ANY")}" : "DECLINE" } : source is "hub" or "irelia" ? new[] { accept ? "PAY" : "DECLINE" } : accept ? [source == "vi" ? "TARGET" : source == "return" ? "BF" : "LEGEND"] : Array.Empty<string>();
                foreach (var id in selected)
                {
                    if (controller.Current!.TargetIds.Contains(id)) continue;
                    var option = controller.CurrentChoices.Single(c => c.Id == id);
                    Check(controller.TrySelectChoice("target", option.Id), "The server offers the cost choice");
                }
                Check(controller.Current!.TargetIds.SequenceEqual(selected), "Selected decision is retained");
                Check(controller.Current.Summary.Contains(sett ? "立即继续原结算" : accept ? "双方响应后结算" : source is "hub" or "irelia" ? "不支付费用" : source == "return" ? "保留草丛" : source == "vi" ? "放弃" : "不横置传奇"), "Summary explains the timing and cost");
                if (sett) {
                    Check(label == "选择摧毁替换", "Replacement has a distinct action label");
                    using var snapshot = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, "snapshot.json")));
                    var phase = MatchPhasePresentation.Build(snapshot.RootElement, "P2", id => id);
                    Check(phase["title"].AsString() == "摧毁替换 · 等待选择" && phase["detail"].AsString().Length > 0, "Replacement has instructions and is not presented as a response window");
                }
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
                File.WriteAllText(Path.Combine(dir, deck ? "native-command.json" : "command.json"), JsonSerializer.Serialize(command)); bar.Free();
            }
            GD.Print(sett ? "SETT_REPLACEMENT_INTERACTION_PASS: four production replacement decisions" : deck ? "LEGEND_DECK_COST_PASS: production exhaust and decline commands" : conquest ? "LEGEND_CONQUEST_INTERACTION_PASS: four production payment and target decisions" : battlefield ? "BATTLEFIELD_REPLACEMENT_INTERACTION_PASS: four production create and return decisions" : "TRIGGER_COST_INTERACTION_PASS: six production cost and decline commands"); GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}

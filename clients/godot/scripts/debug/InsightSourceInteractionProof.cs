using System.Reflection;
using System.Text.Json;
using Godot;
using Riftbound.GodotClient.Interaction;
using Riftbound.GodotClient.Ui;

namespace Riftbound.GodotClient.Debug;

public partial class InsightSourceInteractionProof : Control
{
    public override async void _Ready()
    {
        try
        {
            GetWindow().ContentScaleSize = new Vector2I(1200, 360);
            var root = OS.GetCmdlineUserArgs().First(a => a.StartsWith("--evidence="))[11..];
            var completion = OS.GetCmdlineUserArgs().Contains("--spell-completion");
            var fields = completion || OS.GetCmdlineUserArgs().Contains("--field-triggers");
            var cases = completion ? new (string Prompt, string Name, string[] Picks)[] { ("exile", "exile-accept", ["C"]), ("exile", "exile-decline", []), ("hall", "hall-accept", ["P1-BATTLEFIELD-ALLY"]), ("hall", "hall-decline", []) } : fields ? new (string Prompt, string Name, string[] Picks)[] { ("gem", "gem", ["V"]), ("fan", "fan", ["A"]), ("fan", "fan-decline", []) } : new (string Prompt, string Name, string[] Picks)[]
            {
                ("blossom-activation", "blossom-activation", []),
                ("blossom", "blossom-keep", []),
                ("blossom", "blossom-recycle", ["D1"]),
                ("blossom", "blossom-all", ["D1", "D2"]),
                ("blossom-order", "blossom-order", ["D2", "D1"]),
                ("diana-payment", "diana-pay", ["PAY"]),
                ("diana-payment", "diana-decline", ["DECLINE"]),
                ("diana", "diana-keep", []),
                ("diana", "diana-recycle", ["D1"])
            };
            foreach (var scenario in cases)
            {
                using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, scenario.Prompt + "-prompt.json")));
                var map = typeof(Main).GetMethod("BuildPromptView", BindingFlags.Static | BindingFlags.NonPublic)!;
                var view = (Godot.Collections.Dictionary)map.Invoke(null, [document.RootElement])!;
                var controller = new PromptInteractionController();
                controller.Load(view);
                var isOrder = scenario.Prompt.EndsWith("-order");
                var action = scenario.Prompt.EndsWith("-activation") ? "ACTIVATE_ABILITY" : scenario.Prompt.EndsWith("-payment") ? "PAY_COST" : "CHOOSE_CARDS";
                var bar = GD.Load<PackedScene>("res://scenes/components/ActionBar.tscn").Instantiate<ActionBar>();
                AddChild(bar);
                void Refresh() => bar.ShowSelection(controller.Current!, controller.CurrentChoices, controller.CurrentStepLabel, controller.CurrentStepRequired);
                bar.ShowPrompt(view["message"].AsString(), controller.Actions);
                bar.ActionSelected += name => { Check(controller.SelectAction(name), "Production action button selects action"); Refresh(); };
                var label = controller.Actions.Single(a => a.Name == action).Label;
                bar.GetNode<HBoxContainer>("%ActionChoices").GetChildren().OfType<Button>().Single(b => b.Text == label).EmitSignal(BaseButton.SignalName.Pressed);
                Check(!controller.TrySelectObject("D3"), "Unviewed cards are not selectable");
                if (action == "PAY_COST" || isOrder) Check(!controller.Current!.CanSubmit, "Payment and ordering require an explicit choice");
                bar.ChoiceSelected += (role, id) => { Check(controller.TrySelectChoice(role, id), "Visible choice is accepted"); Refresh(); };
                var candidate = document.RootElement.GetProperty("candidates").EnumerateArray().Single(c => c.GetProperty("action").GetString() == action);
                var type = typeof(Main).GetNestedType("PromptSelection", BindingFlags.NonPublic)!;
                var build = typeof(Main).GetMethod("CommandFromTemplate", BindingFlags.Static | BindingFlags.NonPublic)!;
                Dictionary<string, object?>? payload = null;
                bar.SubmitRequested += state =>
                {
                    var selection = Activator.CreateInstance(type, [state.SourceId, state.TargetIds.ToArray(), state.DestinationId, state.Mode, state.OptionalCostIds.ToArray()]);
                    payload = (Dictionary<string, object?>)build.Invoke(null, [candidate, selection, controller.PromptId, controller.SnapshotTick])!;
                };
                if (action == "PAY_COST" && OS.GetCmdlineUserArgs().Contains("--visual-proof"))
                {
                    for (var frame = 0; frame < 5; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                    GetViewport().GetTexture().GetImage().SavePng(Path.Combine(root, "native-" + scenario.Name + "-options.png"));
                }
                foreach (var id in scenario.Picks)
                {
                    if (controller.Current!.TargetIds.Contains(id)) continue;
                    var choices = controller.CurrentChoices.ToArray();
                    var index = Array.FindIndex(choices, c => c.Id == id);
                    Check(index >= 0, "Requested card is offered by the server");
                    bar.GetNode<HBoxContainer>("%StepChoices").GetChildren().OfType<Button>().ElementAt(index).EmitSignal(BaseButton.SignalName.Pressed);
                }
                Check(controller.Current!.TargetIds.SequenceEqual(scenario.Picks), "Click order is retained");
                if (fields) Check(controller.Current.Summary.Contains(scenario.Picks.Length == 0 ? "放弃触发技能" : "技能目标："), "Trigger summary describes the actual decision");
                if (action == "CHOOSE_CARDS" && !fields) Check(controller.Current.Summary.Contains(isOrder ? "牌库顶 →" : scenario.Picks.Length == 0 ? "全部保留" : "回收："), "Confirmation describes the actual choice");
                if (action == "PAY_COST") Check(controller.Current.Summary.Contains(scenario.Picks[0] == "PAY" ? "支付 1 法力" : "放弃此效果"), "Payment confirmation shows the chosen branch");
                var submit = bar.GetNode<Button>("%SubmitButton");
                Check(!submit.Disabled, "Valid choice enables confirmation");
                if (OS.GetCmdlineUserArgs().Contains("--visual-proof"))
                {
                    for (var frame = 0; frame < 5; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                    GetViewport().GetTexture().GetImage().SavePng(Path.Combine(root, "native-" + scenario.Name + ".png"));
                }
                submit.EmitSignal(BaseButton.SignalName.Pressed);
                Check(payload is not null, "Production submission generated a command");
                if (action == "ACTIVATE_ABILITY") Check((string)payload!["sourceObjectId"]! == "B", "Activation uses the offered equipment source");
                else Check(((IEnumerable<string>)payload![action == "PAY_COST" ? "paymentChoiceIds" : "chosenObjectIds"]!).SequenceEqual(scenario.Picks), "Production command preserves decision and order");
                File.WriteAllText(Path.Combine(root, "native-" + scenario.Name + "-command.json"), JsonSerializer.Serialize(payload));
                RemoveChild(bar);
                bar.QueueFree();
            }
            GD.Print(completion ? "SPELL_COMPLETION_INTERACTION_PASS: four production target and decline button paths" : fields ? "FIELD_TRIGGER_INTERACTION_PASS: three production target/decline button paths" : "INSIGHT_SOURCE_INTERACTION_PASS: nine production button paths, activation, pay/decline, private subsets, reversed top order");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
    private static void Check(bool pass, string message) { if (!pass) throw new InvalidOperationException(message); }
}

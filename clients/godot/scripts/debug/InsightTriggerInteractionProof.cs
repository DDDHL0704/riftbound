using System.Reflection;
using System.Text.Json;
using Godot;
using Riftbound.GodotClient.Interaction;
using Riftbound.GodotClient.Ui;

namespace Riftbound.GodotClient.Debug;

public partial class InsightTriggerInteractionProof : Control
{
    public override async void _Ready()
    {
        try
        {
            GetWindow().ContentScaleSize = new Vector2I(1200, 360);
            var root = OS.GetCmdlineUserArgs().First(a => a.StartsWith("--evidence="))[11..];
            var cases = new (string Prompt, string Name, string[] Picks)[]
            {
                ("library", "library-keep", []),
                ("library", "library-recycle", ["D1"]),
                ("visionary", "visionary-keep", []),
                ("visionary", "visionary-recycle", ["D1"]),
                ("visionary", "visionary-all", ["D1", "D2"]),
                ("visionary-order", "visionary-order", ["D2", "D1"])
            };
            foreach (var scenario in cases)
            {
                using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, scenario.Prompt + "-prompt.json")));
                var map = typeof(Main).GetMethod("BuildPromptView", BindingFlags.Static | BindingFlags.NonPublic)!;
                var view = (Godot.Collections.Dictionary)map.Invoke(null, [document.RootElement])!;
                var controller = new PromptInteractionController();
                controller.Load(view);
                var isOrder = scenario.Prompt.EndsWith("-order");
                Check(controller.Current!.CanSubmit == !isOrder, "Only recycling allows an empty submission");
                Check(controller.CurrentStepLabel.Contains(isOrder ? "牌库顶" : "不选则保留"), "Prompt describes the current selection stage");
                Check(!controller.TrySelectObject("D3"), "An unviewed card cannot be selected");
                var bar = GD.Load<PackedScene>("res://scenes/components/ActionBar.tscn").Instantiate<ActionBar>();
                AddChild(bar);
                void Refresh() => bar.ShowSelection(controller.Current!, controller.CurrentChoices, controller.CurrentStepLabel, controller.CurrentStepRequired);
                bar.ShowPrompt(view["message"].AsString(), controller.Actions);
                Refresh();
                bar.ChoiceSelected += (role, id) => { Check(controller.TrySelectChoice(role, id), "Visible choice is accepted"); Refresh(); };
                var candidate = document.RootElement.GetProperty("candidates").EnumerateArray().Single(c => c.GetProperty("action").GetString() == "CHOOSE_CARDS");
                var type = typeof(Main).GetNestedType("PromptSelection", BindingFlags.NonPublic)!;
                var build = typeof(Main).GetMethod("CommandFromTemplate", BindingFlags.Static | BindingFlags.NonPublic)!;
                Dictionary<string, object?>? payload = null;
                bar.SubmitRequested += state =>
                {
                    var selection = Activator.CreateInstance(type, [state.SourceId, state.TargetIds.ToArray(), state.DestinationId, state.Mode, state.OptionalCostIds.ToArray()]);
                    payload = (Dictionary<string, object?>)build.Invoke(null, [candidate, selection, controller.PromptId, controller.SnapshotTick])!;
                };
                foreach (var id in scenario.Picks)
                {
                    if (controller.Current!.TargetIds.Contains(id)) continue;
                    var choices = controller.CurrentChoices.ToArray();
                    var index = Array.FindIndex(choices, c => c.Id == id);
                    Check(index >= 0, "Requested card is offered by the server");
                    bar.GetNode<HBoxContainer>("%StepChoices").GetChildren().OfType<Button>().ElementAt(index).EmitSignal(BaseButton.SignalName.Pressed);
                }
                Check(controller.Current!.TargetIds.SequenceEqual(scenario.Picks), "Click order is retained");
                Check(controller.Current.Summary.Contains(isOrder ? "牌库顶 →" : scenario.Picks.Length == 0 ? "全部保留" : "回收："), "Confirmation describes the actual choice");
                var submit = bar.GetNode<Button>("%SubmitButton");
                Check(!submit.Disabled, "Valid choice enables confirmation");
                if (OS.GetCmdlineUserArgs().Contains("--visual-proof"))
                {
                    for (var frame = 0; frame < 5; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                    GetViewport().GetTexture().GetImage().SavePng(Path.Combine(root, "native-" + scenario.Name + ".png"));
                }
                submit.EmitSignal(BaseButton.SignalName.Pressed);
                Check(payload is not null && ((IEnumerable<string>)payload["chosenObjectIds"]!).SequenceEqual(scenario.Picks), "Production command preserves choice order");
                File.WriteAllText(Path.Combine(root, "native-" + scenario.Name + "-command.json"), JsonSerializer.Serialize(payload));
                RemoveChild(bar);
                bar.QueueFree();
            }
            GD.Print("INSIGHT_TRIGGER_INTERACTION_PASS: six production button paths, optional subsets, reversed top order and commands");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
    private static void Check(bool pass, string message) { if (!pass) throw new InvalidOperationException(message); }
}

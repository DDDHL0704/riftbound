using System.Reflection;
using System.Text.Json;
using Godot;
using Riftbound.GodotClient.Ui;
using Riftbound.GodotClient.Interaction;

namespace Riftbound.GodotClient.Debug;

public partial class DeathObserverInteractionProof : Control
{
    public override void _Ready()
    {
        try {
            var root=OS.GetCmdlineUserArgs().Single(a=>a.StartsWith("--evidence="))[11..];
            foreach(var branch in new[] { "forward","reverse" }) {
                var dir=Path.Combine(root,branch);
                using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(dir,"prompt.json")));
                var view=(Godot.Collections.Dictionary)typeof(Main).GetMethod("BuildPromptView",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[doc.RootElement])!;
                var action=view["actions"].AsGodotArray().Select(v=>v.AsGodotDictionary()).Single(a=>a["action"].AsString()=="ORDER_TRIGGERS");
                var overlay=GD.Load<PackedScene>("res://scenes/overlays/TriggerOrderOverlay.tscn").Instantiate<TriggerOrderOverlay>();AddChild(overlay);
                Check(overlay.ShowPrompt(action,out var reason),reason);
                var rows=overlay.GetNode<VBoxContainer>("%TriggerRows");
                Check(rows.GetChild(0).GetChild<Button>(0).Text.Contains("凶残颚鱼"),"Human-readable captured card label");
                var before=overlay.OrderedTriggerIds.ToArray();
                if(branch=="reverse") rows.GetChild(0).GetNode<Button>("MoveDownButton").EmitSignal(BaseButton.SignalName.Pressed);
                Check(overlay.OrderedTriggerIds.SequenceEqual(branch=="reverse"?Enumerable.Reverse(before):before),"Production move button orders both events");
                Dictionary<string,object?>? command=null;
                overlay.Confirmed+=ids=> { Check(SpecialPromptCommandBuilder.TryBuildOrderTriggersPayload(action,ids,out var payload,out _,out var error),error);
                    payload["promptId"]=doc.RootElement.GetProperty("promptId").GetString();payload["snapshotTick"]=doc.RootElement.GetProperty("snapshotTick").GetInt64();command=payload; };
                overlay.GetNode<Button>("%ConfirmButton").EmitSignal(BaseButton.SignalName.Pressed);
                Check(command is not null,"Production confirm creates command");File.WriteAllText(Path.Combine(dir,"command.json"),JsonSerializer.Serialize(command));overlay.Free();
            }

            foreach (var victim in new[] { "D", "D2" }) {
                var dir = Path.Combine(root, "first-" + victim);
                using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, "prompt.json")));
                var view = (Godot.Collections.Dictionary)typeof(Main).GetMethod("BuildPromptView", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [doc.RootElement])!;
                var controller = new PromptInteractionController(); controller.Load(view);
                var bar = GD.Load<PackedScene>("res://scenes/components/ActionBar.tscn").Instantiate<ActionBar>(); AddChild(bar);
                bar.ActionSelected += id => controller.SelectAction(id); bar.ShowPrompt(view["message"].AsString(), controller.Actions);
                Check(controller.Actions.Single(a => a.Name == "PAY_COST").Label == "选择首次死亡事件", "Specific first-death action label");
                var button = bar.GetNode<HBoxContainer>("%ActionChoices").GetChildren().OfType<Button>().Single(b => b.Text == "选择首次死亡事件");
                button.EmitSignal(BaseButton.SignalName.Pressed);
                var candidate = doc.RootElement.GetProperty("candidates").EnumerateArray().Single(c => c.GetProperty("action").GetString() == "PAY_COST");
                var options = candidate.GetProperty("metadata").GetProperty("paymentChoices").EnumerateArray().ToArray();
                var option = options.Single(o => o.GetProperty("objectIds")[0].GetString() == victim).GetProperty("id").GetString()!;
                Check(controller.TrySelectChoice("target", option), "Select desired death event");
                using var snapshot = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, "snapshot.json")));
                var phase = MatchPhasePresentation.Build(snapshot.RootElement, "P2", id => id);
                Check(phase["title"].AsString() == "首次死亡 · 选择事件", "First death is distinct from replacement");
                bar.ShowSelection(controller.Current!, controller.CurrentChoices, controller.CurrentStepLabel, controller.CurrentStepRequired);
                Dictionary<string, object?>? command = null;
                bar.SubmitRequested += selection => {
                    var type = typeof(Main).GetNestedType("PromptSelection", BindingFlags.NonPublic)!;
                    var state = Activator.CreateInstance(type, [selection.SourceId, selection.TargetIds.ToArray(), selection.DestinationId, selection.Mode, selection.OptionalCostIds.ToArray()]);
                    command = (Dictionary<string, object?>)typeof(Main).GetMethod("CommandFromTemplate", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [candidate, state, controller.PromptId, controller.SnapshotTick])!;
                };
                var submit = bar.GetNode<Button>("%SubmitButton"); Check(!submit.Disabled, "First death can be submitted"); submit.EmitSignal(BaseButton.SignalName.Pressed);
                Check(command is not null, "Production submit creates first-death choice");
                File.WriteAllText(Path.Combine(dir, "command.json"), JsonSerializer.Serialize(command)); bar.Free();
            }
            GD.Print("DEATH_OBSERVER_ORDER_PASS: two ordering and two first-death production commands");GetTree().Quit();
        } catch(Exception e) { GD.PushError(e.ToString());GetTree().Quit(1); }
    }
    private static void Check(bool valid,string message) { if(!valid)throw new InvalidOperationException(message); }
}

using System.Reflection;
using System.Text.Json;
using Godot;
using Riftbound.GodotClient.Ui;
using Riftbound.GodotClient.Interaction;

namespace Riftbound.GodotClient.Debug;

public partial class MovementObserverInteractionProof : Control
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
                Check(rows.GetChildren().Any(row=>row.GetChild<Button>(0).Text.Contains("寻宝猎人")),"Captured hunter label");
                Check(rows.GetChildren().Any(row=>row.GetChild<Button>(0).Text.Contains("后巷酒吧")),"Captured battlefield label");
                var before=overlay.OrderedTriggerIds.ToArray();
                if(branch=="reverse") rows.GetChild(0).GetNode<Button>("MoveDownButton").EmitSignal(BaseButton.SignalName.Pressed);
                Check(overlay.OrderedTriggerIds.SequenceEqual(branch=="reverse"?Enumerable.Reverse(before):before),"Production move button orders both events");
                Dictionary<string,object?>? command=null;
                overlay.Confirmed+=ids=> { Check(SpecialPromptCommandBuilder.TryBuildOrderTriggersPayload(action,ids,out var payload,out _,out var error),error);
                    payload["promptId"]=doc.RootElement.GetProperty("promptId").GetString();payload["snapshotTick"]=doc.RootElement.GetProperty("snapshotTick").GetInt64();command=payload; };
                overlay.GetNode<Button>("%ConfirmButton").EmitSignal(BaseButton.SignalName.Pressed);
                Check(command is not null,"Production confirm creates command");File.WriteAllText(Path.Combine(dir,"command.json"),JsonSerializer.Serialize(command));overlay.Free();
            }

            GD.Print("MOVEMENT_OBSERVER_ORDER_PASS: two mixed movement-trigger production order commands");GetTree().Quit();
        } catch(Exception e) { GD.PushError(e.ToString());GetTree().Quit(1); }
    }
    private static void Check(bool valid,string message) { if(!valid)throw new InvalidOperationException(message); }
}

using System.Reflection;
using System.Text.Json;
using Godot;
using Riftbound.Contracts;
using Riftbound.GodotClient.Ui;

namespace Riftbound.GodotClient.Debug;

public partial class GroupedPlayInteractionProof : Control
{
    public override void _Ready()
    {
        try {
            var root=OS.GetCmdlineUserArgs().Single(a=>a.StartsWith("--evidence="))[11..];
            var json=new JsonSerializerOptions(JsonSerializerDefaults.Web);
            foreach(var branch in new[]{"many-zero"}) {
                var dir=Path.Combine(root,branch);
                using var prompt=JsonDocument.Parse(File.ReadAllText(Path.Combine(dir,"prompt.json")));
                var p=prompt.RootElement;
                using var snapshot=JsonDocument.Parse(File.ReadAllText(Path.Combine(dir,"snapshot.json")));
                var card=snapshot.RootElement.GetProperty("players").GetProperty("P2").GetProperty("objects").GetProperty("D");
                var display=SnapshotCardRef.FromSnapshot("D",card,"P1");
                Check(display.CurrentPower==0,"Production card display uses current power");
                var candidate=p.GetProperty("candidates").EnumerateArray().Single(c=>c.GetProperty("action").GetString()=="PLAY_CARD");
                var overlay=new PlayCardOverlay {TableMode=true}; AddChild(overlay);
                PlayCostPreviewRequestDto? request=null;
                overlay.PreviewRequested+=value=>request=value;
                Check(overlay.Open(candidate,p.GetProperty("promptId").GetString()!,p.GetProperty("snapshotTick").GetInt64(),_=>null,"AOE"),"Open production play composer");
                Check(!overlay.TrySelectTableObject("HIDDEN-OPPONENT-CARD"),"Hidden target is rejected");
                foreach(var id in new[]{"D","D2","D3","D4","D5"}) Check(overlay.TrySelectTableObject(id),"All five zero-power targets are selectable");
                Check(request is not null && request.Command.TargetObjectIds.SequenceEqual(["D","D2","D3","D4","D5"]),"Selected target reaches payment preview");
                File.WriteAllText(Path.Combine(dir,"preview.json"),JsonSerializer.Serialize(request,json));
                var quote=JsonSerializer.Deserialize<PlayCostQuoteDto>(File.ReadAllText(Path.Combine(dir,"quote.json")),json)!;
                overlay.ApplyQuote(quote with {RequestId=request!.RequestId});
                Dictionary<string,object?>? command=null;
                overlay.Confirmed+=value=>{
                    command=value;
                    // Main adds the composer identity before dispatch to the server.
                    command["promptId"]=overlay.PromptId; command["snapshotTick"]=overlay.SnapshotTick;
                };
                var submit=(Button)typeof(PlayCardOverlay).GetField("_confirm",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(overlay)!;
                Check(!submit.Disabled,"Server payment quote enables submit"); submit.EmitSignal(BaseButton.SignalName.Pressed);
                Check(command is not null,"Production submit emits command");
                File.WriteAllText(Path.Combine(dir,"command.json"),JsonSerializer.Serialize(command,json)); overlay.Free();
            }
            GD.Print("GROUPED_PLAY_PASS: five-target production play command"); GetTree().Quit();
        } catch(Exception e) {GD.PushError(e.ToString());GetTree().Quit(1);}
    }
    private static void Check(bool valid,string message) {if(!valid) throw new InvalidOperationException(message);}
}

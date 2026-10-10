using System.Reflection;
using System.Text.Json;
using Godot;
using Riftbound.Contracts;
using Riftbound.GodotClient.Ui;

namespace Riftbound.GodotClient.Debug;

public partial class FaceOffInteractionProof : Control
{
    public override void _Ready()
    {
        try {
            var root=OS.GetCmdlineUserArgs().Single(a=>a.StartsWith("--evidence="))[11..];
            var json=new JsonSerializerOptions(JsonSerializerDefaults.Web);
            foreach(var branch in new[]{"base","other-field"}) {
                var dir=Path.Combine(root,branch);
                using var prompt=JsonDocument.Parse(File.ReadAllText(Path.Combine(dir,"prompt.json")));
                var p=prompt.RootElement;
                using var snapshot=JsonDocument.Parse(File.ReadAllText(Path.Combine(dir,"snapshot.json")));
                var candidate=p.GetProperty("candidates").EnumerateArray().Single(c=>c.GetProperty("action").GetString()=="PLAY_CARD");
                var overlay=new PlayCardOverlay {TableMode=true}; AddChild(overlay);
                PlayCostPreviewRequestDto? request=null;
                overlay.PreviewRequested+=value=>request=value;
                Check(overlay.Open(candidate,p.GetProperty("promptId").GetString()!,p.GetProperty("snapshotTick").GetInt64(),_=>null,"AOE"),"Open production play composer");
                Check(!overlay.TrySelectTableObject("HIDDEN-OPPONENT-CARD"),"Hidden target is rejected");
                Check(!overlay.TrySelectTableObject("D"),"Enemy unit cannot be first target");
                Check(overlay.TrySelectTableObject("A"),"Friendly unit is selectable");
                Check(!overlay.TrySelectTableObject("D"),"Enemy unit cannot replace battlefield target");
                Check(overlay.TrySelectTableObject("BF"),"Battlefield is selectable");
                Check(request is not null && request.Command.TargetObjectIds.SequenceEqual(["A","BF"]),"Selected target reaches payment preview");
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
            GD.Print("FACE_OFF_PASS: two production play commands"); GetTree().Quit();
        } catch(Exception e) {GD.PushError(e.ToString());GetTree().Quit(1);}
    }
    private static void Check(bool valid,string message) {if(!valid) throw new InvalidOperationException(message);}
}

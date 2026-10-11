using System.Reflection;
using System.Text.Json;
using Godot;
using Riftbound.Contracts;
using Riftbound.GodotClient.Ui;
namespace Riftbound.GodotClient.Debug;
public partial class SequentialMovementInteractionProof:Control
{
    public override void _Ready()
    {
        try {
            var root=OS.GetCmdlineUserArgs().Single(a=>a.StartsWith("--evidence="))[11..];var json=new JsonSerializerOptions(JsonSerializerDefaults.Web);
            foreach(var branch in new[]{"base","same","split","echo"}) {
                var dir=Path.Combine(root,branch);using var prompt=JsonDocument.Parse(File.ReadAllText(Path.Combine(dir,"prompt.json")));var p=prompt.RootElement;
                using var selection=JsonDocument.Parse(File.ReadAllText(Path.Combine(dir,"selection.json")));var destinations=selection.RootElement.GetProperty("destinations").EnumerateArray().Select(x=>x.GetString()!).ToArray();
                var candidate=p.GetProperty("candidates").EnumerateArray().Single(c=>c.GetProperty("action").GetString()=="PLAY_CARD");
                var overlay=new PlayCardOverlay {TableMode=true};AddChild(overlay);PlayCostPreviewRequestDto? request=null;overlay.PreviewRequested+=value=>request=value;
                Check(overlay.Open(candidate,p.GetProperty("promptId").GetString()!,p.GetProperty("snapshotTick").GetInt64(),_=>null,"AOE"),"Open real play composer");
                Check(overlay.TrySelectTableObject("A")&&overlay.TrySelectTableObject("D"),"Select ordered friendly and enemy targets");
                var moves=(IEnumerable<(OptionButton Picker,string[] Ids)>)typeof(PlayCardOverlay).GetField("_movementDestinations",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(overlay)!;
                Check(moves.Count()==2,"Two independent movement controls");
                var index=0;foreach(var move in moves){var selected=Array.IndexOf(move.Ids,destinations[index++]);Check(selected>0,"Destination supplied by server");move.Picker.Select(selected);move.Picker.EmitSignal(OptionButton.SignalName.ItemSelected,selected);}
                if(branch=="echo") {
                    var flags=BindingFlags.Instance|BindingFlags.NonPublic;
                    var optional=(Dictionary<string,CheckBox>)typeof(PlayCardOverlay).GetField("_optional",flags)!.GetValue(overlay)!;optional["ECHO"].ButtonPressed=true;
                    var repeats=(System.Collections.IList)typeof(PlayCardOverlay).GetField("_repeats",flags)!.GetValue(overlay)!;Check(repeats.Count==1,"Echo adds an execution");var repeat=repeats[0]!;
                    var targets=(List<(OptionButton Picker,string[] Ids,bool Required)>)repeat.GetType().GetField("Targets")!.GetValue(repeat)!;
                    var targetIds=new[]{"A2","D2"};for(var i=0;i<2;i++){var k=Array.IndexOf(targets[i].Ids,targetIds[i]);Check(k>0,"Repeat target");targets[i].Picker.Select(k);targets[i].Picker.EmitSignal(OptionButton.SignalName.ItemSelected,k);}
                    var repeatMoves=(List<(OptionButton Picker,string[] Ids)>)repeat.GetType().GetField("MovementDestinations")!.GetValue(repeat)!;
                    var nextDestinations=new[]{"BATTLEFIELD:BF","BATTLEFIELD:BF2"};for(var i=0;i<2;i++){var k=Array.IndexOf(repeatMoves[i].Ids,nextDestinations[i]);repeatMoves[i].Picker.Select(k);repeatMoves[i].Picker.EmitSignal(OptionButton.SignalName.ItemSelected,k);}
                    Check(request?.Command.RepeatChoices?[0].MovementDestinations?.SequenceEqual(nextDestinations)==true,"Echo preview keeps independent destinations");
                }
                Check(request is not null&&request.Command.MovementDestinations!.SequenceEqual(destinations),"Preview preserves both destinations");
                File.WriteAllText(Path.Combine(dir,"preview.json"),JsonSerializer.Serialize(request,json));
                var quote=JsonSerializer.Deserialize<PlayCostQuoteDto>(File.ReadAllText(Path.Combine(dir,"quote.json")),json)!;overlay.ApplyQuote(quote with {RequestId=request!.RequestId});
                Dictionary<string,object?>? command=null;overlay.Confirmed+=value=>{command=value;command["promptId"]=overlay.PromptId;command["snapshotTick"]=overlay.SnapshotTick;};
                var submit=(Button)typeof(PlayCardOverlay).GetField("_confirm",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(overlay)!;
                Check(!submit.Disabled,"Server quote enables submission");submit.EmitSignal(BaseButton.SignalName.Pressed);Check(command is not null,"Submit emitted");File.WriteAllText(Path.Combine(dir,"command.json"),JsonSerializer.Serialize(command,json));overlay.Free();
                using var snapshot=JsonDocument.Parse(File.ReadAllText(Path.Combine(dir,"play-snapshot.json")));
                var main=new Main();var chain=(Godot.Collections.Array<Godot.Collections.Dictionary>)typeof(Main).GetMethod("BuildTableChain",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(main,[snapshot.RootElement,"P2",new Dictionary<string,SnapshotCardRef>()])!;
                var detail=chain[0]["detail"].AsString();Check(detail.Contains("第 1 次")&&detail.Contains("第 2 次"),"Opponent sees both movement instructions before responding");if(branch=="base")Check(detail.Contains("我方基地"),"Viewer-specific destination name");if(branch=="echo")Check(detail.Contains("执行 2"),"Second execution is public before response");main.Free();
            }
            GD.Print("SEQUENTIAL_MOVEMENT_PASS: four production dual-destination commands including Echo and chain views");GetTree().Quit();
        }catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(1);}
    }
    private static void Check(bool valid,string message){if(!valid)throw new InvalidOperationException(message);}
}

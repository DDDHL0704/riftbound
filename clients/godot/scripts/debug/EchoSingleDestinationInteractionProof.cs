using System.Reflection;
using System.Text.Json;
using Godot;
using Riftbound.Contracts;
using Riftbound.GodotClient.Ui;
namespace Riftbound.GodotClient.Debug;
public partial class EchoSingleDestinationInteractionProof:Control
{
    public override void _Ready()
    {
        try {
            var root=OS.GetCmdlineUserArgs().Single(a=>a.StartsWith("--evidence="))[11..];
            foreach(var branch in new[]{"rage","tentacles"})Produce(Path.Combine(root,branch));
            GD.Print("ECHO_SINGLE_DESTINATION_PASS: 2 production commands with independent repeat destinations");GetTree().Quit();
        }catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(1);}
    }
    private static object Field(object target,string name)=>target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public)!.GetValue(target)!;
    private void Produce(string dir)
    {
        var json=new JsonSerializerOptions(JsonSerializerDefaults.Web);using var document=JsonDocument.Parse(File.ReadAllText(Path.Combine(dir,"play-prompt.json")));var p=document.RootElement;
        var candidate=p.GetProperty("candidates").EnumerateArray().Single(c=>c.GetProperty("action").GetString()=="PLAY_CARD");
        var overlay=new PlayCardOverlay{TableMode=true};AddChild(overlay);PlayCostPreviewRequestDto? request=null;overlay.PreviewRequested+=r=>request=r;
        Check(overlay.Open(candidate,p.GetProperty("promptId").GetString()!,p.GetProperty("snapshotTick").GetInt64(),_=>null,"AOE"),"Open composer");
        Check(overlay.TrySelectTableDestination("BASE:P2"),"Choose first enemy base");Check(overlay.TrySelectTableObject("D"),"Choose first unit");
        var optional=(Dictionary<string,CheckBox>)Field(overlay,"_optional");optional["ECHO"].ButtonPressed=true;
        var repeats=((System.Collections.IEnumerable)Field(overlay,"_repeats")).Cast<object>().ToArray();Check(repeats.Length==1,"One extra execution");var repeat=repeats[0];
        var targets=(List<(OptionButton Picker,string[] Ids,bool Required)>)Field(repeat,"Targets");
        var index=Array.IndexOf(targets[0].Ids,"D2");Check(index>0,"Server offers second target");targets[0].Picker.Select(index);targets[0].Picker.EmitSignal(OptionButton.SignalName.ItemSelected,index);
        var destination=(OptionButton)Field(repeat,"Destination");destination.EmitSignal(Control.SignalName.FocusEntered);
        Check(overlay.TableDestinations.Contains("BATTLEFIELD:BF2"),"Focused execution exposes its server destinations");
        Check(overlay.TrySelectTableDestination("BATTLEFIELD:BF2"),"Table selects repeat destination");
        Check(request is not null&&request.Command.Destination=="BASE:P2"&&request.Command.RepeatChoices![0].Destination=="BATTLEFIELD:BF2","Preview preserves distinct destinations");
        Check(request!.Command.TargetObjectIds.SequenceEqual(new[]{"D"})&&request.Command.RepeatChoices![0].TargetObjectIds.SequenceEqual(new[]{"D2"}),"Preview preserves distinct targets");
        File.WriteAllText(Path.Combine(dir,"play-preview.json"),JsonSerializer.Serialize(request,json));
        var quote=JsonSerializer.Deserialize<PlayCostQuoteDto>(File.ReadAllText(Path.Combine(dir,"play-quote.json")),json)!;overlay.ApplyQuote(quote with {RequestId=request.RequestId});
        Dictionary<string,object?>? command=null;overlay.Confirmed+=value=>{command=value;command["promptId"]=overlay.PromptId;command["snapshotTick"]=overlay.SnapshotTick;};
        var submit=(Button)Field(overlay,"_confirm");Check(!submit.Disabled,"Quote enables confirmation");submit.EmitSignal(BaseButton.SignalName.Pressed);Check(command is not null,"Production command emitted");
        File.WriteAllText(Path.Combine(dir,"play-command.json"),JsonSerializer.Serialize(command,json));overlay.Free();
    }
    private static void Check(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
}

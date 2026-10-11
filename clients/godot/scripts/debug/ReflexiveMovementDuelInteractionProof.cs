using System.Reflection;
using System.Text.Json;
using Godot;
using Riftbound.GodotClient.Interaction;
using Riftbound.Contracts;
using Riftbound.GodotClient.Ui;
namespace Riftbound.GodotClient.Debug;
public partial class ReflexiveMovementDuelInteractionProof:Control
{
    public override void _Ready()
    {
        try {
            var root=OS.GetCmdlineUserArgs().Single(a=>a.StartsWith("--evidence="))[11..];var count=0;
            foreach(var branch in new[]{"plain","ward-pay","ward-decline"}) { ProducePlay(Path.Combine(root,branch));count++; }
            foreach(var branch in new[]{"plain","ward-pay","ward-decline"})
            foreach(var name in branch=="plain"?new[]{"target"}:new[]{"target","payment"}) {
                var dir=Path.Combine(root,branch);using var prompt=JsonDocument.Parse(File.ReadAllText(Path.Combine(dir,name+"-prompt.json")));
                var view=(Godot.Collections.Dictionary)typeof(Main).GetMethod("BuildPromptView",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[prompt.RootElement])!;
                var controller=new PromptInteractionController();controller.Load(view);
                var bar=GD.Load<PackedScene>("res://scenes/components/ActionBar.tscn").Instantiate<ActionBar>();AddChild(bar);
                var action=name=="target"?"CHOOSE_CARDS":"PAY_COST";bar.ActionSelected+=id=>controller.SelectAction(id);bar.ShowPrompt(view["message"].AsString(),controller.Actions);
                var label=controller.Actions.Single(a=>a.Name==action).Label;
                bar.GetNode<HBoxContainer>("%ActionChoices").GetChildren().OfType<Button>().Single(b=>b.Text==label).EmitSignal(BaseButton.SignalName.Pressed);
                var id=name=="target"?"E":branch=="ward-pay"?"PAY":"DECLINE";
                if(!controller.Current!.TargetIds.Contains(id)) Check(controller.TrySelectChoice("target",id),"Server offers the selected target or payment");
                Check(controller.Current!.TargetIds.SequenceEqual(new[]{id}),"Selection retained");
                bar.ShowSelection(controller.Current,controller.CurrentChoices,controller.CurrentStepLabel,controller.CurrentStepRequired);
                var candidate=prompt.RootElement.GetProperty("candidates").EnumerateArray().Single(c=>c.GetProperty("action").GetString()==action);
                Dictionary<string,object?>? command=null;
                bar.SubmitRequested+=selection=> {
                    var type=typeof(Main).GetNestedType("PromptSelection",BindingFlags.NonPublic)!;
                    var selected=Activator.CreateInstance(type,[selection.SourceId,selection.TargetIds.ToArray(),selection.DestinationId,selection.Mode,selection.OptionalCostIds.ToArray()]);
                    command=(Dictionary<string,object?>)typeof(Main).GetMethod("CommandFromTemplate",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[candidate,selected,controller.PromptId,controller.SnapshotTick])!;
                };
                var submit=bar.GetNode<Button>("%SubmitButton");Check(!submit.Disabled,"Production selection is submittable");submit.EmitSignal(BaseButton.SignalName.Pressed);
                Check(command is not null,"Production submit produced command");File.WriteAllText(Path.Combine(dir,name+"-command.json"),JsonSerializer.Serialize(command));bar.Free();count++;
            }
            GD.Print($"REFLEXIVE_MOVEMENT_DUEL_PASS: {count} production play, target and Ward commands");GetTree().Quit();
        } catch(Exception e) {GD.PushError(e.ToString());GetTree().Quit(1);}
    }
    private void ProducePlay(string dir)
    {
        var json=new JsonSerializerOptions(JsonSerializerDefaults.Web);
        using var prompt=JsonDocument.Parse(File.ReadAllText(Path.Combine(dir,"play-prompt.json")));var p=prompt.RootElement;
        var candidate=p.GetProperty("candidates").EnumerateArray().Single(c=>c.GetProperty("action").GetString()=="PLAY_CARD");
        var overlay=new PlayCardOverlay {TableMode=true};AddChild(overlay);PlayCostPreviewRequestDto? request=null;overlay.PreviewRequested+=value=>request=value;
        Check(overlay.Open(candidate,p.GetProperty("promptId").GetString()!,p.GetProperty("snapshotTick").GetInt64(),_=>null,"AOE"),"Open move spell composer");
        var destinations=(string[])typeof(PlayCardOverlay).GetField("_destinations",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(overlay)!;
        var picker=(OptionButton)typeof(PlayCardOverlay).GetField("_destination",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(overlay)!;
        var index=Array.IndexOf(destinations,"BASE:P2");Check(index>=0,"Server offers enemy base");picker.Select(index);picker.EmitSignal(OptionButton.SignalName.ItemSelected,index);
        Check(overlay.TrySelectTableObject("D"),"First enemy can be selected");
        Check(request is not null && request.Command.TargetObjectIds.SequenceEqual(new[]{"D"}) && request.Command.Destination=="BASE:P2","Only moved unit and destination declared");
        File.WriteAllText(Path.Combine(dir,"play-preview.json"),JsonSerializer.Serialize(request,json));
        var quote=JsonSerializer.Deserialize<PlayCostQuoteDto>(File.ReadAllText(Path.Combine(dir,"play-quote.json")),json)!;overlay.ApplyQuote(quote with {RequestId=request!.RequestId});
        Dictionary<string,object?>? command=null;overlay.Confirmed+=value=>{command=value;command["promptId"]=overlay.PromptId;command["snapshotTick"]=overlay.SnapshotTick;};
        var submit=(Button)typeof(PlayCardOverlay).GetField("_confirm",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(overlay)!;
        Check(!submit.Disabled,"Server quote enables play");submit.EmitSignal(BaseButton.SignalName.Pressed);Check(command is not null,"Production play emitted");
        File.WriteAllText(Path.Combine(dir,"play-command.json"),JsonSerializer.Serialize(command,json));overlay.Free();
    }
    private static void Check(bool condition,string message) {if(!condition)throw new InvalidOperationException(message);}
}

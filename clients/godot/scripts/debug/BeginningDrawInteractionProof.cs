using System.Reflection;
using System.Text.Json;
using Godot;
using Riftbound.Contracts;
using Riftbound.GodotClient.Interaction;
using Riftbound.GodotClient.Ui;
namespace Riftbound.GodotClient.Debug;
public partial class BeginningDrawInteractionProof : Control
{
    public override void _Ready()
    {
        try {
            var root = OS.GetCmdlineUserArgs().Single(a=>a.StartsWith("--evidence="))[11..];
            foreach (var name in new[]{"pass-reaction-1","pass-reaction-2","pass-jinx-1","pass-jinx-2"}) ProducePass(root,name);
            ProduceReaction(root);
            GD.Print("BEGINNING_DRAW_PASS: 5 production response and reaction commands"); GetTree().Quit();
        } catch(Exception e) { GD.PushError(e.ToString());GetTree().Quit(1); }
    }
    private void ProducePass(string root,string name)
    {
        using var document=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,name+"-prompt.json")));
        var prompt=document.RootElement;
        var view=(Godot.Collections.Dictionary)typeof(Main).GetMethod("BuildPromptView",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[prompt])!;
        var controller=new PromptInteractionController();controller.Load(view);
        var bar=GD.Load<PackedScene>("res://scenes/components/ActionBar.tscn").Instantiate<ActionBar>();AddChild(bar);
        bar.ActionSelected+=id=>controller.SelectAction(id);bar.ShowPrompt(view["message"].AsString(),controller.Actions);
        var primary=(Button)typeof(ActionBar).GetField("_primaryAction",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(bar)!;
        Check(!primary.Disabled && primary.Text=="不响应 · 让过","Production primary action is response pass");
        primary.EmitSignal(BaseButton.SignalName.Pressed);
        bar.ShowSelection(controller.Current ?? throw new InvalidOperationException("Missing pass selection"),controller.CurrentChoices,controller.CurrentStepLabel,controller.CurrentStepRequired);
        var candidate=prompt.GetProperty("candidates").EnumerateArray().Single(c=>c.GetProperty("action").GetString()=="PASS_PRIORITY");
        Dictionary<string,object?>? command=null;
        bar.SubmitRequested+=selection=>{
            var type=typeof(Main).GetNestedType("PromptSelection",BindingFlags.NonPublic)!;
            var selected=Activator.CreateInstance(type,[selection.SourceId,selection.TargetIds.ToArray(),selection.DestinationId,selection.Mode,selection.OptionalCostIds.ToArray()]);
            command=(Dictionary<string,object?>)typeof(Main).GetMethod("CommandFromTemplate",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[candidate,selected,controller.PromptId,controller.SnapshotTick])!;
        };
        var submit=bar.GetNode<Button>("%SubmitButton");Check(!submit.Disabled,"Response pass is available");submit.EmitSignal(BaseButton.SignalName.Pressed);
        Check(command is not null,"Production pass emitted");File.WriteAllText(Path.Combine(root,name+"-command.json"),JsonSerializer.Serialize(command));bar.Free();
    }
    private void ProduceReaction(string root)
    {
        var json=new JsonSerializerOptions(JsonSerializerDefaults.Web);
        using var document=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"reaction-prompt.json")));var p=document.RootElement;
        var candidate=p.GetProperty("candidates").EnumerateArray().Single(c=>c.GetProperty("action").GetString()=="PLAY_CARD");
        var overlay=new PlayCardOverlay {TableMode=true};AddChild(overlay);PlayCostPreviewRequestDto? request=null;overlay.PreviewRequested+=value=>request=value;
        Check(overlay.Open(candidate,p.GetProperty("promptId").GetString()!,p.GetProperty("snapshotTick").GetInt64(),_=>null,"FLASH"),"Open reaction during turn start");
        Check(overlay.TrySelectTableObject("E"),"Select friendly unit for the response spell");
        Check(request is not null && request.Command.TargetObjectIds.SequenceEqual(new[]{"E"}),"Reaction preview has actual target");
        File.WriteAllText(Path.Combine(root,"reaction-preview.json"),JsonSerializer.Serialize(request,json));
        var quote=JsonSerializer.Deserialize<PlayCostQuoteDto>(File.ReadAllText(Path.Combine(root,"reaction-quote.json")),json)!;overlay.ApplyQuote(quote with {RequestId=request!.RequestId});
        Dictionary<string,object?>? command=null;overlay.Confirmed+=value=>{command=value;command["promptId"]=overlay.PromptId;command["snapshotTick"]=overlay.SnapshotTick;};
        var submit=(Button)typeof(PlayCardOverlay).GetField("_confirm",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(overlay)!;
        Check(!submit.Disabled,"Reaction quote enables submit");submit.EmitSignal(BaseButton.SignalName.Pressed);Check(command is not null,"Production reaction emitted");
        File.WriteAllText(Path.Combine(root,"reaction-command.json"),JsonSerializer.Serialize(command,json));overlay.Free();
    }
    private static void Check(bool value,string message) {if(!value)throw new InvalidOperationException(message);}
}

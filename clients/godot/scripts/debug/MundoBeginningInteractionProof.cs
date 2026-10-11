using System.Reflection;
using System.Text.Json;
using Godot;
using Riftbound.Contracts;
using Riftbound.GodotClient.Interaction;
using Riftbound.GodotClient.Ui;
namespace Riftbound.GodotClient.Debug;
public partial class MundoBeginningInteractionProof : Control
{
    public override void _Ready()
    {
        try {
            var root = OS.GetCmdlineUserArgs().Single(a=>a.StartsWith("--evidence="))[11..];
            foreach (var name in new[]{"pass-1","pass-2"}) ProducePass(root,name);
            ProduceRecycle(root);
            GD.Print("MUNDO_BEGINNING_PASS: 3 production response and recycle commands"); GetTree().Quit();
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
    private void ProduceRecycle(string root)
    {
                var dir=root;var name="recycle";using var prompt=JsonDocument.Parse(File.ReadAllText(Path.Combine(dir,name+"-prompt.json")));
                var view=(Godot.Collections.Dictionary)typeof(Main).GetMethod("BuildPromptView",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[prompt.RootElement])!;
                var controller=new PromptInteractionController();controller.Load(view);
                var bar=GD.Load<PackedScene>("res://scenes/components/ActionBar.tscn").Instantiate<ActionBar>();AddChild(bar);
                var action="CHOOSE_CARDS";bar.ActionSelected+=id=>controller.SelectAction(id);bar.ShowPrompt(view["message"].AsString(),controller.Actions);
                var label=controller.Actions.Single(a=>a.Name==action).Label;
                bar.GetNode<HBoxContainer>("%ActionChoices").GetChildren().OfType<Button>().Single(b=>b.Text==label).EmitSignal(BaseButton.SignalName.Pressed);
                var ids=new[]{"G2","G4","G5"};
                foreach(var id in ids)if(!controller.Current!.TargetIds.Contains(id)) Check(controller.TrySelectChoice("target",id),"Server offers the selected target or payment");
                Check(controller.Current!.TargetIds.SequenceEqual(ids),"Selection retained");
                bar.ShowSelection(controller.Current,controller.CurrentChoices,controller.CurrentStepLabel,controller.CurrentStepRequired);
                var candidate=prompt.RootElement.GetProperty("candidates").EnumerateArray().Single(c=>c.GetProperty("action").GetString()==action);
                Dictionary<string,object?>? command=null;
                bar.SubmitRequested+=selection=> {
                    var type=typeof(Main).GetNestedType("PromptSelection",BindingFlags.NonPublic)!;
                    var selected=Activator.CreateInstance(type,[selection.SourceId,selection.TargetIds.ToArray(),selection.DestinationId,selection.Mode,selection.OptionalCostIds.ToArray()]);
                    command=(Dictionary<string,object?>)typeof(Main).GetMethod("CommandFromTemplate",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[candidate,selected,controller.PromptId,controller.SnapshotTick])!;
                };
                var submit=bar.GetNode<Button>("%SubmitButton");Check(!submit.Disabled,"Production selection is submittable");submit.EmitSignal(BaseButton.SignalName.Pressed);
                Check(command is not null,"Production submit produced command");File.WriteAllText(Path.Combine(dir,name+"-command.json"),JsonSerializer.Serialize(command));bar.Free();
    }
    private static void Check(bool value,string message) {if(!value)throw new InvalidOperationException(message);}
}

using System.Reflection;
using System.Text.Json;
using Godot;
using Riftbound.GodotClient.Interaction;
using Riftbound.Contracts;
using Riftbound.GodotClient.Ui;
namespace Riftbound.GodotClient.Debug;
public partial class ArmamentMightInteractionProof:Control
{
    public override void _Ready()
    {
        try {
            var root=OS.GetCmdlineUserArgs().Single(a=>a.StartsWith("--evidence="))[11..];var count=0;
            foreach(var branch in new[]{"long-sword","world-atlas"})
            {
                var name="assemble";
                var dir=Path.Combine(root,branch);using var prompt=JsonDocument.Parse(File.ReadAllText(Path.Combine(dir,name+"-prompt.json")));
                var view=(Godot.Collections.Dictionary)typeof(Main).GetMethod("BuildPromptView",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[prompt.RootElement])!;
                var controller=new PromptInteractionController();controller.Load(view);
                var bar=GD.Load<PackedScene>("res://scenes/components/ActionBar.tscn").Instantiate<ActionBar>();AddChild(bar);
                var action="ASSEMBLE_EQUIPMENT";bar.ActionSelected+=id=>controller.SelectAction(id);bar.ShowPrompt(view["message"].AsString(),controller.Actions);
                var label=controller.Actions.Single(a=>a.Name==action).Label;
                bar.GetNode<HBoxContainer>("%ActionChoices").GetChildren().OfType<Button>().Single(b=>b.Text==label).EmitSignal(BaseButton.SignalName.Pressed);
                if(controller.Current!.SourceId!="GEAR") Check(controller.TrySelectChoice("source","GEAR"),"Select server armament source");
                var ids=new[]{"HOST"};
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
                Check(command is not null,"Production submit produced command");File.WriteAllText(Path.Combine(dir,name+"-command.json"),JsonSerializer.Serialize(command));bar.Free();count++;
            }
            GD.Print($"ARMAMENT_MIGHT_PASS: {count} production assemble commands");GetTree().Quit();
        } catch(Exception e) {GD.PushError(e.ToString());GetTree().Quit(1);}
    }
    private static void Check(bool condition,string message) {if(!condition)throw new InvalidOperationException(message);}
}

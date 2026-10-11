using System.Reflection;
using System.Text.Json;
using Godot;
using Riftbound.GodotClient.Interaction;
using Riftbound.GodotClient.Ui;
namespace Riftbound.GodotClient.Debug;
public partial class AttachmentObserverInteractionProof:Control
{
    public override void _Ready()
    {
        try {
            var root=OS.GetCmdlineUserArgs().Single(a=>a.StartsWith("--evidence="))[11..];var count=0;
            foreach(var name in new[]{"assemble","equip-pass-1","equip-pass-2","pay","draw-pass-1","draw-pass-2"}) {
                using var document=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,name+"-prompt.json")));var prompt=document.RootElement;
                var view=(Godot.Collections.Dictionary)typeof(Main).GetMethod("BuildPromptView",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[prompt])!;
                var controller=new PromptInteractionController();controller.Load(view);
                var bar=GD.Load<PackedScene>("res://scenes/components/ActionBar.tscn").Instantiate<ActionBar>();AddChild(bar);
                bar.ActionSelected+=id=>controller.SelectAction(id);bar.ShowPrompt(view["message"].AsString(),controller.Actions);
                var action=name=="assemble"?"ASSEMBLE_EQUIPMENT":name=="pay"?"PAY_COST":"PASS_PRIORITY";
                if(action=="PASS_PRIORITY") {
                    var primary=(Button)typeof(ActionBar).GetField("_primaryAction",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(bar)!;
                    Check(!primary.Disabled && primary.Text=="不响应 · 让过","Production pass is enabled");primary.EmitSignal(BaseButton.SignalName.Pressed);
                } else {
                    var label=controller.Actions.Single(a=>a.Name==action).Label;
                    bar.GetNode<HBoxContainer>("%ActionChoices").GetChildren().OfType<Button>().Single(b=>b.Text==label).EmitSignal(BaseButton.SignalName.Pressed);
                    if(action=="ASSEMBLE_EQUIPMENT") {
                        if(controller.Current!.SourceId!="GEAR")Check(controller.TrySelectChoice("source","GEAR"),"Armament source");
                        if(!controller.Current!.TargetIds.Contains("HOST"))Check(controller.TrySelectChoice("target","HOST"),"Host selection");
                    } else {
                        Check(!controller.Current!.CanSubmit,"Payment requires an explicit decision");
                        Check(controller.TrySelectChoice("target","PAY"),"Confirm shared trigger payment");
                        Check(controller.Current.TargetIds.SequenceEqual(new[]{"PAY"}),"Payment choice retained");
                    }
                }
                bar.ShowSelection(controller.Current!,controller.CurrentChoices,controller.CurrentStepLabel,controller.CurrentStepRequired);
                var candidate=prompt.GetProperty("candidates").EnumerateArray().Single(c=>c.GetProperty("action").GetString()==action);
                Dictionary<string,object?>? command=null;
                bar.SubmitRequested+=selection=>{
                    var type=typeof(Main).GetNestedType("PromptSelection",BindingFlags.NonPublic)!;
                    var selected=Activator.CreateInstance(type,[selection.SourceId,selection.TargetIds.ToArray(),selection.DestinationId,selection.Mode,selection.OptionalCostIds.ToArray()]);
                    command=(Dictionary<string,object?>)typeof(Main).GetMethod("CommandFromTemplate",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[candidate,selected,controller.PromptId,controller.SnapshotTick])!;
                };
                var submit=bar.GetNode<Button>("%SubmitButton");Check(!submit.Disabled,"Production command is submittable");submit.EmitSignal(BaseButton.SignalName.Pressed);
                Check(command is not null,"Production submit emitted");File.WriteAllText(Path.Combine(root,name+"-command.json"),JsonSerializer.Serialize(command));bar.Free();count++;
            }
            GD.Print($"ATTACHMENT_OBSERVER_PASS: {count} production equip, payment and response commands");GetTree().Quit();
        }catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(1);}
    }
    private static void Check(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
}

using System.Reflection;
using System.Text.Json;
using Godot;
using Riftbound.GodotClient.Interaction;
using Riftbound.GodotClient.Ui;

namespace Riftbound.GodotClient.Debug;

public partial class TokenReplacementInteractionProof : Control
{
    public override void _Ready()
    {
        try
        {
            var root=OS.GetCmdlineUserArgs().Single(a=>a.StartsWith("--evidence="))[11..];
            foreach(var branch in new[]{"accept","decline"})
            {
                var dir=Path.Combine(root,branch);
                using var prompt=JsonDocument.Parse(File.ReadAllText(Path.Combine(dir,"prompt.json")));
                var view=(Godot.Collections.Dictionary)typeof(Main).GetMethod("BuildPromptView",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[prompt.RootElement])!;
                var controller=new PromptInteractionController();controller.Load(view);
                var bar=GD.Load<PackedScene>("res://scenes/components/ActionBar.tscn").Instantiate<ActionBar>();AddChild(bar);
                bar.ActionSelected+=action=>controller.SelectAction(action);bar.ShowPrompt(view["message"].AsString(),controller.Actions);
                var label=controller.Actions.Single(a=>a.Name=="CHOOSE_CARDS").Label;
                Check(label=="确认指示物替换","Server provides explicit replacement action");
                bar.GetNode<HBoxContainer>("%ActionChoices").GetChildren().OfType<Button>().Single(b=>b.Text==label).EmitSignal(BaseButton.SignalName.Pressed);
                Check(!controller.TrySelectObject("TARGET"),"Only server-listed replacement sources can be selected");
                if(branch=="accept")Check(controller.TrySelectObject("Z2"),"Player can apply the second source first");
                Check(controller.Current!.Summary.Contains(branch=="accept"?"多打出一个复制体":"保留未使用"),"Acceptance and decline consequences are visible");
                bar.ShowSelection(controller.Current,controller.CurrentChoices,controller.CurrentStepLabel,controller.CurrentStepRequired);
                var candidate=prompt.RootElement.GetProperty("candidates").EnumerateArray().Single(c=>c.GetProperty("action").GetString()=="CHOOSE_CARDS");
                Dictionary<string,object?>? command=null;
                bar.SubmitRequested+=selection=>
                {
                    var type=typeof(Main).GetNestedType("PromptSelection",BindingFlags.NonPublic)!;
                    var state=Activator.CreateInstance(type,[selection.SourceId,selection.TargetIds.ToArray(),selection.DestinationId,selection.Mode,selection.OptionalCostIds.ToArray()]);
                    command=(Dictionary<string,object?>)typeof(Main).GetMethod("CommandFromTemplate",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[candidate,state,controller.PromptId,controller.SnapshotTick])!;
                };
                var submit=bar.GetNode<Button>("%SubmitButton");Check(!submit.Disabled,"Both optional paths submit");submit.EmitSignal(BaseButton.SignalName.Pressed);
                Check(command is not null,"Production button creates the intent");
                File.WriteAllText(Path.Combine(dir,"native-command.json"),JsonSerializer.Serialize(command));
                bar.Free();
            }
            GD.Print("TOKEN_REPLACEMENT_INTERACTION_PASS: chosen source order, explicit decline, production commands");GetTree().Quit();
        }
        catch(Exception error){GD.PushError(error.ToString());GetTree().Quit(1);}
    }
    private static void Check(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
}

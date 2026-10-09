using System.Reflection;
using System.Text.Json;
using Godot;
using Riftbound.GodotClient.Interaction;
using Riftbound.GodotClient.Ui;

namespace Riftbound.GodotClient.Debug;

public partial class TokenCreationInteractionProof : Control
{
    public override void _Ready()
    {
        try
        {
            var root=OS.GetCmdlineUserArgs().Single(a=>a.StartsWith("--evidence="))[11..];
            foreach(var step in new[]{("accept", "cost", "H2"), ("decline", "cost", ""), ("accept", "copy", "UNIT")})
            {
                var (branch, stage, selected) = step;
                var dir=Path.Combine(root,branch);
                using var prompt=JsonDocument.Parse(File.ReadAllText(Path.Combine(dir,stage + "-prompt.json")));
                var view=(Godot.Collections.Dictionary)typeof(Main).GetMethod("BuildPromptView",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[prompt.RootElement])!;
                var controller=new PromptInteractionController();controller.Load(view);
                var bar=GD.Load<PackedScene>("res://scenes/components/ActionBar.tscn").Instantiate<ActionBar>();AddChild(bar);
                bar.ActionSelected+=action=>controller.SelectAction(action);bar.ShowPrompt(view["message"].AsString(),controller.Actions);
                var label=controller.Actions.Single(a=>a.Name=="CHOOSE_CARDS").Label;
                Check(label == (stage == "cost" ? "确认触发费用" : "确认触发技能"), "Server distinguishes cost and copy stages");
                bar.GetNode<HBoxContainer>("%ActionChoices").GetChildren().OfType<Button>().Single(b=>b.Text==label).EmitSignal(BaseButton.SignalName.Pressed);
                Check(!controller.TrySelectObject(stage == "cost" ? "UNIT" : "Z1"), "Only server-listed choices at this stage can be selected");
                if (selected.Length > 0) Check(controller.Current!.TargetIds.Contains(selected) || controller.TrySelectObject(selected), "Player selection or unique mandatory choice matches the intended card");
                Check(controller.Current!.Summary.Contains(stage == "copy" ? "技能目标" : branch == "accept" ? "进场后再选复制对象" : "不弃牌、不横置传奇"), "Stage and consequences are explicit");
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
                File.WriteAllText(Path.Combine(dir,stage + "-command.json"),JsonSerializer.Serialize(command));
                bar.Free();
            }
            GD.Print("TOKEN_CREATION_INTERACTION_PASS: explicit discard/exhaust cost, decline, later copy target, production commands");GetTree().Quit();
        }
        catch(Exception error){GD.PushError(error.ToString());GetTree().Quit(1);}
    }
    private static void Check(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
}

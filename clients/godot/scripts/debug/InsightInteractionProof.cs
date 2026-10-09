using System.Reflection;
using System.Text.Json;
using Godot;
using Riftbound.GodotClient.Interaction;
using Riftbound.GodotClient.Ui;

namespace Riftbound.GodotClient.Debug;

public partial class InsightInteractionProof : Control
{
    public override async void _Ready()
    {
        try
        {
            GetWindow().ContentScaleSize=new Vector2I(1200,360);
            var root=OS.GetCmdlineUserArgs().First(a=>a.StartsWith("--evidence="))[11..];
            foreach(var scenario in new[]{"abandon","eclipse"})
            {
                using var document=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,scenario+"-prompt.json")));
                var map=typeof(Main).GetMethod("BuildPromptView",BindingFlags.Static|BindingFlags.NonPublic)!;
                var view=(Godot.Collections.Dictionary)map.Invoke(null,[document.RootElement])!;
                Check(view["message"].AsString().Contains("仅你可见"),"Looked card is explicitly private");
                var controller=new PromptInteractionController();controller.Load(view);
                Check(controller.Current is {CanSubmit:true},"Keeping the card needs no forced selection");
                Check(controller.CurrentStepLabel.Contains("不选则保留"),"Choice explains keep versus recycle");
                Check(!controller.TrySelectObject("D2"),"An unviewed deck card cannot be chosen");
                var bar=GD.Load<PackedScene>("res://scenes/components/ActionBar.tscn").Instantiate<ActionBar>();AddChild(bar);
                void Refresh()=>bar.ShowSelection(controller.Current!,controller.CurrentChoices,controller.CurrentStepLabel,controller.CurrentStepRequired);
                bar.ShowPrompt(view["message"].AsString(),controller.Actions);Refresh();
                bar.ChoiceSelected+=(role,id)=>{Check(controller.TrySelectChoice(role,id),"Visible choice is accepted");Refresh();};
                var candidate=document.RootElement.GetProperty("candidates").EnumerateArray().Single(c=>c.GetProperty("action").GetString()=="CHOOSE_CARDS");
                var type=typeof(Main).GetNestedType("PromptSelection",BindingFlags.NonPublic)!;
                var build=typeof(Main).GetMethod("CommandFromTemplate",BindingFlags.Static|BindingFlags.NonPublic)!;
                Dictionary<string,object?>? payload=null;
                bar.SubmitRequested+=state=>{
                    var selection=Activator.CreateInstance(type,[state.SourceId,state.TargetIds.ToArray(),state.DestinationId,state.Mode,state.OptionalCostIds.ToArray()]);
                    payload=(Dictionary<string,object?>)build.Invoke(null,[candidate,selection,controller.PromptId,controller.SnapshotTick])!;
                };
                var submit=bar.GetNode<Button>("%SubmitButton");
                if(OS.GetCmdlineUserArgs().Contains("--visual-proof"))
                {
                    for(var frame=0;frame<5;frame++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
                    await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                    GetViewport().GetTexture().GetImage().SavePng(Path.Combine(root,"native-"+scenario+"-keep.png"));
                }
                submit.EmitSignal(BaseButton.SignalName.Pressed);
                Check(payload is not null && !((IEnumerable<string>)payload["chosenObjectIds"]!).Any(),"Keep sends an empty choice");
                File.WriteAllText(Path.Combine(root,"native-"+scenario+"-keep-command.json"),JsonSerializer.Serialize(payload));
                var choose=bar.GetNode<HBoxContainer>("%StepChoices").GetChildren().OfType<Button>().First();
                choose.EmitSignal(BaseButton.SignalName.Pressed);
                Check(controller.Current!.TargetIds.SequenceEqual(["D1"]),"Recycling selects exactly the viewed card");
                for(var frame=0;frame<5;frame++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
                if(OS.GetCmdlineUserArgs().Contains("--visual-proof"))
                {
                    await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                    GetViewport().GetTexture().GetImage().SavePng(Path.Combine(root,"native-"+scenario+".png"));
                }
                submit.EmitSignal(BaseButton.SignalName.Pressed);
                Check(((IEnumerable<string>)payload!["chosenObjectIds"]!).SequenceEqual(["D1"]),"Confirmation uses the chosen card");
                File.WriteAllText(Path.Combine(root,"native-"+scenario+"-command.json"),JsonSerializer.Serialize(payload));
                controller.Load(new Godot.Collections.Dictionary{["promptId"]="next",["snapshotTick"]=999L,["actions"]=new Godot.Collections.Array<Godot.Collections.Dictionary>()});
                Check(controller.Current is null,"A new prompt clears the old private choice");
                RemoveChild(bar);bar.QueueFree();
            }
            GD.Print("INSIGHT_INTERACTION_PASS: private prompt, keep, recycle, production buttons and stale-selection clearing");GetTree().Quit();
        }
        catch(Exception error){GD.PushError(error.ToString());GetTree().Quit(1);}
    }
    private static void Check(bool pass,string message){if(!pass)throw new InvalidOperationException(message);}
}

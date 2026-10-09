using System.Reflection;
using System.Text.Json;
using Godot;
using Riftbound.Contracts;
using Riftbound.GodotClient.Interaction;
using Riftbound.GodotClient.Ui;
namespace Riftbound.GodotClient.Debug;

public partial class MechanicalRecastInteractionProof : Control
{
    public override async void _Ready()
    {
        try
        {
            GetWindow().Size=new Vector2I(1000,850);GetWindow().ContentScaleSize=new Vector2I(1000,850);
            var root=OS.GetCmdlineUserArgs().First(a=>a.StartsWith("--evidence="))[11..];
            var json=new JsonSerializerOptions(JsonSerializerDefaults.Web);
            foreach(var branch in new[]{"choose-unit","play-mech","decline"})
            {
                var dir=Path.Combine(root,branch);
                using var prompt=JsonDocument.Parse(File.ReadAllText(Path.Combine(dir,"prompt.json")));
                Dictionary<string,object?>? command=null;Control component;
                if(branch!="play-mech")
                {
                    var view=(Godot.Collections.Dictionary)typeof(Main).GetMethod("BuildPromptView",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[prompt.RootElement])!;
                    var controller=new PromptInteractionController();controller.Load(view);
                    var bar=GD.Load<PackedScene>("res://scenes/components/ActionBar.tscn").Instantiate<ActionBar>();AddChild(bar);component=bar;
                    bar.ShowPrompt(view["message"].AsString(),controller.Actions);
                    bar.ActionSelected+=action=>{Check(controller.SelectAction(action),"Decline action selectable");bar.ShowSelection(controller.Current!,controller.CurrentChoices,controller.CurrentStepLabel,controller.CurrentStepRequired);};
                    var label=controller.Actions.Single(a=>a.Name=="CHOOSE_CARDS").Label;
                    bar.GetNode<HBoxContainer>("%ActionChoices").GetChildren().OfType<Button>().Single(b=>b.Text==label).EmitSignal(BaseButton.SignalName.Pressed);
                    if(branch=="choose-unit")Check(controller.TrySelectObject("B"),"Player chooses second unit on table");
                    Check(controller.Current!.Summary.Contains(branch=="choose-unit"?"支付回收费用":"放弃回收与再次打出"),"Cost or decline is explicit");
                    bar.ShowSelection(controller.Current!,controller.CurrentChoices,controller.CurrentStepLabel,controller.CurrentStepRequired);
                    var candidate=prompt.RootElement.GetProperty("candidates").EnumerateArray().Single(c=>c.GetProperty("action").GetString()=="CHOOSE_CARDS");
                    bar.SubmitRequested+=selection=>
                    {
                        var type=typeof(Main).GetNestedType("PromptSelection",BindingFlags.NonPublic)!;
                        var state=Activator.CreateInstance(type,[selection.SourceId,selection.TargetIds.ToArray(),selection.DestinationId,selection.Mode,selection.OptionalCostIds.ToArray()]);
                        command=(Dictionary<string,object?>)typeof(Main).GetMethod("CommandFromTemplate",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[candidate,state,controller.PromptId,controller.SnapshotTick])!;
                    };
                    var submit=bar.GetNode<Button>("%SubmitButton");Check(!submit.Disabled,"Decline can submit");submit.EmitSignal(BaseButton.SignalName.Pressed);
                }
                else
                {
                    using var candidate=JsonDocument.Parse(File.ReadAllText(Path.Combine(dir,"candidate.json")));
                    var quote=JsonSerializer.Deserialize<PlayCostQuoteDto>(File.ReadAllText(Path.Combine(dir,"quote.json")),json)!;
                    var overlay=new PlayCardOverlay{TableMode=true};AddChild(overlay);component=overlay;
                    PlayCostPreviewRequestDto? request=null;overlay.PreviewRequested+=value=>request=value;overlay.Confirmed+=value=>command=value;
                    Check(overlay.Open(candidate.RootElement,quote.PromptId,quote.SnapshotTick,id=>new Godot.Collections.Dictionary {
                        ["visible"]=true,["objectId"]=id,["cardName"]=id=="BASE"?"基地":"进步荣光",["cardNo"]=id=="BASE"?"":"SFD·075/221",["imagePath"]="",["zone"]=id=="F"?"BASE":"GRAVEYARD",["owner"]="self"}),"Authoritative graveyard candidate opens");
                    var expectedSource="MECH2";
                    var requirements=candidate.RootElement.GetProperty("metadata").GetProperty("sourceRequirements").EnumerateArray().ToArray();
                    var index=Array.FindIndex(requirements,r=>r.GetProperty("sourceObjectId").GetString()==expectedSource);
                    Check(index>=0,"Server offers chosen card");Check(index>0,"Player chooses a different graveyard unit");
                    var picker=(OptionButton)typeof(PlayCardOverlay).GetField("_source",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(overlay)!;
                    picker.Select(index);picker.EmitSignal(OptionButton.SignalName.ItemSelected,(long)index);

                    Check(request is not null && request.Command.SourceObjectId==expectedSource,"Actual selection requests a quote");
                    var submit=(Button)overlay.FindChild("ConfirmPlayCardButton",true,false);Check(submit.Disabled,"No quote cannot authorize play");
                    overlay.ApplyQuote(quote with {RequestId=request!.RequestId});Check(!submit.Disabled,"Current quote authorizes play");
                    var origin=(Label)typeof(PlayCardOverlay).GetField("_origin",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(overlay)!;
                    Check(origin.Text.Contains("减少 3") && origin.Text.Contains("符能"),"Origin explains cost exception");
                    submit.EmitSignal(BaseButton.SignalName.Pressed);
                    command!["promptId"]=quote.PromptId;command["snapshotTick"]=quote.SnapshotTick;
                }
                Check(command is not null,"Production confirmation emits intent");
                File.WriteAllText(Path.Combine(dir,"native-command.json"),JsonSerializer.Serialize(command,json));
                if(OS.GetCmdlineUserArgs().Contains("--visual-proof"))
                {
                    for(var i=0;i<4;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
                    await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                    GetViewport().GetTexture().GetImage().SavePng(Path.Combine(dir,"native.png"));
                }
                RemoveChild(component);component.QueueFree();
            }
            GD.Print("MECHANICAL_INTERACTION_PASS: chosen recycle cost, paid mechanical replay, and decline");GetTree().Quit();
        }
        catch(Exception error){GD.PushError(error.ToString());GetTree().Quit(1);}
    }
    private static void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);}
}

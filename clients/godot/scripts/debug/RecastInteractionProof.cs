using System.Reflection;
using System.Text.Json;
using Godot;
using Riftbound.Contracts;
using Riftbound.GodotClient.Interaction;
using Riftbound.GodotClient.Ui;
namespace Riftbound.GodotClient.Debug;

public partial class RecastInteractionProof : Control
{
    public override async void _Ready()
    {
        try
        {
            GetWindow().Size=new Vector2I(800,800);GetWindow().ContentScaleSize=new Vector2I(800,800);
            var root=OS.GetCmdlineUserArgs().First(a=>a.StartsWith("--evidence="))[11..];
            var json=new JsonSerializerOptions(JsonSerializerDefaults.Web);
            var executed=0;
            foreach(var branch in new[]{"pick-second","pay-power","decline","unit-play","unit-decline","hand-pick","hand-play","hand-decline","hand-recycle","hand-investigator","hand-investigator-decline","hand-scout-continue"})
            {
                var dir=Path.Combine(root,branch);
                if(!Directory.Exists(dir)) continue;
                var unit=branch.StartsWith("unit-");
                var revealed=branch.StartsWith("hand-");
                executed++;
                using var prompt=JsonDocument.Parse(File.ReadAllText(Path.Combine(dir,"prompt.json")));
                Dictionary<string,object?>? command=null;Control component;
                if(branch.EndsWith("decline") || revealed && branch!="hand-play")
                {
                    var view=(Godot.Collections.Dictionary)typeof(Main).GetMethod("BuildPromptView",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[prompt.RootElement])!;
                    var controller=new PromptInteractionController();controller.Load(view);
                    var bar=GD.Load<PackedScene>("res://scenes/components/ActionBar.tscn").Instantiate<ActionBar>();AddChild(bar);component=bar;
                    bar.ShowPrompt(view["message"].AsString(),controller.Actions);
                    bar.ActionSelected+=action=>{Check(controller.SelectAction(action),"Decline action selectable");bar.ShowSelection(controller.Current!,controller.CurrentChoices,controller.CurrentStepLabel,controller.CurrentStepRequired);};
                    var label=controller.Actions.Single(a=>a.Name=="CHOOSE_CARDS").Label;
                    bar.GetNode<HBoxContainer>("%ActionChoices").GetChildren().OfType<Button>().Single(b=>b.Text==label).EmitSignal(BaseButton.SignalName.Pressed);
                    if(revealed && !branch.EndsWith("decline") && branch!="hand-scout-continue")
                    {
                        bar.ChoiceSelected+=(role,id)=>Check(controller.TrySelectChoice(role,id),"Choose through production choice button");
                        var expected=branch=="hand-recycle"?"SPELL":"U";
                        if(!controller.Current!.TargetIds.Contains(expected))
                        {
                            var choiceIndex=controller.CurrentChoices.ToList().FindIndex(c=>c.Id==expected);
                            Check(choiceIndex>=0,"Authoritative choice is available");
                            var choiceButton=bar.GetNode<HBoxContainer>("%StepChoices").GetChildren().OfType<Button>().ElementAt(choiceIndex);
                            choiceButton.EmitSignal(BaseButton.SignalName.Pressed);
                        }
                        Check(controller.Current!.TargetIds.Contains(expected),"Effect controller chooses a revealed unit");
                    }
                    if(revealed)Check(view["message"].AsString().Contains("已公开展示") && !view["message"].AsString().Contains("仅你可见"),"Public reveal has correct visibility label");
                    else Check(controller.Current!.Summary.Contains("放弃再次打出"),"Decline summary is explicit");
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
                        ["visible"]=true,["objectId"]=id,["cardName"]=id=="F"?"菲兹":revealed?"冷血贵族":unit?"警觉的哨兵":branch=="pay-power"?"镜花水月":"冥想",["cardNo"]=id=="F"?"SFD·140/221":revealed?"OGN·208/298":unit?"OGN·096/298":branch=="pay-power"?"UNL-200/219":"OGN·048/298",["imagePath"]="",["zone"]=id=="F"?"BASE":"GRAVEYARD",["owner"]="self"}),"Authoritative graveyard candidate opens");
                    var expectedSource=revealed?"U":unit?"G":branch=="pay-power"?"C":"OTHER";
                    var requirements=candidate.RootElement.GetProperty("metadata").GetProperty("sourceRequirements").EnumerateArray().ToArray();
                    var index=Array.FindIndex(requirements,r=>r.GetProperty("sourceObjectId").GetString()==expectedSource);
                    Check(index>=0,"Server offers chosen card");if(branch=="pick-second")Check(index>0,"Selects a different card from the default");
                    var picker=(OptionButton)typeof(PlayCardOverlay).GetField("_source",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(overlay)!;
                    picker.Select(index);picker.EmitSignal(OptionButton.SignalName.ItemSelected,(long)index);
                    if(branch=="pay-power")Check(overlay.TrySelectTableObject("F"),"Table selects legal target");
                    Check(request is not null && request.Command.SourceObjectId==expectedSource,"Actual selection requests a quote");
                    var submit=(Button)overlay.FindChild("ConfirmPlayCardButton",true,false);Check(submit.Disabled,"No quote cannot authorize play");
                    overlay.ApplyQuote(quote with {RequestId=request!.RequestId});Check(!submit.Disabled,"Current quote authorizes play");
                    var origin=(Label)typeof(PlayCardOverlay).GetField("_origin",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(overlay)!;
                    Check(revealed ? origin.Text.Contains("忽略一切费用") && origin.Text.Contains("不能支付额外费用")
                        : origin.Text.Contains("忽略基础法力") && origin.Text.Contains("符能"),"Origin explains cost exception");
                    if(revealed)Check(request.Command.Destination=="BATTLEFIELD:BF","Owner must use the specified battlefield");
                    if(unit)Check(origin.Text.Contains("额外费用仍需支付"),"Base waiver preserves additional costs");
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
            Check(executed>0,"At least one authoritative scenario is required");
            GD.Print("RECAST_INTERACTION_PASS: production effect-play selection and confirmation");GetTree().Quit();
        }
        catch(Exception error){GD.PushError(error.ToString());GetTree().Quit(1);}
    }
    private static void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);}
}

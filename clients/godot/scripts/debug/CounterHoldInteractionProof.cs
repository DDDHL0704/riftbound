using System.Collections;
using System.Reflection;
using System.Text.Json;
using Godot;
using Riftbound.Contracts;
using Riftbound.GodotClient.Interaction;
using Riftbound.GodotClient.Ui;

namespace Riftbound.GodotClient.Debug;

public partial class CounterHoldInteractionProof : Control
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    public override async void _Ready()
    {
        try
        {
            GetWindow().ContentScaleSize = new Vector2I(560, 950);
            var root = OS.GetCmdlineUserArgs().First(a => a.StartsWith("--evidence="))[11..];
            await Play(root, "counter", counter: true);
            await Play(root, "reinforcements", counter: false);
            using var prompt = JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"ornn-prompt.json")));
            var map = typeof(Main).GetMethod("BuildPromptView", BindingFlags.Static | BindingFlags.NonPublic)!;
            var view = (Godot.Collections.Dictionary)map.Invoke(null,[prompt.RootElement])!;
            Check(view["message"].AsString().Contains("仅你可见"),"Ornn viewed cards have an explicit private prompt");
            var controller = new PromptInteractionController(); controller.Load(view);
            Check(controller.Current is { ActionName:"CHOOSE_CARDS", CanSubmit:true },"Ornn allows optional decline");
            Check(!controller.TrySelectObject("U1"),"A looked-at unit cannot satisfy equipment choice");
            Check(controller.TrySelectObject("E2"),"Can choose the second equipment");
            var candidate = prompt.RootElement.GetProperty("candidates").EnumerateArray().Single(c=>c.GetProperty("action").GetString()=="CHOOSE_CARDS");
            var selectionType=typeof(Main).GetNestedType("PromptSelection",BindingFlags.NonPublic)!;
            var selection=Activator.CreateInstance(selectionType,[null,new[]{"E2"},null,null,Array.Empty<string>()]);
            var build=typeof(Main).GetMethod("CommandFromTemplate",BindingFlags.Static|BindingFlags.NonPublic)!;
            var command=(Dictionary<string,object?>)build.Invoke(null,[candidate,selection,controller.PromptId,controller.SnapshotTick])!;
            Check(((IEnumerable<string>)command["chosenObjectIds"]!).SequenceEqual(["E2"]),"Template keeps chosen equipment");
            File.WriteAllText(Path.Combine(root,"native-ornn-command.json"),JsonSerializer.Serialize(command));
            using var effectPrompt=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"reinforcements-prompt.json")));
            var effectView=(Godot.Collections.Dictionary)map.Invoke(null,[effectPrompt.RootElement])!;
            Check(effectView["message"].AsString().Contains("仅你可见"),"Reinforcements shows the owner's viewed cards");
            GD.Print("COUNTER_HOLD_INTERACTION_PASS: counter repeat targets, Ornn equipment selection, reduced-cost deck play, private prompts");
            GetTree().Quit();
        }
        catch(Exception error){GD.PushError(error.ToString());GetTree().Quit(1);}
    }
    private async Task Play(string root,string scenario,bool counter)
    {
        var json=new JsonSerializerOptions(JsonSerializerDefaults.Web);
        using var candidate=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,scenario+"-candidate.json")));
        var quote=JsonSerializer.Deserialize<PlayCostQuoteDto>(File.ReadAllText(Path.Combine(root,scenario+"-quote.json")),json)!;
        var overlay=new PlayCardOverlay{TableMode=true};AddChild(overlay);
        PlayCostPreviewRequestDto? request=null;Dictionary<string,object?>? command=null;
        overlay.PreviewRequested+=value=>request=value;overlay.Confirmed+=value=>command=value;
        Check(overlay.Open(candidate.RootElement,quote.PromptId,quote.SnapshotTick,_=>null),"Open real backend candidate");
        if(counter)
        {
            var targets=(List<(OptionButton Picker,string[] Ids,bool Required)>)typeof(PlayCardOverlay).GetField("_targets",Private)!.GetValue(overlay)!;
            Check(targets[0].Picker.GetItemText(Array.IndexOf(targets[0].Ids,"S1")) != targets[0].Picker.GetItemText(Array.IndexOf(targets[0].Ids,"S2")), "Identical spell cards remain distinguishable by chain position");
            Select(targets[0].Picker,Array.IndexOf(targets[0].Ids,"S1"));
            var optional=(Dictionary<string,CheckBox>)typeof(PlayCardOverlay).GetField("_optional",Private)!.GetValue(overlay)!;
            optional["ECHO"].ButtonPressed=true;
            var repeats=(IList)typeof(PlayCardOverlay).GetField("_repeats",Private)!.GetValue(overlay)!;
            Check(repeats.Count==1,"One grant creates one repeated execution");
            var entry=repeats[0]!;
            var repeatTargets=(List<(OptionButton Picker,string[] Ids,bool Required)>)entry.GetType().GetField("Targets")!.GetValue(entry)!;
            Select(repeatTargets[0].Picker,Array.IndexOf(repeatTargets[0].Ids,"S2"));
            Check(request!.Command.RepeatChoices![0].TargetObjectIds.SequenceEqual(["S2"]),"Repeat chooses another chain item");
        }
        else
        {
            Check(request!.Command.SourceObjectId=="U","Deck unit comes from server-authorized source list");
            var reason=(Label)typeof(PlayCardOverlay).GetField("_origin",Private)!.GetValue(overlay)!;
            Check(reason.Text.Contains("减少 5"),"Reduced-cost origin is visible");
            Check(quote.Cost!.Mana==0,"Printed four mana is reduced to zero");
        }
        var confirm=(Button)overlay.FindChild("ConfirmPlayCardButton",true,false);
        Check(confirm.Disabled,"Confirmation waits for authoritative quote");
        overlay.ApplyQuote(quote with {RequestId=request!.RequestId,PromptId=request.PromptId,SnapshotTick=request.SnapshotTick});
        Check(!confirm.Disabled,"Matching quote enables confirmation");
        for(var i=0;i<6;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
        Check(confirm.GetGlobalRect().End.Y<=GetViewportRect().End.Y,"Confirmation stays visible");
        var origin=(Label)typeof(PlayCardOverlay).GetField("_origin",Private)!.GetValue(overlay)!;
        var header=origin.GetParent().GetChildren().OfType<Control>().Take(4).Where(c=>c.Visible).ToArray();
        for(var i=1;i<header.Length;i++)
            Check(header[i].GetGlobalRect().Position.Y>=header[i-1].GetGlobalRect().End.Y,"Header, origin and source must not overlap");
        File.WriteAllText(Path.Combine(root,"native-"+scenario+"-layout.json"),JsonSerializer.Serialize(header.Select(c=>new{
            text=c is Label label?label.Text:c.Name.ToString(),x=c.GlobalPosition.X,y=c.GlobalPosition.Y,width=c.Size.X,height=c.Size.Y})));

        if(OS.GetCmdlineUserArgs().Contains("--visual-proof"))
        {
            await ToSignal(GetTree().CreateTimer(0.3), SceneTreeTimer.SignalName.Timeout);
            await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
            RenderingServer.ForceSync();
            RenderingServer.ForceDraw();
            GetViewport().GetTexture().GetImage().SavePng(Path.Combine(root,"native-"+scenario+".png"));
        }
        confirm.EmitSignal(BaseButton.SignalName.Pressed);
        Check(command is not null,"Production confirmation emits a command");
        command!["promptId"]=overlay.PromptId;command["snapshotTick"]=overlay.SnapshotTick;
        File.WriteAllText(Path.Combine(root,"native-"+scenario+"-command.json"),JsonSerializer.Serialize(command,json));
        RemoveChild(overlay);overlay.QueueFree();
    }
    private static void Select(OptionButton picker,int index)
    {Check(index>=0,"Expected choice is legal");picker.Select(index);picker.EmitSignal(OptionButton.SignalName.ItemSelected,(long)index);}
    private static void Check(bool value,string message){if(!value)throw new InvalidOperationException(message);}
}

using System.Reflection;
using System.Text.Json;
using Godot;
using Riftbound.Contracts;
using Riftbound.GodotClient.Ui;
namespace Riftbound.GodotClient.Debug;
public partial class WardCostInteractionProof:Control
{
    public override async void _Ready()
    {
        try {
            GetWindow().ContentScaleSize=new Vector2I(520,850);
            var root=OS.GetCmdlineUserArgs().First(a=>a.StartsWith("--evidence="))[11..];
            var json=new JsonSerializerOptions(JsonSerializerDefaults.Web);
            using var candidate=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"native-candidate.json")));
            var quote=JsonSerializer.Deserialize<PlayCostQuoteDto>(File.ReadAllText(Path.Combine(root,"native-quote.json")),json)!;
            var overlay=new PlayCardOverlay{TableMode=true};AddChild(overlay);
            PlayCostPreviewRequestDto? request=null;Dictionary<string,object?>? command=null;
            overlay.PreviewRequested+=value=>request=value;overlay.Confirmed+=value=>command=value;
            Check(overlay.Open(candidate.RootElement,quote.PromptId,quote.SnapshotTick,id=>new Godot.Collections.Dictionary {
                ["visible"]=true,["objectId"]=id,["cardName"]=id=="CARD"?"焚烧":"法盾目标",["cardNo"]=id=="CARD"?"OGS·003/024":"UNL-104/219",["imagePath"]="",["zone"]=id=="CARD"?"HAND":"BATTLEFIELD",["owner"]=id=="CARD"?"self":"opponent"}),"Server candidate opens");
            Check(overlay.TrySelectTableObject("T"),"Table click selects server target");
            Check(request is not null,"Selection requests authoritative cost");
            var confirm=(Button)overlay.FindChild("ConfirmPlayCardButton",true,false);Check(confirm.Disabled,"Cannot submit before quote");
            var matching=quote with {RequestId=request!.RequestId};
            overlay.ApplyQuote(matching with {RequestId="stale"});Check(confirm.Disabled,"Stale quote rejected");
            overlay.ApplyQuote(matching);Check(!confirm.Disabled,"Current quote enables confirmation");
            var cost=(Label)typeof(PlayCardOverlay).GetField("_cost",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(overlay)!;
            Check(cost.Text.Contains("2 法力") && cost.Text.Contains("任意符能 1") && cost.Text.Contains("法盾费用  +1 符能"),"Ward displayed as power, separate from mana");
            for(var i=0;i<4;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            Check(confirm.GetGlobalRect().End.Y<=GetViewportRect().End.Y,"Confirmation remains visible");
            if(OS.GetCmdlineUserArgs().Contains("--visual-proof")){await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);GetViewport().GetTexture().GetImage().SavePng(Path.Combine(root,"native-ward-cost.png"));}
            confirm.EmitSignal(BaseButton.SignalName.Pressed);Check(command is not null,"Production button emits command");
            command!["promptId"]=quote.PromptId;command["snapshotTick"]=quote.SnapshotTick;
            File.WriteAllText(Path.Combine(root,"native-command.json"),JsonSerializer.Serialize(command,json));
            GD.Print("WARD_COST_INTERACTION_PASS: table target, current quote, generic power label, confirmation command");GetTree().Quit();
        } catch(Exception error){GD.PushError(error.ToString());GetTree().Quit(1);}
    }
    private static void Check(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
}

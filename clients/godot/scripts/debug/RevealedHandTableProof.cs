using System.Reflection;
using System.Text.Json;
using Godot;
using Riftbound.GodotClient.Ui;
namespace Riftbound.GodotClient.Debug;

public partial class RevealedHandTableProof : Control
{
    public override async void _Ready()
    {
        try
        {
            var root=OS.GetCmdlineUserArgs().First(a=>a.StartsWith("--evidence="))[11..];
            GetWindow().Size=new Vector2I(1440,900);GetWindow().ContentScaleSize=new Vector2I(1440,900);
            var main=new Main();
            var catalog=await new OfficialCardCatalogService().LoadSnapshotAsync("res://data/card-catalog.zh-CN.json");
            const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
            typeof(Main).GetField("_officialCatalog",flags)!.SetValue(main,catalog);
            var screen=GD.Load<PackedScene>("res://scenes/screens/MatchScreen.tscn").Instantiate<MatchScreen>();
            AddChild(screen);screen.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            foreach(var stage in new[]{"hand-pick","hand-hidden"})
            {
                using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,stage,"snapshot.json")));
                var snapshot=doc.RootElement;var table=snapshot.GetProperty("table");
                var objects=typeof(Main).GetMethod("VisibleObjectIndex",flags)!.Invoke(main,[snapshot,table]);
                var pending=(Task)typeof(Main).GetMethod("BuildWireTableSectionAsync",flags)!.Invoke(main,[snapshot,table,objects])!;
                await pending;
                var tuple=pending.GetType().GetProperty("Result")!.GetValue(pending)!;
                var section=(Godot.Collections.Dictionary)tuple.GetType().GetField("Item1")!.GetValue(tuple)!;
                screen.RenderSections(new Godot.Collections.Array<Godot.Collections.Dictionary>{section});
                for(var i=0;i<4;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
                var cards=screen.TableLayout.OpponentHand.GetChildren().OfType<OfficialCardView>().ToArray();
                var visible=cards.Select(card=>card.TryGetVisibleCard(out var data)?data["objectId"].AsString():null).OfType<string>().Order().ToArray();
                Check(stage=="hand-pick"?visible.SequenceEqual(new[]{"SPELL","U"}):visible.Length==0,"Render only server-authorized revealed identities");
                Check(cards.Length==(stage=="hand-pick"?2:1),"Restore concealed hand pile after reveal ends");
                if(OS.GetCmdlineUserArgs().Contains("--visual-proof"))
                {
                    await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                    GetViewport().GetTexture().GetImage().SavePng(Path.Combine(root,stage,"table.png"));
                }
                File.WriteAllText(Path.Combine(root,stage,"table-proof.json"),JsonSerializer.Serialize(new{visibleIds=visible,cardViews=cards.Length}));
            }
            main.Free();GD.Print("REVEALED_HAND_TABLE_PASS: public hand cards render and return to concealed pile");GetTree().Quit();
        }
        catch(Exception error){GD.PushError(error.ToString());GetTree().Quit(1);}
    }
    private static void Check(bool value,string message){if(!value)throw new InvalidOperationException(message);}
}

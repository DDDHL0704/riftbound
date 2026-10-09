using System.Reflection;
using System.Text.Json;
using Godot;
using Riftbound.GodotClient.Ui;

namespace Riftbound.GodotClient.Debug;

public partial class InformationPermissionProof : Control
{
    public override async void _Ready()
    {
        try
        {
            var root = OS.GetCmdlineUserArgs().First(a => a.StartsWith("--evidence="))[11..];
            Theme = GD.Load<Theme>("res://assets/InterfaceTheme.tres");
            GetWindow().Size = new Vector2I(1440, 900); GetWindow().ContentScaleSize = new Vector2I(1440, 900);
            var main = new Main();
            var catalog = await new OfficialCardCatalogService().LoadSnapshotAsync("res://data/card-catalog.zh-CN.json");
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(Main).GetField("_officialCatalog", flags)!.SetValue(main, catalog);
            var screen = GD.Load<PackedScene>("res://scenes/screens/MatchScreen.tscn").Instantiate<MatchScreen>();
            AddChild(screen); screen.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            foreach (var stage in new[] { "before", "hand-scout-continue", "granted", "spectator", "expired" })
            {
                typeof(Main).GetField("_authenticatedHandle", flags)!.SetValue(main, stage == "spectator" ? "__spectator__" : "P1");
                using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, stage, "snapshot.json")));
                var snapshot = doc.RootElement; var table = snapshot.GetProperty("table");
                var objects = typeof(Main).GetMethod("VisibleObjectIndex", flags)!.Invoke(main, [snapshot, table]);
                var pending = (Task)typeof(Main).GetMethod("BuildWireTableSectionAsync", flags)!.Invoke(main, [snapshot, table, objects])!;
                await pending;
                var tuple = pending.GetType().GetProperty("Result")!.GetValue(pending)!;
                var section = (Godot.Collections.Dictionary)tuple.GetType().GetField("Item1")!.GetValue(tuple)!;
                screen.RenderSections(new Godot.Collections.Array<Godot.Collections.Dictionary> { section });
                for (var i = 0; i < 4; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                var views = Cards(screen).ToArray();
                var visible = views.Select(c => c.TryGetVisibleCard(out var data) ? data : null).OfType<Godot.Collections.Dictionary>().ToArray();
                var inspected = visible.SingleOrDefault(c => c["objectId"].AsString() == "STANDBY-X");
                Check((inspected is not null) == (stage == "granted"), "Face-down identity visible only to the authorized viewer this turn");
                if (inspected is not null)
                {
                    Check(inspected["isStandby"].AsBool(), "Inspection does not reveal or play the standby");
                    var view = views.Single(c => c.TryGetVisibleCard(out var data) && data["objectId"].AsString() == "STANDBY-X");
                    Check(view.GetParent().GetChildren().OfType<OfficialCardView>().Count() == 1, "Visible face-down card must not create a second hidden pile");
                }
                Check(!visible.Any(c => c["objectId"].AsString() == "SPELL") || stage == "hand-scout-continue", "Hand disclosure is temporary");
                File.WriteAllText(Path.Combine(root, stage, "table-proof.json"), JsonSerializer.Serialize(new { stage, faceDownIdentityVisible = inspected is not null, visibleCount = visible.Length }));
                if (OS.GetCmdlineUserArgs().Contains("--visual-proof"))
                {
                    await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                    GetViewport().GetTexture().GetImage().SavePng(Path.Combine(root, stage, "table.png"));
                }
            }
            using var forged = JsonDocument.Parse("{\"isFaceDown\":true,\"cardNo\":\"UNL-013/219\",\"controllerId\":\"P2\",\"canInspectFaceDown\":true,\"inspectionViewerId\":\"P2\"}");
            Check(!SnapshotCardRef.FromSnapshot("foreign", forged.RootElement, "P1").Visible, "A permission for a different viewer cannot authorize inspection");
            main.Free(); GD.Print("INFORMATION_PERMISSION_PASS: reveal, authorized inspection, spectator redaction and turn expiry"); GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }

    private static IEnumerable<OfficialCardView> Cards(Node node)
    {
        if (node is OfficialCardView card) yield return card;
        foreach (var child in node.GetChildren()) foreach (var found in Cards(child)) yield return found;
    }
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}

using System.Reflection;
using System.Text.Json;
using Godot;
using Riftbound.GodotClient.Ui;

namespace Riftbound.GodotClient.Debug;

public partial class HoldSequenceInteractionProof : Control
{
    public override async void _Ready()
    {
        try
        {
            var root = OS.GetCmdlineUserArgs().First(a => a.StartsWith("--evidence="))[11..];
            var visual = OS.GetCmdlineUserArgs().Contains("--visual-proof");
            var main = new Main();
            var catalog = await new OfficialCardCatalogService().LoadSnapshotAsync("res://data/card-catalog.zh-CN.json");
            typeof(Main).GetField("_officialCatalog", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(main, catalog);
            var screen = GD.Load<PackedScene>("res://scenes/screens/MatchScreen.tscn").Instantiate<MatchScreen>();
            AddChild(screen); screen.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            foreach (var (file, expected) in new[] {
                ("OGN-280-298-1-P1-snapshot.json", "回合开始 · 响应窗口"),
                ("OGN-288-298-3-P1-snapshot.json", "回合开始 · 效果选择"),
                ("SFD-214-221-3-P1-snapshot.json", "回合开始 · 效果支付") })
            {
                using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, file)));
                var json = document.RootElement;
                var table = json.GetProperty("table");
                var objects = typeof(Main).GetMethod("VisibleObjectIndex", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(main, [json, table]);
                var pending = (Task)typeof(Main).GetMethod("BuildWireTableSectionAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(main, [json, table, objects])!;
                await pending;
                var tuple = pending.GetType().GetProperty("Result")!.GetValue(pending)!;
                var section = (Godot.Collections.Dictionary)tuple.GetType().GetField("Item1")!.GetValue(tuple)!;
                screen.RenderSections(new Godot.Collections.Array<Godot.Collections.Dictionary> { section });
                screen.SetTurnStatus("", "", true, useWindowDetail: true);
                Check(screen.TableLayout.PhaseTitle.Text == expected, $"Expected {expected}, got {screen.TableLayout.PhaseTitle.Text}");
                foreach (var size in new[] { new Vector2(1280, 800), new Vector2(1440, 900), new Vector2(1920, 1080) })
                {
                    if (!visual) { screen.SetAnchorsAndOffsetsPreset(LayoutPreset.TopLeft); screen.Size = size; }
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    Check(screen.TableLayout.PhaseTitle.Visible, "Turn start timing must remain visible");
                    foreach (var field in screen.TableLayout.Battlefields)
                        Check(field.Panel.GetGlobalRect().End.Y <= screen.TableLayout.BaseZone.GetGlobalRect().Position.Y,
                            "Phase window must not cover the battlefield");
                }
                if (visual)
                {
                    await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                    GetViewport().GetTexture().GetImage().SavePng(Path.Combine(root, file.Replace("-snapshot.json", "-window.png")));
                }
            }
            GD.Print("HOLD_SEQUENCE_INTERACTION_PASS: real server snapshots, response/choice/payment labels, three window sizes");
            main.Free(); GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}

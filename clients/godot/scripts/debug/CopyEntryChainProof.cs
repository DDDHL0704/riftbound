using System.Reflection;
using System.Text.Json;
using Godot;
using Riftbound.GodotClient.Ui;

namespace Riftbound.GodotClient.Debug;

public partial class CopyEntryChainProof : Control
{
    public override async void _Ready()
    {
        try
        {
            var root = OS.GetCmdlineUserArgs().Single(a => a.StartsWith("--evidence="))[11..];
            var main = new Main();
            var catalog = await new OfficialCardCatalogService().LoadSnapshotAsync("res://data/card-catalog.zh-CN.json");
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(Main).GetField("_officialCatalog", flags)!.SetValue(main, catalog);
            var screen = GD.Load<PackedScene>("res://scenes/screens/MatchScreen.tscn").Instantiate<MatchScreen>();
            AddChild(screen);
            foreach (var (kind, label) in new[] { ("copy", "内嵌复制"), ("entry", "进场眩晕与移动限制") })
            foreach (var player in new[] { "P1", "P2" })
            {
                using var snapshot = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, kind + "-" + player + "-snapshot.json")));
                var json = snapshot.RootElement;
                var table = json.GetProperty("table");
                var objects = typeof(Main).GetMethod("VisibleObjectIndex", flags)!.Invoke(main, [json, table]);
                var pending = (Task)typeof(Main).GetMethod("BuildWireTableSectionAsync", flags)!.Invoke(main, [json, table, objects])!;
                await pending;
                var tuple = pending.GetType().GetProperty("Result")!.GetValue(pending)!;
                var section = (Godot.Collections.Dictionary)tuple.GetType().GetField("Item1")!.GetValue(tuple)!;
                screen.RenderSections(new Godot.Collections.Array<Godot.Collections.Dictionary> { section });
                screen.SetTurnStatus("", "", true, useWindowDetail: true);
                foreach (var size in new[] { new Vector2(1280, 800), new Vector2(1440, 900), new Vector2(1920, 1080) })
                {
                    screen.SetAnchorsAndOffsetsPreset(LayoutPreset.TopLeft);
                    screen.Size = size;
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    Check(screen.TableLayout.ChainPanel.Visible, "Response chain must be visible");
                    Check(screen.TableLayout.Chain.GetChildren().SelectMany(n => n.GetChildren()).OfType<Button>()
                        .Any(b => b.Text.Contains(label)), "Production chain must render server ability label: " + label);
                    Check(screen.TableLayout.PhaseTitle.Text.Contains("响应窗口"), "Separate trigger must expose response timing");
                    foreach (var battlefield in screen.TableLayout.Battlefields)
                        Check(battlefield.Panel.GetGlobalRect().End.Y <= screen.TableLayout.BaseZone.GetGlobalRect().Position.Y,
                            "Chain must not overlap battlefield units");
                }
            }
            main.Free();
            GD.Print("COPY_ENTRY_CHAIN_PASS: two authoritative response windows, both viewers, three sizes");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

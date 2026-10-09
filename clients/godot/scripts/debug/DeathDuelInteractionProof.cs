using System.Reflection;
using System.Text.Json;
using Godot;
using Riftbound.Contracts;
using Riftbound.GodotClient.Interaction;
using Riftbound.GodotClient.Ui;

namespace Riftbound.GodotClient.Debug;

public partial class DeathDuelInteractionProof : Control
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
            AddChild(screen);
            screen.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            var sizes = new[] { new Vector2(1280, 800), new Vector2(1440, 900), new Vector2(1920, 1080) };
            foreach (var (kind, expected) in new[] { ("focus", "法术对决 · 出牌窗口"), ("duel-stack", "法术对决 · 响应窗口"),
                ("death", "法术与技能 · 响应窗口"), ("battle-response", "战斗 · 响应窗口") })
            {
                using var snapshot = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, kind + "-P2-snapshot.json")));
                var json = snapshot.RootElement;
                var table = json.GetProperty("table");
                var objects = typeof(Main).GetMethod("VisibleObjectIndex", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(main, [json, table]);
                var pending = (Task)typeof(Main).GetMethod("BuildWireTableSectionAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(main, [json, table, objects])!;
                await pending;
                var tuple = pending.GetType().GetProperty("Result")!.GetValue(pending)!;
                var section = (Godot.Collections.Dictionary)tuple.GetType().GetField("Item1")!.GetValue(tuple)!;
                screen.RenderSections(new Godot.Collections.Array<Godot.Collections.Dictionary> { section });
                screen.SetTurnStatus("", "", true, useWindowDetail: true);
                using var stagePrompt = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, kind + "-P2-prompt.json")));
                var promptView = (Godot.Collections.Dictionary)typeof(Main).GetMethod("BuildPromptView", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [stagePrompt.RootElement])!;
                var interactions = new PromptInteractionController();
                interactions.Load(promptView);
                screen.ActionBar.ShowPrompt(screen.TableLayout.TurnDetail.Text, interactions.Actions);
                Check(screen.TableLayout.PhaseTitle.Text == expected, "Each server window needs an explicit stage label: " + kind);
                Check(screen.TableLayout.PassConsequence.Visible, "Passing consequence must remain visible");
                Check(screen.TableLayout.ChainPanel.Visible, "The response window must show an empty chain too");
                foreach (var size in sizes)
                {
                    if (!visual) { screen.SetAnchorsAndOffsetsPreset(LayoutPreset.TopLeft); screen.Size = size; }
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    Check(screen.TableLayout.TurnDetail.GetGlobalRect().End.Y <= screen.TableLayout.Rail.GetGlobalRect().End.Y, "Timing detail must fit the window");
                    foreach (var battlefield in screen.TableLayout.Battlefields)
                        Check(battlefield.Panel.GetGlobalRect().End.Y <= screen.TableLayout.BaseZone.GetGlobalRect().Position.Y,
                            "The timing window cannot cover battlefield units");
                }
                if (visual)
                {
                    await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                    GetViewport().GetTexture().GetImage().SavePng(Path.Combine(root, "window-" + kind + ".png"));
                }
            }
            using var prompt = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "battle-response-P2-prompt.json")));
            var p = prompt.RootElement;
            var candidate = p.GetProperty("candidates").EnumerateArray().Single(c => c.GetProperty("action").GetString() == "PLAY_CARD");
            var overlay = new PlayCardOverlay { TableMode = true };
            screen.ComposerHost.AddChild(overlay);
            screen.SetComposerVisible(true);
            PlayCostPreviewRequestDto? request = null;
            overlay.PreviewRequested += value => request = value;
            Check(overlay.Open(candidate, p.GetProperty("promptId").GetString()!, p.GetProperty("snapshotTick").GetInt64(), _ => null, "SPELL"), "Defender's reaction must open from actual server candidates");
            Check(!overlay.TrySelectTableObject("HIDDEN-OPPONENT-CARD"), "No hidden target may be selected");
            Check(overlay.TrySelectTableObject("A"), "The attacking unit must be selectable on the table");
            Check(request is not null && request.Command.SourceObjectId == "SPELL" && request.Command.TargetObjectIds.SequenceEqual(["A"]), "The chosen reaction and target must reach server preview");
            File.WriteAllText(Path.Combine(root, "native-response-preview.json"), JsonSerializer.Serialize(request));
            Check(screen.TableLayout.PhaseTitle.Visible && screen.TableLayout.PassConsequence.Visible,
                "Opening the card composer must retain timing context");
            screen.SetConnectionStatus(false, true);
            Check(screen.TableLayout.TurnHeadline.Text == "正在恢复连接", "Disconnected status must override action instructions");
            GD.Print("DEATH_DUEL_INTERACTION_PASS: four real server windows, three sizes, defender reaction selection, hidden target and reconnect guards");
            main.Free();
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}

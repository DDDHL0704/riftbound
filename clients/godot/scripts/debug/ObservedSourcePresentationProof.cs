using System.Reflection;
using System.Text.Json;
using Godot;
using Riftbound.GodotClient.Interaction;
using Riftbound.GodotClient.Ui;

namespace Riftbound.GodotClient.Debug;

// Exercises the production prompt mapping and Main's real preview resolver.
// Inputs are projections from an ordinary opening, never injected live matches.
public partial class ObservedSourcePresentationProof : Control
{
    private readonly List<string> _errors = [];

    public override async void _Ready()
    {
        try
        {
            var args = OS.GetCmdlineUserArgs();
            var folder = args.Single(arg => arg.StartsWith("--evidence="))[11..];
            var kind = args.FirstOrDefault(arg => arg.StartsWith("--audit-case="))?[13..] ?? "all";
            var held = args.Contains("--hold");
            GetWindow().Title = "Riftbound observed source UI proof";
            RenderingServer.SetDefaultClearColor(MinimalTheme.AppBackground);
            var catalog = await new OfficialCardCatalogService().LoadSnapshotAsync("res://data/card-catalog.zh-CN.json");
            using var index = JsonDocument.Parse(File.ReadAllText(Path.Combine(folder, "index.json")));
            var count = 0;
            foreach (var row in index.RootElement.EnumerateArray())
            {
                var name = row.GetProperty("name").GetString()!;
                var branchKind = row.GetProperty("kind").GetString()!;
                if (kind != "all" && branchKind != kind) continue;
                if (held && !name.StartsWith("unl-")) continue;
                using var prompt = JsonDocument.Parse(File.ReadAllText(Path.Combine(folder, name, "P1-prompt.json")));
                using var snapshot = JsonDocument.Parse(File.ReadAllText(Path.Combine(folder, name, "P1-snapshot.json")));
                using var opponent = JsonDocument.Parse(File.ReadAllText(Path.Combine(folder, name, "P2-prompt.json")));
                var view = (Godot.Collections.Dictionary)Method("BuildPromptView").Invoke(null, [prompt.RootElement])!;
                if (branchKind == "banish")
                {
                    var controller = new PromptInteractionController(); controller.Load(view);
                    var action = controller.Actions.Single(option => option.Name == "PAY_COST");
                    Check(action.Label == "选择是否放逐", "Observed banish action must describe banishing, not destruction.");
                    var phase = MatchPhasePresentation.Build(snapshot.RootElement, "P1", id => id);
                    var expectedKind = row.TryGetProperty("expectedObservationKind", out var expectedDisclosure)
                        ? expectedDisclosure.GetString()! : "LOOK";
                    Check(snapshot.RootElement.GetProperty("timing").GetProperty("pendingPayment")
                        .TryGetProperty("deckObservationKind", out var disclosure) && disclosure.ValueKind == JsonValueKind.String
                        && disclosure.GetString() == expectedKind, "Actual disclosure must match the independent ordinary-opening branch.");
                    var publicObservation = expectedKind == "REVEAL";
                    Check(phase["title"].AsString() == (publicObservation ? "展示牌库 · 放逐选择" : "查看牌库 · 放逐选择"), "Observed banish phase must match actual public or private disclosure.");
                    Check(phase["detail"].AsString().Contains("向双方展示") == publicObservation, "Only a public observation may describe disclosure to both players.");
                    Check(phase["detail"].AsString().Contains("原效果完成后"), "Explain the deferred play opportunity.");
                    var bar = GD.Load<PackedScene>("res://scenes/components/ActionBar.tscn").Instantiate<ActionBar>();
                    AddChild(bar); bar.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomWide); bar.OffsetTop = -96;
                    bar.ShowPrompt(view["message"].AsString(), controller.Actions);
                    bar.ActionSelected += id => controller.SelectAction(id);
                    bar.GetNode<HBoxContainer>("%ActionChoices").GetChildren().OfType<Button>()
                        .Single(button => button.Text == action.Label).EmitSignal(BaseButton.SignalName.Pressed);
                    Check(controller.Current?.ActionName == "PAY_COST", "Real action button keeps the server payment template.");
                    Check(controller.CurrentChoices.Any(choice => choice.Id == "BANISH")
                        && controller.CurrentChoices.Any(choice => choice.Id == "KEEP"), "Preserve both actual server options.");
                    Check(!opponent.RootElement.GetProperty("candidates").EnumerateArray()
                        .Any(candidate => candidate.GetProperty("action").GetString() == "PAY_COST" && candidate.GetProperty("enabled").GetBoolean()), "Opponent cannot submit the private replacement.");
                    if (held)
                    {
                        var title = new Label { Text = phase["title"].AsString(), Position = new Vector2(1040, 110), Size = new Vector2(235, 35) };
                        AddChild(title);
                        var detail = new Label { Text = phase["detail"].AsString(), Position = new Vector2(1040, 150), Size = new Vector2(235, 160), AutowrapMode = TextServer.AutowrapMode.WordSmart };
                        AddChild(detail); break;
                    }
                    bar.Free();
                }
                else
                {
                    var source = row.GetProperty("sourceId").GetString()!;
                    var overlay = new PlayCardOverlay { TableMode = true, Position = new Vector2(1035, 110), Size = new Vector2(240, 600) };
                    AddChild(overlay);
                    var main = new Main();
                    Field("_officialCatalog").SetValue(main, catalog);
                    Field("_lastAppliedPromptView").SetValue(main, view);
                    Field("_playCardOverlay").SetValue(main, overlay);
                    Check((bool)Method("TryOpenPlayCard").Invoke(main, [source])!, "Actual Main must open the server-authorized STACK source.");
                    var preview = (OfficialCardView)typeof(PlayCardOverlay).GetField("_preview", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(overlay)!;
                    Check(preview.TryGetVisibleCard(out var card) && card["cardNo"].AsString() == "OGN·194/298"
                        && card["objectId"].AsString() == source && card["cardName"].AsString() == catalog["OGN·194/298"].CardName,
                        "Main must render the exact observed Nocturne rather than an empty CARD placeholder.");
                    var resolver = (Func<string, Godot.Collections.Dictionary?>)typeof(PlayCardOverlay)
                        .GetField("_cardView", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(overlay)!;
                    Check(resolver("UNOBSERVED-PRIVATE-CARD") is null, "Catalog knowledge cannot reveal an unauthorized source object.");
                    Check(!(bool)Method("TryOpenPlayCard").Invoke(main, ["UNOBSERVED-PRIVATE-CARD"])!, "A source absent from the actual requirement cannot open a play.");
                    Check((bool)Method("TryOpenPlayCard").Invoke(main, [source])!, "Reopen the original authorized source.");
                    Dictionary<string, object?>? command = null; overlay.Confirmed += value => command = value;
                    var confirm = (Button)typeof(PlayCardOverlay).GetField("_confirm", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(overlay)!;
                    Check(!confirm.Disabled, "The actual server resource choices permit confirmation.");
                    if (!held) confirm.EmitSignal(BaseButton.SignalName.Pressed);
                    if (!held) Check(command is not null && command["sourceObjectId"] as string == source
                        && command["cardNo"] as string == "OGN·194/298"
                        && overlay.PromptId == prompt.RootElement.GetProperty("promptId").GetString()
                        && overlay.SnapshotTick == prompt.RootElement.GetProperty("snapshotTick").GetInt64(), "Real confirm preserves exact card, source and prompt envelope.");
                    Field("_lastAppliedPromptView").SetValue(main, Method("BuildPromptView").Invoke(null, [opponent.RootElement]));
                    Check(!(bool)Method("TryOpenPlayCard").Invoke(main, [source])!, "The opponent's actual prompt cannot authorize that private source.");
                    if (held) break;
                    overlay.Free(); main.Free();
                }
                count++;
            }
            foreach (var error in _errors) GD.Print("OBSERVED_SOURCE_UI_FAILURE: " + error);
            GD.Print($"OBSERVED_SOURCE_PRESENTATION_{(_errors.Count == 0 ? "PASS" : "FAIL")}: {count} ordinary projected production-control cases, {_errors.Count} failures; live native acceptance separate");
            if (!held) GetTree().Quit(_errors.Count == 0 ? 0 : 1);
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }

    private void Check(bool condition, string message) { if (!condition) _errors.Add(message); }
    private static MethodInfo Method(string name) => typeof(Main).GetMethod(name, BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static FieldInfo Field(string name) => typeof(Main).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!;
}

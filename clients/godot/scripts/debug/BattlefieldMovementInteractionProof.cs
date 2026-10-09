using System.Reflection;
using System.Text.Json;
using Godot;
using Riftbound.GodotClient.Ui;

namespace Riftbound.GodotClient.Debug;

public partial class BattlefieldMovementInteractionProof : Control
{
    public override async void _Ready()
    {
        try
        {
            var root = OS.GetCmdlineUserArgs().First(a => a.StartsWith("--evidence="))[11..];
            using var snapshot = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "after-P2-snapshot.json")));
            using var prompt = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "after-P2-prompt.json")));
            var main = new Main();
            var catalog = await new OfficialCardCatalogService().LoadSnapshotAsync("res://data/card-catalog.zh-CN.json");
            typeof(Main).GetField("_officialCatalog", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(main, catalog);
            var objects = typeof(Main).GetMethod("VisibleObjectIndex", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(main,
                [snapshot.RootElement, snapshot.RootElement.GetProperty("table")]);
            var pending = (Task)typeof(Main).GetMethod("BuildWireTableSectionAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(main,
                [snapshot.RootElement, snapshot.RootElement.GetProperty("table"), objects])!;
            await pending;
            var result = pending.GetType().GetProperty("Result")!.GetValue(pending)!;
            var section = (Godot.Collections.Dictionary)result.GetType().GetField("Item1")!.GetValue(result)!;
            var screen = GD.Load<PackedScene>("res://scenes/screens/MatchScreen.tscn").Instantiate<MatchScreen>();
            AddChild(screen); screen.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            screen.RenderSections(new Godot.Collections.Array<Godot.Collections.Dictionary> { section });
            var candidate = prompt.RootElement.GetProperty("candidates").EnumerateArray().Single(c => c.GetProperty("action").GetString() == "MOVE_UNIT");
            var source = candidate.GetProperty("sources")[0].GetProperty("id").GetString()!;
            var destination = candidate.GetProperty("metadata").GetProperty("sourceRequirements").EnumerateArray()
                .Single(r => r.GetProperty("mode").GetString() == "ROAM").GetProperty("destinationChoices")[0].GetProperty("id").GetString()!;
            var overlay = new MovementOverlay { TableMode = true };
            screen.ComposerHost.AddChild(overlay); screen.SetComposerVisible(true);
            var p = prompt.RootElement;
            var promptId = p.GetProperty("promptId").GetString()!;
            var tick = p.GetProperty("snapshotTick").GetInt64();
            var own = section["lanes"].As<Godot.Collections.Array<Godot.Collections.Dictionary>>()
                .SelectMany(l => l["selfUnits"].As<Godot.Collections.Array<Godot.Collections.Dictionary>>()).ToArray();
            Check(overlay.Open(candidate, promptId, tick, id => own.FirstOrDefault(c => c["objectId"].AsString() == id), label => (string)typeof(Main).GetMethod("MovementDestinationLabel", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(main, [label])!
            , source), "Current real prompt must open in the production movement composer");
            Check(overlay.TableDestinations.Contains(destination), "Zaun must be a real available destination");
            Check(!overlay.TrySelectTableDestination("BATTLEFIELD:FORGED"), "Unknown destination must be rejected");
            Check(overlay.TrySelectTableDestination(destination), "The battlefield can be selected on the table");
            Check(overlay.TableSelectedObjects.Contains(source), "Changing destination preserves the legal selected unit");
            var destinations = overlay.FindChildren("*", "OptionButton", true, false).OfType<OptionButton>().Single();
            Check(destinations.GetItemText(destinations.Selected) == "祖安地沟", "Destination label must show the official name, never an internal object ID");
            Check(overlay.FindChildren("*", "CheckBox", true, false).OfType<CheckBox>().Any(c => c.Text.Contains("游走")), "Granted movement must be visible");
            screen.SetDestinationChoices(overlay.TableDestinations, overlay.TableDestination);
            foreach (var id in overlay.TableSources) screen.SetObjectState(id, OfficialCardVisualState.Selectable);
            foreach (var id in overlay.TableSelectedObjects) screen.SetObjectState(id, OfficialCardVisualState.Selected);
            screen.SetSelectionLinks(overlay.TableSelectedObjects, overlay.TableDestination);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (OS.GetCmdlineUserArgs().Contains("--visual-proof"))
            {
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                GetViewport().GetTexture().GetImage().SavePng(Path.Combine(root, "native-wind-hill-movement.png"));
            }
            var calls = 0;
            overlay.Confirmed += payload =>
            {
                calls++;
                Check(!payload.ContainsKey("origin"), "Native group payload must not override authoritative per-unit origins");
                Check((string)payload["destination"]! == destination, "Confirmed payload preserves the selected battlefield");
                // Capture the production composer payload unchanged, adding the same
                // prompt identity as Main.SubmitPromptPayloadAsync.
                payload["promptId"] = promptId;
                payload["snapshotTick"] = tick;
                File.WriteAllText(Path.Combine(root, "native-movement-command.json"), JsonSerializer.Serialize(payload));
            };
            var confirm = overlay.FindChild("ConfirmMovementButton", true, false) as Button;
            Check(confirm is not null && !confirm.Disabled, "Move can be confirmed");
            confirm!.EmitSignal(Button.SignalName.Pressed); confirm.EmitSignal(Button.SignalName.Pressed);
            Check(calls == 1 && overlay.IsSubmitting, "Double clicks must submit once");
            overlay.ApplyReceipt(promptId, tick, false, "测试拒绝");
            Check(overlay.Visible && overlay.TableSelectedObjects.Contains(source), "Rejected intent preserves selection");
            GD.Print("BATTLEFIELD_MOVEMENT_PASS: real tick-46 snapshot and prompt, granted Roam label, Zaun selection, unknown destination, duplicate-click and receipt guards");
            main.Free();
            GetTree().Quit(0);
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}

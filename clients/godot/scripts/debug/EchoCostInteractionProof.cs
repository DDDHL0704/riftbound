using System.Reflection;
using System.Text.Json;
using Godot;
using Riftbound.Contracts;
using Riftbound.GodotClient.Ui;

namespace Riftbound.GodotClient.Debug;

// Uses the backend candidate and quote exported by OfficialEchoCostTests.
public partial class EchoCostInteractionProof : Control
{
    public override async void _Ready()
    {
        try
        {
            GetWindow().ContentScaleSize = new Vector2I(520, 850);
            var root = OS.GetCmdlineUserArgs().First(a => a.StartsWith("--evidence="))[11..];
            var visual = OS.GetCmdlineUserArgs().Contains("--visual-proof");
            var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
            using var candidate = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "native-candidate.json")));
            var quote = JsonSerializer.Deserialize<PlayCostQuoteDto>(File.ReadAllText(Path.Combine(root, "native-quote.json")), json)!;
            var overlay = new PlayCardOverlay { TableMode = true };
            AddChild(overlay);
            PlayCostPreviewRequestDto? request = null;
            Dictionary<string, object?>? command = null;
            overlay.PreviewRequested += value => request = value;
            overlay.Confirmed += value => command = value;
            Check(overlay.Open(candidate.RootElement, "ECHO-PROOF", 1, id => id == "CARD" ? new Godot.Collections.Dictionary
            {
                ["visible"] = true, ["objectId"] = "CARD", ["cardName"] = "危险温度", ["cardNo"] = "SFD·182/221",
                ["imagePath"] = "", ["zone"] = "HAND", ["owner"] = "self"
            } : null), "Candidate must open");
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var checks = (Dictionary<string, CheckBox>)typeof(PlayCardOverlay).GetField("_optional", flags)!.GetValue(overlay)!;
            Check(checks["ECHO"].Text.Contains("符能"), "Printed Echo must show power");
            Check(checks["ECHO:GRANTED:1"].Text.Contains("符能"), "Granted Echo must show power");
            checks["ECHO"].ButtonPressed = true;
            checks["ECHO:GRANTED:1"].ButtonPressed = true;
            Check(request!.Command.OptionalCosts!.Count == 2, "Independent Echo choices must coexist");
            var confirm = (Button)overlay.FindChild("ConfirmPlayCardButton", true, false);
            Check(confirm.Disabled, "Selection must await server quote");
            overlay.ApplyQuote(quote with { RequestId = request.RequestId, PromptId = request.PromptId, SnapshotTick = request.SnapshotTick });
            Check(!confirm.Disabled, "Matching authoritative quote must enable confirmation");
            var cost = (Label)typeof(PlayCardOverlay).GetField("_cost", flags)!.GetValue(overlay)!;
            Check(cost.Text.Contains("3"), "Total cost must be visible");
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Check(confirm.GetGlobalRect().End.Y <= GetViewportRect().End.Y, "Confirm button must remain in the window");
            if (visual)
            {
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                GetViewport().GetTexture().GetImage().SavePng(Path.Combine(root, "native-echo-cost.png"));
            }
            confirm.EmitSignal(BaseButton.SignalName.Pressed);
            Check(command is not null, "Confirmation must emit command");
            File.WriteAllText(Path.Combine(root, "native-command.json"), JsonSerializer.Serialize(command, json));
            GD.Print("ECHO_COST_INTERACTION_PASS: backend candidate and quote, independent options, visible power, submitted command");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}

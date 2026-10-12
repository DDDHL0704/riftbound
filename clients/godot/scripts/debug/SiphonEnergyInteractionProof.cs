using System.Reflection;
using System.Text.Json;
using Godot;
using Riftbound.Contracts;
using Riftbound.GodotClient.Ui;

namespace Riftbound.GodotClient.Debug;

public partial class SiphonEnergyInteractionProof : Control
{
    public override void _Ready()
    {
        try {
            var root = OS.GetCmdlineUserArgs().Single(a => a.StartsWith("--evidence="))[11..];
            var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
            foreach (var branch in new[] { "aura-floor", "enemy-only" }) {
                var dir = Path.Combine(root, branch);
                using var prompt = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, "prompt.json")));
                var p = prompt.RootElement;
                var candidate = p.GetProperty("candidates").EnumerateArray().Single(c => c.GetProperty("action").GetString() == "PLAY_CARD");
                var overlay = new PlayCardOverlay { TableMode = true }; AddChild(overlay);
                PlayCostPreviewRequestDto? request = null; overlay.PreviewRequested += value => request = value;
                Check(overlay.Open(candidate, p.GetProperty("promptId").GetString()!, p.GetProperty("snapshotTick").GetInt64(), _ => null, "AOE"), "Open production composer");
                Check(!overlay.TrySelectTableObject("HIDDEN-OPPONENT-CARD"), "Reject hidden object");
                Check(!overlay.TrySelectTableObject("D"), "A unit cannot replace the battlefield target");
                string[] targets = [branch == "enemy-only" ? "BF2" : "BF"];
                Check(overlay.TrySelectTableObject(targets[0]), "Select the required battlefield");
                Check(request is not null && request.Command.TargetObjectIds.SequenceEqual(targets), "Preview preserves battlefield identity");
                File.WriteAllText(Path.Combine(dir, "preview.json"), JsonSerializer.Serialize(request, json));
                var quote = JsonSerializer.Deserialize<PlayCostQuoteDto>(File.ReadAllText(Path.Combine(dir, "quote.json")), json)!;
                overlay.ApplyQuote(quote with { RequestId = request!.RequestId });
                Dictionary<string, object?>? command = null;
                overlay.Confirmed += value => { command = value; command["promptId"] = overlay.PromptId; command["snapshotTick"] = overlay.SnapshotTick; };
                var submit = (Button)typeof(PlayCardOverlay).GetField("_confirm", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(overlay)!;
                Check(!submit.Disabled, "Quote enables confirmation"); submit.EmitSignal(BaseButton.SignalName.Pressed);
                Check(command is not null, "Production composer emits command");
                File.WriteAllText(Path.Combine(dir, "command.json"), JsonSerializer.Serialize(command, json)); overlay.Free();
            }
            GD.Print("SIPHON_ENERGY_PASS: two production play commands"); GetTree().Quit();
        } catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }

    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}

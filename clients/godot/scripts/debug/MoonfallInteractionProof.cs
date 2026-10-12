using System.Reflection;
using System.Text.Json;
using Godot;
using Riftbound.Contracts;
using Riftbound.GodotClient.Ui;

namespace Riftbound.GodotClient.Debug;

public partial class MoonfallInteractionProof : Control
{
    public override void _Ready()
    {
        try {
            var root = OS.GetCmdlineUserArgs().Single(a => a.StartsWith("--evidence="))[11..];
            var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
            foreach (var branch in new[] { "no-move", "other-field", "base" }) {
                var dir = Path.Combine(root, branch);
                using var prompt = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, "prompt.json")));
                var p = prompt.RootElement;
                var candidate = p.GetProperty("candidates").EnumerateArray().Single(c => c.GetProperty("action").GetString() == "PLAY_CARD");
                var overlay = new PlayCardOverlay { TableMode = true }; AddChild(overlay);
                PlayCostPreviewRequestDto? request = null; overlay.PreviewRequested += value => request = value;
                Check(overlay.Open(candidate, p.GetProperty("promptId").GetString()!, p.GetProperty("snapshotTick").GetInt64(), _ => null, "AOE"), "Open production composer");
                Check(!overlay.TrySelectTableObject("HIDDEN-OPPONENT-CARD"), "Reject hidden object");
                Check(!overlay.TrySelectTableObject("BF2"), "Reject battlefield without friendly units");
                Check(overlay.TrySelectTableObject("BF"), "Select friendly occupied battlefield");
                Check(request is not null && request.Command.TargetObjectIds.SequenceEqual(["BF"]), "Optional move may be declined");
                if (branch != "no-move") {
                    Check(overlay.TrySelectTableObject(branch == "base" ? "D3" : "D2"), "Choose optional enemy");
                }
                string[] targets = branch == "no-move" ? ["BF"] : ["BF", branch == "base" ? "D3" : "D2"];
                Check(request is not null && request.Command.TargetObjectIds.SequenceEqual(targets), "Preview preserves target roles");
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
            GD.Print("MOONFALL_PASS: three production play commands"); GetTree().Quit();
        } catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }

    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
